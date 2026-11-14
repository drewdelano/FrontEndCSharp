using System;
using System.ComponentModel.Composition;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType(FecsxContentType.Name)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class FecsxLinkedEditingControllerProvider : IWpfTextViewCreationListener
{
    public void TextViewCreated(IWpfTextView textView) =>
        textView.Properties.GetOrCreateSingletonProperty(() => new FecsxLinkedEditingController(textView));
}

internal sealed class FecsxLinkedEditingController : IDisposable
{
    private readonly IWpfTextView _view;
    private readonly ITextBuffer _buffer;
    private readonly FecsxEditorService _service = new FecsxEditorService();
    private bool _applyingLinkedEdit;

    public FecsxLinkedEditingController(IWpfTextView view)
    {
        _view = view;
        _buffer = view.TextBuffer;
        _buffer.ChangedLowPriority += OnBufferChanged;
        _view.Closed += OnClosed;
    }

    public void Dispose()
    {
        _buffer.ChangedLowPriority -= OnBufferChanged;
        _view.Closed -= OnClosed;
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        if (_applyingLinkedEdit || e.Changes.Count != 1)
        {
            return;
        }

        var change = e.Changes[0];
        var linkedEdit = _service.GetLinkedTagNameEdit(
            e.Before.GetText(),
            new FecsxTextSpan(change.OldPosition, change.OldLength),
            change.NewText);
        if (linkedEdit == null)
        {
            return;
        }

        var delta = change.NewLength - change.OldLength;
        var replacementStart = linkedEdit.Span.Start > change.OldPosition
            ? linkedEdit.Span.Start + delta
            : linkedEdit.Span.Start;
        if (replacementStart < 0 || replacementStart + change.OldLength > e.After.Length)
        {
            return;
        }

        _applyingLinkedEdit = true;
        try
        {
            using var edit = _buffer.CreateEdit();
            edit.Replace(new Span(replacementStart, linkedEdit.Span.Length), linkedEdit.NewText);
            edit.Apply();
        }
        finally
        {
            _applyingLinkedEdit = false;
        }
    }

    private void OnClosed(object sender, EventArgs e) => Dispose();
}
