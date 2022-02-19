using AssetRipper.VersionUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeTreeDumpToTpk
{
    public class CommandLineValues
    {
        public static readonly string DEFAULT_TYPE_TREE_DUMPS_URL = "https://github.com/AssetRipper/TypeTreeDumps";
        public static readonly UnityVersion DEFAULT_UNITY_VERSION = new UnityVersion(0);
        public static readonly List<UnityVersionType> DEFAULT_UNITY_VERSION_TYPES = new List<UnityVersionType>
        {
            UnityVersionType.Final,
            UnityVersionType.Patch
        };

        public bool Continue { get; }

        public string Command { get; }

        public RepoDownloadType RepoDownloadType { get; }
        public string RepoPath { get; }
        public UnityVersion SelectedVersionLow { get; }
        public UnityVersion SelectedVersionHigh { get; }
        public TpkCompressionType TpkCompressionType { get; }
        public TpkBuildType TpkBuildType { get; }
        public List<UnityVersionType> CldbUnityVersionTypes { get; }
        public CldbVersionSkipType CldbVersionSkipType { get; }
        public CldbExistBehavior CldbExistBehavior { get; }
        public string CldbPath { get; }
        //public TpkCompressionGroup TpkCompressionGroup { get; }

        private static HashSet<string> VALID_COMMANDS = new HashSet<string>
        {
            "ttdtotpk",
            /*
            "ttdtocldb",
            "cldbtotpk",
            "addcldbtotpk"
            */
        };

        public CommandLineValues(string[] args)
        {
            Continue = true;

            if (args.Length == 0)
            {
                Continue = false;
                PrintHelp();
                return;
            }

            string command = args[0];
            if (command == "help" || command == "-h" || command == "--help")
            {
                PrintHelp();
                Continue = false;
                return;
            }
            else if (!VALID_COMMANDS.Contains(command.ToLower()))
            {
                Console.WriteLine("invalid command!");
                Continue = false;
                return;
            }
            Command = args[0];

            RepoDownloadType = RepoDownloadType.Git;
            RepoPath = DEFAULT_TYPE_TREE_DUMPS_URL;
            SelectedVersionLow = DEFAULT_UNITY_VERSION;
            SelectedVersionHigh = DEFAULT_UNITY_VERSION;
            TpkCompressionType = TpkCompressionType.LZMA;
            TpkBuildType = TpkBuildType.Both;
            CldbUnityVersionTypes = DEFAULT_UNITY_VERSION_TYPES;
            CldbVersionSkipType = CldbVersionSkipType.Minor;
            CldbExistBehavior = CldbExistBehavior.Quit;
            CldbPath = "CldbDumps";

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--version")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string versionArg = args[i];
                    if (versionArg.Contains("-"))
                    {
                        string[] versionSplit = versionArg.Split('-');

                        if (versionSplit[0].ToLower() != "none")
                        {
                            SelectedVersionLow = UnityVersion.Parse(versionSplit[0]);
                        }
                        if (versionSplit[1].ToLower() != "none")
                        {
                            SelectedVersionHigh = UnityVersion.Parse(versionSplit[1]);
                        }
                    }
                    else
                    {
                        if (versionArg.ToLower() != "none")
                        {
                            SelectedVersionLow = UnityVersion.Parse(versionArg);
                            SelectedVersionHigh = UnityVersion.Parse(versionArg);
                        }
                    }
                }
                else if (arg == "--repodownloadtype")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string downloadType = args[i].ToLower();
                    if (downloadType == "git")
                    {
                        RepoDownloadType = RepoDownloadType.Git;
                    }
                    else if (downloadType == "local")
                    {
                        RepoDownloadType = RepoDownloadType.Local;
                    }
                    else
                    {
                        Console.WriteLine("repodownloadtype must be \"git\" or \"local\"!");
                        return;
                    }
                }
                else if (arg == "--repopath")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    RepoPath = args[i].ToLower();
                }
                else if (arg == "--tpkcompressiontype")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string compressionType = args[i].ToLower();
                    if (compressionType == "none")
                    {
                        TpkCompressionType = TpkCompressionType.None;
                    }
                    else if (compressionType == "lz4")
                    {
                        TpkCompressionType = TpkCompressionType.LZ4;
                    }
                    else if (compressionType == "lzma")
                    {
                        TpkCompressionType = TpkCompressionType.LZMA;
                    }
                    else
                    {
                        Console.WriteLine("repodownloadtype must be \"none\", \"lz4\" or \"lzma\"!");
                        Continue = false;
                        return;
                    }
                }
                else if (arg == "--tpkbuildtype")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string buildType = args[i].ToLower();
                    if (buildType == "release")
                    {
                        TpkBuildType = TpkBuildType.Release;
                    }
                    else if (buildType == "editor")
                    {
                        TpkBuildType = TpkBuildType.Editor;
                    }
                    else if (buildType == "both")
                    {
                        TpkBuildType = TpkBuildType.Both;
                    }
                    else
                    {
                        Console.WriteLine("tpkbuildtype must be \"release\", \"editor\" or \"both\"!");
                        Continue = false;
                        return;
                    }
                }
                else if (arg == "--cldbvertype")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    CldbUnityVersionTypes = new List<UnityVersionType>();
                    string versionTypes = args[i].ToLower();
                    foreach (char c in versionTypes)
                    {
                        UnityVersionType versionType = c switch
                        {
                            'a' => UnityVersionType.Alpha,
                            'b' => UnityVersionType.Beta,
                            'c' => UnityVersionType.China,
                            'f' => UnityVersionType.Final,
                            'p' => UnityVersionType.Patch,
                            'x' => UnityVersionType.Experimental,
                            _ => UnityVersionType.MaxValue + 1
                        };

                        if (versionType == UnityVersionType.MaxValue + 1)
                        {
                            Console.WriteLine($"invalid version type {c}!");
                            Continue = false;
                            return;
                        }

                        CldbUnityVersionTypes.Add(versionType);
                    }
                }
                else if (arg == "--cldbskip")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string versionSkip = args[i].ToLower();
                    if (versionSkip == "minor")
                    {
                        CldbVersionSkipType = CldbVersionSkipType.Minor;
                    }
                    else if (versionSkip == "type")
                    {
                        CldbVersionSkipType = CldbVersionSkipType.Type;
                    }
                    else if (versionSkip == "none")
                    {
                        CldbVersionSkipType = CldbVersionSkipType.None;
                    }
                    else
                    {
                        Console.WriteLine("cldbskip must be \"minor\", \"build\" or \"none\"!");
                        Continue = false;
                        return;
                    }
                }
                else if (arg == "--cldbexistbehavior")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    string existSettings = args[i].ToLower();
                    if (existSettings == "quit")
                    {
                        CldbExistBehavior = CldbExistBehavior.Quit;
                    }
                    else if (existSettings == "delete")
                    {
                        CldbExistBehavior = CldbExistBehavior.Delete;
                    }
                    else if (existSettings == "append")
                    {
                        CldbExistBehavior = CldbExistBehavior.Append;
                    }
                    else
                    {
                        Console.WriteLine("cldbexistbehavior must be \"quit\", \"delete\" or \"append\"!");
                        Continue = false;
                        return;
                    }
                }
                else if (arg == "--cldbpath")
                {
                    i++;
                    if (i >= args.Length)
                    {
                        Console.WriteLine("missing value!");
                        Continue = false;
                        return;
                    }

                    CldbPath = args[i].ToLower();
                }
                else
                {
                    Console.WriteLine("unrecognized flag " + arg);
                    return;
                }
            }
        }

        private void PrintHelp()
        {
            Console.WriteLine("typetreedumptotpk [command] <flags>");
            Console.WriteLine("commands:");
            Console.WriteLine("  help:");
            Console.WriteLine("    show this screen");
            Console.WriteLine("  ttdtotpk:");
            Console.WriteLine("    convert entire ttd repo to tpk");
            /*
            Console.WriteLine("    runs ttdtocldb and cldbtotpk");
            Console.WriteLine("  ttdtocldb:");
            Console.WriteLine("    convert entire ttd repo to cldbs");
            Console.WriteLine("  cldbtotpk:");
            Console.WriteLine("    add cldb files to new tpk");
            Console.WriteLine("  addcldbtotpk:");
            Console.WriteLine("    add cldb files to existing tpk");
            */
            Console.WriteLine();
            Console.WriteLine("flags:");
            Console.WriteLine("  --version: (default none)");
            Console.WriteLine("    set the version or versions to convert");
            Console.WriteLine("    examples:");
            Console.WriteLine("      --version 2019.4.0");
            Console.WriteLine("      --version 2019.2-2020.2.3");
            Console.WriteLine("  --repodownloadtype: (default git)");
            Console.WriteLine("    set the repo download type");
            Console.WriteLine("    can be either \"git\" or \"local\"");
            Console.WriteLine("    if local, you must set --repopath");
            Console.WriteLine("  --repopath: (default " + DEFAULT_TYPE_TREE_DUMPS_URL + ")");
            Console.WriteLine("    set the path to download ttd from");
            Console.WriteLine("    should be a url if --repodownloadtype is \"git\"");
            Console.WriteLine("    or a file path if --repodownloadtype is \"local\"");
            Console.WriteLine("  --tpkcompressiontype: (default LZMA)");
            Console.WriteLine("    set the compression method used on the tpk");
            Console.WriteLine("    can be \"none\", \"LZ4\", or \"LZMA\"");
            Console.WriteLine("  --tpkbuildtype: (default both)");
            Console.WriteLine("    set the tpk types to build");
            Console.WriteLine("    can be \"release\", \"editor\", or \"both\"");
            Console.WriteLine("  --cldbvertype: (default fp)");
            Console.WriteLine("    set the version types to convert (final, patch, etc.)");
            Console.WriteLine("    can be any of the following: abcfpx");
            Console.WriteLine("  --cldbskip: (default minor)");
            Console.WriteLine("    set how many versions to skip");
            Console.WriteLine("    can be \"minor\", \"type\", or \"none\"");
            Console.WriteLine("  --cldbexistbehavior: (default quit)");
            Console.WriteLine("    set what to do when the cldb path isn't empty");
            Console.WriteLine("    can be \"quit\", \"delete\" (delete all files in");
            Console.WriteLine("    cldb dump folder), or \"append\" (add or replace");
            Console.WriteLine("    cldb files to the folder)");
            Console.WriteLine("  --cldbpath: (default CldbDumps)");
            Console.WriteLine("    set the path to dump cldbs and copy to tpks");
            /*
            not supported (useful) in this version of at.net
            since the load method loads every entry at once anyway.
            the tpk needs to be modified to allow version regex in
            the tpk rather than each cldb file.
            Console.WriteLine("  --tpkcompressiongroup (default whole)");
            Console.WriteLine("    set the compression grouping used on the tpk");
            Console.WriteLine("    can be \"self\" or \"whole\"");
            Console.WriteLine("    whole is smaller but requires unpacking the entire tpk");
            Console.WriteLine("    self is bigger but only requires unpacking one file");
            */
        }
    }

    public enum RepoDownloadType
    {
        Git,
        Local
    };

    public enum TpkCompressionType
    {
        None,
        LZ4,
        LZMA
    };

    public enum TpkCompressionGroup
    {
        Self,
        Whole
    };

    public enum TpkBuildType
    {
        Release = 1,
        Editor = 2,
        Both = Release | Editor
    };

    public enum CldbVersionSkipType
    {
        Minor,
        Type,
        None
    };

    public enum CldbExistBehavior
    {
        Quit,
        Delete,
        Append
    };
}
