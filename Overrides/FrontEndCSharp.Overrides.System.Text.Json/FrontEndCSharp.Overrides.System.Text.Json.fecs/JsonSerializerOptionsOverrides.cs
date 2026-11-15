using FrontEndCSharp.RuntimeOverrides;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace System.Text.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(JsonSerializerOptions))]
internal static class JsonSerializerOptionsOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern JsonSerializerOptions Create();

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.Constructor)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern JsonSerializerOptions Create(JsonSerializerDefaults defaults);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "get_Converters")]
    [RuntimeOverrideReturnType(typeof(List<>))]
    internal static IList<JsonConverter> GetConverters(JsonSerializerOptions @this)
    {
        var converters = new List<JsonConverter>();
        converters.Add(null!);
        converters.Clear();
        return converters;
    }

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void VerifyMutable(JsonSerializerOptions @this);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "set_DefaultIgnoreCondition")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetDefaultIgnoreCondition(
        JsonSerializerOptions @this,
        JsonIgnoreCondition value);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "set_IgnoreNullValues")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetIgnoreNullValues(
        JsonSerializerOptions @this,
        bool value);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "set_ReadCommentHandling")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetReadCommentHandling(
        JsonSerializerOptions @this,
        JsonCommentHandling value);

    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "set_MaxDepth")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern void SetMaxDepth(JsonSerializerOptions @this, int value);
}
