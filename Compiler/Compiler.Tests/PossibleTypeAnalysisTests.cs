using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.PossibleType;
using Compiler.Read;
using Compiler.Write;
using FrontEndCSharp.RuntimeOverrides;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Compiler.Tests;

public sealed class PossibleTypeAnalysisTests
{
    private static readonly Lazy<CallGraphInfo> FocusedJsonCallGraph = new(
        CreateFocusedJsonCallGraph,
        LazyThreadSafetyMode.ExecutionAndPublication);

    [Test]
    public void PossibleTypeAnalysisHasItsOwnCompilerNamespace()
    {
        var analysisTypes = typeof(Reader).Assembly.GetTypes()
            .Where(type => type.Name.StartsWith("PossibleType", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(analysisTypes);
        Assert.All(
            analysisTypes,
            type => Assert.Equal("Compiler.PossibleType", type.Namespace));
        Assert.Contains(
            analysisTypes,
            type => type.Name == "PossibleTypeCallGraph");
    }

    [Test]
    public void AwaitableResultAnalysisFollowsReturnedValuesAndPreservesNestedGenericTypes()
    {
        var method = typeof(PossibleTypeAnalysisTests).GetMethod(
            nameof(CreateNestedAwaitableResult),
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var decoder = new MethodDecoder();
        decoder.LoadMethod(method, validateRuntimeSemantics: false);

        var resultTypes = PossibleTypeAnalysis.FindAwaitableResultTypes(decoder);

        Assert.Equal(
            [
                typeof(NestedAwaitableResult<Dictionary<string, List<int>>>),
                typeof(NestedAwaitableResult<List<string>>),
            ],
            resultTypes.OrderBy(type => type.ToString(), StringComparer.Ordinal).ToArray());
        Assert.DoesNotContain(typeof(UnreturnedAwaitableResult), resultTypes);
    }

    [Test]
    public void VirtualDispatchUsesTheConcreteTypeReturnedByTheFactory()
    {
        var projectDirectory = Path.GetDirectoryName(GetSourcePath())!;
        var assemblyPath = Path.Combine(
            projectDirectory,
            "Fixtures",
            "PossibleTypes",
            "bin",
            "Debug",
            "net8.0",
            "PossibleTypes.dll");

        var callGraph = Reader.GetCallGraph(assemblyPath);
        var methods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "Speak")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();

        Assert.Contains("Cat", methods);
        Assert.Contains("Dog", methods);
        Assert.DoesNotContain("Fox", methods);
        Assert.DoesNotContain("Speaker", methods);

        var delegateFactoryMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "DelegateFactoryOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", delegateFactoryMethods);
        Assert.DoesNotContain("Fox", delegateFactoryMethods);
        Assert.DoesNotContain("Speaker", delegateFactoryMethods);

        var parameterMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "ParameterOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", parameterMethods);
        Assert.DoesNotContain("Cat", parameterMethods);
        Assert.DoesNotContain("Fox", parameterMethods);
        Assert.DoesNotContain("Speaker", parameterMethods);

        var fieldMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "FieldOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", fieldMethods);
        Assert.DoesNotContain("Cat", fieldMethods);
        Assert.DoesNotContain("Fox", fieldMethods);
        Assert.DoesNotContain("Speaker", fieldMethods);

        var arrayMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "ArrayOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", arrayMethods);
        Assert.DoesNotContain("Cat", arrayMethods);
        Assert.DoesNotContain("Fox", arrayMethods);
        Assert.DoesNotContain("Speaker", arrayMethods);

        var allocationSiteFieldMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "FieldOnly"
                && method.DeclaringType!.Name.StartsWith("Allocation", StringComparison.Ordinal))
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("AllocationDog", allocationSiteFieldMethods);
        Assert.DoesNotContain("AllocationFox", allocationSiteFieldMethods);
        Assert.DoesNotContain("AllocationSpeaker", allocationSiteFieldMethods);

        var allocationSiteArrayMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "ArrayOnly"
                && method.DeclaringType!.Name.StartsWith("Allocation", StringComparison.Ordinal))
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("AllocationDog", allocationSiteArrayMethods);
        Assert.DoesNotContain("AllocationFox", allocationSiteArrayMethods);
        Assert.DoesNotContain("AllocationSpeaker", allocationSiteArrayMethods);

        var mergedFieldMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "MergedField")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("AllocationDog", mergedFieldMethods);
        Assert.Contains("AllocationFox", mergedFieldMethods);
        Assert.DoesNotContain("AllocationSpeaker", mergedFieldMethods);

        var mergedArrayMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "MergedArray")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("AllocationDog", mergedArrayMethods);
        Assert.Contains("AllocationFox", mergedArrayMethods);
        Assert.DoesNotContain("AllocationSpeaker", mergedArrayMethods);

        var interfaceMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "Work")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("WorkingDog", interfaceMethods);
        Assert.DoesNotContain("WorkingFox", interfaceMethods);
        Assert.DoesNotContain("IWorker", interfaceMethods);

        var closureMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "ClosureOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", closureMethods);
        Assert.DoesNotContain("Cat", closureMethods);
        Assert.DoesNotContain("Fox", closureMethods);
        Assert.DoesNotContain("Speaker", closureMethods);

        var asyncMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.Name == "AsyncOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", asyncMethods);
        Assert.DoesNotContain("Cat", asyncMethods);
        Assert.DoesNotContain("Fox", asyncMethods);
        Assert.DoesNotContain("Speaker", asyncMethods);

        var reflectedTypes = callGraph.ReflectedTypes.Select(type => type.Name).ToArray();
        Assert.Contains("ReflectedModel", reflectedTypes);
        Assert.Contains("RuntimeReflectedModel", reflectedTypes);
        Assert.Contains("ClosureReflectedModel", reflectedTypes);
        Assert.Contains("AsyncReflectedModel", reflectedTypes);
        Assert.Contains("HandlerReflectedModel", reflectedTypes);
        Assert.DoesNotContain("RuntimeInspectedOnlyModel", reflectedTypes);
        Assert.DoesNotContain("UnrelatedTypeToken", reflectedTypes);

        var reflectedAccessorBodies = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .Where(method => method.DeclaringType?.Name is "ReflectedModel" or "RuntimeReflectedModel")
            .Where(method => method.Name.StartsWith("get_", StringComparison.Ordinal)
                || method.Name.StartsWith("set_", StringComparison.Ordinal))
            .ToArray();
        Assert.Empty(reflectedAccessorBodies);

        var rootedMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .ToArray();
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "ReadReflectedModel" && method.Name == "get_Value");
        Assert.DoesNotContain(rootedMethods, method =>
            method.DeclaringType?.Name == "ReadReflectedModel" && method.Name == "set_Value");
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "WriteReflectedModel" && method.Name == "set_Value");
        Assert.DoesNotContain(rootedMethods, method =>
            method.DeclaringType?.Name == "WriteReflectedModel" && method.Name == "get_Value");
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "InvokedReflectedModel" && method.Name == "Run");
        Assert.DoesNotContain(rootedMethods, method =>
            method.DeclaringType?.Name == "UninvokedReflectedModel" && method.Name == "Run");
        Assert.Contains("InvalidOperationException", reflectedTypes);
        Assert.DoesNotContain("ArgumentException", reflectedTypes);

        var exceptionMethods = rootedMethods
            .Where(method => method.Name == "ExceptionOnly")
            .Select(method => method.DeclaringType!.Name)
            .ToArray();
        Assert.Contains("Dog", exceptionMethods);
        Assert.DoesNotContain("Fox", exceptionMethods);
        Assert.DoesNotContain("Speaker", exceptionMethods);
        Assert.Contains("DivideByZeroException", reflectedTypes);
        Assert.Contains(rootedMethods, method => method.Name == "DivideByZeroOnly");
        Assert.DoesNotContain(rootedMethods, method => method.Name == "InvalidCastOnly");
        Assert.DoesNotContain(rootedMethods, method => method.Name == "NullOnly");
    }

    [Test]
    public void RuntimeOverrideReturnsPreserveSuccessorsAndConcreteRuntimeTypes()
    {
        var projectDirectory = Path.GetDirectoryName(GetSourcePath())!;
        var assemblyPath = Path.Combine(
            projectDirectory,
            "Fixtures",
            "RuntimeOverrideReturns",
            "Entry",
            "bin",
            "Debug",
            "net8.0",
            "RuntimeOverrideReturns.Entry.dll");

        var callGraph = Reader.GetCallGraph(assemblyPath);
        var methods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .ToArray();

        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "AfterContractOverride"
                && method.IsConstructor);
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "AfterParentOverride"
                && method.IsConstructor);
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "ReturnContractImplementation"
                && method.Name == "Describe");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "ChildReturn"
                && method.Name == "Describe");
        Assert.DoesNotContain(methods, method =>
            method.DeclaringType?.Name == "ParentReturn"
                && method.Name == "Describe");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "ManagedPayload"
                && method.Name == "get_Value");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "FirstOutContract"
                && method.Name == "Describe");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "SecondOutContract"
                && method.Name == "Describe");
        Assert.DoesNotContain(methods, method =>
            method.DeclaringType?.Name == "ReplacedOutContract"
                && method.Name == "Describe");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "IncomingRefContract"
                && method.Name == "Describe");
        Assert.Contains(methods, method =>
            method.DeclaringType?.Name == "WrittenRefContract"
                && method.Name == "Describe");

        var types = callGraph.AllReferencedFields.Keys
            .Select(type => type.Name)
            .ToArray();
        Assert.Contains("IReturnContract", types);
        Assert.Contains("ReturnContractImplementation", types);
        Assert.Contains("ParentReturn", types);
        Assert.Contains("ChildReturn", types);
    }

    [Test]
    public void ManagedTypeAccessRetainsOnlyDirectApplicationValueTypeConstructors()
    {
        var projectDirectory = Path.GetDirectoryName(GetSourcePath())!;
        var assemblyPath = Path.Combine(
            projectDirectory,
            "Fixtures",
            "RuntimeOverrideReturns",
            "Entry",
            "bin",
            "Debug",
            "net8.0",
            "RuntimeOverrideReturns.Entry.dll");

        var callGraph = Reader.GetCallGraph(assemblyPath);
        var constructors = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .OfType<ConstructorInfo>()
            .ToArray();
        var valueConstructors = constructors
            .Where(constructor => constructor.DeclaringType?.Name == "ExplicitConstructorValue")
            .OrderBy(constructor => constructor.GetParameters().Length)
            .ToArray();

        Assert.Equal(2, valueConstructors.Length);
        Assert.Empty(valueConstructors[0].GetParameters());
        Assert.True(valueConstructors[0].IsPublic);
        var parameter = Assert.Single(valueConstructors[1].GetParameters());
        Assert.True(valueConstructors[1].IsPrivate);
        Assert.Equal("value", parameter.Name);
        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(17, parameter.DefaultValue);
        var fixtureAttribute = Assert.Single(valueConstructors[1].CustomAttributes, attribute =>
            attribute.AttributeType.Name == "FixtureConstructorAttribute");
        Assert.Collection(
            fixtureAttribute.ConstructorArguments,
            argument =>
            {
                Assert.Equal(typeof(ulong), argument.ArgumentType);
                Assert.Equal(ulong.MaxValue, Assert.IsType<ulong>(argument.Value));
            },
            argument =>
            {
                Assert.Equal(typeof(long), argument.ArgumentType);
                Assert.Equal(long.MinValue, Assert.IsType<long>(argument.Value));
            });

        Assert.DoesNotContain(constructors, constructor =>
            constructor.DeclaringType == typeof(int)
                || constructor.DeclaringType?.Name == "FixtureEnum"
                || constructor.DeclaringType?.IsGenericType == true
                    && constructor.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>)
                || constructor.DeclaringType?.FullName?.StartsWith(
                    "System.ValueTuple`",
                    StringComparison.Ordinal) == true
                || constructor.DeclaringType == typeof(DateTime));

        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        try
        {
            MsilBinaryPackage.Write(firstPath, callGraph);
            MsilBinaryPackage.Write(secondPath, callGraph);
            Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));

            var metadata = MsilBinaryPackage.ReadTypeMetadata(
                firstPath,
                "ExplicitConstructorValue");
            var constructorMetadata = Assert.Single(
                Assert.IsType<JArray>(metadata["constructors"]),
                constructor => constructor!.Value<bool>("isPublic") == false);
            var encodedAttribute = Assert.Single(
                Assert.IsType<JArray>(constructorMetadata!["attributes"]),
                attribute => attribute!.Value<string>("typeName")
                    == "FixtureConstructorAttribute");
            var encodedArguments = Assert.IsType<JArray>(
                encodedAttribute!["constructorArguments"]);
            Assert.Equal(
                ulong.MaxValue,
                Assert.IsType<ulong>(Assert.IsType<JValue>(encodedArguments[0]).Value));
            Assert.Equal(
                long.MinValue,
                Assert.IsType<long>(Assert.IsType<JValue>(encodedArguments[1]).Value));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Test]
    public void ManagedTypeAccessExclusionsAreProviderOwnedAndDoNotChangePocoBinding()
    {
        var projectDirectory = Path.GetDirectoryName(GetSourcePath())!;
        var fixtureDirectory = Path.Combine(
            projectDirectory,
            "Fixtures",
            "RuntimeOverrideReturns");
        var excludedAssemblyPath = Path.Combine(
            fixtureDirectory,
            "Entry",
            "bin",
            "Debug",
            "net8.0",
            "RuntimeOverrideReturns.Entry.dll");
        var explicitAssemblyPath = Path.Combine(
            fixtureDirectory,
            "ExplicitStringEntry",
            "bin",
            "Debug",
            "net8.0",
            "RuntimeOverrideReturns.ExplicitStringEntry.dll");

        var excludedMethods = Reader.GetCallGraph(excludedAssemblyPath).MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .ToArray();
        var excludedConstructors = excludedMethods
            .OfType<ConstructorInfo>()
            .ToArray();

        Assert.DoesNotContain(excludedMethods, method =>
            method.DeclaringType == typeof(string));
        Assert.Contains(excludedConstructors, constructor =>
            constructor.DeclaringType?.Name == "ExplicitConstructorValue"
                && constructor.GetParameters().Length == 1
                && constructor.IsPrivate);

        var companionPath = Path.Combine(
            fixtureDirectory,
            "Companion",
            "bin",
            "Debug",
            "net8.0",
            "RuntimeOverrideReturns.Contracts.fecs.dll");
        var explicitAccess = Assert.Single(
            RuntimeOverrideAssemblyCatalog.Load(companionPath).Overrides[
                "RuntimeOverrideReturns.ReturnFactory.InspectString<T>()"]
                .ManagedGenericTypeAccesses);
        Assert.Equal(ManagedTypeAccess.InstanceConstructors, explicitAccess.Access);
        Assert.Empty(explicitAccess.ExcludedTypes);
        var terminalParameterAccess = Assert.Single(
            RuntimeOverrideAssemblyCatalog.Load(companionPath).Overrides[
                "RuntimeOverrideReturns.ReturnFactory.Consume(System.Object)"]
                .ManagedParameterTypeAccesses);
        Assert.Equal(
            ["System.String"],
            terminalParameterAccess.ExcludedTypes.Select(type => type.TypeName).ToArray());

        // System.String constructors are mandatory runtime intrinsics rather than
        // ordinary IL methods. Reaching that diagnostic proves this independent
        // provider request was honored instead of being suppressed globally.
        var exception = Assert.Throws<Exception>(() =>
            Reader.GetCallGraph(explicitAssemblyPath));
        Assert.Equal("Missing intrinsics in compilation process", exception.Message);
    }

    [Test]
    public void ManagedTypeAccessRetainsPublicAndNonPublicInstanceConstructors()
    {
        var rootedMethods = FocusedJsonCallGraph.Value.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .ToArray();

        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "SystemTextJsonParameterizedPayload"
                && method.IsConstructor
                && method.GetParameters().Length == 1
                && method.IsPublic);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "SystemTextJsonAttributedPrivatePayload"
                && method.IsConstructor
                && method.GetParameters().Length == 1
                && method.IsPrivate);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "NewtonsoftPrivateDefaultPayload"
                && method.IsConstructor
                && method.GetParameters().Length == 0
                && method.IsPrivate);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "NewtonsoftAttributedPrivatePayload"
                && method.IsConstructor
                && method.GetParameters().Length == 1
                && method.IsPrivate);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.IsGenericType == true
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>)
                && method.IsConstructor
                && method.GetParameters().Length == 0);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.IsGenericType == true
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && method.IsConstructor
                && method.GetParameters().Length == 0);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.IsGenericType == true
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && method.Name.EndsWith("Add", StringComparison.Ordinal)
                && method.GetParameters().Length == 2
                && method.GetParameters().All(parameter => parameter.ParameterType.IsGenericParameter));
    }

    [Test]
    public void ManagedTypeAccessRecursesIntoPocoPropertyAndFieldTypes()
    {
        var rootedConstructors = FocusedJsonCallGraph.Value.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .OfType<ConstructorInfo>()
            .ToArray();

        Assert.Contains(rootedConstructors, constructor =>
            constructor.DeclaringType?.Name == "SystemTextJsonNestedPropertyPayload"
                && constructor.IsPublic
                && constructor.GetParameters().Length == 0);
        Assert.Contains(rootedConstructors, constructor =>
            constructor.DeclaringType?.Name == "SystemTextJsonNestedFieldPayload"
                && constructor.IsPublic
                && constructor.GetParameters().Length == 0);
    }

    [Test]
    public void ManagedTypeAccessRetainsRequestedNonPublicPropertiesAndFields()
    {
        var callGraph = FocusedJsonCallGraph.Value;
        var rootedMethods = callGraph.MethodDecoders
            .Select(decoder => decoder.MethodBase)
            .ToArray();
        var rootedFields = callGraph.AllReferencedFields
            .SelectMany(item => item.Value)
            .ToArray();

        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "SystemTextJsonNonPublicIncludePayload"
                && method.Name == "set_PrivateProperty"
                && method.IsPrivate);
        Assert.Contains(rootedMethods, method =>
            method.DeclaringType?.Name == "NewtonsoftNonPublicMemberPayload"
                && method.Name == "set_PrivateProperty"
                && method.IsPrivate);
        Assert.DoesNotContain(rootedMethods, method =>
            method.DeclaringType?.Name == "SystemTextJsonNonPublicIncludePayload"
                && method.Name.Contains("UnattributedProperty", StringComparison.Ordinal));
        Assert.DoesNotContain(rootedMethods, method =>
            method.DeclaringType?.Name == "NewtonsoftNonPublicMemberPayload"
                && method.Name.Contains("UnattributedProperty", StringComparison.Ordinal));
        Assert.Contains(rootedFields, field =>
            field.DeclaringType?.Name == "SystemTextJsonNonPublicIncludePayload"
                && field.Name == "privateField"
                && field.IsPrivate);
        Assert.Contains(rootedFields, field =>
            field.DeclaringType?.Name == "NewtonsoftNonPublicMemberPayload"
                && field.Name == "privateField"
                && field.IsPrivate);
        Assert.DoesNotContain(rootedFields, field =>
            field.DeclaringType?.Name == "SystemTextJsonNonPublicIncludePayload"
                && field.Name == "unattributedField");
        Assert.DoesNotContain(rootedFields, field =>
            field.DeclaringType?.Name == "NewtonsoftNonPublicMemberPayload"
                && field.Name == "unattributedField");
    }

    [Test]
    public void FieldReflectionRootsOnlyTypesProvenAtGetFieldsCalls()
    {
        var callGraph = FocusedJsonCallGraph.Value;

        var reflectedFieldType = Assert.Single(callGraph.FieldReflectedTypes);
        Assert.Equal("FieldIdentityDerived`1", reflectedFieldType.Name);
        Assert.DoesNotContain(callGraph.FieldReflectedTypes, type =>
            type.Namespace == "System.Collections.Generic");
    }

    [Test]
    public void ManagedTypeAccessRetainsOnlyRequestedEnumMetadata()
    {
        var callGraph = FocusedJsonCallGraph.Value;
        var enumTypes = callGraph.EnumMetadataTypes
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "JsonByteEnum",
                "JsonFlagsEnum",
                "JsonInt16Enum",
                "JsonInt32Enum",
                "JsonInt64Enum",
                "JsonNamedEnum",
                "JsonSByteEnum",
                "JsonUInt16Enum",
                "JsonUInt32Enum",
                "JsonUInt64Enum",
            ],
            enumTypes);
        Assert.DoesNotContain("UnusedJsonEnum", enumTypes);
        Assert.All(callGraph.EnumMetadataTypes, type =>
        {
            Assert.True(type.IsEnum);
            Assert.All(
                callGraph.AllReferencedFields[type],
                field => Assert.Equal("value__", field.Name));
            Assert.DoesNotContain(
                callGraph.MethodDecoders,
                decoder => decoder.MethodBase.DeclaringType == type.AsType());
        });
    }

    [Test]
    public void EnumMetadataPreservesFlagsAliasesOrderAndUnsignedValues()
    {
        var metadata = MsilBinaryPackage.CreateEnumMetadata(
            typeof(EnumMetadataFixture).GetTypeInfo());

        Assert.Equal("System.UInt64", metadata.Value<string>("enumUnderlyingType"));
        Assert.True(metadata.Value<bool>("enumIsFlags"));
        var values = metadata["enumValues"]!.Children().OfType<JObject>().ToArray();
        Assert.Equal(["None", "High", "HighAlias", "Maximum"],
            values.Select(value => value.Value<string>("name")!).ToArray());
        Assert.Equal(0UL, Assert.IsType<ulong>(
            Assert.IsType<JValue>(values[0]["value"]).Value));
        Assert.Equal(
            9223372036854775808UL,
            Assert.IsType<ulong>(Assert.IsType<JValue>(values[1]["value"]).Value));
        Assert.Equal(
            9223372036854775808UL,
            Assert.IsType<ulong>(Assert.IsType<JValue>(values[2]["value"]).Value));
        Assert.Equal(
            ulong.MaxValue,
            Assert.IsType<ulong>(Assert.IsType<JValue>(values[3]["value"]).Value));
    }

    private static CallGraphInfo CreateFocusedJsonCallGraph()
    {
        var projectDirectory = Path.GetDirectoryName(GetSourcePath())!;
        var assemblyPath = Path.Combine(
            projectDirectory,
            "Fixtures",
            "JsonReachability",
            "bin",
            "Debug",
            "net8.0",
            "JsonReachability.dll");

        return Reader.GetCallGraph(assemblyPath);
    }

    [Flags]
    private enum EnumMetadataFixture : ulong
    {
        None = 0,
        High = 9223372036854775808UL,
        HighAlias = High,
        Maximum = ulong.MaxValue,
    }

    private static Task<AwaitableResult> CreateNestedAwaitableResult(bool useList)
    {
        _ = new UnreturnedAwaitableResult();
        AwaitableResult result = useList
            ? new NestedAwaitableResult<List<string>>()
            : new NestedAwaitableResult<Dictionary<string, List<int>>>();
        return Task.FromResult(result);
    }

    private abstract class AwaitableResult;

    private sealed class NestedAwaitableResult<T> : AwaitableResult;

    private sealed class UnreturnedAwaitableResult : AwaitableResult;

    private static string GetSourcePath([CallerFilePath] string callerFilePath = "") => callerFilePath;
}
