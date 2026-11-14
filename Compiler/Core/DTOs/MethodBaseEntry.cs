using System.Reflection;

namespace Compiler.DTOs;

public class MethodBaseEntry
{
    public MethodBase MethodBase { get; set; }
    public MethodBaseEntry Caller { get; set; }
    public int Order { get; set; }

    public override string ToString() => $"{MethodBase.DeclaringType} -> {MethodBase}";
}
