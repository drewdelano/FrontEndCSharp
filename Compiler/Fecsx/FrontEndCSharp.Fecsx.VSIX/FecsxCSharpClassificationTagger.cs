using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(ITaggerProvider))]
[ContentType(FecsxContentType.Name)]
[TagType(typeof(ClassificationTag))]
internal sealed class FecsxCSharpClassificationTaggerProvider : ITaggerProvider
{
    private readonly IClassificationTypeRegistryService _classificationTypes;
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxCSharpClassificationTaggerProvider(
        IClassificationTypeRegistryService classificationTypes,
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _classificationTypes = classificationTypes;
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
            () => new FecsxCSharpClassificationTagger(
                buffer,
                document,
                _documents,
                workspace,
                _classificationTypes)) as ITagger<T>;
    }

}

internal sealed class FecsxCSharpClassificationTagger : ITagger<ClassificationTag>, IDisposable
{
    private readonly ITextBuffer _buffer;
    private readonly ITextDocument _document;
    private readonly ITextDocumentFactoryService _documents;
    private readonly string _filePath;
    private readonly Workspace _workspace;
    private readonly FecsxRoslynDocumentService _roslynDocuments;
    private readonly AdhocWorkspace _fallbackWorkspace;
    private readonly Project _fallbackProject;
    private readonly IClassificationTypeRegistryService _classificationTypes;
    private readonly object _gate = new object();
    private CancellationTokenSource _fallbackCancellation = new CancellationTokenSource();
    private CancellationTokenSource _semanticCancellation = new CancellationTokenSource();
    private bool _disposed;
    private ITextSnapshot? _classifiedSnapshot;
    private ITextSnapshot? _semanticSnapshot;
    private IReadOnlyList<CachedClassification> _classifications = Array.Empty<CachedClassification>();

    public FecsxCSharpClassificationTagger(
        ITextBuffer buffer,
        ITextDocument document,
        ITextDocumentFactoryService documents,
        Workspace workspace,
        IClassificationTypeRegistryService classificationTypes)
    {
        _buffer = buffer;
        _document = document;
        _documents = documents;
        _filePath = Path.GetFullPath(document.FilePath);
        _workspace = workspace;
        _roslynDocuments = new FecsxRoslynDocumentService(workspace, _filePath);
        _fallbackWorkspace = new AdhocWorkspace();
        _fallbackProject = _fallbackWorkspace.AddProject("FECSX syntax fallback", LanguageNames.CSharp);
        _classificationTypes = classificationTypes;
        _buffer.Changed += OnBufferChanged;
        _workspace.WorkspaceChanged += OnWorkspaceChanged;
        _documents.TextDocumentDisposed += OnTextDocumentDisposed;
        ScheduleFallbackClassification(_buffer.CurrentSnapshot);
        ScheduleSemanticClassification(_buffer.CurrentSnapshot, delayMilliseconds: 0);
    }

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0)
        {
            yield break;
        }

        var snapshot = spans[0].Snapshot;
        IReadOnlyList<CachedClassification> classifications;
        lock (_gate)
        {
            if (!ReferenceEquals(snapshot, _classifiedSnapshot))
            {
                yield break;
            }

            classifications = _classifications;
        }

        foreach (var classification in classifications)
        {
            var span = new SnapshotSpan(snapshot, classification.Start, classification.Length);
            if (spans.IntersectsWith(span))
            {
                yield return new TagSpan<ClassificationTag>(span, new ClassificationTag(classification.Type));
            }
        }
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        ScheduleFallbackClassification(e.After);
        ScheduleSemanticClassification(e.After, delayMilliseconds: 150);
    }

    private void OnWorkspaceChanged(object sender, WorkspaceChangeEventArgs e)
    {
        var project = FecsxRoslynDocumentService.FindOwningProject(e.NewSolution, _filePath);
        if (project == null || (e.ProjectId !=  null && !Equals(e.ProjectId, project.Id)))
        {
            return;
        }

        var snapshot = _buffer.CurrentSnapshot;
        var delayMilliseconds = HasSemanticClassificationFor(snapshot) ? 250 : 0;
        ScheduleSemanticClassification(snapshot, delayMilliseconds);
    }

    private void OnTextDocumentDisposed(object sender, Microsoft.VisualStudio.Text.TextDocumentEventArgs e)
    {
        if (ReferenceEquals(e.TextDocument, _document))
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource fallbackCancellation;
        CancellationTokenSource semanticCancellation;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            fallbackCancellation = _fallbackCancellation;
            semanticCancellation = _semanticCancellation;
        }

        _buffer.Changed -= OnBufferChanged;
        _workspace.WorkspaceChanged -= OnWorkspaceChanged;
        _documents.TextDocumentDisposed -= OnTextDocumentDisposed;
        fallbackCancellation.Cancel();
        fallbackCancellation.Dispose();
        semanticCancellation.Cancel();
        semanticCancellation.Dispose();
        _fallbackWorkspace.Dispose();
    }

    private bool HasSemanticClassificationFor(ITextSnapshot snapshot)
    {
        lock (_gate)
        {
            return ReferenceEquals(snapshot, _semanticSnapshot);
        }
    }

    private void ScheduleFallbackClassification(ITextSnapshot snapshot) =>
        ScheduleClassification(snapshot, _fallbackProject, isSemantic: false, delayMilliseconds: 0);

    private void ScheduleSemanticClassification(ITextSnapshot snapshot, int delayMilliseconds) =>
        ScheduleClassification(snapshot, project: null, isSemantic: true, delayMilliseconds);

    private void ScheduleClassification(
        ITextSnapshot snapshot,
        Project? project,
        bool isSemantic,
        int delayMilliseconds)
    {
        CancellationTokenSource previous;
        CancellationTokenSource next;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            next = new CancellationTokenSource();
            if (isSemantic)
            {
                previous = _semanticCancellation;
                _semanticCancellation = next;
            }
            else
            {
                previous = _fallbackCancellation;
                _fallbackCancellation = next;
            }
        }

        previous.Cancel();
        previous.Dispose();
        _ = ClassifyAfterDelayAsync(snapshot, project, isSemantic, delayMilliseconds, next.Token);
    }

    private async Task ClassifyAfterDelayAsync(
        ITextSnapshot snapshot,
        Project? project,
        bool isSemantic,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        try
        {
            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            }

            project = isSemantic
                ? await FindOwningProjectAfterLoadAsync(cancellationToken).ConfigureAwait(false)
                : project;
            Trace($"Project lookup: semantic={isSemantic}, found={project !=  null}, file={_filePath}");
            if (isSemantic && project == null)
            {
                var candidates = _workspace.CurrentSolution.Projects
                    .Select(candidate =>
                        $"{candidate.Name}: project={candidate.FilePath}, additional={candidate.AdditionalDocuments.Count()}");
                Trace("Workspace projects: " + string.Join(" | ", candidates));
            }
            if (project !=  null)
            {
                await ClassifyAsync(snapshot, project, isSemantic, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Trace($"Classification failed: semantic={isSemantic}, {exception}");
            // Classification is additive. TextMate remains the lexical fallback if
            // Roslyn is unavailable while a solution is loading or being replaced.
        }
    }

    private async Task<Project?> FindOwningProjectAfterLoadAsync(CancellationToken cancellationToken)
    {
        const int attempts = 120;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var project = _roslynDocuments.FindOwningProject();
            if (project !=  null)
            {
                return project;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    private async Task ClassifyAsync(
        ITextSnapshot snapshot,
        Project project,
        bool isSemantic,
        CancellationToken cancellationToken)
    {
        var projectedDocument = _roslynDocuments.Create(snapshot.GetText(), project);
        var classifiedSpans = (await Classifier.GetClassifiedSpansAsync(
            projectedDocument.Document,
            new Microsoft.CodeAnalysis.Text.TextSpan(0, projectedDocument.Projection.Text.Length),
            cancellationToken).ConfigureAwait(false)).ToArray();
        var classifications = MapClassifications(projectedDocument.Projection, classifiedSpans);
        var diagnosticIds = isSemantic && IsTraceEnabled()
            ? await GetDiagnosticIdsAsync(
                projectedDocument.Document,
                cancellationToken).ConfigureAwait(false)
            : string.Empty;
        Trace(
            $"Classification complete: semantic={isSemantic}, Roslyn={classifiedSpans.Length}, " +
            $"mapped={classifications.Count}, project={project.Name}, " +
            $"diagnostics={diagnosticIds}, " +
            $"types={DescribeMappedTypes(snapshot, projectedDocument.Projection, classifiedSpans)}");
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!isSemantic && ReferenceEquals(snapshot, _semanticSnapshot))
            {
                return;
            }

            _classifiedSnapshot = snapshot;
            _classifications = classifications;
            if (isSemantic)
            {
                _semanticSnapshot = snapshot;
            }
        }

        RaiseTagsChanged(snapshot);
    }

    private static async Task<string> GetDiagnosticIdsAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document
            .GetSemanticModelAsync(cancellationToken)
            .ConfigureAwait(false);
        if (semanticModel == null)
        {
            return "unavailable";
        }

        return string.Join(",", semanticModel
            .GetDiagnostics(cancellationToken: cancellationToken)
            .Where(diagnostic =>
                diagnostic.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal));
    }

    private static string DescribeMappedTypes(
        ITextSnapshot snapshot,
        FecsxCSharpProjection projection,
        IEnumerable<ClassifiedSpan> classifiedSpans) =>
        string.Join(",", classifiedSpans
            .Where(span =>
                span.ClassificationType == ClassificationTypeNames.ClassName ||
                span.ClassificationType == ClassificationTypeNames.StructName ||
                span.ClassificationType == ClassificationTypeNames.InterfaceName)
            .Select(span => new FecsxTextSpan(span.TextSpan.Start, span.TextSpan.Length))
            .Select(span => projection.TryMapProjectedSpan(span, out var sourceSpan)
                ? snapshot.GetText(sourceSpan.Start, sourceSpan.Length)
                : null)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct());

    private IReadOnlyList<CachedClassification> MapClassifications(
        FecsxCSharpProjection projection,
        IEnumerable<ClassifiedSpan> classifiedSpans)
    {
        var result = new List<CachedClassification>();
        foreach (var classifiedSpan in classifiedSpans)
        {
            var projectedSpan = new FecsxTextSpan(classifiedSpan.TextSpan.Start, classifiedSpan.TextSpan.Length);
            if (!projection.TryMapProjectedSpan(projectedSpan, out var sourceSpan))
            {
                continue;
            }

            var classificationType = _classificationTypes.GetClassificationType(classifiedSpan.ClassificationType);
            if (classificationType !=  null)
            {
                result.Add(new CachedClassification(sourceSpan.Start, sourceSpan.Length, classificationType));
            }
        }

        return result;
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

    private static void Trace(string message)
    {
        var path = Environment.GetEnvironmentVariable("FRONTENDCSHARP_FECSX_CLASSIFICATION_LOG");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.AppendAllText(path, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Classification diagnostics must never affect the editor.
        }
    }

    private static bool IsTraceEnabled() =>
        !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("FRONTENDCSHARP_FECSX_CLASSIFICATION_LOG"));

    private sealed class CachedClassification
    {
        public CachedClassification(int start, int length, IClassificationType type)
        {
            Start = start;
            Length = length;
            Type = type;
        }

        public int Start { get; }
        public int Length { get; }
        public IClassificationType Type { get; }
    }
}
