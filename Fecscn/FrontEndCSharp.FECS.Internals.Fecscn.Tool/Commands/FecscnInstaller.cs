using System.Text;
using FrontEndCSharp.Fecscn.Registry;

namespace FrontEndCSharp.Fecscn.Commands;

public static class FecscnInstaller
{
    public static void Install(FecscnProject project, FecscnInstallationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(plan);
        var writes = new List<(string Path, byte[] Content)>();
        var targets = new HashSet<string>(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);
        var browserRoot = Path.GetFullPath(project.BrowserDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var browserPrefix = browserRoot + Path.DirectorySeparatorChar;

        foreach (var item in plan.Items)
        {
            foreach (var file in item.Files.OrderBy(value => value.Target, StringComparer.Ordinal))
            {
                var relative = ValidateTarget(file.Target);
                var target = Path.GetFullPath(Path.Combine(browserRoot, relative));
                if (!target.StartsWith(browserPrefix,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                    || !targets.Add(target))
                {
                    throw new FecscnCommandException(
                        $"Registry target '{file.Target}' escapes the browser project or collides with another item.");
                }
                var content = file.Content is not null
                    ? Encoding.UTF8.GetBytes(file.Content)
                    : Convert.FromBase64String(file.ContentBase64!);
                if (File.Exists(target) && !File.ReadAllBytes(target).SequenceEqual(content))
                {
                    throw new FecscnCommandException(
                        $"Installing '{item.Name}' would overwrite the locally changed file '{target}'.");
                }
                if (!File.Exists(target))
                {
                    writes.Add((target, content));
                }
            }
        }

        var packages = plan.Items
            .SelectMany(item => item.Packages ?? [])
            .GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Distinct().ToArray())
            .OrderBy(group => group[0].Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var versions in packages)
        {
            if (versions.Select(value => value.Version).Distinct(StringComparer.Ordinal).Count() != 1)
            {
                throw new FecscnCommandException(
                    $"Registry items require conflicting versions of package '{versions[0].Name}'.");
            }
            ProjectFileEditor.ValidatePackageReference(
                project.BrowserProjectPath, versions[0].Name, versions[0].Version);
        }

        foreach (var write in writes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(write.Path)!);
            var temporary = write.Path + ".fecscn.tmp";
            File.WriteAllBytes(temporary, write.Content);
            File.Move(temporary, write.Path);
        }
        foreach (var versions in packages)
        {
            ProjectFileEditor.EnsurePackageReference(
                project.BrowserProjectPath, versions[0].Name, versions[0].Version);
        }
    }

    private static string ValidateTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\')
            || path.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new FecscnCommandException(
                $"Registry target '{path}' is not a canonical project-relative path.");
        }
        return path.Replace('/', Path.DirectorySeparatorChar);
    }
}
