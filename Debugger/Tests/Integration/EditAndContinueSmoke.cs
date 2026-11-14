using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Debugger.Integration;

internal static class EditAndContinueSmoke
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMinutes(5);

    public static void Run()
    {
        var repository = RepositoryLayout.Root;
        var fixtureSource = Path.Combine(
            repository,
            "Debugger",
            "Tests",
            "Fixtures",
            "EditAndContinueFixture");
        var sessionRoot = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp-EnC-" + Guid.NewGuid().ToString("N"));
        var projectRoot = Path.Combine(sessionRoot, "project");
        var stateRoot = Path.Combine(sessionRoot, "state");
        var baselineOutput = Path.Combine(sessionRoot, "baseline-js");
        var candidateOutput = Path.Combine(sessionRoot, "candidate-js");
        var hostPath = Path.Combine(
            repository,
            "Debugger",
            "EditAndContinue",
            "bin",
            "Debug",
            "net8.0",
            "FrontEndCSharp.EditAndContinue.exe");
        var compilerPath = Path.Combine(
            repository,
            "Compiler",
            "Core",
            "bin",
            "Debug",
            "net8.0",
            "Compiler.exe");
        CopyFixture(fixtureSource, projectRoot);
        try
        {
            var projectPath = Path.Combine(projectRoot, "EditAndContinueFixture.csproj");
            var sourcePath = Path.Combine(projectRoot, "Program.cs");
            File.WriteAllText(
                projectPath,
                File.ReadAllText(projectPath).Replace(
                    @"..\..\..\..\RuntimeOverrides",
                    Path.Combine(repository, "RuntimeOverrides"),
                    StringComparison.Ordinal));
            using var host = new JsonLineProcess(hostPath, projectPath, stateRoot);
            var initial = host.NextResponse();
            RequireSuccess(initial, expected: true);
            Require(GetInt32(initial, "generation") == 0, "Initial generation was not zero.");
            var initialAssembly = GetString(initial, "assemblyPath");
            RunCompiler(compilerPath, repository,
                "--no-bundle", projectPath, baselineOutput, initialAssembly);
            var baselineManifest = Path.Combine(
                Path.GetDirectoryName(initialAssembly)!,
                "EditAndContinueFixture.frontendcsharp.debug.json");

            var baselineSource = File.ReadAllText(sourcePath);
            File.WriteAllText(
                sourcePath,
                baselineSource.Replace("var result = 1;", "var result = 2;", StringComparison.Ordinal));
            var prepared = host.Request(new { command = "prepare", generation = 0 });
            RequireSuccess(prepared, expected: true);
            Require(GetInt32(prepared, "generation") == 1, "Prepared generation was not one.");
            var updatedMethodTokens = GetProperty(prepared, "updatedMethodTokens")
                .EnumerateArray()
                .Select(token => token.GetInt32())
                .ToArray();
            Require(
                updatedMethodTokens.SequenceEqual(new[] { 0x06000002 }),
                "The Edit-and-Continue host updated unexpected method tokens.");
            foreach (var property in new[]
            {
                "metadataDeltaPath",
                "ilDeltaPath",
                "pdbDeltaPath",
            })
            {
                var deltaPath = GetString(prepared, property);
                Require(
                    new FileInfo(deltaPath).Length > 0,
                    $"Edit-and-Continue delta '{deltaPath}' was empty.");
            }

            var preparedAssembly = GetString(prepared, "assemblyPath");
            RunCompiler(compilerPath, repository,
                "--no-bundle", projectPath, candidateOutput, preparedAssembly);
            var candidateManifest = Path.Combine(
                Path.GetDirectoryName(preparedAssembly)!,
                "EditAndContinueFixture.frontendcsharp.debug.json");
            var updateManifest = Path.Combine(
                Path.GetDirectoryName(preparedAssembly)!,
                "update.frontendcsharp.debug.json");
            var translation = RunCompiler(
                compilerPath,
                repository,
                "--create-debug-update",
                baselineManifest,
                candidateManifest,
                baselineOutput,
                candidateOutput,
                "1",
                string.Join(',', updatedMethodTokens),
                updateManifest);
            using var translationDocument = JsonDocument.Parse(
                translation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[^1]);
            var update = translationDocument.RootElement;
            var updateModulePath = GetString(update, "updateModulePath");
            var updateHash = GetString(update, "hash");
            var updateManifestPath = GetString(update, "debugManifestPath");
            var runtimeResult = BoundedProcess.Run(
                "node",
                repository,
                TimeSpan.FromSeconds(30),
                Path.Combine(
                    repository,
                    "Debugger",
                    "Tests",
                    "Integration",
                    "edit-and-continue.runtime.mjs"),
                baselineOutput,
                updateManifestPath,
                updateModulePath,
                updateHash);
            using var runtimeDocument = JsonDocument.Parse(runtimeResult.StandardOutput);
            var runtime = runtimeDocument.RootElement;
            Require(runtime.GetProperty("before").GetInt32() == 1,
                "The baseline runtime method did not return one.");
            Require(runtime.GetProperty("after").GetInt32() == 2,
                "The updated runtime method did not return two.");
            Require(runtime.GetProperty("generation").GetInt32() == 1,
                "The runtime acknowledged the wrong generation.");
            Require(runtime.GetProperty("hash").GetString() == updateHash,
                "The runtime acknowledged the wrong update hash.");
            Require(runtime.GetProperty("staleRejected").GetBoolean(),
                "The runtime accepted a stale duplicate update.");

            var committed = host.Request(new { command = "commit", generation = 1 });
            RequireSuccess(committed, expected: true);
            var changedSource = File.ReadAllText(sourcePath);
            File.WriteAllText(
                sourcePath,
                Regex.Replace(
                    changedSource,
                    "public static class Program\\r?\\n\\{",
                    "public static class Program\n{\n    private static int AddedField;"));
            RequireFailure(
                host.Request(new { command = "prepare", generation = 1 }),
                "Only method-body edits are supported");

            File.WriteAllText(
                sourcePath,
                changedSource.Replace("var result = 2;", "var result = ;", StringComparison.Ordinal));
            RequireFailure(
                host.Request(new { command = "prepare", generation = 1 }),
                "updated compilation failed");

            File.WriteAllText(
                sourcePath,
                changedSource.Replace("() => value;", "() => value + 1;", StringComparison.Ordinal));
            RequireFailure(
                host.Request(new { command = "prepare", generation = 1 }),
                "closure, lambda, or local-function state");

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                generation = 1,
                updatedMethodTokens,
                before = 1,
                after = 2,
                layoutRejected = true,
                compileFailureRejected = true,
                closureEditRejected = true,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            Directory.Delete(sessionRoot, recursive: true);
        }
    }

    private static string RunCompiler(
        string compilerPath,
        string repository,
        params string[] arguments) =>
        BoundedProcess.Run(compilerPath, repository, ProcessTimeout, arguments).StandardOutput;

    private static void CopyFixture(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(segment => segment == "bin" || segment == "obj"))
            {
                continue;
            }
            var output = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.Copy(file, output);
        }
    }

    private static void RequireFailure(JsonElement response, string expectedMessage)
    {
        RequireSuccess(response, expected: false);
        var error = GetString(response, "error");
        Require(
            error.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase),
            $"Expected failure containing '{expectedMessage}', received '{error}'.");
    }

    private static void RequireSuccess(JsonElement response, bool expected)
    {
        var success = GetProperty(response, "success").GetBoolean();
        if (success != expected)
        {
            var error = TryGetProperty(response, "error", out var errorProperty)
                ? errorProperty.GetString()
                : response.GetRawText();
            throw new InvalidDataException(error);
        }
    }

    private static int GetInt32(JsonElement element, string name) =>
        GetProperty(element, name).GetInt32();

    private static string GetString(JsonElement element, string name) =>
        GetProperty(element, name).GetString()
        ?? throw new InvalidDataException($"JSON property '{name}' was null.");

    private static JsonElement GetProperty(JsonElement element, string name)
    {
        if (TryGetProperty(element, name, out var property))
        {
            return property;
        }
        throw new InvalidDataException(
            $"JSON did not contain '{name}' or its Pascal-case equivalent: {element.GetRawText()}");
    }

    private static bool TryGetProperty(
        JsonElement element,
        string name,
        out JsonElement property) =>
        element.TryGetProperty(name, out property)
        || element.TryGetProperty(char.ToUpperInvariant(name[0]) + name[1..], out property);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
