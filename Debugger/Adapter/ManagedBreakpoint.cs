namespace FrontEndCSharp.DebugAdapter;

internal sealed record ManagedBreakpoint(
    int Id,
    string ModuleId,
    int MethodToken,
    int BlockIndex,
    int InstructionIndex);
