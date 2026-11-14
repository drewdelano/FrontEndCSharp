using Interop.JS.Browser;
using System.Text.Json;
using System.Text.Json.Serialization;

var payload = JsonSerializer.Deserialize<WeatherPayload>(
    "{\"Summary\":\"clear\",\"temperatureC\":19,\"temperatureC\":21,\"Active\":true,"
        + "\"Ignored\":\"incoming\",\"Text\":\"line\\n\\u003C\\u00E9+&'>\","
        + "\"Large\":9007199254740993,\"Optional\":null,\"Included\":\"included\","
        + "\"InitOnly\":\"init\",\"NeverIgnored\":\"never\",\"ReadOnly\":\"incoming\","
        + "\"Item\":\"incoming\",\"Unknown\":42}");
var serialized = JsonSerializer.Serialize(payload);
var systemTextJsonEmptyRejected = false;
var systemTextJsonIncompleteRejected = false;
var systemTextJsonOverflowRejected = false;
var systemTextJsonSyntaxLocationMatched = false;
var systemTextJsonUtf8LocationMatched = false;
try
{
    JsonSerializer.Deserialize<WeatherPayload>("");
}
catch (JsonException)
{
    systemTextJsonEmptyRejected = true;
}
try
{
    JsonSerializer.Deserialize<WeatherPayload>("{");
}
catch (JsonException)
{
    systemTextJsonIncompleteRejected = true;
}
try
{
    JsonSerializer.Deserialize<WeatherPayload>("{\"temperatureC\":2147483648}");
}
catch (JsonException exception)
{
    systemTextJsonOverflowRejected = exception.Path == "$.temperatureC"
        && exception.LineNumber == 0
        && exception.BytePositionInLine == 26;
}
try
{
    JsonSerializer.Deserialize<WeatherPayload>(
        "{\n  \"TemperatureC\": 1,\n  \"Active\": truX\n}");
}
catch (JsonException exception)
{
    systemTextJsonSyntaxLocationMatched = exception.Path == "$.Active"
        && exception.LineNumber == 2
        && exception.BytePositionInLine == 15;
}
try
{
    JsonSerializer.Deserialize<WeatherPayload>("{\"Text\":\"é\",\"Active\":truX}");
}
catch (JsonException exception)
{
    systemTextJsonUtf8LocationMatched = exception.Path == "$.Active"
        && exception.LineNumber == 0
        && exception.BytePositionInLine == 25;
}
var systemTextJsonSucceeded = payload != null
    && payload.Summary == "set:clear"
    && payload.TemperatureC == 21
    && payload.Active
    && payload.Ignored == "constructor-default"
    && payload.Text == "line\n<é+&'>"
    && payload.Large == 9007199254740993L
    && payload.Optional == null
    && payload.Included == "included"
    && payload.InitOnly == "init"
    && payload.NeverIgnored == "never"
    && payload.ReadOnly == "read-only"
    && JsonSerializer.Deserialize<WeatherPayload>("null") == null
    && systemTextJsonEmptyRejected
    && systemTextJsonIncompleteRejected
    && systemTextJsonOverflowRejected
    && systemTextJsonSyntaxLocationMatched
    && systemTextJsonUtf8LocationMatched
    && serialized == "{\"Summary\":\"set:clear\",\"temperatureC\":21,\"Active\":true,"
        + "\"Text\":\"line\\n\\u003C\\u00E9\\u002B\\u0026\\u0027\\u003E\","
        + "\"Large\":9007199254740993,\"Optional\":null,\"Included\":\"included\","
        + "\"InitOnly\":\"init\",\"NeverIgnored\":\"never\",\"ReadOnly\":\"read-only\"}";

var newtonsoftJson =
    "{\"summary\":\"clear\",\"temperature_c\":19,\"temperature_c\":21,\"active\":true,"
        + "\"Ignored\":\"incoming\",\"publicfield\":7,\"text\":\"line\\n\\u003C\\u00E9+&'>\","
        + "\"large\":9007199254740993,\"optional\":null,\"Unknown\":42}";
var newtonsoftPayload = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(newtonsoftJson);
var newtonsoftSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftPayload);
var newtonsoftSettingsPayload = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
    newtonsoftJson,
    settings: null);
var newtonsoftSettingsSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(
    newtonsoftSettingsPayload,
    settings: null);
var newtonsoftIncompleteRejected = false;
var newtonsoftOverflowRejected = false;
var newtonsoftSyntaxLocationMatched = false;
var newtonsoftSettingsLocationsMatched = false;
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>("{");
}
catch (Newtonsoft.Json.JsonSerializationException)
{
    newtonsoftIncompleteRejected = true;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>("{\"Small\":2147483648}");
}
catch (Newtonsoft.Json.JsonReaderException exception)
{
    newtonsoftOverflowRejected = exception.Path == "Small"
        && exception.LineNumber == 1
        && exception.LinePosition == 19;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
        "{\n  \"Small\": 1,\n  \"Active\": truX\n}");
}
catch (Newtonsoft.Json.JsonReaderException exception)
{
    newtonsoftSyntaxLocationMatched = exception.Path == "Active"
        && exception.LineNumber == 3
        && exception.LinePosition == 15;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
        "{\n  \"Small\": 1,\n  \"Active\": truX\n}",
        settings: null);
}
catch (Newtonsoft.Json.JsonReaderException syntaxException)
{
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
            "{\n  \"Small\": 2147483648\n}",
            settings: null);
    }
    catch (Newtonsoft.Json.JsonReaderException conversionException)
    {
        newtonsoftSettingsLocationsMatched = syntaxException.Path == "Active"
            && syntaxException.LineNumber == 3
            && syntaxException.LinePosition == 15
            && conversionException.Path == "Small"
            && conversionException.LineNumber == 2
            && conversionException.LinePosition == 21;
    }
}
var newtonsoftSucceeded = newtonsoftPayload != null
    && newtonsoftPayload.Summary == "set:clear"
    && newtonsoftPayload.TemperatureC == 21
    && newtonsoftPayload.Active
    && newtonsoftPayload.Ignored == "constructor-default"
    && newtonsoftPayload.PublicField == 7
    && newtonsoftPayload.Text == "line\n<é+&'>"
    && newtonsoftPayload.Large == 9007199254740993L
    && newtonsoftPayload.Optional == null
    && Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>("null") == null
    && Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(" \r\n\t ") == null
    && newtonsoftIncompleteRejected
    && newtonsoftOverflowRejected
    && newtonsoftSyntaxLocationMatched
    && newtonsoftSettingsLocationsMatched
    && newtonsoftSettingsSerialized == newtonsoftSerialized
    && newtonsoftSerialized == "{\"PublicField\":7,\"Summary\":\"set:clear\",\"temperature_c\":21,\"Active\":true,"
        + "\"Text\":\"line\\n<é+&'>\",\"Large\":9007199254740993,\"Optional\":null,"
        + "\"Small\":0}";

const string dateTimeJson = "\"2024-02-29T12:34:56.7891234Z\"";
const string dateTimeOffsetJson = "\"2024-02-29T12:34:56.7891234+05:30\"";
const string dateOnlyJson = "\"2024-02-29\"";
const string timeOnlyJson = "\"12:34:56.7891234\"";
const string timeSpanJson = "\"1.02:03:04.0050060\"";
var dateTime = new DateTime(2024, 2, 29, 12, 34, 56, 789, DateTimeKind.Utc).AddTicks(1234);
var dateTimeOffset = new DateTimeOffset(
    2024,
    2,
    29,
    12,
    34,
    56,
    789,
    TimeSpan.FromMinutes(330)).AddTicks(1234);
var dateOnly = new DateOnly(2024, 2, 29);
var timeOnly = new TimeOnly(12, 34, 56, 789).Add(TimeSpan.FromTicks(1234));
var timeSpan = new TimeSpan(1, 2, 3, 4, 5).Add(TimeSpan.FromTicks(60));
var systemTextJsonTemporalOutput = JsonSerializer.Serialize(dateTime)
    + "|" + JsonSerializer.Serialize(dateTimeOffset)
    + "|" + JsonSerializer.Serialize(dateOnly)
    + "|" + JsonSerializer.Serialize(timeOnly)
    + "|" + JsonSerializer.Serialize(timeSpan);
var newtonsoftTemporalOutput = Newtonsoft.Json.JsonConvert.SerializeObject(dateTime)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(dateTimeOffset)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(dateOnly)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(timeOnly)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(timeSpan);
var expectedTemporalOutput = dateTimeJson + "|" + dateTimeOffsetJson + "|" + dateOnlyJson
    + "|" + timeOnlyJson + "|" + timeSpanJson;
var temporalSucceeded = systemTextJsonTemporalOutput == expectedTemporalOutput
    && newtonsoftTemporalOutput == expectedTemporalOutput
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<DateTime>(dateTimeJson)) == dateTimeJson
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<DateTimeOffset>(dateTimeOffsetJson)) == dateTimeOffsetJson
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<DateOnly>(dateOnlyJson)) == dateOnlyJson
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<TimeOnly>(timeOnlyJson)) == timeOnlyJson
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<TimeSpan>(timeSpanJson)) == timeSpanJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<DateTime>(dateTimeJson)) == dateTimeJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<DateTimeOffset>(dateTimeOffsetJson)) == dateTimeOffsetJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<DateOnly>(dateOnlyJson)) == dateOnlyJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<TimeOnly>(timeOnlyJson)) == timeOnlyJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<TimeSpan>(timeSpanJson)) == timeSpanJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(dateTimeOffset, settings: null) == dateTimeOffsetJson
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<DateTimeOffset>(dateTimeOffsetJson, settings: null),
        settings: null) == dateTimeOffsetJson;

var systemTextJsonInherited = JsonSerializer.Deserialize<SystemTextJsonDerivedPayload>(
    "{\"DerivedProperty\":\"derived-input\",\"BaseProperty\":\"base-input\"}");
var systemTextJsonInheritedOutput = JsonSerializer.Serialize(systemTextJsonInherited);
var newtonsoftInherited = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDerivedPayload>(
    "{\"derivedfield\":2,\"basefield\":1,\"derivedproperty\":\"derived-input\","
        + "\"baseproperty\":\"base-input\"}");
var newtonsoftInheritedOutput = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftInherited);
var inheritanceSucceeded = systemTextJsonInherited?.DerivedProperty == "derived-input"
    && systemTextJsonInherited.BaseProperty == "base-input"
    && systemTextJsonInheritedOutput
        == "{\"DerivedProperty\":\"derived-input\",\"BaseProperty\":\"base-input\"}"
    && newtonsoftInherited?.DerivedField == 2
    && newtonsoftInherited.BaseField == 1
    && newtonsoftInherited.DerivedProperty == "derived-input"
    && newtonsoftInherited.BaseProperty == "base-input"
    && newtonsoftInheritedOutput
        == "{\"DerivedField\":2,\"BaseField\":1,\"DerivedProperty\":\"derived-input\","
            + "\"BaseProperty\":\"base-input\"}";

var systemTextJsonShadowed = JsonSerializer.Deserialize<SystemTextJsonShadowedPayload>(
    "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\"}");
var systemTextJsonShadowedOutput = JsonSerializer.Serialize(systemTextJsonShadowed);
var newtonsoftShadowed = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftShadowedPayload>(
    "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\","
        + "\"ReadOnlyField\":99,\"ReadOnlyProperty\":\"incoming\",\"Item\":\"incoming\"}");
var newtonsoftShadowedOutput = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftShadowed);
var shadowedMembersSucceeded = systemTextJsonShadowedOutput
        == "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\"}"
    && newtonsoftShadowedOutput
        == "{\"ReadOnlyField\":7,\"derivedValue\":\"derived-input\","
            + "\"baseValue\":\"base-input\",\"ReadOnlyProperty\":\"read-only\"}";

var systemTextJsonIntegers = JsonSerializer.Deserialize<long[]>(
    "[9007199254740993,-9007199254740993]");
var systemTextJsonStrings = JsonSerializer.Deserialize<string?[]>(
    "[\"line\\n\\u003C\\u00E9\",null]");
var systemTextJsonIntegerArrayOutput = JsonSerializer.Serialize(systemTextJsonIntegers);
var systemTextJsonStringArrayOutput = JsonSerializer.Serialize(systemTextJsonStrings);
var newtonsoftIntegers = Newtonsoft.Json.JsonConvert.DeserializeObject<long[]>(
    "[9007199254740993,-9007199254740993]");
var newtonsoftStrings = Newtonsoft.Json.JsonConvert.DeserializeObject<string?[]>(
    "[\"line\\n\\u003C\\u00E9\",null]");
var newtonsoftIntegerArrayOutput = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftIntegers);
var newtonsoftStringArrayOutput = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftStrings);
var expectedBytes = new byte[] { 0, 1, 2, 254, 255 };
var systemTextJsonBytes = JsonSerializer.Deserialize<byte[]>("\"AAEC/v8=\"");
var systemTextJsonByteArrayOutput = JsonSerializer.Serialize(expectedBytes);
var newtonsoftBytes = Newtonsoft.Json.JsonConvert.DeserializeObject<byte[]>("\"AAEC/v8=\"");
var newtonsoftByteArrayOutput = Newtonsoft.Json.JsonConvert.SerializeObject(expectedBytes);
var systemTextJsonInvalidBase64Rejected = false;
var systemTextJsonNumericByteArrayRejected = false;
var newtonsoftInvalidBase64Rejected = false;
try
{
    JsonSerializer.Deserialize<byte[]>("\"not-base64\"");
}
catch (JsonException)
{
    systemTextJsonInvalidBase64Rejected = true;
}
try
{
    JsonSerializer.Deserialize<byte[]>("[0,1,2]");
}
catch (JsonException)
{
    systemTextJsonNumericByteArrayRejected = true;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<byte[]>("\"not-base64\"");
}
catch (FormatException)
{
    newtonsoftInvalidBase64Rejected = true;
}
var newtonsoftNumericBytes =
    Newtonsoft.Json.JsonConvert.DeserializeObject<byte[]>("[0,1,2]");
var arraysSucceeded = systemTextJsonIntegers?.Length == 2
    && systemTextJsonIntegers[0] == 9007199254740993L
    && systemTextJsonIntegers[1] == -9007199254740993L
    && systemTextJsonStrings?.Length == 2
    && systemTextJsonStrings[0] == "line\n<é"
    && systemTextJsonStrings[1] == null
    && systemTextJsonIntegerArrayOutput == "[9007199254740993,-9007199254740993]"
    && systemTextJsonStringArrayOutput == "[\"line\\n\\u003C\\u00E9\",null]"
    && newtonsoftIntegers?.Length == 2
    && newtonsoftIntegers[0] == 9007199254740993L
    && newtonsoftIntegers[1] == -9007199254740993L
    && newtonsoftStrings?.Length == 2
    && newtonsoftStrings[0] == "line\n<é"
    && newtonsoftStrings[1] == null
    && newtonsoftIntegerArrayOutput == "[9007199254740993,-9007199254740993]"
    && newtonsoftStringArrayOutput == "[\"line\\n<é\",null]"
    && systemTextJsonBytes?.Length == 5
    && systemTextJsonBytes[0] == 0
    && systemTextJsonBytes[1] == 1
    && systemTextJsonBytes[2] == 2
    && systemTextJsonBytes[3] == 254
    && systemTextJsonBytes[4] == 255
    && systemTextJsonByteArrayOutput == "\"AAEC/v8=\""
    && systemTextJsonInvalidBase64Rejected
    && systemTextJsonNumericByteArrayRejected
    && newtonsoftBytes?.Length == 5
    && newtonsoftBytes[0] == 0
    && newtonsoftBytes[1] == 1
    && newtonsoftBytes[2] == 2
    && newtonsoftBytes[3] == 254
    && newtonsoftBytes[4] == 255
    && newtonsoftByteArrayOutput == "\"AAEC/v8=\""
    && newtonsoftInvalidBase64Rejected
    && newtonsoftNumericBytes?.Length == 3
    && newtonsoftNumericBytes[0] == 0
    && newtonsoftNumericBytes[1] == 1
    && newtonsoftNumericBytes[2] == 2;

var systemTextJsonCharacter = JsonSerializer.Deserialize<char>("\"é\"");
var systemTextJsonCharacterOutput = JsonSerializer.Serialize('é');
var newtonsoftCharacter = Newtonsoft.Json.JsonConvert.DeserializeObject<char>("\"é\"");
var newtonsoftCharacterOutput = Newtonsoft.Json.JsonConvert.SerializeObject('é');
var newtonsoftSettingsCharacterOutput =
    Newtonsoft.Json.JsonConvert.SerializeObject('é', settings: null);
var systemTextJsonCharacters = JsonSerializer.Deserialize<char[]>("[\"a\",\"é\"]");
var systemTextJsonCharacterArrayOutput = JsonSerializer.Serialize(new[] { 'a', 'é' });
var newtonsoftCharacters =
    Newtonsoft.Json.JsonConvert.DeserializeObject<char[]>("[\"a\",\"é\"]");
var newtonsoftCharacterArrayOutput =
    Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 'a', 'é' });
var systemTextJsonEmptyCharacterRejected = false;
var systemTextJsonLongCharacterRejected = false;
var newtonsoftEmptyCharacterRejected = false;
var newtonsoftLongCharacterRejected = false;
try
{
    JsonSerializer.Deserialize<char>("\"\"");
}
catch (JsonException)
{
    systemTextJsonEmptyCharacterRejected = true;
}
try
{
    JsonSerializer.Deserialize<char>("\"ab\"");
}
catch (JsonException)
{
    systemTextJsonLongCharacterRejected = true;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<char>("\"\"");
}
catch (Newtonsoft.Json.JsonSerializationException)
{
    newtonsoftEmptyCharacterRejected = true;
}
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<char>("\"ab\"");
}
catch (Newtonsoft.Json.JsonSerializationException)
{
    newtonsoftLongCharacterRejected = true;
}
var charactersSucceeded = systemTextJsonCharacter == 'é'
    && systemTextJsonCharacterOutput == "\"\\u00E9\""
    && systemTextJsonEmptyCharacterRejected
    && systemTextJsonLongCharacterRejected
    && newtonsoftCharacter == 'é'
    && newtonsoftCharacterOutput == "\"é\""
    && newtonsoftSettingsCharacterOutput == "\"é\""
    && newtonsoftEmptyCharacterRejected
    && newtonsoftLongCharacterRejected
    && systemTextJsonCharacters?.Length == 2
    && systemTextJsonCharacters[0] == 'a'
    && systemTextJsonCharacters[1] == 'é'
    && systemTextJsonCharacterArrayOutput == "[\"a\",\"\\u00E9\"]"
    && newtonsoftCharacters?.Length == 2
    && newtonsoftCharacters[0] == 'a'
    && newtonsoftCharacters[1] == 'é'
    && newtonsoftCharacterArrayOutput == "[\"a\",\"é\"]";

var primitiveRootsSucceeded = JsonSerializer.Serialize(true) == "true"
    && JsonSerializer.Serialize(7) == "7"
    && JsonSerializer.Serialize(9007199254740993L) == "9007199254740993"
    && Newtonsoft.Json.JsonConvert.SerializeObject(true) == "true"
    && Newtonsoft.Json.JsonConvert.SerializeObject(7) == "7"
    && Newtonsoft.Json.JsonConvert.SerializeObject(9007199254740993L)
        == "9007199254740993"
    && Newtonsoft.Json.JsonConvert.SerializeObject(true, settings: null) == "true"
    && Newtonsoft.Json.JsonConvert.SerializeObject(7, settings: null) == "7"
    && Newtonsoft.Json.JsonConvert.SerializeObject(9007199254740993L, settings: null)
        == "9007199254740993";

var systemTextJsonNaNRejected = false;
var systemTextJsonInfinityRejected = false;
var systemTextJsonNamedFloatRejected = false;
var systemTextJsonQuotedFloatRejected = false;
var systemTextJsonQuotedFloatMemberRejected = false;
try
{
    JsonSerializer.Serialize(double.NaN);
}
catch (ArgumentException)
{
    systemTextJsonNaNRejected = true;
}
try
{
    JsonSerializer.Serialize(double.PositiveInfinity);
}
catch (ArgumentException)
{
    systemTextJsonInfinityRejected = true;
}
try
{
    JsonSerializer.Deserialize<double>("\"NaN\"");
}
catch (JsonException)
{
    systemTextJsonNamedFloatRejected = true;
}
try
{
    JsonSerializer.Deserialize<double>("\"1.25\"");
}
catch (JsonException exception)
{
    systemTextJsonQuotedFloatRejected = exception.Path == "$";
}
try
{
    JsonSerializer.Deserialize<FloatingPointPayload>("{\"DoubleValue\":\"1.25\"}");
}
catch (JsonException exception)
{
    systemTextJsonQuotedFloatMemberRejected = exception.Path == "$.DoubleValue";
}
var systemTextJsonOverflowingDouble = JsonSerializer.Deserialize<double>("1e400");
var newtonsoftQuotedNaN =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("\"NaN\"");
var newtonsoftBareNaN = Newtonsoft.Json.JsonConvert.DeserializeObject<double>("NaN");
var newtonsoftQuotedDouble =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("\"1.25\"");
var newtonsoftSettingsQuotedDouble =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("\"1.25\"", settings: null);
var newtonsoftOverflowingDouble =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("1e400");
var newtonsoftQuotedOverflowingDouble =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("\"1e400\"");
var systemTextJsonFloatingArray = JsonSerializer.Deserialize<double[]>("[1.25,-2.5]");
var newtonsoftFloatingArray =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double[]>("[1.25,-2.5]");
var systemTextJsonFloatingPayload = JsonSerializer.Deserialize<FloatingPointPayload>(
    "{\"DoubleValue\":1,\"SingleValue\":1.25}");
var newtonsoftFloatingPayload =
    Newtonsoft.Json.JsonConvert.DeserializeObject<FloatingPointPayload>(
        "{\"DoubleValue\":\"1.25\",\"SingleValue\":\"-2.5\"}");
var newtonsoftInvalidFloatingStringRejected = false;
try
{
    Newtonsoft.Json.JsonConvert.DeserializeObject<FloatingPointPayload>(
        "{\"DoubleValue\":\"invalid\"}");
}
catch (Newtonsoft.Json.JsonReaderException exception)
{
    newtonsoftInvalidFloatingStringRejected = exception.Path == "DoubleValue";
}
var negativeZeroPayload = new FloatingPointPayload
{
    DoubleValue = -0.0d,
    SingleValue = -0.0f,
};
var negativeZeroSingles = new[] { -0.0f, 1.0f, 2.0f, 3.0f };
var negativeZeroDoubles = new[] { -0.0d, 1.0d, 2.0d, 3.0d };
object boxedNegativeZeroSingle = -0.0f;
object boxedNegativeZeroDouble = -0.0d;
var managedNegativeZeroPreserved = IsNegativeSingleZero(negativeZeroPayload.SingleValue)
    && IsNegativeDoubleZero(negativeZeroPayload.DoubleValue)
    && IsNegativeSingleZero(negativeZeroSingles[0])
    && IsNegativeDoubleZero(negativeZeroDoubles[0])
    && IsNegativeSingleZero((float)boxedNegativeZeroSingle)
    && IsNegativeDoubleZero((double)boxedNegativeZeroDouble)
    && JsonSerializer.Serialize(-0.0f) == "-0"
    && JsonSerializer.Serialize(-0.0d) == "-0"
    && JsonSerializer.Serialize(negativeZeroPayload)
        == "{\"DoubleValue\":-0,\"SingleValue\":-0}"
    && Newtonsoft.Json.JsonConvert.SerializeObject(-0.0f) == "-0.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(-0.0d) == "-0.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(negativeZeroPayload)
        == "{\"DoubleValue\":-0.0,\"SingleValue\":-0.0}";
static bool IsNegativeSingleZero(float value) => value == 0.0f && 1.0f / value < 0.0f;
static bool IsNegativeDoubleZero(double value) => value == 0.0d && 1.0d / value < 0.0d;
var systemTextJsonFloatingWritesSucceeded = JsonSerializer.Serialize(1.25d) == "1.25"
    && JsonSerializer.Serialize(1d) == "1"
    && JsonSerializer.Serialize(1e20d) == "1E+20"
    && JsonSerializer.Serialize(1e16d) == "10000000000000000"
    && JsonSerializer.Serialize(1e17d) == "1E+17"
    && JsonSerializer.Serialize(1e-5d) == "1E-05"
    && JsonSerializer.Serialize(double.MaxValue) == "1.7976931348623157E+308"
    && JsonSerializer.Serialize(double.Epsilon) == "5E-324"
    && JsonSerializer.Serialize(1.25f) == "1.25"
    && JsonSerializer.Serialize(1e8f) == "100000000"
    && JsonSerializer.Serialize(1e-5f) == "1E-05"
    && JsonSerializer.Serialize(JsonSerializer.Deserialize<double>("-0")) == "-0"
    && JsonSerializer.Serialize(new[] { 1.25d, -2.5d }) == "[1.25,-2.5]";
var systemTextJsonFloatingReadsSucceeded =
    JsonSerializer.Deserialize<double>("1.25") == 1.25d
    && JsonSerializer.Deserialize<float>("1.25") == 1.25f
    && systemTextJsonFloatingArray?.Length == 2
    && systemTextJsonFloatingArray[0] == 1.25d
    && systemTextJsonFloatingArray[1] == -2.5d
    && systemTextJsonFloatingPayload?.DoubleValue == 1d
    && systemTextJsonFloatingPayload.SingleValue == 1.25f
    && JsonSerializer.Serialize(systemTextJsonFloatingPayload)
        == "{\"DoubleValue\":1,\"SingleValue\":1.25}"
    && systemTextJsonOverflowingDouble == double.PositiveInfinity
    && systemTextJsonNaNRejected
    && systemTextJsonInfinityRejected
    && systemTextJsonNamedFloatRejected
    && systemTextJsonQuotedFloatRejected
    && systemTextJsonQuotedFloatMemberRejected;
var newtonsoftFloatingWritesSucceeded =
    Newtonsoft.Json.JsonConvert.SerializeObject(1.25d) == "1.25"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1d) == "1.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e20d) == "1E+20"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e16d) == "10000000000000000.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e17d) == "1E+17"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e-5d) == "1E-05"
    && Newtonsoft.Json.JsonConvert.SerializeObject(double.MaxValue)
        == "1.7976931348623157E+308"
    && Newtonsoft.Json.JsonConvert.SerializeObject(double.Epsilon) == "5E-324"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1.25f) == "1.25"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e8f) == "100000000.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(1e-5f) == "1E-05"
    && Newtonsoft.Json.JsonConvert.SerializeObject(double.NaN) == "\"NaN\""
    && Newtonsoft.Json.JsonConvert.SerializeObject(double.PositiveInfinity)
        == "\"Infinity\""
    && Newtonsoft.Json.JsonConvert.SerializeObject(double.NegativeInfinity)
        == "\"-Infinity\""
    && Newtonsoft.Json.JsonConvert.SerializeObject(
        Newtonsoft.Json.JsonConvert.DeserializeObject<double>("-0")) == "-0.0"
    && Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 1.25d, -2.5d })
        == "[1.25,-2.5]";
var newtonsoftFloatingReadsSucceeded =
    Newtonsoft.Json.JsonConvert.DeserializeObject<double>("1.25") == 1.25d
    && Newtonsoft.Json.JsonConvert.DeserializeObject<float>("1.25") == 1.25f
    && newtonsoftQuotedDouble == 1.25d
    && newtonsoftSettingsQuotedDouble == 1.25d
    && newtonsoftFloatingArray?.Length == 2
    && newtonsoftFloatingArray[0] == 1.25d
    && newtonsoftFloatingArray[1] == -2.5d
    && newtonsoftFloatingPayload?.DoubleValue == 1.25d
    && newtonsoftFloatingPayload.SingleValue == -2.5f
    && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftFloatingPayload)
        == "{\"DoubleValue\":1.25,\"SingleValue\":-2.5}"
    && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftQuotedNaN) == "\"NaN\""
    && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftBareNaN) == "\"NaN\""
    && newtonsoftOverflowingDouble == double.PositiveInfinity
    && newtonsoftQuotedOverflowingDouble == double.PositiveInfinity
    && newtonsoftInvalidFloatingStringRejected;
var floatingPointSucceeded = systemTextJsonFloatingWritesSucceeded
    && systemTextJsonFloatingReadsSucceeded
    && newtonsoftFloatingWritesSucceeded
    && newtonsoftFloatingReadsSucceeded;
var systemTextJsonFloatingOutputs = JsonSerializer.Serialize(1.25d)
    + "|" + JsonSerializer.Serialize(1d)
    + "|" + JsonSerializer.Serialize(-0d)
    + "|" + JsonSerializer.Serialize(1e20d)
    + "|" + JsonSerializer.Serialize(1e-5d)
    + "|" + JsonSerializer.Serialize(1.25f)
    + "|" + JsonSerializer.Serialize(1e-5f);
var newtonsoftFloatingOutputs = Newtonsoft.Json.JsonConvert.SerializeObject(1.25d)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(1d)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(-0d)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(1e20d)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(1e-5d)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(1.25f)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(1e-5f)
    + "|" + Newtonsoft.Json.JsonConvert.SerializeObject(double.NaN);

var numericStringConversionsSucceeded = VerifyNumericStringConversions();
var booleanConversionsSucceeded = VerifyBooleanConversions();
var stringConversionsSucceeded = VerifyStringConversions();
var stringEscapingSucceeded = VerifyStringEscaping();
var newtonsoftWriterSettingsSucceeded = VerifyNewtonsoftWriterSettings();
var newtonsoftGlobalSettingsSucceeded = VerifyNewtonsoftGlobalSettings();
var newtonsoftStructuralSettingsSucceeded = VerifyNewtonsoftStructuralSettings();
var duplicateMemberNamesSucceeded = VerifyDuplicateMemberNames();
var characterNumericConversionsSucceeded = VerifyCharacterNumericConversions();
var namedFloatingContainersSucceeded = VerifyNamedFloatingContainers();
var emptyInputSemanticsSucceeded = VerifyEmptyInputSemantics();
var explicitMemberOrderingSucceeded = VerifyExplicitMemberOrdering();
var requiredMembersSucceeded = VerifyRequiredMembers();
var newtonsoftNullValueHandlingSucceeded = VerifyNewtonsoftNullValueHandling();
var newtonsoftDefaultValueHandlingSucceeded = VerifyNewtonsoftDefaultValueHandling();
var systemTextJsonNumberHandlingSucceeded = VerifySystemTextJsonNumberHandling();
var systemTextJsonTypeNumberHandlingSucceeded = VerifySystemTextJsonTypeNumberHandling();
var systemTextJsonUnmappedMemberHandlingSucceeded = VerifySystemTextJsonUnmappedMemberHandling();
var systemTextJsonOptionsSucceeded = VerifySystemTextJsonOptions();
var systemTextJsonReferenceHandlingSucceeded = VerifySystemTextJsonReferenceHandling();
var systemTextJsonMemberSelectionOptionsSucceeded = VerifySystemTextJsonMemberSelectionOptions();
var systemTextJsonIgnoreNullValuesSucceeded = VerifySystemTextJsonIgnoreNullValues();
var systemTextJsonReaderSyntaxAndDepthOptionsSucceeded = VerifySystemTextJsonReaderSyntaxAndDepthOptions();
var newtonsoftMissingMemberHandlingSucceeded = VerifyNewtonsoftMissingMemberHandling();
var newtonsoftItemRequiredSucceeded = VerifyNewtonsoftItemRequired();
var newtonsoftItemNullValueHandlingSucceeded = VerifyNewtonsoftItemNullValueHandling();
var newtonsoftJsonObjectMetadataSucceeded = VerifyNewtonsoftJsonObjectMetadata();
var newtonsoftNamingStrategiesSucceeded = VerifyNewtonsoftNamingStrategies();
var fieldIdentitySucceeded = FieldIdentityBrowserVerification.Verify();
var valueTypeConstructorsSucceeded = ValueTypeConstructorBrowserVerification.Verify();

var systemTextJsonPrivateDefaultRejected = false;
try
{
    JsonSerializer.Deserialize<SystemTextJsonPrivateDefaultPayload>("{\"Name\":\"input\"}");
}
catch (NotSupportedException)
{
    systemTextJsonPrivateDefaultRejected = true;
}
var systemTextJsonParameterized = JsonSerializer.Deserialize<SystemTextJsonParameterizedPayload>(
    "{\"display_name\":\"input\",\"Count\":3}");
var systemTextJsonAttributed = JsonSerializer.Deserialize<SystemTextJsonAttributedPrivatePayload>(
    "{\"Name\":\"input\"}");
var newtonsoftPrivateDefault =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftPrivateDefaultPayload>(
        "{\"Name\":\"input\"}");
var newtonsoftParameterized =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftParameterizedPayload>(
        "{\"display_name\":\"input\",\"Count\":3}");
var newtonsoftAttributed =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftAttributedPrivatePayload>(
        "{\"Name\":\"input\"}",
        settings: null);
var constructorsSucceeded = systemTextJsonPrivateDefaultRejected
    && systemTextJsonParameterized?.Name == "ctor:input"
    && systemTextJsonParameterized.SetterCalls == 0
    && systemTextJsonParameterized.Count == 3
    && systemTextJsonAttributed?.Name == "attributed:input"
    && newtonsoftPrivateDefault?.ConstructorMarker == "private-default"
    && newtonsoftPrivateDefault.Name == "input"
    && newtonsoftParameterized?.Name == "ctor:input"
    && newtonsoftParameterized.SetterCalls == 0
    && newtonsoftParameterized.Count == 3
    && newtonsoftAttributed?.Name == "attributed:input";

var systemTextJsonRecord = JsonSerializer.Deserialize<SystemTextJsonRecordPayload>(
    "{\"display_name\":\"record\",\"Count\":3}");
var systemTextJsonOptional = JsonSerializer.Deserialize<SystemTextJsonOptionalConstructorPayload>("{}");
var newtonsoftRecord =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRecordPayload>(
        "{\"display_name\":\"record\",\"Count\":3}");
var newtonsoftOptional =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftOptionalConstructorPayload>("{}");
var recordsAndConstructorDefaultsSucceeded = systemTextJsonRecord?.Name == "record"
    && systemTextJsonRecord.Count == 3
    && JsonSerializer.Serialize(systemTextJsonRecord)
        == "{\"display_name\":\"record\",\"Count\":3}"
    && systemTextJsonOptional?.Name == "system-text-default"
    && systemTextJsonOptional.Count == 17
    && systemTextJsonOptional.Active
    && systemTextJsonOptional.Large == 9007199254740993L
    && systemTextJsonOptional.Absent == null
    && systemTextJsonOptional.Marker == 'Ω'
    && systemTextJsonOptional.SingleValue == 1.25f
    && systemTextJsonOptional.DoubleValue == -2.5d
    && newtonsoftRecord?.Name == "record"
    && newtonsoftRecord.Count == 3
    && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftRecord)
        == "{\"display_name\":\"record\",\"Count\":3}"
    && newtonsoftOptional != null
    && newtonsoftOptional.Name == null
    && newtonsoftOptional.Count == 0
    && !newtonsoftOptional.Active
    && newtonsoftOptional.Large == 0
    && newtonsoftOptional.Absent == null;

var systemTextJsonNonPublic = JsonSerializer.Deserialize<SystemTextJsonNonPublicIncludePayload>(
    "{\"private_field\":7,\"private_property\":\"input\"}");
var newtonsoftNonPublic =
    Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNonPublicMemberPayload>(
        "{\"private_field\":7,\"private_property\":\"input\"}");
var nonPublicMembersSucceeded = systemTextJsonNonPublic?.PrivatePropertyValue == "set:input"
    && JsonSerializer.Serialize(systemTextJsonNonPublic)
        == "{\"private_property\":\"set:input\",\"private_field\":7}"
    && newtonsoftNonPublic?.PrivatePropertyValue == "set:input"
    && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftNonPublic)
        == "{\"private_field\":7,\"private_property\":\"set:input\"}";

Document.Body.SetAttribute("data-fecs-json-summary", payload?.Summary ?? "null");
Document.Body.SetAttribute("data-fecs-json-output", serialized);
Document.Body.SetAttribute(
    "data-fecs-json-temporal-status",
    temporalSucceeded ? "ready" : "failed");
Document.Body.SetAttribute("data-fecs-json-temporal-output", systemTextJsonTemporalOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-temporal-output", newtonsoftTemporalOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-summary", newtonsoftPayload?.Summary ?? "null");
Document.Body.SetAttribute("data-fecs-newtonsoft-output", newtonsoftSerialized);
Document.Body.SetAttribute("data-fecs-json-inherited-output", systemTextJsonInheritedOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-inherited-output", newtonsoftInheritedOutput);
Document.Body.SetAttribute("data-fecs-json-shadowed-output", systemTextJsonShadowedOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-shadowed-output", newtonsoftShadowedOutput);
Document.Body.SetAttribute("data-fecs-json-integer-array-output", systemTextJsonIntegerArrayOutput);
Document.Body.SetAttribute("data-fecs-json-string-array-output", systemTextJsonStringArrayOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-integer-array-output", newtonsoftIntegerArrayOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-string-array-output", newtonsoftStringArrayOutput);
Document.Body.SetAttribute("data-fecs-json-byte-array-output", systemTextJsonByteArrayOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-byte-array-output", newtonsoftByteArrayOutput);
Document.Body.SetAttribute("data-fecs-json-character-output", systemTextJsonCharacterOutput);
Document.Body.SetAttribute("data-fecs-newtonsoft-character-output", newtonsoftCharacterOutput);
var collectionsSucceeded = CollectionBrowserVerification.Verify();
Document.Body.SetAttribute(
    "data-fecs-json-character-array-output",
    systemTextJsonCharacterArrayOutput);
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-character-array-output",
    newtonsoftCharacterArrayOutput);
Document.Body.SetAttribute(
    "data-fecs-json-character-value-status",
    systemTextJsonCharacter == 'é' ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-character-value-status",
    newtonsoftCharacter == 'é' ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-character-error-status",
    systemTextJsonEmptyCharacterRejected && systemTextJsonLongCharacterRejected
        ? "ready"
        : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-character-error-status",
    newtonsoftEmptyCharacterRejected && newtonsoftLongCharacterRejected
        ? "ready"
        : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-constructor-status",
    constructorsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-value-constructor-status",
    valueTypeConstructorsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-record-constructor-default-status",
    recordsAndConstructorDefaultsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-non-public-status",
    nonPublicMembersSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-character-status",
    charactersSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-primitive-root-status",
    primitiveRootsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-floating-point-status",
    floatingPointSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-numeric-string-status",
    numericStringConversionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-boolean-conversion-status",
    booleanConversionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-string-conversion-status",
    stringConversionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-string-escaping-status",
    stringEscapingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-writer-settings-status",
    newtonsoftWriterSettingsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-global-settings-status",
    newtonsoftGlobalSettingsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-structural-settings-status",
    newtonsoftStructuralSettingsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-duplicate-member-name-status",
    duplicateMemberNamesSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-character-numeric-status",
    characterNumericConversionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-named-floating-container-status",
    namedFloatingContainersSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-empty-input-status",
    emptyInputSemanticsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-explicit-order-status",
    explicitMemberOrderingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-required-member-status",
    requiredMembersSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-null-value-handling-status",
    newtonsoftNullValueHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-default-value-handling-status",
    newtonsoftDefaultValueHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-opt-in-status",
    NewtonsoftOptInBrowserVerification.Succeeded ? "ready" : "failed");
var reflectedEnumNames = typeof(JsonUInt64Enum).GetEnumNames();
var reflectedEnumValues = (JsonUInt64Enum[])typeof(JsonUInt64Enum).GetEnumValues();
var reflectedUnderlyingValues =
    (ulong[])typeof(JsonUInt64Enum).GetEnumValuesAsUnderlyingType();
var enumReflectionSucceeded = typeof(JsonUInt64Enum).IsEnum
    && !typeof(string).IsEnum
    && typeof(JsonUInt64Enum).GetEnumUnderlyingType() == typeof(ulong)
    && reflectedEnumNames.Length == 2
    && reflectedEnumNames[0] == nameof(JsonUInt64Enum.Large)
    && reflectedEnumNames[1] == nameof(JsonUInt64Enum.Maximum)
    && reflectedEnumValues.Length == 2
    && reflectedEnumValues[0] == JsonUInt64Enum.Large
    && reflectedEnumValues[1] == JsonUInt64Enum.Maximum
    && reflectedUnderlyingValues.Length == 2
    && reflectedUnderlyingValues[1] == ulong.MaxValue
    && typeof(JsonUInt64Enum).GetEnumName(JsonUInt64Enum.Maximum)
        == nameof(JsonUInt64Enum.Maximum)
    && typeof(JsonUInt64Enum).IsEnumDefined(nameof(JsonUInt64Enum.Large))
    && !typeof(JsonUInt64Enum).IsEnumDefined((JsonUInt64Enum)2);
var enumSucceeded = EnumBrowserVerification.Succeeded && enumReflectionSucceeded;
Document.Body.SetAttribute(
    "data-fecs-json-enum-status",
    enumSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-enum-failure",
    EnumBrowserVerification.Failure ?? string.Empty);
Document.Body.SetAttribute(
    "data-fecs-json-number-handling-status",
    systemTextJsonNumberHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-type-number-handling-status",
    systemTextJsonTypeNumberHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-unmapped-member-handling-status",
    systemTextJsonUnmappedMemberHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-options-status",
    systemTextJsonOptionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-reference-handling-status",
    systemTextJsonReferenceHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-member-selection-options-status",
    systemTextJsonMemberSelectionOptionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-ignore-null-values-status",
    systemTextJsonIgnoreNullValuesSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-reader-syntax-depth-options-status",
    systemTextJsonReaderSyntaxAndDepthOptionsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-missing-member-handling-status",
    newtonsoftMissingMemberHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-item-required-status",
    newtonsoftItemRequiredSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-item-null-value-handling-status",
    newtonsoftItemNullValueHandlingSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-json-object-metadata-status",
    newtonsoftJsonObjectMetadataSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-naming-strategy-status",
    newtonsoftNamingStrategiesSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-field-identity-status",
    fieldIdentitySucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-floating-write-status",
    systemTextJsonFloatingWritesSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-floating-read-status",
    systemTextJsonFloatingReadsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-floating-write-status",
    newtonsoftFloatingWritesSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-floating-read-status",
    newtonsoftFloatingReadsSucceeded ? "ready" : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-managed-negative-zero-status",
    managedNegativeZeroPreserved ? "ready" : "known-runtime-gap");
Document.Body.SetAttribute(
    "data-fecs-json-floating-outputs",
    systemTextJsonFloatingOutputs);
Document.Body.SetAttribute(
    "data-fecs-newtonsoft-floating-outputs",
    newtonsoftFloatingOutputs);
Document.Body.SetAttribute(
    "data-fecs-json-location-status",
    systemTextJsonSyntaxLocationMatched && systemTextJsonUtf8LocationMatched
        && systemTextJsonOverflowRejected
        && newtonsoftSyntaxLocationMatched && newtonsoftOverflowRejected
        && newtonsoftSettingsLocationsMatched
        ? "ready"
        : "failed");
Document.Body.SetAttribute(
    "data-fecs-json-collection-status",
    collectionsSucceeded
        ? "ready"
        : CollectionBrowserVerification.Failure);
Document.Body.SetAttribute(
    "data-fecs-status",
    systemTextJsonSucceeded && newtonsoftSucceeded && inheritanceSucceeded
        && shadowedMembersSucceeded && arraysSucceeded && constructorsSucceeded
        && valueTypeConstructorsSucceeded
        && recordsAndConstructorDefaultsSucceeded && nonPublicMembersSucceeded
        && charactersSucceeded && primitiveRootsSucceeded && floatingPointSucceeded
        && numericStringConversionsSucceeded && booleanConversionsSucceeded
        && stringConversionsSucceeded && stringEscapingSucceeded
        && newtonsoftWriterSettingsSucceeded
        && newtonsoftGlobalSettingsSucceeded
        && newtonsoftStructuralSettingsSucceeded
        && duplicateMemberNamesSucceeded
        && characterNumericConversionsSucceeded
        && namedFloatingContainersSucceeded && emptyInputSemanticsSucceeded
        && explicitMemberOrderingSucceeded && requiredMembersSucceeded
        && newtonsoftNullValueHandlingSucceeded
        && newtonsoftDefaultValueHandlingSucceeded
        && NewtonsoftOptInBrowserVerification.Succeeded
        && enumSucceeded
        && systemTextJsonNumberHandlingSucceeded
        && systemTextJsonTypeNumberHandlingSucceeded
        && systemTextJsonUnmappedMemberHandlingSucceeded
        && systemTextJsonOptionsSucceeded
        && systemTextJsonReferenceHandlingSucceeded
        && systemTextJsonMemberSelectionOptionsSucceeded
        && systemTextJsonIgnoreNullValuesSucceeded
        && systemTextJsonReaderSyntaxAndDepthOptionsSucceeded
        && newtonsoftMissingMemberHandlingSucceeded
        && newtonsoftItemRequiredSucceeded
        && newtonsoftItemNullValueHandlingSucceeded
        && newtonsoftJsonObjectMetadataSucceeded
        && newtonsoftNamingStrategiesSucceeded
        && fieldIdentitySucceeded
        && collectionsSucceeded
        ? "ready"
        : "failed");

static bool VerifySystemTextJsonNumberHandling()
{
    var payload = JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
        "{\"QuotedInteger\":\"9007199254740993\",\"QuotedDouble\":\"1.25\","
            + "\"ReadQuotedWriteNumber\":\"7\",\"NamedDouble\":\"NaN\"}");
    if (payload == null) return false;

    var namedThroughStringHandling =
        JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
            "{\"QuotedDouble\":\"NaN\"}");
    var invalidQuotedNumberRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
            "{\"QuotedDouble\":\"not-a-number\"}");
    }
    catch (JsonException exception)
    {
        invalidQuotedNumberRejected = exception.Path == "$.QuotedDouble";
    }

    return payload.QuotedInteger == 9007199254740993L
        && payload.QuotedDouble == 1.25d
        && payload.ReadQuotedWriteNumber == 7
        && double.IsNaN(payload.NamedDouble)
        && namedThroughStringHandling != null
        && double.IsNaN(namedThroughStringHandling.QuotedDouble)
        && JsonSerializer.Serialize(payload)
            == "{\"QuotedInteger\":\"9007199254740993\",\"QuotedDouble\":\"1.25\","
                + "\"ReadQuotedWriteNumber\":7,\"NamedDouble\":\"NaN\"}"
        && invalidQuotedNumberRejected;
}

static bool VerifySystemTextJsonTypeNumberHandling()
{
    var payload = JsonSerializer.Deserialize<SystemTextJsonTypeNumberHandlingPayload>(
        "{\"QuotedInteger\":\"9007199254740993\",\"NamedDouble\":\"NaN\",\"StrictInteger\":5}");
    if (payload == null) return false;

    var strictQuotedNumberRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonTypeNumberHandlingPayload>(
            "{\"StrictInteger\":\"5\"}");
    }
    catch (JsonException exception)
    {
        strictQuotedNumberRejected = exception.Path == "$.StrictInteger";
    }

    return payload.QuotedInteger == 9007199254740993L
        && double.IsNaN(payload.NamedDouble)
        && payload.StrictInteger == 5
        && JsonSerializer.Serialize(payload)
            == "{\"QuotedInteger\":\"9007199254740993\",\"NamedDouble\":\"NaN\",\"StrictInteger\":5}"
        && strictQuotedNumberRejected;
}

static bool VerifySystemTextJsonUnmappedMemberHandling()
{
    var skipped = JsonSerializer.Deserialize<SystemTextJsonSkipUnmappedPayload>(
        "{\"Known\":6,\"Unknown\":42}");
    var payload = JsonSerializer.Deserialize<SystemTextJsonDisallowUnmappedPayload>(
        "{\"Known\":7}");
    var unknownRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonDisallowUnmappedPayload>(
            "{\"Known\":7,\"Unknown\":42}");
    }
    catch (JsonException exception)
    {
        unknownRejected = exception.Path == "$.Unknown";
    }

    return skipped?.Known == 6
        && payload?.Known == 7
        && JsonSerializer.Serialize(payload) == "{\"Known\":7}"
        && unknownRejected;
}

static bool VerifySystemTextJsonOptions()
{
    const string defaultJson =
        "{\"URLValue\":\"url\",\"SHA512Managed\":7,\"ExplicitName\":\"explicit\",\"Count\":3}";
    var defaultOptions = new JsonSerializerOptions();
    var defaultPayload = JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
        defaultJson,
        defaultOptions);
    if (defaultPayload?.URLValue != "url"
        || JsonSerializer.Serialize(defaultPayload, defaultOptions) != defaultJson) return false;
    var lockedOptionMutationRejected = false;
    try
    {
        defaultOptions.WriteIndented = true;
    }
    catch (InvalidOperationException)
    {
        lockedOptionMutationRejected = true;
    }
    if (!defaultOptions.IsReadOnly || !lockedOptionMutationRejected) return false;
    var explicitDefaultOptions = new JsonSerializerOptions
    {
        AllowTrailingCommas = false,
        IncludeFields = false,
        WriteIndented = false,
    };
    if (JsonSerializer.Serialize(defaultPayload, explicitDefaultOptions) != defaultJson) return false;

    if (!VerifySystemTextJsonNamingPolicy(
            JsonNamingPolicy.CamelCase,
            "{\"urlValue\":\"url\",\"shA512Managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}")
        || !VerifySystemTextJsonNamingPolicy(
            JsonNamingPolicy.SnakeCaseLower,
            "{\"url_value\":\"url\",\"sha512_managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}")
        || !VerifySystemTextJsonNamingPolicy(
            JsonNamingPolicy.SnakeCaseUpper,
            "{\"URL_VALUE\":\"url\",\"SHA512_MANAGED\":7,\"ExplicitName\":\"explicit\",\"COUNT\":3}")
        || !VerifySystemTextJsonNamingPolicy(
            JsonNamingPolicy.KebabCaseLower,
            "{\"url-value\":\"url\",\"sha512-managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}")
        || !VerifySystemTextJsonNamingPolicy(
            JsonNamingPolicy.KebabCaseUpper,
            "{\"URL-VALUE\":\"url\",\"SHA512-MANAGED\":7,\"ExplicitName\":\"explicit\",\"COUNT\":3}")) return false;

    var insensitiveOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };
    var insensitive = JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
        "{\"urlvalue\":\"first\",\"URLVALUE\":\"last\",\"explicitname\":\"explicit\"}",
        insensitiveOptions);
    if (insensitive?.URLValue != "last" || insensitive.ExplicitValue != "explicit") return false;
    var required = JsonSerializer.Deserialize<SystemTextJsonOptionsRequiredPayload>(
        "{\"value\":\"required\"}",
        insensitiveOptions);
    if (required?.Value != "required") return false;

    var constructorOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    var constructed = JsonSerializer.Deserialize<SystemTextJsonOptionsConstructorPayload>(
        "{\"url_value\":\"constructed\"}",
        constructorOptions);
    if (constructed?.URLValue != "constructed") return false;

    var numberOptions = new JsonSerializerOptions
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
            | JsonNumberHandling.WriteAsString,
    };
    var numbers = JsonSerializer.Deserialize<SystemTextJsonOptionsNumberPayload>(
        "{\"Count\":\"7\",\"StrictCount\":8}",
        numberOptions);
    if (numbers?.Count != 7 || numbers.StrictCount != 8
        || JsonSerializer.Serialize(numbers, numberOptions)
            != "{\"Count\":\"7\",\"StrictCount\":8}") return false;

    var disallowOptions = new JsonSerializerOptions
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    var unknownRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
            "{\"Unknown\":1}",
            disallowOptions);
    }
    catch (JsonException exception)
    {
        unknownRejected = exception.Path == "$.Unknown";
    }
    var attributedSkip = JsonSerializer.Deserialize<SystemTextJsonSkipUnmappedPayload>(
        "{\"Known\":6,\"Unknown\":42}",
        disallowOptions);
    if (!unknownRejected || attributedSkip?.Known != 6) return false;

    var webOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var web = JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
        "{\"urlvalue\":\"web\",\"count\":\"9\"}",
        webOptions);
    if (web?.URLValue != "web" || web.Count != 9
        || JsonSerializer.Serialize(web, webOptions)
            != "{\"urlValue\":\"web\",\"shA512Managed\":0,\"ExplicitName\":null,\"count\":9}") {
        return false;
    }

    var camelOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
    var namingCollisionRejected = false;
    var namingWriteCollisionRejected = false;
    var caseCollisionRejected = false;
    var customPolicyRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonOptionsNamingCollisionPayload>(
            "{}",
            camelOptions);
    }
    catch (InvalidOperationException)
    {
        namingCollisionRejected = true;
    }
    try
    {
        JsonSerializer.Serialize(
            new SystemTextJsonOptionsNamingCollisionPayload(),
            camelOptions);
    }
    catch (InvalidOperationException)
    {
        namingWriteCollisionRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonOptionsCaseCollisionPayload>(
            "{}",
            insensitiveOptions);
    }
    catch (InvalidOperationException)
    {
        caseCollisionRejected = true;
    }
    var indentedOptions = new JsonSerializerOptions { WriteIndented = true };
    var indented = JsonSerializer.Serialize(
        new SystemTextJsonDepthOptionsPayload
        {
            Value = 1,
            Child = new SystemTextJsonDepthOptionsPayload { Value = 2 },
        },
        indentedOptions);
    var indentedArray = JsonSerializer.Serialize(new[] { 1, 2 }, indentedOptions);
    var indentedEmptyArray = JsonSerializer.Serialize(Array.Empty<int>(), indentedOptions);
    var indentedRead = JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
        "{\"Value\":3,\"Child\":null}",
        indentedOptions);
    var indentedObjectMatched =
        indented == "{\r\n  \"Value\": 1,\r\n  \"Child\": {\r\n    \"Value\": 2,\r\n    \"Child\": null\r\n  }\r\n}";
    var indentedArrayMatched = indentedArray == "[\r\n  1,\r\n  2\r\n]";
    var indentedEmptyArrayMatched = indentedEmptyArray == "[]";
    var indentedReadMatched = indentedRead?.Value == 3 && indentedRead.Child == null;
    Document.Body.SetAttribute(
        "data-fecs-json-write-indented-object-status",
        indentedObjectMatched ? "ready" : "failed");
    Document.Body.SetAttribute(
        "data-fecs-json-write-indented-array-status",
        indentedArrayMatched ? "ready" : "failed");
    Document.Body.SetAttribute(
        "data-fecs-json-write-indented-empty-array-status",
        indentedEmptyArrayMatched ? "ready" : "failed");
    Document.Body.SetAttribute(
        "data-fecs-json-write-indented-read-status",
        indentedReadMatched ? "ready" : "failed");
    try
    {
        JsonSerializer.Serialize(
            new SystemTextJsonOptionsPayload(),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = new SystemTextJsonCustomNamingPolicy(),
            });
    }
    catch (NotSupportedException)
    {
        customPolicyRejected = true;
    }

    return namingCollisionRejected && namingWriteCollisionRejected
        && caseCollisionRejected && customPolicyRejected
        && indentedObjectMatched
        && indentedArrayMatched
        && indentedEmptyArrayMatched
        && indentedReadMatched;
}

static bool VerifySystemTextJsonReferenceHandling()
{
    var loop = new SystemTextJsonDepthOptionsPayload { Value = 1 };
    loop.Child = loop;
    var defaultRejected = false;
    try
    {
        JsonSerializer.Serialize(loop);
    }
    catch (JsonException)
    {
        defaultRejected = true;
    }

    var ignoreCyclesOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };
    var ignored = JsonSerializer.Serialize(loop, ignoreCyclesOptions);
    var read = JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
        "{\"Value\":3,\"Child\":null}",
        ignoreCyclesOptions);

    var ignoreNullCyclesOptions = new JsonSerializerOptions
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    var ignoredWithNullPolicy = JsonSerializer.Serialize(loop, ignoreNullCyclesOptions);

    var shared = new SystemTextJsonDepthOptionsPayload { Value = 2 };
    var pair = new SystemTextJsonReferencePairPayload
    {
        First = shared,
        Second = shared,
    };
    var sharedOutput = JsonSerializer.Serialize(pair, ignoreCyclesOptions);

    var preserveRejected = false;
    var preserveReadRejected = false;
    try
    {
        JsonSerializer.Serialize(
            loop,
            new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve });
    }
    catch (NotSupportedException)
    {
        preserveRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
            "{\"Value\":1}",
            new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.Preserve });
    }
    catch (NotSupportedException)
    {
        preserveReadRejected = true;
    }

    return defaultRejected
        && ignored == "{\"Value\":1,\"Child\":null}"
        && read?.Value == 3
        && read.Child == null
        && ignoredWithNullPolicy == "{\"Value\":1}"
        && sharedOutput == "{\"First\":{\"Value\":2,\"Child\":null},"
            + "\"Second\":{\"Value\":2,\"Child\":null}}"
        && preserveRejected
        && preserveReadRejected;
}

static bool VerifySystemTextJsonNamingPolicy(JsonNamingPolicy policy, string json)
{
    var options = new JsonSerializerOptions { PropertyNamingPolicy = policy };
    var payload = JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(json, options);
    return payload?.URLValue == "url"
        && payload.SHA512Managed == 7
        && payload.ExplicitValue == "explicit"
        && payload.Count == 3
        && JsonSerializer.Serialize(payload, options) == json;
}

static bool VerifySystemTextJsonMemberSelectionOptions()
{
    var defaultIgnoringOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        IgnoreReadOnlyFields = true,
        IgnoreReadOnlyProperties = true,
        IncludeFields = true,
    };
    var payload = new SystemTextJsonMemberSelectionOptionsPayload
    {
        Value = "value",
    };
    if (JsonSerializer.Serialize(payload, defaultIgnoringOptions)
        != "{\"Value\":\"value\",\"NeverIgnoredDefault\":0,"
            + "\"NeverIgnoredReadOnlyProperty\":\"kept\",\"IncludedField\":3,"
            + "\"NeverIgnoredReadOnlyField\":5}") return false;

    var nullIgnoringOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    if (JsonSerializer.Serialize(payload, nullIgnoringOptions)
        != "{\"Value\":\"value\",\"ZeroValue\":0,\"FalseValue\":false,"
            + "\"ReadOnlyProperty\":\"read-only\",\"NeverIgnoredDefault\":0,"
            + "\"NeverIgnoredReadOnlyProperty\":\"kept\"}") return false;

    var deserialized = JsonSerializer.Deserialize<SystemTextJsonMemberSelectionOptionsPayload>(
        "{\"Value\":\"incoming\",\"IncludedField\":9,\"ReadOnlyField\":99,"
            + "\"DefaultField\":8,\"NeverIgnoredReadOnlyField\":100}",
        defaultIgnoringOptions);
    if (deserialized?.Value != "incoming"
        || deserialized.IncludedField != 9
        || deserialized.ReadOnlyField != 4
        || deserialized.DefaultField != 8
        || deserialized.NeverIgnoredReadOnlyField != 5) return false;

    var fieldsExcluded = JsonSerializer.Deserialize<SystemTextJsonMemberSelectionOptionsPayload>(
        "{\"IncludedField\":9}",
        new JsonSerializerOptions());
    if (fieldsExcluded?.IncludedField != 3) return false;

    var includeFieldsOptions = new JsonSerializerOptions { IncludeFields = true };
    var writeCollisionRejected = false;
    var readCollisionRejected = false;
    try
    {
        JsonSerializer.Serialize(
            new SystemTextJsonMemberSelectionCollisionPayload(),
            includeFieldsOptions);
    }
    catch (InvalidOperationException)
    {
        writeCollisionRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonMemberSelectionCollisionPayload>(
            "{}",
            includeFieldsOptions);
    }
    catch (InvalidOperationException)
    {
        readCollisionRejected = true;
    }

    return writeCollisionRejected && readCollisionRejected;
}

static bool VerifySystemTextJsonIgnoreNullValues()
{
#pragma warning disable SYSLIB0020
    var options = new JsonSerializerOptions
    {
        IgnoreNullValues = true,
        IncludeFields = true,
    };
#pragma warning restore SYSLIB0020
    var writePayload = new SystemTextJsonIgnoreNullValuesPayload
    {
        Value = null,
        Required = null,
        Never = null,
        NullValue = null,
        Tracked = null,
        Field = null,
        NeverField = null,
    };
    if (JsonSerializer.Serialize(writePayload, options)
        != "{\"Never\":null,\"NeverField\":null}") return false;

    var readPayload = JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesPayload>(
        "{\"Value\":null,\"Required\":null,\"Never\":null,\"NullValue\":null,"
            + "\"Tracked\":null,\"Field\":null,\"NeverField\":null}",
        options);
    if (readPayload?.Value != "constructed"
        || readPayload.Required != null
        || readPayload.Never != null
        || readPayload.NullValue != null
        || readPayload.Tracked != "tracked"
        || readPayload.SetterCalls != 0
        || readPayload.Field != "field"
        || readPayload.NeverField != null) return false;

    var missingRequiredRejected = false;
    var valueNullRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesPayload>("{}", options);
    }
    catch (JsonException)
    {
        missingRequiredRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
            "{\"Value\":null}",
            options);
    }
    catch (JsonException)
    {
        valueNullRejected = true;
    }

    var constructor = JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesConstructorPayload>(
        "{\"Value\":null}",
        options);
    var arrayJson = JsonSerializer.Serialize(new string?[] { null, "value" }, options);
    var array = JsonSerializer.Deserialize<string?[]>("[null,\"value\"]", options);
    var nullJson = JsonSerializer.Serialize<string?>(null, options);

    var ignoreFirstConflictRejected = false;
    var defaultFirstConflictRejected = false;
    var usedMutationRejected = false;
#pragma warning disable SYSLIB0020
    try
    {
        options.IgnoreNullValues = false;
    }
    catch (InvalidOperationException)
    {
        usedMutationRejected = true;
    }
    var ignoreFirst = new JsonSerializerOptions { IgnoreNullValues = true };
    try
    {
        ignoreFirst.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }
    catch (InvalidOperationException)
    {
        ignoreFirstConflictRejected = true;
    }
    var defaultFirst = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    try
    {
        defaultFirst.IgnoreNullValues = true;
    }
    catch (InvalidOperationException)
    {
        defaultFirstConflictRejected = true;
    }
#pragma warning restore SYSLIB0020

    return missingRequiredRejected
        && valueNullRejected
        && constructor?.Value == "fallback"
        && arrayJson == "[null,\"value\"]"
        && array != null
        && array.Length == 2
        && array[0] == null
        && array[1] == "value"
        && nullJson == "null"
        && usedMutationRejected
        && ignoreFirstConflictRejected
        && defaultFirstConflictRejected;
}

static bool VerifySystemTextJsonReaderSyntaxAndDepthOptions()
{
    var trailingOptions = new JsonSerializerOptions { AllowTrailingCommas = true };
    var trailingObject = JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
        "{\"Value\":7,}",
        trailingOptions);
    var trailingArray = JsonSerializer.Deserialize<int[]>("[1,2,]", trailingOptions);
    if (trailingObject?.Value != 7
        || trailingArray == null
        || trailingArray.Length != 2
        || trailingArray[0] != 1
        || trailingArray[1] != 2) return false;

    var defaultObjectTrailingRejected = false;
    var defaultArrayTrailingRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>("{\"Value\":7,}");
    }
    catch (JsonException)
    {
        defaultObjectTrailingRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<int[]>("[1,2,]");
    }
    catch (JsonException)
    {
        defaultArrayTrailingRejected = true;
    }
    if (!defaultObjectTrailingRejected || !defaultArrayTrailingRejected) return false;

    var commentOptions = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
    var commented = JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
        "/* before */ { /* property */ \"Value\": /* value */ 9 /* after value */,"
            + " /* next */ \"Text\":\"// literal /* text */\","
            + " // trailing\r\n } /* after */",
        commentOptions);
    if (commented?.Value != 9 || commented.Text != "// literal /* text */") return false;
    var beforeColonRejected = false;
    var defaultCommentRejected = false;
    var unterminatedCommentRejected = false;
    var doubledCommaRejected = false;
    var allowModeRejected = false;
    var negativeDepthRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
            "{\"Value\"/* before colon */:9}",
            commentOptions);
    }
    catch (JsonException)
    {
        beforeColonRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
            "/* rejected */ {\"Value\":9}");
    }
    catch (JsonException)
    {
        defaultCommentRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
            "/* unterminated",
            commentOptions);
    }
    catch (JsonException)
    {
        unterminatedCommentRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
            "{\"Value\":9,,}",
            commentOptions);
    }
    catch (JsonException)
    {
        doubledCommaRejected = true;
    }
    try
    {
        _ = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Allow };
    }
    catch (ArgumentOutOfRangeException)
    {
        allowModeRejected = true;
    }
    try
    {
        _ = new JsonSerializerOptions { MaxDepth = -1 };
    }
    catch (ArgumentOutOfRangeException)
    {
        negativeDepthRejected = true;
    }
    if (!beforeColonRejected
        || !defaultCommentRejected
        || !unterminatedCommentRejected
        || !doubledCommaRejected
        || !allowModeRejected
        || !negativeDepthRejected) return false;

    var writeDepthOptions = new JsonSerializerOptions { MaxDepth = 2 };
    var readDepthOptions = new JsonSerializerOptions { MaxDepth = 1 };
    var shallow = new SystemTextJsonDepthOptionsPayload { Value = 1 };
    if (JsonSerializer.Serialize(shallow, writeDepthOptions)
        != "{\"Value\":1,\"Child\":null}") return false;
    var shallowRead = JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
        "{\"Value\":1,\"Child\":null}",
        readDepthOptions);
    if (shallowRead?.Value != 1 || shallowRead.Child != null) return false;

    var writeDepthRejected = false;
    var readDepthRejected = false;
    try
    {
        JsonSerializer.Serialize(
            new SystemTextJsonDepthOptionsPayload
            {
                Value = 1,
                Child = new SystemTextJsonDepthOptionsPayload { Value = 2 },
            },
            writeDepthOptions);
    }
    catch (JsonException)
    {
        writeDepthRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
            "{\"Value\":1,\"Child\":{\"Value\":2}}",
            readDepthOptions);
    }
    catch (JsonException exception)
    {
        readDepthRejected = exception.Path == "$.Child";
    }

    return writeDepthRejected && readDepthRejected;
}

static bool VerifyNewtonsoftMissingMemberHandling()
{
    var ignored = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftIgnoreMissingMemberPayload>(
        "{\"Known\":6,\"Unknown\":42}");
    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
        "{\"Known\":7}",
        settings: null);
    var unknownRejected = false;
    var settingsUnknownRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
            "{\"Known\":7,\"Unknown\":42}");
    }
    catch (Newtonsoft.Json.JsonSerializationException exception)
    {
        unknownRejected = exception.Message
            == "Could not find member 'Unknown' on object of type "
                + "'NewtonsoftErrorMissingMemberPayload'. Path 'Unknown', line 1, position 21.";
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
            "{\"Known\":7,\"Unknown\":42}",
            settings: null);
    }
    catch (Newtonsoft.Json.JsonSerializationException exception)
    {
        settingsUnknownRejected = exception.Message
            == "Could not find member 'Unknown' on object of type "
                + "'NewtonsoftErrorMissingMemberPayload'. Path 'Unknown', line 1, position 21.";
    }

    return ignored?.Known == 6
        && payload?.Known == 7
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload) == "{\"Known\":7}"
        && unknownRejected
        && settingsUnknownRejected;
}

static bool VerifyNewtonsoftItemRequired()
{
    const string validAlways =
        "{\"RequiredByType\":\"set\",\"ExplicitAllowNull\":null}";
    var always =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
            validAlways);
    var settingsAlways =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
            validAlways,
            settings: null);
    var missingAlwaysRejected = false;
    var settingsNullAlwaysRejected = false;
    var writeAlwaysRejected = false;
    var missingAllowNullRejected = false;
    var nullDisallowNullRejected = false;
    var writeDisallowNullRejected = false;
    var invalidReadRejected = false;
    var invalidSettingsReadRejected = false;
    var invalidWriteRejected = false;
    var invalidSettingsWriteRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
            "{\"ExplicitAllowNull\":null}");
    }
    catch (Exception)
    {
        missingAlwaysRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
            "{\"RequiredByType\":null,\"ExplicitAllowNull\":null}",
            settings: null);
    }
    catch (Exception)
    {
        settingsNullAlwaysRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftItemRequiredAlwaysPayload
        {
            RequiredByType = null,
            ExplicitDefault = null,
            ExplicitAllowNull = null,
        });
    }
    catch (Exception)
    {
        writeAlwaysRejected = true;
    }

    var allowNull =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAllowNullPayload>(
            "{\"Value\":null}");
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredAllowNullPayload>(
            "{}");
    }
    catch (Exception)
    {
        missingAllowNullRejected = true;
    }

    var disallowNull =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredDisallowNullPayload>(
            "{}");
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemRequiredDisallowNullPayload>(
            "{\"Value\":null}");
    }
    catch (Exception)
    {
        nullDisallowNullRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(disallowNull, settings: null);
    }
    catch (Exception)
    {
        writeDisallowNullRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftInvalidItemRequiredPayload>("{}");
    }
    catch (NotSupportedException)
    {
        invalidReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftInvalidItemRequiredPayload>(
            "{}",
            settings: null);
    }
    catch (NotSupportedException)
    {
        invalidSettingsReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftInvalidItemRequiredPayload());
    }
    catch (NotSupportedException)
    {
        invalidWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftInvalidItemRequiredPayload(),
            settings: null);
    }
    catch (NotSupportedException)
    {
        invalidSettingsWriteRejected = true;
    }

    const string expectedAlways =
        "{\"RequiredByType\":\"set\",\"ExplicitDefault\":null,"
            + "\"ExplicitAllowNull\":null}";
    return always?.RequiredByType == "set"
        && always.ExplicitDefault == null
        && always.ExplicitAllowNull == null
        && settingsAlways?.RequiredByType == "set"
        && Newtonsoft.Json.JsonConvert.SerializeObject(always) == expectedAlways
        && Newtonsoft.Json.JsonConvert.SerializeObject(always, settings: null) == expectedAlways
        && missingAlwaysRejected
        && settingsNullAlwaysRejected
        && writeAlwaysRejected
        && allowNull?.Value == null
        && Newtonsoft.Json.JsonConvert.SerializeObject(allowNull) == "{\"Value\":null}"
        && missingAllowNullRejected
        && disallowNull?.Value == null
        && nullDisallowNullRejected
        && writeDisallowNullRejected
        && invalidReadRejected
        && invalidSettingsReadRejected
        && invalidWriteRejected
        && invalidSettingsWriteRejected;
}

static bool VerifyNewtonsoftItemNullValueHandling()
{
    const string input =
        "{\"IgnoredByType\":null,\"ExplicitInclude\":null}";
    var payload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(input);
    var settingsPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(
            input,
            settings: null);
    var requiredReadRejected = false;
    var requiredSettingsReadRejected = false;
    var invalidReadRejected = false;
    var invalidSettingsReadRejected = false;
    var invalidWriteRejected = false;
    var invalidSettingsWriteRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemNullIgnoreRequiredPayload>(
            "{\"Value\":null}");
    }
    catch (Exception)
    {
        requiredReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemNullIgnoreRequiredPayload>(
            "{\"Value\":null}",
            settings: null);
    }
    catch (Exception)
    {
        requiredSettingsReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftInvalidItemNullPayload>("{}");
    }
    catch (NotSupportedException)
    {
        invalidReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftInvalidItemNullPayload>(
            "{}",
            settings: null);
    }
    catch (NotSupportedException)
    {
        invalidSettingsReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftInvalidItemNullPayload());
    }
    catch (NotSupportedException)
    {
        invalidWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftInvalidItemNullPayload(),
            settings: null);
    }
    catch (NotSupportedException)
    {
        invalidSettingsWriteRejected = true;
    }

    if (payload == null || settingsPayload == null) return false;
    var readSucceeded = payload.IgnoredByType == "type-default"
        && payload.ExplicitInclude == null
        && settingsPayload.IgnoredByType == "type-default"
        && settingsPayload.ExplicitInclude == null;
    payload.IgnoredByType = null;
    const string expected = "{\"ExplicitInclude\":null}";
    var writeSucceeded = Newtonsoft.Json.JsonConvert.SerializeObject(payload) == expected
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload, settings: null) == expected;
    var requiredPayload = new NewtonsoftItemNullIgnoreRequiredPayload { Value = null };
    var requiredWriteSucceeded =
        Newtonsoft.Json.JsonConvert.SerializeObject(requiredPayload) == "{}"
        && Newtonsoft.Json.JsonConvert.SerializeObject(requiredPayload, settings: null) == "{}";
    return readSucceeded
        && writeSucceeded
        && requiredReadRejected
        && requiredSettingsReadRejected
        && requiredWriteSucceeded
        && invalidReadRejected
        && invalidSettingsReadRejected
        && invalidWriteRejected
        && invalidSettingsWriteRejected;
}

static bool VerifyNewtonsoftJsonObjectMetadata()
{
    const string input = "{\"Value\":\"input\"}";
    var payload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDescriptiveJsonObjectPayload>(
            input);
    var settingsPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDescriptiveJsonObjectPayload>(
            input,
            settings: null);
    var activeReadRejected = false;
    var activeSettingsReadRejected = false;
    var activeWriteRejected = false;
    var activeSettingsWriteRejected = false;
    var activeItemReferenceRejected = false;
    var activeItemReferenceLoopRejected = false;
    var activeItemTypeNameRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftReferenceJsonObjectPayload>(
            input);
    }
    catch (NotSupportedException)
    {
        activeReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftReferenceJsonObjectPayload>(
            input,
            settings: null);
    }
    catch (NotSupportedException)
    {
        activeSettingsReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftReferenceJsonObjectPayload { Value = "input" });
    }
    catch (NotSupportedException)
    {
        activeWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftReferenceJsonObjectPayload { Value = "input" },
            settings: null);
    }
    catch (NotSupportedException)
    {
        activeSettingsWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftItemReferenceJsonObjectPayload { Value = "input" });
    }
    catch (NotSupportedException)
    {
        activeItemReferenceRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftItemReferenceLoopJsonObjectPayload { Value = "input" });
    }
    catch (NotSupportedException)
    {
        activeItemReferenceLoopRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftItemTypeNameJsonObjectPayload { Value = "input" });
    }
    catch (NotSupportedException)
    {
        activeItemTypeNameRejected = true;
    }

    return payload?.Value == "input"
        && settingsPayload?.Value == "input"
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload) == input
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload, settings: null) == input
        && activeReadRejected
        && activeSettingsReadRejected
        && activeWriteRejected
        && activeSettingsWriteRejected
        && activeItemReferenceRejected
        && activeItemReferenceLoopRejected
        && activeItemTypeNameRejected;
}

static bool VerifyNewtonsoftNamingStrategies()
{
    const string camelJson =
        "{\"urlValue\":\"url\",\"ExplicitName\":\"explicit\"}";
    var camel =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftCamelNamingPayload>(
            camelJson);
    const string overriddenJson = "{\"explicitName\":\"overridden\"}";
    var overridden =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftCamelOverrideNamingPayload>(
            overriddenJson,
            settings: null);
    const string snakeJson = "{\"url_value\":\"constructed\"}";
    var snake =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftSnakeConstructorNamingPayload>(
            snakeJson);
    var kebab =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftKebabNamingPayload>(
            "{\"sh-a512-managed\":7}");
    const string memberJson =
        "{\"http-value\":\"member\",\"explicit-http-name\":\"explicit\"}";
    var member =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftMemberNamingPayload>(
            memberJson,
            settings: null);
    const string inheritedJson =
        "{\"derivedProperty\":\"derived\",\"BaseProperty\":\"base\"}";
    var inherited =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDerivedNamingPayload>(
            inheritedJson);
    const string identityJson = "{\"PascalName\":\"identity\"}";
    var identity =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDefaultNamingPayload>(
            identityJson,
            settings: null);

    var readCollisionRejected = false;
    var writeCollisionRejected = false;
    var customReadRejected = false;
    var customSettingsWriteRejected = false;
    var malformedParametersRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNamingCollisionPayload>("{}");
    }
    catch (Exception)
    {
        readCollisionRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftNamingCollisionPayload());
    }
    catch (Exception)
    {
        writeCollisionRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftCustomNamingPayload>("{}");
    }
    catch (NotSupportedException)
    {
        customReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftCustomNamingPayload(),
            settings: null);
    }
    catch (NotSupportedException)
    {
        customSettingsWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftMalformedNamingParametersPayload());
    }
    catch (NotSupportedException)
    {
        malformedParametersRejected = true;
    }

    return camel?.URLValue == "url"
        && camel.ExplicitValue == "explicit"
        && Newtonsoft.Json.JsonConvert.SerializeObject(camel) == camelJson
        && Newtonsoft.Json.JsonConvert.SerializeObject(camel, settings: null) == camelJson
        && overridden?.Value == "overridden"
        && Newtonsoft.Json.JsonConvert.SerializeObject(overridden) == overriddenJson
        && snake?.URLValue == "constructed"
        && Newtonsoft.Json.JsonConvert.SerializeObject(snake, settings: null) == snakeJson
        && kebab?.SHA512Managed == 7
        && Newtonsoft.Json.JsonConvert.SerializeObject(kebab)
            == "{\"sh-a512-managed\":7}"
        && member?.HTTPValue == "member"
        && member.ExplicitValue == "explicit"
        && Newtonsoft.Json.JsonConvert.SerializeObject(member) == memberJson
        && inherited?.DerivedProperty == "derived"
        && inherited.BaseProperty == "base"
        && Newtonsoft.Json.JsonConvert.SerializeObject(inherited) == inheritedJson
        && identity?.PascalName == "identity"
        && Newtonsoft.Json.JsonConvert.SerializeObject(identity) == identityJson
        && readCollisionRejected
        && writeCollisionRejected
        && customReadRejected
        && customSettingsWriteRejected
        && malformedParametersRejected;
}

static bool VerifyStringEscaping()
{
    const string value = "\0\u001F\u0085\u2028\u2029😀<>&'+\"\\/";
    const string invalidSurrogates = "\uD800x\uDC00";
    const string expectedSystemTextJson =
        "\"\\u0000\\u001F\\u0085\\u2028\\u2029\\uD83D\\uDE00"
            + "\\u003C\\u003E\\u0026\\u0027\\u002B\\u0022\\\\/\"";
    const string expectedNewtonsoft =
        "\"\\u0000\\u001f\\u0085\\u2028\\u2029😀<>&'+\\\"\\\\/\"";

    var systemTextJsonInvalidInputRejected = false;
    try
    {
        JsonSerializer.Deserialize<StringConversionPayload>(
            "{\"Text\":\"\\uD800x\\uDC00\"}");
    }
    catch (JsonException exception)
    {
        systemTextJsonInvalidInputRejected = exception.Path == "$.Text"
            && exception.LineNumber == 0
            && exception.BytePositionInLine == 23;
    }
    var newtonsoftInvalidInput =
        Newtonsoft.Json.JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":\"\\uD800x\\uDC00\"}");
    var newtonsoftSettingsInvalidInput =
        Newtonsoft.Json.JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":\"\\uD800x\\uDC00\"}",
            settings: null);
    var newtonsoftReplacementInput =
        Newtonsoft.Json.JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":\"\\uFFFDx\\uFFFD\"}");

    return JsonSerializer.Serialize(value) == expectedSystemTextJson
        && JsonSerializer.Serialize(invalidSurrogates) == "\"\\uFFFDx\\uFFFD\""
        && Newtonsoft.Json.JsonConvert.SerializeObject(value) == expectedNewtonsoft
        && Newtonsoft.Json.JsonConvert.SerializeObject(value, settings: null)
            == expectedNewtonsoft
        && systemTextJsonInvalidInputRejected
        && newtonsoftInvalidInput?.Text == newtonsoftReplacementInput?.Text
        && newtonsoftSettingsInvalidInput?.Text == newtonsoftReplacementInput?.Text;
}

static bool VerifyNewtonsoftWriterSettings()
{
    const string value = "é😀<>&'+\"\\/\u0085";
    var payload = new NewtonsoftWriterSettingsPayload
    {
        Text = value,
        Values = [1, 2],
    };
    var defaultSettings = new Newtonsoft.Json.JsonSerializerSettings();
    var defaultRead = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
        "{\"Text\":\"read\",\"Values\":[3,4]}",
        defaultSettings);
    var explicitDefaultSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        Formatting = Newtonsoft.Json.Formatting.None,
        StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.Default,
    };
    var indentedHtmlSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        Formatting = Newtonsoft.Json.Formatting.Indented,
        StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeHtml,
    };
    var settingsRead = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
        "{\"Text\":\"writer-only\",\"Values\":[]}",
        indentedHtmlSettings);
    var nonAsciiSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        StringEscapeHandling = Newtonsoft.Json.StringEscapeHandling.EscapeNonAscii,
    };
    var invalidFormattingRejected = false;
    var invalidStringEscapeHandlingRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            payload,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = (Newtonsoft.Json.Formatting)99,
            });
    }
    catch (ArgumentOutOfRangeException)
    {
        invalidFormattingRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            payload,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                StringEscapeHandling = (Newtonsoft.Json.StringEscapeHandling)99,
            });
    }
    catch (ArgumentOutOfRangeException)
    {
        invalidStringEscapeHandlingRejected = true;
    }
    var invalidWriterSettingsRead =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
            "{}",
            new Newtonsoft.Json.JsonSerializerSettings
            {
                Formatting = (Newtonsoft.Json.Formatting)99,
                StringEscapeHandling = (Newtonsoft.Json.StringEscapeHandling)99,
            });
    return Newtonsoft.Json.JsonConvert.SerializeObject(payload, defaultSettings)
            == "{\"Text\":\"é😀<>&'+\\\"\\\\/\\u0085\",\"Values\":[1,2]}"
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload, explicitDefaultSettings)
            == "{\"Text\":\"é😀<>&'+\\\"\\\\/\\u0085\",\"Values\":[1,2]}"
        && defaultRead?.Text == "read"
        && defaultRead.Values.Length == 2
        && defaultRead.Values[0] == 3
        && defaultRead.Values[1] == 4
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload, indentedHtmlSettings)
            == "{\r\n  \"Text\": \"é😀\\u003c\\u003e\\u0026\\u0027+\\u0022\\\\/\\u0085\",\r\n"
                + "  \"Values\": [\r\n    1,\r\n    2\r\n  ]\r\n}"
        && Newtonsoft.Json.JsonConvert.SerializeObject(new[] { 1, 2 }, indentedHtmlSettings)
            == "[\r\n  1,\r\n  2\r\n]"
        && Newtonsoft.Json.JsonConvert.SerializeObject(new int[0], indentedHtmlSettings)
            == "[]"
        && settingsRead?.Text == "writer-only"
        && settingsRead.Values.Length == 0
        && Newtonsoft.Json.JsonConvert.SerializeObject(value, nonAsciiSettings)
            == "\"\\u00e9\\ud83d\\ude00<>&'+\\\"\\\\/\\u0085\""
        && invalidFormattingRejected
        && invalidStringEscapeHandlingRejected
        && invalidWriterSettingsRead !=  null;
}

static bool VerifyNewtonsoftGlobalSettings()
{
    var ignoreSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore,
    };
    var payload = new NewtonsoftGlobalSettingsPayload
    {
        IgnoredNull = null,
        IncludedNull = null,
        IgnoredDefault = 0,
        IncludedDefault = 0,
        AttributedDefault = 7,
    };
    var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(payload, ignoreSettings);
    var read = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
        "{\"IgnoredNull\":null,\"IncludedNull\":null,"
            + "\"IgnoredDefault\":0,\"IncludedDefault\":0,"
            + "\"AttributedDefault\":7}",
        ignoreSettings);
    var populateSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate,
    };
    var populated = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
        "{}",
        populateSettings);
    var ignoreAndPopulateSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.IgnoreAndPopulate,
    };
    var ignoredAndPopulated =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
            "{\"IgnoredDefault\":0,\"AttributedDefault\":7}",
            ignoreAndPopulateSettings);
    var constructorPopulated =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
            "{}",
            populateSettings);
    var constructorIgnored =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
            "{\"Count\":7}",
            new Newtonsoft.Json.JsonSerializerSettings
            {
                DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore,
            });
    var constructorNullRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
            "{\"Count\":null}",
            new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            });
    }
    catch (Exception)
    {
        constructorNullRejected = true;
    }
    var typeNullOverride =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(
            "{\"IgnoredByType\":null,\"ExplicitInclude\":null}",
            new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Include,
            });
    var requiredNullRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":null}",
            new Newtonsoft.Json.JsonSerializerSettings
            {
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
            });
    }
    catch (Exception)
    {
        requiredNullRejected = true;
    }
    var missingSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Error,
    };
    var globalMissingRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
            "{\"Unknown\":42}",
            missingSettings);
    }
    catch (Exception exception)
    {
        globalMissingRejected = exception.Message
            == "Could not find member 'Unknown' on object of type "
                + "'NewtonsoftGlobalSettingsPayload'. Path 'Unknown', line 1, position 11.";
    }
    var typeMissingOverride =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftIgnoreMissingMemberPayload>(
            "{\"Known\":6,\"Unknown\":42}",
            missingSettings);

    return serialized == "{\"IncludedNull\":null,\"IncludedDefault\":0}"
        && read?.IgnoredNull == "ignored-null-default"
        && read.IgnoredNullSetterCalls == 0
        && read.IncludedNull == null
        && read.IncludedNullSetterCalls == 1
        && read.IgnoredDefault == 5
        && read.IgnoredDefaultSetterCalls == 0
        && read.IncludedDefault == 0
        && read.IncludedDefaultSetterCalls == 1
        && read.AttributedDefault == 3
        && read.AttributedDefaultSetterCalls == 0
        && populated !=  null
        && populated.IgnoredNull == null
        && populated.IgnoredNullSetterCalls == 1
        && populated.IncludedNull == "included-null-default"
        && populated.IncludedNullSetterCalls == 0
        && populated.IgnoredDefault == 0
        && populated.IgnoredDefaultSetterCalls == 1
        && populated.IncludedDefault == 6
        && populated.IncludedDefaultSetterCalls == 0
        && populated.AttributedDefault == 7
        && populated.AttributedDefaultSetterCalls == 1
        && ignoredAndPopulated !=  null
        && ignoredAndPopulated.IgnoredNull == null
        && ignoredAndPopulated.IgnoredNullSetterCalls == 1
        && ignoredAndPopulated.IncludedNull == "included-null-default"
        && ignoredAndPopulated.IncludedNullSetterCalls == 0
        && ignoredAndPopulated.IgnoredDefault == 0
        && ignoredAndPopulated.IgnoredDefaultSetterCalls == 1
        && ignoredAndPopulated.IncludedDefault == 6
        && ignoredAndPopulated.IncludedDefaultSetterCalls == 0
        && ignoredAndPopulated.AttributedDefault == 7
        && ignoredAndPopulated.AttributedDefaultSetterCalls == 1
        && Newtonsoft.Json.JsonConvert.SerializeObject(
            ignoredAndPopulated,
            ignoreAndPopulateSettings)
            == "{\"IncludedNull\":\"included-null-default\","
                + "\"IncludedDefault\":6}"
        && constructorPopulated?.Count == 7
        && constructorIgnored?.Count == 7
        && constructorNullRejected
        && typeNullOverride?.IgnoredByType == "type-default"
        && typeNullOverride.ExplicitInclude == null
        && requiredNullRejected
        && globalMissingRejected
        && typeMissingOverride?.Known == 6
        && Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftGlobalSettingsPayload(),
            missingSettings)
            == "{\"IgnoredNull\":\"ignored-null-default\","
                + "\"IncludedNull\":\"included-null-default\","
                + "\"IgnoredDefault\":5,\"IncludedDefault\":6,"
                + "\"AttributedDefault\":3}";
}

static bool VerifyNewtonsoftStructuralSettings()
{
    const string constructorJson = "{\"marker\":\"input\",\"Value\":\"set\"}";
    var defaultConstructor =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftConstructorHandlingPayload>(
            constructorJson,
            new Newtonsoft.Json.JsonSerializerSettings());
    var constructorSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        ConstructorHandling =
            Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor,
    };
    var nonPublicConstructor =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftConstructorHandlingPayload>(
            constructorJson,
            constructorSettings);
    var attributedConstructor =
        Newtonsoft.Json.JsonConvert.DeserializeObject<
            NewtonsoftAttributedConstructorHandlingPayload>(
                constructorJson,
                constructorSettings);

    var loop = new NewtonsoftLoopSettingsPayload { Name = "root" };
    loop.Self = loop;
    var defaultLoopRejected = false;
    var defaultSettingsLoopRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(loop);
    }
    catch (Exception)
    {
        defaultLoopRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            loop,
            new Newtonsoft.Json.JsonSerializerSettings());
    }
    catch (Exception)
    {
        defaultSettingsLoopRejected = true;
    }
    var ignoreLoopSettings = new Newtonsoft.Json.JsonSerializerSettings
    {
        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
    };
    var ignoredLoop = Newtonsoft.Json.JsonConvert.SerializeObject(loop, ignoreLoopSettings);
    var loopRead =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            "{\"Name\":\"read\",\"Self\":null}",
            ignoreLoopSettings);

    var memberIgnore = new NewtonsoftMemberIgnoreLoopPayload { Name = "member" };
    memberIgnore.Self = memberIgnore;
    var memberIgnored = Newtonsoft.Json.JsonConvert.SerializeObject(memberIgnore);
    var settingsMemberIgnored = Newtonsoft.Json.JsonConvert.SerializeObject(
        memberIgnore,
        new Newtonsoft.Json.JsonSerializerSettings());
    var memberIgnoreRead =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftMemberIgnoreLoopPayload>(
            "{\"Name\":\"read\",\"Self\":null}");
    var memberError = new NewtonsoftMemberErrorLoopPayload { Name = "member" };
    memberError.Self = memberError;
    var memberErrorRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(memberError, ignoreLoopSettings);
    }
    catch (Exception)
    {
        memberErrorRejected = true;
    }

    const string nestedJson = "{\"Name\":\"root\",\"Self\":{\"Name\":\"nested\"}}";
    var depthRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            nestedJson,
            new Newtonsoft.Json.JsonSerializerSettings { MaxDepth = 1 });
    }
    catch (Exception)
    {
        depthRejected = true;
    }
    var readerOnlySettingsWrite = Newtonsoft.Json.JsonConvert.SerializeObject(
        new NewtonsoftLoopSettingsPayload { Name = "write" },
        new Newtonsoft.Json.JsonSerializerSettings
        {
            MaxDepth = 1,
            CheckAdditionalContent = true,
        });

    const string deepJson = "{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":{\"Self\":null}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}}";
    var defaultDepthRejected = false;
    var defaultSettingsDepthRejected = false;
    var unlimitedDepthAccepted = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(deepJson);
    }
    catch (Exception)
    {
        defaultDepthRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            deepJson,
            new Newtonsoft.Json.JsonSerializerSettings());
    }
    catch (Exception)
    {
        defaultSettingsDepthRejected = true;
    }
    try
    {
        unlimitedDepthAccepted =
            Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                deepJson,
                new Newtonsoft.Json.JsonSerializerSettings { MaxDepth = null }) !=  null;
    }
    catch (Exception)
    {
        unlimitedDepthAccepted = false;
    }

    const string additionalJson = "{\"Name\":\"first\"} 42";
    var defaultAdditionalRejected = false;
    var defaultSettingsAdditionalRejected = false;
    var explicitAdditionalRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            additionalJson);
    }
    catch (Exception)
    {
        defaultAdditionalRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            additionalJson,
            new Newtonsoft.Json.JsonSerializerSettings());
    }
    catch (Exception)
    {
        defaultSettingsAdditionalRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            additionalJson,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                CheckAdditionalContent = true,
            });
    }
    catch (Exception)
    {
        explicitAdditionalRejected = true;
    }
    var firstContent =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            additionalJson,
            new Newtonsoft.Json.JsonSerializerSettings
            {
                CheckAdditionalContent = false,
            });

    return defaultConstructor?.Source == "public:input"
        && defaultConstructor.Value == "set"
        && nonPublicConstructor?.Source == "private"
        && nonPublicConstructor.Value == "set"
        && Newtonsoft.Json.JsonConvert.SerializeObject(
            nonPublicConstructor,
            constructorSettings)
            == "{\"Source\":\"private\",\"Value\":\"set\"}"
        && attributedConstructor?.Source == "attributed:input"
        && attributedConstructor.Value == "set"
        && defaultLoopRejected
        && defaultSettingsLoopRejected
        && ignoredLoop == "{\"Name\":\"root\"}"
        && loopRead?.Name == "read"
        && loopRead.Self == null
        && memberIgnored == "{\"Name\":\"member\"}"
        && settingsMemberIgnored == "{\"Name\":\"member\"}"
        && memberIgnoreRead?.Name == "read"
        && memberErrorRejected
        && depthRejected
        && readerOnlySettingsWrite == "{\"Name\":\"write\",\"Self\":null}"
        && defaultDepthRejected
        && defaultSettingsDepthRejected
        && unlimitedDepthAccepted
        && defaultAdditionalRejected
        && defaultSettingsAdditionalRejected
        && explicitAdditionalRejected
        && firstContent?.Name == "first";
}

static bool VerifyDuplicateMemberNames()
{
    var systemTextJsonReadRejected = false;
    var systemTextJsonWriteRejected = false;
    var newtonsoftReadRejected = false;
    var newtonsoftSettingsReadRejected = false;
    var newtonsoftWriteRejected = false;
    var newtonsoftSettingsWriteRejected = false;

    try
    {
        JsonSerializer.Deserialize<SystemTextJsonNameCollisionPayload>("{}");
    }
    catch (InvalidOperationException)
    {
        systemTextJsonReadRejected = true;
    }
    try
    {
        JsonSerializer.Serialize(new SystemTextJsonNameCollisionPayload());
    }
    catch (InvalidOperationException)
    {
        systemTextJsonWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNameCollisionPayload>("{}");
    }
    catch (Exception)
    {
        newtonsoftReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNameCollisionPayload>(
            "{}",
            settings: null);
    }
    catch (Exception)
    {
        newtonsoftSettingsReadRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftNameCollisionPayload());
    }
    catch (Exception)
    {
        newtonsoftWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(
            new NewtonsoftNameCollisionPayload(),
            settings: null);
    }
    catch (Exception)
    {
        newtonsoftSettingsWriteRejected = true;
    }

    return systemTextJsonReadRejected
        && systemTextJsonWriteRejected
        && newtonsoftReadRejected
        && newtonsoftSettingsReadRejected
        && newtonsoftWriteRejected
        && newtonsoftSettingsWriteRejected;
}

static bool VerifyRequiredMembers()
{
    var systemTextJson = JsonSerializer.Deserialize<SystemTextJsonRequiredPayload>(
        "{\"required_text\":null}");
    var systemTextJsonMissingRejected = false;
    var systemTextJsonKeywordMissingRejected = false;
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonRequiredPayload>("{}");
    }
    catch (JsonException exception)
    {
        systemTextJsonMissingRejected = exception.Path == "$";
    }
    var systemTextJsonKeyword =
        JsonSerializer.Deserialize<SystemTextJsonKeywordRequiredPayload>(
            "{\"KeywordRequired\":null}");
    try
    {
        JsonSerializer.Deserialize<SystemTextJsonKeywordRequiredPayload>("{}");
    }
    catch (JsonException exception)
    {
        systemTextJsonKeywordMissingRejected = exception.Path == "$";
    }
    var newtonsoftKeyword =
        Newtonsoft.Json.JsonConvert.DeserializeObject<SystemTextJsonKeywordRequiredPayload>("{}");

    var newtonsoft =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}");
    var newtonsoftSettings =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}",
            settings: null);
    var newtonsoftMissingDisallow =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null}");
    var missingAlwaysRejected = false;
    var nullAlwaysRejected = false;
    var missingAllowNullRejected = false;
    var nullDisallowedRejected = false;
    var settingsMissingRejected = false;
    var nullAlwaysWriteRejected = false;
    var nullDisallowedWriteRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"AllowNull\":null}");
    }
    catch (Exception)
    {
        missingAlwaysRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":null,\"AllowNull\":null}");
    }
    catch (Exception)
    {
        nullAlwaysRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\"}");
    }
    catch (Exception)
    {
        missingAllowNullRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":null}");
    }
    catch (Exception)
    {
        nullDisallowedRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\"}",
            settings: null);
    }
    catch (Exception)
    {
        settingsMissingRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftRequiredPayload
        {
            Always = null,
            AllowNull = null,
            DisallowNull = "optional",
        });
    }
    catch (Exception)
    {
        nullAlwaysWriteRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftRequiredPayload
        {
            Always = "set",
            AllowNull = null,
            DisallowNull = null,
        }, settings: null);
    }
    catch (Exception)
    {
        nullDisallowedWriteRejected = true;
    }

    return systemTextJson != null
        && systemTextJson.RequiredText == null
        && JsonSerializer.Serialize(systemTextJson) == "{\"required_text\":null}"
        && systemTextJsonMissingRejected
        && systemTextJsonKeyword?.KeywordRequired == null
        && systemTextJsonKeywordMissingRejected
        && newtonsoftKeyword?.KeywordRequired == null
        && newtonsoft?.Always == "set"
        && newtonsoft.AllowNull == null
        && newtonsoft.DisallowNull == "optional"
        && newtonsoftSettings?.Always == "set"
        && newtonsoftMissingDisallow?.DisallowNull == null
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft)
            == "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}"
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft, settings: null)
            == "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}"
        && missingAlwaysRejected
        && nullAlwaysRejected
        && missingAllowNullRejected
        && nullDisallowedRejected
        && settingsMissingRejected
        && nullAlwaysWriteRejected
        && nullDisallowedWriteRejected;
}

static bool VerifyNewtonsoftNullValueHandling()
{
    const string json = "{\"Ignored\":null,\"Included\":null}";
    var payload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNullValueHandlingPayload>(json);
    var settingsPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftNullValueHandlingPayload>(
            json,
            settings: null);
    if (payload == null || settingsPayload == null) return false;

    var readSucceeded = payload.Ignored == "ignored-default"
        && payload.IgnoredSetterCalls == 0
        && payload.Included == null
        && payload.IncludedSetterCalls == 1
        && settingsPayload.Ignored == "ignored-default"
        && settingsPayload.IgnoredSetterCalls == 0
        && settingsPayload.Included == null
        && settingsPayload.IncludedSetterCalls == 1;

    payload.Ignored = null;
    return readSucceeded
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload) == "{\"Included\":null}"
        && Newtonsoft.Json.JsonConvert.SerializeObject(payload, settings: null)
            == "{\"Included\":null}";
}

static bool VerifyNewtonsoftDefaultValueHandling()
{
    const string json =
        "{\"Ignored\":0,\"Included\":0,\"AttributedIgnoredInteger\":7,"
        + "\"AttributedAssignedString\":\"custom\",\"AttributedIgnoredBoolean\":true,"
        + "\"AttributedIgnoredCharacter\":\"Z\","
        + "\"AttributedIgnoredInt64\":9007199254740993,"
        + "\"AttributedIgnoredUInt64\":9007199254740993,"
        + "\"AttributedIgnoredSingle\":1.25,\"AttributedIgnoredDouble\":1.25}";
    var payload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDefaultValueHandlingPayload>(
            json);
    var settingsPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftDefaultValueHandlingPayload>(
            json,
            settings: null);
    if (payload == null || settingsPayload == null)
    {
        return false;
    }

    var readSucceeded = payload.Ignored == 5
        && payload.IgnoredSetterCalls == 0
        && payload.Included == 0
        && payload.IncludedSetterCalls == 1
        && payload.Populated == 0
        && payload.PopulatedSetterCalls == 1
        && payload.IgnoredAndPopulated == 0
        && payload.IgnoredAndPopulatedSetterCalls == 1
        && settingsPayload.Ignored == 5
        && settingsPayload.IgnoredSetterCalls == 0
        && settingsPayload.Included == 0
        && settingsPayload.IncludedSetterCalls == 1
        && settingsPayload.Populated == 0
        && settingsPayload.PopulatedSetterCalls == 1
        && settingsPayload.IgnoredAndPopulated == 0
        && settingsPayload.IgnoredAndPopulatedSetterCalls == 1
        && payload.AttributedIgnoredInteger == 3
        && payload.AttributedPopulatedInteger == 7
        && payload.AttributedAssignedString == "custom"
        && !payload.AttributedIgnoredBoolean
        && payload.AttributedIgnoredCharacter == 'A'
        && payload.AttributedIgnoredInt64 == 4
        && payload.AttributedIgnoredUInt64 == 4
        && payload.AttributedIgnoredSingle == 2.5f
        && payload.AttributedIgnoredDouble == 2.5d
        && settingsPayload.AttributedIgnoredInteger == 3
        && settingsPayload.AttributedPopulatedInteger == 7
        && settingsPayload.AttributedAssignedString == "custom"
        && !settingsPayload.AttributedIgnoredBoolean
        && settingsPayload.AttributedIgnoredCharacter == 'A'
        && settingsPayload.AttributedIgnoredInt64 == 4
        && settingsPayload.AttributedIgnoredUInt64 == 4
        && settingsPayload.AttributedIgnoredSingle == 2.5f
        && settingsPayload.AttributedIgnoredDouble == 2.5d;

    payload.Ignored = 0;
    settingsPayload.Ignored = 0;
    payload.SetAttributedDefaults();
    settingsPayload.SetAttributedDefaults();
    const string expected =
        "{\"Included\":0,\"Populated\":0,\"AttributedPopulatedInteger\":7}";
    var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
    var settingsSerialized = Newtonsoft.Json.JsonConvert.SerializeObject(
        settingsPayload,
        settings: null);
    return readSucceeded
        && serialized == expected
        && settingsSerialized == expected;
}

static bool VerifyExplicitMemberOrdering()
{
    var systemTextJson = JsonSerializer.Deserialize<SystemTextJsonOrderedPayload>(
        "{\"Late\":60,\"AfterDefaults\":50,\"DefaultSecond\":40,"
            + "\"Early\":30,\"DefaultFirst\":20,\"IncludedField\":10}");
    var newtonsoft =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftOrderedPayload>(
            "{\"LateProperty\":60,\"AfterDefaults\":50,\"DefaultProperty\":40,"
                + "\"EarlyProperty\":30,\"DefaultField\":20,\"EarlyField\":10}");
    const string expectedNewtonsoft =
        "{\"EarlyField\":10,\"EarlyProperty\":30,\"DefaultField\":20,"
            + "\"DefaultProperty\":40,\"AfterDefaults\":50,\"LateProperty\":60}";

    return systemTextJson != null
        && JsonSerializer.Serialize(systemTextJson)
            == "{\"IncludedField\":10,\"Early\":30,\"DefaultFirst\":20,"
                + "\"DefaultSecond\":40,\"AfterDefaults\":50,\"Late\":60}"
        && newtonsoft != null
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft) == expectedNewtonsoft
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft, settings: null)
            == expectedNewtonsoft;
}

static bool VerifyEmptyInputSemantics()
{
    const string whitespace = " \r\n\t ";
    var systemTextJsonPrimitiveRejected = false;
    var systemTextJsonClassRejected = false;
    var newtonsoftPrimitiveRejected = false;
    var newtonsoftSettingsPrimitiveRejected = false;
    try
    {
        JsonSerializer.Deserialize<int>(whitespace);
    }
    catch (JsonException)
    {
        systemTextJsonPrimitiveRejected = true;
    }
    try
    {
        JsonSerializer.Deserialize<FloatingPointPayload>(whitespace);
    }
    catch (JsonException)
    {
        systemTextJsonClassRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<int>(whitespace);
    }
    catch (Exception)
    {
        newtonsoftPrimitiveRejected = true;
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<double>(whitespace, settings: null);
    }
    catch (Exception)
    {
        newtonsoftSettingsPrimitiveRejected = true;
    }

    return systemTextJsonPrimitiveRejected
        && systemTextJsonClassRejected
        && newtonsoftPrimitiveRejected
        && newtonsoftSettingsPrimitiveRejected
        && Newtonsoft.Json.JsonConvert.DeserializeObject<FloatingPointPayload>(whitespace) == null
        && Newtonsoft.Json.JsonConvert.DeserializeObject<FloatingPointPayload>(
            whitespace,
            settings: null) == null;
}

static bool VerifyNamedFloatingContainers()
{
    var newtonsoftPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<FloatingPointPayload>(
            "{\"DoubleValue\":NaN,\"SingleValue\":Infinity}");
    var newtonsoftValues = Newtonsoft.Json.JsonConvert.DeserializeObject<double[]>(
        "[NaN,Infinity,-Infinity]",
        settings: null);
    var systemTextJsonRejected = false;
    try
    {
        JsonSerializer.Deserialize<FloatingPointPayload>("{\"DoubleValue\":NaN}");
    }
    catch (JsonException exception)
    {
        systemTextJsonRejected = exception.Path == "$.DoubleValue";
    }

    return newtonsoftPayload != null
        && double.IsNaN(newtonsoftPayload.DoubleValue)
        && float.IsPositiveInfinity(newtonsoftPayload.SingleValue)
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftPayload)
            == "{\"DoubleValue\":\"NaN\",\"SingleValue\":\"Infinity\"}"
        && newtonsoftValues?.Length == 3
        && double.IsNaN(newtonsoftValues[0])
        && double.IsPositiveInfinity(newtonsoftValues[1])
        && double.IsNegativeInfinity(newtonsoftValues[2])
        && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftValues)
            == "[\"NaN\",\"Infinity\",\"-Infinity\"]"
        && systemTextJsonRejected;
}

static bool VerifyNumericStringConversions()
{
    var systemTextJsonQuotedIntegerRejected = false;
    var systemTextJsonQuotedIntegerMemberRejected = false;
    var systemTextJsonFloatingIntegerRejected = false;
    try
    {
        JsonSerializer.Deserialize<int>("\"7\"");
    }
    catch (JsonException exception)
    {
        systemTextJsonQuotedIntegerRejected = exception.Path == "$";
    }
    try
    {
        JsonSerializer.Deserialize<NumericStringPayload>("{\"Count\":\"7\"}");
    }
    catch (JsonException exception)
    {
        systemTextJsonQuotedIntegerMemberRejected = exception.Path == "$.Count";
    }
    try
    {
        JsonSerializer.Deserialize<int>("1.0");
    }
    catch (JsonException exception)
    {
        systemTextJsonFloatingIntegerRejected = exception.Path == "$";
    }

    var newtonsoftQuotedInteger =
        Newtonsoft.Json.JsonConvert.DeserializeObject<int>("\"7\"");
    var newtonsoftPaddedQuotedInteger =
        Newtonsoft.Json.JsonConvert.DeserializeObject<int>("\"  +007  \"");
    var newtonsoftQuotedLong =
        Newtonsoft.Json.JsonConvert.DeserializeObject<long>("\"9007199254740993\"");
    var newtonsoftSettingsQuotedInteger =
        Newtonsoft.Json.JsonConvert.DeserializeObject<int>("\"7\"", settings: null);
    var newtonsoftNumericStringPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<NumericStringPayload>(
            "{\"Count\":\"7\",\"Large\":\"9007199254740993\"}");
    var newtonsoftInvalidIntegerStringRejected = false;
    var newtonsoftOverflowingIntegerStringRejected = false;
    var newtonsoftFloatingIntegerRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NumericStringPayload>(
            "{\"Count\":\"invalid\"}");
    }
    catch (Newtonsoft.Json.JsonReaderException exception)
    {
        newtonsoftInvalidIntegerStringRejected = exception.Path == "Count";
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NumericStringPayload>(
            "{\"Count\":\"2147483648\"}");
    }
    catch (Newtonsoft.Json.JsonReaderException exception)
    {
        newtonsoftOverflowingIntegerStringRejected = exception.Path == "Count";
    }
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<NumericStringPayload>(
            "{\"Count\":1.5}");
    }
    catch (Newtonsoft.Json.JsonReaderException exception)
    {
        newtonsoftFloatingIntegerRejected = exception.Path == "Count";
    }

    return systemTextJsonQuotedIntegerRejected
        && systemTextJsonQuotedIntegerMemberRejected
        && systemTextJsonFloatingIntegerRejected
        && newtonsoftQuotedInteger == 7
        && newtonsoftPaddedQuotedInteger == 7
        && newtonsoftQuotedLong == 9007199254740993L
        && newtonsoftSettingsQuotedInteger == 7
        && newtonsoftNumericStringPayload?.Count == 7
        && newtonsoftNumericStringPayload.Large == 9007199254740993L
        && newtonsoftInvalidIntegerStringRejected
        && newtonsoftOverflowingIntegerStringRejected
        && newtonsoftFloatingIntegerRejected;
}

static bool VerifyBooleanConversions()
{
    var systemTextJsonBooleanStringRejected = false;
    var systemTextJsonBooleanNumberRejected = false;
    var systemTextJsonBooleanMemberRejected = false;
    try
    {
        JsonSerializer.Deserialize<bool>("\"true\"");
    }
    catch (JsonException exception)
    {
        systemTextJsonBooleanStringRejected = exception.Path == "$";
    }
    try
    {
        JsonSerializer.Deserialize<bool>("1");
    }
    catch (JsonException exception)
    {
        systemTextJsonBooleanNumberRejected = exception.Path == "$";
    }
    try
    {
        JsonSerializer.Deserialize<BooleanPayload>("{\"Active\":\"true\"}");
    }
    catch (JsonException exception)
    {
        systemTextJsonBooleanMemberRejected = exception.Path == "$.Active";
    }

    var newtonsoftBooleanString =
        Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("\"  TRUE  \"");
    var newtonsoftBooleanFalseString =
        Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("\"False\"");
    var newtonsoftBooleanOne = Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("1");
    var newtonsoftBooleanZero = Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("0.0");
    var newtonsoftBooleanFraction = Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("0.5");
    var newtonsoftSettingsBoolean =
        Newtonsoft.Json.JsonConvert.DeserializeObject<bool>("\"true\"", settings: null);
    var newtonsoftBooleanStringPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<BooleanPayload>("{\"Active\":\"true\"}");
    var newtonsoftBooleanNumericPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<BooleanPayload>("{\"Active\":0}");
    var newtonsoftInvalidBooleanStringRejected = false;
    try
    {
        Newtonsoft.Json.JsonConvert.DeserializeObject<BooleanPayload>(
            "{\"Active\":\"invalid\"}");
    }
    catch (Newtonsoft.Json.JsonReaderException exception)
    {
        newtonsoftInvalidBooleanStringRejected = exception.Path == "Active";
    }

    return systemTextJsonBooleanStringRejected
        && systemTextJsonBooleanNumberRejected
        && systemTextJsonBooleanMemberRejected
        && newtonsoftBooleanString
        && !newtonsoftBooleanFalseString
        && newtonsoftBooleanOne
        && !newtonsoftBooleanZero
        && newtonsoftBooleanFraction
        && newtonsoftSettingsBoolean
        && newtonsoftBooleanStringPayload?.Active == true
        && newtonsoftBooleanNumericPayload?.Active == false
        && newtonsoftInvalidBooleanStringRejected;
}

static bool VerifyStringConversions()
{
    var systemTextJsonMemberRejected = false;
    try
    {
        JsonSerializer.Deserialize<StringConversionPayload>("{\"Text\":7}");
    }
    catch (JsonException exception)
    {
        systemTextJsonMemberRejected = exception.Path == "$.Text";
    }

    var newtonsoftPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":9007199254740993}");
    var newtonsoftSettingsPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":true}", settings: null);
    var newtonsoftArray =
        Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>("[7,false,1.25]");

    return systemTextJsonMemberRejected
        && newtonsoftPayload?.Text == "9007199254740993"
        && newtonsoftSettingsPayload?.Text == "true"
        && newtonsoftArray?.Length == 3
        && newtonsoftArray[0] == "7"
        && newtonsoftArray[1] == "false"
        && newtonsoftArray[2] == "1.25";
}

static bool VerifyCharacterNumericConversions()
{
    var systemTextJsonRootRejected = false;
    var systemTextJsonMemberRejected = false;
    try
    {
        JsonSerializer.Deserialize<char>("65");
    }
    catch (JsonException exception)
    {
        systemTextJsonRootRejected = exception.Path == "$";
    }
    try
    {
        JsonSerializer.Deserialize<CharacterPayload>("{\"Value\":65}");
    }
    catch (JsonException exception)
    {
        systemTextJsonMemberRejected = exception.Path == "$.Value";
    }

    var newtonsoftRoot = Newtonsoft.Json.JsonConvert.DeserializeObject<char>("65");
    var newtonsoftSettingsRoot =
        Newtonsoft.Json.JsonConvert.DeserializeObject<char>("65", settings: null);
    var newtonsoftPayload =
        Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterPayload>("{\"Value\":233}");
    var newtonsoftArray =
        Newtonsoft.Json.JsonConvert.DeserializeObject<char[]>("[65,233]");
    return systemTextJsonRootRejected
        && systemTextJsonMemberRejected
        && newtonsoftRoot == 'A'
        && newtonsoftSettingsRoot == 'A'
        && newtonsoftPayload?.Value == 'é'
        && newtonsoftArray?.Length == 2
        && newtonsoftArray[0] == 'A'
        && newtonsoftArray[1] == 'é';
}

internal sealed class WeatherPayload
{
    private string summary = "constructor-default";

    public string Summary
    {
        get => summary;
        set => summary = "set:" + value;
    }

    [JsonPropertyName("temperatureC")]
    public int TemperatureC { get; set; }

    public bool Active { get; set; }

    [JsonIgnore]
    public string Ignored { get; set; } = "constructor-default";

    public string Text { get; set; } = "constructor-default";

    public long Large { get; set; }

    public string? Optional { get; set; } = "constructor-default";

    [JsonInclude]
    public string Included { get; private set; } = "constructor-default";

    public string InitOnly { get; init; } = "constructor-default";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OmittedNull { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int OmittedDefault { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string NeverIgnored { get; set; } = "constructor-default";

    public string ReadOnly => "read-only";

    public string this[int index] => $"item:{index}";
}

internal sealed class NewtonsoftWeatherPayload
{
    private string summary = "constructor-default";

    public int PublicField = -1;

    public string Summary
    {
        get => summary;
        set => summary = "set:" + value;
    }

    [Newtonsoft.Json.JsonProperty("temperature_c")]
    public int TemperatureC { get; set; }

    public bool Active { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    public string Ignored { get; set; } = "constructor-default";

    public string Text { get; set; } = "constructor-default";

    public long Large { get; set; }

    public string? Optional { get; set; } = "constructor-default";

    public int Small { get; set; }
}

internal sealed class SystemTextJsonPrivateDefaultPayload
{
    private SystemTextJsonPrivateDefaultPayload()
    {
    }

    public string Name { get; set; } = "default";
}

internal sealed class SystemTextJsonParameterizedPayload
{
    private string name;

    public SystemTextJsonParameterizedPayload(string name)
    {
        this.name = "ctor:" + name;
    }

    [JsonPropertyName("display_name")]
    public string Name
    {
        get => name;
        set
        {
            name = "setter:" + value;
            SetterCalls++;
        }
    }

    [JsonIgnore]
    public int SetterCalls { get; private set; }

    public int Count { get; set; }
}

internal sealed class SystemTextJsonAttributedPrivatePayload
{
    [JsonConstructor]
    private SystemTextJsonAttributedPrivatePayload(string name)
    {
        Name = "attributed:" + name;
    }

    public string Name { get; }
}

internal sealed record SystemTextJsonRecordPayload(
    [property: JsonPropertyName("display_name")] string Name,
    int Count);

internal sealed class SystemTextJsonOptionalConstructorPayload
{
    public SystemTextJsonOptionalConstructorPayload(
        string name = "system-text-default",
        int count = 17,
        bool active = true,
        long large = 9007199254740993L,
        string? absent = null,
        char marker = 'Ω',
        float singleValue = 1.25f,
        double doubleValue = -2.5d)
    {
        Name = name;
        Count = count;
        Active = active;
        Large = large;
        Absent = absent;
        Marker = marker;
        SingleValue = singleValue;
        DoubleValue = doubleValue;
    }

    public string Name { get; }

    public int Count { get; }

    public bool Active { get; }

    public long Large { get; }

    public string? Absent { get; }

    public char Marker { get; }

    public float SingleValue { get; }

    public double DoubleValue { get; }
}

internal static class ValueTypeConstructorBrowserVerification
{
    public static bool Verify()
    {
        SystemTextJsonValueWithDefaultConstructor.Calls = 0;
        var systemTextDefault = default(SystemTextJsonValueWithDefaultConstructor);
        var systemTextNew = new SystemTextJsonValueWithDefaultConstructor();
        var systemTextRead = JsonSerializer.Deserialize<
            SystemTextJsonValueWithDefaultConstructor>("{}");
        var systemTextAttributed = JsonSerializer.Deserialize<
            SystemTextJsonParameterizedValue>("{}");
        var systemTextOrdinary = JsonSerializer.Deserialize<
            SystemTextJsonOrdinaryParameterizedValue>("{\"Value\":23}");

        NewtonsoftValueWithDefaultConstructor.Calls = 0;
        var newtonsoftDefault = default(NewtonsoftValueWithDefaultConstructor);
        var newtonsoftNew = new NewtonsoftValueWithDefaultConstructor();
        var newtonsoftRead = Newtonsoft.Json.JsonConvert.DeserializeObject<
            NewtonsoftValueWithDefaultConstructor>("{}");
        var newtonsoftAttributed = Newtonsoft.Json.JsonConvert.DeserializeObject<
            NewtonsoftParameterizedValue>("{}");
        var newtonsoftOrdinary = Newtonsoft.Json.JsonConvert.DeserializeObject<
            NewtonsoftOrdinaryParameterizedValue>("{\"Value\":29}");

        var systemTextExceptionMatched = false;
        try
        {
            JsonSerializer.Deserialize<SystemTextJsonThrowingValue>("{}");
        }
        catch (InvalidOperationException exception)
        {
            systemTextExceptionMatched = exception.Message == "system-text-value-constructor";
        }

        var newtonsoftExceptionMatched = false;
        try
        {
            Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftThrowingValue>("{}");
        }
        catch (InvalidOperationException exception)
        {
            newtonsoftExceptionMatched = exception.Message == "newtonsoft-value-constructor";
        }

        return systemTextDefault.Marker == 0
            && systemTextNew.Marker == 31
            && systemTextRead.Marker == 31
            && SystemTextJsonValueWithDefaultConstructor.Calls == 2
            && systemTextAttributed.Value == 17
            && systemTextOrdinary.Value == 0
            && JsonSerializer.Serialize(systemTextRead) == "{\"Marker\":31}"
            && newtonsoftDefault.Marker == 0
            && newtonsoftNew.Marker == 41
            && newtonsoftRead.Marker == 0
            && NewtonsoftValueWithDefaultConstructor.Calls == 1
            && newtonsoftAttributed.Value == 0
            && newtonsoftOrdinary.Value == 29
            && Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftRead) == "{\"Marker\":0}"
            && systemTextExceptionMatched
            && newtonsoftExceptionMatched;
    }
}

internal struct SystemTextJsonValueWithDefaultConstructor
{
    public static int Calls;

    public SystemTextJsonValueWithDefaultConstructor()
    {
        Calls++;
        Marker = 31;
    }

    public int Marker { get; set; }
}

internal struct SystemTextJsonParameterizedValue
{
    [JsonConstructor]
    private SystemTextJsonParameterizedValue(int value = 17)
    {
        Value = value;
    }

    public int Value { get; }
}

internal struct SystemTextJsonOrdinaryParameterizedValue
{
    public SystemTextJsonOrdinaryParameterizedValue(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

internal struct SystemTextJsonThrowingValue
{
    public SystemTextJsonThrowingValue()
    {
        throw new InvalidOperationException("system-text-value-constructor");
    }
}

internal struct NewtonsoftValueWithDefaultConstructor
{
    public static int Calls;

    public NewtonsoftValueWithDefaultConstructor()
    {
        Calls++;
        Marker = 41;
    }

    public int Marker { get; set; }
}

internal struct NewtonsoftParameterizedValue
{
    [Newtonsoft.Json.JsonConstructor]
    private NewtonsoftParameterizedValue(int value = 17)
    {
        Value = value;
    }

    public int Value { get; }
}

internal struct NewtonsoftOrdinaryParameterizedValue
{
    public NewtonsoftOrdinaryParameterizedValue(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

internal struct NewtonsoftThrowingValue
{
    [Newtonsoft.Json.JsonConstructor]
    private NewtonsoftThrowingValue(int value = 0)
    {
        throw new InvalidOperationException("newtonsoft-value-constructor");
    }

    public int Value { get; }
}

internal sealed class SystemTextJsonNonPublicIncludePayload
{
    [JsonInclude]
    [JsonPropertyName("private_field")]
#pragma warning disable CS0169, IDE0044 // Accessed only through serializer reflection metadata.
    private int privateField;
#pragma warning restore CS0169, IDE0044

    private string privateProperty = "default";

#pragma warning disable CS0169, IDE0044 // Verifies filtered non-public member reachability.
    private int unattributedField;
#pragma warning restore CS0169, IDE0044

    private string UnattributedProperty { get; set; } = "unattributed";

    [JsonInclude]
    [JsonPropertyName("private_property")]
    private string PrivateProperty
    {
        get => privateProperty;
        set => privateProperty = "set:" + value;
    }

    [JsonIgnore]
    public string PrivatePropertyValue => PrivateProperty;
}

internal sealed class NewtonsoftPrivateDefaultPayload
{
    private NewtonsoftPrivateDefaultPayload()
    {
        ConstructorMarker = "private-default";
    }

    public string ConstructorMarker { get; }

    public string Name { get; set; } = "default";
}

internal sealed class NewtonsoftParameterizedPayload
{
    private string name;

    public NewtonsoftParameterizedPayload(string name)
    {
        this.name = "ctor:" + name;
    }

    [Newtonsoft.Json.JsonProperty("display_name")]
    public string Name
    {
        get => name;
        set
        {
            name = "setter:" + value;
            SetterCalls++;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int SetterCalls { get; private set; }

    public int Count { get; set; }
}

internal sealed class NewtonsoftAttributedPrivatePayload
{
    [Newtonsoft.Json.JsonConstructor]
    private NewtonsoftAttributedPrivatePayload(string name)
    {
        Name = "attributed:" + name;
    }

    public string Name { get; }
}

internal sealed record NewtonsoftRecordPayload(
    [property: Newtonsoft.Json.JsonProperty("display_name")] string Name,
    int Count);

internal sealed class NewtonsoftOptionalConstructorPayload
{
    public NewtonsoftOptionalConstructorPayload(
        string name = "newtonsoft-default",
        int count = 23,
        bool active = true,
        long large = 9007199254740993L,
        string? absent = "newtonsoft-optional-default")
    {
        Name = name;
        Count = count;
        Active = active;
        Large = large;
        Absent = absent;
    }

    public string? Name { get; }

    public int Count { get; }

    public bool Active { get; }

    public long Large { get; }

    public string? Absent { get; }
}

internal sealed class NewtonsoftNonPublicMemberPayload
{
    [Newtonsoft.Json.JsonProperty("private_field")]
#pragma warning disable CS0169, IDE0044 // Accessed only through serializer reflection metadata.
    private int privateField;
#pragma warning restore CS0169, IDE0044

    private string privateProperty = "default";

#pragma warning disable CS0169, IDE0044 // Verifies filtered non-public member reachability.
    private int unattributedField;
#pragma warning restore CS0169, IDE0044

    private string UnattributedProperty { get; set; } = "unattributed";

    [Newtonsoft.Json.JsonProperty("private_property")]
    private string PrivateProperty
    {
        get => privateProperty;
        set => privateProperty = "set:" + value;
    }

    [Newtonsoft.Json.JsonIgnore]
    public string PrivatePropertyValue => PrivateProperty;
}

internal class SystemTextJsonBasePayload
{
    public string BaseProperty { get; set; } = "base-default";
}

internal sealed class SystemTextJsonDerivedPayload : SystemTextJsonBasePayload
{
    public string DerivedProperty { get; set; } = "derived-default";
}

internal class SystemTextJsonShadowedBase
{
    private string baseValue = "base-default";

    [JsonPropertyName("baseValue")]
    public string Value
    {
        get => baseValue;
        set => baseValue = value;
    }
}

internal sealed class SystemTextJsonShadowedPayload : SystemTextJsonShadowedBase
{
    private string derivedValue = "derived-default";

    [JsonPropertyName("derivedValue")]
    public new string Value
    {
        get => derivedValue;
        set => derivedValue = value;
    }
}

internal class NewtonsoftBasePayload
{
    public int BaseField = -1;

    public string BaseProperty { get; set; } = "base-default";
}

internal sealed class NewtonsoftDerivedPayload : NewtonsoftBasePayload
{
    public int DerivedField = -1;

    public string DerivedProperty { get; set; } = "derived-default";
}

internal class NewtonsoftShadowedBase
{
    private string baseValue = "base-default";

    [Newtonsoft.Json.JsonProperty("baseValue")]
    public string Value
    {
        get => baseValue;
        set => baseValue = value;
    }
}

internal sealed class NewtonsoftShadowedPayload : NewtonsoftShadowedBase
{
    private string derivedValue = "derived-default";

    public readonly int ReadOnlyField = 7;

    [Newtonsoft.Json.JsonProperty("derivedValue")]
    public new string Value
    {
        get => derivedValue;
        set => derivedValue = value;
    }

    public string ReadOnlyProperty => "read-only";

    public string this[int index] => $"item:{index}";
}

internal sealed class FloatingPointPayload
{
    public double DoubleValue { get; set; }

    public float SingleValue { get; set; }
}

internal sealed class SystemTextJsonNumberHandlingPayload
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public long QuotedInteger { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public double QuotedDouble { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int ReadQuotedWriteNumber { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    public double NamedDouble { get; set; }
}

[JsonNumberHandling(
    JsonNumberHandling.AllowReadingFromString
    | JsonNumberHandling.WriteAsString
    | JsonNumberHandling.AllowNamedFloatingPointLiterals)]
internal sealed class SystemTextJsonTypeNumberHandlingPayload
{
    public long QuotedInteger { get; set; }

    public double NamedDouble { get; set; }

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int StrictInteger { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class SystemTextJsonDisallowUnmappedPayload
{
    public int Known { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
internal sealed class SystemTextJsonSkipUnmappedPayload
{
    public int Known { get; set; }
}

internal sealed class SystemTextJsonOptionsPayload
{
    public string? URLValue { get; set; }

    public int SHA512Managed { get; set; }

    [JsonPropertyName("ExplicitName")]
    public string? ExplicitValue { get; set; }

    public int Count { get; set; }
}

internal sealed class SystemTextJsonOptionsConstructorPayload
{
    public SystemTextJsonOptionsConstructorPayload(string? URLValue)
    {
        this.URLValue = URLValue;
    }

    public string? URLValue { get; }
}

internal sealed class SystemTextJsonOptionsNumberPayload
{
    public int Count { get; set; }

    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int StrictCount { get; set; }
}

internal sealed class SystemTextJsonOptionsNamingCollisionPayload
{
    public string? URLValue { get; set; }

    public string? UrlValue { get; set; }
}

internal sealed class SystemTextJsonOptionsCaseCollisionPayload
{
    public string? Value { get; set; }

    public string? value { get; set; }
}

internal sealed class SystemTextJsonOptionsRequiredPayload
{
    [JsonRequired]
    public string? Value { get; set; }
}

internal sealed class SystemTextJsonMemberSelectionOptionsPayload
{
    public string? Value { get; set; } = "constructed";

    public string? NullValue { get; set; }

    public int ZeroValue { get; set; }

    public bool FalseValue { get; set; }

    public string ReadOnlyProperty => "read-only";

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int NeverIgnoredDefault { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttributeIgnoredNull { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string NeverIgnoredReadOnlyProperty => "kept";

    public int IncludedField = 3;

    public readonly int ReadOnlyField = 4;

    public int DefaultField = 0;

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public readonly int NeverIgnoredReadOnlyField = 5;
}

internal sealed class SystemTextJsonMemberSelectionCollisionPayload
{
    public int Value { get; set; }

    [JsonPropertyName("Value")]
    public int CollidingField = 0;
}

internal sealed class SystemTextJsonIgnoreNullValuesPayload
{
    private string? _tracked = "tracked";

    public string? Value { get; set; } = "constructed";

    [JsonRequired]
    public string? Required { get; set; } = "required";

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Never { get; set; } = "never";

    public string? NullValue { get; set; }

    public string? Tracked
    {
        get => _tracked;
        set
        {
            _tracked = value;
            SetterCalls++;
        }
    }

    [JsonIgnore]
    public int SetterCalls { get; private set; }

    public string? Field = "field";

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? NeverField = "never-field";
}

internal sealed class SystemTextJsonIgnoreNullValuesConstructorPayload
{
    public SystemTextJsonIgnoreNullValuesConstructorPayload(string? value = "fallback")
    {
        Value = value;
    }

    public string? Value { get; }
}

internal sealed class SystemTextJsonReaderOptionsPayload
{
    public int Value { get; set; }

    public string? Text { get; set; }
}

internal sealed class SystemTextJsonDepthOptionsPayload
{
    public int Value { get; set; }

    public SystemTextJsonDepthOptionsPayload? Child { get; set; }
}

internal sealed class SystemTextJsonReferencePairPayload
{
    public SystemTextJsonDepthOptionsPayload? First { get; set; }

    public SystemTextJsonDepthOptionsPayload? Second { get; set; }
}

internal sealed class SystemTextJsonCustomNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name) => "custom_" + name;
}

[Newtonsoft.Json.JsonObject(
    MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Error)]
internal sealed class NewtonsoftErrorMissingMemberPayload
{
    public int Known { get; set; }
}

[Newtonsoft.Json.JsonObject(
    MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore)]
internal sealed class NewtonsoftIgnoreMissingMemberPayload
{
    public int Known { get; set; }
}

[Newtonsoft.Json.JsonObject(ItemRequired = Newtonsoft.Json.Required.Always)]
internal sealed class NewtonsoftItemRequiredAlwaysPayload
{
    public string? RequiredByType { get; set; }

    [Newtonsoft.Json.JsonProperty(Required = Newtonsoft.Json.Required.Default)]
    public string? ExplicitDefault { get; set; }

    [Newtonsoft.Json.JsonProperty(Required = Newtonsoft.Json.Required.AllowNull)]
    public string? ExplicitAllowNull { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    public string? Ignored { get; set; }
}

[Newtonsoft.Json.JsonObject(ItemRequired = Newtonsoft.Json.Required.AllowNull)]
internal sealed class NewtonsoftItemRequiredAllowNullPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(ItemRequired = Newtonsoft.Json.Required.DisallowNull)]
internal sealed class NewtonsoftItemRequiredDisallowNullPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(ItemRequired = (Newtonsoft.Json.Required)99)]
internal sealed class NewtonsoftInvalidItemRequiredPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    ItemNullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
internal sealed class NewtonsoftItemNullIgnorePayload
{
    public string? IgnoredByType { get; set; } = "type-default";

    [Newtonsoft.Json.JsonProperty(
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
    public string? ExplicitInclude { get; set; } = "include-default";
}

[Newtonsoft.Json.JsonObject(
    ItemNullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore,
    ItemRequired = Newtonsoft.Json.Required.Always)]
internal sealed class NewtonsoftItemNullIgnoreRequiredPayload
{
    public string? Value { get; set; } = "required-default";
}

[Newtonsoft.Json.JsonObject(
    ItemNullValueHandling = (Newtonsoft.Json.NullValueHandling)99)]
internal sealed class NewtonsoftInvalidItemNullPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    Id = "descriptive-contract",
    Title = "Descriptive contract",
    Description = "Schema-only metadata",
    IsReference = false,
    ItemIsReference = false,
    ItemReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Error,
    ItemTypeNameHandling = Newtonsoft.Json.TypeNameHandling.None)]
internal sealed class NewtonsoftDescriptiveJsonObjectPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(IsReference = true)]
internal sealed class NewtonsoftReferenceJsonObjectPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(ItemIsReference = true)]
internal sealed class NewtonsoftItemReferenceJsonObjectPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    ItemReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore)]
internal sealed class NewtonsoftItemReferenceLoopJsonObjectPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    ItemTypeNameHandling = Newtonsoft.Json.TypeNameHandling.Objects)]
internal sealed class NewtonsoftItemTypeNameJsonObjectPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
internal sealed class NewtonsoftCamelNamingPayload
{
    public string? URLValue { get; set; }

    [Newtonsoft.Json.JsonProperty("ExplicitName")]
    public string? ExplicitValue { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy),
    NamingStrategyParameters = new object[] { false, true, false })]
internal sealed class NewtonsoftCamelOverrideNamingPayload
{
    [Newtonsoft.Json.JsonProperty("ExplicitName")]
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
internal sealed class NewtonsoftSnakeConstructorNamingPayload
{
    public NewtonsoftSnakeConstructorNamingPayload(string? URLValue)
    {
        this.URLValue = URLValue;
    }

    public string? URLValue { get; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
internal sealed class NewtonsoftKebabNamingPayload
{
    public int SHA512Managed { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
internal sealed class NewtonsoftMemberNamingPayload
{
    [Newtonsoft.Json.JsonProperty(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
    public string? HTTPValue { get; set; }

    [Newtonsoft.Json.JsonProperty(
        "ExplicitHTTPName",
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy),
        NamingStrategyParameters = new object[] { false, true })]
    public string? ExplicitValue { get; set; }
}

internal class NewtonsoftBaseNamingPayload
{
    public string? BaseProperty { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
internal sealed class NewtonsoftDerivedNamingPayload : NewtonsoftBaseNamingPayload
{
    public string? DerivedProperty { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.DefaultNamingStrategy))]
internal sealed class NewtonsoftDefaultNamingPayload
{
    public string? PascalName { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
internal sealed class NewtonsoftNamingCollisionPayload
{
    public string? URLValue { get; set; }

    public string? UrlValue { get; set; }
}

internal sealed class NewtonsoftCustomNamingStrategy
    : Newtonsoft.Json.Serialization.NamingStrategy
{
    protected override string ResolvePropertyName(string name) => "custom_" + name;
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType = typeof(NewtonsoftCustomNamingStrategy))]
internal sealed class NewtonsoftCustomNamingPayload
{
    public string? Value { get; set; }
}

[Newtonsoft.Json.JsonObject(
    NamingStrategyType =
        typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy),
    NamingStrategyParameters = new object[] { 1 })]
internal sealed class NewtonsoftMalformedNamingParametersPayload
{
    public string? Value { get; set; }
}

internal sealed class SystemTextJsonOrderedPayload
{
    public int DefaultFirst { get; set; } = 1;

    [System.Text.Json.Serialization.JsonPropertyOrder(2)]
    public int Late { get; set; } = 6;

    [System.Text.Json.Serialization.JsonPropertyOrder(-2)]
    public int Early { get; set; } = 3;

    public int DefaultSecond { get; set; } = 4;

    [System.Text.Json.Serialization.JsonPropertyOrder(1)]
    public int AfterDefaults { get; set; } = 5;

    [System.Text.Json.Serialization.JsonInclude]
    [System.Text.Json.Serialization.JsonPropertyOrder(-3)]
    public int IncludedField = 2;
}

internal sealed class NewtonsoftOrderedPayload
{
    [Newtonsoft.Json.JsonProperty(Order = -3)]
    public int EarlyField = 1;

    public int DefaultField = 2;

    [Newtonsoft.Json.JsonProperty(Order = 2)]
    public int LateProperty { get; set; } = 6;

    [Newtonsoft.Json.JsonProperty(Order = -2)]
    public int EarlyProperty { get; set; } = 3;

    public int DefaultProperty { get; set; } = 4;

    [Newtonsoft.Json.JsonProperty(Order = 1)]
    public int AfterDefaults { get; set; } = 5;
}

internal sealed class SystemTextJsonRequiredPayload
{
    [System.Text.Json.Serialization.JsonPropertyName("required_text")]
    [System.Text.Json.Serialization.JsonRequired]
    public string? RequiredText { get; set; }
}

internal sealed class SystemTextJsonKeywordRequiredPayload
{
    public required string? KeywordRequired { get; set; }
}

internal sealed class NewtonsoftRequiredPayload
{
    [Newtonsoft.Json.JsonRequired]
    public string? Always { get; set; }

    [Newtonsoft.Json.JsonProperty(Required = Newtonsoft.Json.Required.AllowNull)]
    public string? AllowNull { get; set; }

    [Newtonsoft.Json.JsonProperty(Required = Newtonsoft.Json.Required.DisallowNull)]
    public string? DisallowNull { get; set; }
}

internal sealed class NewtonsoftNullValueHandlingPayload
{
    private string? ignored = "ignored-default";
    private string? included = "included-default";

    [Newtonsoft.Json.JsonIgnore]
    public int IgnoredSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public int IncludedSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonProperty(
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public string? Ignored
    {
        get => ignored;
        set
        {
            ignored = value;
            IgnoredSetterCalls++;
        }
    }

    [Newtonsoft.Json.JsonProperty(
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)]
    public string? Included
    {
        get => included;
        set
        {
            included = value;
            IncludedSetterCalls++;
        }
    }
}

internal sealed class NewtonsoftDefaultValueHandlingPayload
{
    private int ignored = 5;
    private int included = 5;
    private int populated = 5;
    private int ignoredAndPopulated = 5;

    [Newtonsoft.Json.JsonIgnore]
    public int IgnoredSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public int IncludedSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public int PopulatedSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public int IgnoredAndPopulatedSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public int Ignored
    {
        get => ignored;
        set
        {
            ignored = value;
            IgnoredSetterCalls++;
        }
    }

    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include)]
    public int Included
    {
        get => included;
        set
        {
            included = value;
            IncludedSetterCalls++;
        }
    }

    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
    public int Populated
    {
        get => populated;
        set
        {
            populated = value;
            PopulatedSetterCalls++;
        }
    }

    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.IgnoreAndPopulate)]
    public int IgnoredAndPopulated
    {
        get => ignoredAndPopulated;
        set
        {
            ignoredAndPopulated = value;
            IgnoredAndPopulatedSetterCalls++;
        }
    }

    [System.ComponentModel.DefaultValue(7)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public int AttributedIgnoredInteger { get; set; } = 3;

    [System.ComponentModel.DefaultValue(7)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Populate)]
    public int AttributedPopulatedInteger { get; set; } = 3;

    [System.ComponentModel.DefaultValue("custom")]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.IgnoreAndPopulate)]
    public string AttributedAssignedString { get; set; } = "initial";

    [System.ComponentModel.DefaultValue(true)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public bool AttributedIgnoredBoolean { get; set; }

    [System.ComponentModel.DefaultValue('Z')]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public char AttributedIgnoredCharacter { get; set; } = 'A';

    [System.ComponentModel.DefaultValue(9007199254740993L)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public long AttributedIgnoredInt64 { get; set; } = 4;

    [System.ComponentModel.DefaultValue(9007199254740993UL)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public ulong AttributedIgnoredUInt64 { get; set; } = 4;

    [System.ComponentModel.DefaultValue(1.25f)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public float AttributedIgnoredSingle { get; set; } = 2.5f;

    [System.ComponentModel.DefaultValue(1.25d)]
    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
    public double AttributedIgnoredDouble { get; set; } = 2.5d;

    public void SetAttributedDefaults()
    {
        AttributedIgnoredInteger = 7;
        AttributedPopulatedInteger = 7;
        AttributedAssignedString = "custom";
        AttributedIgnoredBoolean = true;
        AttributedIgnoredCharacter = 'Z';
        AttributedIgnoredInt64 = 9007199254740993L;
        AttributedIgnoredUInt64 = 9007199254740993UL;
        AttributedIgnoredSingle = 1.25f;
        AttributedIgnoredDouble = 1.25d;
    }
}

[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
internal sealed class NewtonsoftOptInPayload
{
    [Newtonsoft.Json.JsonProperty("included_property")]
    public string IncludedProperty { get; set; } = "included-default";

    public string ExcludedProperty { get; set; } = "excluded-default";

    [Newtonsoft.Json.JsonProperty("included_field")]
#pragma warning disable IDE0044 // Mutated through serializer reflection metadata.
    private int includedField = 1;
#pragma warning restore IDE0044

    public int ExcludedField = 2;

    public int IncludedFieldValue => includedField;
}

internal static class NewtonsoftOptInBrowserVerification
{
    public static bool Succeeded { get; } = Verify();

    private static bool Verify()
    {
        const string json = "{\"included_property\":\"input\",\"ExcludedProperty\":\"incoming\","
            + "\"included_field\":7,\"ExcludedField\":9}";
        var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<NewtonsoftOptInPayload>(json);
        if (payload == null) return false;

        var fieldsModeRejected = false;
        try
        {
            Newtonsoft.Json.JsonConvert.SerializeObject(new NewtonsoftFieldsPayload());
        }
        catch (NotSupportedException)
        {
            fieldsModeRejected = true;
        }

        const string expected = "{\"included_field\":7,\"included_property\":\"input\"}";
        return fieldsModeRejected
            && payload.IncludedProperty == "input"
            && payload.ExcludedProperty == "excluded-default"
            && payload.IncludedFieldValue == 7
            && payload.ExcludedField == 2
            && Newtonsoft.Json.JsonConvert.SerializeObject(payload) == expected
            && Newtonsoft.Json.JsonConvert.SerializeObject(payload, settings: null) == expected;
    }
}

[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.Fields)]
internal sealed class NewtonsoftFieldsPayload
{
    public int Value = 1;
}

internal static class EnumBrowserVerification
{
    public static string? Failure { get; private set; }
    public static bool Succeeded => VerifySafely();

    private static bool VerifySafely()
    {
        try
        {
            return Verify();
        }
        catch (Exception exception)
        {
            Failure = exception.Message;
            return false;
        }
    }

    private static bool Fail(string reason)
    {
        Failure = reason;
        return false;
    }

    private static bool Verify()
    {
        const string json = "{\"SByte\":-128,\"Byte\":255,\"Int16\":-32768,\"UInt16\":65535,"
            + "\"Int32\":-2147483648,\"UInt32\":4294967295,"
            + "\"Int64\":-9007199254740993,\"UInt64\":9007199254740993,"
            + "\"Flags\":3,\"Undefined\":123,\"Values\":[-9007199254740993,7]}";
        var systemTextJson = JsonSerializer.Deserialize<JsonEnumPayload>(json);
        var newtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonEnumPayload>(json);
        var systemTextJsonStringRejected = false;
        var newtonsoftMaximumRejected = false;
        try
        {
            JsonSerializer.Deserialize<JsonFlagsEnum>("\"Read\"");
        }
        catch (JsonException)
        {
            systemTextJsonStringRejected = true;
        }
        try
        {
            Newtonsoft.Json.JsonConvert.DeserializeObject<JsonUInt64Enum>(
                "18446744073709551615");
        }
        catch (Newtonsoft.Json.JsonSerializationException)
        {
            newtonsoftMaximumRejected = true;
        }

        var systemTextJsonOptions = new JsonSerializerOptions();
        systemTextJsonOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var newtonsoftSettings = new Newtonsoft.Json.JsonSerializerSettings();
        newtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter(
                new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy(),
                allowIntegerValues: false));
        var systemTextJsonUndefinedRejected = false;
        var newtonsoftUndefinedRejected = false;
        try
        {
            JsonSerializer.Serialize((JsonFlagsEnum)8, systemTextJsonOptions);
        }
        catch (JsonException)
        {
            systemTextJsonUndefinedRejected = true;
        }
        try
        {
            Newtonsoft.Json.JsonConvert.SerializeObject(
                (JsonFlagsEnum)8,
                newtonsoftSettings);
        }
        catch (Newtonsoft.Json.JsonSerializationException)
        {
            newtonsoftUndefinedRejected = true;
        }

        var systemTextJsonOutput = JsonSerializer.Serialize(systemTextJson);
        if (systemTextJsonOutput != json) return Fail("System.Text.Json output: " + systemTextJsonOutput);
        var newtonsoftOutput = Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft);
        if (newtonsoftOutput != json) return Fail("Newtonsoft output: " + newtonsoftOutput);
        var systemTextJsonPayloadFailure = FindPayloadFailure(systemTextJson);
        if (systemTextJsonPayloadFailure != string.Empty)
            return Fail("System.Text.Json payload: " + systemTextJsonPayloadFailure);
        var newtonsoftPayloadFailure = FindPayloadFailure(newtonsoft);
        if (newtonsoftPayloadFailure != string.Empty)
            return Fail("Newtonsoft payload: " + newtonsoftPayloadFailure);
        if (JsonSerializer.Serialize(JsonInt64Enum.NegativeLarge) != "-9007199254740993")
            return Fail("System.Text.Json Int64 root");
        if (Newtonsoft.Json.JsonConvert.SerializeObject(JsonUInt64Enum.Maximum)
            != "18446744073709551615") return Fail("Newtonsoft UInt64 root");
        if (JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonUInt64Enum>("18446744073709551615"))
            != "18446744073709551615") return Fail("System.Text.Json UInt64 root");
        if (JsonSerializer.Deserialize<JsonInt64Enum>("-9007199254740993")
            != JsonInt64Enum.NegativeLarge) return Fail("System.Text.Json Int64 read");
        if (Newtonsoft.Json.JsonConvert.DeserializeObject<JsonFlagsEnum>("\"read, WRITE\"")
            != (JsonFlagsEnum.Read | JsonFlagsEnum.Write)) return Fail("Newtonsoft flags root");
        if (Newtonsoft.Json.JsonConvert.DeserializeObject<JsonInt64Enum>(
            "\"NegativeLargeAlias\"") != JsonInt64Enum.NegativeLarge)
            return Fail("Newtonsoft alias root");
        if (!systemTextJsonStringRejected) return Fail("System.Text.Json string acceptance");
        if (!newtonsoftMaximumRejected) return Fail("Newtonsoft UInt64 acceptance");
        if (JsonSerializer.Serialize(
                JsonFlagsEnum.Read | JsonFlagsEnum.Write,
                systemTextJsonOptions) != "\"read, write\"")
            return Fail("System.Text.Json string enum write");
        if (JsonSerializer.Deserialize<JsonFlagsEnum>(
                "\"READ, write\"",
                systemTextJsonOptions) != (JsonFlagsEnum.Read | JsonFlagsEnum.Write))
            return Fail("System.Text.Json string enum read");
        if (Newtonsoft.Json.JsonConvert.SerializeObject(
                JsonFlagsEnum.Read | JsonFlagsEnum.Write,
                newtonsoftSettings) != "\"read, write\"")
            return Fail("Newtonsoft string enum write");
        if (Newtonsoft.Json.JsonConvert.DeserializeObject<JsonFlagsEnum>(
                "\"READ, write\"",
                newtonsoftSettings) != (JsonFlagsEnum.Read | JsonFlagsEnum.Write))
            return Fail("Newtonsoft string enum read");
        if (!systemTextJsonUndefinedRejected)
            return Fail("System.Text.Json string enum integer write");
        if (!newtonsoftUndefinedRejected)
            return Fail("Newtonsoft string enum integer write");

        var genericSystemTextJsonOptions = new JsonSerializerOptions();
        genericSystemTextJsonOptions.Converters.Add(
            new JsonStringEnumConverter<JsonNamedEnum>(
                JsonNamingPolicy.KebabCaseUpper,
                allowIntegerValues: false));
        if (JsonSerializer.Serialize(
                JsonNamedEnum.FirstValue,
                genericSystemTextJsonOptions) != "\"FIRST-VALUE\"")
            return Fail("System.Text.Json generic string enum write");
        if (JsonSerializer.Deserialize<JsonNamedEnum>(
                "\"FIRST-VALUE\"",
                genericSystemTextJsonOptions) != JsonNamedEnum.FirstValue)
            return Fail("System.Text.Json generic string enum read");
        var transformedCaseRejected = false;
        try
        {
            JsonSerializer.Deserialize<JsonNamedEnum>(
                "\"first-value\"",
                genericSystemTextJsonOptions);
        }
        catch (JsonException)
        {
            transformedCaseRejected = true;
        }
        if (!transformedCaseRejected)
            return Fail("System.Text.Json transformed enum name casing");
        var genericNumberRejected = false;
        try
        {
            JsonSerializer.Deserialize<JsonNamedEnum>(
                "1",
                genericSystemTextJsonOptions);
        }
        catch (JsonException)
        {
            genericNumberRejected = true;
        }
        if (!genericNumberRejected)
            return Fail("System.Text.Json generic string enum integer read");
        if (JsonSerializer.Serialize(
                JsonFlagsEnum.Read,
                genericSystemTextJsonOptions) != "1")
            return Fail("System.Text.Json generic converter scope");

        var namedNewtonsoftSettings = new Newtonsoft.Json.JsonSerializerSettings();
        namedNewtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter(
                new Newtonsoft.Json.Serialization.KebabCaseNamingStrategy(),
                allowIntegerValues: false));
        if (Newtonsoft.Json.JsonConvert.SerializeObject(
                JsonNamedEnum.FirstValue,
                namedNewtonsoftSettings) != "\"first-value\"")
            return Fail("Newtonsoft named string enum write");
        if (Newtonsoft.Json.JsonConvert.DeserializeObject<JsonNamedEnum>(
                "\"FIRST-VALUE\"",
                namedNewtonsoftSettings) != JsonNamedEnum.FirstValue)
            return Fail("Newtonsoft named string enum read");

        var defaultNewtonsoftSettings = new Newtonsoft.Json.JsonSerializerSettings();
        defaultNewtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter());
        if (Newtonsoft.Json.JsonConvert.SerializeObject(
                (JsonFlagsEnum)8,
                defaultNewtonsoftSettings) != "8")
            return Fail("Newtonsoft default string enum integer write");
        if (Newtonsoft.Json.JsonConvert.DeserializeObject<JsonFlagsEnum>(
                "8",
                defaultNewtonsoftSettings) != (JsonFlagsEnum)8)
            return Fail("Newtonsoft default string enum integer read");
        return true;
    }

    private static string FindPayloadFailure(JsonEnumPayload? payload)
    {
        if (payload == null) return "null";
        if (payload.SByte != JsonSByteEnum.Minimum) return "SByte";
        if (payload.Byte != JsonByteEnum.Maximum) return "Byte";
        if (payload.Int16 != JsonInt16Enum.Minimum) return "Int16";
        if (payload.UInt16 != JsonUInt16Enum.Maximum) return "UInt16";
        if (payload.Int32 != JsonInt32Enum.Minimum) return "Int32";
        if (payload.Int64 != JsonInt64Enum.NegativeLarge) return "Int64";
        if (payload.UInt64 != JsonUInt64Enum.Large) return "UInt64";
        if (payload.Flags != (JsonFlagsEnum.Read | JsonFlagsEnum.Write)) return "Flags";
        if (payload.Undefined != (JsonInt32Enum)123) return "Undefined";
        if (payload.Values.Length != 2) return "Values.Length";
        if (payload.Values[0] != JsonInt64Enum.NegativeLarge) return "Values[0]";
        if (payload.Values[1] != (JsonInt64Enum)7) return "Values[1]";
        return string.Empty;
    }
}

internal enum JsonSByteEnum : sbyte
{
    Minimum = sbyte.MinValue,
}

internal enum JsonByteEnum : byte
{
    Maximum = byte.MaxValue,
}

internal enum JsonInt16Enum : short
{
    Minimum = short.MinValue,
}

internal enum JsonUInt16Enum : ushort
{
    Maximum = ushort.MaxValue,
}

internal enum JsonInt32Enum : int
{
    Minimum = int.MinValue,
}

internal enum JsonUInt32Enum : uint
{
    Maximum = uint.MaxValue,
}

internal enum JsonInt64Enum : long
{
    NegativeLarge = -9007199254740993L,
    NegativeLargeAlias = NegativeLarge,
}

internal enum JsonUInt64Enum : ulong
{
    Large = 9007199254740993UL,
    Maximum = ulong.MaxValue,
}

[Flags]
internal enum JsonFlagsEnum : byte
{
    None = 0,
    Read = 1,
    Write = 2,
}

internal enum JsonNamedEnum
{
    FirstValue = 1,
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
    public JsonInt32Enum Undefined { get; set; }
    public JsonInt64Enum[] Values { get; set; } = [];
}

internal enum UnusedJsonEnum : int
{
    Value = 1,
}

internal sealed class NumericStringPayload
{
    public int Count { get; set; }

    public long Large { get; set; }
}

internal sealed class BooleanPayload
{
    public bool Active { get; set; }
}

internal sealed class StringConversionPayload
{
    public string Text { get; set; } = string.Empty;
}

internal sealed class NewtonsoftWriterSettingsPayload
{
    public string Text { get; set; } = string.Empty;

    public int[] Values { get; set; } = [];
}

internal sealed class NewtonsoftGlobalSettingsPayload
{
    private string? _ignoredNull = "ignored-null-default";
    private string? _includedNull = "included-null-default";
    private int _ignoredDefault = 5;
    private int _includedDefault = 6;
    private int _attributedDefault = 3;

    public string? IgnoredNull
    {
        get => _ignoredNull;
        set
        {
            IgnoredNullSetterCalls++;
            _ignoredNull = value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int IgnoredNullSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonProperty(
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include,
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include)]
    public string? IncludedNull
    {
        get => _includedNull;
        set
        {
            IncludedNullSetterCalls++;
            _includedNull = value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int IncludedNullSetterCalls { get; private set; }

    public int IgnoredDefault
    {
        get => _ignoredDefault;
        set
        {
            IgnoredDefaultSetterCalls++;
            _ignoredDefault = value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int IgnoredDefaultSetterCalls { get; private set; }

    [Newtonsoft.Json.JsonProperty(
        DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include)]
    public int IncludedDefault
    {
        get => _includedDefault;
        set
        {
            IncludedDefaultSetterCalls++;
            _includedDefault = value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int IncludedDefaultSetterCalls { get; private set; }

    [System.ComponentModel.DefaultValue(7)]
    public int AttributedDefault
    {
        get => _attributedDefault;
        set
        {
            AttributedDefaultSetterCalls++;
            _attributedDefault = value;
        }
    }

    [Newtonsoft.Json.JsonIgnore]
    public int AttributedDefaultSetterCalls { get; private set; }
}

internal sealed class NewtonsoftGlobalSettingsConstructorPayload
{
    [Newtonsoft.Json.JsonConstructor]
    public NewtonsoftGlobalSettingsConstructorPayload(int count)
    {
        Count = count;
    }

    [System.ComponentModel.DefaultValue(7)]
    public int Count { get; }
}

internal sealed class NewtonsoftConstructorHandlingPayload
{
    private NewtonsoftConstructorHandlingPayload()
    {
        Source = "private";
    }

    public NewtonsoftConstructorHandlingPayload(string? marker)
    {
        Source = "public:" + marker;
    }

    public string Source { get; }

    public string? Value { get; set; }
}

internal sealed class NewtonsoftAttributedConstructorHandlingPayload
{
    private NewtonsoftAttributedConstructorHandlingPayload()
    {
        Source = "private";
    }

    [Newtonsoft.Json.JsonConstructor]
    public NewtonsoftAttributedConstructorHandlingPayload(string? marker)
    {
        Source = "attributed:" + marker;
    }

    public string Source { get; }

    public string? Value { get; set; }
}

internal sealed class NewtonsoftLoopSettingsPayload
{
    public string? Name { get; set; }

    public NewtonsoftLoopSettingsPayload? Self { get; set; }
}

internal sealed class NewtonsoftMemberIgnoreLoopPayload
{
    public string? Name { get; set; }

    [Newtonsoft.Json.JsonProperty(
        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore)]
    public NewtonsoftMemberIgnoreLoopPayload? Self { get; set; }
}

internal sealed class NewtonsoftMemberErrorLoopPayload
{
    public string? Name { get; set; }

    [Newtonsoft.Json.JsonProperty(
        ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Error)]
    public NewtonsoftMemberErrorLoopPayload? Self { get; set; }
}

internal sealed class SystemTextJsonNameCollisionPayload
{
    [JsonPropertyName("same")]
    public int First { get; set; }

    [JsonPropertyName("same")]
    public int Second { get; set; }
}

internal sealed class NewtonsoftNameCollisionPayload
{
    [Newtonsoft.Json.JsonProperty("same")]
    public int First { get; set; }

    [Newtonsoft.Json.JsonProperty("same")]
    public int Second { get; set; }
}

internal sealed class CharacterPayload
{
    public char Value { get; set; }
}

internal static class CollectionBrowserVerification
{
    public static string Failure { get; private set; } = string.Empty;

    public static bool Verify()
    {
        var operation = "Dictionary custom comparer behavior";
        Mark(operation);
        var comparer = new BrowserAsciiIgnoreCaseComparer();
        operation = "Dictionary custom comparer direct equality";
        Mark(operation);
        if (!comparer.Equals("alpha", "ALPHA")) return Fail(operation);
        operation = "Dictionary custom comparer direct hash";
        Mark(operation);
        if (comparer.GetHashCode("alpha") != comparer.GetHashCode("ALPHA")) return Fail(operation);
        operation = "Dictionary custom comparer routing";
        Mark(operation);
        var comparerDictionary = new Dictionary<string, int>(comparer)
        {
            ["alpha"] = 1,
        };
        if (!comparerDictionary.ContainsKey("ALPHA")
            || comparerDictionary["Alpha"] != 1)
        {
            return Fail(operation);
        }
        operation = "Dictionary managed key behavior";
        Mark(operation);
        var firstManagedKey = new BrowserDictionaryKey(7);
        var secondManagedKey = new BrowserDictionaryKey(7);
        var thirdManagedKey = new BrowserDictionaryKey(7);
        var managedKeyDictionary = new Dictionary<BrowserDictionaryKey, string>
        {
            [firstManagedKey] = "value",
        };
        operation = "Dictionary managed key ContainsKey";
        Mark(operation);
        if (!managedKeyDictionary.ContainsKey(secondManagedKey))
        {
            return Fail(operation);
        }
        operation = "Dictionary managed key indexer";
        Mark(operation);
        if (managedKeyDictionary[thirdManagedKey] != "value")
        {
            return Fail(operation);
        }

        operation = "System.Text.Json List<T> read";
        Mark(operation);
        {
            const string listJson = "[{\"Name\":\"first\",\"Value\":1},{\"Name\":\"second\",\"Value\":2}]";
            const string dictionaryJson = "{\"first\":{\"Name\":\"one\",\"Value\":1},\"empty\":null}";

            var systemTextJsonList = JsonSerializer.Deserialize<List<BrowserCollectionItem>>(listJson);
            operation = "System.Text.Json IList<T> read";
            Mark(operation);
            var systemTextJsonInterface = JsonSerializer.Deserialize<IList<int>>("[1,2,3]");
            operation = "System.Text.Json Dictionary<TKey, TValue> read";
            Mark(operation);
            var systemTextJsonDictionary =
                JsonSerializer.Deserialize<Dictionary<string, BrowserCollectionItem?>>(dictionaryJson);
            operation = "Newtonsoft.Json List<T> read";
            Mark(operation);
            var newtonsoftList =
                Newtonsoft.Json.JsonConvert.DeserializeObject<List<BrowserCollectionItem>>(listJson);
            operation = "Newtonsoft.Json IList<T> read";
            Mark(operation);
            var newtonsoftInterface =
                Newtonsoft.Json.JsonConvert.DeserializeObject<IList<int>>("[1,2,3]");
            operation = "Newtonsoft.Json Dictionary<TKey, TValue> read";
            Mark(operation);
            var newtonsoftDictionary =
                Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, BrowserCollectionItem?>>(dictionaryJson);

            operation = "System.Text.Json List<T> write";
            Mark(operation);
            if (JsonSerializer.Serialize(systemTextJsonList) != listJson) return Fail(operation);
            operation = "System.Text.Json IList<T> write";
            Mark(operation);
            if (JsonSerializer.Serialize(systemTextJsonInterface) != "[1,2,3]") return Fail(operation);
            operation = "System.Text.Json Dictionary<TKey, TValue> write";
            Mark(operation);
            if (JsonSerializer.Serialize(systemTextJsonDictionary) != dictionaryJson) return Fail(operation);
            operation = "Newtonsoft.Json List<T> write";
            Mark(operation);
            if (Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftList) != listJson) return Fail(operation);
            operation = "Newtonsoft.Json IList<T> write";
            Mark(operation);
            if (Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftInterface) != "[1,2,3]") return Fail(operation);
            operation = "Newtonsoft.Json Dictionary<TKey, TValue> write";
            Mark(operation);
            if (Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoftDictionary) != dictionaryJson)
                return Fail(operation);

            operation = "System.Text.Json DictionaryKeyPolicy read";
            Mark(operation);
            var dictionaryOptions = new JsonSerializerOptions
            {
                DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            };
            var policyDictionary = JsonSerializer.Deserialize<Dictionary<string, int>>(
                "{\"URLValue\":1,\"SHA512Managed\":2}",
                dictionaryOptions);
            if (policyDictionary == null
                || policyDictionary["URLValue"] != 1
                || policyDictionary["SHA512Managed"] != 2)
                return Fail(operation);

            operation = "System.Text.Json DictionaryKeyPolicy write";
            Mark(operation);
            return JsonSerializer.Serialize(policyDictionary, dictionaryOptions)
                    == "{\"url_value\":1,\"sha512_managed\":2}"
                || Fail(operation);
        }
    }

    private static bool Fail(string reason)
    {
        Failure = reason;
        return false;
    }

    private static void Mark(string operation) =>
        Document.Body.SetAttribute("data-fecs-json-collection-status", operation);
}

internal sealed class BrowserCollectionItem
{
    public string? Name { get; set; }

    public int Value { get; set; }
}

internal static class FieldIdentityBrowserVerification
{
    public static bool Verify()
    {
        var derived = new FieldIdentityDerived<int>();
        FieldIdentityBase<int> asBase = derived;
        derived.Value = 11;
        asBase.Value = 22;
        derived.Property = 33;
        asBase.SetBaseProperty(44);
        derived.SetDerivedValueViaReference(55);
        asBase.SetBaseValueViaReference(66);

        var reflected = 0;
        foreach (var field in typeof(FieldIdentityDerived<int>).GetFields())
        {
            if (field.Name != nameof(FieldIdentityDerived<int>.Value)) continue;
            var current = (int)field.GetValue(derived)!;
            field.SetValue(derived, current == 55 ? 77 : 88);
            reflected++;
        }

        return derived.Property == 33
            && asBase.ReadBaseProperty() == 44
            && derived.Value == 77
            && asBase.Value == 88
            && reflected == 2;
    }
}

internal class FieldIdentityBase<T>
{
    public T Value = default!;

    public T Property { get; set; } = default!;

    public void SetBaseProperty(T value) => Property = value;

    public T ReadBaseProperty() => Property;

    public void SetBaseValueViaReference(T value) => Assign(ref Value, value);

    private static void Assign(ref T location, T value) => location = value;
}

internal sealed class FieldIdentityDerived<T> : FieldIdentityBase<T>
{
    public new T Value = default!;

    public new T Property { get; set; } = default!;

    public void SetDerivedValueViaReference(T value) => Assign(ref Value, value);

    private static void Assign(ref T location, T value) => location = value;
}

internal sealed class BrowserAsciiIgnoreCaseComparer : IEqualityComparer<string>
{
    public bool Equals(string? left, string? right)
    {
        return left == right
            || left == "alpha" && right == "ALPHA"
            || left == "ALPHA" && right == "alpha"
            || left == "Alpha" && (right == "alpha" || right == "ALPHA")
            || right == "Alpha" && (left == "alpha" || left == "ALPHA");
    }

    public int GetHashCode(string value) => 1;
}

internal sealed class BrowserDictionaryKey
{
    public BrowserDictionaryKey(int id) => Id = id;

    public int Id { get; }

    public override bool Equals(object? obj) =>
        obj is BrowserDictionaryKey other && other.Id == Id;

    public override int GetHashCode() => Id;
}
