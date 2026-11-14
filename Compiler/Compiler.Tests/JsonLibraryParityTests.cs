using System.ComponentModel;
using System.Net.Http.Json;
using Newtonsoft.Json;

namespace Compiler.Tests;

public sealed class JsonLibraryParityTests
{
    private const string SystemTextJsonPayload =
        "{\"Summary\":\"clear\",\"temperatureC\":19,\"temperatureC\":21,\"Active\":true,"
        + "\"Ignored\":\"incoming\",\"Text\":\"line\\n\\u003C\\u00E9+&'>\","
        + "\"Large\":9007199254740993,\"Optional\":null,\"Included\":\"included\","
        + "\"InitOnly\":\"init\",\"NeverIgnored\":\"never\",\"ReadOnly\":\"incoming\","
        + "\"Item\":\"incoming\",\"Unknown\":42}";

    private const string NewtonsoftPayload =
        "{\"summary\":\"clear\",\"temperature_c\":19,\"temperature_c\":21,\"active\":true,"
        + "\"Ignored\":\"incoming\",\"publicfield\":7,\"text\":\"line\\n\\u003C\\u00E9+&'>\","
        + "\"large\":9007199254740993,\"optional\":null,\"Unknown\":42}";

    [Test]
    public async Task HttpContentReadFromJsonAsyncBaseline()
    {
        const string json =
            "{\"location\":\"Seattle\",\"forecasts\":[{\"day\":\"Monday\",\"temperatureC\":18},"
            + "{\"day\":\"Tuesday\",\"temperatureC\":21}],\"measurements\":{\"Humidity\":72}}";
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var payload = await content.ReadFromJsonAsync<HttpJsonResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Seattle", payload.Location);
        Assert.Collection(
            payload.Forecasts,
            forecast =>
            {
                Assert.Equal("Monday", forecast.Day);
                Assert.Equal(18, forecast.TemperatureC);
            },
            forecast =>
            {
                Assert.Equal("Tuesday", forecast.Day);
                Assert.Equal(21, forecast.TemperatureC);
            });
        Assert.Equal(72, payload.Measurements["Humidity"]);
    }

    [Test]
    public void GenericCollectionsAndStringDictionariesBaseline()
    {
        const string listJson = "[{\"Name\":\"first\",\"Value\":1},{\"Name\":\"second\",\"Value\":2}]";
        const string dictionaryJson = "{\"first\":{\"Name\":\"one\",\"Value\":1},\"empty\":null}";

        var systemTextJsonList = System.Text.Json.JsonSerializer.Deserialize<List<JsonCollectionItem>>(listJson);
        Assert.Equal(listJson, System.Text.Json.JsonSerializer.Serialize(systemTextJsonList));
        var systemTextJsonInterface = System.Text.Json.JsonSerializer.Deserialize<IList<int>>("[1,2,3]");
        Assert.IsType<List<int>>(systemTextJsonInterface);
        Assert.Equal("[1,2,3]", System.Text.Json.JsonSerializer.Serialize(systemTextJsonInterface));
        var systemTextJsonDictionary =
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonCollectionItem?>>(dictionaryJson);
        Assert.Equal(dictionaryJson, System.Text.Json.JsonSerializer.Serialize(systemTextJsonDictionary));

        var newtonsoftList = JsonConvert.DeserializeObject<List<JsonCollectionItem>>(listJson);
        Assert.Equal(listJson, JsonConvert.SerializeObject(newtonsoftList));
        var newtonsoftInterface = JsonConvert.DeserializeObject<IList<int>>("[1,2,3]");
        Assert.IsType<List<int>>(newtonsoftInterface);
        Assert.Equal("[1,2,3]", JsonConvert.SerializeObject(newtonsoftInterface));
        var newtonsoftDictionary =
            JsonConvert.DeserializeObject<Dictionary<string, JsonCollectionItem?>>(dictionaryJson);
        Assert.Equal(dictionaryJson, JsonConvert.SerializeObject(newtonsoftDictionary));
    }

    [Test]
    public void ExplicitValueTypeConstructorsBaseline()
    {
        SystemTextJsonValueWithDefaultConstructor.Calls = 0;
        var systemTextDefault = default(SystemTextJsonValueWithDefaultConstructor);
        var systemTextNew = new SystemTextJsonValueWithDefaultConstructor();
        var systemTextRead = System.Text.Json.JsonSerializer.Deserialize<
            SystemTextJsonValueWithDefaultConstructor>("{}");
        var systemTextParameterized = System.Text.Json.JsonSerializer.Deserialize<
            SystemTextJsonParameterizedValue>("{}");
        var systemTextOrdinary = System.Text.Json.JsonSerializer.Deserialize<
            SystemTextJsonOrdinaryParameterizedValue>("{\"Value\":23}");

        Assert.Equal(0, systemTextDefault.Marker);
        Assert.Equal(31, systemTextNew.Marker);
        Assert.Equal(31, systemTextRead.Marker);
        Assert.Equal(2, SystemTextJsonValueWithDefaultConstructor.Calls);
        Assert.Equal(17, systemTextParameterized.Value);
        Assert.Equal(0, systemTextOrdinary.Value);
        Assert.Equal("{\"Marker\":31}", System.Text.Json.JsonSerializer.Serialize(systemTextRead));

        NewtonsoftValueWithDefaultConstructor.Calls = 0;
        var newtonsoftDefault = default(NewtonsoftValueWithDefaultConstructor);
        var newtonsoftNew = new NewtonsoftValueWithDefaultConstructor();
        var newtonsoftRead = JsonConvert.DeserializeObject<
            NewtonsoftValueWithDefaultConstructor>("{}");
        var newtonsoftParameterized = JsonConvert.DeserializeObject<
            NewtonsoftParameterizedValue>("{}");
        var newtonsoftOrdinary = JsonConvert.DeserializeObject<
            NewtonsoftOrdinaryParameterizedValue>("{\"Value\":29}");

        Assert.Equal(0, newtonsoftDefault.Marker);
        Assert.Equal(41, newtonsoftNew.Marker);
        Assert.Equal(0, newtonsoftRead.Marker);
        Assert.Equal(1, NewtonsoftValueWithDefaultConstructor.Calls);
        Assert.Equal(0, newtonsoftParameterized.Value);
        Assert.Equal(29, newtonsoftOrdinary.Value);
        Assert.Equal("{\"Marker\":0}", JsonConvert.SerializeObject(newtonsoftRead));

        Assert.Throws<InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonThrowingValue>("{}"));
        Assert.Throws<InvalidOperationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftThrowingValue>("{}"));
    }

    [Test]
    public void SystemTextJsonDictionaryKeyPolicyBaseline()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        };
        var payload = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(
            "{\"URLValue\":1,\"SHA512Managed\":2}",
            options);

        Assert.NotNull(payload);
        Assert.Equal(1, payload["URLValue"]);
        Assert.Equal(2, payload["SHA512Managed"]);
        Assert.Equal(
            "{\"url_value\":1,\"sha512_managed\":2}",
            System.Text.Json.JsonSerializer.Serialize(payload, options));
    }

    [Test]
    public void DateAndTimeTypesBaseline()
    {
        const System.Reflection.BindingFlags instanceFields =
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        Assert.Equal(typeof(ulong), typeof(DateTime).GetField("_dateData", instanceFields)?.FieldType);
        Assert.Equal(typeof(DateTime), typeof(DateTimeOffset).GetField("_dateTime", instanceFields)?.FieldType);
        Assert.Equal(typeof(short), typeof(DateTimeOffset).GetField("_offsetMinutes", instanceFields)?.FieldType);
        Assert.Equal(typeof(int), typeof(DateOnly).GetField("_dayNumber", instanceFields)?.FieldType);
        Assert.Equal(typeof(long), typeof(TimeOnly).GetField("_ticks", instanceFields)?.FieldType);
        Assert.Equal(typeof(long), typeof(TimeSpan).GetField("_ticks", instanceFields)?.FieldType);

        var dateTime = new DateTime(2024, 2, 29, 12, 34, 56, 789, DateTimeKind.Utc)
            .AddTicks(1234);
        var dateTimeOffset = new DateTimeOffset(2024, 2, 29, 12, 34, 56, 789, TimeSpan.FromHours(5.5))
            .AddTicks(1234);
        var dateOnly = new DateOnly(2024, 2, 29);
        var timeOnly = new TimeOnly(12, 34, 56, 789).Add(TimeSpan.FromTicks(1234));
        var timeSpan = new TimeSpan(1, 2, 3, 4, 5).Add(TimeSpan.FromTicks(60));

        const string dateTimeJson = "\"2024-02-29T12:34:56.7891234Z\"";
        const string dateTimeOffsetJson = "\"2024-02-29T12:34:56.7891234+05:30\"";
        const string dateOnlyJson = "\"2024-02-29\"";
        const string timeOnlyJson = "\"12:34:56.7891234\"";
        const string timeSpanJson = "\"1.02:03:04.0050060\"";

        Assert.Equal(dateTimeJson, System.Text.Json.JsonSerializer.Serialize(dateTime));
        Assert.Equal(dateTimeOffsetJson, System.Text.Json.JsonSerializer.Serialize(dateTimeOffset));
        Assert.Equal(dateOnlyJson, System.Text.Json.JsonSerializer.Serialize(dateOnly));
        Assert.Equal(timeOnlyJson, System.Text.Json.JsonSerializer.Serialize(timeOnly));
        Assert.Equal(timeSpanJson, System.Text.Json.JsonSerializer.Serialize(timeSpan));
        Assert.Equal(dateTime, System.Text.Json.JsonSerializer.Deserialize<DateTime>(dateTimeJson));
        Assert.Equal(dateTimeOffset, System.Text.Json.JsonSerializer.Deserialize<DateTimeOffset>(dateTimeOffsetJson));
        Assert.Equal(dateOnly, System.Text.Json.JsonSerializer.Deserialize<DateOnly>(dateOnlyJson));
        Assert.Equal(timeOnly, System.Text.Json.JsonSerializer.Deserialize<TimeOnly>(timeOnlyJson));
        Assert.Equal(timeSpan, System.Text.Json.JsonSerializer.Deserialize<TimeSpan>(timeSpanJson));

        Assert.Equal(dateTimeJson, JsonConvert.SerializeObject(dateTime));
        Assert.Equal(dateTimeOffsetJson, JsonConvert.SerializeObject(dateTimeOffset));
        Assert.Equal(dateOnlyJson, JsonConvert.SerializeObject(dateOnly));
        Assert.Equal(timeOnlyJson, JsonConvert.SerializeObject(timeOnly));
        Assert.Equal(timeSpanJson, JsonConvert.SerializeObject(timeSpan));
        Assert.Equal(dateTime, JsonConvert.DeserializeObject<DateTime>(dateTimeJson));
        Assert.Equal(dateTimeOffset, JsonConvert.DeserializeObject<DateTimeOffset>(dateTimeOffsetJson));
        Assert.Equal(dateOnly, JsonConvert.DeserializeObject<DateOnly>(dateOnlyJson));
        Assert.Equal(timeOnly, JsonConvert.DeserializeObject<TimeOnly>(timeOnlyJson));
        Assert.Equal(timeSpan, JsonConvert.DeserializeObject<TimeSpan>(timeSpanJson));
        Assert.Equal(dateTimeOffsetJson, JsonConvert.SerializeObject(dateTimeOffset, settings: null));
        Assert.Equal(
            dateTimeOffset,
            JsonConvert.DeserializeObject<DateTimeOffset>(dateTimeOffsetJson, settings: null));

        Assert.Equal(
            "\"2024-02-29T12:34:56Z\"",
            System.Text.Json.JsonSerializer.Serialize(new DateTime(2024, 2, 29, 12, 34, 56, DateTimeKind.Utc)));
        Assert.Equal("\"00:00:00\"", System.Text.Json.JsonSerializer.Serialize(TimeOnly.MinValue));
        Assert.Equal("\"00:00:00\"", System.Text.Json.JsonSerializer.Serialize(TimeSpan.Zero));
        Assert.Equal(
            "\"-00:00:00.5000001\"",
            System.Text.Json.JsonSerializer.Serialize(TimeSpan.FromTicks(-5000001)));
        Assert.Equal("\"00:00:00\"", JsonConvert.SerializeObject(TimeOnly.MinValue));
        Assert.Equal("\"00:00:00\"", JsonConvert.SerializeObject(TimeSpan.Zero));
        Assert.Equal("\"-00:00:00.5000001\"", JsonConvert.SerializeObject(TimeSpan.FromTicks(-5000001)));

        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<DateOnly>("\"2023-02-29\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<TimeOnly>("\"24:00:00\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<TimeSpan>("\"1.24:00:00\""));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<DateOnly>("\"2023-02-29\""));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<TimeOnly>("\"24:00:00\""));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<TimeSpan>("\"1.24:00:00\""));
    }

    [Test]
    public void SystemTextJsonDefaultPocoContractBaseline()
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(
            SystemTextJsonPayload);

        Assert.NotNull(payload);
        Assert.Equal("set:clear", payload.Summary);
        Assert.Equal(21, payload.TemperatureC);
        Assert.True(payload.Active);
        Assert.Equal("constructor-default", payload.Ignored);
        Assert.Equal("line\n<é+&'>", payload.Text);
        Assert.Equal(9007199254740993L, payload.Large);
        Assert.Null(payload.Optional);
        Assert.Equal("included", payload.Included);
        Assert.Equal("init", payload.InitOnly);
        Assert.Equal("never", payload.NeverIgnored);
        Assert.Equal("read-only", payload.ReadOnly);
        Assert.Equal(
            "{\"Summary\":\"set:clear\",\"temperatureC\":21,\"Active\":true,"
                + "\"Text\":\"line\\n\\u003C\\u00E9\\u002B\\u0026\\u0027\\u003E\","
                + "\"Large\":9007199254740993,\"Optional\":null,\"Included\":\"included\","
                + "\"InitOnly\":\"init\",\"NeverIgnored\":\"never\",\"ReadOnly\":\"read-only\"}",
            System.Text.Json.JsonSerializer.Serialize(payload));
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>("null"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(string.Empty));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>("{"));
        var systemTextJsonOverflow = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(
                "{\"temperatureC\":2147483648}"));
        Assert.Equal("$.temperatureC", systemTextJsonOverflow.Path);
    }

    [Test]
    public void NewtonsoftDefaultPocoContractBaseline()
    {
        var payload = JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(NewtonsoftPayload);

        Assert.NotNull(payload);
        Assert.Equal("set:clear", payload.Summary);
        Assert.Equal(21, payload.TemperatureC);
        Assert.True(payload.Active);
        Assert.Equal("constructor-default", payload.Ignored);
        Assert.Equal(7, payload.PublicField);
        Assert.Equal("line\n<é+&'>", payload.Text);
        Assert.Equal(9007199254740993L, payload.Large);
        Assert.Null(payload.Optional);
        Assert.Equal(
            "{\"PublicField\":7,\"Summary\":\"set:clear\",\"temperature_c\":21,\"Active\":true,"
                + "\"Text\":\"line\\n<é+&'>\",\"Large\":9007199254740993,\"Optional\":null,"
                + "\"Small\":0}",
            JsonConvert.SerializeObject(payload));
        var settingsPayload = JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
            NewtonsoftPayload,
            settings: null);
        Assert.Equal(JsonConvert.SerializeObject(payload), JsonConvert.SerializeObject(settingsPayload, settings: null));
        Assert.Null(JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>("null"));
        Assert.Null(JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(" \r\n\t "));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>("{"));
        var newtonsoftOverflow = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
                "{\"Small\":2147483648}"));
        Assert.Equal("Small", newtonsoftOverflow.Path);
    }

    [Test]
    public void InheritedMemberOrderingBaseline()
    {
        var systemTextJson = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDerivedPayload>(
            "{\"DerivedProperty\":\"derived-input\",\"BaseProperty\":\"base-input\"}");

        Assert.NotNull(systemTextJson);
        Assert.Equal("derived-input", systemTextJson.DerivedProperty);
        Assert.Equal("base-input", systemTextJson.BaseProperty);
        Assert.Equal(
            "{\"DerivedProperty\":\"derived-input\",\"BaseProperty\":\"base-input\"}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJson));

        var newtonsoft = JsonConvert.DeserializeObject<NewtonsoftDerivedPayload>(
            "{\"derivedfield\":2,\"basefield\":1,\"derivedproperty\":\"derived-input\","
                + "\"baseproperty\":\"base-input\"}");

        Assert.NotNull(newtonsoft);
        Assert.Equal(2, newtonsoft.DerivedField);
        Assert.Equal(1, newtonsoft.BaseField);
        Assert.Equal("derived-input", newtonsoft.DerivedProperty);
        Assert.Equal("base-input", newtonsoft.BaseProperty);
        Assert.Equal(
            "{\"DerivedField\":2,\"BaseField\":1,\"DerivedProperty\":\"derived-input\","
                + "\"BaseProperty\":\"base-input\"}",
            JsonConvert.SerializeObject(newtonsoft));
    }

    [Test]
    public void ExplicitMemberOrderingBaseline()
    {
        var systemTextJson =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOrderedPayload>(
                "{\"Late\":60,\"AfterDefaults\":50,\"DefaultSecond\":40,"
                    + "\"Early\":30,\"DefaultFirst\":20,\"IncludedField\":10}");
        Assert.NotNull(systemTextJson);
        Assert.Equal(
            "{\"IncludedField\":10,\"Early\":30,\"DefaultFirst\":20,"
                + "\"DefaultSecond\":40,\"AfterDefaults\":50,\"Late\":60}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJson));

        var newtonsoft = JsonConvert.DeserializeObject<NewtonsoftOrderedPayload>(
            "{\"LateProperty\":60,\"AfterDefaults\":50,\"DefaultProperty\":40,"
                + "\"EarlyProperty\":30,\"DefaultField\":20,\"EarlyField\":10}");
        Assert.NotNull(newtonsoft);
        const string expectedNewtonsoft =
            "{\"EarlyField\":10,\"EarlyProperty\":30,\"DefaultField\":20,"
                + "\"DefaultProperty\":40,\"AfterDefaults\":50,\"LateProperty\":60}";
        Assert.Equal(expectedNewtonsoft, JsonConvert.SerializeObject(newtonsoft));
        Assert.Equal(
            expectedNewtonsoft,
            JsonConvert.SerializeObject(newtonsoft, settings: null));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(new NewtonsoftRequiredPayload
            {
                Always = null,
                AllowNull = null,
                DisallowNull = "optional",
            }));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(new NewtonsoftRequiredPayload
            {
                Always = "set",
                AllowNull = null,
                DisallowNull = null,
            }, settings: null));
    }

    [Test]
    public void RequiredMemberBaseline()
    {
        var systemTextJson =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonRequiredPayload>(
                "{\"required_text\":null}");
        Assert.NotNull(systemTextJson);
        Assert.Null(systemTextJson.RequiredText);
        Assert.Equal(
            "{\"required_text\":null}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJson));
        var systemTextJsonMissing = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonRequiredPayload>("{}"));
        Assert.Equal("$", systemTextJsonMissing.Path);

        var keywordRequired =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonKeywordRequiredPayload>(
                "{\"KeywordRequired\":null}");
        Assert.NotNull(keywordRequired);
        Assert.Null(keywordRequired.KeywordRequired);
        Assert.Equal(
            "$",
            Assert.Throws<System.Text.Json.JsonException>(() =>
                System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonKeywordRequiredPayload>(
                    "{}")).Path);
        Assert.Null(
            JsonConvert.DeserializeObject<SystemTextJsonKeywordRequiredPayload>("{}")
                ?.KeywordRequired);

        var newtonsoft = JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}");
        Assert.NotNull(newtonsoft);
        Assert.Equal("set", newtonsoft.Always);
        Assert.Null(newtonsoft.AllowNull);
        Assert.Equal("optional", newtonsoft.DisallowNull);
        Assert.Null(JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null}")?.DisallowNull);
        const string expectedNewtonsoft =
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}";
        Assert.Equal(expectedNewtonsoft, JsonConvert.SerializeObject(newtonsoft));
        Assert.Equal(
            expectedNewtonsoft,
            JsonConvert.SerializeObject(newtonsoft, settings: null));
        Assert.NotNull(JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
            "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":\"optional\"}",
            settings: null));

        var missingAlways = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"AllowNull\":null}"));
        Assert.Equal(string.Empty, missingAlways.Path);
        var nullAlways = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"Always\":null,\"AllowNull\":null}"));
        Assert.Equal(string.Empty, nullAlways.Path);
        var missingAllowNull = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"Always\":\"set\"}"));
        Assert.Equal(string.Empty, missingAllowNull.Path);
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"Always\":\"set\"}",
                settings: null));
        var nullDisallowed = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":null}"));
        Assert.Equal(string.Empty, nullDisallowed.Path);
    }

    [Test]
    public void NewtonsoftPerMemberNullValueHandlingBaseline()
    {
        const string json = "{\"Ignored\":null,\"Included\":null}";
        var payload = JsonConvert.DeserializeObject<NewtonsoftNullValueHandlingPayload>(json);

        Assert.NotNull(payload);
        Assert.Equal("ignored-default", payload.Ignored);
        Assert.Equal(0, payload.IgnoredSetterCalls);
        Assert.Null(payload.Included);
        Assert.Equal(1, payload.IncludedSetterCalls);

        var settingsPayload = JsonConvert.DeserializeObject<NewtonsoftNullValueHandlingPayload>(
            json,
            settings: null);
        Assert.NotNull(settingsPayload);
        Assert.Equal("ignored-default", settingsPayload.Ignored);
        Assert.Equal(0, settingsPayload.IgnoredSetterCalls);
        Assert.Null(settingsPayload.Included);
        Assert.Equal(1, settingsPayload.IncludedSetterCalls);

        payload.Ignored = null;
        Assert.Equal("{\"Included\":null}", JsonConvert.SerializeObject(payload));
        Assert.Equal(
            "{\"Included\":null}",
            JsonConvert.SerializeObject(payload, settings: null));
    }

    [Test]
    public void NewtonsoftPerMemberDefaultValueHandlingBaseline()
    {
        const string json =
            "{\"Ignored\":0,\"Included\":0,\"AttributedIgnoredInteger\":7,"
            + "\"AttributedAssignedString\":\"custom\",\"AttributedIgnoredBoolean\":true,"
            + "\"AttributedIgnoredCharacter\":\"Z\","
            + "\"AttributedIgnoredInt64\":9007199254740993,"
            + "\"AttributedIgnoredUInt64\":9007199254740993,"
            + "\"AttributedIgnoredSingle\":1.25,\"AttributedIgnoredDouble\":1.25}";
        var payload = JsonConvert.DeserializeObject<NewtonsoftDefaultValueHandlingPayload>(json);

        Assert.NotNull(payload);
        Assert.Equal(5, payload.Ignored);
        Assert.Equal(0, payload.IgnoredSetterCalls);
        Assert.Equal(0, payload.Included);
        Assert.Equal(1, payload.IncludedSetterCalls);
        Assert.Equal(0, payload.Populated);
        Assert.Equal(1, payload.PopulatedSetterCalls);
        Assert.Equal(0, payload.IgnoredAndPopulated);
        Assert.Equal(1, payload.IgnoredAndPopulatedSetterCalls);
        Assert.Equal(3, payload.AttributedIgnoredInteger);
        Assert.Equal(7, payload.AttributedPopulatedInteger);
        Assert.Equal("custom", payload.AttributedAssignedString);
        Assert.False(payload.AttributedIgnoredBoolean);
        Assert.Equal('A', payload.AttributedIgnoredCharacter);
        Assert.Equal(4, payload.AttributedIgnoredInt64);
        Assert.Equal(4UL, payload.AttributedIgnoredUInt64);
        Assert.Equal(2.5f, payload.AttributedIgnoredSingle);
        Assert.Equal(2.5d, payload.AttributedIgnoredDouble);
        payload.Ignored = 0;
        payload.AttributedIgnoredInteger = 7;
        payload.AttributedPopulatedInteger = 7;
        payload.AttributedAssignedString = "custom";
        payload.AttributedIgnoredBoolean = true;
        payload.AttributedIgnoredCharacter = 'Z';
        payload.AttributedIgnoredInt64 = 9007199254740993L;
        payload.AttributedIgnoredUInt64 = 9007199254740993UL;
        payload.AttributedIgnoredSingle = 1.25f;
        payload.AttributedIgnoredDouble = 1.25d;
        Assert.Equal(
            "{\"Included\":0,\"Populated\":0,\"AttributedPopulatedInteger\":7}",
            JsonConvert.SerializeObject(payload));

        var settingsPayload =
            JsonConvert.DeserializeObject<NewtonsoftDefaultValueHandlingPayload>(
                json,
                settings: null);
        Assert.NotNull(settingsPayload);
        Assert.Equal(5, settingsPayload.Ignored);
        Assert.Equal(0, settingsPayload.IgnoredSetterCalls);
        Assert.Equal(0, settingsPayload.Included);
        Assert.Equal(1, settingsPayload.IncludedSetterCalls);
        Assert.Equal(0, settingsPayload.Populated);
        Assert.Equal(1, settingsPayload.PopulatedSetterCalls);
        Assert.Equal(0, settingsPayload.IgnoredAndPopulated);
        Assert.Equal(1, settingsPayload.IgnoredAndPopulatedSetterCalls);
        Assert.Equal(3, settingsPayload.AttributedIgnoredInteger);
        Assert.Equal(7, settingsPayload.AttributedPopulatedInteger);
        Assert.Equal("custom", settingsPayload.AttributedAssignedString);
        Assert.False(settingsPayload.AttributedIgnoredBoolean);
        Assert.Equal('A', settingsPayload.AttributedIgnoredCharacter);
        Assert.Equal(4, settingsPayload.AttributedIgnoredInt64);
        Assert.Equal(4UL, settingsPayload.AttributedIgnoredUInt64);
        Assert.Equal(2.5f, settingsPayload.AttributedIgnoredSingle);
        Assert.Equal(2.5d, settingsPayload.AttributedIgnoredDouble);
        settingsPayload.Ignored = 0;
        settingsPayload.AttributedIgnoredInteger = 7;
        settingsPayload.AttributedPopulatedInteger = 7;
        settingsPayload.AttributedAssignedString = "custom";
        settingsPayload.AttributedIgnoredBoolean = true;
        settingsPayload.AttributedIgnoredCharacter = 'Z';
        settingsPayload.AttributedIgnoredInt64 = 9007199254740993L;
        settingsPayload.AttributedIgnoredUInt64 = 9007199254740993UL;
        settingsPayload.AttributedIgnoredSingle = 1.25f;
        settingsPayload.AttributedIgnoredDouble = 1.25d;
        Assert.Equal(
            "{\"Included\":0,\"Populated\":0,\"AttributedPopulatedInteger\":7}",
            JsonConvert.SerializeObject(settingsPayload, settings: null));
    }

    [Test]
    public void NewtonsoftOptInMemberSerializationBaseline()
    {
        const string json = "{\"included_property\":\"input\",\"ExcludedProperty\":\"incoming\","
            + "\"included_field\":7,\"ExcludedField\":9}";

        var payload = JsonConvert.DeserializeObject<NewtonsoftOptInPayload>(json);

        Assert.NotNull(payload);
        Assert.Equal("input", payload.IncludedProperty);
        Assert.Equal("excluded-default", payload.ExcludedProperty);
        Assert.Equal(7, payload.IncludedFieldValue);
        Assert.Equal(2, payload.ExcludedField);
        Assert.Equal(
            "{\"included_field\":7,\"included_property\":\"input\"}",
            JsonConvert.SerializeObject(payload));
        Assert.Equal(
            JsonConvert.SerializeObject(payload),
            JsonConvert.SerializeObject(payload, settings: null));
    }

    [Test]
    public void EnumNumericContractBaseline()
    {
        const string json = "{\"SByte\":-128,\"Byte\":255,\"Int16\":-32768,\"UInt16\":65535,"
            + "\"Int32\":-2147483648,\"UInt32\":4294967295,"
            + "\"Int64\":-9007199254740993,\"UInt64\":9007199254740993,"
            + "\"Flags\":3,\"Undefined\":123,\"Values\":[-9007199254740993,7]}";

        var systemTextJson = System.Text.Json.JsonSerializer.Deserialize<EnumPayload>(json);
        var newtonsoft = JsonConvert.DeserializeObject<EnumPayload>(json);

        AssertEnumPayload(systemTextJson);
        AssertEnumPayload(newtonsoft);
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(systemTextJson));
        Assert.Equal(json, JsonConvert.SerializeObject(newtonsoft));
        Assert.Equal(
            "-9007199254740993",
            System.Text.Json.JsonSerializer.Serialize(JsonInt64Enum.NegativeLarge));
        Assert.Equal(
            "18446744073709551615",
            JsonConvert.SerializeObject(JsonUInt64Enum.Maximum));
        Assert.Equal(
            JsonUInt64Enum.Maximum,
            System.Text.Json.JsonSerializer.Deserialize<JsonUInt64Enum>(
                "18446744073709551615"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<JsonUInt64Enum>("18446744073709551615"));
        Assert.Equal(
            JsonInt64Enum.NegativeLarge,
            System.Text.Json.JsonSerializer.Deserialize<JsonInt64Enum>("-9007199254740993"));
        Assert.Equal(
            JsonFlagsEnum.Read | JsonFlagsEnum.Write,
            JsonConvert.DeserializeObject<JsonFlagsEnum>("\"read, WRITE\""));
        Assert.Equal(
            JsonInt64Enum.NegativeLarge,
            JsonConvert.DeserializeObject<JsonInt64Enum>("\"NegativeLargeAlias\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonFlagsEnum>("\"Read\""));
    }

    [Test]
    public void BuiltInStringEnumConverterBaseline()
    {
        var systemTextJsonOptions = new System.Text.Json.JsonSerializerOptions();
        systemTextJsonOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.CamelCase,
                allowIntegerValues: false));
        Assert.Equal(
            "\"read, write\"",
            System.Text.Json.JsonSerializer.Serialize(
                JsonFlagsEnum.Read | JsonFlagsEnum.Write,
                systemTextJsonOptions));
        Assert.Equal(
            JsonFlagsEnum.Read | JsonFlagsEnum.Write,
            System.Text.Json.JsonSerializer.Deserialize<JsonFlagsEnum>(
                "\"READ, write\"",
                systemTextJsonOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Serialize(
                (JsonFlagsEnum)8,
                systemTextJsonOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonFlagsEnum>(
                "8",
                systemTextJsonOptions));

        var genericSystemTextJsonOptions = new System.Text.Json.JsonSerializerOptions();
        genericSystemTextJsonOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter<JsonNamedEnum>(
                System.Text.Json.JsonNamingPolicy.KebabCaseUpper,
                allowIntegerValues: false));
        Assert.Equal(
            "\"FIRST-VALUE\"",
            System.Text.Json.JsonSerializer.Serialize(
                JsonNamedEnum.FirstValue,
                genericSystemTextJsonOptions));
        Assert.Equal(
            JsonNamedEnum.FirstValue,
            System.Text.Json.JsonSerializer.Deserialize<JsonNamedEnum>(
                "\"FIRST-VALUE\"",
                genericSystemTextJsonOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonNamedEnum>(
                "\"first-value\"",
                genericSystemTextJsonOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<JsonNamedEnum>(
                "1",
                genericSystemTextJsonOptions));
        Assert.Equal(
            "1",
            System.Text.Json.JsonSerializer.Serialize(
                JsonFlagsEnum.Read,
                genericSystemTextJsonOptions));

        var newtonsoftSettings = new JsonSerializerSettings();
        newtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter(
                new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy(),
                allowIntegerValues: false));
        Assert.Equal(
            "\"read, write\"",
            JsonConvert.SerializeObject(
                JsonFlagsEnum.Read | JsonFlagsEnum.Write,
                newtonsoftSettings));
        Assert.Equal(
            JsonFlagsEnum.Read | JsonFlagsEnum.Write,
            JsonConvert.DeserializeObject<JsonFlagsEnum>(
                "\"READ, write\"",
                newtonsoftSettings));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject((JsonFlagsEnum)8, newtonsoftSettings));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<JsonFlagsEnum>("8", newtonsoftSettings));

        var namedNewtonsoftSettings = new JsonSerializerSettings();
        namedNewtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter(
                new Newtonsoft.Json.Serialization.KebabCaseNamingStrategy(),
                allowIntegerValues: false));
        Assert.Equal(
            "\"first-value\"",
            JsonConvert.SerializeObject(
                JsonNamedEnum.FirstValue,
                namedNewtonsoftSettings));
        Assert.Equal(
            JsonNamedEnum.FirstValue,
            JsonConvert.DeserializeObject<JsonNamedEnum>(
                "\"FIRST-VALUE\"",
                namedNewtonsoftSettings));

        var defaultNewtonsoftSettings = new JsonSerializerSettings();
        defaultNewtonsoftSettings.Converters.Add(
            new Newtonsoft.Json.Converters.StringEnumConverter());
        Assert.Equal(
            "8",
            JsonConvert.SerializeObject(
                (JsonFlagsEnum)8,
                defaultNewtonsoftSettings));
        Assert.Equal(
            (JsonFlagsEnum)8,
            JsonConvert.DeserializeObject<JsonFlagsEnum>(
                "8",
                defaultNewtonsoftSettings));
    }

    private static void AssertEnumPayload(EnumPayload? payload)
    {
        Assert.NotNull(payload);
        Assert.Equal(JsonSByteEnum.Minimum, payload.SByte);
        Assert.Equal(JsonByteEnum.Maximum, payload.Byte);
        Assert.Equal(JsonInt16Enum.Minimum, payload.Int16);
        Assert.Equal(JsonUInt16Enum.Maximum, payload.UInt16);
        Assert.Equal(JsonInt32Enum.Minimum, payload.Int32);
        Assert.Equal(JsonUInt32Enum.Maximum, payload.UInt32);
        Assert.Equal(JsonInt64Enum.NegativeLarge, payload.Int64);
        Assert.Equal(JsonUInt64Enum.Large, payload.UInt64);
        Assert.Equal(JsonFlagsEnum.Read | JsonFlagsEnum.Write, payload.Flags);
        Assert.Equal((JsonInt32Enum)123, payload.Undefined);
        Assert.Equal([JsonInt64Enum.NegativeLarge, (JsonInt64Enum)7], payload.Values);
    }

    [Test]
    public void PrimitiveArrayBaseline()
    {
        var systemTextJsonIntegers = System.Text.Json.JsonSerializer.Deserialize<long[]>(
            "[9007199254740993,-9007199254740993]");
        var systemTextJsonStrings = System.Text.Json.JsonSerializer.Deserialize<string?[]>(
            "[\"line\\n\\u003C\\u00E9\",null]");

        Assert.Equal(new[] { 9007199254740993L, -9007199254740993L }, systemTextJsonIntegers);
        Assert.Equal(new string?[] { "line\n<é", null }, systemTextJsonStrings);
        Assert.Equal(
            "[9007199254740993,-9007199254740993]",
            System.Text.Json.JsonSerializer.Serialize(systemTextJsonIntegers));
        Assert.Equal(
            "[\"line\\n\\u003C\\u00E9\",null]",
            System.Text.Json.JsonSerializer.Serialize(systemTextJsonStrings));

        var newtonsoftIntegers = JsonConvert.DeserializeObject<long[]>(
            "[9007199254740993,-9007199254740993]");
        var newtonsoftStrings = JsonConvert.DeserializeObject<string?[]>(
            "[\"line\\n\\u003C\\u00E9\",null]");

        Assert.Equal(new[] { 9007199254740993L, -9007199254740993L }, newtonsoftIntegers);
        Assert.Equal(new string?[] { "line\n<é", null }, newtonsoftStrings);
        Assert.Equal(
            "[9007199254740993,-9007199254740993]",
            JsonConvert.SerializeObject(newtonsoftIntegers));
        Assert.Equal(
            "[\"line\\n<é\",null]",
            JsonConvert.SerializeObject(newtonsoftStrings));
    }

    [Test]
    public void ByteArrayBase64Baseline()
    {
        byte[] expected = [0, 1, 2, 254, 255];

        Assert.Equal(
            expected,
            System.Text.Json.JsonSerializer.Deserialize<byte[]>("\"AAEC/v8=\""));
        Assert.Equal(
            "\"AAEC/v8=\"",
            System.Text.Json.JsonSerializer.Serialize(expected));
        Assert.Equal(
            Array.Empty<byte>(),
            System.Text.Json.JsonSerializer.Deserialize<byte[]>("\"\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<byte[]>("\"not-base64\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<byte[]>("[0,1,2]"));

        Assert.Equal(
            expected,
            JsonConvert.DeserializeObject<byte[]>("\"AAEC/v8=\""));
        Assert.Equal("\"AAEC/v8=\"", JsonConvert.SerializeObject(expected));
        Assert.Equal(
            Array.Empty<byte>(),
            JsonConvert.DeserializeObject<byte[]>("\"\""));
        Assert.Throws<FormatException>(() =>
            JsonConvert.DeserializeObject<byte[]>("\"not-base64\""));
        Assert.Equal(
            new byte[] { 0, 1, 2 },
            JsonConvert.DeserializeObject<byte[]>("[0,1,2]"));
    }

    [Test]
    public void CharacterBaseline()
    {
        Assert.Equal('é', System.Text.Json.JsonSerializer.Deserialize<char>("\"é\""));
        Assert.Equal("\"\\u00E9\"", System.Text.Json.JsonSerializer.Serialize('é'));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<char>("\"\""));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<char>("\"ab\""));
        Assert.Equal(
            new[] { 'a', 'é' },
            System.Text.Json.JsonSerializer.Deserialize<char[]>("[\"a\",\"é\"]"));
        Assert.Equal(
            "[\"a\",\"\\u00E9\"]",
            System.Text.Json.JsonSerializer.Serialize(new[] { 'a', 'é' }));

        Assert.Equal('é', JsonConvert.DeserializeObject<char>("\"é\""));
        Assert.Equal("\"é\"", JsonConvert.SerializeObject('é'));
        Assert.Equal("\"é\"", JsonConvert.SerializeObject('é', settings: null));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<char>("\"\""));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<char>("\"ab\""));
        Assert.Equal(
            new[] { 'a', 'é' },
            JsonConvert.DeserializeObject<char[]>("[\"a\",\"é\"]"));
        Assert.Equal("[\"a\",\"é\"]", JsonConvert.SerializeObject(new[] { 'a', 'é' }));
    }

    [Test]
    public void CharacterNumericConversionBaseline()
    {
        var systemTextJsonRoot = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<char>("65"));
        Assert.Equal("$", systemTextJsonRoot.Path);
        var systemTextJsonMember = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<CharacterPayload>("{\"Value\":65}"));
        Assert.Equal("$.Value", systemTextJsonMember.Path);

        Assert.Equal('A', JsonConvert.DeserializeObject<char>("65"));
        Assert.Equal('A', JsonConvert.DeserializeObject<char>("65", settings: null));
        var newtonsoftPayload = JsonConvert.DeserializeObject<CharacterPayload>("{\"Value\":233}");
        Assert.NotNull(newtonsoftPayload);
        Assert.Equal('é', newtonsoftPayload.Value);
        Assert.Equal(
            new[] { 'A', 'é' },
            JsonConvert.DeserializeObject<char[]>("[65,233]"));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<char>("65536"));
        var newtonsoftMemberOverflow = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<CharacterPayload>("{\"Value\":65536}"));
        Assert.Equal("Value", newtonsoftMemberOverflow.Path);
    }

    [Test]
    public void PrimitiveRootSerializationBaseline()
    {
        Assert.Equal("true", System.Text.Json.JsonSerializer.Serialize(true));
        Assert.Equal("7", System.Text.Json.JsonSerializer.Serialize(7));
        Assert.Equal(
            "9007199254740993",
            System.Text.Json.JsonSerializer.Serialize(9007199254740993L));

        Assert.Equal("true", JsonConvert.SerializeObject(true));
        Assert.Equal("7", JsonConvert.SerializeObject(7));
        Assert.Equal("9007199254740993", JsonConvert.SerializeObject(9007199254740993L));
        Assert.Equal("true", JsonConvert.SerializeObject(true, settings: null));
        Assert.Equal("7", JsonConvert.SerializeObject(7, settings: null));
        Assert.Equal(
            "9007199254740993",
            JsonConvert.SerializeObject(9007199254740993L, settings: null));
    }

    [Test]
    public void NullPrimitiveRootBaseline()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int>("null"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<bool>("null"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<char>("null"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<double>("null"));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<int>("null"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<bool>("null"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<char>("null"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<double>("null"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<int>("null", settings: null));
    }

    [Test]
    public void WhitespacePrimitiveRootBaseline()
    {
        const string whitespace = " \r\n\t ";
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int>(whitespace));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<char>(whitespace));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<double>(whitespace));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<int>(whitespace));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<bool>(whitespace));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<char>(whitespace));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<float>(whitespace));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<double>(whitespace));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<double>(whitespace, settings: null));
        Assert.Null(JsonConvert.DeserializeObject<FloatingPointPayload>(whitespace));
    }

    [Test]
    public void FloatingPointBaseline()
    {
        var actual = string.Join(
            "|",
            System.Text.Json.JsonSerializer.Serialize(1.25d),
            System.Text.Json.JsonSerializer.Serialize(1d),
            System.Text.Json.JsonSerializer.Serialize(-0d),
            System.Text.Json.JsonSerializer.Serialize(1.25f),
            System.Text.Json.JsonSerializer.Serialize(1e20d),
            System.Text.Json.JsonSerializer.Serialize(1e21d),
            System.Text.Json.JsonSerializer.Serialize(1e-5d),
            System.Text.Json.JsonSerializer.Serialize(1e-6d),
            System.Text.Json.JsonSerializer.Serialize(1e-7d),
            System.Text.Json.JsonSerializer.Serialize(double.MaxValue),
            System.Text.Json.JsonSerializer.Serialize(double.Epsilon),
            System.Text.Json.JsonSerializer.Deserialize<double>("1.25") == 1.25d,
            CaptureExceptionType(() => System.Text.Json.JsonSerializer.Serialize(double.NaN)),
            CaptureExceptionType(() => System.Text.Json.JsonSerializer.Serialize(double.PositiveInfinity)),
            CaptureExceptionType(() =>
                System.Text.Json.JsonSerializer.Deserialize<double>("\"NaN\"")),
            double.IsPositiveInfinity(
                System.Text.Json.JsonSerializer.Deserialize<double>("1e400")),
            JsonConvert.SerializeObject(1.25d),
            JsonConvert.SerializeObject(1d),
            JsonConvert.SerializeObject(-0d),
            JsonConvert.SerializeObject(1.25f),
            JsonConvert.SerializeObject(1e20d),
            JsonConvert.SerializeObject(1e21d),
            JsonConvert.SerializeObject(1e-5d),
            JsonConvert.SerializeObject(1e-6d),
            JsonConvert.SerializeObject(1e-7d),
            JsonConvert.SerializeObject(double.MaxValue),
            JsonConvert.SerializeObject(double.Epsilon),
            JsonConvert.SerializeObject(double.NaN),
            JsonConvert.SerializeObject(double.PositiveInfinity),
            JsonConvert.SerializeObject(double.NegativeInfinity),
            JsonConvert.DeserializeObject<double>("1.25") == 1.25d,
            double.IsNaN(JsonConvert.DeserializeObject<double>("\"NaN\"")),
            double.IsNaN(JsonConvert.DeserializeObject<double>("NaN")),
            double.IsPositiveInfinity(JsonConvert.DeserializeObject<double>("1e400")));

        Assert.Equal(
            "1.25|1|-0|1.25|1E+20|1E+21|1E-05|1E-06|1E-07|1.7976931348623157E+308|5E-324"
                + "|True|ArgumentException|ArgumentException|JsonException|True"
                + "|1.25|1.0|-0.0|1.25|1E+20|1E+21|1E-05|1E-06|1E-07"
                + "|1.7976931348623157E+308|5E-324|\"NaN\"|\"Infinity\"|\"-Infinity\""
                + "|True|True|True|True",
            actual);

        Assert.Equal(
            new[] { 1.25d, -2.5d },
            System.Text.Json.JsonSerializer.Deserialize<double[]>("[1.25,-2.5]"));
        Assert.Equal(
            "[1.25,-2.5]",
            System.Text.Json.JsonSerializer.Serialize(new[] { 1.25d, -2.5d }));
        Assert.Equal(
            new[] { 1.25d, -2.5d },
            JsonConvert.DeserializeObject<double[]>("[1.25,-2.5]"));
        Assert.Equal("[1.25,-2.5]", JsonConvert.SerializeObject(new[] { 1.25d, -2.5d }));

        var systemTextJsonPayload =
            System.Text.Json.JsonSerializer.Deserialize<FloatingPointPayload>(
                "{\"DoubleValue\":1,\"SingleValue\":1.25}");
        Assert.NotNull(systemTextJsonPayload);
        Assert.Equal(1d, systemTextJsonPayload.DoubleValue);
        Assert.Equal(1.25f, systemTextJsonPayload.SingleValue);
        Assert.Equal(
            "{\"DoubleValue\":1,\"SingleValue\":1.25}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJsonPayload));

        var newtonsoftPayload = JsonConvert.DeserializeObject<FloatingPointPayload>(
            "{\"DoubleValue\":1,\"SingleValue\":1.25}");
        Assert.NotNull(newtonsoftPayload);
        Assert.Equal(1d, newtonsoftPayload.DoubleValue);
        Assert.Equal(1.25f, newtonsoftPayload.SingleValue);
        Assert.Equal(
            "{\"DoubleValue\":1.0,\"SingleValue\":1.25}",
            JsonConvert.SerializeObject(newtonsoftPayload));
    }

    [Test]
    public void NamedFloatingPointContainerBaseline()
    {
        var newtonsoftPayload = JsonConvert.DeserializeObject<FloatingPointPayload>(
            "{\"DoubleValue\":NaN,\"SingleValue\":Infinity}");
        Assert.NotNull(newtonsoftPayload);
        Assert.True(double.IsNaN(newtonsoftPayload.DoubleValue));
        Assert.True(float.IsPositiveInfinity(newtonsoftPayload.SingleValue));
        Assert.Equal(
            "{\"DoubleValue\":\"NaN\",\"SingleValue\":\"Infinity\"}",
            JsonConvert.SerializeObject(newtonsoftPayload));

        var newtonsoftValues = JsonConvert.DeserializeObject<double[]>(
            "[NaN,Infinity,-Infinity]");
        Assert.NotNull(newtonsoftValues);
        Assert.True(double.IsNaN(newtonsoftValues[0]));
        Assert.True(double.IsPositiveInfinity(newtonsoftValues[1]));
        Assert.True(double.IsNegativeInfinity(newtonsoftValues[2]));
        Assert.Equal(
            "[\"NaN\",\"Infinity\",\"-Infinity\"]",
            JsonConvert.SerializeObject(newtonsoftValues));

        var systemTextJsonException = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<FloatingPointPayload>(
                "{\"DoubleValue\":NaN}"));
        Assert.Equal("$.DoubleValue", systemTextJsonException.Path);
    }

    [Test]
    public void SystemTextJsonMemberNumberHandlingBaseline()
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
            "{\"QuotedInteger\":\"9007199254740993\",\"QuotedDouble\":\"1.25\","
                + "\"ReadQuotedWriteNumber\":\"7\",\"NamedDouble\":\"NaN\"}");

        Assert.NotNull(payload);
        Assert.Equal(9007199254740993L, payload.QuotedInteger);
        Assert.Equal(1.25d, payload.QuotedDouble);
        Assert.Equal(7, payload.ReadQuotedWriteNumber);
        Assert.True(double.IsNaN(payload.NamedDouble));
        Assert.Equal(
            "{\"QuotedInteger\":\"9007199254740993\",\"QuotedDouble\":\"1.25\","
                + "\"ReadQuotedWriteNumber\":7,\"NamedDouble\":\"NaN\"}",
            System.Text.Json.JsonSerializer.Serialize(payload));

        var namedThroughStringHandling =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
                "{\"QuotedDouble\":\"NaN\"}");
        Assert.NotNull(namedThroughStringHandling);
        Assert.True(double.IsNaN(namedThroughStringHandling.QuotedDouble));

        var invalidQuotedNumber = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonNumberHandlingPayload>(
                "{\"QuotedDouble\":\"not-a-number\"}"));
        Assert.Equal("$.QuotedDouble", invalidQuotedNumber.Path);
    }

    [Test]
    public void SystemTextJsonTypeNumberHandlingBaseline()
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonTypeNumberHandlingPayload>(
            "{\"QuotedInteger\":\"9007199254740993\",\"NamedDouble\":\"NaN\",\"StrictInteger\":5}");

        Assert.NotNull(payload);
        Assert.Equal(9007199254740993L, payload.QuotedInteger);
        Assert.True(double.IsNaN(payload.NamedDouble));
        Assert.Equal(5, payload.StrictInteger);
        Assert.Equal(
            "{\"QuotedInteger\":\"9007199254740993\",\"NamedDouble\":\"NaN\",\"StrictInteger\":5}",
            System.Text.Json.JsonSerializer.Serialize(payload));

        var strictQuotedNumber = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonTypeNumberHandlingPayload>(
                "{\"StrictInteger\":\"5\"}"));
        Assert.Equal("$.StrictInteger", strictQuotedNumber.Path);
    }

    [Test]
    public void SystemTextJsonUnmappedMemberHandlingBaseline()
    {
        var skipped = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonSkipUnmappedPayload>(
            "{\"Known\":6,\"Unknown\":42}");
        var payload = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDisallowUnmappedPayload>(
            "{\"Known\":7}");

        Assert.NotNull(skipped);
        Assert.Equal(6, skipped.Known);
        Assert.NotNull(payload);
        Assert.Equal(7, payload.Known);
        Assert.Equal("{\"Known\":7}", System.Text.Json.JsonSerializer.Serialize(payload));

        var exception = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDisallowUnmappedPayload>(
                "{\"Known\":7,\"Unknown\":42}"));
        Assert.Equal("$.Unknown", exception.Path);
    }

    [Test]
    public void SystemTextJsonSerializerOptionsBaseline()
    {
        const string defaultJson =
            "{\"URLValue\":\"url\",\"SHA512Managed\":7,\"ExplicitName\":\"explicit\",\"Count\":3}";
        var defaultOptions = new System.Text.Json.JsonSerializerOptions();
        var defaultPayload =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
                defaultJson,
                defaultOptions);
        Assert.NotNull(defaultPayload);
        Assert.Equal("url", defaultPayload.URLValue);
        Assert.Equal(defaultJson, System.Text.Json.JsonSerializer.Serialize(defaultPayload, defaultOptions));
        Assert.True(defaultOptions.IsReadOnly);
        Assert.Throws<System.InvalidOperationException>(() => defaultOptions.WriteIndented = true);
        var explicitDefaultOptions = new System.Text.Json.JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            IncludeFields = false,
            WriteIndented = false,
        };
        Assert.Equal(
            defaultJson,
            System.Text.Json.JsonSerializer.Serialize(defaultPayload, explicitDefaultOptions));

        AssertOptionsNamingPolicy(
            System.Text.Json.JsonNamingPolicy.CamelCase,
            "{\"urlValue\":\"url\",\"shA512Managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}");
        AssertOptionsNamingPolicy(
            System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
            "{\"url_value\":\"url\",\"sha512_managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}");
        AssertOptionsNamingPolicy(
            System.Text.Json.JsonNamingPolicy.SnakeCaseUpper,
            "{\"URL_VALUE\":\"url\",\"SHA512_MANAGED\":7,\"ExplicitName\":\"explicit\",\"COUNT\":3}");
        AssertOptionsNamingPolicy(
            System.Text.Json.JsonNamingPolicy.KebabCaseLower,
            "{\"url-value\":\"url\",\"sha512-managed\":7,\"ExplicitName\":\"explicit\",\"count\":3}");
        AssertOptionsNamingPolicy(
            System.Text.Json.JsonNamingPolicy.KebabCaseUpper,
            "{\"URL-VALUE\":\"url\",\"SHA512-MANAGED\":7,\"ExplicitName\":\"explicit\",\"COUNT\":3}");

        var insensitiveOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        var insensitive =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
                "{\"urlvalue\":\"first\",\"URLVALUE\":\"last\",\"explicitname\":\"explicit\"}",
                insensitiveOptions);
        Assert.NotNull(insensitive);
        Assert.Equal("last", insensitive.URLValue);
        Assert.Equal("explicit", insensitive.ExplicitValue);
        var required =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsRequiredPayload>(
                "{\"value\":\"required\"}",
                insensitiveOptions);
        Assert.Equal("required", required?.Value);

        var constructorOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        };
        var constructed =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsConstructorPayload>(
                "{\"url_value\":\"constructed\"}",
                constructorOptions);
        Assert.Equal("constructed", constructed?.URLValue);

        var numberOptions = new System.Text.Json.JsonSerializerOptions
        {
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString,
        };
        var numbers = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsNumberPayload>(
            "{\"Count\":\"7\",\"StrictCount\":8}",
            numberOptions);
        Assert.NotNull(numbers);
        Assert.Equal(7, numbers.Count);
        Assert.Equal(8, numbers.StrictCount);
        Assert.Equal(
            "{\"Count\":\"7\",\"StrictCount\":8}",
            System.Text.Json.JsonSerializer.Serialize(numbers, numberOptions));

        var disallowOptions = new System.Text.Json.JsonSerializerOptions
        {
            UnmappedMemberHandling =
                System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        };
        var unknown = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
                "{\"Unknown\":1}",
                disallowOptions));
        Assert.Equal("$.Unknown", unknown.Path);
        var attributedSkip =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonSkipUnmappedPayload>(
                "{\"Known\":6,\"Unknown\":42}",
                disallowOptions);
        Assert.Equal(6, attributedSkip?.Known);

        var webOptions = new System.Text.Json.JsonSerializerOptions(
            System.Text.Json.JsonSerializerDefaults.Web);
        var web = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
            "{\"urlvalue\":\"web\",\"count\":\"9\"}",
            webOptions);
        Assert.NotNull(web);
        Assert.Equal("web", web.URLValue);
        Assert.Equal(9, web.Count);
        Assert.Equal(
            "{\"urlValue\":\"web\",\"shA512Managed\":0,\"ExplicitName\":null,\"count\":9}",
            System.Text.Json.JsonSerializer.Serialize(web, webOptions));

        var camelOptions = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        };
        Assert.Throws<System.InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsNamingCollisionPayload>(
                "{}",
                camelOptions));
        Assert.Throws<System.InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Serialize(
                new SystemTextJsonOptionsNamingCollisionPayload(),
                camelOptions));
        Assert.Throws<System.InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsCaseCollisionPayload>(
                "{}",
                insensitiveOptions));
    }

    [Test]
    public void SystemTextJsonReferenceHandlingBaseline()
    {
        var loop = new SystemTextJsonDepthOptionsPayload { Value = 1 };
        loop.Child = loop;
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Serialize(loop));

        var ignoreCyclesOptions = new System.Text.Json.JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        };
        Assert.Equal(
            "{\"Value\":1,\"Child\":null}",
            System.Text.Json.JsonSerializer.Serialize(loop, ignoreCyclesOptions));
        var read =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
                "{\"Value\":3,\"Child\":null}",
                ignoreCyclesOptions);
        Assert.NotNull(read);
        Assert.Equal(3, read.Value);
        Assert.Null(read.Child);

        var ignoreNullCyclesOptions = new System.Text.Json.JsonSerializerOptions
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        Assert.Equal(
            "{\"Value\":1}",
            System.Text.Json.JsonSerializer.Serialize(loop, ignoreNullCyclesOptions));

        var shared = new SystemTextJsonDepthOptionsPayload { Value = 2 };
        var pair = new SystemTextJsonReferencePairPayload
        {
            First = shared,
            Second = shared,
        };
        Assert.Equal(
            "{\"First\":{\"Value\":2,\"Child\":null},"
                + "\"Second\":{\"Value\":2,\"Child\":null}}",
            System.Text.Json.JsonSerializer.Serialize(pair, ignoreCyclesOptions));
    }

    [Test]
    public void SystemTextJsonWriteIndentedOptionBaseline()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        };
        var payload = new SystemTextJsonDepthOptionsPayload
        {
            Value = 1,
            Child = new SystemTextJsonDepthOptionsPayload
            {
                Value = 2,
            },
        };

        Assert.Equal(
            "{\r\n  \"Value\": 1,\r\n  \"Child\": {\r\n    \"Value\": 2,\r\n    \"Child\": null\r\n  }\r\n}",
            System.Text.Json.JsonSerializer.Serialize(payload, options));
        Assert.Equal(
            "[\r\n  1,\r\n  2\r\n]",
            System.Text.Json.JsonSerializer.Serialize(new[] { 1, 2 }, options));
        Assert.Equal("[]", System.Text.Json.JsonSerializer.Serialize(Array.Empty<int>(), options));

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
            "{\"Value\":3,\"Child\":null}",
            options);
        Assert.NotNull(deserialized);
        Assert.Equal(3, deserialized.Value);
        Assert.Null(deserialized.Child);
    }

    private static void AssertOptionsNamingPolicy(
        System.Text.Json.JsonNamingPolicy policy,
        string json)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = policy,
        };
        var payload = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionsPayload>(
            json,
            options);
        Assert.NotNull(payload);
        Assert.Equal("url", payload.URLValue);
        Assert.Equal(7, payload.SHA512Managed);
        Assert.Equal("explicit", payload.ExplicitValue);
        Assert.Equal(3, payload.Count);
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(payload, options));
    }

    [Test]
    public void SystemTextJsonMemberSelectionOptionsBaseline()
    {
        var defaultIgnoringOptions = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            IgnoreReadOnlyFields = true,
            IgnoreReadOnlyProperties = true,
            IncludeFields = true,
        };
        var payload = new SystemTextJsonMemberSelectionOptionsPayload
        {
            Value = "value",
        };
        Assert.Equal(
            "{\"Value\":\"value\",\"NeverIgnoredDefault\":0,"
                + "\"NeverIgnoredReadOnlyProperty\":\"kept\",\"IncludedField\":3,"
                + "\"NeverIgnoredReadOnlyField\":5}",
            System.Text.Json.JsonSerializer.Serialize(payload, defaultIgnoringOptions));

        var nullIgnoringOptions = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        Assert.Equal(
            "{\"Value\":\"value\",\"ZeroValue\":0,\"FalseValue\":false,"
                + "\"ReadOnlyProperty\":\"read-only\",\"NeverIgnoredDefault\":0,"
                + "\"NeverIgnoredReadOnlyProperty\":\"kept\"}",
            System.Text.Json.JsonSerializer.Serialize(payload, nullIgnoringOptions));

        var deserialized =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonMemberSelectionOptionsPayload>(
                "{\"Value\":\"incoming\",\"IncludedField\":9,\"ReadOnlyField\":99,"
                    + "\"DefaultField\":8,\"NeverIgnoredReadOnlyField\":100}",
                defaultIgnoringOptions);
        Assert.NotNull(deserialized);
        Assert.Equal("incoming", deserialized.Value);
        Assert.Equal(9, deserialized.IncludedField);
        Assert.Equal(4, deserialized.ReadOnlyField);
        Assert.Equal(8, deserialized.DefaultField);
        Assert.Equal(5, deserialized.NeverIgnoredReadOnlyField);

        var fieldsExcluded =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonMemberSelectionOptionsPayload>(
                "{\"IncludedField\":9}",
                new System.Text.Json.JsonSerializerOptions());
        Assert.Equal(3, fieldsExcluded?.IncludedField);

        var includeFieldsOptions = new System.Text.Json.JsonSerializerOptions
        {
            IncludeFields = true,
        };
        Assert.Throws<System.InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Serialize(
                new SystemTextJsonMemberSelectionCollisionPayload(),
                includeFieldsOptions));
        Assert.Throws<System.InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonMemberSelectionCollisionPayload>(
                "{}",
                includeFieldsOptions));
    }

    [Test]
    public void SystemTextJsonIgnoreNullValuesBaseline()
    {
#pragma warning disable SYSLIB0020
        var options = new System.Text.Json.JsonSerializerOptions
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
        Assert.Equal(
            "{\"Never\":null,\"NeverField\":null}",
            System.Text.Json.JsonSerializer.Serialize(writePayload, options));

        var readPayload =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesPayload>(
                "{\"Value\":null,\"Required\":null,\"Never\":null,\"NullValue\":null,"
                    + "\"Tracked\":null,\"Field\":null,\"NeverField\":null}",
                options);
        Assert.NotNull(readPayload);
        Assert.Equal("constructed", readPayload.Value);
        Assert.Null(readPayload.Required);
        Assert.Null(readPayload.Never);
        Assert.Null(readPayload.NullValue);
        Assert.Equal("tracked", readPayload.Tracked);
        Assert.Equal(0, readPayload.SetterCalls);
        Assert.Equal("field", readPayload.Field);
        Assert.Null(readPayload.NeverField);
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesPayload>(
                "{}",
                options));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "{\"Value\":null}",
                options));

        var constructor =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonIgnoreNullValuesConstructorPayload>(
                "{\"Value\":null}",
                options);
        Assert.Equal("fallback", constructor?.Value);

        Assert.Equal(
            "[null,\"value\"]",
            System.Text.Json.JsonSerializer.Serialize(new string?[] { null, "value" }, options));
        var array = System.Text.Json.JsonSerializer.Deserialize<string?[]>(
            "[null,\"value\"]",
            options);
        Assert.NotNull(array);
        Assert.Null(array[0]);
        Assert.Equal("value", array[1]);
        Assert.Equal("null", System.Text.Json.JsonSerializer.Serialize<string?>(null, options));
        Assert.Null(System.Text.Json.JsonSerializer.Deserialize<string?>("null", options));

#pragma warning disable SYSLIB0020
        Assert.Throws<System.InvalidOperationException>(() => options.IgnoreNullValues = false);
        var ignoreFirst = new System.Text.Json.JsonSerializerOptions
        {
            IgnoreNullValues = true,
        };
        Assert.Throws<System.InvalidOperationException>(() =>
            ignoreFirst.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);
        var defaultFirst = new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        Assert.Throws<System.InvalidOperationException>(() => defaultFirst.IgnoreNullValues = true);
#pragma warning restore SYSLIB0020
    }

    [Test]
    public void SystemTextJsonReaderSyntaxAndDepthOptionsBaseline()
    {
        var trailingOptions = new System.Text.Json.JsonSerializerOptions
        {
            AllowTrailingCommas = true,
        };
        var trailingObject =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "{\"Value\":7,}",
                trailingOptions);
        Assert.Equal(7, trailingObject?.Value);
        Assert.Equal(
            new[] { 1, 2 },
            System.Text.Json.JsonSerializer.Deserialize<int[]>("[1,2,]", trailingOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "{\"Value\":7,}"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int[]>("[1,2,]"));

        var commentOptions = new System.Text.Json.JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        };
        var commented =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "/* before */ { /* property */ \"Value\": /* value */ 9 /* after value */,"
                    + " /* next */ \"Text\":\"// literal /* text */\","
                    + " // trailing\r\n } /* after */",
                commentOptions);
        Assert.Equal(9, commented?.Value);
        Assert.Equal("// literal /* text */", commented?.Text);
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "{\"Value\"/* before colon */:9}",
                commentOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "/* rejected */ {\"Value\":9}"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "/* unterminated",
                commentOptions));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonReaderOptionsPayload>(
                "{\"Value\":9,,}",
                commentOptions));
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new System.Text.Json.JsonSerializerOptions
            {
                ReadCommentHandling = System.Text.Json.JsonCommentHandling.Allow,
            });
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new System.Text.Json.JsonSerializerOptions { MaxDepth = -1 });

        var writeDepthOptions = new System.Text.Json.JsonSerializerOptions
        {
            MaxDepth = 2,
        };
        var readDepthOptions = new System.Text.Json.JsonSerializerOptions
        {
            MaxDepth = 1,
        };
        var shallow = new SystemTextJsonDepthOptionsPayload { Value = 1 };
        Assert.Equal(
            "{\"Value\":1,\"Child\":null}",
            System.Text.Json.JsonSerializer.Serialize(shallow, writeDepthOptions));
        var shallowRead =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
                "{\"Value\":1,\"Child\":null}",
                readDepthOptions);
        Assert.Equal(1, shallowRead?.Value);
        Assert.Null(shallowRead?.Child);
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Serialize(
                new SystemTextJsonDepthOptionsPayload
                {
                    Value = 1,
                    Child = new SystemTextJsonDepthOptionsPayload { Value = 2 },
                },
                writeDepthOptions));
        var depthRead = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonDepthOptionsPayload>(
                "{\"Value\":1,\"Child\":{\"Value\":2}}",
                readDepthOptions));
        Assert.Equal("$.Child", depthRead.Path);
    }

    [Test]
    public void NewtonsoftJsonObjectMissingMemberHandlingBaseline()
    {
        var ignored = JsonConvert.DeserializeObject<NewtonsoftIgnoreMissingMemberPayload>(
            "{\"Known\":6,\"Unknown\":42}");
        var payload = JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
            "{\"Known\":7}",
            settings: null);

        Assert.NotNull(ignored);
        Assert.Equal(6, ignored.Known);
        Assert.NotNull(payload);
        Assert.Equal(7, payload.Known);
        Assert.Equal("{\"Known\":7}", JsonConvert.SerializeObject(payload));

        var exception = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
                "{\"Known\":7,\"Unknown\":42}"));
        var settingsException = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftErrorMissingMemberPayload>(
                "{\"Known\":7,\"Unknown\":42}",
                settings: null));
        Assert.Equal("Unknown", exception.Path);
        Assert.Equal(1, exception.LineNumber);
        Assert.Equal(21, exception.LinePosition);
        Assert.Equal(
            "Could not find member 'Unknown' on object of type "
                + "'NewtonsoftErrorMissingMemberPayload'. Path 'Unknown', line 1, position 21.",
            exception.Message);
        Assert.Equal(exception.Message, settingsException.Message);
        Assert.Equal(exception.Path, settingsException.Path);
        Assert.Equal(exception.LineNumber, settingsException.LineNumber);
        Assert.Equal(exception.LinePosition, settingsException.LinePosition);
    }

    [Test]
    public void NewtonsoftJsonObjectItemRequiredBaseline()
    {
        const string validAlways =
            "{\"RequiredByType\":\"set\",\"ExplicitAllowNull\":null}";
        var always = JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
            validAlways);
        var settingsAlways =
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
                validAlways,
                settings: null);

        Assert.NotNull(always);
        Assert.Equal("set", always.RequiredByType);
        Assert.Null(always.ExplicitDefault);
        Assert.Null(always.ExplicitAllowNull);
        Assert.NotNull(settingsAlways);
        Assert.Equal(
            "{\"RequiredByType\":\"set\",\"ExplicitDefault\":null,"
                + "\"ExplicitAllowNull\":null}",
            JsonConvert.SerializeObject(always));
        Assert.Equal(
            "{\"RequiredByType\":\"set\",\"ExplicitDefault\":null,"
                + "\"ExplicitAllowNull\":null}",
            JsonConvert.SerializeObject(always, settings: null));

        var missingAlways = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
                "{\"ExplicitAllowNull\":null}"));
        Assert.Equal(string.Empty, missingAlways.Path);
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredAlwaysPayload>(
                "{\"RequiredByType\":null,\"ExplicitAllowNull\":null}",
                settings: null));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(new NewtonsoftItemRequiredAlwaysPayload
            {
                RequiredByType = null,
                ExplicitDefault = null,
                ExplicitAllowNull = null,
            }));

        var allowNull = JsonConvert.DeserializeObject<NewtonsoftItemRequiredAllowNullPayload>(
            "{\"Value\":null}");
        Assert.NotNull(allowNull);
        Assert.Null(allowNull.Value);
        Assert.Equal("{\"Value\":null}", JsonConvert.SerializeObject(allowNull));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredAllowNullPayload>("{}"));

        var disallowNull =
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredDisallowNullPayload>("{}");
        Assert.NotNull(disallowNull);
        Assert.Null(disallowNull.Value);
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemRequiredDisallowNullPayload>(
                "{\"Value\":null}"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(disallowNull, settings: null));
    }

    [Test]
    public void NewtonsoftJsonObjectItemNullValueHandlingBaseline()
    {
        const string input =
            "{\"IgnoredByType\":null,\"ExplicitInclude\":null}";
        var payload =
            JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(input);
        var settingsPayload =
            JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(
                input,
                settings: null);

        Assert.NotNull(payload);
        Assert.Equal("type-default", payload.IgnoredByType);
        Assert.Null(payload.ExplicitInclude);
        Assert.NotNull(settingsPayload);
        Assert.Equal("type-default", settingsPayload.IgnoredByType);
        Assert.Null(settingsPayload.ExplicitInclude);

        payload.IgnoredByType = null;
        Assert.Equal(
            "{\"ExplicitInclude\":null}",
            JsonConvert.SerializeObject(payload));
        Assert.Equal(
            "{\"ExplicitInclude\":null}",
            JsonConvert.SerializeObject(payload, settings: null));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemNullIgnoreRequiredPayload>(
                "{\"Value\":null}"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftItemNullIgnoreRequiredPayload>(
                "{\"Value\":null}",
                settings: null));
        Assert.Equal(
            "{}",
            JsonConvert.SerializeObject(new NewtonsoftItemNullIgnoreRequiredPayload
            {
                Value = null,
            }));
        Assert.Equal(
            "{}",
            JsonConvert.SerializeObject(
                new NewtonsoftItemNullIgnoreRequiredPayload { Value = null },
                settings: null));
    }

    [Test]
    public void NewtonsoftJsonObjectDescriptiveAndDefaultContainerSettingsBaseline()
    {
        const string input = "{\"Value\":\"input\"}";
        var payload =
            JsonConvert.DeserializeObject<NewtonsoftDescriptiveJsonObjectPayload>(input);
        var settingsPayload =
            JsonConvert.DeserializeObject<NewtonsoftDescriptiveJsonObjectPayload>(
                input,
                settings: null);

        Assert.NotNull(payload);
        Assert.Equal("input", payload.Value);
        Assert.NotNull(settingsPayload);
        Assert.Equal("input", settingsPayload.Value);
        Assert.Equal(input, JsonConvert.SerializeObject(payload));
        Assert.Equal(input, JsonConvert.SerializeObject(payload, settings: null));
    }

    [Test]
    public void NewtonsoftBuiltInNamingStrategiesBaseline()
    {
        const string camelJson =
            "{\"urlValue\":\"url\",\"ExplicitName\":\"explicit\"}";
        var camel = JsonConvert.DeserializeObject<NewtonsoftCamelNamingPayload>(camelJson);
        Assert.NotNull(camel);
        Assert.Equal("url", camel.URLValue);
        Assert.Equal("explicit", camel.ExplicitValue);
        Assert.Equal(camelJson, JsonConvert.SerializeObject(camel));
        Assert.Equal(camelJson, JsonConvert.SerializeObject(camel, settings: null));

        const string overriddenJson = "{\"explicitName\":\"overridden\"}";
        var overridden =
            JsonConvert.DeserializeObject<NewtonsoftCamelOverrideNamingPayload>(
                overriddenJson,
                settings: null);
        Assert.NotNull(overridden);
        Assert.Equal("overridden", overridden.Value);
        Assert.Equal(overriddenJson, JsonConvert.SerializeObject(overridden));

        const string snakeJson = "{\"url_value\":\"constructed\"}";
        var snake =
            JsonConvert.DeserializeObject<NewtonsoftSnakeConstructorNamingPayload>(
                snakeJson);
        Assert.NotNull(snake);
        Assert.Equal("constructed", snake.URLValue);
        Assert.Equal(snakeJson, JsonConvert.SerializeObject(snake, settings: null));

        var kebab = JsonConvert.DeserializeObject<NewtonsoftKebabNamingPayload>(
            "{\"sh-a512-managed\":7}");
        Assert.NotNull(kebab);
        Assert.Equal(7, kebab.SHA512Managed);
        Assert.Equal("{\"sh-a512-managed\":7}", JsonConvert.SerializeObject(kebab));

        var member = JsonConvert.DeserializeObject<NewtonsoftMemberNamingPayload>(
            "{\"http-value\":\"member\",\"explicit-http-name\":\"explicit\"}");
        Assert.NotNull(member);
        Assert.Equal("member", member.HTTPValue);
        Assert.Equal("explicit", member.ExplicitValue);
        Assert.Equal(
            "{\"http-value\":\"member\",\"explicit-http-name\":\"explicit\"}",
            JsonConvert.SerializeObject(member));

        var inherited = JsonConvert.DeserializeObject<NewtonsoftDerivedNamingPayload>(
            "{\"derivedProperty\":\"derived\",\"BaseProperty\":\"base\"}");
        Assert.NotNull(inherited);
        Assert.Equal("derived", inherited.DerivedProperty);
        Assert.Equal("base", inherited.BaseProperty);
        Assert.Equal(
            "{\"derivedProperty\":\"derived\",\"BaseProperty\":\"base\"}",
            JsonConvert.SerializeObject(inherited));

        var identity = JsonConvert.DeserializeObject<NewtonsoftDefaultNamingPayload>(
            "{\"PascalName\":\"identity\"}");
        Assert.NotNull(identity);
        Assert.Equal("identity", identity.PascalName);
        Assert.Equal("{\"PascalName\":\"identity\"}", JsonConvert.SerializeObject(identity));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftNamingCollisionPayload>("{}"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(new NewtonsoftNamingCollisionPayload()));
    }

    [Test]
    public void FloatingPointFormatThresholdBaseline()
    {
        var actual = string.Join(
            "|",
            System.Text.Json.JsonSerializer.Serialize(1e14d),
            System.Text.Json.JsonSerializer.Serialize(1e15d),
            System.Text.Json.JsonSerializer.Serialize(1e16d),
            System.Text.Json.JsonSerializer.Serialize(1e17d),
            System.Text.Json.JsonSerializer.Serialize(1e-4d),
            System.Text.Json.JsonSerializer.Serialize(1e6f),
            System.Text.Json.JsonSerializer.Serialize(1e7f),
            System.Text.Json.JsonSerializer.Serialize(1e8f),
            System.Text.Json.JsonSerializer.Serialize(1e-4f),
            System.Text.Json.JsonSerializer.Serialize(1e-5f),
            JsonConvert.SerializeObject(1e14d),
            JsonConvert.SerializeObject(1e15d),
            JsonConvert.SerializeObject(1e16d),
            JsonConvert.SerializeObject(1e17d),
            JsonConvert.SerializeObject(1e-4d),
            JsonConvert.SerializeObject(1e6f),
            JsonConvert.SerializeObject(1e7f),
            JsonConvert.SerializeObject(1e8f),
            JsonConvert.SerializeObject(1e-4f),
            JsonConvert.SerializeObject(1e-5f));

        Assert.Equal(
            "100000000000000|1000000000000000|10000000000000000|1E+17|0.0001"
                + "|1000000|10000000|100000000|0.0001|1E-05"
                + "|100000000000000.0|1000000000000000.0|10000000000000000.0"
                + "|1E+17|0.0001|1000000.0|10000000.0|100000000.0|0.0001|1E-05",
            actual);
    }

    [Test]
    public void FloatingPointStringConversionBaseline()
    {
        var systemTextJsonRoot = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<double>("\"1.25\""));
        Assert.Equal("$", systemTextJsonRoot.Path);

        var systemTextJsonMember = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<FloatingPointPayload>(
                "{\"DoubleValue\":\"1.25\"}"));
        Assert.Equal("$.DoubleValue", systemTextJsonMember.Path);

        Assert.Equal(1.25d, JsonConvert.DeserializeObject<double>("\"1.25\""));
        Assert.Equal(1.25f, JsonConvert.DeserializeObject<float>("\"1.25\""));
        Assert.Equal(
            1.25d,
            JsonConvert.DeserializeObject<double>("\"1.25\"", settings: null));
        Assert.True(double.IsPositiveInfinity(
            JsonConvert.DeserializeObject<double>("\"1e400\"")));

        var newtonsoftPayload = JsonConvert.DeserializeObject<FloatingPointPayload>(
            "{\"DoubleValue\":\"1.25\",\"SingleValue\":\"-2.5\"}");
        Assert.NotNull(newtonsoftPayload);
        Assert.Equal(1.25d, newtonsoftPayload.DoubleValue);
        Assert.Equal(-2.5f, newtonsoftPayload.SingleValue);

        var newtonsoftInvalid = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<FloatingPointPayload>(
                "{\"DoubleValue\":\"invalid\"}"));
        Assert.Equal("DoubleValue", newtonsoftInvalid.Path);
    }

    [Test]
    public void IntegralStringConversionBaseline()
    {
        var systemTextJsonRoot = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int>("\"7\""));
        Assert.Equal("$", systemTextJsonRoot.Path);

        var systemTextJsonMember = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<NumericStringPayload>(
                "{\"Count\":\"7\"}"));
        Assert.Equal("$.Count", systemTextJsonMember.Path);

        Assert.Equal(7, JsonConvert.DeserializeObject<int>("\"7\""));
        Assert.Equal(7, JsonConvert.DeserializeObject<int>("\"  +007  \""));
        Assert.Equal(
            9007199254740993L,
            JsonConvert.DeserializeObject<long>("\"9007199254740993\""));
        Assert.Equal(
            7,
            JsonConvert.DeserializeObject<int>("\"7\"", settings: null));

        var newtonsoftPayload = JsonConvert.DeserializeObject<NumericStringPayload>(
            "{\"Count\":\"7\",\"Large\":\"9007199254740993\"}");
        Assert.NotNull(newtonsoftPayload);
        Assert.Equal(7, newtonsoftPayload.Count);
        Assert.Equal(9007199254740993L, newtonsoftPayload.Large);

        var newtonsoftInvalid = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NumericStringPayload>("{\"Count\":\"invalid\"}"));
        Assert.Equal("Count", newtonsoftInvalid.Path);

        var newtonsoftOverflow = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NumericStringPayload>("{\"Count\":\"2147483648\"}"));
        Assert.Equal("Count", newtonsoftOverflow.Path);
    }

    [Test]
    public void FloatingPointToIntegralRejectionBaseline()
    {
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int>("1.0"));
        Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<int>("1e0"));

        Assert.Equal(
            string.Empty,
            Assert.Throws<JsonReaderException>(() =>
                JsonConvert.DeserializeObject<int>("1.0")).Path);
        Assert.Equal(
            string.Empty,
            Assert.Throws<JsonReaderException>(() =>
                JsonConvert.DeserializeObject<int>("1e0", settings: null)).Path);
        Assert.Equal(
            "Count",
            Assert.Throws<JsonReaderException>(() =>
                JsonConvert.DeserializeObject<NumericStringPayload>(
                    "{\"Count\":1.5}")).Path);
    }

    [Test]
    public void BooleanConversionBaseline()
    {
        var systemTextJsonString = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<bool>("\"true\""));
        Assert.Equal("$", systemTextJsonString.Path);
        var systemTextJsonNumber = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<bool>("1"));
        Assert.Equal("$", systemTextJsonNumber.Path);
        var systemTextJsonMember = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<BooleanPayload>("{\"Active\":\"true\"}"));
        Assert.Equal("$.Active", systemTextJsonMember.Path);

        Assert.True(JsonConvert.DeserializeObject<bool>("\"true\""));
        Assert.False(JsonConvert.DeserializeObject<bool>("\"False\""));
        Assert.True(JsonConvert.DeserializeObject<bool>("\"  TRUE  \""));
        Assert.True(JsonConvert.DeserializeObject<bool>("1"));
        Assert.False(JsonConvert.DeserializeObject<bool>("0"));
        Assert.True(JsonConvert.DeserializeObject<bool>("-2"));
        Assert.False(JsonConvert.DeserializeObject<bool>("0.0"));
        Assert.True(JsonConvert.DeserializeObject<bool>("0.5"));
        Assert.True(JsonConvert.DeserializeObject<bool>("\"true\"", settings: null));

        var stringPayload = JsonConvert.DeserializeObject<BooleanPayload>("{\"Active\":\"true\"}");
        Assert.NotNull(stringPayload);
        Assert.True(stringPayload.Active);
        var numericPayload = JsonConvert.DeserializeObject<BooleanPayload>("{\"Active\":0}");
        Assert.NotNull(numericPayload);
        Assert.False(numericPayload.Active);

        var newtonsoftInvalid = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<BooleanPayload>("{\"Active\":\"invalid\"}"));
        Assert.Equal("Active", newtonsoftInvalid.Path);
    }

    [Test]
    public void StringConversionBaseline()
    {
        var systemTextJsonNumber = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<string>("7"));
        Assert.Equal("$", systemTextJsonNumber.Path);
        var systemTextJsonBoolean = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<string>("true"));
        Assert.Equal("$", systemTextJsonBoolean.Path);
        var systemTextJsonMember = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<StringConversionPayload>("{\"Text\":7}"));
        Assert.Equal("$.Text", systemTextJsonMember.Path);

        Assert.Equal("7", JsonConvert.DeserializeObject<string>("7"));
        Assert.Equal(
            "9007199254740993",
            JsonConvert.DeserializeObject<string>("9007199254740993"));
        Assert.Equal("1.25", JsonConvert.DeserializeObject<string>("1.25"));
        Assert.Equal("1e2", JsonConvert.DeserializeObject<string>("1e2"));
        Assert.Equal("true", JsonConvert.DeserializeObject<string>("true"));
        Assert.Equal("7", JsonConvert.DeserializeObject<string>("7", settings: null));

        var newtonsoftPayload = JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":9007199254740993}");
        Assert.NotNull(newtonsoftPayload);
        Assert.Equal("9007199254740993", newtonsoftPayload.Text);
        Assert.Equal(
            new[] { "7", "false", "1.25" },
            JsonConvert.DeserializeObject<string[]>("[7,false,1.25]"));
    }

    [Test]
    public void StringEscapingBaseline()
    {
        const string value = "\0\u001F\u0085\u2028\u2029😀<>&'+\"\\/";
        const string invalidSurrogates = "\uD800x\uDC00";

        Assert.Equal(
            "\"\\u0000\\u001F\\u0085\\u2028\\u2029\\uD83D\\uDE00"
                + "\\u003C\\u003E\\u0026\\u0027\\u002B\\u0022\\\\/\"",
            System.Text.Json.JsonSerializer.Serialize(value));
        Assert.Equal(
            "\"\\uFFFDx\\uFFFD\"",
            System.Text.Json.JsonSerializer.Serialize(invalidSurrogates));
        Assert.Equal(
            "\"\\u0000\\u001f\\u0085\\u2028\\u2029😀<>&'+\\\"\\\\/\"",
            JsonConvert.SerializeObject(value));
        Assert.Equal(
            "\"\\u0000\\u001f\\u0085\\u2028\\u2029😀<>&'+\\\"\\\\/\"",
            JsonConvert.SerializeObject(value, settings: null));

        var systemTextJsonInvalidInput = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<StringConversionPayload>(
                "{\"Text\":\"\\uD800x\\uDC00\"}"));
        Assert.Equal("$.Text", systemTextJsonInvalidInput.Path);
        Assert.Equal(0, systemTextJsonInvalidInput.LineNumber);
        Assert.Equal(23, systemTextJsonInvalidInput.BytePositionInLine);

        var newtonsoftInvalidInput = JsonConvert.DeserializeObject<StringConversionPayload>(
            "{\"Text\":\"\\uD800x\\uDC00\"}");
        Assert.NotNull(newtonsoftInvalidInput);
        Assert.Equal("\uFFFDx\uFFFD", newtonsoftInvalidInput.Text);
    }

    [Test]
    public void NewtonsoftWriterSettingsBaseline()
    {
        const string value = "é😀<>&'+\"\\/\u0085";
        var payload = new NewtonsoftWriterSettingsPayload
        {
            Text = value,
            Values = [1, 2],
        };
        var defaultSettings = new JsonSerializerSettings();

        Assert.Equal(
            "{\"Text\":\"é😀<>&'+\\\"\\\\/\\u0085\",\"Values\":[1,2]}",
            JsonConvert.SerializeObject(payload, defaultSettings));
        var defaultRead = JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
            "{\"Text\":\"read\",\"Values\":[3,4]}",
            defaultSettings);
        Assert.NotNull(defaultRead);
        Assert.Equal("read", defaultRead.Text);
        Assert.Equal([3, 4], defaultRead.Values);
        var explicitDefaultSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            StringEscapeHandling = StringEscapeHandling.Default,
        };
        Assert.Equal(
            "{\"Text\":\"é😀<>&'+\\\"\\\\/\\u0085\",\"Values\":[1,2]}",
            JsonConvert.SerializeObject(payload, explicitDefaultSettings));

        var indentedHtmlSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            StringEscapeHandling = StringEscapeHandling.EscapeHtml,
        };
        Assert.Equal(
            "{\r\n  \"Text\": \"é😀\\u003c\\u003e\\u0026\\u0027+\\u0022\\\\/\\u0085\",\r\n"
                + "  \"Values\": [\r\n    1,\r\n    2\r\n  ]\r\n}",
            JsonConvert.SerializeObject(payload, indentedHtmlSettings));
        Assert.Equal(
            "[\r\n  1,\r\n  2\r\n]",
            JsonConvert.SerializeObject(new[] { 1, 2 }, indentedHtmlSettings));
        Assert.Equal("[]", JsonConvert.SerializeObject(Array.Empty<int>(), indentedHtmlSettings));
        var settingsRead = JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
            "{\"Text\":\"writer-only\",\"Values\":[]}",
            indentedHtmlSettings);
        Assert.NotNull(settingsRead);
        Assert.Equal("writer-only", settingsRead.Text);

        var nonAsciiSettings = new JsonSerializerSettings
        {
            StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
        };
        Assert.Equal(
            "\"\\u00e9\\ud83d\\ude00<>&'+\\\"\\\\/\\u0085\"",
            JsonConvert.SerializeObject(value, nonAsciiSettings));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                Formatting = (Formatting)99,
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JsonConvert.SerializeObject(payload, new JsonSerializerSettings
            {
                StringEscapeHandling = (StringEscapeHandling)99,
            }));
        var invalidWriterSettingsRead =
            JsonConvert.DeserializeObject<NewtonsoftWriterSettingsPayload>(
                "{}",
                new JsonSerializerSettings
                {
                    Formatting = (Formatting)99,
                    StringEscapeHandling = (StringEscapeHandling)99,
                });
        Assert.NotNull(invalidWriterSettingsRead);
    }

    [Test]
    public void NewtonsoftGlobalValueAndMissingMemberSettingsBaseline()
    {
        var ignoreSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore,
        };
        var payload = new NewtonsoftGlobalSettingsPayload
        {
            IgnoredNull = null,
            IncludedNull = null,
            IgnoredDefault = 0,
            IncludedDefault = 0,
            AttributedDefault = 7,
        };
        Assert.Equal(
            "{\"IncludedNull\":null,\"IncludedDefault\":0}",
            JsonConvert.SerializeObject(payload, ignoreSettings));

        var read = JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
            "{\"IgnoredNull\":null,\"IncludedNull\":null,"
                + "\"IgnoredDefault\":0,\"IncludedDefault\":0,"
                + "\"AttributedDefault\":7}",
            ignoreSettings);
        Assert.NotNull(read);
        Assert.Equal("ignored-null-default", read.IgnoredNull);
        Assert.Equal(0, read.IgnoredNullSetterCalls);
        Assert.Null(read.IncludedNull);
        Assert.Equal(1, read.IncludedNullSetterCalls);
        Assert.Equal(5, read.IgnoredDefault);
        Assert.Equal(0, read.IgnoredDefaultSetterCalls);
        Assert.Equal(0, read.IncludedDefault);
        Assert.Equal(1, read.IncludedDefaultSetterCalls);
        Assert.Equal(3, read.AttributedDefault);
        Assert.Equal(0, read.AttributedDefaultSetterCalls);

        var populateSettings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Populate,
        };
        var populated = JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
            "{}",
            populateSettings);
        Assert.NotNull(populated);
        Assert.Null(populated.IgnoredNull);
        Assert.Equal(1, populated.IgnoredNullSetterCalls);
        Assert.Equal("included-null-default", populated.IncludedNull);
        Assert.Equal(0, populated.IncludedNullSetterCalls);
        Assert.Equal(0, populated.IgnoredDefault);
        Assert.Equal(1, populated.IgnoredDefaultSetterCalls);
        Assert.Equal(6, populated.IncludedDefault);
        Assert.Equal(0, populated.IncludedDefaultSetterCalls);
        Assert.Equal(7, populated.AttributedDefault);
        Assert.Equal(1, populated.AttributedDefaultSetterCalls);

        var ignoreAndPopulateSettings = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
        };
        var ignoredAndPopulated =
            JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
                "{\"IgnoredDefault\":0,\"AttributedDefault\":7}",
                ignoreAndPopulateSettings);
        Assert.NotNull(ignoredAndPopulated);
        Assert.Null(ignoredAndPopulated.IgnoredNull);
        Assert.Equal(1, ignoredAndPopulated.IgnoredNullSetterCalls);
        Assert.Equal("included-null-default", ignoredAndPopulated.IncludedNull);
        Assert.Equal(0, ignoredAndPopulated.IncludedNullSetterCalls);
        Assert.Equal(0, ignoredAndPopulated.IgnoredDefault);
        Assert.Equal(1, ignoredAndPopulated.IgnoredDefaultSetterCalls);
        Assert.Equal(6, ignoredAndPopulated.IncludedDefault);
        Assert.Equal(0, ignoredAndPopulated.IncludedDefaultSetterCalls);
        Assert.Equal(7, ignoredAndPopulated.AttributedDefault);
        Assert.Equal(1, ignoredAndPopulated.AttributedDefaultSetterCalls);
        Assert.Equal(
            "{\"IncludedNull\":\"included-null-default\","
                + "\"IncludedDefault\":6}",
            JsonConvert.SerializeObject(ignoredAndPopulated, ignoreAndPopulateSettings));

        var constructorPopulated =
            JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
                "{}",
                populateSettings);
        Assert.NotNull(constructorPopulated);
        Assert.Equal(7, constructorPopulated.Count);
        var constructorIgnored =
            JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
                "{\"Count\":7}",
                new JsonSerializerSettings
                {
                    DefaultValueHandling = DefaultValueHandling.Ignore,
                });
        Assert.NotNull(constructorIgnored);
        Assert.Equal(7, constructorIgnored.Count);
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsConstructorPayload>(
                "{\"Count\":null}",
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                }));

        var typeNullOverride = JsonConvert.DeserializeObject<NewtonsoftItemNullIgnorePayload>(
            "{\"IgnoredByType\":null,\"ExplicitInclude\":null}",
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
            });
        Assert.NotNull(typeNullOverride);
        Assert.Equal("type-default", typeNullOverride.IgnoredByType);
        Assert.Null(typeNullOverride.ExplicitInclude);
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftRequiredPayload>(
                "{\"Always\":\"set\",\"AllowNull\":null,\"DisallowNull\":null}",
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                }));

        var missingSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };
        var missingException = Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftGlobalSettingsPayload>(
                "{\"Unknown\":42}",
                missingSettings));
        Assert.Equal("Unknown", missingException.Path);
        Assert.Equal(1, missingException.LineNumber);
        Assert.Equal(11, missingException.LinePosition);
        var typeMissingOverride =
            JsonConvert.DeserializeObject<NewtonsoftIgnoreMissingMemberPayload>(
                "{\"Known\":6,\"Unknown\":42}",
                missingSettings);
        Assert.NotNull(typeMissingOverride);
        Assert.Equal(6, typeMissingOverride.Known);
        Assert.Equal(
            "{\"IgnoredNull\":\"ignored-null-default\","
                + "\"IncludedNull\":\"included-null-default\","
                + "\"IgnoredDefault\":5,\"IncludedDefault\":6,"
                + "\"AttributedDefault\":3}",
            JsonConvert.SerializeObject(new NewtonsoftGlobalSettingsPayload(), missingSettings));
    }

    [Test]
    public void NewtonsoftStructuralSettingsBaseline()
    {
        const string constructorJson = "{\"marker\":\"input\",\"Value\":\"set\"}";
        var defaultConstructor =
            JsonConvert.DeserializeObject<NewtonsoftConstructorHandlingPayload>(
                constructorJson,
                new JsonSerializerSettings());
        Assert.NotNull(defaultConstructor);
        Assert.Equal("public:input", defaultConstructor.Source);
        Assert.Equal("set", defaultConstructor.Value);

        var constructorSettings = new JsonSerializerSettings
        {
            ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        };
        var nonPublicConstructor =
            JsonConvert.DeserializeObject<NewtonsoftConstructorHandlingPayload>(
                constructorJson,
                constructorSettings);
        Assert.NotNull(nonPublicConstructor);
        Assert.Equal("private", nonPublicConstructor.Source);
        Assert.Equal("set", nonPublicConstructor.Value);
        Assert.Equal(
            "{\"Source\":\"private\",\"Value\":\"set\"}",
            JsonConvert.SerializeObject(nonPublicConstructor, constructorSettings));

        var attributedConstructor =
            JsonConvert.DeserializeObject<NewtonsoftAttributedConstructorHandlingPayload>(
                constructorJson,
                constructorSettings);
        Assert.NotNull(attributedConstructor);
        Assert.Equal("attributed:input", attributedConstructor.Source);
        Assert.Equal("set", attributedConstructor.Value);

        var loop = new NewtonsoftLoopSettingsPayload { Name = "root" };
        loop.Self = loop;
        Assert.Throws<JsonSerializationException>(() => JsonConvert.SerializeObject(loop));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(loop, new JsonSerializerSettings()));
        var ignoreLoopSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };
        Assert.Equal(
            "{\"Name\":\"root\"}",
            JsonConvert.SerializeObject(loop, ignoreLoopSettings));
        var loopRead = JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
            "{\"Name\":\"read\",\"Self\":null}",
            ignoreLoopSettings);
        Assert.NotNull(loopRead);
        Assert.Equal("read", loopRead.Name);
        Assert.Null(loopRead.Self);

        var memberIgnore = new NewtonsoftMemberIgnoreLoopPayload { Name = "member" };
        memberIgnore.Self = memberIgnore;
        Assert.Equal(
            "{\"Name\":\"member\"}",
            JsonConvert.SerializeObject(memberIgnore));
        Assert.Equal(
            "{\"Name\":\"member\"}",
            JsonConvert.SerializeObject(memberIgnore, new JsonSerializerSettings()));
        var memberIgnoreRead =
            JsonConvert.DeserializeObject<NewtonsoftMemberIgnoreLoopPayload>(
                "{\"Name\":\"read\",\"Self\":null}");
        Assert.NotNull(memberIgnoreRead);
        Assert.Equal("read", memberIgnoreRead.Name);

        var memberError = new NewtonsoftMemberErrorLoopPayload { Name = "member" };
        memberError.Self = memberError;
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(memberError, ignoreLoopSettings));

        const string nestedJson = "{\"Name\":\"root\",\"Self\":{\"Name\":\"nested\"}}";
        var depthSettings = new JsonSerializerSettings { MaxDepth = 1 };
        var depthException = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                nestedJson,
                depthSettings));
        Assert.Equal("Self", depthException.Path);
        Assert.Equal(1, depthException.LineNumber);
        Assert.Equal(23, depthException.LinePosition);
        Assert.Equal(
            "{\"Name\":\"write\",\"Self\":null}",
            JsonConvert.SerializeObject(
                new NewtonsoftLoopSettingsPayload { Name = "write" },
                new JsonSerializerSettings
                {
                    MaxDepth = 1,
                    CheckAdditionalContent = true,
                }));

        var deepJson = "null";
        for (var depth = 0; depth < 65; depth++)
        {
            deepJson = "{\"Self\":" + deepJson + "}";
        }
        Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(deepJson));
        Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                deepJson,
                new JsonSerializerSettings()));
        Assert.NotNull(
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                deepJson,
                new JsonSerializerSettings { MaxDepth = null }));

        const string additionalJson = "{\"Name\":\"first\"} 42";
        Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(additionalJson));
        Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                additionalJson,
                new JsonSerializerSettings()));
        var additionalException = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                additionalJson,
                new JsonSerializerSettings { CheckAdditionalContent = true }));
        Assert.Equal(string.Empty, additionalException.Path);
        Assert.Equal(1, additionalException.LineNumber);
        Assert.Equal(17, additionalException.LinePosition);
        var firstContent =
            JsonConvert.DeserializeObject<NewtonsoftLoopSettingsPayload>(
                additionalJson,
                new JsonSerializerSettings { CheckAdditionalContent = false });
        Assert.NotNull(firstContent);
        Assert.Equal("first", firstContent.Name);
    }

    [Test]
    public void DuplicateEffectiveMemberNameBaseline()
    {
        Assert.Throws<InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonNameCollisionPayload>(
                "{}"));
        Assert.Throws<InvalidOperationException>(() =>
            System.Text.Json.JsonSerializer.Serialize(
                new SystemTextJsonNameCollisionPayload()));

        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftNameCollisionPayload>("{}"));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftNameCollisionPayload>(
                "{}",
                settings: null));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(new NewtonsoftNameCollisionPayload()));
        Assert.Throws<JsonSerializationException>(() =>
            JsonConvert.SerializeObject(
                new NewtonsoftNameCollisionPayload(),
                settings: null));
    }

    private static string CaptureExceptionType(Action action)
    {
        try
        {
            action();
            return "none";
        }
        catch (Exception exception)
        {
            return exception.GetType().Name;
        }
    }

    [Test]
    public void ExceptionLocationBaseline()
    {
        var systemTextJsonSyntax = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(
                "{\n  \"TemperatureC\": 1,\n  \"Active\": truX\n}"));
        var systemTextJsonConversion = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(
                "{\n  \"temperatureC\": 2147483648\n}"));
        var systemTextJsonUtf8Syntax = Assert.Throws<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonWeatherPayload>(
                "{\"Text\":\"é\",\"Active\":truX}"));
        var newtonsoftSyntax = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
                "{\n  \"Small\": 1,\n  \"Active\": truX\n}"));
        var newtonsoftConversion = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
                "{\n  \"Small\": 2147483648\n}"));
        var newtonsoftSettingsSyntax = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
                "{\n  \"Small\": 1,\n  \"Active\": truX\n}",
                settings: null));
        var newtonsoftSettingsConversion = Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<NewtonsoftWeatherPayload>(
                "{\n  \"Small\": 2147483648\n}",
                settings: null));

        Assert.Equal("$.Active|2|15", $"{systemTextJsonSyntax.Path}|{systemTextJsonSyntax.LineNumber}|{systemTextJsonSyntax.BytePositionInLine}");
        Assert.Equal("$.temperatureC|1|28", $"{systemTextJsonConversion.Path}|{systemTextJsonConversion.LineNumber}|{systemTextJsonConversion.BytePositionInLine}");
        Assert.Equal("$.Active|0|25", $"{systemTextJsonUtf8Syntax.Path}|{systemTextJsonUtf8Syntax.LineNumber}|{systemTextJsonUtf8Syntax.BytePositionInLine}");
        Assert.Equal("Active|3|15", $"{newtonsoftSyntax.Path}|{newtonsoftSyntax.LineNumber}|{newtonsoftSyntax.LinePosition}");
        Assert.Equal("Small|2|21", $"{newtonsoftConversion.Path}|{newtonsoftConversion.LineNumber}|{newtonsoftConversion.LinePosition}");
        Assert.Equal("Active|3|15", $"{newtonsoftSettingsSyntax.Path}|{newtonsoftSettingsSyntax.LineNumber}|{newtonsoftSettingsSyntax.LinePosition}");
        Assert.Equal("Small|2|21", $"{newtonsoftSettingsConversion.Path}|{newtonsoftSettingsConversion.LineNumber}|{newtonsoftSettingsConversion.LinePosition}");
    }

    [Test]
    public void ShadowedAndReadOnlyMemberBaseline()
    {
        var systemTextJson = System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonShadowedPayload>(
            "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\"}");

        Assert.NotNull(systemTextJson);
        Assert.Equal("derived-input", systemTextJson.Value);
        Assert.Equal("base-input", ((SystemTextJsonShadowedBase)systemTextJson).Value);
        Assert.Equal(
            "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\"}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJson));

        var newtonsoft = JsonConvert.DeserializeObject<NewtonsoftShadowedPayload>(
            "{\"derivedValue\":\"derived-input\",\"baseValue\":\"base-input\","
                + "\"ReadOnlyField\":99,\"ReadOnlyProperty\":\"incoming\",\"Item\":\"incoming\"}");

        Assert.NotNull(newtonsoft);
        Assert.Equal("derived-input", newtonsoft.Value);
        Assert.Equal("base-input", ((NewtonsoftShadowedBase)newtonsoft).Value);
        Assert.Equal(7, newtonsoft.ReadOnlyField);
        Assert.Equal("read-only", newtonsoft.ReadOnlyProperty);
        Assert.Equal(
            "{\"ReadOnlyField\":7,\"derivedValue\":\"derived-input\","
                + "\"baseValue\":\"base-input\",\"ReadOnlyProperty\":\"read-only\"}",
            JsonConvert.SerializeObject(newtonsoft));
    }

    [Test]
    public void ConstructorSelectionBaseline()
    {
        Assert.Throws<NotSupportedException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonPrivateDefaultPayload>(
                "{\"Name\":\"input\"}"));

        var systemTextJsonParameterized =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonParameterizedPayload>(
                "{\"display_name\":\"input\",\"Count\":3}");
        Assert.NotNull(systemTextJsonParameterized);
        Assert.Equal("ctor:input", systemTextJsonParameterized.Name);
        Assert.Equal(0, systemTextJsonParameterized.SetterCalls);
        Assert.Equal(3, systemTextJsonParameterized.Count);

        var systemTextJsonAttributed =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonAttributedPrivatePayload>(
                "{\"Name\":\"input\"}");
        Assert.NotNull(systemTextJsonAttributed);
        Assert.Equal("attributed:input", systemTextJsonAttributed.Name);

        var newtonsoftPrivateDefault =
            JsonConvert.DeserializeObject<NewtonsoftPrivateDefaultPayload>(
                "{\"Name\":\"input\"}");
        Assert.NotNull(newtonsoftPrivateDefault);
        Assert.Equal("private-default", newtonsoftPrivateDefault.ConstructorMarker);
        Assert.Equal("input", newtonsoftPrivateDefault.Name);

        var newtonsoftParameterized =
            JsonConvert.DeserializeObject<NewtonsoftParameterizedPayload>(
                "{\"display_name\":\"input\",\"Count\":3}");
        Assert.NotNull(newtonsoftParameterized);
        Assert.Equal("ctor:input", newtonsoftParameterized.Name);
        Assert.Equal(0, newtonsoftParameterized.SetterCalls);
        Assert.Equal(3, newtonsoftParameterized.Count);

        var newtonsoftAttributed =
            JsonConvert.DeserializeObject<NewtonsoftAttributedPrivatePayload>(
                "{\"Name\":\"input\"}");
        Assert.NotNull(newtonsoftAttributed);
        Assert.Equal("attributed:input", newtonsoftAttributed.Name);
    }

    [Test]
    public void RecordAndMissingConstructorArgumentBaseline()
    {
        var systemTextJsonRecord =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonRecordPayload>(
                "{\"display_name\":\"record\",\"Count\":3}");
        Assert.NotNull(systemTextJsonRecord);
        Assert.Equal("record", systemTextJsonRecord.Name);
        Assert.Equal(3, systemTextJsonRecord.Count);
        Assert.Equal(
            "{\"display_name\":\"record\",\"Count\":3}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJsonRecord));

        var systemTextJsonMissing =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonOptionalConstructorPayload>("{}");
        Assert.NotNull(systemTextJsonMissing);
        Assert.Equal("system-text-default", systemTextJsonMissing.Name);
        Assert.Equal(17, systemTextJsonMissing.Count);
        Assert.True(systemTextJsonMissing.Active);
        Assert.Equal(9007199254740993L, systemTextJsonMissing.Large);
        Assert.Null(systemTextJsonMissing.Absent);
        Assert.Equal('Ω', systemTextJsonMissing.Marker);
        Assert.Equal(1.25f, systemTextJsonMissing.SingleValue);
        Assert.Equal(-2.5d, systemTextJsonMissing.DoubleValue);

        var newtonsoftRecord =
            JsonConvert.DeserializeObject<NewtonsoftRecordPayload>(
                "{\"display_name\":\"record\",\"Count\":3}");
        Assert.NotNull(newtonsoftRecord);
        Assert.Equal("record", newtonsoftRecord.Name);
        Assert.Equal(3, newtonsoftRecord.Count);
        Assert.Equal(
            "{\"display_name\":\"record\",\"Count\":3}",
            JsonConvert.SerializeObject(newtonsoftRecord));

        var newtonsoftMissing =
            JsonConvert.DeserializeObject<NewtonsoftOptionalConstructorPayload>("{}");
        Assert.NotNull(newtonsoftMissing);
        Assert.Null(newtonsoftMissing.Name);
        Assert.Equal(0, newtonsoftMissing.Count);
        Assert.False(newtonsoftMissing.Active);
        Assert.Equal(0, newtonsoftMissing.Large);
        Assert.Null(newtonsoftMissing.Absent);
    }

    [Test]
    public void OptInNonPublicMemberBaseline()
    {
        var systemTextJson =
            System.Text.Json.JsonSerializer.Deserialize<SystemTextJsonNonPublicIncludePayload>(
                "{\"private_field\":7,\"private_property\":\"input\"}");
        Assert.NotNull(systemTextJson);
        Assert.Equal("set:input", systemTextJson.PrivatePropertyValue);
        Assert.Equal(
            "{\"private_property\":\"set:input\",\"private_field\":7}",
            System.Text.Json.JsonSerializer.Serialize(systemTextJson));

        var newtonsoft = JsonConvert.DeserializeObject<NewtonsoftNonPublicMemberPayload>(
            "{\"private_field\":7,\"private_property\":\"input\"}");
        Assert.NotNull(newtonsoft);
        Assert.Equal("set:input", newtonsoft.PrivatePropertyValue);
        Assert.Equal(
            "{\"private_field\":7,\"private_property\":\"set:input\"}",
            JsonConvert.SerializeObject(newtonsoft));
    }

    private sealed class NewtonsoftWeatherPayload
    {
        private string summary = "constructor-default";

        public int PublicField = -1;

        public string Summary
        {
            get => summary;
            set => summary = "set:" + value;
        }

        [JsonProperty("temperature_c")]
        public int TemperatureC { get; set; }

        public bool Active { get; set; }

        [JsonIgnore]
        public string Ignored { get; set; } = "constructor-default";

        public string Text { get; set; } = "constructor-default";

        public long Large { get; set; }

        public string? Optional { get; set; } = "constructor-default";

        public int Small { get; set; }
    }

    private sealed class SystemTextJsonWeatherPayload
    {
        private string summary = "constructor-default";

        public string Summary
        {
            get => summary;
            set => summary = "set:" + value;
        }

        [System.Text.Json.Serialization.JsonPropertyName("temperatureC")]
        public int TemperatureC { get; set; }

        public bool Active { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string Ignored { get; set; } = "constructor-default";

        public string Text { get; set; } = "constructor-default";

        public long Large { get; set; }

        public string? Optional { get; set; } = "constructor-default";

        [System.Text.Json.Serialization.JsonInclude]
        public string Included { get; private set; } = "constructor-default";

        public string InitOnly { get; init; } = "constructor-default";

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? OmittedNull { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public int OmittedDefault { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public string NeverIgnored { get; set; } = "constructor-default";

        public string ReadOnly => "read-only";

        public string this[int index] => $"item:{index}";
    }

    private sealed class SystemTextJsonPrivateDefaultPayload
    {
        private SystemTextJsonPrivateDefaultPayload()
        {
        }

        public string Name { get; set; } = "default";
    }

    private sealed class SystemTextJsonParameterizedPayload
    {
        private string name;

        public SystemTextJsonParameterizedPayload(string name)
        {
            this.name = "ctor:" + name;
        }

        [System.Text.Json.Serialization.JsonPropertyName("display_name")]
        public string Name
        {
            get => name;
            set
            {
                name = "setter:" + value;
                SetterCalls++;
            }
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public int SetterCalls { get; private set; }

        public int Count { get; set; }
    }

    private sealed class SystemTextJsonAttributedPrivatePayload
    {
        [System.Text.Json.Serialization.JsonConstructor]
        private SystemTextJsonAttributedPrivatePayload(string name)
        {
            Name = "attributed:" + name;
        }

        public string Name { get; }
    }

    private sealed record SystemTextJsonRecordPayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("display_name")] string Name,
        int Count);

    private sealed class SystemTextJsonOptionalConstructorPayload
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

    private sealed class SystemTextJsonNonPublicIncludePayload
    {
        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName("private_field")]
#pragma warning disable CS0169, IDE0044 // Accessed only through serializer reflection metadata.
        private int privateField;
#pragma warning restore CS0169, IDE0044

        private string privateProperty = "default";

        [System.Text.Json.Serialization.JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName("private_property")]
        private string PrivateProperty
        {
            get => privateProperty;
            set => privateProperty = "set:" + value;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string PrivatePropertyValue => PrivateProperty;
    }

    private class SystemTextJsonBasePayload
    {
        public string BaseProperty { get; set; } = "base-default";
    }

    private sealed class SystemTextJsonDerivedPayload : SystemTextJsonBasePayload
    {
        public string DerivedProperty { get; set; } = "derived-default";
    }

    private sealed class SystemTextJsonOrderedPayload
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

    private sealed class SystemTextJsonRequiredPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("required_text")]
        [System.Text.Json.Serialization.JsonRequired]
        public string? RequiredText { get; set; }
    }

    private sealed class SystemTextJsonKeywordRequiredPayload
    {
        public required string? KeywordRequired { get; set; }
    }

    private class SystemTextJsonShadowedBase
    {
        private string baseValue = "base-default";

        [System.Text.Json.Serialization.JsonPropertyName("baseValue")]
        public string Value
        {
            get => baseValue;
            set => baseValue = value;
        }
    }

    private sealed class SystemTextJsonShadowedPayload : SystemTextJsonShadowedBase
    {
        private string derivedValue = "derived-default";

        [System.Text.Json.Serialization.JsonPropertyName("derivedValue")]
        public new string Value
        {
            get => derivedValue;
            set => derivedValue = value;
        }
    }

    private class NewtonsoftBasePayload
    {
        public int BaseField = -1;

        public string BaseProperty { get; set; } = "base-default";
    }

    private sealed class NewtonsoftDerivedPayload : NewtonsoftBasePayload
    {
        public int DerivedField = -1;

        public string DerivedProperty { get; set; } = "derived-default";
    }

    private sealed class NewtonsoftOrderedPayload
    {
        [JsonProperty(Order = -3)]
        public int EarlyField = 1;

        public int DefaultField = 2;

        [JsonProperty(Order = 2)]
        public int LateProperty { get; set; } = 6;

        [JsonProperty(Order = -2)]
        public int EarlyProperty { get; set; } = 3;

        public int DefaultProperty { get; set; } = 4;

        [JsonProperty(Order = 1)]
        public int AfterDefaults { get; set; } = 5;
    }

    private sealed class NewtonsoftRequiredPayload
    {
        [JsonRequired]
        public string? Always { get; set; }

        [JsonProperty(Required = Required.AllowNull)]
        public string? AllowNull { get; set; }

        [JsonProperty(Required = Required.DisallowNull)]
        public string? DisallowNull { get; set; }
    }

    private sealed class NewtonsoftNullValueHandlingPayload
    {
        private string? ignored = "ignored-default";
        private string? included = "included-default";

        [JsonIgnore]
        public int IgnoredSetterCalls { get; private set; }

        [JsonIgnore]
        public int IncludedSetterCalls { get; private set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? Ignored
        {
            get => ignored;
            set
            {
                ignored = value;
                IgnoredSetterCalls++;
            }
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
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

    private sealed class NewtonsoftDefaultValueHandlingPayload
    {
        private int ignored = 5;
        private int included = 5;
        private int populated = 5;
        private int ignoredAndPopulated = 5;

        [JsonIgnore]
        public int IgnoredSetterCalls { get; private set; }

        [JsonIgnore]
        public int IncludedSetterCalls { get; private set; }

        [JsonIgnore]
        public int PopulatedSetterCalls { get; private set; }

        [JsonIgnore]
        public int IgnoredAndPopulatedSetterCalls { get; private set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int Ignored
        {
            get => ignored;
            set
            {
                ignored = value;
                IgnoredSetterCalls++;
            }
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int Included
        {
            get => included;
            set
            {
                included = value;
                IncludedSetterCalls++;
            }
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int Populated
        {
            get => populated;
            set
            {
                populated = value;
                PopulatedSetterCalls++;
            }
        }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public int IgnoredAndPopulated
        {
            get => ignoredAndPopulated;
            set
            {
                ignoredAndPopulated = value;
                IgnoredAndPopulatedSetterCalls++;
            }
        }

        [DefaultValue(7)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int AttributedIgnoredInteger { get; set; } = 3;

        [DefaultValue(7)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AttributedPopulatedInteger { get; set; } = 3;

        [DefaultValue("custom")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string AttributedAssignedString { get; set; } = "initial";

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool AttributedIgnoredBoolean { get; set; }

        [DefaultValue('Z')]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public char AttributedIgnoredCharacter { get; set; } = 'A';

        [DefaultValue(9007199254740993L)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long AttributedIgnoredInt64 { get; set; } = 4;

        [DefaultValue(9007199254740993UL)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ulong AttributedIgnoredUInt64 { get; set; } = 4;

        [DefaultValue(1.25f)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float AttributedIgnoredSingle { get; set; } = 2.5f;

        [DefaultValue(1.25d)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double AttributedIgnoredDouble { get; set; } = 2.5d;
    }

    [JsonObject(MemberSerialization.OptIn)]
    private sealed class NewtonsoftOptInPayload
    {
        [JsonProperty("included_property")]
        public string IncludedProperty { get; set; } = "included-default";

        public string ExcludedProperty { get; set; } = "excluded-default";

        [JsonProperty("included_field")]
#pragma warning disable IDE0044 // Mutated through serializer reflection metadata.
        private int includedField = 1;
#pragma warning restore IDE0044

        public int ExcludedField = 2;

        public int IncludedFieldValue => includedField;
    }

    private enum JsonSByteEnum : sbyte
    {
        Minimum = sbyte.MinValue,
    }

    private enum JsonByteEnum : byte
    {
        Maximum = byte.MaxValue,
    }

    private enum JsonInt16Enum : short
    {
        Minimum = short.MinValue,
    }

    private enum JsonUInt16Enum : ushort
    {
        Maximum = ushort.MaxValue,
    }

    private enum JsonInt32Enum : int
    {
        Minimum = int.MinValue,
    }

    private enum JsonUInt32Enum : uint
    {
        Maximum = uint.MaxValue,
    }

    private enum JsonInt64Enum : long
    {
        NegativeLarge = -9007199254740993L,
        NegativeLargeAlias = NegativeLarge,
    }

    private enum JsonUInt64Enum : ulong
    {
        Large = 9007199254740993UL,
        Maximum = ulong.MaxValue,
    }

    [Flags]
    private enum JsonFlagsEnum : byte
    {
        None = 0,
        Read = 1,
        Write = 2,
    }

    private enum JsonNamedEnum
    {
        FirstValue = 1,
    }

    private sealed class EnumPayload
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

    private sealed class NewtonsoftPrivateDefaultPayload
    {
        private NewtonsoftPrivateDefaultPayload()
        {
            ConstructorMarker = "private-default";
        }

        public string ConstructorMarker { get; }

        public string Name { get; set; } = "default";
    }

    private sealed class NewtonsoftParameterizedPayload
    {
        private string name;

        public NewtonsoftParameterizedPayload(string name)
        {
            this.name = "ctor:" + name;
        }

        [JsonProperty("display_name")]
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

    private sealed class NewtonsoftAttributedPrivatePayload
    {
        [Newtonsoft.Json.JsonConstructor]
        private NewtonsoftAttributedPrivatePayload(string name)
        {
            Name = "attributed:" + name;
        }

        public string Name { get; }
    }

    private sealed record NewtonsoftRecordPayload(
        [property: JsonProperty("display_name")] string Name,
        int Count);

    private sealed class NewtonsoftOptionalConstructorPayload
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

    private sealed class NewtonsoftNonPublicMemberPayload
    {
        [JsonProperty("private_field")]
#pragma warning disable CS0169, IDE0044 // Accessed only through serializer reflection metadata.
        private int privateField;
#pragma warning restore CS0169, IDE0044

        private string privateProperty = "default";

        [JsonProperty("private_property")]
        private string PrivateProperty
        {
            get => privateProperty;
            set => privateProperty = "set:" + value;
        }

        [JsonIgnore]
        public string PrivatePropertyValue => PrivateProperty;
    }

    private class NewtonsoftShadowedBase
    {
        private string baseValue = "base-default";

        [JsonProperty("baseValue")]
        public string Value
        {
            get => baseValue;
            set => baseValue = value;
        }
    }

    private sealed class NewtonsoftShadowedPayload : NewtonsoftShadowedBase
    {
        private string derivedValue = "derived-default";

        public readonly int ReadOnlyField = 7;

        [JsonProperty("derivedValue")]
        public new string Value
        {
            get => derivedValue;
            set => derivedValue = value;
        }

        public string ReadOnlyProperty => "read-only";

        public string this[int index] => $"item:{index}";
    }

    private sealed class FloatingPointPayload
    {
        public double DoubleValue { get; set; }

        public float SingleValue { get; set; }
    }

    private sealed class SystemTextJsonNumberHandlingPayload
    {
        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
        public long QuotedInteger { get; set; }

        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString)]
        public double QuotedDouble { get; set; }

        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
        public int ReadQuotedWriteNumber { get; set; }

        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals)]
        public double NamedDouble { get; set; }
    }

    [System.Text.Json.Serialization.JsonNumberHandling(
        System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString
        | System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    private sealed class SystemTextJsonTypeNumberHandlingPayload
    {
        public long QuotedInteger { get; set; }

        public double NamedDouble { get; set; }

        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.Strict)]
        public int StrictInteger { get; set; }
    }

    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
    private sealed class SystemTextJsonDisallowUnmappedPayload
    {
        public int Known { get; set; }
    }

    [System.Text.Json.Serialization.JsonUnmappedMemberHandling(
        System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip)]
    private sealed class SystemTextJsonSkipUnmappedPayload
    {
        public int Known { get; set; }
    }

    private sealed class SystemTextJsonOptionsPayload
    {
        public string? URLValue { get; set; }

        public int SHA512Managed { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("ExplicitName")]
        public string? ExplicitValue { get; set; }

        public int Count { get; set; }
    }

    private sealed class SystemTextJsonOptionsConstructorPayload
    {
        public SystemTextJsonOptionsConstructorPayload(string? URLValue)
        {
            this.URLValue = URLValue;
        }

        public string? URLValue { get; }
    }

    private sealed class SystemTextJsonOptionsNumberPayload
    {
        public int Count { get; set; }

        [System.Text.Json.Serialization.JsonNumberHandling(
            System.Text.Json.Serialization.JsonNumberHandling.Strict)]
        public int StrictCount { get; set; }
    }

    private sealed class SystemTextJsonOptionsNamingCollisionPayload
    {
        public string? URLValue { get; set; }

        public string? UrlValue { get; set; }
    }

    private sealed class SystemTextJsonOptionsCaseCollisionPayload
    {
        public string? Value { get; set; }

        public string? value { get; set; }
    }

    private sealed class SystemTextJsonOptionsRequiredPayload
    {
        [System.Text.Json.Serialization.JsonRequired]
        public string? Value { get; set; }
    }

    private sealed class SystemTextJsonMemberSelectionOptionsPayload
    {
        public string? Value { get; set; } = "constructed";

        public string? NullValue { get; set; }

        public int ZeroValue { get; set; }

        public bool FalseValue { get; set; }

        public string ReadOnlyProperty => "read-only";

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public int NeverIgnoredDefault { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? AttributeIgnoredNull { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public string NeverIgnoredReadOnlyProperty => "kept";

        public int IncludedField = 3;

        public readonly int ReadOnlyField = 4;

        public int DefaultField = 0;

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public readonly int NeverIgnoredReadOnlyField = 5;
    }

    private sealed class SystemTextJsonMemberSelectionCollisionPayload
    {
        public int Value { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("Value")]
        public int CollidingField = 0;
    }

    private sealed class SystemTextJsonIgnoreNullValuesPayload
    {
        private string? _tracked = "tracked";

        public string? Value { get; set; } = "constructed";

        [System.Text.Json.Serialization.JsonRequired]
        public string? Required { get; set; } = "required";

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
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

        [System.Text.Json.Serialization.JsonIgnore]
        public int SetterCalls { get; private set; }

        public string? Field = "field";

        [System.Text.Json.Serialization.JsonIgnore(
            Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)]
        public string? NeverField = "never-field";
    }

    private sealed class SystemTextJsonIgnoreNullValuesConstructorPayload
    {
        public SystemTextJsonIgnoreNullValuesConstructorPayload(string? value = "fallback")
        {
            Value = value;
        }

        public string? Value { get; }
    }

    private sealed class SystemTextJsonReaderOptionsPayload
    {
        public int Value { get; set; }

        public string? Text { get; set; }
    }

    private sealed class SystemTextJsonDepthOptionsPayload
    {
        public int Value { get; set; }

        public SystemTextJsonDepthOptionsPayload? Child { get; set; }
    }

    private sealed class SystemTextJsonReferencePairPayload
    {
        public SystemTextJsonDepthOptionsPayload? First { get; set; }

        public SystemTextJsonDepthOptionsPayload? Second { get; set; }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    private sealed class NewtonsoftErrorMissingMemberPayload
    {
        public int Known { get; set; }
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Ignore)]
    private sealed class NewtonsoftIgnoreMissingMemberPayload
    {
        public int Known { get; set; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    private sealed class NewtonsoftItemRequiredAlwaysPayload
    {
        public string? RequiredByType { get; set; }

        [JsonProperty(Required = Required.Default)]
        public string? ExplicitDefault { get; set; }

        [JsonProperty(Required = Required.AllowNull)]
        public string? ExplicitAllowNull { get; set; }

        [JsonIgnore]
        public string? Ignored { get; set; }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    private sealed class NewtonsoftItemRequiredAllowNullPayload
    {
        public string? Value { get; set; }
    }

    [JsonObject(ItemRequired = Required.DisallowNull)]
    private sealed class NewtonsoftItemRequiredDisallowNullPayload
    {
        public string? Value { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    private sealed class NewtonsoftItemNullIgnorePayload
    {
        public string? IgnoredByType { get; set; } = "type-default";

        [JsonProperty(NullValueHandling = NullValueHandling.Include)]
        public string? ExplicitInclude { get; set; } = "include-default";
    }

    [JsonObject(
        ItemNullValueHandling = NullValueHandling.Ignore,
        ItemRequired = Required.Always)]
    private sealed class NewtonsoftItemNullIgnoreRequiredPayload
    {
        public string? Value { get; set; } = "required-default";
    }

    [JsonObject(
        Id = "descriptive-contract",
        Title = "Descriptive contract",
        Description = "Schema-only metadata",
        IsReference = false,
        ItemIsReference = false,
        ItemReferenceLoopHandling = ReferenceLoopHandling.Error,
        ItemTypeNameHandling = TypeNameHandling.None)]
    private sealed class NewtonsoftDescriptiveJsonObjectPayload
    {
        public string? Value { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
    private sealed class NewtonsoftCamelNamingPayload
    {
        public string? URLValue { get; set; }

        [JsonProperty("ExplicitName")]
        public string? ExplicitValue { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy),
        NamingStrategyParameters = new object[] { false, true, false })]
    private sealed class NewtonsoftCamelOverrideNamingPayload
    {
        [JsonProperty("ExplicitName")]
        public string? Value { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
    private sealed class NewtonsoftSnakeConstructorNamingPayload
    {
        public NewtonsoftSnakeConstructorNamingPayload(string? URLValue)
        {
            this.URLValue = URLValue;
        }

        public string? URLValue { get; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
    private sealed class NewtonsoftKebabNamingPayload
    {
        public int SHA512Managed { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.SnakeCaseNamingStrategy))]
    private sealed class NewtonsoftMemberNamingPayload
    {
        [JsonProperty(
            NamingStrategyType =
                typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy))]
        public string? HTTPValue { get; set; }

        [JsonProperty(
            "ExplicitHTTPName",
            NamingStrategyType =
                typeof(Newtonsoft.Json.Serialization.KebabCaseNamingStrategy),
            NamingStrategyParameters = new object[] { false, true })]
        public string? ExplicitValue { get; set; }
    }

    private class NewtonsoftBaseNamingPayload
    {
        public string? BaseProperty { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
    private sealed class NewtonsoftDerivedNamingPayload : NewtonsoftBaseNamingPayload
    {
        public string? DerivedProperty { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.DefaultNamingStrategy))]
    private sealed class NewtonsoftDefaultNamingPayload
    {
        public string? PascalName { get; set; }
    }

    [JsonObject(
        NamingStrategyType =
            typeof(Newtonsoft.Json.Serialization.CamelCaseNamingStrategy))]
    private sealed class NewtonsoftNamingCollisionPayload
    {
        public string? URLValue { get; set; }

        public string? UrlValue { get; set; }
    }

    private sealed class NumericStringPayload
    {
        public int Count { get; set; }

        public long Large { get; set; }
    }

    private sealed class BooleanPayload
    {
        public bool Active { get; set; }
    }

    private sealed class StringConversionPayload
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class NewtonsoftWriterSettingsPayload
    {
        public string Text { get; set; } = string.Empty;

        public int[] Values { get; set; } = [];
    }

    private sealed class NewtonsoftGlobalSettingsPayload
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

        [JsonIgnore]
        public int IgnoredNullSetterCalls { get; private set; }

        [JsonProperty(
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include)]
        public string? IncludedNull
        {
            get => _includedNull;
            set
            {
                IncludedNullSetterCalls++;
                _includedNull = value;
            }
        }

        [JsonIgnore]
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

        [JsonIgnore]
        public int IgnoredDefaultSetterCalls { get; private set; }

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
        public int IncludedDefault
        {
            get => _includedDefault;
            set
            {
                IncludedDefaultSetterCalls++;
                _includedDefault = value;
            }
        }

        [JsonIgnore]
        public int IncludedDefaultSetterCalls { get; private set; }

        [DefaultValue(7)]
        public int AttributedDefault
        {
            get => _attributedDefault;
            set
            {
                AttributedDefaultSetterCalls++;
                _attributedDefault = value;
            }
        }

        [JsonIgnore]
        public int AttributedDefaultSetterCalls { get; private set; }
    }

    private sealed class NewtonsoftGlobalSettingsConstructorPayload
    {
        [JsonConstructor]
        public NewtonsoftGlobalSettingsConstructorPayload(int count)
        {
            Count = count;
        }

        [DefaultValue(7)]
        public int Count { get; }
    }

    private sealed class NewtonsoftConstructorHandlingPayload
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

    private sealed class NewtonsoftAttributedConstructorHandlingPayload
    {
        private NewtonsoftAttributedConstructorHandlingPayload()
        {
            Source = "private";
        }

        [JsonConstructor]
        public NewtonsoftAttributedConstructorHandlingPayload(string? marker)
        {
            Source = "attributed:" + marker;
        }

        public string Source { get; }

        public string? Value { get; set; }
    }

    private sealed class NewtonsoftLoopSettingsPayload
    {
        public string? Name { get; set; }

        public NewtonsoftLoopSettingsPayload? Self { get; set; }
    }

    private sealed class NewtonsoftMemberIgnoreLoopPayload
    {
        public string? Name { get; set; }

        [JsonProperty(ReferenceLoopHandling = ReferenceLoopHandling.Ignore)]
        public NewtonsoftMemberIgnoreLoopPayload? Self { get; set; }
    }

    private sealed class NewtonsoftMemberErrorLoopPayload
    {
        public string? Name { get; set; }

        [JsonProperty(ReferenceLoopHandling = ReferenceLoopHandling.Error)]
        public NewtonsoftMemberErrorLoopPayload? Self { get; set; }
    }

    private sealed class SystemTextJsonNameCollisionPayload
    {
        [System.Text.Json.Serialization.JsonPropertyName("same")]
        public int First { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("same")]
        public int Second { get; set; }
    }

    private sealed class NewtonsoftNameCollisionPayload
    {
        [JsonProperty("same")]
        public int First { get; set; }

        [JsonProperty("same")]
        public int Second { get; set; }
    }

    private sealed class CharacterPayload
    {
        public char Value { get; set; }
    }

    private sealed class JsonCollectionItem
    {
        public string? Name { get; set; }

        public int Value { get; set; }
    }

    private struct SystemTextJsonValueWithDefaultConstructor
    {
        public static int Calls;

        public SystemTextJsonValueWithDefaultConstructor()
        {
            Calls++;
            Marker = 31;
        }

        public int Marker { get; set; }
    }

    private struct SystemTextJsonParameterizedValue
    {
        [System.Text.Json.Serialization.JsonConstructor]
        private SystemTextJsonParameterizedValue(int value = 17)
        {
            Value = value;
        }

        public int Value { get; }
    }

    private struct SystemTextJsonThrowingValue
    {
        public SystemTextJsonThrowingValue()
        {
            throw new InvalidOperationException("system-text-value-constructor");
        }
    }

    private struct SystemTextJsonOrdinaryParameterizedValue
    {
        public SystemTextJsonOrdinaryParameterizedValue(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    private struct NewtonsoftValueWithDefaultConstructor
    {
        public static int Calls;

        public NewtonsoftValueWithDefaultConstructor()
        {
            Calls++;
            Marker = 41;
        }

        public int Marker { get; set; }
    }

    private struct NewtonsoftParameterizedValue
    {
        [JsonConstructor]
        private NewtonsoftParameterizedValue(int value = 17)
        {
            Value = value;
        }

        public int Value { get; }
    }

    private struct NewtonsoftThrowingValue
    {
        [JsonConstructor]
        private NewtonsoftThrowingValue(int value = 0)
        {
            throw new InvalidOperationException("newtonsoft-value-constructor");
        }

        public int Value { get; }
    }

    private struct NewtonsoftOrdinaryParameterizedValue
    {
        public NewtonsoftOrdinaryParameterizedValue(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    private sealed class HttpJsonResponse
    {
        public string? Location { get; set; }

        public List<HttpJsonForecast> Forecasts { get; set; } = [];

        public Dictionary<string, int> Measurements { get; set; } = [];
    }

    private sealed class HttpJsonForecast
    {
        public string? Day { get; set; }

        public int TemperatureC { get; set; }
    }
}
