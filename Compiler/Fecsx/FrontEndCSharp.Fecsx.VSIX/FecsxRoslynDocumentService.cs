using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

namespace FrontEndCSharp.Fecsx.VSIX;

internal sealed class FecsxRoslynDocument
{
    public FecsxRoslynDocument(
        FecsxCSharpProjection projection,
        Document document,
        Project componentProject)
    {
        Projection = projection;
        Document = document;
        ComponentProject = componentProject;
    }

    public FecsxCSharpProjection Projection { get; }
    public Document Document { get; }
    public Project ComponentProject { get; }
}

internal sealed class FecsxRoslynDocumentService
{
    private const string TailwindAvailableProperty = "build_property.FrontEndCSharpTailwindAvailable";
    private const string TailwindEnabledProperty = "build_property.FrontEndCSharpTailwindEnabled";
    private const string TailwindEditorCatalogProperty =
        "build_property.FrontEndCSharpTailwindEditorCatalog";
    private const string BrowserProjectProperty = "build_property.FrontEndCSharpProject";
    private static readonly object TailwindCatalogGate = new();
    private static readonly Dictionary<string, TailwindCatalogCacheEntry> TailwindCatalogs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Workspace _workspace;
    private readonly string _filePath;

    public FecsxRoslynDocumentService(Workspace workspace, string filePath)
    {
        _workspace = workspace;
        _filePath = Path.GetFullPath(filePath);
    }

    public Project? FindOwningProject() => FindOwningProject(_workspace.CurrentSolution, _filePath);

    public bool IsTailwindAvailable()
    {
        var solution = _workspace.CurrentSolution;
        var owningProject = FindOwningProject(solution, _filePath);
        if (owningProject?.FilePath == null)
        {
            return false;
        }

        // Tailwind normally belongs to the web host while FECSX documents belong
        // to its paired browser project, so both evaluated projects participate.
        var configurations = solution.Projects
            .Where(project => project.FilePath != null)
            .Select(CreateTailwindConfiguration);
        return FecsxTailwindProjectAvailability.IsAvailable(
            owningProject.FilePath,
            configurations);
    }

    public string? GetTailwindEditorCatalogPath()
    {
        var solution = _workspace.CurrentSolution;
        var owningProject = FindOwningProject(solution, _filePath);
        if (owningProject?.FilePath == null)
        {
            return null;
        }

        var configurations = solution.Projects
            .Where(project => project.FilePath != null)
            .Select(CreateTailwindConfiguration);
        return FecsxTailwindProjectAvailability.GetEditorCatalogPath(
            owningProject.FilePath,
            configurations);
    }

    public FecsxTailwindClassCatalog? GetTailwindEditorCatalog()
    {
        var path = GetTailwindEditorCatalogPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var catalogPath = path!;

        try
        {
            var information = new FileInfo(catalogPath);
            if (!information.Exists)
            {
                return null;
            }

            lock (TailwindCatalogGate)
            {
                if (TailwindCatalogs.TryGetValue(catalogPath, out var cached)
                    && cached.Length == information.Length
                    && cached.LastWriteTimeUtc == information.LastWriteTimeUtc)
                {
                    return cached.Catalog;
                }

                var catalog = FecsxTailwindClassCatalog.FromCompressedBytes(
                    File.ReadAllBytes(catalogPath));
                TailwindCatalogs[catalogPath] = new TailwindCatalogCacheEntry(
                    information.Length,
                    information.LastWriteTimeUtc,
                    catalog);
                return catalog;
            }
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }

    public FecsxRoslynDocument? Create(string source)
    {
        var project = FindOwningProject();
        return project == null ? null : Create(source, project);
    }

    public FecsxRoslynDocument Create(string source, Project project)
    {
        var sourceDocument = project.AdditionalDocuments.FirstOrDefault(document =>
            document.FilePath != null && PathsEqual(document.FilePath, _filePath));
        if (sourceDocument != null)
        {
            project = project.Solution
                .WithAdditionalDocumentText(sourceDocument.Id, SourceText.From(source))
                .GetProject(project.Id)!;
        }

        var componentProject = project;
        project = FecsxCSharpProject.WithoutSourceGenerator(componentProject);
        var projection = FecsxCSharpProjector.Create(source);
        var document = project.AddDocument(
            $"__FecsxEditorProjection_{Guid.NewGuid():N}.cs",
            SourceText.From(projection.Text),
            filePath: _filePath + ".editor.g.cs");
        return new FecsxRoslynDocument(projection, document, componentProject);
    }

    public static Project? FindOwningProject(Solution solution, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var additionalDocumentOwner = solution.Projects.FirstOrDefault(project =>
            project.AdditionalDocuments.Any(document =>
                document.FilePath !=  null && PathsEqual(document.FilePath, fullPath)));
        if (additionalDocumentOwner !=  null)
        {
            return additionalDocumentOwner;
        }

        var normalizedFilePath = NormalizePath(fullPath);
        return solution.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project.FilePath))
            .Select(project => new
            {
                Project = project,
                Directory = Path.GetDirectoryName(NormalizePath(project.FilePath!))
            })
            .Where(candidate =>
                candidate.Directory !=  null &&
                IsWithinDirectory(normalizedFilePath, candidate.Directory))
            .OrderByDescending(candidate => candidate.Directory!.Length)
            .Select(candidate => candidate.Project)
            .FirstOrDefault();
    }

    public static Workspace? GetVisualStudioWorkspace(IServiceProvider serviceProvider)
    {
        try
        {
            const string workspaceContract =
                "Microsoft.VisualStudio.LanguageServices.VisualStudioWorkspace";
            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            return componentModel?.DefaultExportProvider
                .GetExportedValue<object>(workspaceContract) as Workspace;
        }
        catch
        {
            return null;
        }
    }

    internal static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private static FecsxTailwindProjectConfiguration CreateTailwindConfiguration(Project project)
    {
        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        var isAvailable = TryGetBoolean(options, TailwindAvailableProperty, out var available) && available;
        var isEnabled = TryGetBoolean(options, TailwindEnabledProperty, out var enabled) && enabled;
        options.TryGetValue(BrowserProjectProperty, out var browserProjectPath);
        options.TryGetValue(TailwindEditorCatalogProperty, out var editorCatalogPath);
        return new FecsxTailwindProjectConfiguration(
            project.FilePath!,
            isAvailable,
            isEnabled,
            browserProjectPath,
            editorCatalogPath);
    }

    private static bool TryGetBoolean(
        Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions options,
        string key,
        out bool value)
    {
        value = false;
        return options.TryGetValue(key, out var text) && bool.TryParse(text, out value);
    }

    private sealed class TailwindCatalogCacheEntry
    {
        public TailwindCatalogCacheEntry(
            long length,
            DateTime lastWriteTimeUtc,
            FecsxTailwindClassCatalog catalog)
        {
            Length = length;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Catalog = catalog;
        }

        public long Length { get; }
        public DateTime LastWriteTimeUtc { get; }
        public FecsxTailwindClassCatalog Catalog { get; }
    }

    private static bool IsWithinDirectory(string filePath, string directory)
    {
        var prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return filePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':')
        {
            return fullPath;
        }

        try
        {
            var driveName = root.Substring(0, 2);
            var capacity = 512;
            var remoteName = new StringBuilder(capacity);
            if (WNetGetConnection(driveName, remoteName, ref capacity) == 0)
            {
                return remoteName.ToString().TrimEnd('\\') + fullPath.Substring(2);
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }

        return fullPath;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(
        string localName,
        StringBuilder remoteName,
        ref int length);
}

internal static class FecsxProjectConfiguration
{
    public static FecsxAttributeCasing GetAttributeCasing(Project? project)
    {
        if (project == null)
        {
            return FecsxAttributeCasing.CamelCase;
        }

        var options = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        return options.TryGetValue(FecsxAttributeNaming.AnalyzerConfigPropertyName, out var value)
            && FecsxAttributeNaming.TryParse(value, out var casing)
                ? casing
                : FecsxAttributeCasing.CamelCase;
    }
}
