using Compiler.Helpers;
using Compiler.Read;
using System.Collections.Generic;
using System.Reflection;

namespace Compiler.DTOs
{
    public class CallGraphInfo
    {
        public RuntimeOverrideCollection RuntimeOverrides { get; internal set; } = RuntimeOverrideCollection.Empty;
        public List<MethodDecoder> MethodDecoders { get; internal set; }
        public Dictionary<TypeInfo, List<FieldInfo>> AllReferencedFields { get; internal set; }
        public MethodInfo EntryPoint { get; internal set; }
        public HashSet<TypeInfo> ReflectedTypes { get; internal set; } = new HashSet<TypeInfo>();
        public HashSet<TypeInfo> FieldReflectedTypes { get; internal set; } = new HashSet<TypeInfo>();
        public HashSet<TypeInfo> EnumMetadataTypes { get; internal set; } = new HashSet<TypeInfo>();
    }
}
