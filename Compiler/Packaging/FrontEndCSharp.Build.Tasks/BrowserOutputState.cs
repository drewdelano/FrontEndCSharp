using System.Security.Cryptography;
using System.Text;

namespace FrontEndCSharp.Packaging;

public static class BrowserOutputState
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static bool Check(
        string inputListPath,
        string configurationPath,
        string outputListPath,
        string statePath,
        string candidatePath)
    {
        var fingerprint = ComputeFingerprint(inputListPath, configurationPath);
        WriteAtomically(candidatePath, fingerprint + Environment.NewLine);
        return File.Exists(statePath)
            && string.Equals(File.ReadAllText(statePath).Trim(), fingerprint, StringComparison.Ordinal)
            && ReadPaths(outputListPath).All(File.Exists);
    }

    public static void Commit(string candidatePath, string outputListPath, string statePath)
    {
        var missingOutput = ReadPaths(outputListPath).FirstOrDefault(path => !File.Exists(path));
        if (missingOutput != null)
        {
            throw new FileNotFoundException(
                "The browser compiler did not publish a required output.",
                missingOutput);
        }
        var stateDirectory = Path.GetDirectoryName(Path.GetFullPath(statePath))!;
        Directory.CreateDirectory(stateDirectory);
        File.Move(candidatePath, statePath, overwrite: true);
    }

    public static string ComputeFingerprint(string inputListPath, string configurationPath)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFile(hash, Path.GetFullPath(configurationPath));
        foreach (var input in ReadPaths(inputListPath)
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer))
        {
            AppendFile(hash, input);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static IReadOnlyList<string> ReadPaths(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length != 0)
        .ToArray();

    private static void AppendFile(IncrementalHash hash, string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("An incremental browser-build input does not exist.", path);
        }
        var identity = Encoding.UTF8.GetBytes(
            OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path);
        hash.AppendData(BitConverter.GetBytes(identity.Length));
        hash.AppendData(identity);
        using var stream = File.OpenRead(path);
        var buffer = new byte[64 * 1024];
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            hash.AppendData(buffer, 0, count);
        }
    }

    private static void WriteAtomically(string path, string contents)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
