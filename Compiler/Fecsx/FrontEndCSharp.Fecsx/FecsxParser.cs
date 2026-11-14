using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FrontEndCSharp.Fecsx;

/// <summary>
/// Parses the JSX-shaped portion of FECSX documents while preserving incomplete
/// trees for generators and editors. Its structure and recovery policy are
/// adapted from the JSX parser in TypeScript 5.2.2 (Apache-2.0).
/// </summary>
public static class FecsxParser
{
    public static FecsxDocumentSyntax Parse(string source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new Parser(source).ParseDocument();
    }

    public static FecsxMarkupExpressionResult ParseExpression(string source, int start)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (start < 0 || start > source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        return new Parser(source, start).ParseMarkupExpression();
    }

    private sealed class Parser
    {
        private readonly string _source;
        private readonly List<FecsxDiagnostic> _diagnostics = new List<FecsxDiagnostic>();
        private readonly List<string?> _openElements = new List<string?>();
        private int _position;

        public Parser(string source, int position = 0)
        {
            _source = source;
            _position = position;
        }

        public FecsxMarkupExpressionResult ParseMarkupExpression()
        {
            var start = _position;
            if (Current != '<' || (!StartsWith("<>") && !CanStartTagName(Peek(1))))
            {
                AddDiagnostic("FECSX1001", "Expected an element or fragment.", new FecsxTextSpan(start, 0));
                return new FecsxMarkupExpressionResult(null, start, _diagnostics.AsReadOnly());
            }

            var root = ParseChild();
            return new FecsxMarkupExpressionResult(root, _position, _diagnostics.AsReadOnly());
        }

        public FecsxDocumentSyntax ParseDocument()
        {
            var children = new List<FecsxChildSyntax>();
            while (!IsAtEnd)
            {
                if (IsClosingTagStart())
                {
                    var closing = ConsumeClosingTag();
                    AddDiagnostic(
                        "FECSX1003",
                        closing.IsFragment
                            ? "Unexpected closing fragment."
                            : $"Unexpected closing tag '{closing.Name}'.",
                        closing.Span);
                    continue;
                }

                children.Add(ParseChild());
            }

            return new FecsxDocumentSyntax(
                new FecsxTextSpan(0, _source.Length),
                children.AsReadOnly(),
                _diagnostics.AsReadOnly());
        }

        private FecsxChildSyntax ParseChild()
        {
            if (Current == '<')
            {
                if (StartsWith("<>"))
                {
                    return ParseFragment();
                }

                if (CanStartTagName(Peek(1)))
                {
                    return ParseElement();
                }
            }

            if (Current == '{')
            {
                return ParseExpression();
            }

            return ParseText();
        }

        private FecsxElementSyntax ParseElement()
        {
            var start = _position;
            _position++;
            var nameSpan = ParseNameSpan();
            var name = Text(nameSpan);
            if (name.Length == 0)
            {
                AddDiagnostic("FECSX1001", "Expected an element name.", PointSpan());
            }

            var attributes = new List<FecsxAttributeSyntax>();
            var isSelfClosing = false;
            var openingTagTerminated = false;
            ParseAttributes(attributes, out isSelfClosing, out openingTagTerminated);
            var openingTagEnd = _position;

            if (isSelfClosing || !openingTagTerminated)
            {
                return new FecsxElementSyntax(
                    SpanFrom(start),
                    name,
                    nameSpan,
                    new FecsxTextSpan(start, openingTagEnd - start),
                    attributes.AsReadOnly(),
                    Array.Empty<FecsxChildSyntax>(),
                    isSelfClosing,
                    null,
                    null);
            }

            _openElements.Add(name);
            var children = ParseChildren();
            FecsxTextSpan? closingNameSpan = null;
            FecsxTextSpan? closingTagSpan = null;

            if (IsClosingTagStart())
            {
                var nextClosing = PeekClosingTag();
                if (!nextClosing.IsFragment && NamesAreEquivalent(name, nextClosing.Name))
                {
                    var closing = ConsumeClosingTag();
                    closingNameSpan = closing.NameSpan;
                    closingTagSpan = closing.Span;
                }
                else if (ClosingBelongsToAncestor(nextClosing))
                {
                    AddMissingClosingTagDiagnostic(name, nameSpan);
                }
                else
                {
                    var closing = ConsumeClosingTag();
                    AddDiagnostic(
                        "FECSX1004",
                        closing.IsFragment
                            ? $"Expected a closing tag for '{name}', but found a closing fragment."
                            : $"Expected a closing tag for '{name}', but found '{closing.Name}'.",
                        closing.NameSpan ?? closing.Span);
                    closingNameSpan = closing.NameSpan;
                    closingTagSpan = closing.Span;
                }
            }
            else
            {
                AddMissingClosingTagDiagnostic(name, nameSpan);
            }

            _openElements.RemoveAt(_openElements.Count - 1);
            return new FecsxElementSyntax(
                SpanFrom(start),
                name,
                nameSpan,
                new FecsxTextSpan(start, openingTagEnd - start),
                attributes.AsReadOnly(),
                children.AsReadOnly(),
                false,
                closingNameSpan,
                closingTagSpan);
        }

        private FecsxFragmentSyntax ParseFragment()
        {
            var start = _position;
            _position += 2;
            var openingSpan = new FecsxTextSpan(start, 2);
            _openElements.Add(null);
            var children = ParseChildren();
            FecsxTextSpan? closingSpan = null;

            if (StartsWith("</>"))
            {
                closingSpan = new FecsxTextSpan(_position, 3);
                _position += 3;
            }
            else
            {
                AddDiagnostic("FECSX1005", "JSX fragment has no corresponding closing tag.", openingSpan);
            }

            _openElements.RemoveAt(_openElements.Count - 1);
            return new FecsxFragmentSyntax(
                SpanFrom(start),
                openingSpan,
                children.AsReadOnly(),
                closingSpan);
        }

        private List<FecsxChildSyntax> ParseChildren()
        {
            var children = new List<FecsxChildSyntax>();
            while (!IsAtEnd && !IsClosingTagStart())
            {
                children.Add(ParseChild());
            }

            return children;
        }

        private FecsxTextSyntax ParseText()
        {
            var start = _position;
            while (!IsAtEnd && Current != '<' && Current != '{')
            {
                _position++;
            }

            if (_position == start)
            {
                _position++;
            }

            var span = new FecsxTextSpan(start, _position - start);
            return new FecsxTextSyntax(span, Text(span));
        }

        private FecsxExpressionSyntax ParseExpression()
        {
            var start = _position;
            _position++;
            var contentStart = _position;
            var relativeClosingPosition = FindExpressionClosingBrace(_source.Substring(contentStart));
            var terminated = relativeClosingPosition >= 0;
            var contentLength = terminated ? relativeClosingPosition : _source.Length - contentStart;
            var contentSpan = new FecsxTextSpan(contentStart, contentLength);
            var rawExpressionText = Text(contentSpan);
            var expressionText = rawExpressionText.Trim();
            var leadingWhitespaceLength = rawExpressionText.Length - rawExpressionText.TrimStart().Length;
            var isEmpty = !ContainsSignificantToken(expressionText);
            Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax? expression = null;

            if (!isEmpty)
            {
                expression = SyntaxFactory.ParseExpression(expressionText, options: CSharpParseOptions.Default, consumeFullText: true);
                foreach (var diagnostic in expression.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                {
                    var relativeStart = diagnostic.Location == Location.None ? 0 : diagnostic.Location.SourceSpan.Start;
                    var relativeLength = diagnostic.Location == Location.None ? 0 : diagnostic.Location.SourceSpan.Length;
                    AddDiagnostic(
                        "FECSX2001",
                        $"{diagnostic.Id}: {diagnostic.GetMessage()}",
                        ClampSpan(contentStart + leadingWhitespaceLength + relativeStart, relativeLength));
                }
            }

            if (terminated)
            {
                _position = contentStart + contentLength + 1;
            }
            else
            {
                _position = _source.Length;
                AddDiagnostic("FECSX1006", "Expression container is missing a closing '}'.", new FecsxTextSpan(start, 1));
            }

            return new FecsxExpressionSyntax(
                SpanFrom(start),
                contentSpan,
                expressionText,
                expression,
                isEmpty,
                terminated);
        }

        private void ParseAttributes(
            List<FecsxAttributeSyntax> attributes,
            out bool isSelfClosing,
            out bool openingTagTerminated)
        {
            isSelfClosing = false;
            openingTagTerminated = false;

            while (!IsAtEnd)
            {
                SkipWhitespace();
                if (StartsWith("/>"))
                {
                    _position += 2;
                    isSelfClosing = true;
                    openingTagTerminated = true;
                    return;
                }

                if (Current == '>')
                {
                    _position++;
                    openingTagTerminated = true;
                    return;
                }

                var attributeStart = _position;
                var nameSpan = ParseNameSpan();
                var name = Text(nameSpan);
                if (name.Length == 0)
                {
                    AddDiagnostic("FECSX1002", "Expected an attribute name.", PointSpan());
                    _position++;
                    continue;
                }

                SkipWhitespace();
                if (Current != '=')
                {
                    attributes.Add(new FecsxBooleanAttributeSyntax(SpanFrom(attributeStart), name, nameSpan));
                    continue;
                }

                _position++;
                SkipWhitespace();
                if (Current == '\'' || Current == '"')
                {
                    var quote = Current;
                    _position++;
                    var valueStart = _position;
                    while (!IsAtEnd && Current != quote)
                    {
                        _position++;
                    }

                    var valueSpan = new FecsxTextSpan(valueStart, _position - valueStart);
                    if (Current == quote)
                    {
                        _position++;
                    }
                    else
                    {
                        AddDiagnostic("FECSX1007", $"Attribute '{name}' has an unterminated quoted value.", nameSpan);
                    }

                    attributes.Add(new FecsxStringAttributeSyntax(
                        SpanFrom(attributeStart),
                        name,
                        nameSpan,
                        Text(valueSpan),
                        valueSpan,
                        quote));
                    continue;
                }

                if (Current == '{')
                {
                    var expression = ParseExpression();
                    attributes.Add(new FecsxExpressionAttributeSyntax(
                        SpanFrom(attributeStart),
                        name,
                        nameSpan,
                        expression));
                    continue;
                }

                AddDiagnostic("FECSX1008", $"Attribute '{name}' requires a quoted value or C# expression container.", PointSpan());
                while (!IsAtEnd && !char.IsWhiteSpace(Current) && Current != '>')
                {
                    _position++;
                }
            }

            AddDiagnostic("FECSX1009", "Opening tag is missing a closing '>'.", PointSpan());
        }

        private int FindExpressionClosingBrace(string remaining)
        {
            var depth = 0;
            foreach (var token in SyntaxFactory.ParseTokens(remaining, options: CSharpParseOptions.Default))
            {
                if (token.IsKind(SyntaxKind.OpenBraceToken))
                {
                    depth++;
                }
                else if (token.IsKind(SyntaxKind.CloseBraceToken))
                {
                    if (depth == 0)
                    {
                        return token.SpanStart;
                    }

                    depth--;
                }
            }

            return -1;
        }

        private static bool ContainsSignificantToken(string expressionText) =>
            SyntaxFactory.ParseTokens(expressionText, options: CSharpParseOptions.Default)
                .Any(token => !token.IsKind(SyntaxKind.EndOfFileToken));

        private ClosingTag PeekClosingTag()
        {
            var savedPosition = _position;
            var closing = ConsumeClosingTag(addTerminationDiagnostic: false);
            _position = savedPosition;
            return closing;
        }

        private ClosingTag ConsumeClosingTag(bool addTerminationDiagnostic = true)
        {
            var start = _position;
            _position += 2;
            SkipWhitespace();
            if (Current == '>')
            {
                _position++;
                return new ClosingTag(string.Empty, true, null, SpanFrom(start));
            }

            var nameSpan = ParseNameSpan();
            var name = Text(nameSpan);
            SkipWhitespace();
            if (Current == '>')
            {
                _position++;
            }
            else if (addTerminationDiagnostic)
            {
                AddDiagnostic("FECSX1010", $"Closing tag '{name}' is missing a closing '>'.", PointSpan());
            }

            return new ClosingTag(name, false, nameSpan, SpanFrom(start));
        }

        private bool ClosingBelongsToAncestor(ClosingTag closing)
        {
            for (var index = _openElements.Count - 2; index >= 0; index--)
            {
                var ancestor = _openElements[index];
                if (closing.IsFragment ? ancestor == null : ancestor !=  null && NamesAreEquivalent(ancestor, closing.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddMissingClosingTagDiagnostic(string name, FecsxTextSpan nameSpan) =>
            AddDiagnostic("FECSX1005", $"Element '{name}' has no corresponding closing tag.", nameSpan);

        private FecsxTextSpan ParseNameSpan()
        {
            var start = _position;
            if (!CanStartTagName(Current))
            {
                return new FecsxTextSpan(start, 0);
            }

            _position++;
            while (!IsAtEnd && IsTagNamePart(Current))
            {
                _position++;
            }

            return new FecsxTextSpan(start, _position - start);
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(Current))
            {
                _position++;
            }
        }

        private void AddDiagnostic(string id, string message, FecsxTextSpan span) =>
            _diagnostics.Add(new FecsxDiagnostic(id, message, span));

        private FecsxTextSpan ClampSpan(int start, int length)
        {
            var clampedStart = Math.Max(0, Math.Min(start, _source.Length));
            var clampedLength = Math.Max(0, Math.Min(length, _source.Length - clampedStart));
            return new FecsxTextSpan(clampedStart, clampedLength);
        }

        private FecsxTextSpan SpanFrom(int start) => new FecsxTextSpan(start, _position - start);

        private FecsxTextSpan PointSpan() => new FecsxTextSpan(Math.Min(_position, _source.Length), 0);

        private string Text(FecsxTextSpan span) => _source.Substring(span.Start, span.Length);

        private bool IsClosingTagStart() => StartsWith("</");

        private bool StartsWith(string value) =>
            _position + value.Length <= _source.Length
            && string.CompareOrdinal(_source, _position, value, 0, value.Length) == 0;

        private char Current => Peek(0);

        private char Peek(int offset)
        {
            var index = _position + offset;
            return index >= 0 && index < _source.Length ? _source[index] : '\0';
        }

        private bool IsAtEnd => _position >= _source.Length;

        private static bool CanStartTagName(char value) =>
            char.IsLetter(value) || value == '_' || value == '$';

        private static bool IsTagNamePart(char value) =>
            char.IsLetterOrDigit(value)
            || value == '_'
            || value == '$'
            || value == '-'
            || value == '.'
            || value == ':';

        private static bool NamesAreEquivalent(string left, string right) =>
            string.Equals(left, right, StringComparison.Ordinal);

        private sealed class ClosingTag
        {
            public ClosingTag(string name, bool isFragment, FecsxTextSpan? nameSpan, FecsxTextSpan span)
            {
                Name = name;
                IsFragment = isFragment;
                NameSpan = nameSpan;
                Span = span;
            }

            public string Name { get; }

            public bool IsFragment { get; }

            public FecsxTextSpan? NameSpan { get; }

            public FecsxTextSpan Span { get; }
        }
    }
}
