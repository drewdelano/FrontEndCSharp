using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Compiler.DTOs
{
    public sealed class RuntimeOverrideManagedParameterDependency
    {
        public MethodBase Method { get; init; }

        public int ParameterIndex { get; init; }

        public ManagedTypeAccess Access { get; init; }

        public string NonPublicMemberAttributeTypeName { get; init; }

        public IReadOnlySet<Type> ExcludedTypes { get; init; }
            = new HashSet<Type>();

        public MethodBaseEntry Caller { get; init; }

        public HashSet<Type> RegisteredTypes { get; } = new HashSet<Type>();
    }
}
