using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.RuntimeOverrides.Tests;

internal static class RuntimeOverrideCompanionContract
{
    internal static void AssertValid(
        string companionAssemblyName,
        string targetAssemblyName,
        int expectedOverrideCount,
        bool targetAssemblyIsProjectReference = false)
    {
        var companionAssembly = LoadOutputAssembly(companionAssemblyName);
        Assert.Equal(companionAssemblyName, companionAssembly.GetName().Name);

        var targetAttribute = Assert.Single(
            companionAssembly.GetCustomAttributesData(),
            attribute => attribute.AttributeType.FullName ==
                "FrontEndCSharp.RuntimeOverrides.RuntimeOverrideAssemblyAttribute");
        Assert.Equal(targetAssemblyName, Assert.IsType<string>(Assert.Single(targetAttribute.ConstructorArguments).Value));

        if (targetAssemblyIsProjectReference)
        {
            var targetAssembly = LoadOutputAssembly(targetAssemblyName);
            Assert.Equal(targetAssemblyName, targetAssembly.GetName().Name);
        }

        var manifestResourceName = Assert.Single(
            companionAssembly.GetManifestResourceNames(),
            name => name.EndsWith(".Overrides.manifest.json", StringComparison.Ordinal));
        using var manifestStream = companionAssembly.GetManifestResourceStream(manifestResourceName);
        Assert.NotNull(manifestStream);
        using var manifest = JsonDocument.Parse(manifestStream);

        var overrides = manifest.RootElement.GetProperty("overrides").EnumerateArray().ToArray();
        Assert.Equal(expectedOverrideCount, overrides.Length);

        var embeddedResources = companionAssembly.GetManifestResourceNames().ToHashSet(StringComparer.Ordinal);
        var assignedJavaScriptResources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in overrides)
        {
            var signature = entry.GetProperty("signature").GetString();
            var file = entry.GetProperty("file").GetString();
            Assert.False(string.IsNullOrWhiteSpace(signature));
            Assert.False(string.IsNullOrWhiteSpace(file));

            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature!))).ToLowerInvariant();
            Assert.Equal($"method.{hash}.js", file);
            var resourceName = Assert.Single(
                embeddedResources,
                name => name.EndsWith($".Overrides.{file}", StringComparison.Ordinal));
            Assert.True(assignedJavaScriptResources.Add(resourceName));
        }

        const string runtimeAssetMarker = ".RuntimeAssets.";
        var runtimeAssetPrefix = companionAssemblyName + runtimeAssetMarker;
        Assert.DoesNotContain(
            embeddedResources,
            name => name.Contains(runtimeAssetMarker, StringComparison.Ordinal)
                && !name.StartsWith(runtimeAssetPrefix, StringComparison.Ordinal));
        var runtimeAssets = embeddedResources
            .Where(name => name.StartsWith(runtimeAssetPrefix, StringComparison.Ordinal))
            .ToArray();
        Assert.All(runtimeAssets, resourceName =>
        {
            var fileName = resourceName[runtimeAssetPrefix.Length..];
            Assert.Equal(fileName, Path.GetFileName(fileName));
            Assert.Matches("^[a-z0-9][a-z0-9.-]*\\.js$", fileName);
            using var stream = companionAssembly.GetManifestResourceStream(resourceName);
            Assert.NotNull(stream);
            Assert.True(stream.Length > 0);
            Assert.True(assignedJavaScriptResources.Add(resourceName));
        });
        Assert.DoesNotContain(
            embeddedResources,
            name => name.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                && !assignedJavaScriptResources.Contains(name));
    }

    private static Assembly LoadOutputAssembly(string assemblyName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.dll");
        Assert.True(File.Exists(path), $"Expected project output '{path}'.");
        return Assembly.LoadFrom(path);
    }
}
