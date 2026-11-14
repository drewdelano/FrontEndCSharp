using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrontEndCSharp.Fecsx.Editor;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(ITaggerProvider))]
[ContentType(FecsxContentType.Name)]
[TagType(typeof(ErrorTag))]
internal sealed class FecsxDiagnosticTaggerProvider : ITaggerProvider
{
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxDiagnosticTaggerProvider(
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public ITagger<T>? CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
        if (!_documents.TryGetTextDocument(buffer, out var document))
        {
            return null;
        }

        var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
        if (workspace == null)
        {
            return null;
        }

        return buffer.Properties.GetOrCreateSingletonProperty(
            () => new FecsxDiagnosticTagger(buffer, document, _documents, workspace)) as ITagger<T>;
    }
}

internal sealed class FecsxDiagnosticTagger : ITagger<ErrorTag>, IDisposable
{
    private readonly ITextBuffer _buffer;
    private readonly ITextDocument _document;
    private readonly ITextDocumentFactoryService _documents;
    private readonly Workspace _workspace;
    private readonly FecsxRoslynDocumentService _roslynDocuments;
    private readonly string _filePath;
    private readonly object _gate = new object();
    private CancellationTokenSource _semanticCancellation = new CancellationTokenSource();
    private ITextSnapshot? _taggedSnapshot;
    private IReadOnlyList<FecsxEditorDiagnostic> _nativeDiagnostics = Array.Empty<FecsxEditorDiagnostic>();
    private IReadOnlyList<FecsxEditorDiagnostic> _semanticDiagnostics = Array.Empty<FecsxEditorDiagnostic>();
    private bool _disposed;

    public FecsxDiagnosticTagger(
        ITextBuffer buffer,
        ITextDocument document,
        ITextDocumentFactoryService documents,
        Workspace workspace)
    {
        _buffer = buffer;
        _document = document;
        _documents = documents;
        _workspace = workspace;
        _filePath = Path.GetFullPath(document.FilePath);
        _roslynDocuments = new FecsxRoslynDocumentService(workspace, _filePath);
        _buffer.Changed += OnBufferChanged;
        _workspace.WorkspaceChanged += OnWorkspaceChanged;
        _documents.TextDocumentDisposed += OnTextDocumentDisposed;
        UpdateNativeDiagnostics(_buffer.CurrentSnapshot);
        ScheduleSemanticDiagnostics(_buffer.CurrentSnapshot, 0);
    }

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

    public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0)
        {
            yield break;
        }

        var snapshot = spans[0].Snapshot;
        IReadOnlyList<FecsxEditorDiagnostic> diagnostics;
        lock (_gate)
        {
            if (!ReferenceEquals(snapshot, _taggedSnapshot))
            {
                yield break;
            }

            diagnostics = _nativeDiagnostics.Concat(_semanticDiagnostics).ToArray();
        }

        foreach (var diagnostic in diagnostics)
        {
            var span = CreateSnapshotSpan(snapshot, diagnostic.Span.Start, diagnostic.Span.Length);
            if (span == null || !spans.IntersectsWith(span.Value))
            {
                continue;
            }

            var errorType = diagnostic.Severity == DiagnosticSeverity.Warning
                ? PredefinedErrorTypeNames.Warning
                : PredefinedErrorTypeNames.CompilerError;
            yield return new TagSpan<ErrorTag>(
                span.Value,
                new ErrorTag(errorType, $"{diagnostic.Id}: {diagnostic.Message}"));
        }
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        UpdateNativeDiagnostics(e.After);
        ScheduleSemanticDiagnostics(e.After, 150);
    }

    private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
    {
        var project = FecsxRoslynDocumentService.FindOwningProject(e.NewSolution, _filePath);
        if (project == null || (e.ProjectId !=  null && !Equals(e.ProjectId, project.Id)))
        {
            return;
        }

        ScheduleSemanticDiagnostics(_buffer.CurrentSnapshot, 250);
    }

    private void OnTextDocumentDisposed(object sender, Microsoft.VisualStudio.Text.TextDocumentEventArgs e)
    {
        if (ReferenceEquals(e.TextDocument, _document))
        {
            Dispose();
        }
    }

    private void UpdateNativeDiagnostics(ITextSnapshot snapshot)
    {
        var diagnostics = FecsxEditorDiagnostics.GetNativeDiagnostics(snapshot.GetText());
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _taggedSnapshot = snapshot;
            _nativeDiagnostics = diagnostics;
            _semanticDiagnostics = Array.Empty<FecsxEditorDiagnostic>();
        }

        RaiseTagsChanged(snapshot);
    }

    private void ScheduleSemanticDiagnostics(ITextSnapshot snapshot, int delayMilliseconds)
    {
        CancellationTokenSource previous;
        CancellationTokenSource next;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            previous = _semanticCancellation;
            next = new CancellationTokenSource();
            _semanticCancellation = next;
        }

        previous.Cancel();
        previous.Dispose();
        _ = UpdateSemanticDiagnosticsAsync(snapshot, delayMilliseconds, next.Token);
    }

    private async Task UpdateSemanticDiagnosticsAsync(
        ITextSnapshot snapshot,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            var projectedDocument = _roslynDocuments.Create(snapshot.GetText());
            if (projectedDocument == null)
            {
                return;
            }

            var semanticModel = await projectedDocument.Document
                .GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (semanticModel == null)
            {
                return;
            }

            var diagnostics = FecsxEditorDiagnostics.MapRoslynDiagnostics(
                projectedDocument.Projection,
                semanticModel.GetDiagnostics(cancellationToken: cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();

            lock (_gate)
            {
                if (_disposed || !ReferenceEquals(snapshot, _taggedSnapshot))
                {
                    return;
                }

                _semanticDiagnostics = diagnostics;
            }

            RaiseTagsChanged(snapshot);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Native FECSX diagnostics remain available while Roslyn reloads a solution.
        }
    }

    public void Dispose()
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cancellation = _semanticCancellation;
        }

        _buffer.Changed -= OnBufferChanged;
        _workspace.WorkspaceChanged -= OnWorkspaceChanged;
        _documents.TextDocumentDisposed -= OnTextDocumentDisposed;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void RaiseTagsChanged(ITextSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        TagsChanged?.Invoke(
            this,
            new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    private static SnapshotSpan? CreateSnapshotSpan(ITextSnapshot snapshot, int start, int length)
    {
        if (start < 0 || start > snapshot.Length)
        {
            return null;
        }

        var boundedLength = Math.Min(length, snapshot.Length - start);
        if (boundedLength == 0)
        {
            if (snapshot.Length == 0)
            {
                return null;
            }

            start = Math.Min(start, snapshot.Length - 1);
            boundedLength = 1;
        }

        return new SnapshotSpan(snapshot, start, boundedLength);
    }
}
