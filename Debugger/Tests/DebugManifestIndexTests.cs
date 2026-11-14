using FrontEndCSharp.DebugAdapter;

namespace Debugger.Tests;

public sealed class DebugManifestIndexTests
{
    [Test]
    public void LoadsTheSharedBuildAndUpdateIdentity()
    {
        var manifestPath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-identity-" + Guid.NewGuid().ToString("N") + ".frontendcsharp.debug.json");
        try
        {
            File.WriteAllText(manifestPath, """
                {
                  "schemaVersion": 1,
                  "buildId": "baseline-build",
                  "generation": 3,
                  "contentHash": "updated-content",
                  "modules": []
                }
                """);

            var index = DebugManifestIndex.Load(manifestPath);

            Assert.Equal("baseline-build", index.BuildId);
            Assert.Equal(3, index.Generation);
            Assert.Equal("updated-content", index.ContentHash);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [Test]
    public void RejectsAnInvalidGenerationThroughTheSharedValidationPolicy()
    {
        var manifestPath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-invalid-generation-" + Guid.NewGuid().ToString("N") + ".frontendcsharp.debug.json");
        try
        {
            File.WriteAllText(manifestPath, """
                {
                  "schemaVersion": 1,
                  "buildId": "baseline-build",
                  "generation": -1,
                  "modules": []
                }
                """);

            var exception = Assert.Throws<InvalidDataException>(() =>
                DebugManifestIndex.Load(manifestPath));

            Assert.Contains("invalid generation -1", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    [Test]
    public void ProjectsCapturedLocalsInsteadOfTheCompilerGeneratedDisplayClass()
    {
        var manifestPath = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-closure-" + Guid.NewGuid().ToString("N") + ".frontendcsharp.debug.json");
        try
        {
            File.WriteAllText(manifestPath, """
                {
                  "schemaVersion": 1,
                  "buildId": "closure-build",
                  "modules": [{
                    "moduleId": "module-one",
                    "methods": [{
                      "methodToken": 100663297,
                      "name": "Program.Main",
                      "instructions": [{ "ilOffset": 0, "blockIndex": 0, "instructionIndex": 0 }],
                      "sequencePoints": [],
                      "localScopes": [{
                        "startOffset": 0,
                        "endOffset": 10,
                        "locals": [{
                          "slot": 0,
                          "name": "CS$<>8__locals0",
                          "isCompilerGeneratedClosure": true,
                          "capturedLocals": [{
                            "name": "captured",
                            "type": "System.Int32",
                            "fieldPath": ["captured"]
                          }]
                        }]
                      }]
                    }]
                  }]
                }
                """);
            var index = DebugManifestIndex.Load(manifestPath);
            var target = new ManagedAssignmentTarget
            {
                Kind = "local",
                Index = 0,
                Path = new[] { "captured" }
            };

            var frame = index.ResolveFrame(
                "module-one",
                100663297,
                0,
                0,
                Array.Empty<ManagedValueSnapshot>(),
                new[]
                {
                    new ManagedValueSnapshot(
                        "Program+<>c__DisplayClass0_0",
                        "{Program+<>c__DisplayClass0_0}",
                        objectId: 1)
                },
                Array.Empty<ManagedNamedValueSnapshot>(),
                new[]
                {
                    new ManagedObjectSnapshot(1, new[]
                    {
                        new ManagedNamedValueSnapshot(
                            "captured",
                            "System.Int32",
                            "42")
                        {
                            Target = target
                        }
                    })
                });

            var local = Assert.Single(frame.Locals);
            Assert.Equal("captured", local.Name);
            Assert.Equal("42", local.Value);
            Assert.Same(target, local.Target);
            Assert.DoesNotContain(frame.Locals, item => item.Name == "CS$<>8__locals0");
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }
}
