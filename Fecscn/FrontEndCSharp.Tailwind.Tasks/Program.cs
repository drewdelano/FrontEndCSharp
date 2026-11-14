using FrontEndCSharp.Tailwind;

if (args.Length == 3 && string.Equals(args[0], "create-payload", StringComparison.Ordinal))
{
    try
    {
        TailwindPayloadArchive.Create(args[1], args[2]);
        return 0;
    }
    catch (Exception exception) when (exception is TailwindBuildException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

if (args.Length == 3 && string.Equals(args[0], "generate-editor-catalog", StringComparison.Ordinal))
{
    try
    {
        TailwindEditorCatalogGenerator.Generate(args[1], args[2]);
        Console.WriteLine($"Generated Tailwind editor catalog: {Path.GetFullPath(args[2])}");
        return 0;
    }
    catch (Exception exception) when (exception is TailwindBuildException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

if (args.Length != 9 || !string.Equals(args[0], "compile", StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        "Usage: FrontEndCSharp.Tailwind.Tasks create-payload <source-directory> <archive>\n"
        + "   or: FrontEndCSharp.Tailwind.Tasks generate-editor-catalog <package-directory> <catalog.tsv.gz>\n"
        + "   or: FrontEndCSharp.Tailwind.Tasks compile <project> <tool-archive> <cache> <theme-input-or-empty> <output> <state> <configuration> <sources-file>");
    return 2;
}

try
{
    var sources = File.Exists(args[8])
        ? File.ReadAllLines(args[8]).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray()
        : throw new TailwindBuildException($"The Tailwind sources file does not exist: '{args[8]}'.");
    var result = await TailwindCompiler.CompileAsync(new(
        ProjectDirectory: args[1],
        ToolRoot: null,
        ToolArchive: args[2],
        CacheDirectory: args[3],
        ThemeInputPath: string.IsNullOrWhiteSpace(args[4]) ? null : args[4],
        OutputPath: args[5],
        StatePath: args[6],
        Configuration: args[7],
        Sources: sources));
    Console.WriteLine(result == TailwindCompileResult.Built
        ? $"Generated Tailwind CSS: {Path.GetFullPath(args[5])}"
        : "Tailwind CSS is up to date.");
    return 0;
}
catch (Exception exception) when (exception is TailwindBuildException or IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
