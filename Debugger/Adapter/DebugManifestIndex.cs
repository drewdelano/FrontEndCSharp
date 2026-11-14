using Debugging = FrontEndCSharp.Debugging;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class DebugManifestIndex
{
    private readonly Debugging.DebugManifestIndex index;

    private DebugManifestIndex(Debugging.DebugManifestIndex index)
    {
        this.index = index;
    }

    internal string BuildId => index.BuildId;
    internal int Generation => index.Generation;
    internal string ContentHash => index.ContentHash;

    internal static DebugManifestIndex Load(string path) =>
        new(Debugging.DebugManifestIndex.Load(path));

    internal bool TryResolve(
        string sourcePath,
        int requestedLine,
        out ResolvedBreakpoint? result,
        out string? error)
    {
        var resolution = index.Resolve(sourcePath, requestedLine);
        if (!resolution.IsResolved)
        {
            result = null;
            error = resolution.Error;
            return false;
        }

        result = new ResolvedBreakpoint(
            resolution.ModuleId,
            resolution.MethodToken,
            resolution.BlockIndex,
            resolution.InstructionIndex,
            resolution.IlOffset,
            resolution.MethodName,
            resolution.SourcePath ?? Path.GetFullPath(sourcePath),
            resolution.Line,
            resolution.Column);
        error = null;
        return true;
    }

    internal ResolvedFrame ResolveFrame(
        string moduleId,
        int methodToken,
        int blockIndex,
        int instructionIndex,
        IReadOnlyList<ManagedValueSnapshot> arguments,
        IReadOnlyList<ManagedValueSnapshot> locals,
        IReadOnlyList<ManagedNamedValueSnapshot> fields,
        IReadOnlyList<ManagedObjectSnapshot> objects)
    {
        var frame = new Debugging.ManagedRuntimeFrameSnapshot
        {
            ModuleId = moduleId,
            MethodToken = methodToken,
            BlockIndex = blockIndex,
            InstructionIndex = instructionIndex,
            Arguments = arguments.Select(ToManifestValue).ToList(),
            Locals = locals.Select(ToManifestValue).ToList(),
            Fields = fields.Select(ToManifestNamedValue).ToList()
        };
        var inspection = index.ResolveFrame(frame);
        var layout = index.ResolveFrameLayout(Debugging.ManagedBreakpointResolution.Resolved(
            moduleId,
            methodToken,
            blockIndex,
            instructionIndex,
            inspection.Line,
            inspection.Column));
        var fieldsByName = fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var objectsById = objects.ToDictionary(item => item.Id);

        return new ResolvedFrame(
            moduleId,
            inspection.MethodName,
            inspection.SourcePath,
            inspection.Line,
            inspection.Column,
            inspection.IlOffset,
            inspection.MethodToken,
            ResolveVariables(layout.Arguments, arguments, locals, fieldsByName, objectsById),
            ResolveVariables(layout.Locals, arguments, locals, fieldsByName, objectsById));
    }

    internal IReadOnlyList<ManagedStepLocation> GetStepLocations() => index
        .ResolveStepLocations()
        .Select(location => new ManagedStepLocation(
            location.ModuleId,
            location.MethodToken,
            location.BlockIndex,
            location.InstructionIndex))
        .ToList();

    private static IReadOnlyList<NamedVariable> ResolveVariables(
        IReadOnlyList<Debugging.ManagedRuntimeVariable> variables,
        IReadOnlyList<ManagedValueSnapshot> arguments,
        IReadOnlyList<ManagedValueSnapshot> locals,
        IReadOnlyDictionary<string, ManagedNamedValueSnapshot> fields,
        IReadOnlyDictionary<int, ManagedObjectSnapshot> objects)
    {
        var result = new List<NamedVariable>();
        foreach (var variable in variables)
        {
            var value = ResolveVariable(variable, arguments, locals, fields, objects);
            if (value != null)
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static NamedVariable? ResolveVariable(
        Debugging.ManagedRuntimeVariable variable,
        IReadOnlyList<ManagedValueSnapshot> arguments,
        IReadOnlyList<ManagedValueSnapshot> locals,
        IReadOnlyDictionary<string, ManagedNamedValueSnapshot> fields,
        IReadOnlyDictionary<int, ManagedObjectSnapshot> objects)
    {
        switch (variable.Kind)
        {
            case Debugging.ManagedRuntimeValueKind.Argument:
                return variable.Index >= 0 && variable.Index < arguments.Count
                    ? FromValue(
                        variable.Name,
                        arguments[variable.Index],
                        new ManagedAssignmentTarget { Kind = "argument", Index = variable.Index })
                    : null;
            case Debugging.ManagedRuntimeValueKind.Local:
                return variable.Index >= 0 && variable.Index < locals.Count
                    ? FromValue(
                        variable.Name,
                        locals[variable.Index],
                        new ManagedAssignmentTarget { Kind = "local", Index = variable.Index })
                    : null;
            case Debugging.ManagedRuntimeValueKind.Field:
                return variable.FieldName != null
                    && fields.TryGetValue(variable.FieldName, out var field)
                        ? FromValue(variable.Name, field, field.Target)
                        : null;
            case Debugging.ManagedRuntimeValueKind.LocalField:
                return ResolveCapturedLocal(variable, locals, objects);
            default:
                throw new InvalidDataException(
                    $"Unsupported managed runtime value kind '{variable.Kind}'.");
        }
    }

    private static NamedVariable? ResolveCapturedLocal(
        Debugging.ManagedRuntimeVariable variable,
        IReadOnlyList<ManagedValueSnapshot> locals,
        IReadOnlyDictionary<int, ManagedObjectSnapshot> objects)
    {
        if (variable.Index < 0 || variable.Index >= locals.Count)
        {
            return null;
        }

        ManagedValueSnapshot current = locals[variable.Index];
        ManagedNamedValueSnapshot? field = null;
        foreach (var fieldName in variable.FieldPath)
        {
            if (!current.ObjectId.HasValue
                || !objects.TryGetValue(current.ObjectId.Value, out var instance))
            {
                return null;
            }
            field = instance.Fields.FirstOrDefault(item => string.Equals(
                item.Name,
                fieldName,
                StringComparison.Ordinal));
            if (field == null)
            {
                return null;
            }
            current = field;
        }
        return field == null ? null : FromValue(variable.Name, field, field.Target);
    }

    private static NamedVariable FromValue(
        string name,
        ManagedValueSnapshot value,
        ManagedAssignmentTarget? target) =>
        new(name, value.Value, value.Type, value.ObjectId, target);

    private static Debugging.ManagedValueSnapshot ToManifestValue(ManagedValueSnapshot value) =>
        new() { Type = value.Type, Value = value.Value };

    private static Debugging.ManagedNamedValueSnapshot ToManifestNamedValue(
        ManagedNamedValueSnapshot value) =>
        new() { Name = value.Name, Type = value.Type, Value = value.Value };
}

internal sealed record ResolvedBreakpoint(
    string ModuleId,
    int MethodToken,
    int BlockIndex,
    int InstructionIndex,
    int IlOffset,
    string MethodName,
    string SourcePath,
    int Line,
    int Column);

internal sealed record ResolvedFrame(
    string ModuleId,
    string MethodName,
    string? SourcePath,
    int Line,
    int Column,
    int IlOffset,
    int MethodToken,
    IReadOnlyList<NamedVariable> Arguments,
    IReadOnlyList<NamedVariable> Locals);

internal sealed record NamedVariable(
    string Name,
    string Value,
    string Type,
    int? ObjectId = null,
    ManagedAssignmentTarget? Target = null);

internal sealed record ManagedStepLocation(
    string ModuleId,
    int MethodToken,
    int BlockIndex,
    int InstructionIndex);
