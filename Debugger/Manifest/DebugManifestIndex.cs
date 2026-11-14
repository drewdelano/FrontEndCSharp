using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FrontEndCSharp.Debugging;

public sealed class DebugManifestIndex
{
    private readonly IReadOnlyList<DebugManifestModule> modules;

    internal DebugManifestIndex(DebugManifestData manifest)
    {
        BuildId = manifest.BuildId;
        Generation = manifest.Generation;
        ContentHash = manifest.ContentHash;
        modules = manifest.Modules;
    }

    public string BuildId { get; }
    public int Generation { get; }
    public string ContentHash { get; }

    public static DebugManifestIndex Load(string path) =>
        new DebugManifestIndex(DebugManifestLoader.Load(path));

    public bool OwnsSource(string sourcePath) => modules
        .SelectMany(module => module.Documents)
        .Any(document => PathsEqual(document.Path, sourcePath));

    public bool OwnsFrame(ManagedRuntimeFrameSnapshot frame) => modules.Any(module =>
        string.Equals(module.ModuleId, frame.ModuleId, StringComparison.OrdinalIgnoreCase)
        && module.Methods.Any(method => method.MethodToken == frame.MethodToken));

    public ManagedBreakpointResolution Resolve(string sourcePath, int requestedLine)
    {
        foreach (var module in modules)
        {
            var document = module.Documents.FirstOrDefault(item => PathsEqual(item.Path, sourcePath));
            if (document == null)
            {
                continue;
            }
            if (!ChecksumMatches(document, sourcePath))
            {
                return ManagedBreakpointResolution.Unresolved(
                    "The source file differs from the active FrontEndCSharp build.",
                    requestedLine);
            }

            var points = module.Methods
                .SelectMany(method => method.SequencePoints
                    .Where(point => !point.IsHidden
                        && point.DocumentId == document.Id
                        && point.BlockIndex.HasValue
                        && point.InstructionIndex.HasValue)
                    .Select(point => new MethodSequencePoint(method, point)))
                .OrderBy(item => item.Point.StartLine)
                .ThenBy(item => item.Point.StartColumn)
                .ToList();
            var match = points.FirstOrDefault(item =>
                item.Point.StartLine <= requestedLine && requestedLine <= item.Point.EndLine)
                ?? points.FirstOrDefault(item => item.Point.StartLine >= requestedLine);
            if (match == null)
            {
                return ManagedBreakpointResolution.Unresolved(
                    "The source file has no executable sequence point at or after this line.",
                    requestedLine);
            }

            return ManagedBreakpointResolution.Resolved(
                module.ModuleId,
                match.Method.MethodToken,
                match.Point.BlockIndex!.Value,
                match.Point.InstructionIndex!.Value,
                match.Point.StartLine,
                match.Point.StartColumn,
                match.Point.IlOffset,
                match.Method.Name,
                document.Path);
        }

        return ManagedBreakpointResolution.Unresolved(
            "The source file is not present in the active FrontEndCSharp build.",
            requestedLine);
    }

    public ManagedFrameInspection ResolveFrame(ManagedRuntimeFrameSnapshot frame)
    {
        var module = modules.Single(item => string.Equals(
            item.ModuleId,
            frame.ModuleId,
            StringComparison.OrdinalIgnoreCase));
        var method = module.Methods.Single(item => item.MethodToken == frame.MethodToken);
        var instruction = method.Instructions.Single(item =>
            item.BlockIndex == frame.BlockIndex
            && item.InstructionIndex == frame.InstructionIndex);
        var visiblePoints = method.SequencePoints
            .Where(item => !item.IsHidden)
            .OrderBy(item => item.IlOffset)
            .ToList();
        var point = visiblePoints
            .LastOrDefault(item => item.IlOffset <= instruction.IlOffset)
            ?? visiblePoints.FirstOrDefault();
        var document = point?.DocumentId is int documentId
            ? module.Documents.SingleOrDefault(item => item.Id == documentId)
            : null;
        if (document != null && !ChecksumMatches(document, document.Path))
        {
            throw new InvalidDataException(
                "The managed frame source differs from the active FrontEndCSharp build.");
        }
        var displayMethod = method.StateMachineKickoffMethodToken is int kickoffToken
            ? module.Methods.SingleOrDefault(item => item.MethodToken == kickoffToken) ?? method
            : method;
        var fieldsByName = frame.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        List<ManagedVariable> parameters;
        if (displayMethod != method)
        {
            parameters = new List<ManagedVariable>();
            if (displayMethod.HasThis && fieldsByName.TryGetValue("<>4__this", out var thisField))
            {
                parameters.Add(new ManagedVariable("this", thisField.Value, thisField.Type));
            }
            foreach (var parameter in displayMethod.Parameters.OrderBy(item => item.Index))
            {
                if (fieldsByName.TryGetValue(parameter.Name, out var field))
                {
                    parameters.Add(new ManagedVariable(parameter.Name, field.Value, field.Type));
                }
            }
        }
        else
        {
            parameters = frame.Arguments.Select((value, index) => new ManagedVariable(
                method.HasThis && index == 0
                    ? "this"
                    : method.Parameters.FirstOrDefault(parameter =>
                        parameter.Index == index - (method.HasThis ? 1 : 0))?.Name ?? "arg" + index,
                value.Value,
                value.Type)).ToList();
        }

        var activeLocals = method.LocalScopes
            .Where(scope => scope.StartOffset <= instruction.IlOffset
                && instruction.IlOffset < scope.EndOffset)
            .SelectMany(scope => scope.Locals)
            .GroupBy(local => local.Slot)
            .ToDictionary(group => group.Key, group => group.First());
        var locals = frame.Locals
            .Select((value, slot) => new IndexedLocalValue(value, slot))
            .Where(item => activeLocals.TryGetValue(item.Slot, out var local)
                && !local.IsCompilerGeneratedClosure)
            .Select(item => new ManagedVariable(
                activeLocals[item.Slot].Name,
                item.Value.Value,
                item.Value.Type))
            .ToList();
        if (displayMethod != method)
        {
            locals = method.HoistedLocals
                .Where(local => local.StartOffset <= instruction.IlOffset
                    && instruction.IlOffset < local.EndOffset
                    && fieldsByName.ContainsKey(local.FieldName))
                .Select(local =>
                {
                    var field = fieldsByName[local.FieldName];
                    return new ManagedVariable(local.Name, field.Value, field.Type);
                })
                .ToList();
        }

        return new ManagedFrameInspection(
            displayMethod.Name,
            document?.Path,
            point?.StartLine ?? 1,
            point?.StartColumn ?? 1,
            parameters,
            locals,
            instruction.IlOffset,
            displayMethod.MethodToken);
    }

    public ManagedFrameLayout ResolveFrameLayout(ManagedBreakpointResolution resolution)
    {
        var module = modules.Single(item => string.Equals(
            item.ModuleId,
            resolution.ModuleId,
            StringComparison.OrdinalIgnoreCase));
        var method = module.Methods.Single(item => item.MethodToken == resolution.MethodToken);
        var instruction = method.Instructions.Single(item =>
            item.BlockIndex == resolution.BlockIndex
            && item.InstructionIndex == resolution.InstructionIndex);
        var displayMethod = method.StateMachineKickoffMethodToken is int kickoffToken
            ? module.Methods.SingleOrDefault(item => item.MethodToken == kickoffToken) ?? method
            : method;
        var arguments = new List<ManagedRuntimeVariable>();
        var locals = new List<ManagedRuntimeVariable>();
        if (displayMethod != method)
        {
            if (displayMethod.HasThis)
            {
                arguments.Add(new ManagedRuntimeVariable(
                    "this",
                    ManagedRuntimeValueKind.Field,
                    fieldName: "<>4__this"));
            }
            arguments.AddRange(displayMethod.Parameters
                .OrderBy(item => item.Index)
                .Select(parameter => new ManagedRuntimeVariable(
                    parameter.Name,
                    ManagedRuntimeValueKind.Field,
                    fieldName: parameter.Name)));
            locals.AddRange(method.HoistedLocals
                .Where(local => local.StartOffset <= instruction.IlOffset
                    && instruction.IlOffset < local.EndOffset)
                .Select(local => new ManagedRuntimeVariable(
                    local.Name,
                    ManagedRuntimeValueKind.Field,
                    fieldName: local.FieldName)));
        }
        else
        {
            if (method.HasThis)
            {
                arguments.Add(new ManagedRuntimeVariable(
                    "this",
                    ManagedRuntimeValueKind.Argument,
                    0));
            }
            arguments.AddRange(method.Parameters
                .OrderBy(item => item.Index)
                .Select(parameter => new ManagedRuntimeVariable(
                    parameter.Name,
                    ManagedRuntimeValueKind.Argument,
                    parameter.Index + (method.HasThis ? 1 : 0))));
            locals.AddRange(method.LocalScopes
                .Where(scope => scope.StartOffset <= instruction.IlOffset
                    && instruction.IlOffset < scope.EndOffset)
                .SelectMany(scope => scope.Locals)
                .GroupBy(local => local.Slot)
                .Select(group => group.First())
                .OrderBy(local => local.Slot)
                .SelectMany(local => local.IsCompilerGeneratedClosure
                    ? local.CapturedLocals.Select(captured => new ManagedRuntimeVariable(
                        captured.Name,
                        ManagedRuntimeValueKind.LocalField,
                        local.Slot,
                        fieldPath: captured.FieldPath))
                    : new[] { new ManagedRuntimeVariable(
                        local.Name,
                        ManagedRuntimeValueKind.Local,
                        local.Slot) }));
        }
        return new ManagedFrameLayout(arguments, locals);
    }

    public IReadOnlyList<ManagedRuntimeLocation> ResolveStepLocations() => modules
        .SelectMany(module => module.Methods.SelectMany(method => method.SequencePoints
            .Where(point => !point.IsHidden
                && point.BlockIndex.HasValue
                && point.InstructionIndex.HasValue)
            .Select(point => new ManagedRuntimeLocation(
                module.ModuleId,
                method.MethodToken,
                point.BlockIndex!.Value,
                point.InstructionIndex!.Value))))
        .GroupBy(location => string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1}:{2}:{3}",
            location.ModuleId,
            location.MethodToken,
            location.BlockIndex,
            location.InstructionIndex), StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToList();

    private static bool ChecksumMatches(DebugManifestDocument document, string sourcePath)
    {
        if (string.IsNullOrEmpty(document.ChecksumAlgorithm)
            || string.IsNullOrEmpty(document.Checksum))
        {
            return true;
        }

        using HashAlgorithm algorithm = document.ChecksumAlgorithm.ToUpperInvariant() switch
        {
            "SHA1" => SHA1.Create(),
            "SHA256" => SHA256.Create(),
            _ => throw new InvalidDataException(
                "The FrontEndCSharp debug manifest uses an unsupported checksum algorithm.")
        };
        using var stream = File.OpenRead(sourcePath);
        var actual = BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        return string.Equals(actual, document.Checksum, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
    {
        var fullLeft = Path.GetFullPath(left);
        var fullRight = Path.GetFullPath(right);
        if (string.Equals(
            NormalizeWindowsDevicePrefix(fullLeft),
            NormalizeWindowsDevicePrefix(fullRight),
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        var finalLeft = TryGetFinalPath(fullLeft);
        var finalRight = TryGetFinalPath(fullRight);
        return finalLeft != null
            && finalRight != null
            && string.Equals(finalLeft, finalRight, StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryGetFinalPath(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var capacity = 512;
            while (capacity <= 32768)
            {
                var result = new StringBuilder(capacity);
                var length = GetFinalPathNameByHandle(
                    stream.SafeFileHandle,
                    result,
                    (uint)result.Capacity,
                    0);
                if (length == 0)
                {
                    return null;
                }
                if (length < result.Capacity)
                {
                    return NormalizeWindowsDevicePrefix(result.ToString());
                }
                capacity = checked((int)length + 1);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException
            || exception is ArgumentException)
        {
            return null;
        }
        return null;
    }

    private static string NormalizeWindowsDevicePrefix(string path)
    {
        const string deviceUncPrefix = @"\\?\UNC\";
        const string devicePrefix = @"\\?\";
        if (path.StartsWith(deviceUncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return @"\\" + path.Substring(deviceUncPrefix.Length);
        }
        return path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(devicePrefix.Length)
            : path;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle fileHandle,
        [Out] StringBuilder filePath,
        uint filePathLength,
        uint flags);

    private sealed class MethodSequencePoint
    {
        internal MethodSequencePoint(
            DebugManifestMethod method,
            DebugManifestSequencePoint point)
        {
            Method = method;
            Point = point;
        }

        internal DebugManifestMethod Method { get; }
        internal DebugManifestSequencePoint Point { get; }
    }

    private sealed class IndexedLocalValue
    {
        internal IndexedLocalValue(ManagedValueSnapshot value, int slot)
        {
            Value = value;
            Slot = slot;
        }

        internal ManagedValueSnapshot Value { get; }
        internal int Slot { get; }
    }
}
