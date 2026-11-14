namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpManagedFrame
{
    public string? CallFrameId { get; set; }
    public string ModuleId { get; set; } = string.Empty;
    public int MethodToken { get; set; }
    public int BlockIndex { get; set; }
    public int InstructionIndex { get; set; }
    public int DebugDepth { get; set; }
    public string StopReason { get; set; } = string.Empty;
    public List<int> BreakpointIds { get; set; } = [];
    public string? ExceptionStage { get; set; }
    public string? ExceptionType { get; set; }
    public List<ManagedValueSnapshot> Arguments { get; set; } = [];
    public List<ManagedValueSnapshot> Locals { get; set; } = [];
    public List<ManagedNamedValueSnapshot> Fields { get; set; } = [];
    public List<ManagedObjectSnapshot> Objects { get; set; } = [];
    public bool IsLogical { get; set; }
    public List<CdpManagedFrame> AsyncParents { get; set; } = [];
}

internal sealed class CdpExceptionSnapshot
{
    public string ExceptionStage { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public List<CdpManagedFrame> Frames { get; set; } = [];
}

internal sealed class RuntimeUpdateAcknowledgement
{
    public string BaselineBuildId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string Hash { get; set; } = string.Empty;
}
