using System.Text;
using FrontEndCSharp.Fecscn.Configuration;

namespace FrontEndCSharp.Fecscn.Commands;

public sealed record FecscnInitResult(bool Changed);

public static class FecscnInitializer
{
    public const string TailwindPackageVersion = "0.1.0";

    public static FecscnInitResult Initialize(FecscnProject project, string registry)
    {
        ArgumentNullException.ThrowIfNull(project);
        var changed = false;
        var configuration = FecscnProjectConfig.CreateDefault(registry);
        var configPath = Path.Combine(project.HostDirectory, "fecscn.json");
        var configContent = FecscnJson.SerializeProjectConfig(configuration) + Environment.NewLine;
        var cssPath = Path.Combine(project.BrowserDirectory, "Styles", "fecscn.css");
        var cssContent = "/* Application theme overrides for the fecscn Base UI / Nova default profile. */" + Environment.NewLine;
        var inputProperty = ToMsBuildPath(project.HostDirectory, cssPath);

        ValidateNewOrEqual(configPath, configContent, "fecscn configuration");
        ValidateNewOrEqual(cssPath, cssContent, "fecscn Tailwind input");
        ProjectFileEditor.ValidatePackageReference(
            project.HostProjectPath, "FrontEndCSharp.FECS.Internals.Tailwind", TailwindPackageVersion);
        ProjectFileEditor.ValidateProperty(
            project.HostProjectPath, "FrontEndCSharpTailwindInput", inputProperty);

        changed |= WriteNewOrEqual(configPath, configContent, "fecscn configuration");
        changed |= WriteNewOrEqual(
            cssPath,
            cssContent,
            "fecscn Tailwind input");

        changed |= ProjectFileEditor.EnsurePackageReference(
            project.HostProjectPath, "FrontEndCSharp.FECS.Internals.Tailwind", TailwindPackageVersion);
        changed |= ProjectFileEditor.EnsureProperty(
            project.HostProjectPath,
            "FrontEndCSharpTailwindInput",
            inputProperty);
        return new(changed);
    }

    private static bool WriteNewOrEqual(string path, string content, string description)
    {
        if (File.Exists(path))
        {
            if (string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
            {
                return false;
            }
            throw new FecscnCommandException(
                $"The existing {description} '{path}' differs; fecscn will not overwrite it.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return true;
    }

    private static void ValidateNewOrEqual(string path, string content, string description)
    {
        if (File.Exists(path) && !string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
        {
            throw new FecscnCommandException(
                $"The existing {description} '{path}' differs; fecscn will not overwrite it.");
        }
    }

    private static string ToMsBuildPath(string projectDirectory, string path)
    {
        var relative = Path.GetRelativePath(projectDirectory, path).Replace('/', '\\');
        return "$(MSBuildProjectDirectory)\\" + relative;
    }
}
