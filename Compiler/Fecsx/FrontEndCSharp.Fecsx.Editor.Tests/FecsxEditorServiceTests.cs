using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxEditorServiceTests
{
    private readonly FecsxEditorService _service = new();

    [TestCase("<Panel>", "</Panel>")]
    [TestCase("<Controls.Panel>", "</Controls.Panel>")]
    [TestCase("<>", "</>")]
    public void CompletesOnlyGenuinelyUnclosedMarkup(string body, string expected)
    {
        var document = Document(body);
        var position = document.IndexOf(body, StringComparison.Ordinal) + body.Length;

        var edit = Assert.IsType<FecsxTextEdit>(_service.GetClosingTagCompletion(document, position));

        Assert.Equal(new FecsxTextSpan(position, 0), edit.Span);
        Assert.Equal(expected, edit.NewText);
        Assert.Null(_service.GetClosingTagCompletion(Document(body + expected), position));
        var selfClosing = Document("<Panel />");
        Assert.Null(_service.GetClosingTagCompletion(selfClosing, selfClosing.Length));
    }

    [Test]
    public void CompletesTheInnermostUnclosedElement()
    {
        var document = Document("<Panel><TaskCard>");
        var position = document.Length;

        Assert.Equal("</TaskCard>", _service.GetClosingTagCompletion(document, position)?.NewText);
    }

    [Test]
    public void DoesNotCompleteAClosingTagFromInsideEmbeddedCSharp()
    {
        var document = Document("<Panel>{props.Name}");
        var position = document.IndexOf("Name", StringComparison.Ordinal) + 2;

        Assert.Null(_service.GetClosingTagCompletion(document, position));
    }

    [Test]
    public void CompletesAPartiallyTypedQualifiedClosingTag()
    {
        var document = Document("<Controls.Panel>content</Controls.Pa");

        var edit = Assert.IsType<FecsxTextEdit>(_service.GetClosingTagNameCompletion(document, document.Length));

        Assert.Equal("Controls.Panel", edit.NewText);
        Assert.Equal("Controls.Pa", document.Substring(edit.Span.Start, edit.Span.Length));
    }

    [Test]
    public void LinksMatchingQualifiedTagNames()
    {
        var document = Document("<Controls.Panel></Controls.Panel>");
        var opening = document.IndexOf("Controls.Panel", StringComparison.Ordinal);
        var closing = document.LastIndexOf("Controls.Panel", StringComparison.Ordinal);

        var result = Assert.IsType<FecsxLinkedEditingInfo>(
            _service.GetLinkedEditingRanges(document, opening + 4));

        Assert.Equal("[a-zA-Z0-9:\\-\\._$]*", result.WordPattern);
        Assert.Equal(new[]
        {
            new FecsxTextSpan(opening, "Controls.Panel".Length),
            new FecsxTextSpan(closing, "Controls.Panel".Length),
        }, result.Ranges);
        Assert.Null(_service.GetLinkedEditingRanges(Document("<Panel></Other>"), opening));
    }

    [Test]
    public void LinksFragmentInsertionPoints()
    {
        var document = Document("<></>");
        var opening = document.IndexOf("<>", StringComparison.Ordinal) + 1;
        var closing = document.IndexOf("</>", StringComparison.Ordinal) + 2;

        var result = Assert.IsType<FecsxLinkedEditingInfo>(
            _service.GetLinkedEditingRanges(document, opening));

        Assert.Equal(new[] { new FecsxTextSpan(opening, 0), new FecsxTextSpan(closing, 0) }, result.Ranges);
    }

    [Test]
    public void MirrorsAPartialTagNameEditIntoTheMatchingTag()
    {
        var document = Document("<Controls.Panel></Controls.Panel>");
        var opening = document.IndexOf("Panel", StringComparison.Ordinal);
        var closing = document.LastIndexOf("Panel", StringComparison.Ordinal);

        var edit = Assert.IsType<FecsxTextEdit>(
            _service.GetLinkedTagNameEdit(document, new FecsxTextSpan(opening, "Panel".Length), "Card"));

        Assert.Equal(new FecsxTextSpan(closing, "Panel".Length), edit.Span);
        Assert.Equal("Card", edit.NewText);
    }

    [Test]
    public void CompletesIntrinsicAttributesWithInsertionShapesAndFiltersExistingAttributes()
    {
        var document = Document("<button className=\"primary\" dis></button>");
        var position = document.IndexOf("dis", StringComparison.Ordinal) + 3;

        var completions = _service.GetAttributeCompletions(document, position);

        Assert.DoesNotContain(completions, item => item.DisplayText == "className");
        var disabled = Assert.Single(completions, item => item.DisplayText == "disabled");
        Assert.Equal("disabled", disabled.InsertText);
        Assert.Equal("dis", document.Substring(disabled.ReplacementSpan.Start, disabled.ReplacementSpan.Length));
        Assert.Equal("title=\"\"", Assert.Single(completions, item => item.DisplayText == "title").InsertText);
        Assert.Equal("tabIndex={}", Assert.Single(completions, item => item.DisplayText == "tabIndex").InsertText);
        Assert.Equal("onClick={}", Assert.Single(completions, item => item.DisplayText == "onClick").InsertText);
    }

    [Test]
    public void ObtainsManagedComponentAttributesThroughTheMetadataSeam()
    {
        var document = Document("<TaskCard ></TaskCard>");
        var position = document.IndexOf("<TaskCard", StringComparison.Ordinal) + "<TaskCard ".Length;
        var provider = new TestComponentMetadataProvider();

        var completions = _service.GetAttributeCompletions(document, position, provider);

        Assert.Equal("name=\"\"", Assert.Single(completions, item => item.DisplayText == "name").InsertText);
        Assert.Equal("isDone", Assert.Single(completions, item => item.DisplayText == "isDone").InsertText);
        Assert.Equal("onToggle={}", Assert.Single(completions, item => item.DisplayText == "onToggle").InsertText);
        Assert.Equal("TaskCard", provider.RequestedComponent);
    }

    [Test]
    public void PascalCasePreferenceChangesIntrinsicSuggestionsAndCanonicalFiltering()
    {
        var document = Document("<button className=\"primary\" Dis></button>");
        var position = document.IndexOf("Dis", StringComparison.Ordinal) + 3;

        var completions = _service.GetAttributeCompletions(
            document,
            position,
            attributeCasing: FecsxAttributeCasing.PascalCase);

        Assert.DoesNotContain(completions, item => item.DisplayText == "ClassName");
        Assert.Equal("Disabled", Assert.Single(completions, item => item.DisplayText == "Disabled").InsertText);
        Assert.Equal("Title=\"\"", Assert.Single(completions, item => item.DisplayText == "Title").InsertText);
        Assert.Equal("TabIndex={}", Assert.Single(completions, item => item.DisplayText == "TabIndex").InsertText);
        Assert.Equal("OnClick={}", Assert.Single(completions, item => item.DisplayText == "OnClick").InsertText);
    }

    [Test]
    public void CamelCasePreferenceChangesManagedSuggestionsAndCanonicalFiltering()
    {
        var document = Document("<TaskCard Name=\"existing\" ></TaskCard>");
        var position = document.IndexOf(" ></TaskCard>", StringComparison.Ordinal);

        var completions = _service.GetAttributeCompletions(
            document,
            position,
            new TestComponentMetadataProvider(),
            FecsxAttributeCasing.CamelCase);

        Assert.DoesNotContain(completions, item => item.DisplayText == "name");
        Assert.Equal("isDone", Assert.Single(completions, item => item.DisplayText == "isDone").InsertText);
        Assert.Equal("onToggle={}", Assert.Single(completions, item => item.DisplayText == "onToggle").InsertText);
    }

    [TestCase("<", "")]
    [TestCase("<Task", "Task")]
    [TestCase("<Controls.Pa", "Controls.Pa")]
    public void FindsTheOpeningTagNameCompletionSpan(string body, string expected)
    {
        var document = Document(body);

        var span = Assert.IsType<FecsxTextSpan>(_service.GetOpeningTagNameCompletionSpan(document, document.Length));

        Assert.Equal(expected, document.Substring(span.Start, span.Length));
    }

    [TestCase("</Task")]
    [TestCase("<Task Name")]
    [TestCase("{props.Store}")]
    public void DoesNotTreatOtherRegionsAsAnOpeningTagName(string body)
    {
        var document = Document(body);

        Assert.Null(_service.GetOpeningTagNameCompletionSpan(document, document.Length));
    }

    [Test]
    public void FindsTheActiveAttributeNameForSemanticCompletion()
    {
        var document = Document("<TaskCard Na></TaskCard>");
        var position = document.IndexOf("Na", StringComparison.Ordinal) + 2;

        var span = Assert.IsType<FecsxTextSpan>(_service.GetAttributeNameCompletionSpan(document, position));

        Assert.Equal("Na", document.Substring(span.Start, span.Length));
        Assert.Null(_service.GetAttributeNameCompletionSpan(
            Document("<TaskCard Name=\"value\"></TaskCard>"),
            Document("<TaskCard Name=\"value").Length));
    }

    [Test]
    public void CompletesPinnedTailwindDefaultsInsideLiteralClassNameAttributes()
    {
        var document = Document("<div className=\"flex drop-shadow-\"></div>");
        var position = document.IndexOf("drop-shadow-", StringComparison.Ordinal) + "drop-shadow-".Length;

        var span = Assert.IsType<FecsxTextSpan>(_service.GetTailwindClassCompletionSpan(document, position));
        var completions = _service.GetTailwindClassCompletions(document, position);

        Assert.Equal("drop-shadow-", document.Substring(span.Start, span.Length));
        var extraLarge = Assert.Single(completions, item => item.DisplayText == "drop-shadow-xl");
        Assert.Equal("drop-shadow-xl", extraLarge.InsertText);
        Assert.Contains(
            completions.OrderBy(item => item.SortText, StringComparer.Ordinal).Take(300),
            item => item.DisplayText == "drop-shadow-xl");
        Assert.Contains("element's rendered shape", extraLarge.Description, StringComparison.Ordinal);
        Assert.Contains("0 9px 7px", extraLarge.Description, StringComparison.Ordinal);
        Assert.Contains("https://tailwindcss.com/docs/filter-drop-shadow", extraLarge.Description, StringComparison.Ordinal);

        var arbitrary = Assert.Single(
            completions,
            item => item.DisplayText == "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]");
        Assert.Equal("drop-shadow-[]", arbitrary.InsertText);
        Assert.Equal(1, arbitrary.CaretOffsetFromEnd);
        Assert.Contains(
            "Example authored value: 0_35px_35px_rgba(0,0,0,0.25)",
            arbitrary.Description,
            StringComparison.Ordinal);
        Assert.Contains(
            "Resulting CSS value: 0 35px 35px rgba(0,0,0,0.25)",
            arbitrary.Description,
            StringComparison.Ordinal);
        var customProperty = Assert.Single(completions, item => item.DisplayText == "drop-shadow-(<custom-property>)");
        Assert.Equal("drop-shadow-(--)", customProperty.InsertText);
        Assert.Equal(1, customProperty.CaretOffsetFromEnd);
    }

    [Test]
    public void OneLetterTailwindSessionIncludesTheCompleteGroupedSurfaceForRetargeting()
    {
        var document = Document("<div className=\"p\"></div>");
        var position = document.IndexOf("className=\"p", StringComparison.Ordinal)
            + "className=\"p".Length;

        var completions = _service.GetTailwindClassCompletions(document, position);

        Assert.True(completions.Count > 100);
        Assert.True(completions.Count < 2_000);
        Assert.Contains(completions, item => item.DisplayText == "p-");
        Assert.DoesNotContain(completions, item => item.DisplayText == "p-0");
        Assert.Contains(completions, item => item.DisplayText == "block");
        Assert.Contains(
            completions
                .Where(item => item.DisplayText.StartsWith("blo", StringComparison.Ordinal))
                .OrderBy(item => item.SortText, StringComparer.Ordinal)
                .Take(300),
            item => item.DisplayText == "block");
    }

    [Test]
    public void GroupsDefaultTailwindUtilitiesByTheirGeneratedFamily()
    {
        var document = Document("<div className=\"m\"></div>");
        var position = document.IndexOf("className=\"m", StringComparison.Ordinal)
            + "className=\"m".Length;

        var completions = _service.GetTailwindClassCompletions(document, position);

        var family = Assert.Single(completions, item => item.DisplayText == "m-");
        Assert.Equal("m-", family.InsertText);
        Assert.DoesNotContain(completions, item => item.DisplayText == "m-1");
        Assert.Contains("m-1", family.Description, StringComparison.Ordinal);
        Assert.Contains("margin: 4px", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-12", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-14", family.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("m-13", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-16", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-[13px]", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-[var(--padding)]", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-(--padding)", family.Description, StringComparison.Ordinal);
        Assert.Contains("Adds whitespace outside the box", family.Description, StringComparison.Ordinal);
        Assert.Contains(completions, item => item.DisplayText == "block");
    }

    [Test]
    public void KeepsValueLessUtilityBesideItsParameterizedFamily()
    {
        var document = Document("<div className=\"g\"></div>");
        var position = document.IndexOf("className=\"g", StringComparison.Ordinal)
            + "className=\"g".Length;

        var ordered = _service.GetTailwindClassCompletions(document, position)
            .OrderBy(item => item.SortText, StringComparer.Ordinal)
            .Select(item => item.DisplayText)
            .ToArray();
        var grow = Array.IndexOf(ordered, "grow");
        var growFamily = Array.IndexOf(ordered, "grow-");

        Assert.True(grow >= 0);
        Assert.Equal(grow + 1, growFamily);
    }

    [Test]
    public void SplitsBroadUtilityRootsByCssPropertyAndGroupsColorScales()
    {
        var document = Document("<div className=\"bg\"></div>");
        var position = document.IndexOf("className=\"bg", StringComparison.Ordinal)
            + "className=\"bg".Length;

        var completions = _service.GetTailwindClassCompletions(document, position);

        Assert.Contains(completions, item => item.DisplayText == "bg-auto");
        Assert.Contains(completions, item => item.DisplayText == "bg-black");
        Assert.Contains(completions, item => item.DisplayText == "bg-clip-");
        var amber = Assert.Single(completions, item => item.DisplayText == "bg-amber-");
        Assert.DoesNotContain(completions, item => item.DisplayText == "bg-amber-50");
        Assert.Contains("background color", amber.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bg-amber-50", amber.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("bg-blue-50", amber.Description, StringComparison.Ordinal);
    }

    [Test]
    public void ShowsTheEffectiveValueThatDistinguishesRingWidths()
    {
        var document = Document("<div className=\"ring\"></div>");
        var position = document.IndexOf("className=\"ring", StringComparison.Ordinal)
            + "className=\"ring".Length;

        var family = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "ring-");

        Assert.Contains("ring-0 → ring width: 0px", family.Description, StringComparison.Ordinal);
        Assert.Contains("ring-1 → ring width: 1px", family.Description, StringComparison.Ordinal);
        Assert.Contains("ring-2 → ring width: 2px", family.Description, StringComparison.Ordinal);
        Assert.Contains("ring-[13px] → ring width: 13px", family.Description, StringComparison.Ordinal);
    }

    [Test]
    public void EveryTailwindFamilyPreviewDistinguishesDifferentGeneratedCss()
    {
        var catalog = TailwindClassCatalog.Current;
        var collapsed = catalog.Families
            .SelectMany(family => catalog.FindByPrefix(family.Prefix)
                .Where(entry => string.Equals(
                    entry.FamilyPrefix,
                    family.Prefix,
                    StringComparison.Ordinal))
                .GroupBy(
                    entry => FecsxTailwindCompletion.DescribeFamilyEntryPreview(catalog, entry),
                    StringComparer.Ordinal)
                .Where(group => group.Select(entry => entry.Css).Distinct(StringComparer.Ordinal).Count() > 1)
                .Select(group => family.Prefix + ": "
                    + string.Join(", ", group.Select(entry => entry.Name).Take(8))))
            .ToArray();

        Assert.Empty(collapsed);
    }

    [TestCase("border", "border-", "border-2 → border-width: 2px")]
    [TestCase("blur", "blur-", "blur-sm → blur: var(--blur-sm)")]
    [TestCase("brightness", "brightness-", "brightness-50 → brightness: 50%")]
    [TestCase("rotate-x", "rotate-x-", "rotate-x-1 → rotate x: 1deg")]
    [TestCase("mask-linear", "mask-linear-", "mask-linear-1 → mask linear position: 1deg")]
    [TestCase(
        "scrollbar-thumb",
        "scrollbar-thumb-",
        "scrollbar-thumb-black → scrollbar thumb: var(--color-black)")]
    public void ShowsDistinctValuesForIndirectTailwindFamilies(
        string authored,
        string expectedFamily,
        string expectedPreview)
    {
        var document = Document($"<div className=\"{authored}\"></div>");
        var position = document.IndexOf("className=\"", StringComparison.Ordinal)
            + "className=\"".Length
            + authored.Length;

        var family = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == expectedFamily);

        Assert.Contains(expectedPreview, family.Description, StringComparison.Ordinal);
    }

    [Test]
    public void ExpandsOnlyRealTailwindValuesAfterAUtilityFamilyPrefix()
    {
        var document = Document("<div className=\"m-\"></div>");
        var position = document.IndexOf("className=\"m-", StringComparison.Ordinal)
            + "className=\"m-".Length;

        var completions = _service.GetTailwindClassCompletions(document, position);

        Assert.Contains(completions, item => item.DisplayText == "m-12");
        Assert.Contains(completions, item => item.DisplayText == "m-14");
        Assert.DoesNotContain(completions, item => item.DisplayText == "m-13");
        Assert.Contains(completions, item => item.DisplayText == "m-16");
        var arbitrary = Assert.Single(completions, item => item.DisplayText == "m-[13px]");
        Assert.Equal("m-[]", arbitrary.InsertText);
        Assert.Equal(1, arbitrary.CaretOffsetFromEnd);
        var customProperty = Assert.Single(
            completions,
            item => item.DisplayText == "m-(<custom-property>)");
        Assert.Equal("m-(--)", customProperty.InsertText);
        Assert.Equal(1, customProperty.CaretOffsetFromEnd);
    }

    [Test]
    public void ResolvesUtilityFamilyPixelsFromTheProjectSpacingTheme()
    {
        using var output = new MemoryStream();
        using (var compressed = new System.IO.Compression.GZipStream(
            output,
            System.IO.Compression.CompressionLevel.SmallestSize,
            leaveOpen: true))
        using (var writer = new StreamWriter(compressed))
        {
            writer.WriteLine("#\t4.3.5\t2\t0.5rem");
            writer.WriteLine("m-1\t\tmargin: var(--spacing);\tm-");
            writer.WriteLine("m-2\t\tmargin: calc(var(--spacing) * 2);\tm-");
            writer.WriteLine("#families\t1");
            writer.WriteLine("m-\tmargin: 13px;\tmargin: var(--value);");
            writer.WriteLine("#variants\t1");
            writer.WriteLine("hover:\t@media (hover: hover) { &:hover }");
        }

        var catalog = FecsxTailwindClassCatalog.FromCompressedBytes(output.ToArray());
        var document = Document("<div className=\"m\"></div>");
        var position = document.IndexOf("className=\"m", StringComparison.Ordinal)
            + "className=\"m".Length;
        var family = Assert.Single(
            _service.GetTailwindClassCompletions(document, position, catalog),
            item => item.DisplayText == "m-");

        Assert.Contains("m-1 → margin: 8px", family.Description, StringComparison.Ordinal);
        Assert.Contains("m-2 → margin: 16px", family.Description, StringComparison.Ordinal);
    }

    [Test]
    public void DefersTailwindDescriptionsUntilTheItemIsHighlighted()
    {
        var requested = false;
        var item = new FecsxTailwindCompletionItem(
            "block",
            "block",
            new FecsxTextSpan(0, 1),
            () =>
            {
                requested = true;
                return "display: block";
            });

        Assert.False(requested);
        Assert.Equal("display: block", item.Description);
        Assert.True(requested);
    }

    [Test]
    public void CompletesTailwindOpacityAfterTheSlashAndExplainsTheParameter()
    {
        var document = Document("<svg className=\"drop-shadow-xl/2\"></svg>");
        var position = document.IndexOf("drop-shadow-xl/2", StringComparison.Ordinal) + "drop-shadow-xl/2".Length;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "drop-shadow-xl/25");

        Assert.Equal("drop-shadow-xl/25", completion.InsertText);
        Assert.Contains("opacity", completion.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("25%", completion.Description, StringComparison.Ordinal);
        Assert.Contains("without changing the selected size", completion.Description, StringComparison.Ordinal);
    }

    [TestCase("drop-shadow-xl", false)]
    [TestCase("m-", true)]
    [TestCase("md:m-", true)]
    [TestCase("drop-shadow-xl/", true)]
    [TestCase("drop-shadow-xl/25", true)]
    [TestCase("drop-shadow-[0_35px", true)]
    [TestCase("drop-shadow-[0_35px]", true)]
    [TestCase("drop-shadow-(--my-drop-shadow", true)]
    [TestCase("drop-shadow-(--my-drop-shadow)", true)]
    [TestCase("block", false)]
    public void IdentifiesTailwindGrammarStatesThatNeedANewVisualStudioSession(
        string token,
        bool expected)
    {
        var document = Document($"<div className=\"{token}\"></div>");
        var position = document.IndexOf(token, StringComparison.Ordinal) + token.Length;

        Assert.Equal(
            expected,
            _service.NeedsTailwindClassCompletionRefresh(document, position));
    }

    [TestCase(
        "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)",
        "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]",
        "filter: drop-shadow(0 35px 35px rgba(0,0,0,0.25));")]
    [TestCase(
        "drop-shadow-(--my-drop-shadow",
        "drop-shadow-(--my-drop-shadow)",
        "filter: drop-shadow(var(--my-drop-shadow));")]
    public void CompletesAndExplainsTailwindArbitrarySyntax(
        string authored,
        string expectedInsertion,
        string expectedCss)
    {
        var document = Document($"<div className=\"{authored}\"></div>");
        var position = document.IndexOf(authored, StringComparison.Ordinal) + authored.Length;

        var completion = Assert.Single(_service.GetTailwindClassCompletions(document, position));

        Assert.Equal(expectedInsertion, completion.InsertText);
        Assert.Contains(expectedCss, completion.Description, StringComparison.Ordinal);
        if (authored.StartsWith("drop-shadow-[", StringComparison.Ordinal))
        {
            Assert.Contains("Authored value:", completion.Description, StringComparison.Ordinal);
            Assert.Contains("Resulting CSS value:", completion.Description, StringComparison.Ordinal);
        }
    }

    [TestCase(
        "drop-shadow-[|0_35px_35px_rgba(0,0,0,0.25)]",
        "Active parameter: offset-x",
        "Positive values move the shadow right; negative values move it left.")]
    [TestCase(
        "drop-shadow-[0_|35px_35px_rgba(0,0,0,0.25)]",
        "Active parameter: offset-y",
        "Positive values move the shadow down; negative values move it up.")]
    [TestCase(
        "drop-shadow-[0_35px_|35px_rgba(0,0,0,0.25)]",
        "Active parameter: blur",
        "larger values make the shadow wider and more diffuse.")]
    [TestCase(
        "drop-shadow-[0_35px_35px_|rgba(0,0,0,0.25)]",
        "Active parameter: color",
        "This accepts a CSS color")]
    public void ExplainsTheDropShadowParameterAtTheCaret(
        string markedToken,
        string expectedParameter,
        string expectedExplanation)
    {
        var marker = markedToken.IndexOf('|');
        var token = markedToken.Remove(marker, 1);
        var document = Document($"<div className=\"{token}\"></div>");
        var position = document.IndexOf(token, StringComparison.Ordinal) + marker;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position));

        Assert.Contains(
            "Syntax: drop-shadow-[offset-x_offset-y_blur_color]",
            completion.Description,
            StringComparison.Ordinal);
        Assert.Contains(expectedParameter, completion.Description, StringComparison.Ordinal);
        Assert.Contains(expectedExplanation, completion.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("spread", completion.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("inset", completion.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public void OpenTailwindBracketOffersAnEditableMappedExample()
    {
        var document = Document("<div className=\"drop-shadow-[\"></div>");
        var position = document.IndexOf("drop-shadow-[", StringComparison.Ordinal)
            + "drop-shadow-[".Length;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position));

        Assert.Equal(
            "drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]",
            completion.DisplayText);
        Assert.Equal("drop-shadow-[]", completion.InsertText);
        Assert.Equal(1, completion.CaretOffsetFromEnd);
        Assert.Contains("each underscore maps to a CSS space", completion.Description, StringComparison.Ordinal);
        Assert.Contains("filter: drop-shadow(0 35px 35px rgba(0,0,0,0.25));", completion.Description, StringComparison.Ordinal);
    }

    [Test]
    public void PreservesTailwindVariantsWhileCompletingTheUtility()
    {
        var document = Document("<div className=\"md:hover:drop-shadow-x\"></div>");
        var position = document.IndexOf("md:hover:drop-shadow-x", StringComparison.Ordinal)
            + "md:hover:drop-shadow-x".Length;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "md:hover:drop-shadow-xl");

        Assert.Equal("md:hover:drop-shadow-xl", completion.InsertText);
    }

    [TestCase("md", "md:", "@media (width >= 48rem)")]
    [TestCase("max-m", "max-md:", "@media (width < 48rem)")]
    [TestCase("hov", "hover:", "@media (hover: hover) { &:hover }")]
    [TestCase("foc", "focus:", "&:focus")]
    [TestCase("dar", "dark:", "@media (prefers-color-scheme: dark)")]
    public void CompletesDefaultTailwindVariants(
        string authored,
        string expected,
        string expectedCondition)
    {
        var document = Document($"<div className=\"{authored}\"></div>");
        var position = document.IndexOf(authored, StringComparison.Ordinal) + authored.Length;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == expected);

        Assert.Equal(expected, completion.InsertText);
        Assert.True(completion.SortText.StartsWith(
            expected.TrimEnd(':').Length.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + ":",
            StringComparison.Ordinal));
        Assert.Contains(expectedCondition, completion.Description, StringComparison.Ordinal);
    }

    [Test]
    public void OneLetterTailwindSessionIncludesEveryDefaultVariantInScope()
    {
        var document = Document("<div className=\"m\"></div>");
        var position = document.IndexOf("className=\"m", StringComparison.Ordinal)
            + "className=\"m".Length;

        var responsiveVariants = _service.GetTailwindClassCompletions(document, position)
            .Where(item => item.DisplayText.EndsWith(":", StringComparison.Ordinal))
            .Select(item => item.DisplayText)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "sm:", "md:", "lg:", "xl:", "2xl:",
                "max-sm:", "max-md:", "max-lg:", "max-xl:", "max-2xl:",
                "hover:", "focus:", "dark:",
            },
            responsiveVariants);
    }

    [Test]
    public void RefreshesAfterAResponsiveVariantAndCompletesPrefixedUtilities()
    {
        var document = Document("<div className=\"md:\"></div>");
        var position = document.IndexOf("md:", StringComparison.Ordinal) + "md:".Length;

        Assert.True(_service.NeedsTailwindClassCompletionRefresh(document, position));
        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "md:block");

        Assert.Equal("md:block", completion.InsertText);
        Assert.Contains("display: block", completion.Description, StringComparison.Ordinal);
        var family = Assert.Single(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "md:m-");
        Assert.DoesNotContain(
            _service.GetTailwindClassCompletions(document, position),
            item => item.DisplayText == "md:m-1");
        Assert.Contains("md:m-1", family.Description, StringComparison.Ordinal);
    }

    [Test]
    public void UsesAProjectTailwindV4CatalogWhenOneHasBeenBuilt()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp.Fecsx.Editor.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var catalogPath = Path.Combine(root, "editor-catalog.tsv.gz");
        try
        {
            using (var output = File.Create(catalogPath))
            using (var compressed = new System.IO.Compression.GZipStream(
                output,
                System.IO.Compression.CompressionLevel.SmallestSize))
            using (var writer = new StreamWriter(compressed))
            {
                writer.WriteLine("#\t4.3.5\t2");
                writer.WriteLine(
                    "drop-shadow-card\t\tfilter: drop-shadow(0 12px 24px rgb(0 0 0 / 0.18));");
                writer.WriteLine("table-auto\t\ttable-layout: auto;");
                writer.WriteLine("#variants\t2");
                writer.WriteLine("tablet:\t@media (width >= 50rem)");
                writer.WriteLine("max-tablet:\t@media (width < 50rem)");
            }

            var utilityDocument = Document("<div className=\"drop-shadow-car\"></div>");
            var projectCatalog = FecsxTailwindClassCatalog.FromCompressedBytes(
                File.ReadAllBytes(catalogPath));
            var utilityPosition = utilityDocument.IndexOf(
                "drop-shadow-car",
                StringComparison.Ordinal) + "drop-shadow-car".Length;
            var utility = Assert.Single(
                _service.GetTailwindClassCompletions(
                    utilityDocument,
                    utilityPosition,
                    projectCatalog),
                item => item.DisplayText == "drop-shadow-card");
            Assert.Contains(
                "filter: drop-shadow(0 12px 24px rgb(0 0 0 / 0.18));",
                utility.Description,
                StringComparison.Ordinal);

            var variantDocument = Document("<div className=\"tabl\"></div>");
            var variantPosition = variantDocument.IndexOf("tabl", StringComparison.Ordinal) + 4;
            var variant = Assert.Single(
                _service.GetTailwindClassCompletions(variantDocument, variantPosition, projectCatalog),
                item => item.DisplayText == "tablet:");
            Assert.Contains("@media (width >= 50rem)", variant.Description, StringComparison.Ordinal);
            var ordered = _service.GetTailwindClassCompletions(
                    variantDocument,
                    variantPosition,
                    projectCatalog)
                .OrderBy(item => item.SortText, StringComparer.Ordinal)
                .Select(item => item.DisplayText)
                .ToArray();
            Assert.True(Array.IndexOf(ordered, "tablet:") < Array.IndexOf(ordered, "table-auto"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FallsBackToTheEmbeddedTailwindCatalogBeforeAProjectCatalogExists()
    {
        var document = Document("<div className=\"blo\"></div>");
        var position = document.IndexOf("blo", StringComparison.Ordinal) + 3;

        var completion = Assert.Single(
            _service.GetTailwindClassCompletions(
                document,
                position,
                projectCatalog: null),
            item => item.DisplayText == "block");

        Assert.Contains("display: block", completion.Description, StringComparison.Ordinal);
    }

    [Test]
    public void DoesNotOfferTailwindOutsideLiteralClassNameValues()
    {
        var ordinaryAttribute = Document("<div title=\"drop-shadow-\"></div>");
        var expressionAttribute = Document("<div className={\"drop-shadow-\"}></div>");

        Assert.Empty(_service.GetTailwindClassCompletions(
            ordinaryAttribute,
            ordinaryAttribute.IndexOf("drop-shadow-", StringComparison.Ordinal) + "drop-shadow-".Length));
        Assert.Empty(_service.GetTailwindClassCompletions(
            expressionAttribute,
            expressionAttribute.IndexOf("drop-shadow-", StringComparison.Ordinal) + "drop-shadow-".Length));
    }

    [Test]
    public void MatchesExpressionContainerBracesWithoutConfusingNestedCSharpBraces()
    {
        var document = Document("<div>{new[] { 1, 2 }}</div>");
        var opening = document.IndexOf("{new", StringComparison.Ordinal);
        var closing = document.IndexOf("}</div>", StringComparison.Ordinal);

        var ranges = _service.GetBraceMatchingRanges(document, opening);

        Assert.Equal(new[] { new FecsxTextSpan(opening, 1), new FecsxTextSpan(closing, 1) }, ranges);
    }

    [Test]
    public void CommentsMarkupAndEmbeddedCSharpWithTheirNativeShapes()
    {
        var document = Document("<div>hello {props.Name}</div>");
        var textStart = document.IndexOf("hello", StringComparison.Ordinal);
        var expressionStart = document.IndexOf("props.Name", StringComparison.Ordinal);

        var markup = _service.CommentSelection(document, new FecsxTextSpan(textStart, 5));
        var csharp = _service.CommentSelection(document, new FecsxTextSpan(expressionStart, 10));

        Assert.Equal(new[] { "{/*", "*/}" }, markup.Select(edit => edit.NewText));
        Assert.Equal(new[] { "/*", "*/" }, csharp.Select(edit => edit.NewText));
    }

    [Test]
    public void CommentsTheCSharpPreambleAsCSharp()
    {
        var document = Document("<div />");
        var selection = new FecsxTextSpan(document.IndexOf("Interop", StringComparison.Ordinal), "Interop".Length);

        var edits = _service.CommentSelection(document, selection);

        Assert.Equal(new[] { "/*", "*/" }, edits.Select(edit => edit.NewText));
    }

    [Test]
    public void FormatsOnlyTheCSharpInsideAnExpressionContainer()
    {
        var document = Document("<div>{props . Name}</div>");
        var position = document.IndexOf("Name", StringComparison.Ordinal);

        var edit = Assert.IsType<FecsxTextEdit>(_service.GetExpressionFormattingEdit(document, position));

        Assert.Equal("{props.Name}", edit.NewText);
    }

    [Test]
    public void PreservesAuthoredJsxTextAndEntitiesWhenFormattingEmbeddedCSharp()
    {
        var document = Document("<div>\n  &copy; {props . Name}\n</div>");
        var position = document.IndexOf("Name", StringComparison.Ordinal);

        var edit = Assert.IsType<FecsxTextEdit>(_service.GetExpressionFormattingEdit(document, position));
        var updated = document.Remove(edit.Span.Start, edit.Span.Length).Insert(edit.Span.Start, edit.NewText);
        var classifications = _service.GetClassifications(updated);

        Assert.Contains("<div>\n  &copy; {props.Name}\n</div>", updated);
        Assert.Contains(
            classifications,
            item => Slice(updated, item).Contains("&copy; ", StringComparison.Ordinal)
                && item.Kind == FecsxClassificationKind.Text);
    }

    [Test]
    public void ClassifiesRecoveredMarkupAndEmbeddedCSharpSeparately()
    {
        var document = Document("<Panel title=\"Focus\">Hello {props.Name}");

        var classifications = _service.GetClassifications(document);

        Assert.Contains(classifications, item => Slice(document, item) == "Panel" && item.Kind == FecsxClassificationKind.TagName);
        Assert.Contains(classifications, item => Slice(document, item) == "title" && item.Kind == FecsxClassificationKind.AttributeName);
        Assert.Contains(classifications, item => Slice(document, item) == "Focus" && item.Kind == FecsxClassificationKind.AttributeValue);
        Assert.Contains(classifications, item => Slice(document, item) == "Hello " && item.Kind == FecsxClassificationKind.Text);
        Assert.Contains(classifications, item => Slice(document, item) == "props.Name" && item.Kind == FecsxClassificationKind.CSharpExpression);
    }

    [Test]
    public void UsesTheComponentContainingTheCaretAndClassifiesEveryComponent()
    {
        const string document = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(Props props, ReactNode[] children)
            {
                return <main>{props.Title}</main>;
            }
            public static ReactNode DarkMode(ThemeProps theme, ReactNode[] children) =>
                <Panel>{theme.Contrast}</Panel>;
            """;
        var position = document.IndexOf("theme.Contrast", StringComparison.Ordinal);

        Assert.NotEmpty(_service.GetBraceMatchingRanges(document, position));
        var classifications = _service.GetClassifications(document);
        Assert.Contains(classifications, item => Slice(document, item) == "main" && item.Kind == FecsxClassificationKind.TagName);
        Assert.Contains(classifications, item => Slice(document, item) == "Panel" && item.Kind == FecsxClassificationKind.TagName);
        Assert.Contains(classifications, item => Slice(document, item) == "theme.Contrast" && item.Kind == FecsxClassificationKind.CSharpExpression);
    }

    private static string Slice(string source, FecsxClassifiedSpan item) =>
        source.Substring(item.Span.Start, item.Span.Length);

    private static string Document(string body) =>
        "using Interop.JS.React;\nnamespace Demo;\npublic static ReactNode Default(Props props, ReactNode[] children) =>\n    " + body;

    private sealed class TestComponentMetadataProvider : IFecsxComponentMetadataProvider
    {
        public string? RequestedComponent { get; private set; }

        public bool TryGetProperties(string componentName, out IReadOnlyList<FecsxCompletionProperty> properties)
        {
            RequestedComponent = componentName;
            properties = new[]
            {
                new FecsxCompletionProperty("Name", FecsxCompletionValueKind.String),
                new FecsxCompletionProperty("IsDone", FecsxCompletionValueKind.Boolean),
                new FecsxCompletionProperty("OnToggle", FecsxCompletionValueKind.EventHandler),
            };
            return true;
        }
    }
}
