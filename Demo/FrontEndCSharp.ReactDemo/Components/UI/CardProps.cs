namespace Fecscn.Components.UI;

public enum CardSize
{
    Default,
    Small
}

public sealed class CardProps
{
    public string? ClassName { get; set; }
    public CardSize Size { get; set; }
}

public sealed class CardPartProps
{
    public string? ClassName { get; set; }
}
