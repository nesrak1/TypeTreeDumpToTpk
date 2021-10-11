using AssetsTools.NET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TypeTreeDumpToTpk.Json;

namespace TypeTreeDumpToTpk
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("TypeTreeDumpToTpk");

            Console.WriteLine("downloading type tree dump repository...");
            ZipUtil.DownloadAndUnpackZip("https://github.com/ds5678/TypeTreeDumps/archive/refs/heads/main.zip", "TypeTreeDumps");
            Console.WriteLine("creating cldbs...");
            
            string repoDir = Path.Combine("TypeTreeDumps", "TypeTreeDumps-main");
            string infoJsonDir = Path.Combine(repoDir, "InfoJson");
            
            string cldbDir = "CldbDumps";
            Directory.CreateDirectory(cldbDir);
            
            foreach (string file in Directory.EnumerateFiles(infoJsonDir))
            {
                try
                {
                    JsonTextReader r = new JsonTextReader(new StreamReader(file));
                    JsonSerializer deserializer = new JsonSerializer();
                    UnityInfo inf = (UnityInfo)deserializer.Deserialize(r, typeof(UnityInfo));
            
                    Console.WriteLine($"converting {inf.Version}...");
            
                    ClassDatabaseFile cldbEditor = ConvertUnityInfoToCldb(inf, true);
                    using (FileStream fsEditor = File.OpenWrite(Path.Combine(cldbDir, inf.Version + "_editor.dat")))
                    {
                        cldbEditor.Write(new AssetsFileWriter(fsEditor), 0, 0);
                    }
            
                    ClassDatabaseFile cldbRelease = ConvertUnityInfoToCldb(inf, false);
                    using (FileStream fsRelease = File.OpenWrite(Path.Combine(cldbDir, inf.Version + "_release.dat")))
                    {
                        cldbRelease.Write(new AssetsFileWriter(fsRelease), 0, 0);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error converting {file}:\n{ex.Message}");
                }
            }
            
            Console.WriteLine("building editor tpk...");
            BuildTpk(cldbDir, "_editor");

            Console.WriteLine("building release tpk...");
            BuildTpk(cldbDir, "_release");
        }

        static ClassDatabaseFile ConvertUnityInfoToCldb(UnityInfo inf, bool editor)
        {
            ClassDatabaseFile cldb = new ClassDatabaseFile();

            string unityVerFul = inf.Version;
            string[] unityVerSplit = unityVerFul.Split('.');
            string unityVerMajor = unityVerSplit[0];
            string unityVerMinor = unityVerSplit[1];
            string unityVerReg = $"{unityVerMajor}.{unityVerMinor}.*";

            //Dictionary<string, uint> gblStrLookup = new Dictionary<string, uint>();
            Dictionary<string, uint> strLookup = new Dictionary<string, uint>();
            Dictionary<string, int> classLookup = new Dictionary<string, int>();

            ////global strings (should be close to Type_0D.strTable)
            //foreach (UnityString uStr in inf.Strings)
            //{
            //    string str = uStr.String + '\0';
            //    gblStrLookup[uStr.String] = uStr.Index;
            //}

            //className -> classId
            foreach (UnityClass uClass in inf.Classes)
            {
                classLookup[uClass.Name] = uClass.TypeID;
            }

            //class strings
            //we iterate the classes twice to get a list of used strings
            //then we can point to positions in the string table
            HashSet<string> usedStrList = new HashSet<string>();
            foreach (UnityClass uClass in inf.Classes)
            {
                if (!usedStrList.Contains(uClass.Name))
                    usedStrList.Add(uClass.Name);

                UnityNode uNode;
                if (editor)
                    uNode = uClass.EditorRootNode;
                else
                    uNode = uClass.ReleaseRootNode;

                if (uNode != null)
                    IterateClassStrings(uNode, usedStrList);
            }

            List<string> orderedStrList = usedStrList.ToList();
            orderedStrList.Sort();

            int strTablePos = 0;
            string strTableStr = "";
            foreach (string str in orderedStrList)
            {
                strLookup[str] = (uint)strTablePos;
                strTableStr += str + '\0';
                strTablePos += str.Length + 1;
            }

            byte[] strTableBytes = Encoding.Latin1.GetBytes(strTableStr);
            cldb.stringTable = strTableBytes;
            
            //read types into cldb fields
            List<ClassDatabaseType> types = new List<ClassDatabaseType>();

            foreach (UnityClass uClass in inf.Classes)
            {
                ClassDatabaseType type = new ClassDatabaseType();
                type.classId = uClass.TypeID;
                if (uClass.Base != "")
                    type.baseClass = classLookup[uClass.Base];
                else
                    type.baseClass = -1;
                type.name = MakeCldbString(strLookup[uClass.Name]);
                type.fields = new List<ClassDatabaseTypeField>();

                UnityNode uNode;
                if (editor)
                    uNode = uClass.EditorRootNode;
                else
                    uNode = uClass.ReleaseRootNode;

                if (uNode != null)
                    ConvertUnityNodeToCldbType(type, uNode, strLookup);

                types.Add(type);
            }

            types = types.OrderBy(t => ReadCldbString(t.name, strTableBytes)).ToList();

            cldb.classes = types;

            cldb.header = new ClassDatabaseFileHeader()
            {
                header = "cldb",
                fileVersion = 3,
                flags = 0,
                compressionType = 0,
                compressedSize = 0,
                uncompressedSize = 0,
                unityVersionCount = 2,
                unityVersions = new string[]
                {
                    unityVerReg,
                    unityVerFul
                },
                stringTableLen = 0,
                stringTablePos = 0
            };

            return cldb;
        }

        static void BuildTpk(string cldbDir, string suffix)
        {
            ClassDatabasePackage pkg = new ClassDatabasePackage
            {
                valid = true,
                header = new ClassDatabasePackageHeader()
                {
                    magic = "CLPK",
                    fileVersion = 1,
                    compressionType = 0x82,
                    stringTableOffset = 0,
                    stringTableLenUncompressed = 0,
                    stringTableLenCompressed = 0,
                    fileBlockSize = 0,
                    fileCount = 0,
                    files = new List<ClassDatabaseFileRef>()
                },
                files = new List<ClassDatabaseFile>(),
                stringTable = new byte[0]
            };

            foreach (string file in Directory.EnumerateFiles(cldbDir, $"*{suffix}.dat"))
            {
                using (FileStream fs = File.OpenRead(file))
                using (AssetsFileReader r = new AssetsFileReader(fs))
                {
                    pkg.ImportFile(r);
            
                    string formattedName = "U" + Path.GetFileName(file).Replace($"{suffix}.dat", "");
                    ClassDatabaseFileRef fileRef = pkg.header.files[^1];
                    fileRef.name = formattedName;
                    pkg.header.files[^1] = fileRef;
                }
            }
            
            using (FileStream pkgfs = File.OpenWrite($"classdata{suffix}.tpk"))
            using (AssetsFileWriter pkgw = new AssetsFileWriter(pkgfs))
            {
                pkg.Write(pkgw, 1, 0x82);
            }
        }

        static void IterateClassStrings(UnityNode uNode, HashSet<string> usedStrList)
        {
            if (!usedStrList.Contains(uNode.Name))
                usedStrList.Add(uNode.Name);
            if (!usedStrList.Contains(uNode.TypeName))
                usedStrList.Add(uNode.TypeName);

            foreach (UnityNode child in uNode.SubNodes)
            {
                IterateClassStrings(child, usedStrList);
            }
        }

        static void ConvertUnityNodeToCldbType(ClassDatabaseType type, UnityNode uNode, Dictionary<string, uint> strLookup)
        {
            type.fields.Add(new ClassDatabaseTypeField()
            {
                typeName = MakeCldbString(strLookup[uNode.TypeName]),
                fieldName = MakeCldbString(strLookup[uNode.Name]),
                depth = uNode.Level,
                isArray = uNode.TypeFlags,
                size = uNode.ByteSize,
                version = (ushort)uNode.Version,
                flags2 = (uint)uNode.MetaFlag,
            });
            foreach (UnityNode child in uNode.SubNodes)
            {
                ConvertUnityNodeToCldbType(type, child, strLookup);
            }
        }

        static ClassDatabaseFileString MakeCldbString(string str)
        {
            return new ClassDatabaseFileString()
            {
                fromStringTable = false,
                str = new ClassDatabaseFileString.TableString()
                {
                    @string = str
                }
            };
        }

        static ClassDatabaseFileString MakeCldbString(uint idx)
        {
            return new ClassDatabaseFileString()
            {
                fromStringTable = true,
                str = new ClassDatabaseFileString.TableString()
                {
                    stringTableOffset = idx,
                }
            };
        }

        static string ReadCldbString(ClassDatabaseFileString str, byte[] stringTable)
        {
            return AssetsFileReader.ReadNullTerminatedArray(stringTable, str.str.stringTableOffset);
        }
    }
}
