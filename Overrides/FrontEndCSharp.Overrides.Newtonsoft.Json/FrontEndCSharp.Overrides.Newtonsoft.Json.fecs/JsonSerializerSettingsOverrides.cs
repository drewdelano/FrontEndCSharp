using FrontEndCSharp.RuntimeOverrides;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Newtonsoft.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(JsonSerializerSettings))]
internal static class JsonSerializerSettingsOverrides
{
    [JavaScriptOverride(
        MemberKind = RuntimeOverrideMemberKind.InstanceMethod,
        TargetMemberName = "get_Converters")]
    [RuntimeOverrideReturnType(typeof(List<>))]
    internal static IList<JsonConverter> GetConverters(JsonSerializerSettings @this)
    {
        var converters = new List<JsonConverter>();
        converters.Add(null!);
        converters.Clear();
        return converters;
    }
}
