using Compiler.Helpers;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using RuntimeOverrideReturns;

namespace Compiler.Tests;

public sealed class RuntimeOverrideAssemblyCatalogTests
{
    [Test]
    public void ExtractsRuntimeAssetsUnderTheirCompanionOwners()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "RuntimeOverrideReturns.Contracts.fecs.dll");
        var companionAssemblyName = AssemblyName.GetAssemblyName(companionPath).Name!;
        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);
        Assert.NotEmpty(catalog.RuntimeAssets);
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-RuntimeAssets-" + Guid.NewGuid().ToString("N"));
        try
        {
            RuntimeOverrideCollection.Load([companionPath])
                .ExtractRuntimeAssets(
                    outputDirectory,
                    [typeof(ReturnFactory).GetMethod(nameof(ReturnFactory.CreateContract))!]);

            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*", SearchOption.TopDirectoryOnly));
            foreach (var asset in catalog.RuntimeAssets)
            {
                var extractedPath = Path.Combine(
                    outputDirectory,
                    companionAssemblyName,
                    asset.Key);
                Assert.Equal(asset.Value, File.ReadAllText(extractedPath));
            }

            Directory.Delete(outputDirectory, recursive: true);
            RuntimeOverrideCollection.Load([companionPath])
                .ExtractRuntimeAssets(
                    outputDirectory,
                    [typeof(ReturnFactory).GetMethod(nameof(ReturnFactory.CreateParent))!]);
            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void RejectsMalformedRuntimeAssets()
    {
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                "Expected.fecs.dll",
                "Expected.fecs",
                new Dictionary<string, byte[]>
                {
                    ["Other.fecs.RuntimeAssets.asset.js"] = Encoding.UTF8.GetBytes("export {};")
                }));
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                "Malformed.fecs.dll",
                "Malformed.fecs",
                new Dictionary<string, byte[]>
                {
                    ["Malformed.fecs.RuntimeAssets.Not-Lowercase.js"] = Encoding.UTF8.GetBytes("export {};")
                }));
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                "Malformed.fecs.dll",
                "Malformed.fecs",
                new Dictionary<string, byte[]>
                {
                    ["Malformed.fecs.RuntimeAssets.empty.js"] = Encoding.UTF8.GetBytes(" \r\n")
                }));
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                "Malformed.fecs.dll",
                "Malformed.fecs",
                new Dictionary<string, byte[]>
                {
                    ["Malformed.fecs.RuntimeAssets.invalid.js"] = [0xff]
                }));
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideResourceCatalogReader.ReadRuntimeAssets(
                "Malformed.fecs.dll",
                "Malformed.fecs",
                new Dictionary<string, byte[]>
                {
                    ["Malformed.fecs.RuntimeAssets.nested/asset.js"] = Encoding.UTF8.GetBytes("export {};")
                }));
    }

    [Test]
    public void LoadsManagedGenericTypeAccessFromSystemTextJsonCompanion()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Text.Json.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.Equal("System.Text.Json", catalog.TargetAssemblyName);
        var deserialize = catalog.Overrides[
            "System.Text.Json.JsonSerializer.Deserialize<TValue>(System.String, System.Text.Json.JsonSerializerOptions)"];
        var deserializeAccess = Assert.Single(deserialize.ManagedGenericTypeAccesses);
        var deserializeReturn = Assert.Single(deserialize.ManagedReturnTypePatterns);
        Assert.Equal(0, deserializeReturn.GenericParameterIndex);
        Assert.Equal(0, deserializeAccess.GenericParameterIndex);
        Assert.Equal(
            ManagedTypeAccess.InstanceConstructors
                | ManagedTypeAccess.EnumMetadata
                | ManagedTypeAccess.WritePublicProperties
                | ManagedTypeAccess.WritePublicFields
                | ManagedTypeAccess.WriteNonPublicProperties
                | ManagedTypeAccess.WriteNonPublicFields,
            deserializeAccess.Access);
        Assert.Equal(
            "System.Text.Json.Serialization.JsonIncludeAttribute",
            deserializeAccess.NonPublicMemberAttributeTypeName);
        Assert.Equal(
            ["System.String"],
            deserializeAccess.ExcludedTypes.Select(type => type.TypeName).ToArray());

        var serialize = catalog.Overrides[
            "System.Text.Json.JsonSerializer.Serialize<TValue>(TValue, System.Text.Json.JsonSerializerOptions)"];
        var serializeAccess = Assert.Single(serialize.ManagedGenericTypeAccesses);
        Assert.Equal(0, serializeAccess.GenericParameterIndex);
        Assert.Equal(
            ManagedTypeAccess.ReadPublicProperties
                | ManagedTypeAccess.ReadPublicFields
                | ManagedTypeAccess.EnumMetadata
                | ManagedTypeAccess.ReadNonPublicProperties
                | ManagedTypeAccess.ReadNonPublicFields,
            serializeAccess.Access);
        Assert.Equal(
            "System.Text.Json.Serialization.JsonIncludeAttribute",
            serializeAccess.NonPublicMemberAttributeTypeName);
        Assert.Equal(
            ["System.String"],
            serializeAccess.ExcludedTypes.Select(type => type.TypeName).ToArray());

        var converters = catalog.Overrides[
            "System.Text.Json.JsonSerializerOptions.get_Converters()"];
        var returnType = Assert.Single(converters.ManagedReturnTypePatterns);
        Assert.Equal("System.Private.CoreLib", returnType.AssemblyName);
        Assert.Equal("System.Collections.Generic.List`1", returnType.TypeName);
    }

    [Test]
    public void LoadsManagedTypeAccessFromNewtonsoftJsonCompanion()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.Newtonsoft.Json.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.Equal("Newtonsoft.Json", catalog.TargetAssemblyName);
        var deserialize = catalog.Overrides[
            "Newtonsoft.Json.JsonConvert.DeserializeObject<T>(System.String, Newtonsoft.Json.JsonSerializerSettings)"];
        var genericAccess = Assert.Single(deserialize.ManagedGenericTypeAccesses);
        Assert.Equal(
            ManagedTypeAccess.InstanceConstructors
                | ManagedTypeAccess.EnumMetadata
                | ManagedTypeAccess.WritePublicProperties
                | ManagedTypeAccess.WritePublicFields
                | ManagedTypeAccess.WriteNonPublicProperties
                | ManagedTypeAccess.WriteNonPublicFields,
            genericAccess.Access);
        Assert.Equal(
            "Newtonsoft.Json.JsonPropertyAttribute",
            genericAccess.NonPublicMemberAttributeTypeName);
        Assert.Equal(
            ["System.String"],
            genericAccess.ExcludedTypes.Select(type => type.TypeName).ToArray());
        var directDeserialize = catalog.Overrides[
            "Newtonsoft.Json.JsonConvert.DeserializeObject<T>(System.String)"];
        Assert.Equal(genericAccess.Access, Assert.Single(directDeserialize.ManagedGenericTypeAccesses).Access);

        var serialize = catalog.Overrides[
            "Newtonsoft.Json.JsonConvert.SerializeObject(System.Object, Newtonsoft.Json.JsonSerializerSettings)"];
        var parameterAccess = Assert.Single(serialize.ManagedParameterTypeAccesses);
        Assert.Equal(0, parameterAccess.ParameterIndex);
        Assert.Equal(
            ManagedTypeAccess.ReadPublicProperties
                | ManagedTypeAccess.EnumMetadata
                | ManagedTypeAccess.ReadPublicFields
                | ManagedTypeAccess.ReadNonPublicProperties
                | ManagedTypeAccess.ReadNonPublicFields,
            parameterAccess.Access);
        Assert.Equal(
            "Newtonsoft.Json.JsonPropertyAttribute",
            parameterAccess.NonPublicMemberAttributeTypeName);
        Assert.Equal(
            ["System.String"],
            parameterAccess.ExcludedTypes.Select(type => type.TypeName).ToArray());
        var directSerialize = catalog.Overrides[
            "Newtonsoft.Json.JsonConvert.SerializeObject(System.Object)"];
        Assert.Equal(parameterAccess.Access, Assert.Single(directSerialize.ManagedParameterTypeAccesses).Access);
    }

    [Test]
    public void LoadsTypedBrowserInteropCompanion()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.Interop.JS.Browser.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.Equal("FrontEndCSharp.Overrides.Interop.JS.Browser", catalog.TargetAssemblyName);
        Assert.Equal(66, catalog.Overrides.Count);
        Assert.Contains(
            "Interop.JS.Browser.Document.get_Body()",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Window.get_InnerWidth()",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserCredentials(System.Net.Http.HttpRequestMessage, Interop.JS.Browser.BrowserRequestCredentials)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.BrowserHttpRequestOptions.SetBrowserIntegrity(System.Net.Http.HttpRequestMessage, System.String)",
            catalog.Overrides.Keys);
        Assert.DoesNotContain(
            catalog.Overrides.Keys,
            signature => signature.StartsWith("Interop.JS.Browser.Web.", StringComparison.Ordinal)
                || signature.EndsWith(".get_Current()", StringComparison.Ordinal)
                || string.Equals(
                    signature,
                    "Interop.JS.Browser.Window.get_Document()",
                    StringComparison.Ordinal));
        Assert.Contains(
            "Interop.JS.Browser.Document.GetElementById(System.String)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Document.QuerySelectorAll(System.String)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Element.AddEventListener(System.String, System.Action`1[T])",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.DomEvent.get_CurrentTarget()",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Element.get_Value()",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Node.InsertBefore(Interop.JS.Browser.Node, Interop.JS.Browser.Node)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "Interop.JS.Browser.Window.QueueMicrotask(System.Action)",
            catalog.Overrides.Keys);
    }

    [Test]
    public void LoadsReactCompanionAndItsPackageRequirements()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.Interop.JS.React.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.Equal("FrontEndCSharp.Overrides.Interop.JS.React", catalog.TargetAssemblyName);
        Assert.Equal(11, catalog.Overrides.Count);
        Assert.Contains(
            "Interop.JS.React.React.CreateElement(Interop.JS.React.ImportedReactComponent, Interop.JS.React.ReactProps, Interop.JS.React.ReactNode[])",
            catalog.Overrides.Keys);
        Assert.Collection(
            catalog.JavaScriptPackages.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            pair =>
            {
                Assert.Equal("react", pair.Key);
                Assert.Equal("19.3.1", pair.Value.VersionRange);
                Assert.Equal(
                    FrontEndCSharp.RuntimeOverrides.JavaScriptPackageDependencyKind.Dependency,
                    pair.Value.Kind);
            },
            pair =>
            {
                Assert.Equal("react-dom", pair.Key);
                Assert.Equal("19.3.1", pair.Value.VersionRange);
                Assert.Equal(
                    FrontEndCSharp.RuntimeOverrides.JavaScriptPackageDependencyKind.Dependency,
                    pair.Value.Kind);
            });
        Assert.NotEmpty(catalog.JavaScriptPackageArchive);

        var extractionDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-ReactPackages-" + Guid.NewGuid().ToString("N"));
        try
        {
            RuntimeOverrideCollection.Load([companionPath])
                .ExtractJavaScriptPackages(extractionDirectory);
            var nodeModules = Path.Combine(extractionDirectory, "node_modules");
            Assert.True(Directory.Exists(Path.Combine(nodeModules, "react")));
            Assert.True(Directory.Exists(Path.Combine(nodeModules, "react-dom")));
            Assert.False(Directory.Exists(Path.Combine(nodeModules, "mobx")));
            Assert.False(Directory.Exists(Path.Combine(nodeModules, "motion")));
        }
        finally
        {
            if (Directory.Exists(extractionDirectory))
            {
                Directory.Delete(extractionDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void CombinesCompatibleJavaScriptPackageRequirementsAcrossSelectedAdapters()
    {
        var packages = new Dictionary<string, JavaScriptPackageDependency>(StringComparer.Ordinal);
        RuntimeOverrideCollection.MergeJavaScriptPackages(packages,
        [
            Package("react", "19.3.1"),
            Package("react-dom", "19.3.1"),
            Package("mobx", "7.0.6"),
        ]);
        RuntimeOverrideCollection.MergeJavaScriptPackages(packages,
        [
            Package("mobx-react-lite", "5.1.0"),
            Package("mobx", "7.0.6", JavaScriptPackageDependencyKind.PeerDependency),
            Package("react", "19.3.1", JavaScriptPackageDependencyKind.PeerDependency),
        ]);
        RuntimeOverrideCollection.MergeJavaScriptPackages(packages,
        [
            Package("motion", "14.4.0"),
            Package("react", "19.3.1", JavaScriptPackageDependencyKind.PeerDependency),
        ]);

        Assert.Equal(
            new[] { "mobx", "mobx-react-lite", "motion", "react", "react-dom" },
            packages.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("7.0.6", packages["mobx"].VersionRange);
        Assert.Equal(JavaScriptPackageDependencyKind.Dependency, packages["mobx"].Kind);
        Assert.Equal("5.1.0", packages["mobx-react-lite"].VersionRange);
        Assert.Equal("14.4.0", packages["motion"].VersionRange);
        Assert.Equal("19.3.1", packages["react"].VersionRange);
        Assert.Throws<InvalidDataException>(() =>
            RuntimeOverrideCollection.MergeJavaScriptPackages(
                packages,
                [Package("react", "20.0.0")]));

        static KeyValuePair<string, JavaScriptPackageDependency> Package(
            string name,
            string version,
            JavaScriptPackageDependencyKind kind = JavaScriptPackageDependencyKind.Dependency) =>
            new(name, new JavaScriptPackageDependency
            {
                PackageName = name,
                VersionRange = version,
                Kind = kind,
            });
    }

    [Test]
    public void LoadsTypedOverrideAndEmbeddedJavaScriptWithoutExecutingProvider()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Console.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.DoesNotContain(
            AppDomain.CurrentDomain.GetAssemblies(),
            assembly => assembly.GetName().Name == "FrontEndCSharp.Overrides.System.Console.fecs"
                && AssemblyLoadContext.GetLoadContext(assembly) == AssemblyLoadContext.Default);
        Assert.Equal("System.Console", catalog.TargetAssemblyName);
        Assert.Equal(10, catalog.Overrides.Count);
        var runtimeOverride = catalog.Overrides["System.Console.WriteLine(System.String)"];
        Assert.Equal("System.Console.WriteLine(System.String)", runtimeOverride.Signature);
        Assert.Contains("console.log(unwrap(str));", runtimeOverride.JavaScript);
        Assert.Empty(runtimeOverride.Dependencies);

        var migratedMethods = typeof(Console)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => catalog.Overrides.ContainsKey(
                NameNormalizers.GetFullyQualifiedMethodName(method)))
            .ToList();
        Assert.Equal(10, migratedMethods.Count);
        var overrides = RuntimeOverrideCollection.Load([companionPath]);
        Assert.All(migratedMethods, method => Assert.True(overrides.HasRuntimeOverride(method)));
    }

    [Test]
    public void LoadsOverridesFromMultipleProductionCompanions()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll");

        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        Assert.Equal("System.Private.CoreLib", catalog.TargetAssemblyName);
        Assert.Equal(635, catalog.Overrides.Count);
        Assert.Contains(
            "System.String.Equals(System.String, System.String)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "System.IO.MemoryStream.Read(System.Byte[], System.Int32, System.Int32)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "System.IO.MemoryStream.ReadAsync(System.Byte[], System.Int32, System.Int32, System.Threading.CancellationToken)",
            catalog.Overrides.Keys);
        Assert.Contains(
            "System.IO.Stream.WriteAsync(System.Byte[], System.Int32, System.Int32, System.Threading.CancellationToken)",
            catalog.Overrides.Keys);
        var runtimeOverride = catalog.Overrides["System.Char.IsWhiteSpace(System.Char)"];
        Assert.Equal("System.Char.IsWhiteSpace(System.Char)", runtimeOverride.Signature);
        Assert.Contains("function isWhiteSpace(charCode)", runtimeOverride.JavaScript);

        Assert.All(
            catalog.Overrides.Values,
            runtimeOverride => Assert.DoesNotContain(
                "import \"./method.",
                runtimeOverride.JavaScript,
                StringComparison.Ordinal));

        var isWhiteSpace = typeof(char).GetMethod(
            nameof(char.IsWhiteSpace),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(char)],
            modifiers: null);
        Assert.NotNull(isWhiteSpace);
        var overrides = RuntimeOverrideCollection.Load([companionPath]);
        Assert.True(overrides.HasRuntimeOverride(isWhiteSpace));
        var staticStringEquals = typeof(string).GetMethod(
            nameof(string.Equals),
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(string), typeof(string)],
            modifiers: null);
        Assert.NotNull(staticStringEquals);
        Assert.True(overrides.HasRuntimeOverride(staticStringEquals));

        Assert.False(Directory.Exists(Path.Combine(
            AppContext.BaseDirectory,
            "JsRuntime",
            "runtime-overrides")));

        var nullableValue = catalog.Overrides["System.Nullable`1[T].get_Value()"];
        Assert.Equal(
            ["System.InvalidOperationException..ctor(System.String)"],
            nullableValue.Dependencies);

        var awaitOnCompleted = catalog.Overrides[
            "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)"];
        Assert.Equal(
            [
                "System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()",
                "System.Runtime.CompilerServices.INotifyCompletion.OnCompleted(System.Action)",
            ],
            awaitOnCompleted.Dependencies);

        Assert.Empty(catalog.Overrides[
            "System.Threading.Tasks.Task.FromResult<TResult>(TResult)"].Dependencies);

        var whenAll = catalog.Overrides[
            "System.Threading.Tasks.Task.WhenAll(System.Collections.Generic.IEnumerable`1[T])"];
        Assert.Equal(
            [
                "System.Collections.Generic.IEnumerable`1[T].GetEnumerator()",
                "System.Collections.Generic.IEnumerator`1[T].get_Current()",
                "System.Collections.IEnumerator.MoveNext()",
                "System.IDisposable.Dispose()",
            ],
            whenAll.Dependencies);

        Assert.Empty(Directory.GetFiles(
            Path.Combine(AppContext.BaseDirectory, "JsRuntime"),
            "*.fecs.dll"));
    }

    [Test]
    public void ResolvesManagedDependenciesFromTheCompanionsTargetAssembly()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.fecs.dll");
        var overrides = RuntimeOverrideCollection.Load([companionPath]);
        var sendCore = typeof(System.Net.Http.HttpClient)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == "<SendAsync>g__Core|83_0");

        var dependency = Assert.Single(overrides.GetDependencies(sendCore));

        Assert.Equal(typeof(System.Net.Http.HttpMessageHandler), dependency.DeclaringType);
        Assert.Equal("SendAsync", dependency.Name);
        Assert.Equal("System.Net.Http", dependency.Module.Assembly.GetName().Name);
    }

    [Test]
    public void PublicHttpClientOverloadsTerminateAtBrowserOverrides()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.fecs.dll");
        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        var getAsync = catalog.Overrides[
            "System.Net.Http.HttpClient.GetAsync(System.String)"];
        Assert.Contains(
            "System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)",
            getAsync.Dependencies);

        var sendAsync = catalog.Overrides[
            "System.Net.Http.HttpClient.SendAsync(System.Net.Http.HttpRequestMessage, System.Net.Http.HttpCompletionOption, System.Threading.CancellationToken)"];
        Assert.Contains(
            "System.Net.Http.HttpMessageHandler.SendAsync(System.Net.Http.HttpRequestMessage, System.Threading.CancellationToken)",
            sendAsync.Dependencies);
        Assert.DoesNotContain(
            sendAsync.Dependencies,
            dependency => dependency.Contains("<SendAsync>g__Core", StringComparison.Ordinal));
    }

    [Test]
    public void HeaderEnumerationDeclaresItsConcreteEnumeratorDependencies()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.fecs.dll");
        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);

        var enumeration = catalog.Overrides[
            "System.Net.Http.Headers.HttpHeaders.GetEnumerator()"];

        Assert.Contains(enumeration.Dependencies, dependency =>
            dependency.EndsWith(".MoveNext()", StringComparison.Ordinal));
        Assert.Contains(enumeration.Dependencies, dependency =>
            dependency.EndsWith(".get_Current()", StringComparison.Ordinal));
        Assert.Contains(enumeration.Dependencies, dependency =>
            dependency.EndsWith(".Dispose()", StringComparison.Ordinal));
    }

    [Test]
    public void HeaderTryGetValuesInfersItsByRefResultsFromProviderIl()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.fecs.dll");
        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);
        var entry = catalog.Overrides[
            "System.Net.Http.Headers.HttpHeaders.TryGetValues(System.String, System.Collections.Generic.IEnumerable`1[T]&)"];

        var declaredWrite = Assert.Single(entry.ManagedByRefParameterWrites);
        Assert.Equal(1, declaredWrite.ParameterIndex);
        Assert.True(declaredWrite.IsOut);
        Assert.True(declaredWrite.IncludesNull);
        var declaredArray = Assert.Single(declaredWrite.Types);
        Assert.Equal("szarray", declaredArray.ArrayKind);
        Assert.Equal(1, declaredArray.ArrayRank);
        Assert.Equal("System.String", declaredArray.ArrayElementType.TypeName);

        var method = typeof(System.Net.Http.Headers.HttpHeaders).GetMethod(
            nameof(System.Net.Http.Headers.HttpHeaders.TryGetValues),
            [typeof(string), typeof(IEnumerable<string>).MakeByRefType()])!;
        var resolvedWrite = Assert.Single(
            RuntimeOverrideCollection.Load([companionPath]).GetByRefParameterWrites(method));
        Assert.Equal(1, resolvedWrite.ParameterIndex);
        Assert.True(resolvedWrite.IsOut);
        Assert.True(resolvedWrite.IncludesNull);
        Assert.Equal(typeof(string[]), Assert.Single(resolvedWrite.Types));
    }

    [Test]
    public void StreamingHttpContentDeclaresItsManagedStreamDependencies()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.fecs.dll");
        var catalog = RuntimeOverrideAssemblyCatalog.Load(companionPath);
        var overrides = RuntimeOverrideCollection.Load([companionPath]);

        var readAsStream = catalog.Overrides[
            "System.Net.Http.HttpContent.ReadAsStreamAsync()"];

        Assert.Contains(
            "System.IO.Stream.ReadAsync(System.Byte[], System.Int32, System.Int32)",
            readAsStream.Dependencies);
        Assert.Contains(
            "System.IO.Stream.ReadAsync(System.Byte[], System.Int32, System.Int32, System.Threading.CancellationToken)",
            readAsStream.Dependencies);
        Assert.Contains(
            "System.IO.Stream.Read(System.Byte[], System.Int32, System.Int32)",
            readAsStream.Dependencies);
        Assert.Contains(
            "System.IO.Stream.Dispose()",
            readAsStream.Dependencies);

        Assert.Equal(
            ["System.IO.MemoryStream", "System.Net.Http.BrowserHttpReadStream"],
            readAsStream.ManagedAwaitableResultTypePatterns
                .Select(pattern => pattern.TypeName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        var readAsStreamMethod = typeof(System.Net.Http.HttpContent).GetMethod(
            nameof(System.Net.Http.HttpContent.ReadAsStreamAsync),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            Type.EmptyTypes,
            modifiers: null)!;
        var resultTypes = overrides.GetAwaitableResultTypePatterns(readAsStreamMethod);
        Assert.Equal(
            ["System.IO.MemoryStream", "System.Net.Http.BrowserHttpReadStream"],
            resultTypes
                .Select(type => type.FullName!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        Assert.All(resultTypes, type => Assert.True(typeof(Stream).IsAssignableFrom(type)));
    }

    [Test]
    public void ResolvesManagedDependenciesInsideTheSelectedMethodsLoadContext()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.Newtonsoft.Json.fecs.dll");
        var targetAssemblyPath = Path.Combine(
            Path.GetDirectoryName(GetSourcePath())!,
            "Fixtures",
            "SystemTextJsonBrowserFixture",
            "bin",
            "Debug",
            "net8.0",
            "Newtonsoft.Json.dll");
        var targetContext = new AssemblyLoadContext(
            "RuntimeOverrideDependencyTarget_" + Guid.NewGuid().ToString("N"),
            isCollectible: true);
        try
        {
            var targetAssembly = targetContext.LoadFromAssemblyPath(targetAssemblyPath);
            var jsonConvert = targetAssembly.GetType(
                "Newtonsoft.Json.JsonConvert",
                throwOnError: true)!;
            var deserialize = jsonConvert
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "DeserializeObject"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                .MakeGenericMethod(typeof(int));
            var overrides = RuntimeOverrideCollection.Load([companionPath]);

            var exceptionConstructor = overrides.GetDependencies(deserialize)
                .OfType<ConstructorInfo>()
                .Single(constructor =>
                    constructor.DeclaringType?.FullName
                        == "Newtonsoft.Json.JsonSerializationException");

            Assert.Same(
                targetContext,
                AssemblyLoadContext.GetLoadContext(exceptionConstructor.Module.Assembly));
            Assert.Equal(
                targetAssembly.ManifestModule.ModuleVersionId,
                exceptionConstructor.Module.ModuleVersionId);
        }
        finally
        {
            targetContext.Unload();
        }
    }

    [Test]
    public void ClosesGenericManagedDependenciesFromSelectedOverride()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.Json.fecs.dll");
        var overrides = RuntimeOverrideCollection.Load([companionPath]);
        var readFromJson = typeof(System.Net.Http.Json.HttpContentJsonExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == "ReadFromJsonAsync"
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(CancellationToken))
            .MakeGenericMethod(typeof(string));

        var deserialize = overrides.GetDependencies(readFromJson)
            .OfType<MethodInfo>()
            .Single(method =>
                method.DeclaringType == typeof(System.Text.Json.JsonSerializer)
                && method.Name == "Deserialize"
                && method.IsConstructedGenericMethod);

        Assert.Equal(typeof(string), Assert.Single(deserialize.GetGenericArguments()));
    }

    [Test]
    public void ClosesGenericJsonWriteDependenciesFromSelectedOverride()
    {
        var companionPath = Path.Combine(
            AppContext.BaseDirectory,
            "FrontEndCSharp.Overrides.System.Net.Http.Json.fecs.dll");
        var overrides = RuntimeOverrideCollection.Load([companionPath]);
        var postAsJson = typeof(System.Net.Http.Json.HttpClientJsonExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == "PostAsJsonAsync"
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 5
                && method.GetParameters()[1].ParameterType == typeof(string)
                && method.GetParameters()[3].ParameterType
                    == typeof(System.Text.Json.JsonSerializerOptions))
            .MakeGenericMethod(typeof(string));

        var serialize = overrides.GetDependencies(postAsJson)
            .OfType<MethodInfo>()
            .Single(method =>
                method.DeclaringType == typeof(System.Text.Json.JsonSerializer)
                && method.Name == "Serialize"
                && method.IsConstructedGenericMethod);

        Assert.Equal(typeof(string), Assert.Single(serialize.GetGenericArguments()));
    }

    [Test]
    public void DependencyDiscoveryUsesTypesThatRemainLoadable()
    {
        var types = RuntimeOverrideCollection.GetLoadableTypes(new PartiallyLoadableAssembly());

        Assert.Equal([typeof(string), typeof(int)], types);
    }

    [Test]
    public void DiscoversOnlyCompanionsDeclaredByTheApplicationDependencyGraph()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp_RuntimeOverrides_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var applicationPath = Path.Combine(temporaryDirectory, "Application.dll");
            File.WriteAllBytes(applicationPath, []);
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Console.fecs.dll"),
                Path.Combine(temporaryDirectory, "FrontEndCSharp.Overrides.System.Console.fecs.dll"));
            File.Copy(
                Path.Combine(AppContext.BaseDirectory, "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll"),
                Path.Combine(temporaryDirectory, "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs.dll"));
            File.WriteAllText(
                Path.ChangeExtension(applicationPath, ".deps.json"),
                """
                {
                  "runtimeTarget": { "name": ".NETCoreApp,Version=v8.0" },
                  "targets": {
                    ".NETCoreApp,Version=v8.0": {
                      "FrontEndCSharp.Overrides.System.Console.fecs/1.0.0": {
                        "runtime": { "FrontEndCSharp.Overrides.System.Console.fecs.dll": {} }
                      }
                    }
                  }
                }
                """);

            var overrides = RuntimeOverrideAssemblyDiscovery.Discover(applicationPath);

            Assert.True(overrides.HasRuntimeOverride(typeof(Console).GetMethod(
                nameof(Console.WriteLine),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(string)],
                modifiers: null)!));
            Assert.False(overrides.HasRuntimeOverride(typeof(char).GetMethod(
                nameof(char.IsWhiteSpace),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(char)],
                modifiers: null)!));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private sealed class PartiallyLoadableAssembly : Assembly
    {
        public override Type[] GetTypes() =>
            throw new ReflectionTypeLoadException(
                [typeof(string), null, typeof(int)],
                [new FileNotFoundException("An optional dependency is unavailable.")]);
    }

    private static string GetSourcePath(
        [System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        sourcePath;
}
