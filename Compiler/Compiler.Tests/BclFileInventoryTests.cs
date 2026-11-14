using System.Reflection;
using Compiler.Helpers;

namespace Compiler.Tests;

public sealed class BclFileInventoryTests
{
    [Test]
    public void InventorySnapshotsPackagedMethodFiles()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_BclFileInventoryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        try
        {
            var included = typeof(string).GetProperty(nameof(string.Length))!.GetMethod!;
            var excluded = typeof(string).GetMethod(
                nameof(string.Contains),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [typeof(string)],
                modifiers: null)!;
            var includedPath = Path.Combine(
                testRoot,
                NameNormalizers.GetFileSystemSafeMethodNameWithPrefix(included));
            File.WriteAllText(includedPath, "// fixture");

            var inventory = new BclFileInventory(testRoot);

            Assert.True(inventory.Contains(included));
            Assert.False(inventory.Contains(excluded));

            File.Delete(includedPath);
            Assert.True(inventory.Contains(included));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
