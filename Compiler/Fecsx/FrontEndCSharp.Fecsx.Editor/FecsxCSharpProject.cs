using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;

namespace FrontEndCSharp.Fecsx.Editor;

public static class FecsxCSharpProject
{
    public static Project WithoutSourceGenerator(Project project)
    {
        if (project == null) throw new ArgumentNullException(nameof(project));

        var analyzerReferences = project.AnalyzerReferences
            .Where(reference => !IsFecsxSourceGenerator(reference))
            .ToArray();
        return analyzerReferences.Length == project.AnalyzerReferences.Count
            ? project
            : project.WithAnalyzerReferences(analyzerReferences);
    }

    private static bool IsFecsxSourceGenerator(AnalyzerReference reference)
    {
        const string assemblyName = "FrontEndCSharp.Fecsx";
        return HasAssemblyName(reference.FullPath, assemblyName)
            || HasAssemblyName(reference.Display, assemblyName)
            || reference.Id is AssemblyIdentity identity
                && string.Equals(identity.Name, assemblyName, StringComparison.OrdinalIgnoreCase)
            || ContainsGeneratorFromAssembly(reference, assemblyName);
    }

    private static bool ContainsGeneratorFromAssembly(
        AnalyzerReference reference,
        string assemblyName)
    {
        try
        {
            return reference.GetGenerators(LanguageNames.CSharp).Any(generator =>
                HasAssemblyName(generator.GetType().Assembly.GetName().Name, assemblyName)
                || GetWrappedIncrementalGenerator(generator) is IIncrementalGenerator incrementalGenerator
                    && HasAssemblyName(
                        incrementalGenerator.GetType().Assembly.GetName().Name,
                        assemblyName));
        }
        catch
        {
            // A temporarily unavailable analyzer must not take down the editor.
            return false;
        }
    }

    private static object? GetWrappedIncrementalGenerator(ISourceGenerator generator) =>
        generator.GetType()
            .GetProperty(
                "Generator",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(generator);

    private static bool HasAssemblyName(string? value, string assemblyName) =>
        !string.IsNullOrWhiteSpace(value)
        && (string.Equals(value, assemblyName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                Path.GetFileNameWithoutExtension(value),
                assemblyName,
                StringComparison.OrdinalIgnoreCase));
}
