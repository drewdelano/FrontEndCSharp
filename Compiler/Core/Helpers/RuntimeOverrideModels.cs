using System;
using System.Collections.Generic;
using System.Linq;
using FrontEndCSharp.RuntimeOverrides;

namespace Compiler.Helpers
{
    public sealed class RuntimeOverrideAssemblyEntry
    {
        public string Signature { get; init; }

        public string JavaScript { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedDependency> ManagedDependencies { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> ManagedReturnTypePatterns { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> ManagedAwaitableResultTypePatterns { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();

        public IReadOnlyList<RuntimeOverrideManagedByRefParameterWrite> ManagedByRefParameterWrites { get; init; }
            = Array.Empty<RuntimeOverrideManagedByRefParameterWrite>();

        public IReadOnlyList<string> Dependencies =>
            ManagedDependencies.Select(dependency => dependency.Signature).ToList();

        public IReadOnlyList<RuntimeOverrideManagedGenericTypeAccess> ManagedGenericTypeAccesses { get; init; }
            = Array.Empty<RuntimeOverrideManagedGenericTypeAccess>();

        public IReadOnlyList<RuntimeOverrideManagedParameterTypeAccess> ManagedParameterTypeAccesses { get; init; }
            = Array.Empty<RuntimeOverrideManagedParameterTypeAccess>();
    }

    public sealed class RuntimeOverrideManagedGenericTypeAccess
    {
        public int GenericParameterIndex { get; init; }

        public ManagedTypeAccess Access { get; init; }

        public string NonPublicMemberAttributeTypeName { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> ExcludedTypes { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();
    }

    public sealed class RuntimeOverrideManagedParameterTypeAccess
    {
        public int ParameterIndex { get; init; }

        public ManagedTypeAccess Access { get; init; }

        public string NonPublicMemberAttributeTypeName { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> ExcludedTypes { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();
    }

    public sealed class RuntimeOverrideManagedByRefParameterWrite
    {
        public int ParameterIndex { get; init; }

        public bool IsOut { get; init; }

        public bool IncludesNull { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> Types { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();
    }

    public sealed class RuntimeOverrideManagedDependency
    {
        public string AssemblyName { get; init; }

        public string Signature { get; init; }

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> GenericArgumentPatterns { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();
    }

    public sealed class RuntimeOverrideManagedTypeReference
    {
        public string AssemblyName { get; init; }

        public string TypeName { get; init; }

        public int GenericParameterIndex { get; init; } = -1;

        public IReadOnlyList<RuntimeOverrideManagedTypeReference> GenericArguments { get; init; }
            = Array.Empty<RuntimeOverrideManagedTypeReference>();

        public RuntimeOverrideManagedTypeReference ArrayElementType { get; init; }

        public string ArrayKind { get; init; }

        public int ArrayRank { get; init; }

        public IReadOnlyList<int> ArraySizes { get; init; } = Array.Empty<int>();

        public IReadOnlyList<int> ArrayLowerBounds { get; init; } = Array.Empty<int>();
    }

    public sealed class JavaScriptPackageDependency
    {
        public string PackageName { get; init; }

        public string VersionRange { get; init; }

        public JavaScriptPackageDependencyKind Kind { get; init; }
    }
}
