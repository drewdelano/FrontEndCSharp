using FrontEndCSharp.Packaging;

if (args.Length == 6 && args[0] == "check-browser-output-state")
{
    try
    {
        return BrowserOutputState.Check(args[1], args[2], args[3], args[4], args[5]) ? 0 : 3;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("Error: " + exception.Message);
        return 1;
    }
}

if (args.Length == 4 && args[0] == "commit-browser-output-state")
{
    try
    {
        BrowserOutputState.Commit(args[1], args[2], args[3]);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("Error: " + exception.Message);
        return 1;
    }
}

if (args.Length > 0 && args[0] == "create-compiler-payload")
{
    if (args.Length is < 3 or > 4)
    {
        Console.Error.WriteLine(
            "Usage: FrontEndCSharp.Build.Tasks create-compiler-payload <source-directory> <destination-path> [edit-and-continue-source-directory]");
        return 2;
    }

    try
    {
        CompilerPayloadArchive.Create(
            args[1],
            args[2],
            args.Length == 4 ? args[3] : null);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("Error: " + exception.Message);
        return 1;
    }
}

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: FrontEndCSharp.Build.Tasks <compiler-payload.zip> <compiler-path-file>\n" +
        "   or: FrontEndCSharp.Build.Tasks create-compiler-payload <source-directory> <destination-path> [edit-and-continue-source-directory]\n" +
        "   or: FrontEndCSharp.Build.Tasks check-browser-output-state <inputs> <configuration> <outputs> <state> <candidate>\n" +
        "   or: FrontEndCSharp.Build.Tasks commit-browser-output-state <candidate> <outputs> <state>");
    return 2;
}

try
{
    var compilerPath = CompilerPayloadCache.GetToolPath(args[0], "Compiler.dll");
    var pointerPath = Path.GetFullPath(args[1]);
    var pointerDirectory = Path.GetDirectoryName(pointerPath)
        ?? throw new InvalidOperationException("The compiler path file has no parent directory.");
    Directory.CreateDirectory(pointerDirectory);
    var temporaryPath = pointerPath + ".tmp-" + Guid.NewGuid().ToString("N");
    try
    {
        File.WriteAllText(temporaryPath, compilerPath + Environment.NewLine);
        File.Move(temporaryPath, pointerPath, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("Error: " + exception.Message);
    return 1;
}
