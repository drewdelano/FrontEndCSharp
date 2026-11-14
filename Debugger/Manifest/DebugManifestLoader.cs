using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal static class DebugManifestLoader
{
    internal static DebugManifestData Load(string path)
    {
        var root = JObject.Parse(File.ReadAllText(path));
        if (root.Value<int?>("schemaVersion") != 1)
        {
            throw new InvalidDataException("The FrontEndCSharp debug manifest schema is unsupported.");
        }

        var buildId = root.Value<string>("buildId");
        if (string.IsNullOrWhiteSpace(buildId))
        {
            throw new InvalidDataException("The FrontEndCSharp debug manifest has no build identity.");
        }

        var generation = root.Value<int>("generation");
        if (generation < 0)
        {
            throw new InvalidDataException(
                $"The FrontEndCSharp debug manifest has invalid generation {generation}.");
        }

        var contentHash = root.Value<string>("contentHash");

        var modules = LoadArray(root, "modules", LoadModule);
        return new DebugManifestData(
            buildId!,
            generation,
            string.IsNullOrWhiteSpace(contentHash) ? buildId! : contentHash!,
            modules);
    }

    private static DebugManifestModule LoadModule(JObject value) => new DebugManifestModule(
        value.Value<string>("moduleId") ?? string.Empty,
        LoadArray(value, "documents", LoadDocument),
        LoadArray(value, "methods", LoadMethod));

    private static DebugManifestDocument LoadDocument(JObject value) => new DebugManifestDocument(
        value.Value<int>("id"),
        value.Value<string>("path") ?? string.Empty,
        value.Value<string>("checksumAlgorithm") ?? string.Empty,
        value.Value<string>("checksum") ?? string.Empty);

    private static DebugManifestMethod LoadMethod(JObject value) => new DebugManifestMethod(
        value.Value<int>("methodToken"),
        value.Value<string>("name") ?? string.Empty,
        value.Value<bool>("hasThis"),
        value.Value<int?>("stateMachineKickoffMethodToken"),
        LoadArray(value, "parameters", LoadParameter),
        LoadArray(value, "instructions", LoadInstruction),
        LoadArray(value, "sequencePoints", LoadSequencePoint),
        LoadArray(value, "localScopes", LoadLocalScope),
        LoadArray(value, "hoistedLocals", LoadHoistedLocal));

    private static DebugManifestParameter LoadParameter(JObject value) => new DebugManifestParameter(
        value.Value<int>("index"),
        value.Value<string>("name") ?? string.Empty);

    private static DebugManifestInstruction LoadInstruction(JObject value) =>
        new DebugManifestInstruction(
            value.Value<int>("ilOffset"),
            value.Value<int>("blockIndex"),
            value.Value<int>("instructionIndex"));

    private static DebugManifestLocalScope LoadLocalScope(JObject value) =>
        new DebugManifestLocalScope(
            value.Value<int>("startOffset"),
            value.Value<int>("endOffset"),
            LoadArray(value, "locals", LoadLocal));

    private static DebugManifestLocal LoadLocal(JObject value) => new DebugManifestLocal(
        value.Value<int>("slot"),
        value.Value<string>("name") ?? string.Empty,
        value.Value<bool?>("isCompilerGeneratedClosure") == true,
        LoadArray(value, "capturedLocals", LoadCapturedLocal));

    private static DebugManifestCapturedLocal LoadCapturedLocal(JObject value) =>
        new DebugManifestCapturedLocal(
            value.Value<string>("name") ?? string.Empty,
            (value["fieldPath"] as JArray ?? new JArray())
                .Values<string>()
                .Where(item => item != null)
                .Select(item => item!)
                .ToList());

    private static DebugManifestHoistedLocal LoadHoistedLocal(JObject value) =>
        new DebugManifestHoistedLocal(
            value.Value<string>("fieldName") ?? string.Empty,
            value.Value<string>("name") ?? string.Empty,
            value.Value<int>("startOffset"),
            value.Value<int>("endOffset"));

    private static DebugManifestSequencePoint LoadSequencePoint(JObject value) =>
        new DebugManifestSequencePoint(
            value.Value<int>("ilOffset"),
            value.Value<int?>("documentId"),
            value.Value<bool>("isHidden"),
            value.Value<int>("startLine"),
            value.Value<int>("startColumn"),
            value.Value<int>("endLine"),
            value.Value<int?>("blockIndex"),
            value.Value<int?>("instructionIndex"));

    private static IReadOnlyList<T> LoadArray<T>(
        JObject value,
        string name,
        Func<JObject, T> load) => (value[name] as JArray ?? new JArray())
            .Children<JObject>()
            .Select(load)
            .ToList();
}
