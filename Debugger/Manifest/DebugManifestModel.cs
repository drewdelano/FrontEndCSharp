using System.Collections.Generic;

namespace FrontEndCSharp.Debugging;

internal sealed class DebugManifestData
{
    internal DebugManifestData(
        string buildId,
        int generation,
        string contentHash,
        IReadOnlyList<DebugManifestModule> modules)
    {
        BuildId = buildId;
        Generation = generation;
        ContentHash = contentHash;
        Modules = modules;
    }

    internal string BuildId { get; }
    internal int Generation { get; }
    internal string ContentHash { get; }
    internal IReadOnlyList<DebugManifestModule> Modules { get; }
}

internal sealed class DebugManifestModule
{
    internal DebugManifestModule(
        string moduleId,
        IReadOnlyList<DebugManifestDocument> documents,
        IReadOnlyList<DebugManifestMethod> methods)
    {
        ModuleId = moduleId;
        Documents = documents;
        Methods = methods;
    }

    internal string ModuleId { get; }
    internal IReadOnlyList<DebugManifestDocument> Documents { get; }
    internal IReadOnlyList<DebugManifestMethod> Methods { get; }
}

internal sealed class DebugManifestDocument
{
    internal DebugManifestDocument(
        int id,
        string path,
        string checksumAlgorithm,
        string checksum)
    {
        Id = id;
        Path = path;
        ChecksumAlgorithm = checksumAlgorithm;
        Checksum = checksum;
    }

    internal int Id { get; }
    internal string Path { get; }
    internal string ChecksumAlgorithm { get; }
    internal string Checksum { get; }
}

internal sealed class DebugManifestMethod
{
    internal DebugManifestMethod(
        int methodToken,
        string name,
        bool hasThis,
        int? stateMachineKickoffMethodToken,
        IReadOnlyList<DebugManifestParameter> parameters,
        IReadOnlyList<DebugManifestInstruction> instructions,
        IReadOnlyList<DebugManifestSequencePoint> sequencePoints,
        IReadOnlyList<DebugManifestLocalScope> localScopes,
        IReadOnlyList<DebugManifestHoistedLocal> hoistedLocals)
    {
        MethodToken = methodToken;
        Name = name;
        HasThis = hasThis;
        StateMachineKickoffMethodToken = stateMachineKickoffMethodToken;
        Parameters = parameters;
        Instructions = instructions;
        SequencePoints = sequencePoints;
        LocalScopes = localScopes;
        HoistedLocals = hoistedLocals;
    }

    internal int MethodToken { get; }
    internal string Name { get; }
    internal bool HasThis { get; }
    internal int? StateMachineKickoffMethodToken { get; }
    internal IReadOnlyList<DebugManifestParameter> Parameters { get; }
    internal IReadOnlyList<DebugManifestInstruction> Instructions { get; }
    internal IReadOnlyList<DebugManifestSequencePoint> SequencePoints { get; }
    internal IReadOnlyList<DebugManifestLocalScope> LocalScopes { get; }
    internal IReadOnlyList<DebugManifestHoistedLocal> HoistedLocals { get; }
}

internal sealed class DebugManifestParameter
{
    internal DebugManifestParameter(int index, string name)
    {
        Index = index;
        Name = name;
    }

    internal int Index { get; }
    internal string Name { get; }
}

internal sealed class DebugManifestInstruction
{
    internal DebugManifestInstruction(int ilOffset, int blockIndex, int instructionIndex)
    {
        IlOffset = ilOffset;
        BlockIndex = blockIndex;
        InstructionIndex = instructionIndex;
    }

    internal int IlOffset { get; }
    internal int BlockIndex { get; }
    internal int InstructionIndex { get; }
}

internal sealed class DebugManifestLocalScope
{
    internal DebugManifestLocalScope(
        int startOffset,
        int endOffset,
        IReadOnlyList<DebugManifestLocal> locals)
    {
        StartOffset = startOffset;
        EndOffset = endOffset;
        Locals = locals;
    }

    internal int StartOffset { get; }
    internal int EndOffset { get; }
    internal IReadOnlyList<DebugManifestLocal> Locals { get; }
}

internal sealed class DebugManifestLocal
{
    internal DebugManifestLocal(
        int slot,
        string name,
        bool isCompilerGeneratedClosure,
        IReadOnlyList<DebugManifestCapturedLocal> capturedLocals)
    {
        Slot = slot;
        Name = name;
        IsCompilerGeneratedClosure = isCompilerGeneratedClosure;
        CapturedLocals = capturedLocals;
    }

    internal int Slot { get; }
    internal string Name { get; }
    internal bool IsCompilerGeneratedClosure { get; }
    internal IReadOnlyList<DebugManifestCapturedLocal> CapturedLocals { get; }
}

internal sealed class DebugManifestCapturedLocal
{
    internal DebugManifestCapturedLocal(string name, IReadOnlyList<string> fieldPath)
    {
        Name = name;
        FieldPath = fieldPath;
    }

    internal string Name { get; }
    internal IReadOnlyList<string> FieldPath { get; }
}

internal sealed class DebugManifestHoistedLocal
{
    internal DebugManifestHoistedLocal(
        string fieldName,
        string name,
        int startOffset,
        int endOffset)
    {
        FieldName = fieldName;
        Name = name;
        StartOffset = startOffset;
        EndOffset = endOffset;
    }

    internal string FieldName { get; }
    internal string Name { get; }
    internal int StartOffset { get; }
    internal int EndOffset { get; }
}

internal sealed class DebugManifestSequencePoint
{
    internal DebugManifestSequencePoint(
        int ilOffset,
        int? documentId,
        bool isHidden,
        int startLine,
        int startColumn,
        int endLine,
        int? blockIndex,
        int? instructionIndex)
    {
        IlOffset = ilOffset;
        DocumentId = documentId;
        IsHidden = isHidden;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        BlockIndex = blockIndex;
        InstructionIndex = instructionIndex;
    }

    internal int IlOffset { get; }
    internal int? DocumentId { get; }
    internal bool IsHidden { get; }
    internal int StartLine { get; }
    internal int StartColumn { get; }
    internal int EndLine { get; }
    internal int? BlockIndex { get; }
    internal int? InstructionIndex { get; }
}
