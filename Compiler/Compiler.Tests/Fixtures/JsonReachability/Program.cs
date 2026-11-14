using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

_ = JsonSerializer.Deserialize<SystemTextJsonParameterizedPayload>("{}");
_ = JsonSerializer.Deserialize<SystemTextJsonAttributedPrivatePayload>("{}");
_ = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftPrivateDefaultPayload>("{}");
_ = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftAttributedPrivatePayload>("{}");
_ = JsonSerializer.Deserialize<SystemTextJsonNonPublicIncludePayload>("{}");
_ = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNonPublicMemberPayload>("{}");
_ = JsonSerializer.Deserialize<List<int>>("[]");
_ = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>("{}");
_ = JsonSerializer.Deserialize<JsonEnumPayload>("{}");
_ = JsonSerializer.Deserialize<SystemTextJsonNestedPayload>("{}");
var fieldIdentity = new FieldIdentityDerived<int> { BaseValue = 1, Value = 2 };
_ = fieldIdentity;
_ = typeof(FieldIdentityDerived<int>).GetFields(BindingFlags.Public | BindingFlags.Instance);

internal sealed class SystemTextJsonParameterizedPayload
{
    public SystemTextJsonParameterizedPayload(string name) => Name = name;
    public string Name { get; set; }
}

internal sealed class SystemTextJsonAttributedPrivatePayload
{
    [System.Text.Json.Serialization.JsonConstructor]
    private SystemTextJsonAttributedPrivatePayload(string name) => Name = name;

    public string Name { get; }
}

internal sealed class NewtonsoftPrivateDefaultPayload
{
    private NewtonsoftPrivateDefaultPayload() { }
    public string? Name { get; set; }
}

internal sealed class NewtonsoftAttributedPrivatePayload
{
    [Newtonsoft.Json.JsonConstructor]
    private NewtonsoftAttributedPrivatePayload(string name) => Name = name;

    public string Name { get; }
}

#pragma warning disable CS0169, IDE0044 // Serializer metadata mutates these fields through reflection.
internal sealed class SystemTextJsonNonPublicIncludePayload
{
    [JsonInclude]
    private string? PrivateProperty { get; set; }

    [JsonInclude]
    private string? privateField;

    private string? UnattributedProperty { get; set; }
    private string? unattributedField;
}

internal sealed class NewtonsoftNonPublicMemberPayload
{
    [Newtonsoft.Json.JsonProperty]
    private string? PrivateProperty { get; set; }

    [Newtonsoft.Json.JsonProperty]
    private string? privateField;

    private string? UnattributedProperty { get; set; }
    private string? unattributedField;
}
#pragma warning restore CS0169, IDE0044

internal class FieldIdentityBase<T>
{
    public T? BaseValue;
}

internal sealed class FieldIdentityDerived<T> : FieldIdentityBase<T>
{
    public T? Value;
}

internal sealed class JsonEnumPayload
{
    public JsonSByteEnum SByte { get; set; }
    public JsonByteEnum Byte { get; set; }
    public JsonInt16Enum Int16 { get; set; }
    public JsonUInt16Enum UInt16 { get; set; }
    public JsonInt32Enum Int32 { get; set; }
    public JsonUInt32Enum UInt32 { get; set; }
    public JsonInt64Enum Int64 { get; set; }
    public JsonUInt64Enum UInt64 { get; set; }
    public JsonFlagsEnum Flags { get; set; }
    public JsonNamedEnum Named { get; set; }
}

#pragma warning disable CS0649 // Serializer metadata mutates this field through reflection.
internal sealed class SystemTextJsonNestedPayload
{
    public SystemTextJsonNestedPropertyPayload? Property { get; set; }

    public SystemTextJsonNestedFieldPayload? Field;
}
#pragma warning restore CS0649

internal sealed class SystemTextJsonNestedPropertyPayload
{
    public SystemTextJsonNestedPropertyPayload()
    {
    }

    public string? Value { get; set; }
}

internal sealed class SystemTextJsonNestedFieldPayload
{
    public SystemTextJsonNestedFieldPayload()
    {
    }

    public string? Value { get; set; }
}

internal enum JsonSByteEnum : sbyte { Value = -1 }
internal enum JsonByteEnum : byte { Value = 1 }
internal enum JsonInt16Enum : short { Value = -2 }
internal enum JsonUInt16Enum : ushort { Value = 2 }
internal enum JsonInt32Enum : int { Value = -3 }
internal enum JsonUInt32Enum : uint { Value = 3 }
internal enum JsonInt64Enum : long { Value = -4 }
internal enum JsonUInt64Enum : ulong { Value = ulong.MaxValue }
[Flags]
internal enum JsonFlagsEnum : int { First = 1, Second = 2 }
internal enum JsonNamedEnum : int { Value = 1 }
internal enum UnusedJsonEnum : int { Value = 1 }
