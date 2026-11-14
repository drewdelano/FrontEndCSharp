using System.Text.RegularExpressions;

namespace Debugger.Tests;

// Pinned minimal snapshot of the Visual Studio 2022 17.11 Microsoft
// registration, overlaid with the actual product pkgdef. This checks the
// category-overlay semantics; current-host physical verification remains the
// compatibility authority for other Visual Studio 17.x versions.
internal sealed class VisualStudioExceptionRegistration
{
    internal const string JavaScriptEngine = "394120B6-2FF9-4D0D-8953-913EF5CD0BCD";
    internal const string ManagedEngine = "9E606F29-A5D4-4EA6-879B-358B21626EEF";
    internal const string JavaScriptCategory = "1ce4d9f7-faac-42ee-b352-87fdfb6fb37b";
    private readonly Dictionary<string, Dictionary<string, string>> sections = new(StringComparer.OrdinalIgnoreCase);

    internal VisualStudioExceptionRegistration()
    {
        // Microsoft JSDiagnostics/Debugger/DebugAdapterExtensions.pkgdef.
        Read("[$RootKey$\\AD7Metrics\\Engine\\{" + JavaScriptEngine + "}]\n" +
            "\"ExceptionBreakpointCategory\"=\"{" + JavaScriptCategory + "}\"\n" +
            "\"CustomProtocolExtension\"=\"{D136063F-F239-4BFC-93CE-C0CF4C4BCFAC}\"\n" +
            "[$RootKey$\\AD7Metrics\\Engine\\{" + JavaScriptEngine + "}\\ExceptionBreakpointMappings]\n" +
            "\"All Exceptions\"=\"all\"\n\"Uncaught Exceptions\"=\"uncaught\"\n" +
            "[$RootKey$\\AD7Metrics\\Exception\\{" + JavaScriptCategory + "}\\JavaScript Exceptions\\All Exceptions]\n" +
            "\"State\"=dword:00000000\n\"Code\"=dword:00000000\n" +
            "[$RootKey$\\AD7Metrics\\Exception\\{" + JavaScriptCategory + "}\\JavaScript Exceptions\\Uncaught Exceptions]\n" +
            "\"State\"=dword:00000001\n\"Code\"=dword:00000000\n");
        Read(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "AdapterRegistration.pkgdef")));
    }

    internal string Metric(string engine, string name) => sections[EnginePath(engine)][name];

    internal Dictionary<string, bool> AccessibleFilters(string engine)
    {
        var prefix = @"$RootKey$\AD7Metrics\Exception\" + Metric(engine, "ExceptionBreakpointCategory") + "\\";
        var filters = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var mapping in sections[EnginePath(engine) + @"\ExceptionBreakpointMappings"])
        {
            var rows = sections.Where(section => section.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && section.Key.EndsWith("\\" + mapping.Key, StringComparison.OrdinalIgnoreCase)
                && section.Value.ContainsKey("Code") && section.Value.ContainsKey("State")).ToArray();
            if (rows.Length > 1) throw new InvalidDataException("Ambiguous filter row: " + mapping.Key);
            if (rows.Length == 1) filters.Add(mapping.Value, (Convert.ToInt32(rows[0].Value["State"], 16) & 1) != 0);
        }
        return filters;
    }

    private static string EnginePath(string engine) => @"$RootKey$\AD7Metrics\Engine\{" + engine + "}";

    private void Read(string source)
    {
        Dictionary<string, string>? section = null;
        foreach (var line in source.Split('\n').Select(line => line.Trim()))
        {
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var key = line[1..^1];
                if (!sections.TryGetValue(key, out section)) sections.Add(key, section = new(StringComparer.OrdinalIgnoreCase));
                continue;
            }
            var value = Regex.Match(line, "^\"([^\"]+)\"=(?:\"([^\"]*)\"|dword:([0-9a-fA-F]+))$");
            if (section == null || !value.Success) throw new InvalidDataException("Unsupported pkgdef fixture line: " + line);
            section[value.Groups[1].Value] = value.Groups[2].Success ? value.Groups[2].Value : value.Groups[3].Value;
        }
    }
}
