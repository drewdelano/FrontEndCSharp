using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FrontEndCSharp.Fecsx.Editor.Tests;

public sealed class FecsxEditorDiagnosticsTests
{
    [Test]
    public void MapsAProjectedRoslynDiagnosticBackToTheAuthoredExpression()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <section>{props.Store.Missing}</section>;
            """;
        var projection = FecsxCSharpProjector.Create(source);
        var tree = CSharpSyntaxTree.ParseText(projection.Text);
        var compilation = CSharpCompilation.Create("Diagnostics")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("""
                    namespace Interop.JS.React { public sealed class ReactNode { } }
                    namespace Demo
                    {
                        public sealed class StoreProps { public Store Store { get; } = new Store(); }
                        public sealed class Store { public int EnergyLevel { get; set; } }
                    }
                    """),
                tree);

        var diagnostics = FecsxEditorDiagnostics.MapRoslynDiagnostics(
            projection,
            compilation.GetDiagnostics());

        var diagnostic = Assert.Single(diagnostics, item => item.Id == "CS1061");
        Assert.Equal("Missing", source.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }

    [Test]
    public void ProducesNativeFecsxDiagnosticsAtAuthoredMarkupSpans()
    {
        const string source = """
            using Interop.JS.React;
            namespace Demo;
            public static ReactNode Default(StoreProps props, ReactNode[] children) =>
                <main />;
            public static ReactNode Details(StoreProps props, ReactNode[] children) =>
                <section definitelyNotAnHtmlProperty="value" />;
            """;

        var diagnostics = FecsxEditorDiagnostics.GetNativeDiagnostics(source);

        var diagnostic = Assert.Single(diagnostics, item => item.Id == "FECSX4002");
        Assert.Equal(
            "definitelyNotAnHtmlProperty",
            source.Substring(diagnostic.Span.Start, diagnostic.Span.Length));
    }
}
