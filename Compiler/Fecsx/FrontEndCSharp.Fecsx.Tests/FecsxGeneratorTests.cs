using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

namespace FrontEndCSharp.Fecsx.Tests;

public sealed class FecsxGeneratorTests
{
    [Test]
    public void ParsesARequiredDefaultAndNamedComponents()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;

            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main>{children}</main>;

            public static ReactNode DarkMode(ThemeProps props, ReactNode[] children) =>
                <section className="dark">{children}</section>;
            """;

        var syntax = FecsxRenderDocumentParser.Parse(source, "App.fecsx");

        Assert.Empty(syntax.Diagnostics);
        Assert.Equal("App", syntax.FacadeName);
        Assert.Equal("Default", Assert.IsType<Syntax.FecsxComponentDeclarationSyntax>(syntax.DefaultComponent).Name);
        Assert.Collection(
            syntax.Components,
            component => Assert.Equal("Default", component.Name),
            component =>
            {
                Assert.Equal("DarkMode", component.Name);
                Assert.Equal("ThemeProps", component.PropsTypeName);
            });
    }

    [Test]
    public void GeneratesTheDefaultFacadeAndNestedNamedFacades()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;

            public static ReactNode Default(AppProps props, ReactNode[] children) =>
                <main><App.DarkMode>{children}</App.DarkMode></main>;

            public static ReactNode DarkMode(ThemeProps props, ReactNode[] children) =>
                <section className="dark">{children}</section>;
            """;

        var result = RunGenerator("App.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("public static class App", generated);
        Assert.Contains("React.Component<global::Demo.AppProps>(RenderDefault)", generated);
        Assert.Contains("public static class DarkMode", generated);
        Assert.Contains("React.Component<global::Demo.ThemeProps>(Render)", generated);
        Assert.Contains("App.DarkMode.Create(__props =>", generated);
    }

    [TestCase("public static ReactNode Card(AppProps props, ReactNode[] children) => <div />;", "FECSX3009")]
    [TestCase("public static ReactNode Default(AppProps props, ReactNode[] children) => <div />; public static ReactNode Default(AppProps props, ReactNode[] children) => <span />;", "FECSX3009")]
    [TestCase("public static ReactNode Default(AppProps props, ReactNode[] children) => <div />; public static ReactNode Card(AppProps props, ReactNode[] children) => <span />; public static ReactNode Card(AppProps props, ReactNode[] children) => <aside />;", "FECSX3010")]
    [TestCase("public static ReactNode Default(AppProps props, ReactNode[] children) => <div />", "FECSX3012")]
    public void RejectsInvalidMultiComponentDocumentContracts(string declarations, string diagnosticId)
    {
        var result = RunGenerator("App.fecsx", "using Interop.JS.React; namespace Demo; " + declarations);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void RejectsAFileNameThatCannotBeTheFacadeIdentity()
    {
        const string source = "using Interop.JS.React; namespace Demo; public static ReactNode Default(AppProps props, ReactNode[] children) => <div />;";

        var result = RunGenerator("app-shell.fecsx", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3011");
    }

    [Test]
    public void ParsesAPublicStaticComponentWhoseResultIsMarkup()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children) =>
                <section>Hello {props.Name}</section>;
            """;
        var syntax = FecsxRenderDocumentParser.Parse(source, "Greeting.fecsx");
        Assert.Empty(syntax.Diagnostics);
        Assert.Equal("Demo", syntax.NamespaceName);
        Assert.Equal("Greeting", syntax.FacadeName);
        var component = Assert.IsType<Syntax.FecsxComponentDeclarationSyntax>(syntax.DefaultComponent);
        Assert.Equal("GreetingProps", component.PropsTypeName);
        Assert.Equal("section", Assert.IsType<Syntax.FecsxElementSyntax>(component.Body).Name);
    }

    [Test]
    public void ParsesABlockBodiedComponentWhoseSingleReturnIsMarkup()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children)
            {
                return <section>Hello {props.Name}</section>;
            }
            public static ReactNode Compact(GreetingProps props, ReactNode[] children) =>
                <span>{props.Name}</span>;
            """;

        var syntax = FecsxRenderDocumentParser.Parse(source, "Greeting.fecsx");

        Assert.Empty(syntax.Diagnostics);
        Assert.Collection(
            syntax.Components,
            component =>
            {
                Assert.Equal("Default", component.Name);
                Assert.Equal(Syntax.FecsxComponentBodyKind.Block, component.BodyKind);
                Assert.Equal('<', source[component.BodyOffset]);
                Assert.Equal("section", Assert.IsType<Syntax.FecsxElementSyntax>(component.Body).Name);
            },
            component =>
            {
                Assert.Equal("Compact", component.Name);
                Assert.Equal(Syntax.FecsxComponentBodyKind.Expression, component.BodyKind);
            });
    }

    [Test]
    public void GeneratesAndCompilesABlockBodiedComponent()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children)
            {
                return <section className="greeting">Hello {props.Name}</section>;
            }
            """;

        var result = RunGenerator("Greeting.fecsx", source, ReactContract);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("React.Text(\"Hello \")", GetComponentSource(result));
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void GeneratesAndCompilesAComponentWithAWrappedMarkupLambda()
    {
        const string source = """
            using Interop.JS.MobX.React;
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children) =>
                MobXReact.Observe(() =>
                    <section className="greeting">Hello {props.Name}</section>
                );
            """;
        const string supportSource = ReactContract + """

            namespace Interop.JS.MobX.React
            {
                public static class MobXReact
                {
                    public static Interop.JS.React.ReactElement Observe(System.Func<Interop.JS.React.ReactNode> render) => null!;
                }
            }
            """;

        var result = RunGenerator("Greeting.fecsx", source, supportSource);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("MobXReact.Observe(() =>", generated, StringComparison.Ordinal);
        Assert.Contains("React.Text(\"Hello \")", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void ParsesABlockBodiedComponentWithAWrappedMarkupLambda()
    {
        const string source = """
            using Interop.JS.MobX.React;
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children)
            {
                return MobXReact.Observe(() =>
                    <section>{props.Name}</section>
                );
            }
            """;

        var syntax = FecsxRenderDocumentParser.Parse(source, "Greeting.fecsx");

        Assert.Empty(syntax.Diagnostics);
        var component = Assert.IsType<Syntax.FecsxComponentDeclarationSyntax>(syntax.DefaultComponent);
        Assert.Equal(Syntax.FecsxComponentBodyKind.Block, component.BodyKind);
        Assert.Equal('M', source[component.ExpressionOffset]);
        Assert.Equal('<', source[component.BodyOffset]);
        Assert.Equal(')', source[component.ExpressionEnd - 1]);
        Assert.Equal("section", Assert.IsType<Syntax.FecsxElementSyntax>(component.Body).Name);
    }

    [Test]
    public void RejectsAWrappedBodyThatIsNotASingleZeroParameterMarkupLambda()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children) =>
                Wrap(props.Name, () => <section />);
            """;

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3014");
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void DoesNotMistakeAMethodInsideAPreambleTypeForABlockBodiedComponent()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public sealed class PreambleProps
            {
                public string Name { get; set; } = "";
                public static string Describe()
                {
                    return "Greeting";
                }
            }
            public static ReactNode Default(PreambleProps props, ReactNode[] children)
            {
                return <section>{props.Name}</section>;
            }
            """;

        var result = RunGenerator("Greeting.fecsx", source, ReactContract);

        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void RejectsAdditionalStatementsInABlockBodiedComponent()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children)
            {
                var title = props.Name;
                return <section>{title}</section>;
            }
            """;

        var result = RunGenerator("Greeting.fecsx", source, ReactContract);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3013");
        Assert.DoesNotContain(
            result.GeneratedTrees,
            tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void GeneratesAStableComponentAroundTheRenderLambda()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children) =>
                <><section className="greeting" hidden={props.Hidden}>Hello {props.Name}</section>{children[0]}</>;
            """;
        var result = RunGenerator("Greeting.fecsx", source);
        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("public static class Greeting", generated);
        Assert.Contains("React.Component<global::Demo.GreetingProps>(RenderDefault)", generated);
        Assert.Contains("React.CreateFragment", generated);
        Assert.Contains("React.Node(props.Name)", generated);
        Assert.Contains("React.Node(children[0])", generated);
    }

    [Test]
    public void GeneratedComponentsComposeAsJsxTagsWithTypedAssignments()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(DashboardProps props, ReactNode[] children) =>
                <TaskCard name={props.Name} isDone />;
            """;
        var result = RunGenerator("Dashboard.fecsx", source);
        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("TaskCard.Create(__props =>", generated);
        Assert.Contains("__props.Name = props.Name;", generated);
        Assert.Contains("__props.IsDone = true;", generated);
    }

    [Test]
    public void GeneratedRenderLambdaCompilesAgainstTheManagedReactContract()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(GreetingProps props, ReactNode[] children) =>
                <><section className="greeting">Hello {props.Name}</section><TaskCard name={props.Name} /></>;
            """;
        var result = RunGenerator("Greeting.fecsx", source, ReactContract);
        Assert.Empty(result.Diagnostics);
        Assert.DoesNotContain(result.OutputCompilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void ResolvesAPropsTypeImportedFromAnotherNamespace()
    {
        const string source = """
            using Interop.JS.React;
            using Demo.Models;
            namespace Demo.Components;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{props.Name}</section>;
            """;
        const string supportSource = """
            namespace Demo.Models;
            public sealed class StoreProps
            {
                public string Name { get; set; } = "";
            }
            """;

        var result = RunGenerator("Store.fecsx", source, supportSource);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("React.Component<global::Demo.Models.StoreProps>(RenderDefault)", generated);
        Assert.DoesNotContain("global::Demo.Components.StoreProps", generated);
        Assert.DoesNotContain(result.OutputCompilation.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void GeneratesTypedIntrinsicPropertiesFromTheCanonicalSchema()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button />;";
        var result = RunGenerator("Greeting.fecsx", source);

        var generated = Assert.Single(
            result.GeneratedTrees,
            tree => tree.FilePath.EndsWith("FecsxIntrinsicProps.g.cs", StringComparison.Ordinal)).GetText().ToString();
        Assert.Contains("public sealed class ButtonProps : HtmlProps", generated);
        Assert.Contains("public string? ClassName { get; set; }", generated);
        Assert.Contains("public bool Disabled", generated);
        Assert.Contains("public global::System.Action<global::Interop.JS.Browser.DomEvent>? OnClick { get; set; }", generated);
        Assert.Contains("props.SetString(\"aria-label\", this.AriaLabel)", generated);
    }

    [Test]
    public void IntrinsicAttributesLowerThroughTheirTypedElementProperties()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button className=\"primary\" aria-label=\"Greeting\" disabled={props.Hidden} />;";
        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("ButtonProps.Create(__props =>", generated);
        Assert.Contains("__props.ClassName = \"primary\";", generated);
        Assert.Contains("__props.AriaLabel = \"Greeting\";", generated);
        Assert.Contains("__props.Disabled = props.Hidden;", generated);
    }

    [Test]
    public void SharedAriaAndDataSlotAttributesLowerThroughTypedIntrinsicProperties()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <span data-slot=\"breadcrumb-page\" aria-current=\"page\" aria-disabled={props.Hidden} aria-hidden={false} />;";
        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("__props.DataSlot = \"breadcrumb-page\";", generated);
        Assert.Contains("__props.AriaCurrent = \"page\";", generated);
        Assert.Contains("__props.AriaDisabled = props.Hidden;", generated);
        Assert.Contains("__props.AriaHidden = false;", generated);
    }

    [Test]
    public void PascalCaseProjectPreferenceLowersIntrinsicAttributesThroughCanonicalProperties()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button ClassName=\"primary\" AriaLabel=\"Greeting\" Disabled={props.Hidden} />;";

        var result = RunGenerator(
            "Greeting.fecsx",
            source,
            attributeCasing: "PascalCase");

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("__props.ClassName = \"primary\";", generated);
        Assert.Contains("__props.AriaLabel = \"Greeting\";", generated);
        Assert.Contains("__props.Disabled = props.Hidden;", generated);
    }

    [Test]
    public void CamelCaseProjectPreferenceLowersManagedAttributesThroughActualCSharpProperties()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <TaskCard name={props.Name} isDone />;";

        var result = RunGenerator(
            "Greeting.fecsx",
            source,
            ReactContract,
            "camelCase");

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("__props.Name = props.Name;", generated);
        Assert.Contains("__props.IsDone = true;", generated);
        Assert.DoesNotContain(
            result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Test]
    public void DefaultCamelCaseRejectsPascalCaseIntrinsicAttributes()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button ClassName=\"primary\" />;";

        var result = RunGenerator("Greeting.fecsx", source);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Id == "FECSX4006");
        Assert.Contains("ClassName", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("className", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(result.GeneratedTrees.Where(tree =>
            tree.FilePath.EndsWith("Greeting.fecsx.g.cs", StringComparison.Ordinal)));
    }

    [Test]
    public void DefaultCamelCaseRejectsPascalCaseManagedAttributes()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <TaskCard Name={props.Name} />;";

        var result = RunGenerator("Greeting.fecsx", source, ReactContract);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Id == "FECSX4006");
        Assert.Contains("Name", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("name", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Test]
    public void PascalCaseRejectsCamelCaseAttributes()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <TaskCard name={props.Name} />;";

        var result = RunGenerator(
            "Greeting.fecsx",
            source,
            ReactContract,
            "PascalCase");

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Id == "FECSX4006");
        Assert.Contains("name", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Name", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Test]
    public void RejectsAnInvalidProjectAttributeCasingValue()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div />;";

        var result = RunGenerator(
            "Greeting.fecsx",
            source,
            attributeCasing: "snake_case");

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Id == "FECSX5001");
        Assert.Contains("camelCase", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("PascalCase", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("snake_case", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Test]
    public void TreatsIntrinsicCasingAliasesAsTheSameAttribute()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div className=\"one\" ClassName=\"two\" />;";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Single(result.Diagnostics, item => item.Id == "FECSX4005");
    }

    [Test]
    public void NormalizesMultilineChildTextLikeReactJsxWhilePreservingInlineSpaces()
    {
        const string source = """
            namespace Demo;
            public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) =>
                <div>
                    Alpha
                    Beta
                    <span> inline </span>
                    Gamma
                </div>;
            """;

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("React.Text(" + SymbolDisplay.FormatLiteral("Alpha Beta", true) + ")", generated);
        Assert.Contains("React.Text(" + SymbolDisplay.FormatLiteral(" inline ", true) + ")", generated);
        Assert.Contains("React.Text(" + SymbolDisplay.FormatLiteral("Gamma", true) + ")", generated);
        Assert.DoesNotContain("React.Text(" + SymbolDisplay.FormatLiteral(Environment.NewLine, true), generated);
    }

    [Test]
    public void OmitsMultilineIndentationOnlyChildrenButRetainsSingleLineWhitespace()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div><span />   <span />\n\t  <span /></div>;";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        Assert.Contains("React.Text(\"   \")", generated);
        Assert.Equal(1, generated.Split(new[] { "React.Text(" }, StringSplitOptions.None).Length - 1);
    }

    [Test]
    public void DecodesJsxEntitiesInTextAndQuotedAttributesWithoutAcceptingInvalidForms()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div title=\"A &amp; &#65; &#x1F680; &#X41; &unknown;\">&copy; &there4; &#65; &#x1F680; &#X41; &unknown;</div>;";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        var generated = GetComponentSource(result);
        var expectedAttribute = "A & A 🚀 &#X41; &unknown;";
        var expectedText = "© ∴ A 🚀 &#X41; &unknown;";
        Assert.Contains("__props.Title = " + SymbolDisplay.FormatLiteral(expectedAttribute, true) + ";", generated);
        Assert.Contains("React.Text(" + SymbolDisplay.FormatLiteral(expectedText, true) + ")", generated);
    }

    [Test]
    public void RejectsAttributesThatAreNotValidForAnIntrinsicElement()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button href=\"/wrong\" />;";
        var result = RunGenerator("Greeting.fecsx", source);

        var diagnostic = Assert.Single(result.Diagnostics, item => item.Id == "FECSX4002");
        Assert.Equal("href", source.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Test]
    public void LetsRoslynTypeCheckIntrinsicAttributeExpressions()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <button disabled={props.Name} />;";
        var result = RunGenerator("Greeting.fecsx", source, ReactContract);

        var diagnostic = Assert.Single(
            result.OutputCompilation.GetDiagnostics(),
            item => item.Id == "CS0029" && item.Severity == DiagnosticSeverity.Error);
        Assert.Equal("Greeting.fecsx", diagnostic.Location.GetMappedLineSpan().Path);
    }

    [Test]
    public void EmitsPortablePdbSequencePointsForTheOriginalFecsxDocument()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp.Tests",
            "Components",
            "Greeting.fecsx");
        const string source = """
            namespace Demo;
            public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children)
            {
                return <section>Hello {props.Name}</section>;
            }
            public static Interop.JS.React.ReactNode DarkMode(GreetingProps props, Interop.JS.React.ReactNode[] children) =>
                <aside>Dark {props.Name}</aside>;
            """;
        var result = RunGenerator(path, source, ReactContract);
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(
            peStream,
            pdbStream,
            options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        peStream.Position = 0;
        pdbStream.Position = 0;
        using var peReader = new PEReader(peStream);
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var assemblyMetadata = peReader.GetMetadataReader();
        var pdbMetadata = pdbProvider.GetMetadataReader();
        var documents = pdbMetadata.Documents
            .Select(handle => pdbMetadata.GetString(pdbMetadata.GetDocument(handle).Name));
        var fecsxDocument = Assert.Single(
            documents,
            document => document.EndsWith("Greeting.fecsx", StringComparison.Ordinal));
        Assert.Equal(path, fecsxDocument, ignoreCase: true);

        var renderMethod = assemblyMetadata.MethodDefinitions.Single(handle =>
            assemblyMetadata.GetString(assemblyMetadata.GetMethodDefinition(handle).Name) == "RenderDefault"
            && assemblyMetadata.GetString(assemblyMetadata.GetTypeDefinition(
                assemblyMetadata.GetMethodDefinition(handle).GetDeclaringType()).Name) == "Greeting");
        var debugHandle = MetadataTokens.MethodDebugInformationHandle(MetadataTokens.GetRowNumber(renderMethod));
        var sequencePoints = pdbMetadata.GetMethodDebugInformation(debugHandle)
            .GetSequencePoints()
            .Where(point => !point.IsHidden)
            .ToArray();
        Assert.Contains(sequencePoints, point => point.StartLine == 4);
        Assert.All(sequencePoints, point =>
        {
            var documentHandle = point.Document.IsNil
                ? pdbMetadata.GetMethodDebugInformation(debugHandle).Document
                : point.Document;
            Assert.Equal(path, pdbMetadata.GetString(pdbMetadata.GetDocument(documentHandle).Name), ignoreCase: true);
        });

        var namedRenderMethod = assemblyMetadata.MethodDefinitions.Single(handle =>
            assemblyMetadata.GetString(assemblyMetadata.GetMethodDefinition(handle).Name) == "Render"
            && assemblyMetadata.GetString(assemblyMetadata.GetTypeDefinition(
                assemblyMetadata.GetMethodDefinition(handle).GetDeclaringType()).Name) == "DarkMode");
        var namedDebugHandle = MetadataTokens.MethodDebugInformationHandle(MetadataTokens.GetRowNumber(namedRenderMethod));
        var namedSequencePoints = pdbMetadata.GetMethodDebugInformation(namedDebugHandle)
            .GetSequencePoints()
            .Where(point => !point.IsHidden)
            .ToArray();
        Assert.Contains(namedSequencePoints, point => point.StartLine == 7);
    }

    [Test]
    public void EmitsPortablePdbSequencePointsForCapturedAndNonCapturingEventHandlers()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "FrontEndCSharp.Tests",
            "Components",
            "InteractiveButton.fecsx");
        const string source = """
            using Interop.JS.React;
            namespace Demo;

            public static ReactNode Default(EventProps props, ReactNode[] children) =>
                <div>
                    <button onClick={_ => props.Toggle()}>Captured</button>
                    <button onClick={_ => EventProps.ToggleGlobal()}>Non-capturing</button>
                </div>;
            """;
        const string supportSource = """
            namespace Demo;
            public sealed class EventProps
            {
                public void Toggle() { }
                public static void ToggleGlobal() { }
            }
            """;
        var result = RunGenerator(path, source, supportSource);
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(
            peStream,
            pdbStream,
            options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        peStream.Position = 0;
        pdbStream.Position = 0;
        using var peReader = new PEReader(peStream);
        using var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var assemblyMetadata = peReader.GetMetadataReader();
        var pdbMetadata = pdbProvider.GetMetadataReader();
        var handlerMethods = assemblyMetadata.MethodDefinitions.Where(handle =>
            assemblyMetadata.GetString(assemblyMetadata.GetMethodDefinition(handle).Name).Contains("b__", StringComparison.Ordinal));
        var handlerSequencePoints = handlerMethods.SelectMany(handle =>
            pdbMetadata.GetMethodDebugInformation(MetadataTokens.MethodDebugInformationHandle(MetadataTokens.GetRowNumber(handle)))
                .GetSequencePoints()
                .Where(point => !point.IsHidden));

        var handlerLines = handlerSequencePoints.Select(point => point.StartLine).ToArray();
        Assert.Contains(6, handlerLines);
        Assert.Contains(7, handlerLines);
    }

    [Test]
    public void ReportsLambdaAndMarkupDiagnosticsAtOriginalLocations()
    {
        const string source = """
            namespace Demo;
            public static Interop.JS.React.ReactNode Default(BrokenProps props) =>
                <div><span></div>;
            """;
        var result = RunGenerator("Broken.fecsx", source);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3002");
        var markup = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX1005");
        Assert.Equal(2, markup.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Test]
    public void UsesTheFileNameAsTheDefaultComponentIdentity()
    {
        const string source = "namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div />;";

        var result = RunGenerator("DifferentFileName.fecsx", source);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("public static class DifferentFileName", GetComponentSource(result));
    }

    [Test]
    public void RejectsTheFormerAnonymousRenderLambdaForm()
    {
        const string source = "namespace Demo; (GreetingProps props, Interop.JS.React.ReactNode[] children) => <div />";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3001");
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [TestCase("static", "FECSX3006")]
    [TestCase("public", "FECSX3006")]
    [TestCase("public static", "FECSX3007", "string")]
    public void RequiresTheExportedComponentContract(string modifiers, string diagnosticId, string returnType = "Interop.JS.React.ReactNode")
    {
        var source = $"namespace Demo; {modifiers} {returnType} Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div />;";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [TestCase("component")]
    [TestCase("class")]
    [TestCase("@class")]
    public void RejectsComponentNamesThatCannotRepresentAReactComponentFacade(string componentName)
    {
        var source = $"namespace Demo; public static Interop.JS.React.ReactNode Default(GreetingProps props, Interop.JS.React.ReactNode[] children) => <div />; public static Interop.JS.React.ReactNode {componentName}(GreetingProps props, Interop.JS.React.ReactNode[] children) => <span />;";

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "FECSX3008");
        Assert.DoesNotContain(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal));
    }

    [Test]
    public void ParsesAMultilineComponentDeclaration()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;

            public static ReactNode Default(
                GreetingProps props,
                ReactNode[] children) =>
                <div />;
            """;

        var result = RunGenerator("Greeting.fecsx", source);

        Assert.Empty(result.Diagnostics);
        Assert.Contains("public static class Greeting", GetComponentSource(result));
    }

    private static GeneratorRunResult RunGenerator(
        string path,
        string fecsx,
        string? supportSource = null,
        string? attributeCasing = null)
    {
        var trees = supportSource == null ? Array.Empty<SyntaxTree>() : new[] { CSharpSyntaxTree.ParseText(supportSource) };
        _ = typeof(Interop.JS.React.React).Assembly;
        _ = typeof(Interop.JS.Browser.DomEvent).Assembly;
        var references = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location)).Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
        var compilation = CSharpCompilation.Create("Tests", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new FecsxGenerator().AsSourceGenerator() },
            new[] { new InMemoryAdditionalText(path, fecsx) },
            optionsProvider: new InMemoryAnalyzerConfigOptionsProvider(attributeCasing));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        return new GeneratorRunResult(driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).Select(source => source.SyntaxTree).ToImmutableArray(), diagnostics, output);
    }

    private static string GetComponentSource(GeneratorRunResult result) =>
        Assert.Single(result.GeneratedTrees, tree => tree.FilePath.EndsWith(".fecsx.g.cs", StringComparison.Ordinal))
            .GetText()
            .ToString();

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public InMemoryAdditionalText(string path, string text) { Path = path; _text = SourceText.From(text, Encoding.UTF8); }
        public override string Path { get; }
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class InMemoryAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions;

        public InMemoryAnalyzerConfigOptionsProvider(string? attributeCasing)
        {
            _globalOptions = new InMemoryAnalyzerConfigOptions(attributeCasing);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => InMemoryAnalyzerConfigOptions.Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => InMemoryAnalyzerConfigOptions.Empty;
    }

    private sealed class InMemoryAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly string? _attributeCasing;

        public static InMemoryAnalyzerConfigOptions Empty { get; } = new(null);

        public InMemoryAnalyzerConfigOptions(string? attributeCasing)
        {
            _attributeCasing = attributeCasing;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (_attributeCasing != null
                && key == "build_property.FrontEndCSharpFecsxAttributeCasing")
            {
                value = _attributeCasing;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }

    private sealed record GeneratorRunResult(ImmutableArray<SyntaxTree> GeneratedTrees, ImmutableArray<Diagnostic> Diagnostics, Compilation OutputCompilation);

    private const string ReactContract = """
        #nullable enable
        using System;
        namespace Demo {
        public sealed class GreetingProps { public string Name {get;set;}=""; public bool Hidden {get;set;} }
        public sealed class TaskCardProps { public string Name {get;set;}=""; public bool IsDone {get;set;} }
        public static class TaskCard { public static Interop.JS.React.ReactElement Create(Action<TaskCardProps> c,params Interop.JS.React.ReactNode[] n)=>null!; }}
        """;
}
