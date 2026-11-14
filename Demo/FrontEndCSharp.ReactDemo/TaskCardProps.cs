namespace FrontEndCSharp.ReactDemo;

public sealed class TaskCardProps
{
    public string IndexLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Meta { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public Action OnToggle { get; set; } = null!;
}
