using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string ExpectedPackageVersion = "1.72.0";
const string ManagedNamespace = "Interop.JS.LucideIcons";

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: Interop.JS.LucideIcons.Generator <lucide-react dist/esm/icons> <adapter-root>");
    return 1;
}

var iconDirectory = Path.GetFullPath(args[0]);
var adapterRoot = Path.GetFullPath(args[1]);
if (!Directory.Exists(iconDirectory))
{
    throw new DirectoryNotFoundException($"Lucide icon module directory not found: {iconDirectory}");
}

var packageRoot = Directory.GetParent(iconDirectory)?.Parent?.Parent?.FullName
    ?? throw new InvalidOperationException("Could not resolve the lucide-react package root.");
using (var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "package.json"))))
{
    var version = package.RootElement.GetProperty("version").GetString();
    if (!string.Equals(version, ExpectedPackageVersion, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Expected lucide-react {ExpectedPackageVersion}, found {version ?? "<missing>"}.");
    }
}

var declarationsPath = Path.Combine(packageRoot, "dist", "lucide-react.d.ts");
var deprecatedDeclarationPattern = new Regex(
    @"/\*\*(?<comment>.*?)\*/\s*declare const (?<name>[A-Za-z_$][A-Za-z0-9_$]*): LucideIcon;",
    RegexOptions.CultureInvariant | RegexOptions.Singleline);
var deprecatedComponents = deprecatedDeclarationPattern.Matches(File.ReadAllText(declarationsPath))
    .Where(match => match.Groups["comment"].Value.Contains("@deprecated", StringComparison.Ordinal))
    .Select(match => match.Groups["name"].Value)
    .ToHashSet(StringComparer.Ordinal);
if (deprecatedComponents.Count != 20)
{
    throw new InvalidDataException(
        $"Expected 20 deprecated Lucide icon declarations, found {deprecatedComponents.Count}. Review the pinned package before regenerating.");
}

var declarationPattern = new Regex(
    @"const\s+(?<name>[A-Za-z_$][A-Za-z0-9_$]*)\s*=\s*createLucideIcon\(__iconData\);",
    RegexOptions.CultureInvariant);
var dataNamePattern = new Regex(
    "name:\\s*\"(?<name>[^\"]+)\"",
    RegexOptions.CultureInvariant);
var icons = new List<IconDefinition>();
var excludedDeprecatedComponents = new HashSet<string>(StringComparer.Ordinal);

foreach (var modulePath in Directory.EnumerateFiles(iconDirectory, "*.mjs")
    .OrderBy(Path.GetFileName, StringComparer.Ordinal))
{
    var source = File.ReadAllText(modulePath);
    if (!source.Contains("createLucideIcon", StringComparison.Ordinal))
    {
        continue;
    }

    var declaration = declarationPattern.Match(source);
    var dataName = dataNamePattern.Match(source);
    if (!declaration.Success || !dataName.Success)
    {
        throw new InvalidDataException($"Canonical Lucide module has an unsupported shape: {modulePath}");
    }

    var componentName = declaration.Groups["name"].Value;
    if (deprecatedComponents.Contains(componentName))
    {
        excludedDeprecatedComponents.Add(componentName);
        continue;
    }
    if (!Regex.IsMatch(componentName, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
    {
        throw new InvalidDataException($"Lucide export is not a valid C# identifier: {componentName}");
    }

    icons.Add(new IconDefinition(
        componentName + "Icon",
        dataName.Groups["name"].Value,
        Path.GetFileName(modulePath)));
}

var duplicateExports = icons.GroupBy(icon => icon.ExportName, StringComparer.Ordinal)
    .Where(group => group.Count() != 1)
    .Select(group => group.Key)
    .ToArray();
var duplicateDataNames = icons.GroupBy(icon => icon.DataName, StringComparer.Ordinal)
    .Where(group => group.Count() != 1)
    .Select(group => group.Key)
    .ToArray();
if (duplicateExports.Length != 0 || duplicateDataNames.Length != 0)
{
    throw new InvalidDataException(
        "The canonical Lucide inventory contains duplicate exports or icon data names: "
        + string.Join(", ", duplicateExports.Concat(duplicateDataNames)));
}
if (!excludedDeprecatedComponents.SetEquals(deprecatedComponents))
{
    throw new InvalidDataException(
        "Deprecated Lucide declarations did not map exactly to canonical icon modules: "
        + string.Join(", ", deprecatedComponents.Except(excludedDeprecatedComponents)));
}

icons.Sort((left, right) => StringComparer.Ordinal.Compare(left.ExportName, right.ExportName));
if (icons.Count != 2032)
{
    throw new InvalidDataException(
        $"Expected 2,032 non-deprecated canonical Lucide icons, found {icons.Count}. Review the pinned package before regenerating.");
}

var managedGeneratedDirectory = Path.Combine(adapterRoot, "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", "Generated");
var companionGeneratedDirectory = Path.Combine(adapterRoot, "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs", "Generated");
var overridesDirectory = Path.Combine(adapterRoot, "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs", "Overrides");
ValidateOutputDirectory(overridesDirectory, "Overrides", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs");
Directory.CreateDirectory(managedGeneratedDirectory);
Directory.CreateDirectory(companionGeneratedDirectory);
Directory.CreateDirectory(overridesDirectory);

File.WriteAllText(
    Path.Combine(managedGeneratedDirectory, "LucideIcons.g.cs"),
    CreateFacades(icons),
    new UTF8Encoding(false));
File.WriteAllText(
    Path.Combine(managedGeneratedDirectory, "LucideIconImports.g.cs"),
    CreateImports(icons),
    new UTF8Encoding(false));
File.WriteAllText(
    Path.Combine(companionGeneratedDirectory, "LucideIconOverrides.g.cs"),
    CreateProviders(icons),
    new UTF8Encoding(false));

foreach (var staleOverride in Directory.EnumerateFiles(overridesDirectory, "method.*.js"))
{
    File.Delete(staleOverride);
}

var manifestEntries = new List<ManifestEntry>(icons.Count);
foreach (var icon in icons)
{
    var signature = $"{ManagedNamespace}.LucideIconImports.{icon.ExportName}()";
    var fileName = GetOverrideFileName(signature);
    File.WriteAllText(
        Path.Combine(overridesDirectory, fileName),
        CreateJavaScriptOverride(icon.ExportName, signature),
        new UTF8Encoding(false));
    manifestEntries.Add(new ManifestEntry(signature, fileName));
}

manifestEntries.Sort((left, right) => StringComparer.Ordinal.Compare(left.File, right.File));
File.WriteAllText(
    Path.Combine(overridesDirectory, "manifest.json"),
    JsonSerializer.Serialize(
        new Manifest(1, manifestEntries),
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + Environment.NewLine,
    new UTF8Encoding(false));

Console.WriteLine(
    $"Generated {icons.Count:N0} non-deprecated canonical Lucide facades, providers, and static imports from lucide-react {ExpectedPackageVersion}.");
return 0;

static string CreateFacades(IEnumerable<IconDefinition> icons)
{
    var source = GeneratedHeader();
    source.AppendLine("using System;");
    source.AppendLine("using Interop.JS.React;");
    source.AppendLine();
    source.AppendLine($"namespace {ManagedNamespace};");
    source.AppendLine();
    source.AppendLine("public static partial class LucideIcons");
    source.AppendLine("{");
    foreach (var icon in icons)
    {
        source.AppendLine($"    public static class {icon.ExportName}");
        source.AppendLine("    {");
        source.AppendLine("        public static ReactElement Create(Action<LucideIconProps> configure, params ReactNode[] children) =>");
        source.AppendLine($"            CreateElement(LucideIconHandles.{icon.ExportName}(), configure, children);");
        source.AppendLine();
        source.AppendLine("        public static ReactElement Create(LucideIconProps? props, params ReactNode[] children) =>");
        source.AppendLine($"            CreateElement(LucideIconHandles.{icon.ExportName}(), props, children);");
        source.AppendLine("    }");
        source.AppendLine();
    }
    source.AppendLine("}");
    return source.ToString();
}

static string CreateImports(IEnumerable<IconDefinition> icons)
{
    var source = GeneratedHeader();
    source.AppendLine("using System.Runtime.CompilerServices;");
    source.AppendLine("using Interop.JS.React;");
    source.AppendLine();
    source.AppendLine($"namespace {ManagedNamespace};");
    source.AppendLine();
    source.AppendLine("internal static class LucideIconHandles");
    source.AppendLine("{");
    foreach (var icon in icons)
    {
        source.AppendLine($"    internal static ImportedReactComponent {icon.ExportName}() => LucideIconImports.{icon.ExportName}();");
    }
    source.AppendLine("}");
    source.AppendLine();
    source.AppendLine("internal static class LucideIconImports");
    source.AppendLine("{");
    foreach (var icon in icons)
    {
        source.AppendLine("    [MethodImpl(MethodImplOptions.InternalCall)]");
        source.AppendLine($"    internal static extern ImportedReactComponent {icon.ExportName}();");
        source.AppendLine();
    }
    source.AppendLine("}");
    return source.ToString();
}

static string CreateProviders(IEnumerable<IconDefinition> icons)
{
    var source = GeneratedHeader();
    source.AppendLine("using System.Runtime.CompilerServices;");
    source.AppendLine("using FrontEndCSharp.RuntimeOverrides;");
    source.AppendLine("using Interop.JS.LucideIcons;");
    source.AppendLine("using Interop.JS.React;");
    source.AppendLine();
    source.AppendLine("namespace Interop.JS.LucideIcons.Overrides;");
    source.AppendLine();
    source.AppendLine("[RuntimeOverrideType(typeof(LucideIconImports))]");
    source.AppendLine("internal static class LucideIconOverrides");
    source.AppendLine("{");
    foreach (var icon in icons)
    {
        source.AppendLine("    [JavaScriptOverride]");
        source.AppendLine("    [MethodImpl(MethodImplOptions.InternalCall)]");
        source.AppendLine($"    internal static extern ImportedReactComponent {icon.ExportName}();");
        source.AppendLine();
    }
    source.AppendLine("}");
    return source.ToString();
}

static string CreateJavaScriptOverride(string exportName, string signature) =>
    $$"""
    import { {{exportName}} } from 'lucide-react';
    import { wrap } from '../msil-type-helpers.js';

    globalThis.DotNet.Methods['{{signature}}'] = function () {
        return wrap({{exportName}}, 'Interop.JS.React.ImportedReactComponent');
    };

    """;

static StringBuilder GeneratedHeader()
{
    var source = new StringBuilder();
    source.AppendLine("// <auto-generated />");
    source.AppendLine("// Generated by Interop.JS.LucideIcons.Generator. Do not edit by hand.");
    source.AppendLine();
    return source;
}

static string GetOverrideFileName(string signature)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signature));
    return $"method.{Convert.ToHexString(hash).ToLowerInvariant()}.js";
}

static void ValidateOutputDirectory(string path, string expectedName, string expectedParentName)
{
    var directory = new DirectoryInfo(path);
    if (!string.Equals(directory.Name, expectedName, StringComparison.Ordinal)
        || !string.Equals(directory.Parent?.Name, expectedParentName, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Refusing to replace generated files outside the expected adapter directory: {path}");
    }
}

internal sealed record IconDefinition(string ExportName, string DataName, string ModuleFile);
internal sealed record Manifest(int Version, IReadOnlyList<ManifestEntry> Overrides);
internal sealed record ManifestEntry(string Signature, string File);
