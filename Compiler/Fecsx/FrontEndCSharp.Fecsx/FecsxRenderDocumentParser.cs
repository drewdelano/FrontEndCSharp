using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace FrontEndCSharp.Fecsx;

public static class FecsxRenderDocumentParser
{
    private static readonly Regex FirstDeclarationPattern = new Regex(
        @"(?<modifiers>(?:(?:public|internal|private|protected|static|readonly|async|partial)\s+)*)(?<returnType>(?:global::)?[A-Za-z_][A-Za-z0-9_:.<>\[\],?]*)\s+(?<name>@?[A-Za-z_][A-Za-z0-9_]*)\s*(?<parameters>\([\s\S]*?\))\s*(?<bodyIntroducer>=>|\{)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DeclarationPattern = new Regex(
        @"\G(?<modifiers>(?:(?:public|internal|private|protected|static|readonly|async|partial)\s+)*)(?<returnType>(?:global::)?[A-Za-z_][A-Za-z0-9_:.<>\[\],?]*)\s+(?<name>@?[A-Za-z_][A-Za-z0-9_]*)\s*(?<parameters>\([\s\S]*?\))\s*(?<bodyIntroducer>=>|\{)",
        RegexOptions.CultureInvariant);

    public static FecsxRenderDocumentSyntax Parse(string source) => Parse(source, null);

    public static FecsxRenderDocumentSyntax Parse(string source, string? path)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var diagnostics = new List<FecsxDiagnostic>();
        var components = new List<FecsxComponentDeclarationSyntax>();
        var first = FindFirstDeclaration(source);
        if (first == null)
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3001",
                "Expected one or more public static FECSX component declarations using either '=> <markup>;' or '{ return <markup>; }'.",
                new FecsxTextSpan(0, source.Length)));
            return Invalid(source, GetFacadeName(path), diagnostics);
        }

        var preamble = source.Substring(0, first.Index);
        var namespaceName = ParsePreamble(preamble, diagnostics);
        var facadeName = GetFacadeName(path);
        ValidateFacadeName(facadeName, path, diagnostics);

        var position = first.Index;
        while (position < source.Length)
        {
            position = SkipTrivia(source, position);
            if (position == source.Length)
            {
                break;
            }

            var match = DeclarationPattern.Match(source, position);
            if (!match.Success)
            {
                diagnostics.Add(new FecsxDiagnostic(
                    "FECSX3001",
                    "Expected a public static FECSX component declaration.",
                    new FecsxTextSpan(position, source.Length - position)));
                break;
            }

            ValidateModifiers(match.Groups["modifiers"], diagnostics);
            ValidateReturnType(match.Groups["returnType"], diagnostics);
            var nameGroup = match.Groups["name"];
            ValidateComponentName(nameGroup, diagnostics);
            var parameterGroup = match.Groups["parameters"];
            var parameters = ParseParameters(
                parameterGroup.Value.Substring(1, parameterGroup.Value.Length - 2),
                parameterGroup.Index + 1,
                diagnostics);

            var bodyKind = match.Groups["bodyIntroducer"].Value == "=>"
                ? FecsxComponentBodyKind.Expression
                : FecsxComponentBodyKind.Block;
            var expressionOffset = SkipTrivia(source, match.Index + match.Length);
            if (bodyKind == FecsxComponentBodyKind.Block)
            {
                if (!IsKeywordAt(source, expressionOffset, "return"))
                {
                    diagnostics.Add(new FecsxDiagnostic(
                        "FECSX3013",
                        "A block-bodied FECSX component must contain exactly one 'return <markup>;' statement.",
                        new FecsxTextSpan(expressionOffset, Math.Min(1, source.Length - expressionOffset))));
                    components.Add(new FecsxComponentDeclarationSyntax(
                        nameGroup.Value,
                        parameters.PropsTypeName,
                        parameters.PropsParameterName,
                        parameters.ChildrenParameterName,
                        new FecsxTextSpan(match.Index, source.Length - match.Index),
                        new FecsxTextSpan(nameGroup.Index, nameGroup.Length),
                        bodyKind,
                        expressionOffset,
                        expressionOffset,
                        expressionOffset,
                        expressionOffset,
                        null));
                    break;
                }

                expressionOffset = SkipTrivia(source, expressionOffset + "return".Length);
            }

            var parsedExpression = ParseComponentExpression(source, expressionOffset, diagnostics);

            var end = SkipTrivia(source, parsedExpression.ExpressionEnd);
            if (end >= source.Length || source[end] != ';')
            {
                diagnostics.Add(new FecsxDiagnostic(
                    "FECSX3012",
                    bodyKind == FecsxComponentBodyKind.Block
                        ? "The return statement in a block-bodied FECSX component must end with a semicolon."
                        : "An expression-bodied FECSX component declaration must end with a semicolon.",
                    new FecsxTextSpan(parsedExpression.ExpressionEnd, 0)));
            }
            else
            {
                end++;
            }

            if (bodyKind == FecsxComponentBodyKind.Block)
            {
                var closeBrace = SkipTrivia(source, end);
                if (closeBrace >= source.Length || source[closeBrace] != '}')
                {
                    diagnostics.Add(new FecsxDiagnostic(
                        "FECSX3013",
                        "A block-bodied FECSX component must contain exactly one 'return <markup>;' statement and end with '}'.",
                        new FecsxTextSpan(closeBrace, Math.Min(1, source.Length - closeBrace))));
                    end = source.Length;
                }
                else
                {
                    end = closeBrace + 1;
                }
            }

            components.Add(new FecsxComponentDeclarationSyntax(
                nameGroup.Value,
                parameters.PropsTypeName,
                parameters.PropsParameterName,
                parameters.ChildrenParameterName,
                new FecsxTextSpan(match.Index, Math.Max(0, end - match.Index)),
                new FecsxTextSpan(nameGroup.Index, nameGroup.Length),
                bodyKind,
                expressionOffset,
                parsedExpression.BodyOffset,
                parsedExpression.BodyEnd,
                parsedExpression.ExpressionEnd,
                parsedExpression.Body));

            if (end <= position)
            {
                break;
            }

            position = end;
        }

        ValidateExports(components, diagnostics);
        return new FecsxRenderDocumentSyntax(
            source,
            preamble,
            namespaceName,
            facadeName,
            components.AsReadOnly(),
            diagnostics.AsReadOnly());
    }

    private static ComponentExpression ParseComponentExpression(
        string source,
        int expressionOffset,
        List<FecsxDiagnostic> diagnostics)
    {
        if (expressionOffset < source.Length && source[expressionOffset] == '<')
        {
            return ParseMarkupExpression(source, expressionOffset, expressionOffset, diagnostics);
        }

        var arrow = FindWrappedMarkupArrow(source, expressionOffset);
        if (arrow < 0)
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3014",
                "A wrapped FECSX component body must have the form 'Wrapper(() => <markup>)'.",
                new FecsxTextSpan(expressionOffset, Math.Min(1, source.Length - expressionOffset))));
            return new ComponentExpression(expressionOffset, expressionOffset, expressionOffset, null);
        }

        var bodyOffset = SkipTrivia(source, arrow + 2);
        var parsed = ParseMarkupExpression(source, expressionOffset, bodyOffset, diagnostics);
        var closeParenthesis = SkipTrivia(source, parsed.BodyEnd);
        if (closeParenthesis >= source.Length || source[closeParenthesis] != ')')
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3014",
                "A wrapped FECSX component body must have the form 'Wrapper(() => <markup>)'.",
                new FecsxTextSpan(closeParenthesis, Math.Min(1, source.Length - closeParenthesis))));
            return parsed;
        }

        var expressionEnd = closeParenthesis + 1;
        var placeholder = source.Substring(expressionOffset, bodyOffset - expressionOffset)
            + "default(global::Interop.JS.React.ReactNode)"
            + source.Substring(parsed.BodyEnd, expressionEnd - parsed.BodyEnd);
        var expression = SyntaxFactory.ParseExpression(
            placeholder,
            options: CSharpParseOptions.Default,
            consumeFullText: true);
        var validWrapper = !expression.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error)
            && expression is InvocationExpressionSyntax invocation
            && invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0].Expression is ParenthesizedLambdaExpressionSyntax lambda
            && lambda.ParameterList.Parameters.Count == 0
            && lambda.Body is ExpressionSyntax;
        if (!validWrapper)
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3014",
                "A wrapped FECSX component body must have the form 'Wrapper(() => <markup>)'.",
                new FecsxTextSpan(expressionOffset, expressionEnd - expressionOffset)));
        }

        return new ComponentExpression(parsed.BodyOffset, parsed.BodyEnd, expressionEnd, parsed.Body);
    }

    private static ComponentExpression ParseMarkupExpression(
        string source,
        int expressionOffset,
        int bodyOffset,
        List<FecsxDiagnostic> diagnostics)
    {
        var bodyBoundary = FecsxParser.ParseExpression(source, bodyOffset);
        var bodyText = source.Substring(bodyOffset, Math.Max(0, bodyBoundary.End - bodyOffset));
        var parsedBody = FecsxParser.ParseExpression(bodyText, 0);
        foreach (var diagnostic in parsedBody.Diagnostics)
        {
            diagnostics.Add(new FecsxDiagnostic(
                diagnostic.Id,
                diagnostic.Message,
                new FecsxTextSpan(
                    bodyOffset + diagnostic.Span.Start,
                    diagnostic.Span.Length)));
        }

        return new ComponentExpression(bodyOffset, bodyBoundary.End, bodyBoundary.End, parsedBody.Root);
    }

    private static int FindWrappedMarkupArrow(string source, int expressionOffset)
    {
        var position = expressionOffset;
        while (position < source.Length)
        {
            var arrow = source.IndexOf("=>", position, StringComparison.Ordinal);
            if (arrow < 0)
            {
                return -1;
            }

            var bodyOffset = SkipTrivia(source, arrow + 2);
            if (bodyOffset < source.Length
                && source[bodyOffset] == '<'
                && bodyOffset + 1 < source.Length
                && (source[bodyOffset + 1] == '>' || SyntaxFacts.IsIdentifierStartCharacter(source[bodyOffset + 1])))
            {
                return arrow;
            }

            position = arrow + 2;
        }

        return -1;
    }

    private static void ValidateModifiers(Group group, List<FecsxDiagnostic> diagnostics)
    {
        var modifiers = group.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (modifiers.Length != 2
            || !modifiers.Contains("public", StringComparer.Ordinal)
            || !modifiers.Contains("static", StringComparer.Ordinal))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3006",
                "An exported FECSX component must be declared 'public static'.",
                new FecsxTextSpan(group.Index, group.Length)));
        }
    }

    private static Match? FindFirstDeclaration(string source)
    {
        foreach (Match candidate in FirstDeclarationPattern.Matches(source))
        {
            if (IsAtTopLevel(source, candidate.Index))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsAtTopLevel(string source, int position)
    {
        var depth = 0;
        foreach (var token in SyntaxFactory.ParseTokens(source.Substring(0, position)))
        {
            if (token.IsKind(SyntaxKind.OpenBraceToken))
            {
                depth++;
            }
            else if (token.IsKind(SyntaxKind.CloseBraceToken) && depth > 0)
            {
                depth--;
            }
        }

        return depth == 0;
    }

    private static void ValidateReturnType(Group group, List<FecsxDiagnostic> diagnostics)
    {
        if (!group.Value.EndsWith("ReactNode", StringComparison.Ordinal))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3007",
                "An FECSX component must return ReactNode.",
                new FecsxTextSpan(group.Index, group.Length)));
        }
    }

    private static void ValidateComponentName(Group group, List<FecsxDiagnostic> diagnostics)
    {
        var name = group.Value;
        if (name.StartsWith("@", StringComparison.Ordinal)
            || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || !SyntaxFacts.IsValidIdentifier(name)
            || !char.IsUpper(name[0]))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3008",
                "An FECSX component name must be an unescaped C# identifier beginning with an uppercase letter.",
                new FecsxTextSpan(group.Index, group.Length)));
        }
    }

    private static void ValidateExports(
        IReadOnlyList<FecsxComponentDeclarationSyntax> components,
        List<FecsxDiagnostic> diagnostics)
    {
        var defaults = components
            .Where(component => string.Equals(component.Name, "Default", StringComparison.Ordinal))
            .ToArray();
        if (defaults.Length != 1)
        {
            var span = defaults.Length > 1
                ? defaults[1].DeclarationSpan
                : components.FirstOrDefault()?.DeclarationSpan ?? default;
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3009",
                "An FECSX file must declare exactly one public static component named 'Default'.",
                span));
        }

        foreach (var duplicate in components
            .Where(component => !string.Equals(component.Name, "Default", StringComparison.Ordinal))
            .GroupBy(component => component.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3010",
                $"The named FECSX component '{duplicate.Key}' is declared more than once.",
                duplicate.Skip(1).First().DeclarationSpan));
        }
    }

    private static string GetFacadeName(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? "Component"
            : Path.GetFileNameWithoutExtension(path);

    private static void ValidateFacadeName(
        string facadeName,
        string? path,
        List<FecsxDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (facadeName.StartsWith("@", StringComparison.Ordinal)
            || SyntaxFacts.GetKeywordKind(facadeName) != SyntaxKind.None
            || !SyntaxFacts.IsValidIdentifier(facadeName)
            || !char.IsUpper(facadeName[0]))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3011",
                "An FECSX filename must be an unescaped C# identifier beginning with an uppercase letter because it supplies the default component identity.",
                new FecsxTextSpan(0, 0)));
        }
    }

    private static string? ParsePreamble(string preamble, List<FecsxDiagnostic> diagnostics)
    {
        const string marker = "internal sealed class __FecsxPreambleMarker { }";
        var unit = SyntaxFactory.ParseCompilationUnit(preamble + "\n" + marker);
        foreach (var diagnostic in unit.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
        {
            var span = diagnostic.Location.SourceSpan;
            var start = Math.Max(0, Math.Min(span.Start, preamble.Length));
            var length = Math.Max(0, Math.Min(span.Length, preamble.Length - start));
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3004",
                $"{diagnostic.Id}: {diagnostic.GetMessage()}",
                new FecsxTextSpan(start, length)));
        }

        var fileScopedNamespaces = unit.Members.OfType<FileScopedNamespaceDeclarationSyntax>().ToArray();
        if (unit.Members.OfType<NamespaceDeclarationSyntax>().Any() || fileScopedNamespaces.Length > 1)
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3005",
                "FECSX supports at most one file-scoped namespace declaration in the preamble.",
                new FecsxTextSpan(0, preamble.Length)));
        }

        return fileScopedNamespaces.Length == 1 ? fileScopedNamespaces[0].Name.ToString() : null;
    }

    private static RenderParameters ParseParameters(string text, int offset, List<FecsxDiagnostic> diagnostics)
    {
        var method = SyntaxFactory.ParseMemberDeclaration($"void __Render({text}) {{ }}") as MethodDeclarationSyntax;
        var parameters = method?.ParameterList.Parameters;
        if (method == null
            || method.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error)
            || parameters == null
            || parameters.Value.Count != 2
            || parameters.Value[0].Type == null
            || parameters.Value[1].Type is not ArrayTypeSyntax childrenType
            || childrenType.RankSpecifiers.Count != 1
            || childrenType.RankSpecifiers[0].Rank != 1
            || !childrenType.ElementType.ToString().EndsWith("ReactNode", StringComparison.Ordinal))
        {
            diagnostics.Add(new FecsxDiagnostic(
                "FECSX3002",
                "An FECSX component must declare exactly '(PropsType props, ReactNode[] children)'.",
                new FecsxTextSpan(offset, text.Length)));
            return new RenderParameters("object", "props", "children");
        }

        return new RenderParameters(
            parameters.Value[0].Type!.ToString(),
            parameters.Value[0].Identifier.ValueText,
            parameters.Value[1].Identifier.ValueText);
    }

    private static int SkipTrivia(string source, int position)
    {
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position]))
            {
                position++;
                continue;
            }

            if (position + 1 < source.Length && source[position] == '/' && source[position + 1] == '/')
            {
                position += 2;
                while (position < source.Length && source[position] != '\r' && source[position] != '\n')
                {
                    position++;
                }
                continue;
            }

            if (position + 1 < source.Length && source[position] == '/' && source[position + 1] == '*')
            {
                var end = source.IndexOf("*/", position + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    return source.Length;
                }

                position = end + 2;
                continue;
            }

            break;
        }

        return position;
    }

    private static bool IsKeywordAt(string source, int position, string keyword) =>
        position >= 0
        && position + keyword.Length <= source.Length
        && string.CompareOrdinal(source, position, keyword, 0, keyword.Length) == 0
        && (position == 0 || !SyntaxFacts.IsIdentifierPartCharacter(source[position - 1]))
        && (position + keyword.Length == source.Length
            || !SyntaxFacts.IsIdentifierPartCharacter(source[position + keyword.Length]));

    private static FecsxRenderDocumentSyntax Invalid(
        string source,
        string facadeName,
        List<FecsxDiagnostic> diagnostics) =>
        new FecsxRenderDocumentSyntax(
            source,
            string.Empty,
            null,
            facadeName,
            Array.Empty<FecsxComponentDeclarationSyntax>(),
            diagnostics.AsReadOnly());

    private sealed class RenderParameters
    {
        public RenderParameters(string propsTypeName, string propsParameterName, string childrenParameterName)
        {
            PropsTypeName = propsTypeName;
            PropsParameterName = propsParameterName;
            ChildrenParameterName = childrenParameterName;
        }

        public string PropsTypeName { get; }
        public string PropsParameterName { get; }
        public string ChildrenParameterName { get; }
    }

    private sealed class ComponentExpression
    {
        public ComponentExpression(int bodyOffset, int bodyEnd, int expressionEnd, FecsxChildSyntax? body)
        {
            BodyOffset = bodyOffset;
            BodyEnd = bodyEnd;
            ExpressionEnd = expressionEnd;
            Body = body;
        }

        public int BodyOffset { get; }
        public int BodyEnd { get; }
        public int ExpressionEnd { get; }
        public FecsxChildSyntax? Body { get; }
    }
}
