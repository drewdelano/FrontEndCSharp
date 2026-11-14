namespace $safeprojectname$;

public sealed class AppProps
{
    public int Count { get; set; }
    public Action Increment { get; set; } = null!;
}
