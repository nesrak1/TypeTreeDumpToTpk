using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypeTreeDumpToTpk.Json
{
    internal class UnityInfo
    {
        public string Version { get; set; }
        public List<UnityString> Strings { get; set; }
        public List<UnityClass> Classes { get; set; }
    }

    internal class UnityClass
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string FullName { get; set; }
        public string Module { get; set; }
        public int TypeID { get; set; }
        public string Base { get; set; }
        public List<string> Derived { get; set; }
        public uint DescendantCount { get; set; }
        public int Size { get; set; }
        public uint TypeIndex { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsEditorOnly { get; set; }
        public bool IsStripped { get; set; }
        public UnityNode EditorRootNode { get; set; }
        public UnityNode ReleaseRootNode { get; set; }
    }

    internal class UnityNode
    {
        public string TypeName { get; set; }
        public string Name { get; set; }
        public byte Level { get; set; }
        public int ByteSize { get; set; }
        public int Index { get; set; }
        public short Version { get; set; }
        public byte TypeFlags { get; set; }
        public int MetaFlag { get; set; }
        public List<UnityNode> SubNodes { get; set; }
    }

    internal class UnityString
    {
        public uint Index { get; set; }
        public string String { get; set; }
    }
}
