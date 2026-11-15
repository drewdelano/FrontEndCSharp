using FrontEndCSharp.RuntimeOverrides;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Newtonsoft.JsonRuntimeOverrides;

[RuntimeOverrideType(typeof(JsonConvert))]
internal static class JsonConvertOverrides
{
    [JavaScriptOverride]
    [RuntimeOverrideReturnType(0)]
    internal static T? DeserializeObject<
        [ManagedTypeAccess(
            ManagedTypeAccess.InstanceConstructors
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.WritePublicProperties
            | ManagedTypeAccess.WritePublicFields
            | ManagedTypeAccess.WriteNonPublicProperties
            | ManagedTypeAccess.WriteNonPublicFields,
            NonPublicMemberAttributeType = typeof(JsonPropertyAttribute),
            ExcludedTypes = new[] { typeof(string) })] T>(
        string value)
    {
        _ = new List<object>();
        ((ICollection<object>)(object)default(T)!).Add(null!);
        _ = new Dictionary<string, object>();
        ((IDictionary<string, object>)(object)default(T)!).Add(null!, null!);
        throw new JsonSerializationException();
    }

    [JavaScriptOverride]
    [RuntimeOverrideReturnType(0)]
    internal static T? DeserializeObject<
        [ManagedTypeAccess(
            ManagedTypeAccess.InstanceConstructors
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.WritePublicProperties
            | ManagedTypeAccess.WritePublicFields
            | ManagedTypeAccess.WriteNonPublicProperties
            | ManagedTypeAccess.WriteNonPublicFields,
            NonPublicMemberAttributeType = typeof(JsonPropertyAttribute),
            ExcludedTypes = new[] { typeof(string) })] T>(
        string value,
        JsonSerializerSettings? settings)
    {
        _ = new List<object>();
        ((ICollection<object>)(object)default(T)!).Add(null!);
        _ = new Dictionary<string, object>();
        ((IDictionary<string, object>)(object)default(T)!).Add(null!, null!);
        throw new JsonSerializationException();
    }

    [JavaScriptOverride]
    internal static string SerializeObject(
        [ManagedTypeAccess(
            ManagedTypeAccess.ReadPublicProperties
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.ReadPublicFields
            | ManagedTypeAccess.ReadNonPublicProperties
            | ManagedTypeAccess.ReadNonPublicFields,
            NonPublicMemberAttributeType = typeof(JsonPropertyAttribute),
            ExcludedTypes = new[] { typeof(string) })] object? value)
    {
        var list = (List<object>)value!;
        _ = list.Count;
        _ = list[0];
        var dictionaryEnumerator =
            ((Dictionary<string, object>)value!).GetEnumerator();
        _ = dictionaryEnumerator.MoveNext();
        _ = dictionaryEnumerator.Current.Key;
        _ = dictionaryEnumerator.Current.Value;
        dictionaryEnumerator.Dispose();
        throw new JsonSerializationException();
    }

    [JavaScriptOverride]
    internal static string SerializeObject(
        [ManagedTypeAccess(
            ManagedTypeAccess.ReadPublicProperties
            | ManagedTypeAccess.EnumMetadata
            | ManagedTypeAccess.ReadPublicFields
            | ManagedTypeAccess.ReadNonPublicProperties
            | ManagedTypeAccess.ReadNonPublicFields,
            NonPublicMemberAttributeType = typeof(JsonPropertyAttribute),
            ExcludedTypes = new[] { typeof(string) })] object? value,
        JsonSerializerSettings? settings)
    {
        var list = (List<object>)value!;
        _ = list.Count;
        _ = list[0];
        var dictionaryEnumerator =
            ((Dictionary<string, object>)value!).GetEnumerator();
        _ = dictionaryEnumerator.MoveNext();
        _ = dictionaryEnumerator.Current.Key;
        _ = dictionaryEnumerator.Current.Value;
        dictionaryEnumerator.Dispose();
        throw new JsonSerializationException();
    }
}
