using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.QuickInfo;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Utilities;
using VsQuickInfoItem = Microsoft.VisualStudio.Language.Intellisense.QuickInfoItem;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("FECSX C# quick info")]
[ContentType(FecsxContentType.Name)]
internal sealed class FecsxCSharpQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxCSharpQuickInfoSourceProvider(
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public IAsyncQuickInfoSource? TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
        if (!_documents.TryGetTextDocument(textBuffer, out var textDocument))
        {
            return null;
        }

        var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
        return workspace == null
            ? null
            : textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new FecsxCSharpQuickInfoSource(
                    textBuffer,
                    new FecsxRoslynDocumentService(workspace, textDocument.FilePath)));
    }
}

internal sealed class FecsxCSharpQuickInfoSource : IAsyncQuickInfoSource
{
    private readonly ITextBuffer _buffer;
    private readonly FecsxRoslynDocumentService _documents;

    public FecsxCSharpQuickInfoSource(ITextBuffer buffer, FecsxRoslynDocumentService documents)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
    }

    public async Task<VsQuickInfoItem?> GetQuickInfoItemAsync(
        IAsyncQuickInfoSession session,
        CancellationToken cancellationToken)
    {
        var snapshot = _buffer.CurrentSnapshot;
        var triggerPoint = session.GetTriggerPoint(snapshot);
        if (triggerPoint == null)
        {
            return null;
        }

        var projectedDocument = _documents.Create(snapshot.GetText());
        if (projectedDocument == null
            || !projectedDocument.Projection.TryMapSourceCaretPosition(
                triggerPoint.Value.Position,
                out var projectedPosition))
        {
            return null;
        }

        var service = QuickInfoService.GetService(projectedDocument.Document);
        if (service == null)
        {
            return null;
        }

        var info = await service.GetQuickInfoAsync(
            projectedDocument.Document,
            projectedPosition,
            cancellationToken).ConfigureAwait(false);
        if (info == null
            || !projectedDocument.Projection.TryMapProjectedSpan(
                new FecsxTextSpan(info.Span.Start, info.Span.Length),
                out var sourceSpan))
        {
            return null;
        }

        var text = string.Join(
            Environment.NewLine,
            info.Sections.Select(section => section.Text).Where(section => section.Length > 0));
        if (text.Length == 0)
        {
            return null;
        }

        var applicableTo = snapshot.CreateTrackingSpan(
            new Span(sourceSpan.Start, sourceSpan.Length),
            SpanTrackingMode.EdgeExclusive);
        return new VsQuickInfoItem(
            applicableTo,
            ClassifiedTextElement.CreatePlainText(text));
    }

    public void Dispose()
    {
    }
}
