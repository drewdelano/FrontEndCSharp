using System;
using System.Collections.Generic;

namespace FrontEndCSharp.Debugging;

public sealed class ManagedRuntimeLocation
{
    public ManagedRuntimeLocation(
        string moduleId,
        int methodToken,
        int blockIndex,
        int instructionIndex)
    {
        ModuleId = moduleId;
        MethodToken = methodToken;
        BlockIndex = blockIndex;
        InstructionIndex = instructionIndex;
    }

    public string ModuleId { get; }
    public int MethodToken { get; }
    public int BlockIndex { get; }
    public int InstructionIndex { get; }
}

public enum ManagedRuntimeValueKind
{
    Argument,
    Local,
    LocalField,
    Field
}

public sealed class ManagedFrameLayout
{
    public ManagedFrameLayout(IReadOnlyList<ManagedRuntimeVariable> variables)
    {
        Arguments = Array.Empty<ManagedRuntimeVariable>();
        Locals = variables;
        Variables = variables;
    }

    public ManagedFrameLayout(
        IReadOnlyList<ManagedRuntimeVariable> arguments,
        IReadOnlyList<ManagedRuntimeVariable> locals)
    {
        Arguments = arguments;
        Locals = locals;
        var variables = new List<ManagedRuntimeVariable>(arguments.Count + locals.Count);
        variables.AddRange(arguments);
        variables.AddRange(locals);
        Variables = variables;
    }

    public IReadOnlyList<ManagedRuntimeVariable> Arguments { get; }
    public IReadOnlyList<ManagedRuntimeVariable> Locals { get; }
    public IReadOnlyList<ManagedRuntimeVariable> Variables { get; }
}

public sealed class ManagedRuntimeVariable
{
    public ManagedRuntimeVariable(
        string name,
        ManagedRuntimeValueKind kind,
        int index = -1,
        string? fieldName = null,
        IReadOnlyList<string>? fieldPath = null)
    {
        Name = name;
        Kind = kind;
        Index = index;
        FieldName = fieldName;
        FieldPath = fieldPath ?? Array.Empty<string>();
    }

    public string Name { get; }
    public ManagedRuntimeValueKind Kind { get; }
    public int Index { get; }
    public string? FieldName { get; }
    public IReadOnlyList<string> FieldPath { get; }
}

public sealed class ManagedRuntimeFrameSnapshot
{
    public string ModuleId { get; set; } = string.Empty;
    public int MethodToken { get; set; }
    public int BlockIndex { get; set; }
    public int InstructionIndex { get; set; }
    public bool IsLogical { get; set; }
    public List<ManagedValueSnapshot> Arguments { get; set; } = new List<ManagedValueSnapshot>();
    public List<ManagedValueSnapshot> Locals { get; set; } = new List<ManagedValueSnapshot>();
    public List<ManagedNamedValueSnapshot> Fields { get; set; } = new List<ManagedNamedValueSnapshot>();
}

public class ManagedValueSnapshot
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class ManagedNamedValueSnapshot : ManagedValueSnapshot
{
    public string Name { get; set; } = string.Empty;
}

public sealed class ManagedFrameInspection
{
    public ManagedFrameInspection(
        string methodName,
        string? sourcePath,
        int line,
        int column,
        IReadOnlyList<ManagedVariable> arguments,
        IReadOnlyList<ManagedVariable> locals,
        int ilOffset = 0,
        int methodToken = 0)
    {
        MethodName = methodName;
        SourcePath = sourcePath;
        Line = line;
        Column = column;
        Arguments = arguments;
        Locals = locals;
        IlOffset = ilOffset;
        MethodToken = methodToken;
    }

    public string MethodName { get; }
    public string? SourcePath { get; }
    public int Line { get; }
    public int Column { get; }
    public IReadOnlyList<ManagedVariable> Arguments { get; }
    public IReadOnlyList<ManagedVariable> Locals { get; }
    public int IlOffset { get; }
    public int MethodToken { get; }
}

public sealed class ManagedVariable
{
    public ManagedVariable(string name, string value, string type)
    {
        Name = name;
        Value = value;
        Type = type;
    }

    public string Name { get; }
    public string Value { get; }
    public string Type { get; }
}

public sealed class ManagedBreakpointResolution
{
    private ManagedBreakpointResolution(
        bool isResolved,
        string error,
        string moduleId,
        int methodToken,
        int blockIndex,
        int instructionIndex,
        int line,
        int column,
        int ilOffset,
        string methodName,
        string? sourcePath)
    {
        IsResolved = isResolved;
        Error = error;
        ModuleId = moduleId;
        MethodToken = methodToken;
        BlockIndex = blockIndex;
        InstructionIndex = instructionIndex;
        Line = line;
        Column = column;
        IlOffset = ilOffset;
        MethodName = methodName;
        SourcePath = sourcePath;
    }

    public bool IsResolved { get; }
    public string Error { get; }
    public string ModuleId { get; }
    public int MethodToken { get; }
    public int BlockIndex { get; }
    public int InstructionIndex { get; }
    public int Line { get; }
    public int Column { get; }
    public int IlOffset { get; }
    public string MethodName { get; }
    public string? SourcePath { get; }

    public static ManagedBreakpointResolution Resolved(
        string moduleId,
        int methodToken,
        int blockIndex,
        int instructionIndex,
        int line,
        int column,
        int ilOffset = 0,
        string methodName = "",
        string? sourcePath = null) => new ManagedBreakpointResolution(
            true,
            string.Empty,
            moduleId,
            methodToken,
            blockIndex,
            instructionIndex,
            line,
            column,
            ilOffset,
            methodName,
            sourcePath);

    public static ManagedBreakpointResolution Unresolved(string error, int requestedLine) =>
        new ManagedBreakpointResolution(
            false,
            error ?? throw new ArgumentNullException(nameof(error)),
            string.Empty,
            0,
            0,
            0,
            requestedLine,
            1,
            0,
            string.Empty,
            null);
}
