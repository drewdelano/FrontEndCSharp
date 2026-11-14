using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using Compiler.Write;
using System.Reflection;
using System.Runtime.Loader;

namespace Compiler.Tests;

public sealed class RuntimeIdentityCanonicalizationTests
{
    [Test]
    public void IdenticalDefinitionsLoadedInDifferentContextsHaveOneMetadataIdentity()
    {
        var assemblyPath = GetNewtonsoftAssemblyPath();
        var firstContext = CreateLoadContext();
        var secondContext = CreateLoadContext();
        try
        {
            var first = LoadSerializationException(firstContext, assemblyPath);
            var second = LoadSerializationException(secondContext, assemblyPath);

            Assert.NotSame(first, second);
            Assert.Equal(
                MetadataIdentity.GetTypeDefinition(first),
                MetadataIdentity.GetTypeDefinition(second));
        }
        finally
        {
            firstContext.Unload();
            secondContext.Unload();
        }
    }

    [Test]
    public void BinaryPackageCanonicalizesIdenticalTypesIndependentOfDiscoveryOrder()
    {
        var assemblyPath = GetNewtonsoftAssemblyPath();
        var firstContext = CreateLoadContext();
        var secondContext = CreateLoadContext();
        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        try
        {
            var first = LoadSerializationException(firstContext, assemblyPath);
            var second = LoadSerializationException(secondContext, assemblyPath);

            MsilBinaryPackage.Write(firstPath, CreateCallGraph(first, second));
            MsilBinaryPackage.Write(secondPath, CreateCallGraph(second, first));

            Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
            firstContext.Unload();
            secondContext.Unload();
        }
    }

    private static CallGraphInfo CreateCallGraph(TypeInfo first, TypeInfo second) =>
        new()
        {
            RuntimeOverrides = RuntimeOverrideCollection.Empty,
            MethodDecoders = new List<MethodDecoder>(),
            AllReferencedFields = new Dictionary<TypeInfo, List<FieldInfo>>
            {
                [first] = new List<FieldInfo>(),
                [second] = new List<FieldInfo>(),
            },
            EntryPoint = typeof(RuntimeIdentityCanonicalizationTests).GetMethod(
                nameof(FixtureEntryPoint),
                BindingFlags.NonPublic | BindingFlags.Static)!,
        };

    private static AssemblyLoadContext CreateLoadContext() =>
        new(
            "RuntimeIdentityCanonicalization_" + Guid.NewGuid().ToString("N"),
            isCollectible: true);

    private static TypeInfo LoadSerializationException(
        AssemblyLoadContext context,
        string assemblyPath) =>
        context.LoadFromAssemblyPath(assemblyPath)
            .GetType(
                "Newtonsoft.Json.JsonSerializationException",
                throwOnError: true)!
            .GetTypeInfo();

    private static string GetNewtonsoftAssemblyPath() =>
        Path.Combine(
            Path.GetDirectoryName(GetSourcePath())!,
            "Fixtures",
            "SystemTextJsonBrowserFixture",
            "bin",
            "Debug",
            "net8.0",
            "Newtonsoft.Json.dll");

    private static void FixtureEntryPoint()
    {
    }

    private static string GetSourcePath(
        [System.Runtime.CompilerServices.CallerFilePath] string sourcePath = "") =>
        sourcePath;
}
