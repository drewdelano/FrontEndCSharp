using System.IO.Compression;
using System.Net;
using System.Xml.Linq;

namespace FrontEndCSharp.Debugger.Integration;

internal static class NuGetPackageMetadataVerifier
{
    private const string ExpectedLicense = "MIT";
    private const string ExpectedRepositoryUrl =
        "https://github.com/drewdelano/FrontEndCSharp.Private";
    private const string NuGetFlatContainer =
        "https://api.nuget.org/v3-flatcontainer";

    public static void Verify(string directory, int expectedPackageCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (expectedPackageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedPackageCount));
        }

        var (fullDirectory, packagePaths) = GetPackagePaths(directory, expectedPackageCount);

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packagePath in packagePaths)
        {
            VerifyPackage(packagePath, identities);
        }

        Console.WriteLine(
            $"Verified public metadata and readmes for {packagePaths.Length} NuGet packages "
            + $"in '{fullDirectory}'.");
    }

    public static void VerifyNuGetOrgAvailability(string directory, int expectedPackageCount)
    {
        var (fullDirectory, packagePaths) = GetPackagePaths(directory, expectedPackageCount);
        var identities = ReadPackageIdentities(packagePaths, fullDirectory);
        var existing = new List<string>();
        using var client = CreateNuGetClient();

        foreach (var identity in identities)
        {
            if (IsPublished(client, identity))
            {
                existing.Add(identity.DisplayName);
            }
        }

        if (existing.Count != 0)
        {
            throw new InvalidOperationException(
                "The following package identities already exist on nuget.org; assign new versions "
                + "before publishing: " + string.Join(", ", existing));
        }

        Console.WriteLine(
            $"Verified that all {packagePaths.Length} package identities are available on nuget.org.");
    }

    public static void VerifyNuGetOrgPublication(string directory, int expectedPackageCount)
    {
        var (fullDirectory, packagePaths) = GetPackagePaths(directory, expectedPackageCount);
        var remaining = ReadPackageIdentities(packagePaths, fullDirectory)
            .ToDictionary(identity => identity.DisplayName, StringComparer.OrdinalIgnoreCase);
        var deadline = DateTimeOffset.UtcNow.AddMinutes(10);
        using var client = CreateNuGetClient();

        while (remaining.Count != 0)
        {
            foreach (var (displayName, identity) in remaining.ToArray())
            {
                if (IsPublished(client, identity))
                {
                    remaining.Remove(displayName);
                }
            }
            if (remaining.Count == 0)
            {
                break;
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    "nuget.org did not make the following published identities available within "
                    + "10 minutes: " + string.Join(", ", remaining.Keys));
            }
            Console.WriteLine(
                $"Waiting for nuget.org to expose {remaining.Count} published package identities...");
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }

        Console.WriteLine(
            $"Verified all {packagePaths.Length} published package identities on nuget.org.");
    }

    private static void VerifyPackage(string packagePath, ISet<string> identities)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var metadata = ReadMetadata(archive, packagePath);
        var id = RequiredValue(metadata, "id", packagePath);
        var version = RequiredValue(metadata, "version", packagePath);
        var identity = id + "/" + version;
        if (!identities.Add(identity))
        {
            throw new InvalidOperationException(
                $"Duplicate package identity '{identity}' in '{Path.GetDirectoryName(packagePath)}'.");
        }

        RequiredValue(metadata, "authors", packagePath);
        var description = RequiredValue(metadata, "description", packagePath);
        if (description.Equals("Package Description", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Package '{identity}' still uses the placeholder package description.");
        }

        var license = RequiredElement(metadata, "license", packagePath);
        if (!string.Equals((string?)license.Attribute("type"), "expression", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(license.Value.Trim(), ExpectedLicense, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Package '{identity}' must use the '{ExpectedLicense}' license expression.");
        }

        var projectUrl = RequiredValue(metadata, "projectUrl", packagePath);
        if (!projectUrl.Equals(ExpectedRepositoryUrl, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Package '{identity}' has unexpected project URL '{projectUrl}'.");
        }

        var repository = RequiredElement(metadata, "repository", packagePath);
        var repositoryUrl = ((string?)repository.Attribute("url"))?.Trim();
        var repositoryType = ((string?)repository.Attribute("type"))?.Trim();
        if (!string.Equals(repositoryUrl, ExpectedRepositoryUrl, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(repositoryType, "git", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Package '{identity}' must identify the FrontEndCSharp Git repository.");
        }

        RequiredValue(metadata, "tags", packagePath);
        var readme = RequiredValue(metadata, "readme", packagePath).Replace('\\', '/');
        if (!archive.Entries.Any(
                entry => entry.FullName.Replace('\\', '/').Equals(
                    readme,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Package '{identity}' declares missing readme '{readme}'.");
        }
    }

    private static XElement ReadMetadata(ZipArchive archive, string packagePath)
    {
        var nuspecEntries = archive.Entries
            .Where(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nuspecEntries.Length != 1)
        {
            throw new InvalidOperationException(
                $"Package '{packagePath}' must contain exactly one .nuspec file; "
                + $"found {nuspecEntries.Length}.");
        }

        XDocument document;
        using (var stream = nuspecEntries[0].Open())
        {
            document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }

        return document.Root?.Elements().SingleOrDefault(
            element => element.Name.LocalName == "metadata")
            ?? throw new InvalidOperationException(
                $"Package '{packagePath}' has no nuspec metadata element.");
    }

    private static PackageIdentity[] ReadPackageIdentities(
        IEnumerable<string> packagePaths,
        string directory)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return packagePaths.Select(packagePath =>
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var metadata = ReadMetadata(archive, packagePath);
            var identity = new PackageIdentity(
                RequiredValue(metadata, "id", packagePath),
                RequiredValue(metadata, "version", packagePath));
            if (!unique.Add(identity.DisplayName))
            {
                throw new InvalidOperationException(
                    $"Duplicate package identity '{identity.DisplayName}' in '{directory}'.");
            }
            return identity;
        }).ToArray();
    }

    private static HttpClient CreateNuGetClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FrontEndCSharp-release-verifier/1.0");
        return client;
    }

    private static bool IsPublished(HttpClient client, PackageIdentity identity)
    {
        var idSegment = Uri.EscapeDataString(identity.Id.ToLowerInvariant());
        var versionSegment = Uri.EscapeDataString(identity.Version.ToLowerInvariant());
        var packageUrl =
            $"{NuGetFlatContainer}/{idSegment}/{versionSegment}/{idSegment}.{versionSegment}.nupkg";
        using var request = new HttpRequestMessage(HttpMethod.Get, packageUrl);
        using var response = client.Send(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            CancellationToken.None);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        throw new HttpRequestException(
            $"nuget.org returned HTTP {(int)response.StatusCode} "
            + $"while checking package '{identity.DisplayName}'.");
    }

    private static (string FullDirectory, string[] PackagePaths) GetPackagePaths(
        string directory,
        int expectedPackageCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (expectedPackageCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedPackageCount));
        }

        var fullDirectory = Path.GetFullPath(directory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException(
                $"NuGet package directory does not exist: '{fullDirectory}'.");
        }

        var packagePaths = Directory.GetFiles(fullDirectory, "*.nupkg")
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (packagePaths.Length != expectedPackageCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedPackageCount} NuGet packages in '{fullDirectory}', "
                + $"found {packagePaths.Length}.");
        }
        return (fullDirectory, packagePaths);
    }

    private static XElement RequiredElement(XElement metadata, string name, string packagePath) =>
        metadata.Elements().SingleOrDefault(element => element.Name.LocalName == name)
        ?? throw new InvalidOperationException(
            $"Package '{packagePath}' is missing nuspec metadata '{name}'.");

    private static string RequiredValue(XElement metadata, string name, string packagePath)
    {
        var value = RequiredElement(metadata, name, packagePath).Value.Trim();
        if (value.Length == 0)
        {
            throw new InvalidOperationException(
                $"Package '{packagePath}' has empty nuspec metadata '{name}'.");
        }
        return value;
    }

    private sealed record PackageIdentity(string Id, string Version)
    {
        public string DisplayName => Id + "/" + Version;
    }
}
