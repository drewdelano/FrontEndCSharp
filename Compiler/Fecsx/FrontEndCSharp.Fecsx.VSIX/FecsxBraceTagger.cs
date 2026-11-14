using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IViewTaggerProvider))]
[ContentType(FecsxContentType.Name)]
[TextViewRole(PredefinedTextViewRoles.Document)]
[TagType(typeof(TextMarkerTag))]
internal sealed class FecsxBraceTaggerProvider : IViewTaggerProvider
{
    public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag =>
        ReferenceEquals(textView.TextBuffer, buffer)
            ? new FecsxBraceTagger(textView, buffer) as ITagger<T>
            : null;
}

internal sealed class FecsxBraceTagger : ITagger<TextMarkerTag>, IDisposable
{
    private readonly ITextView _view;
    private readonly ITextBuffer _buffer;
    private readonly FecsxEditorService _service = new FecsxEditorService();

    public FecsxBraceTagger(ITextView view, ITextBuffer buffer)
    {
        _view = view;
        _buffer = buffer;
        _view.Caret.PositionChanged += OnPositionChanged;
        _buffer.Changed += OnBufferChanged;
        _view.Closed += OnClosed;
    }

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0)
        {
            yield break;
        }

        var snapshot = spans[0].Snapshot;
        var caret = _view.Caret.Position.BufferPosition.TranslateTo(snapshot, PointTrackingMode.Positive).Position;
        foreach (var range in _service.GetBraceMatchingRanges(snapshot.GetText(), caret))
        {
            yield return new TagSpan<TextMarkerTag>(
                new SnapshotSpan(snapshot, range.Start, range.Length),
                new TextMarkerTag("bracehighlight"));
        }
    }

    public void Dispose()
    {
        _view.Caret.PositionChanged -= OnPositionChanged;
        _buffer.Changed -= OnBufferChanged;
        _view.Closed -= OnClosed;
    }

    private void OnPositionChanged(object sender, CaretPositionChangedEventArgs e) => RaiseChanged();
    private void OnBufferChanged(object sender, TextContentChangedEventArgs e) => RaiseChanged();
    private void OnClosed(object sender, EventArgs e) => Dispose();

    private void RaiseChanged()
    {
        var snapshot = _buffer.CurrentSnapshot;
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }
}
