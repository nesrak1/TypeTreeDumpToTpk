using AssetRipper.VersionUtilities;
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
        CommandLineValues cliValues;

        static void Main(string[] args)
        {
            Console.WriteLine("TypeTreeDumpToTpk");

            Program program = new Program();
            program.RunMain(args);
        }

        void RunMain(string[] args)
        {
            cliValues = new CommandLineValues(args);
            if (!cliValues.Continue)
            {
                return;
            }

            if (cliValues.Command == "ttdtotpk")
            {
                TypeTreeDumpToCldb();
                CldbToTpk();
            }
            else if (cliValues.Command == "cldbtotpk")
            {
                CldbToTpk();
            }
        }

        string DownloadOrFindRepoDirectory()
        {
            string repoDir;
            if (cliValues.RepoDownloadType == RepoDownloadType.Git)
            {
                Console.WriteLine("downloading type tree dump repository...");
                ZipUtil.DownloadAndUnpackZip($"{cliValues.RepoPath}/archive/refs/heads/main.zip", "TypeTreeDumps");
                repoDir = Path.Combine("TypeTreeDumps", "TypeTreeDumps-main"); //assumes repo is still called TypeTreeDumps
            }
            else
            {
                Console.WriteLine("using local type tree dump repository...");
                repoDir = cliValues.RepoPath;
            }
            return repoDir;
        }

        //to easily lookup all versions that match
        static string GetVersionMask(UnityVersion unityVersion, CldbVersionSkipType skipType, bool wildcard)
        {
            if (!wildcard)
            {
                if (skipType == CldbVersionSkipType.Minor)
                    return $"{unityVersion.Major}.{unityVersion.Minor}";
                else if (skipType == CldbVersionSkipType.Type)
                    return $"{unityVersion.Major}.{unityVersion.Minor}{unityVersion.Type.ToLiteral()}";
                else
                    return unityVersion.ToString();
            }
            else
            {
                if (skipType == CldbVersionSkipType.Minor)
                    return $"{unityVersion.Major}.{unityVersion.Minor}.*";
                else if (skipType == CldbVersionSkipType.Type)
                    return $"{unityVersion.Major}.{unityVersion.Minor}{unityVersion.Type.ToLiteral()}*";
                else
                    return unityVersion.ToString();
            }
        }

        void TypeTreeDumpToCldb()
        {
            string infoJsonDir = Path.Combine(DownloadOrFindRepoDirectory(), "InfoJson");
            string cldbDir = cliValues.CldbPath;

            Console.WriteLine("creating cldbs...");

            if (Directory.Exists(cldbDir) && !IsDirectoryEmpty(cldbDir))
            {
                if (cliValues.CldbExistBehavior == CldbExistBehavior.Quit)
                {
                    Console.WriteLine("cldb dir is not empty, exiting now.");
                    Console.WriteLine("if you want to override this, set --cldbexistbehavior");
                    Environment.Exit(0);
                    return;
                }
                else if (cliValues.CldbExistBehavior == CldbExistBehavior.Delete)
                {
                    Directory.Delete(cldbDir, true);
                }
            }
            Directory.CreateDirectory(cldbDir);

            List<string> latestFiles = new List<string>();
            Dictionary<string, UnityVersion> selectedVersions = new Dictionary<string, UnityVersion>();

            foreach (string file in Directory.EnumerateFiles(infoJsonDir))
            {
                UnityVersion unityVersion = UnityVersion.Parse(Path.GetFileNameWithoutExtension(file));
                string versionMask = GetVersionMask(unityVersion, cliValues.CldbVersionSkipType, false);

                //skip unselected version types
                if (!cliValues.CldbUnityVersionTypes.Contains(unityVersion.Type))
                    continue;

                if (cliValues.SelectedVersionLow != CommandLineValues.DEFAULT_UNITY_VERSION)
                {
                    if (unityVersion < cliValues.SelectedVersionLow)
                        continue;
                }
                if (cliValues.SelectedVersionHigh != CommandLineValues.DEFAULT_UNITY_VERSION)
                {
                    if (unityVersion > cliValues.SelectedVersionHigh)
                        continue;
                }

                if (!selectedVersions.ContainsKey(versionMask))
                {
                    selectedVersions[versionMask] = unityVersion;
                }
                else
                {
                    UnityVersion compareUnityVersion = selectedVersions[versionMask];
                    if (unityVersion > compareUnityVersion)
                    {
                        selectedVersions[versionMask] = unityVersion;
                    }
                }
            }

            foreach (var versions in selectedVersions)
            {
                string versionString = versions.Value.ToString();
                if (versions.Value.IsLess(5))
                    versionString = versionString[0..^2];

                latestFiles.Add(Path.Combine(infoJsonDir, versionString + ".json"));
            }

            foreach (string file in latestFiles)
            {
                try
                {
                    JsonTextReader r = new JsonTextReader(new StreamReader(file));
                    JsonSerializer deserializer = new JsonSerializer();
                    UnityInfo inf = (UnityInfo)deserializer.Deserialize(r, typeof(UnityInfo));

                    Console.WriteLine($"converting {inf.Version}...");

                    if (cliValues.TpkBuildType.HasFlag(TpkBuildType.Editor))
                    {
                        string fsPath = Path.Combine(cldbDir, inf.Version + "_editor.dat");
                        if (!File.Exists(fsPath))
                        {
                            ClassDatabaseFile cldbEditor = ConvertUnityInfoToCldb(inf, true);
                            using (FileStream fsEditor = File.OpenWrite(fsPath))
                            {
                                cldbEditor.Write(new AssetsFileWriter(fsEditor), 0, 0);
                            }
                        }
                    }

                    if (cliValues.TpkBuildType.HasFlag(TpkBuildType.Release))
                    {
                        string fsPath = Path.Combine(cldbDir, inf.Version + "_release.dat");
                        if (!File.Exists(fsPath))
                        {
                            ClassDatabaseFile cldbRelease = ConvertUnityInfoToCldb(inf, false);
                            using (FileStream fsRelease = File.OpenWrite(fsPath))
                            {
                                cldbRelease.Write(new AssetsFileWriter(fsRelease), 0, 0);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error converting {file}:\n{ex.Message}");
                }
            }
        }

        void CldbToTpk()
        {
            string cldbDir = cliValues.CldbPath;

            if (cliValues.TpkBuildType.HasFlag(TpkBuildType.Editor))
            {
                Console.WriteLine("building editor tpk...");
                BuildTpk(cldbDir, "_editor");
            }

            if (cliValues.TpkBuildType.HasFlag(TpkBuildType.Release))
            {
                Console.WriteLine("building release tpk...");
                BuildTpk(cldbDir, "_release");
            }
        }

        ClassDatabaseFile ConvertUnityInfoToCldb(UnityInfo inf, bool editor)
        {
            ClassDatabaseFile cldb = new ClassDatabaseFile();

            UnityVersion unityVersion = UnityVersion.Parse(inf.Version);
            string unityVerFull = unityVersion.ToString();

            //this will probably be removed in a later version
            //of tpk since we have to parse all the matched
            //versions anyway when there are conflicts
            string unityVerWildcard = GetVersionMask(unityVersion, cliValues.CldbVersionSkipType, true);

            Dictionary<string, uint> strLookup = new Dictionary<string, uint>();
            Dictionary<string, int> classLookup = new Dictionary<string, int>();

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
                    unityVerWildcard,
                    unityVerFull
                },
                stringTableLen = 0,
                stringTablePos = 0
            };

            return cldb;
        }

        void BuildTpk(string cldbDir, string suffix)
        {
            byte compressionType = (byte)(0x80 | (int)cliValues.TpkCompressionType);
            if (compressionType == 0x80) //if no compression set
                compressionType |= 0x20 | 0x40; //set uncompressed cldb/database flags

            ClassDatabasePackage pkg = new ClassDatabasePackage
            {
                valid = true,
                header = new ClassDatabasePackageHeader()
                {
                    magic = "CLPK",
                    fileVersion = 1,
                    compressionType = compressionType, //probably useless but whatever
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
                pkg.Write(pkgw, 1, compressionType);
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

        static bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }
    }
}
