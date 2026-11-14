using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Compiler.Tests;

public class DocumentationOrganizationTests
{
    private static readonly Regex MarkdownDocumentLink = new(
        @"\[[^\]]+\]\((?<target>[^)\s]+\.md(?:#[^)]*)?)\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Test]
    public void AgentDocumentationHasRoutedStructureAndValidLinks()
    {
        var repositoryRoot = GetRepositoryRoot();
        var documentationRoot = Path.Combine(repositoryRoot, "aidocs");
        var expectedCoreDocuments = new[]
        {
            "DISCUSSION.md",
            "GOALS.md",
            "LESSONS_LEARNT.md",
            "README.md",
            "TODO.md",
            "WORKFLOW.md",
        };
        var coreDocuments = Directory.GetFiles(documentationRoot, "*.md")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCoreDocuments, coreDocuments);

        var documents = Directory.GetFiles(documentationRoot, "*.md", SearchOption.AllDirectories)
            .Append(Path.Combine(repositoryRoot, "AGENTS.md"))
            .Append(Path.Combine(repositoryRoot, "README.md"))
            .Append(Path.Combine(
                repositoryRoot,
                "Compiler",
                "Compiler.Tests",
                "Performance",
                "README.md"))
            .ToArray();
        foreach (var document in documents)
        {
            AssertLocalMarkdownLinksExist(document);
        }

        var permittedMissingSolutionItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AGENTS.md",
            "README.md",
            "aidocs/README.md",
        };
        var solutionDocuments = Directory.GetFiles(
                documentationRoot,
                "*.md",
                SearchOption.AllDirectories)
            .Append(Path.Combine(repositoryRoot, "AGENTS.md"))
            .Append(Path.Combine(repositoryRoot, "README.md"));
        var solution = File.ReadAllText(Path.Combine(repositoryRoot, "FrontEndCSharp.sln"));
        foreach (var document in solutionDocuments)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, document).Replace('\\', '/');
            if (permittedMissingSolutionItems.Contains(relativePath))
            {
                continue;
            }

            var solutionPath = relativePath.Replace('/', '\\');
            Assert.Contains($"\t\t{solutionPath} = {solutionPath}", solution, StringComparison.Ordinal);
        }
    }

    private static void AssertLocalMarkdownLinksExist(string document)
    {
        var text = File.ReadAllText(document);
        foreach (Match match in MarkdownDocumentLink.Matches(text))
        {
            var target = match.Groups["target"].Value;
            if (Uri.TryCreate(target, UriKind.Absolute, out _))
            {
                continue;
            }

            var fragmentIndex = target.IndexOf('#', StringComparison.Ordinal);
            var path = fragmentIndex < 0 ? target : target[..fragmentIndex];
            var resolved = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(document)!,
                path.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(
                File.Exists(resolved),
                $"Markdown document '{document}' links to missing file '{target}'.");
        }
    }

    private static string GetRepositoryRoot(
        [CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
}
