using FrontEndCSharp.Fecscn.Configuration;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn.Commands;

public static class FecscnCommandHost
{
    public const string DefaultRegistry = "https://registry.fecscn.dev/r/styles/base-nova";

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        string currentDirectory,
        TextWriter output,
        TextWriter error,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Count == 0 || args[0] is "--help" or "-h")
            {
                await output.WriteLineAsync(Usage());
                return 0;
            }
            var command = args[0];
            var projectArgument = ReadOption(args, "--project");
            var project = FecscnProjectDiscovery.Discover(projectArgument ?? currentDirectory);
            if (command == "init")
            {
                RejectPositionals(args, []);
                var registry = ReadOption(args, "--registry") ?? DefaultRegistry;
                var result = FecscnInitializer.Initialize(project, registry);
                await output.WriteLineAsync(result.Changed
                    ? $"Initialized fecscn in '{project.HostDirectory}'."
                    : "fecscn is already initialized.");
                return 0;
            }
            if (command == "add")
            {
                RejectUnknownOptions(args, ["--project"]);
                var names = ReadPositionals(args);
                if (names.Count == 0)
                {
                    throw new FecscnCommandException("fecscn add requires at least one component name.");
                }
                var configPath = Path.Combine(project.HostDirectory, "fecscn.json");
                if (!File.Exists(configPath))
                {
                    throw new FecscnCommandException(
                        $"fecscn is not initialized. Run 'fecscn init --project \"{project.HostProjectPath}\"' first.");
                }
                var configuration = FecscnJson.ParseProjectConfig(await File.ReadAllTextAsync(configPath, cancellationToken));
                using var ownedClient = client is null ? new HttpClient() : null;
                var registry = new HttpFecscnRegistrySource(client ?? ownedClient!, configuration.Registry);
                var plan = await FecscnRegistryResolver.ResolveAsync(registry, names, cancellationToken);
                FecscnInstaller.Install(project, plan);
                await output.WriteLineAsync(
                    $"Added {string.Join(", ", names.OrderBy(value => value, StringComparer.Ordinal))}.");
                return 0;
            }
            throw new FecscnCommandException(
                $"Unknown command '{command}'. The initial fecscn release supports only 'init' and 'add'.");
        }
        catch (Exception exception) when (exception is FecscnCommandException or FecscnConfigurationException)
        {
            await error.WriteLineAsync("fecscn: " + exception.Message);
            return 1;
        }
    }

    private static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        string? result = null;
        for (var index = 1; index < args.Count; index++)
        {
            if (args[index] != name)
            {
                continue;
            }
            if (result is not null || index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new FecscnCommandException($"Option '{name}' must be specified once with a value.");
            }
            result = args[++index];
        }
        return result;
    }

    private static IReadOnlyList<string> ReadPositionals(IReadOnlyList<string> args)
    {
        var values = new List<string>();
        for (var index = 1; index < args.Count; index++)
        {
            if (args[index].StartsWith("--", StringComparison.Ordinal))
            {
                index++;
                continue;
            }
            values.Add(args[index]);
        }
        return values;
    }

    private static void RejectPositionals(IReadOnlyList<string> args, IReadOnlyCollection<string> allowed)
    {
        RejectUnknownOptions(args, ["--project", "--registry"]);
        var positionals = ReadPositionals(args);
        if (positionals.Count != allowed.Count)
        {
            throw new FecscnCommandException("fecscn init does not accept positional arguments.");
        }
    }

    private static void RejectUnknownOptions(IReadOnlyList<string> args, IReadOnlyCollection<string> allowed)
    {
        for (var index = 1; index < args.Count; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }
            if (!allowed.Contains(args[index], StringComparer.Ordinal))
            {
                throw new FecscnCommandException($"Unknown option '{args[index]}'.");
            }
            index++;
        }
    }

    private static string Usage() => """
        fecscn init [--project <host-or-browser.csproj>] [--registry <url>]
        fecscn add <component> [component ...] [--project <host-or-browser.csproj>]

        The initial release supports only the Base UI / Nova / Lucide / Tailwind CSS v4 default profile.
        """;
}
