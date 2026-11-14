using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows.Threading;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType(FecsxContentType.Name)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class FecsxTailwindCompletionSessionControllerProvider : IWpfTextViewCreationListener
{
    private readonly IAsyncCompletionBroker _completionBroker;
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxTailwindCompletionSessionControllerProvider(
        IAsyncCompletionBroker completionBroker,
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _completionBroker = completionBroker;
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public void TextViewCreated(IWpfTextView textView)
    {
        if (!_documents.TryGetTextDocument(textView.TextBuffer, out var textDocument))
        {
            return;
        }

        textView.Properties.GetOrCreateSingletonProperty(
            () => new FecsxTailwindCompletionSessionController(
                textView,
                _completionBroker,
                () =>
                {
                    var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
                    return workspace != null
                        && new FecsxRoslynDocumentService(workspace, textDocument.FilePath)
                            .IsTailwindAvailable();
                }));
    }
}

internal sealed class FecsxTailwindCompletionSessionController : IDisposable
{
    private readonly IWpfTextView _view;
    private readonly ITextBuffer _buffer;
    private readonly IAsyncCompletionBroker _completionBroker;
    private readonly Func<bool> _isTailwindAvailable;
    private readonly FecsxEditorService _editor = new FecsxEditorService();
    private int _refreshGeneration;
    private bool _disposed;

    public FecsxTailwindCompletionSessionController(
        IWpfTextView view,
        IAsyncCompletionBroker completionBroker,
        Func<bool> isTailwindAvailable)
    {
        _view = view;
        _buffer = view.TextBuffer;
        _completionBroker = completionBroker;
        _isTailwindAvailable = isTailwindAvailable;
        _buffer.ChangedLowPriority += OnBufferChanged;
        _view.Caret.PositionChanged += OnCaretPositionChanged;
        _view.Closed += OnClosed;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _refreshGeneration);
        _buffer.ChangedLowPriority -= OnBufferChanged;
        _view.Caret.PositionChanged -= OnCaretPositionChanged;
        _view.Closed -= OnClosed;
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        if (_disposed || e.Changes.Count == 0 || !_isTailwindAvailable())
        {
            return;
        }

        var caret = _view.Caret.Position.BufferPosition;
        if (!ReferenceEquals(caret.Snapshot.TextBuffer, _buffer)
            || !_editor.NeedsTailwindClassCompletionRefresh(
                e.After.GetText(),
                caret.Position))
        {
            return;
        }

        ScheduleRefresh();
    }

    private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
        if (_disposed
            || _completionBroker.GetSession(_view) == null
            || !_isTailwindAvailable())
        {
            return;
        }

        var caret = e.NewPosition.BufferPosition;
        if (!ReferenceEquals(caret.Snapshot.TextBuffer, _buffer)
            || !_editor.NeedsTailwindClassCompletionRefresh(
                caret.Snapshot.GetText(),
                caret.Position))
        {
            return;
        }

        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        var generation = Interlocked.Increment(ref _refreshGeneration);
#pragma warning disable VSTHRD001 // The low-priority dispatcher turn must run after the current editor input transaction.
        _ = _view.VisualElement.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => RefreshCompletion(generation)));
#pragma warning restore VSTHRD001
    }

    private void RefreshCompletion(int generation)
    {
        if (_disposed
            || generation != _refreshGeneration
            || _view.IsClosed
            || !_isTailwindAvailable())
        {
            return;
        }

        var caret = _view.Caret.Position.BufferPosition;
        if (!ReferenceEquals(caret.Snapshot.TextBuffer, _buffer)
            || !_editor.NeedsTailwindClassCompletionRefresh(
                caret.Snapshot.GetText(),
                caret.Position))
        {
            return;
        }

        _completionBroker.GetSession(_view)?.Dismiss();
        var trigger = new CompletionTrigger(
            CompletionTriggerReason.Invoke,
            _view.TextSnapshot);
        var session = _completionBroker.TriggerCompletion(
            _view,
            trigger,
            caret,
            CancellationToken.None);
        session?.OpenOrUpdate(
            trigger,
            caret,
            CancellationToken.None);
    }

    private void OnClosed(object sender, EventArgs e) => Dispose();
}
