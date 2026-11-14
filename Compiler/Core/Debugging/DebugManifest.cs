using System.Collections.Generic;

namespace Compiler.Debugging
{
    public sealed class DebugManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string BuildId { get; set; }
        public int Generation { get; set; }
        public string ContentHash { get; set; }
        public List<DebugModule> Modules { get; set; } = new List<DebugModule>();
    }

    public sealed class DebugModule
    {
        public string ModuleId { get; set; }
        public string AssemblyName { get; set; }
        public bool IsEntryModule { get; set; }
        public string PdbId { get; set; }
        public bool IsEmbeddedPdb { get; set; }
        public List<DebugDocument> Documents { get; set; } = new List<DebugDocument>();
        public List<DebugMethod> Methods { get; set; } = new List<DebugMethod>();
    }

    public sealed class DebugDocument
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string Language { get; set; }
        public string ChecksumAlgorithm { get; set; }
        public string Checksum { get; set; }
        public bool HasEmbeddedSource { get; set; }
    }

    public sealed class DebugMethod
    {
        public int MethodToken { get; set; }
        public string Name { get; set; }
        public bool HasThis { get; set; }
        public int? StateMachineKickoffMethodToken { get; set; }
        public List<DebugHoistedLocal> HoistedLocals { get; set; } = new List<DebugHoistedLocal>();
        public List<DebugParameter> Parameters { get; set; } = new List<DebugParameter>();
        public List<DebugInstructionLocation> Instructions { get; set; } = new List<DebugInstructionLocation>();
        public List<DebugSequencePoint> SequencePoints { get; set; } = new List<DebugSequencePoint>();
        public List<DebugLocalScope> LocalScopes { get; set; } = new List<DebugLocalScope>();
        public List<DebugCustomInformation> CustomDebugInformation { get; set; } = new List<DebugCustomInformation>();
    }

    public sealed class DebugHoistedLocal
    {
        public string FieldName { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
    }

    public sealed class DebugParameter
    {
        public int Index { get; set; }
        public string Name { get; set; }
    }

    public sealed class DebugInstructionLocation
    {
        public int IlOffset { get; set; }
        public int BlockIndex { get; set; }
        public int InstructionIndex { get; set; }
    }

    public sealed class DebugSequencePoint
    {
        public int IlOffset { get; set; }
        public int? DocumentId { get; set; }
        public bool IsHidden { get; set; }
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public int? BlockIndex { get; set; }
        public int? InstructionIndex { get; set; }
    }

    public sealed class DebugLocalScope
    {
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public List<DebugLocal> Locals { get; set; } = new List<DebugLocal>();
    }

    public sealed class DebugLocal
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public string Attributes { get; set; }
        public bool? IsCompilerGeneratedClosure { get; set; }
        public List<DebugCapturedLocal> CapturedLocals { get; set; }
    }

    public sealed class DebugCapturedLocal
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<string> FieldPath { get; set; } = new List<string>();
    }

    public sealed class DebugCustomInformation
    {
        public string Kind { get; set; }
        public string Value { get; set; }
    }
}
