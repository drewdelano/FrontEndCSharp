using Compiler.Write;
using System;
using System.Diagnostics;
using System.IO;

namespace Compiler.Helpers
{
    internal static class JavaScriptBundler
    {
        public static void Bundle(
            string entryPath,
            string outputPath,
            string sourceMapRoot,
            JavaScriptSourceMapMode sourceMapMode = JavaScriptSourceMapMode.Managed)
        {
            var bundlerPath = Path.Combine(
                Path.GetDirectoryName(typeof(JavaScriptBundler).Assembly.Location)!,
                "JsBundler",
                "index.cjs");
            if (!File.Exists(bundlerPath))
            {
                throw new FileNotFoundException(
                    "The packaged FrontEndCSharp JavaScript bundler was not found.",
                    bundlerPath);
            }

            var stagingDirectory = Path.Combine(
                Path.GetTempPath(),
                $"FrontEndCSharp-Webpack-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);
            var stagedOutput = Path.Combine(stagingDirectory, "index.js");

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                startInfo.ArgumentList.Add(bundlerPath);
                startInfo.ArgumentList.Add(Path.GetFullPath(entryPath));
                startInfo.ArgumentList.Add(stagedOutput);
                startInfo.ArgumentList.Add(Path.GetFullPath(sourceMapRoot));
                startInfo.ArgumentList.Add(sourceMapMode switch
                {
                    JavaScriptSourceMapMode.Managed => "managed",
                    JavaScriptSourceMapMode.Full => "full",
                    _ => throw new ArgumentOutOfRangeException(nameof(sourceMapMode)),
                });

                using var process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException("Unable to start Node.js for webpack bundling.");
                var standardOutput = process.StandardOutput.ReadToEndAsync();
                var standardError = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                var output = standardOutput.GetAwaiter().GetResult();
                var error = standardError.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Webpack failed with exit code {process.ExitCode}.{Environment.NewLine}{output}{error}");
                }

                File.Copy(stagedOutput, outputPath, overwrite: true);
                var stagedSourceMap = stagedOutput + ".map";
                if (File.Exists(stagedSourceMap))
                {
                    File.Copy(stagedSourceMap, outputPath + ".map", overwrite: true);
                }
                var outputDirectory = Path.GetDirectoryName(outputPath)!;
                foreach (var stalePackage in Directory.GetFiles(outputDirectory, "msil.*.bin"))
                {
                    File.Delete(stalePackage);
                }
                File.Delete(entryPath);
            }
            finally
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, recursive: true);
                }
            }
        }
    }
}
