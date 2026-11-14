using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Compiler.Helpers
{
    public class PathLocator
    {
        readonly static string JsRuntimeDirectory;
        readonly static string JsBclCacheDirectory;
        readonly static BclFileInventory JsBclFiles;
        static PathLocator()
        {
            JsRuntimeDirectory = Path.Combine(Path.GetDirectoryName(typeof(PathLocator).Assembly.Location), "JsRuntime");
            JsBclCacheDirectory = Path.Combine(JsRuntimeDirectory, "bcl");
            JsBclFiles = new BclFileInventory(JsBclCacheDirectory);
        }

        public static bool HasBcl(MethodBase method) => JsBclFiles.Contains(method);

        internal static string GetModulePath(Module module)
        {
            var path = module?.FullyQualifiedName;
            if (string.IsNullOrWhiteSpace(path) || path[0] == '<')
            {
                throw new InvalidOperationException(
                    $"Module '{module?.Name ?? "<unknown>"}' is not backed by a physical file.");
            }
            return Path.GetFullPath(path);
        }

        public static void CopyBcl(MethodBase method, string target)
        {
            var bclMethod = Path.Combine(
                JsBclCacheDirectory,
                NameNormalizers.GetFileSystemSafeMethodNameWithPrefix(method));
            File.Copy(bclMethod, target, true);
        }

        public static void CopyRootFile(string target)
        {
            var rootFile = Path.Combine(JsRuntimeDirectory, Path.GetFileName(target));
            File.Copy(rootFile, target, true);
        }

        public static string[] GetRuntimeRootFileNames()
        {
            return Directory.GetFiles(JsRuntimeDirectory)
                .Select(Path.GetFileName)
                .ToArray()!;
        }

        public static void CopyFolder(string target, string directoryName)
        {
            var srcFolder = Path.Combine(JsRuntimeDirectory, directoryName);
            var targetFolder = Path.Combine(target, directoryName);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }
            foreach (var srcFile in Directory.GetFiles(srcFolder))
            {
                var targetFile = Path.Combine(targetFolder, Path.GetFileName(srcFile));
                File.Copy(srcFile, targetFile, true);
            }
        }
    }

    internal sealed class BclFileInventory
    {
        private readonly HashSet<string> fileNames;

        internal BclFileInventory(string directory)
        {
            fileNames = Load(directory);
        }

        internal bool Contains(MethodBase method) =>
            fileNames.Count > 0
            && fileNames.Contains(NameNormalizers.GetFileSystemSafeMethodNameWithPrefix(method));

        private static HashSet<string> Load(string directory)
        {
            var comparer = OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            try
            {
                if (!Directory.Exists(directory))
                {
                    return new HashSet<string>(comparer);
                }
                return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName)
                    .Where(fileName => fileName != null)
                    .ToHashSet(comparer)!;
            }
            catch (IOException)
            {
                return new HashSet<string>(comparer);
            }
            catch (UnauthorizedAccessException)
            {
                return new HashSet<string>(comparer);
            }
        }
    }
}
