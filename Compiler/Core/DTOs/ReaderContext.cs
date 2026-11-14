using Compiler.Helpers;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Compiler.DTOs
{
    public class ReaderContext
    {
        public MethodBase EntryPoint { get; init; }
        public RuntimeOverrideCollection RuntimeOverrides { get; init; } = RuntimeOverrideCollection.Empty;
        public Queue<MethodBaseEntry> Queue { get; set; } = new Queue<MethodBaseEntry>();
        public Dictionary<MethodBase, bool> ExploredMethods { get; set; } = new Dictionary<MethodBase, bool>(10000);
        public Dictionary<MethodBase, MethodDecoderOrder> OrderToOutput { get; set; } = new Dictionary<MethodBase, MethodDecoderOrder>(10000);
        public Dictionary<FieldInfo, bool> AllReferencedFields = new Dictionary<FieldInfo, bool>(10000);
        public Dictionary<TypeInfo, bool> AllReferencedTypes = new Dictionary<TypeInfo, bool>(10000);
        public List<RuntimeOverrideDispatchDependency> RuntimeOverrideDispatchDependencies { get; }
            = new List<RuntimeOverrideDispatchDependency>();
        public List<RuntimeOverrideManagedParameterDependency> RuntimeOverrideManagedParameterDependencies { get; }
            = new List<RuntimeOverrideManagedParameterDependency>();
        public HashSet<TypeInfo> ReflectedTypes { get; } = new HashSet<TypeInfo>();
        public HashSet<TypeInfo> FieldReflectedTypes { get; } = new HashSet<TypeInfo>();
        public HashSet<TypeInfo> EnumMetadataTypes { get; } = new HashSet<TypeInfo>();
        public Dictionary<MethodBase, HashSet<Type>> RuntimeOverrideManagedTypes { get; }
            = new Dictionary<MethodBase, HashSet<Type>>();
        public HashSet<ManagedTypeAccessRegistration> ManagedTypeAccessRegistrations { get; }
            = new HashSet<ManagedTypeAccessRegistration>();
        public HashSet<MethodBase> ReflectionRoots { get; } = new HashSet<MethodBase>();
        public HashSet<MethodBase> RuntimeOverrideDynamicRoots { get; } = new HashSet<MethodBase>();
    }
}
