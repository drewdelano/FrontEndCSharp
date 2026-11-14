using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(ICompletionSourceProvider))]
[Name("FECSX completion")]
[ContentType(FecsxContentType.Name)]
internal sealed class FecsxCompletionSourceProvider : ICompletionSourceProvider
{
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxCompletionSourceProvider(
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
    {
        if (!_documents.TryGetTextDocument(textBuffer, out var textDocument))
        {
            return new FecsxCompletionSource(textBuffer, null);
        }

        var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
        return new FecsxCompletionSource(
            textBuffer,
            workspace == null
                ? null
                : new FecsxRoslynDocumentService(workspace, textDocument.FilePath));
    }
}

internal sealed class FecsxCompletionSource : ICompletionSource
{
    private readonly ITextBuffer _buffer;
    private readonly FecsxRoslynDocumentService? _documents;
    private readonly FecsxEditorService _service = new FecsxEditorService();

    public FecsxCompletionSource(
        ITextBuffer buffer,
        FecsxRoslynDocumentService? documents)
    {
        _buffer = buffer;
        _documents = documents;
    }

    public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
        var snapshot = _buffer.CurrentSnapshot;
        var triggerPoint = session.GetTriggerPoint(snapshot);
        if (triggerPoint == null)
        {
            return;
        }

        var position = triggerPoint.Value.Position;
        var source = snapshot.GetText();
        if (FecsxCSharpProjector.Create(source).TryMapSourceCaretPosition(position, out _))
        {
            return;
        }

        var attributeCompletions = _service.GetAttributeCompletions(
            source,
            position,
            attributeCasing: FecsxProjectConfiguration.GetAttributeCasing(
                _documents?.FindOwningProject()));
        var items = attributeCompletions
            .Select(item => new Completion(item.DisplayText, item.InsertText, item.DisplayText, null, null))
            .ToList();
        var replacement = attributeCompletions.FirstOrDefault()?.ReplacementSpan
            ?? new FecsxTextSpan(position, 0);

        var closingName = _service.GetClosingTagNameCompletion(source, position);
        if (closingName !=  null)
        {
            replacement = closingName.Span;
            items.Add(new Completion(closingName.NewText, closingName.NewText, "Complete the matching opening tag", null, null));
        }
        else
        {
            var closingTag = _service.GetClosingTagCompletion(source, position);
            if (closingTag !=  null)
            {
                replacement = closingTag.Span;
                items.Add(new Completion(closingTag.NewText, closingTag.NewText, "Insert the matching closing tag", null, null));
            }
        }

        if (items.Count == 0)
        {
            return;
        }

        var applicableTo = snapshot.CreateTrackingSpan(
            new Span(replacement.Start, replacement.Length),
            SpanTrackingMode.EdgeInclusive);
        completionSets.Add(new CompletionSet("FECSX", "FECSX", applicableTo, items, null));
    }

    public void Dispose()
    {
    }
}
