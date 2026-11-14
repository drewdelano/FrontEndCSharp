using System.Collections.Generic;
using System.Reflection;

namespace Compiler.DTOs
{
    public sealed class RuntimeOverrideDispatchDependency
    {
        public MethodBase RuntimeOverrideMethod { get; init; }

        public MethodInfo DispatchMethod { get; init; }

        public MethodBaseEntry Caller { get; init; }

        public HashSet<MethodBase> ResolvedTargets { get; } = new HashSet<MethodBase>();
    }
}
