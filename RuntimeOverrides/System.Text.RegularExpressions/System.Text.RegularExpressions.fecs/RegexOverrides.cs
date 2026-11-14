using FrontEndCSharp.RuntimeOverrides;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.SystemTextRegularExpressionsRuntimeOverrides;

[RuntimeOverrideType(typeof(Regex))]
internal static class RegexOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    internal static Regex Create() => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static string Replace(Regex @this, string input, MatchEvaluator evaluator) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static string Replace(Regex @this, string input, string replacement) => throw null!;
}

[RuntimeOverrideType(typeof(Match))]
internal static class MatchOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static GroupCollection get_Groups(Match @this) => throw null!;
}

[RuntimeOverrideType(typeof(GroupCollection))]
internal static class GroupCollectionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Item")]
    internal static Group GetItem(GroupCollection @this, int groupnum) => throw null!;
}

[RuntimeOverrideType(typeof(Capture))]
internal static class CaptureOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static string get_Value(Capture @this) => throw null!;
}

[RuntimeOverrideType(typeof(Group))]
internal static class GroupOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static bool get_Success(Group @this) => throw null!;
}
