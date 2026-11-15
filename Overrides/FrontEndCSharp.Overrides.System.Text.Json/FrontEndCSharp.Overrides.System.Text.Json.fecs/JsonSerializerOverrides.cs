using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace System.Text.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(JsonSerializer))]
internal static class JsonSerializerOverrides
{
    [JavaScriptOverride]
    [RuntimeOverrideReturnType(0)]
    internal static TValue? Deserialize<
        [ManagedTypeAccess(
            ManagedTypeAccess.InstanceConstructors
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.WritePublicProperties
            | ManagedTypeAccess.WritePublicFields
            | ManagedTypeAccess.WriteNonPublicProperties
            | ManagedTypeAccess.WriteNonPublicFields,
            NonPublicMemberAttributeType = typeof(System.Text.Json.Serialization.JsonIncludeAttribute),
            ExcludedTypes = new[] { typeof(string) })] TValue>(
        string json,
        JsonSerializerOptions? options)
    {
        _ = new List<object>();
        ((ICollection<object>)(object)default(TValue)!).Add(null!);
        _ = new Dictionary<string, object>();
        ((IDictionary<string, object>)(object)default(TValue)!).Add(null!, null!);
        throw new JsonException();
    }

    [JavaScriptOverride]
    internal static string Serialize<
        [ManagedTypeAccess(
            ManagedTypeAccess.ReadPublicProperties
            | ManagedTypeAccess.ReadPublicFields
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.ReadNonPublicProperties
            | ManagedTypeAccess.ReadNonPublicFields,
            NonPublicMemberAttributeType = typeof(System.Text.Json.Serialization.JsonIncludeAttribute),
            ExcludedTypes = new[] { typeof(string) })] TValue>(
        TValue value,
        JsonSerializerOptions? options)
    {
        var list = (List<object>)(object)value!;
        _ = list.Count;
        _ = list[0];
        var dictionaryEnumerator =
            ((Dictionary<string, object>)(object)value!).GetEnumerator();
        _ = dictionaryEnumerator.MoveNext();
        _ = dictionaryEnumerator.Current.Key;
        _ = dictionaryEnumerator.Current.Value;
        dictionaryEnumerator.Dispose();
        throw new NotSupportedException();
    }
}
