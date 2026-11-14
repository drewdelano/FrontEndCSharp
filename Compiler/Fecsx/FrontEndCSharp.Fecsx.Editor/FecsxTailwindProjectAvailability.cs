using System;
using System.Collections.Generic;
using System.IO;

namespace FrontEndCSharp.Fecsx.Editor;

internal sealed class FecsxTailwindProjectConfiguration
{
    public FecsxTailwindProjectConfiguration(
        string projectPath,
        bool isAvailable,
        bool isEnabled,
        string? browserProjectPath,
        string? editorCatalogPath = null)
    {
        ProjectPath = projectPath;
        IsAvailable = isAvailable;
        IsEnabled = isEnabled;
        BrowserProjectPath = browserProjectPath;
        EditorCatalogPath = editorCatalogPath;
    }

    public string ProjectPath { get; }
    public bool IsAvailable { get; }
    public bool IsEnabled { get; }
    public string? BrowserProjectPath { get; }
    public string? EditorCatalogPath { get; }
}

internal static class FecsxTailwindProjectAvailability
{
    public static bool IsAvailable(
        string owningProjectPath,
        IEnumerable<FecsxTailwindProjectConfiguration> projects)
        => FindConfiguration(owningProjectPath, projects) is not null;

    public static string? GetEditorCatalogPath(
        string owningProjectPath,
        IEnumerable<FecsxTailwindProjectConfiguration> projects)
    {
        var configuration = FindConfiguration(owningProjectPath, projects);
        if (configuration is null || string.IsNullOrWhiteSpace(configuration.EditorCatalogPath))
        {
            return null;
        }

        var path = configuration.EditorCatalogPath!;
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Path.GetDirectoryName(configuration.ProjectPath)!, path);
        }
        return Path.GetFullPath(path);
    }

    private static FecsxTailwindProjectConfiguration? FindConfiguration(
        string owningProjectPath,
        IEnumerable<FecsxTailwindProjectConfiguration> projects)
    {
        if (string.IsNullOrWhiteSpace(owningProjectPath))
        {
            throw new ArgumentException("The owning project path is required.", nameof(owningProjectPath));
        }
        if (projects == null)
        {
            throw new ArgumentNullException(nameof(projects));
        }

        var normalizedOwner = Path.GetFullPath(owningProjectPath);
        foreach (var project in projects)
        {
            if (!project.IsAvailable || !project.IsEnabled)
            {
                continue;
            }

            var normalizedProject = Path.GetFullPath(project.ProjectPath);
            if (PathsEqual(normalizedOwner, normalizedProject))
            {
                return project;
            }

            if (string.IsNullOrWhiteSpace(project.BrowserProjectPath))
            {
                continue;
            }

            var browserProjectPath = project.BrowserProjectPath!;
            if (!Path.IsPathRooted(browserProjectPath))
            {
                browserProjectPath = Path.Combine(
                    Path.GetDirectoryName(normalizedProject)!,
                    browserProjectPath);
            }

            if (PathsEqual(normalizedOwner, Path.GetFullPath(browserProjectPath)))
            {
                return project;
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
