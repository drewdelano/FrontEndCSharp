using FrontEndCSharp.Fecscn.Registry;

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: FrontEndCSharp.Fecscn.RegistryTool <registry.json> <output-directory>");
    return 2;
}

try
{
    RegistryBuilder.BuildFromFile(args[0], args[1]);
    return 0;
}
catch (FecscnRegistryException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
