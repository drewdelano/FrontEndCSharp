using FluentValidation.Resources;
using FrontEndCSharp.RuntimeOverrides;
using System.Globalization;

namespace FrontEndCSharp.FluentValidationRuntimeOverrides;

[RuntimeOverrideType(typeof(LanguageManager))]
internal static class LanguageManagerOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static string GetString(
        LanguageManager @this,
        string key,
        CultureInfo? culture = null) => throw null!;
}
