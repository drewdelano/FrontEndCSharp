using FrontEndCSharp.RuntimeOverrides;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace System.ComponentModel.AnnotationsRuntimeOverrides;

[RuntimeOverrideType(typeof(ValidationContext))]
internal static class ValidationContextOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ValidationContext Create(
        [ManagedTypeAccess(
            ManagedTypeAccess.ReadPublicProperties
            | ManagedTypeAccess.CustomAttributes
            | ManagedTypeAccess.PublicInstanceMethods)] object instance);
}
