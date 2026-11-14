using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx.Tests;

public sealed class FecsxParserTests
{
    [Test]
    public void ParsesNestedElementsAttributesAndManagedExpressions()
    {
        const string source = "<section className=\"panel\" hidden count={items.Count}><TaskCard Name={task.Name} /></section>";

        var document = FecsxParser.Parse(source);

        Assert.Empty(document.Diagnostics);
        var section = Assert.IsType<FecsxElementSyntax>(Assert.Single(document.Children));
        Assert.Equal("section", section.Name);
        Assert.False(section.IsSelfClosing);
        Assert.Equal(source, source.Substring(section.Span.Start, section.Span.Length));

        var className = Assert.IsType<FecsxStringAttributeSyntax>(section.Attributes[0]);
        Assert.Equal("className", className.Name);
        Assert.Equal("panel", className.Value);

        var hidden = Assert.IsType<FecsxBooleanAttributeSyntax>(section.Attributes[1]);
        Assert.Equal("hidden", hidden.Name);

        var count = Assert.IsType<FecsxExpressionAttributeSyntax>(section.Attributes[2]);
        Assert.Equal("items.Count", count.Expression.Text);

        var card = Assert.IsType<FecsxElementSyntax>(Assert.Single(section.Children));
        Assert.Equal("TaskCard", card.Name);
        Assert.True(card.IsSelfClosing);
        var name = Assert.IsType<FecsxExpressionAttributeSyntax>(Assert.Single(card.Attributes));
        Assert.Equal("task.Name", name.Expression.Text);
        Assert.Equal("TaskCard", source.Substring(card.NameSpan.Start, card.NameSpan.Length));
    }

    [Test]
    public void ParsesFragmentsTextEntitiesAndCommentOnlyContainers()
    {
        const string source = "<>Hello &amp; goodbye{/* editor comment */}<strong>{name}</strong></>";

        var document = FecsxParser.Parse(source);

        Assert.Empty(document.Diagnostics);
        var fragment = Assert.IsType<FecsxFragmentSyntax>(Assert.Single(document.Children));
        Assert.Collection(
            fragment.Children,
            child => Assert.Equal("Hello &amp; goodbye", Assert.IsType<FecsxTextSyntax>(child).Text),
            child => Assert.True(Assert.IsType<FecsxExpressionSyntax>(child).IsEmpty),
            child =>
            {
                var strong = Assert.IsType<FecsxElementSyntax>(child);
                var expression = Assert.IsType<FecsxExpressionSyntax>(Assert.Single(strong.Children));
                Assert.Equal("name", expression.Text);
            });
    }

    [Test]
    public void PreservesAuthoredTextAndAttributeSpansBeforeJsxLowering()
    {
        const string source = "<div title=\"A &amp; B\">\r\n\t  &copy; FECSX\r\n  </div>";

        var document = FecsxParser.Parse(source);

        Assert.Empty(document.Diagnostics);
        var element = Assert.IsType<FecsxElementSyntax>(Assert.Single(document.Children));
        var attribute = Assert.IsType<FecsxStringAttributeSyntax>(Assert.Single(element.Attributes));
        Assert.Equal("A &amp; B", attribute.Value);
        Assert.Equal(attribute.Value, source.Substring(attribute.ValueSpan.Start, attribute.ValueSpan.Length));
        var text = Assert.IsType<FecsxTextSyntax>(Assert.Single(element.Children));
        Assert.Equal("\r\n\t  &copy; FECSX\r\n  ", text.Text);
        Assert.Equal(text.Text, source.Substring(text.Span.Start, text.Span.Length));
    }

    [Test]
    public void UsesRoslynTokensToFindTheEndOfNestedCSharpExpressions()
    {
        const string source = "<div value={new Dictionary<string, int> { [\"x\"] = 1 }} label={$\"{item.Name} {item.Count}\"} />";

        var document = FecsxParser.Parse(source);

        Assert.Empty(document.Diagnostics);
        var element = Assert.IsType<FecsxElementSyntax>(Assert.Single(document.Children));
        var value = Assert.IsType<FecsxExpressionAttributeSyntax>(element.Attributes[0]);
        var label = Assert.IsType<FecsxExpressionAttributeSyntax>(element.Attributes[1]);
        Assert.Equal("new Dictionary<string, int> { [\"x\"] = 1 }", value.Expression.Text);
        Assert.Equal("$\"{item.Name} {item.Count}\"", label.Expression.Text);
    }

    [Test]
    public void RecoversAnUnclosedChildAtAnAncestorClosingTag()
    {
        const string source = "<section><span>hello</section><footer />";

        var document = FecsxParser.Parse(source);

        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("FECSX1005", diagnostic.Id);
        Assert.Contains("span", diagnostic.Message);

        Assert.Collection(
            document.Children,
            child =>
            {
                var section = Assert.IsType<FecsxElementSyntax>(child);
                var span = Assert.IsType<FecsxElementSyntax>(Assert.Single(section.Children));
                Assert.Equal("span", span.Name);
                Assert.Null(span.ClosingNameSpan);
            },
            child => Assert.Equal("footer", Assert.IsType<FecsxElementSyntax>(child).Name));
    }

    [Test]
    public void ReportsMismatchedAndMissingClosingTagsWithoutDiscardingTheTree()
    {
        const string source = "<main><div></span><aside>";

        var document = FecsxParser.Parse(source);

        Assert.Collection(
            document.Diagnostics,
            diagnostic => Assert.Equal("FECSX1004", diagnostic.Id),
            diagnostic => Assert.Equal("FECSX1005", diagnostic.Id),
            diagnostic => Assert.Equal("FECSX1005", diagnostic.Id));
        var main = Assert.IsType<FecsxElementSyntax>(Assert.Single(document.Children));
        Assert.Equal(2, main.Children.Count);
    }

    [Test]
    public void MapsRoslynExpressionDiagnosticsBackToTheFecsxSource()
    {
        const string source = "<div value={item.} />";

        var document = FecsxParser.Parse(source);

        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("FECSX2001", diagnostic.Id);
        Assert.InRange(diagnostic.Span.Start, source.IndexOf("item", StringComparison.Ordinal), source.IndexOf('}'));
        Assert.Equal("item.", Assert.IsType<FecsxExpressionAttributeSyntax>(Assert.IsType<FecsxElementSyntax>(Assert.Single(document.Children)).Attributes[0]).Expression.Text);
    }

    [Test]
    public void IncludesLeadingContainerWhitespaceWhenMappingRoslynDiagnostics()
    {
        const string source = "<div value={  )} />";

        var document = FecsxParser.Parse(source);

        var diagnostic = Assert.Single(document.Diagnostics, diagnostic => diagnostic.Message.StartsWith("CS1525", StringComparison.Ordinal));
        Assert.Equal(source.IndexOf(')'), diagnostic.Span.Start);
    }

    [Test]
    public void ReportsTrailingUnterminatedExpressionAtItsOpeningBrace()
    {
        const string source = "<div>{items.Select(item => new { item.Name })</div>";

        var document = FecsxParser.Parse(source);

        var diagnostic = Assert.Single(document.Diagnostics, diagnostic => diagnostic.Id == "FECSX1006");
        Assert.Equal(source.IndexOf('{'), diagnostic.Span.Start);
    }

    [Test]
    public void ParsesOneMarkupExpressionInsideOrdinaryCSharp()
    {
        const string source = "return <section><span>{name}</span></section>;";
        var start = source.IndexOf('<');

        var result = FecsxParser.ParseExpression(source, start);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(source.IndexOf(';'), result.End);
        var section = Assert.IsType<FecsxElementSyntax>(result.Root);
        Assert.Equal(start, section.Span.Start);
        Assert.Equal("section", section.Name);
    }
}
