using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace FrontEndCSharp.Fecsx.Editor;

public static class FecsxComponentNavigation
{
    public static string CreateTypeResolutionProbe(
        string source,
        string tagName,
        out FecsxTextSpan tagSpan)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(tagName)) throw new ArgumentException("A component tag name is required.", nameof(tagName));

        var syntax = FecsxRenderDocumentParser.Parse(source);
        var preamble = SyntaxFactory.ParseCompilationUnit(syntax.Preamble);
        var builder = new StringBuilder();
        AppendDirectives(builder, preamble.Externs, preamble.Usings);
        var fileNamespace = preamble.Members
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        if (fileNamespace != null)
        {
            builder.Append("namespace ").Append(fileNamespace.Name).AppendLine(";");
            AppendDirectives(builder, fileNamespace.Externs, fileNamespace.Usings);
        }

        builder.AppendLine("internal static class __FecsxComponentNavigationProbe")
            .AppendLine("{")
            .AppendLine("    private static void Resolve()")
            .AppendLine("    {")
            .Append("        _ = ");
        tagSpan = new FecsxTextSpan(builder.Length, tagName.Length);
        builder.Append(tagName).AppendLine(".Component;")
            .AppendLine("    }")
            .AppendLine("}");
        return builder.ToString();
    }

    public static bool TryGetManagedTag(
        string source,
        int position,
        out string tagName)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (position < 0 || position > source.Length) throw new ArgumentOutOfRangeException(nameof(position));

        var classification = new FecsxEditorService()
            .GetClassifications(source)
            .FirstOrDefault(item =>
                item.Kind == FecsxClassificationKind.TagName
                && item.Span.Start <= position
                && position <= item.Span.End);
        if (classification == null)
        {
            tagName = string.Empty;
            return false;
        }

        var candidate = source.Substring(
            classification.Span.Start,
            classification.Span.Length);
        var lastSegment = candidate.Substring(candidate.LastIndexOf('.') + 1);
        if (lastSegment.Length == 0 || !char.IsUpper(lastSegment[0]))
        {
            tagName = string.Empty;
            return false;
        }

        tagName = candidate;
        return true;
    }

    public static bool TryGetDeclaration(
        INamedTypeSymbol componentType,
        string filePath,
        string source,
        out FecsxTextSpan declarationNameSpan)
    {
        if (componentType == null) throw new ArgumentNullException(nameof(componentType));
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        if (source == null) throw new ArgumentNullException(nameof(source));

        var syntax = FecsxRenderDocumentParser.Parse(source, filePath);
        var namespaceName = componentType.ContainingNamespace?.IsGlobalNamespace == false
            ? componentType.ContainingNamespace.ToDisplayString()
            : null;
        if (!string.Equals(namespaceName, syntax.NamespaceName, StringComparison.Ordinal))
        {
            declarationNameSpan = default;
            return false;
        }

        var typeNames = new Stack<string>();
        for (var current = componentType; current != null; current = current.ContainingType)
        {
            typeNames.Push(current.Name);
        }

        if (typeNames.Count == 0
            || !string.Equals(typeNames.Pop(), syntax.FacadeName, StringComparison.Ordinal))
        {
            declarationNameSpan = default;
            return false;
        }

        var declarationName = typeNames.Count == 0 ? "Default" : typeNames.Pop();
        if (typeNames.Count != 0)
        {
            declarationNameSpan = default;
            return false;
        }

        var declaration = syntax.Components.FirstOrDefault(component =>
            string.Equals(
                component.Name.TrimStart('@'),
                declarationName,
                StringComparison.Ordinal));
        if (declaration == null)
        {
            declarationNameSpan = default;
            return false;
        }

        declarationNameSpan = declaration.NameSpan;
        return true;
    }

    private static void AppendDirectives(
        StringBuilder builder,
        SyntaxList<ExternAliasDirectiveSyntax> externs,
        SyntaxList<UsingDirectiveSyntax> usings)
    {
        foreach (var directive in externs)
        {
            builder.AppendLine(directive.ToFullString().Trim());
        }

        foreach (var directive in usings)
        {
            builder.AppendLine(directive.ToFullString().Trim());
        }
    }
}
