namespace FrontEndCSharp.Compiler.Integration;

internal static class RepositoryLayout
{
    public static string Root { get; } = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "..",
        ".."));
}
