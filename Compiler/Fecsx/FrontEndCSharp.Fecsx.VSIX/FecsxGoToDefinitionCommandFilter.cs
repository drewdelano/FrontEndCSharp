using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IVsTextViewCreationListener))]
[ContentType(FecsxContentType.Name)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class FecsxGoToDefinitionCommandFilterProvider : IVsTextViewCreationListener
{
    private readonly IVsEditorAdaptersFactoryService _editorAdapters;
    private readonly ITextDocumentFactoryService _documents;
    private readonly System.IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxGoToDefinitionCommandFilterProvider(
        IVsEditorAdaptersFactoryService editorAdapters,
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] System.IServiceProvider serviceProvider)
    {
        _editorAdapters = editorAdapters;
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public void VsTextViewCreated(IVsTextView textViewAdapter)
    {
        var textView = _editorAdapters.GetWpfTextView(textViewAdapter);
        if (textView == null || !_documents.TryGetTextDocument(textView.TextBuffer, out var document))
        {
            return;
        }

        var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
        if (workspace == null)
        {
            return;
        }

        var filter = new FecsxGoToDefinitionCommandFilter(
            textView,
            document,
            workspace,
            _serviceProvider);
        ErrorHandler.ThrowOnFailure(textViewAdapter.AddCommandFilter(filter, out var next));
        filter.SetNext(next);
        textView.Properties.GetOrCreateSingletonProperty(() => filter);
    }
}

internal sealed class FecsxGoToDefinitionCommandFilter : IOleCommandTarget, IDisposable
{
    private readonly IWpfTextView _textView;
    private readonly Workspace _workspace;
    private readonly string _filePath;
    private readonly FecsxRoslynDocumentService _roslynDocuments;
    private readonly System.IServiceProvider _serviceProvider;
    private IOleCommandTarget? _next;
    private CancellationTokenSource _navigationCancellation = new CancellationTokenSource();
    private bool _disposed;

    public FecsxGoToDefinitionCommandFilter(
        IWpfTextView textView,
        ITextDocument document,
        Workspace workspace,
        System.IServiceProvider serviceProvider)
    {
        _textView = textView;
        _workspace = workspace;
        _filePath = Path.GetFullPath(document.FilePath);
        _roslynDocuments = new FecsxRoslynDocumentService(workspace, document.FilePath);
        _serviceProvider = serviceProvider;
        _textView.Closed += OnClosed;
    }

    public void SetNext(IOleCommandTarget next) => _next = next;

    public int QueryStatus(ref Guid commandGroup, uint commandCount, OLECMD[] commands, IntPtr commandText)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if ((IsGoToDefinition(commandGroup, commandCount, commands) && CanNavigateCaret()) ||
            (IsRename(commandGroup, commandCount, commands) && CanProjectCaret()))
        {
            commands[0].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
            return VSConstants.S_OK;
        }

        return _next?.QueryStatus(ref commandGroup, commandCount, commands, commandText)
            ?? (int)Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    public int Exec(
        ref Guid commandGroup,
        uint commandId,
        uint commandOptions,
        IntPtr input,
        IntPtr output)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (commandGroup == VSConstants.GUID_VSStandardCommandSet97 &&
            commandId == (uint)VSConstants.VSStd97CmdID.GotoDefn &&
            TryCreateNavigationRequest(includeManagedTag: true, out var request))
        {
            var previous = _navigationCancellation;
            var next = new CancellationTokenSource();
            _navigationCancellation = next;
            previous.Cancel();
            previous.Dispose();
            NavigateAsync(request, next.Token).Forget();
            return VSConstants.S_OK;
        }

        if (commandGroup == VSConstants.VSStd2K &&
            (commandId == (uint)VSConstants.VSStd2KCmdID.RENAME ||
             commandId == (uint)VSConstants.VSStd2KCmdID.ECMD_RENAMESYMBOL) &&
            TryCreateNavigationRequest(includeManagedTag: false, out var renameRequest))
        {
            var previous = _navigationCancellation;
            var next = new CancellationTokenSource();
            _navigationCancellation = next;
            previous.Cancel();
            previous.Dispose();
            RenameAsync(renameRequest, next.Token).Forget();
            return VSConstants.S_OK;
        }

        return _next?.Exec(ref commandGroup, commandId, commandOptions, input, output)
            ?? (int)Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    private bool CanProjectCaret()
    {
        var snapshot = _textView.TextSnapshot;
        var sourcePosition = _textView.Caret.Position.BufferPosition.Position;
        return FecsxCSharpProjector.Create(snapshot.GetText())
            .TryMapSourceCaretPosition(sourcePosition, out _);
    }

    private bool CanNavigateCaret()
    {
        if (CanProjectCaret())
        {
            return true;
        }

        var snapshot = _textView.TextSnapshot;
        return FecsxComponentNavigation.TryGetManagedTag(
            snapshot.GetText(),
            _textView.Caret.Position.BufferPosition.Position,
            out _);
    }

    private bool TryCreateNavigationRequest(
        bool includeManagedTag,
        out NavigationRequest request)
    {
        var snapshot = _textView.TextSnapshot;
        var source = snapshot.GetText();
        var projection = FecsxCSharpProjector.Create(source);
        var sourcePosition = _textView.Caret.Position.BufferPosition.Position;
        if (!projection.TryMapSourceCaretPosition(sourcePosition, out _))
        {
            if (includeManagedTag && FecsxComponentNavigation.TryGetManagedTag(
                    source,
                    sourcePosition,
                    out var componentTagName))
            {
                request = new NavigationRequest(source, sourcePosition, componentTagName);
                return true;
            }

            request = default!;
            return false;
        }

        request = new NavigationRequest(source, sourcePosition, componentTagName: null);
        return true;
    }

    private async Task NavigateAsync(NavigationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.ComponentTagName != null)
            {
                var componentTarget = await ResolveComponentNavigationTargetAsync(
                    request,
                    cancellationToken).ConfigureAwait(false);
                if (componentTarget != null)
                {
                    await NavigateToTargetAsync(
                        componentTarget.FilePath,
                        componentTarget.LinePosition,
                        cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            var projected = await CreateProjectedSolutionAsync(request, cancellationToken).ConfigureAwait(false);
            if (projected == null)
            {
                return;
            }

            var activeDocument = projected.Solution.GetDocument(projected.ActiveEntry.ProjectionDocumentId)!;
            var semanticModel = await activeDocument
                .GetSemanticModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (semanticModel == null)
            {
                return;
            }

            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
                semanticModel,
                projected.ProjectedPosition,
                projected.Solution.Workspace,
                cancellationToken).ConfigureAwait(false);
            if (symbol == null)
            {
                return;
            }

            symbol = await SymbolFinder.FindSourceDefinitionAsync(
                symbol,
                projected.Solution,
                cancellationToken).ConfigureAwait(false) ?? symbol;
            if (!TryResolveNavigationTarget(symbol, projected.Entries, out var filePath, out var linePosition))
            {
                return;
            }

            await NavigateToTargetAsync(filePath, linePosition, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (COMException)
        {
            // Let Visual Studio remain responsive if a document closes during navigation.
        }
    }

    private async Task<NavigationTarget?> ResolveComponentNavigationTargetAsync(
        NavigationRequest request,
        CancellationToken cancellationToken)
    {
        var project = _roslynDocuments.FindOwningProject();
        if (project == null || request.ComponentTagName == null)
        {
            return null;
        }

        var activeDocument = project.AdditionalDocuments.FirstOrDefault(document =>
            document.FilePath != null
            && string.Equals(
                Path.GetFullPath(document.FilePath),
                _filePath,
                StringComparison.OrdinalIgnoreCase));
        if (activeDocument != null)
        {
            project = project.Solution.WithAdditionalDocumentText(
                    activeDocument.Id,
                    SourceText.From(request.Source))
                .GetProject(project.Id)!;
        }

        var probeSource = FecsxComponentNavigation.CreateTypeResolutionProbe(
            request.Source,
            request.ComponentTagName,
            out var tagSpan);
        var probeDocument = project.AddDocument(
            "__FecsxComponentNavigationProbe.cs",
            SourceText.From(probeSource));
        var semanticModel = await probeDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var root = await probeDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel == null || root == null)
        {
            return null;
        }

        var tagNode = root.FindNode(new Microsoft.CodeAnalysis.Text.TextSpan(
            tagSpan.Start,
            tagSpan.Length));
        var symbolInfo = semanticModel.GetSymbolInfo(tagNode, cancellationToken);
        var componentType = symbolInfo.Symbol as INamedTypeSymbol
            ?? symbolInfo.CandidateSymbols.OfType<INamedTypeSymbol>().SingleOrDefault();
        if (componentType == null)
        {
            return null;
        }

        foreach (var document in project.AdditionalDocuments.Where(document =>
            document.FilePath?.EndsWith(".fecsx", StringComparison.OrdinalIgnoreCase) == true))
        {
            var filePath = Path.GetFullPath(document.FilePath!);
            var source = string.Equals(filePath, _filePath, StringComparison.OrdinalIgnoreCase)
                ? request.Source
                : (await document.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
            if (!FecsxComponentNavigation.TryGetDeclaration(
                    componentType,
                    filePath,
                    source,
                    out var declarationSpan))
            {
                continue;
            }

            var linePosition = SourceText.From(source).Lines.GetLinePosition(declarationSpan.Start);
            return new NavigationTarget(filePath, linePosition);
        }

        return null;
    }

    private async Task NavigateToTargetAsync(
        string filePath,
        LinePosition linePosition,
        CancellationToken cancellationToken)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (TryNavigateWithinActiveDocument(filePath, linePosition))
        {
            return;
        }

        VsShellUtilities.OpenDocument(
            _serviceProvider,
            filePath,
            Guid.Empty,
            out _,
            out _,
            out _,
            out var targetView);
        ErrorHandler.ThrowOnFailure(targetView.SetCaretPos(linePosition.Line, linePosition.Character));
        ErrorHandler.ThrowOnFailure(targetView.CenterLines(linePosition.Line, 1));
    }

    private bool TryNavigateWithinActiveDocument(string filePath, LinePosition linePosition)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (!string.Equals(
                Path.GetFullPath(filePath),
                _filePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var snapshot = _textView.TextSnapshot;
        if (linePosition.Line < 0 || linePosition.Line >= snapshot.LineCount)
        {
            return false;
        }

        var line = snapshot.GetLineFromLineNumber(linePosition.Line);
        var position = Math.Min(line.Start.Position + linePosition.Character, line.End.Position);
        _textView.Caret.MoveTo(new SnapshotPoint(snapshot, position));
        _textView.Caret.EnsureVisible();
        return true;
    }

    private async Task RenameAsync(NavigationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var projected = await CreateProjectedSolutionAsync(request, cancellationToken).ConfigureAwait(false);
            if (projected == null)
            {
                return;
            }

            var projectedSolution = projected.Solution;
            var entries = projected.Entries;
            var activeDocument = projectedSolution.GetDocument(projected.ActiveEntry.ProjectionDocumentId)!;
            var semanticModel = await activeDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel == null)
            {
                return;
            }

            var symbol = await SymbolFinder.FindSymbolAtPositionAsync(
                semanticModel,
                projected.ProjectedPosition,
                projectedSolution.Workspace,
                cancellationToken).ConfigureAwait(false);
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Name))
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var dialog = new FecsxRenameDialog(symbol.Name);
            if (dialog.ShowDialog() != true ||
                string.Equals(dialog.NewName, symbol.Name, StringComparison.Ordinal) ||
                !SyntaxFacts.IsValidIdentifier(dialog.NewName))
            {
                return;
            }

            var renamedSolution = await Renamer.RenameSymbolAsync(
                projectedSolution,
                symbol,
                new SymbolRenameOptions(),
                dialog.NewName,
                cancellationToken).ConfigureAwait(false);
            var result = _workspace.CurrentSolution;

            foreach (var changedProject in renamedSolution.GetChanges(projectedSolution).GetProjectChanges())
            {
                foreach (var documentId in changedProject.GetChangedDocuments())
                {
                    if (entries.Any(entry => entry.ProjectionDocumentId == documentId) ||
                        result.GetDocument(documentId) == null)
                    {
                        continue;
                    }

                    var changedText = await renamedSolution.GetDocument(documentId)!
                        .GetTextAsync(cancellationToken)
                        .ConfigureAwait(false);
                    result = result.WithDocumentText(documentId, changedText);
                }
            }

            foreach (var entry in entries)
            {
                var originalDocument = projectedSolution.GetDocument(entry.ProjectionDocumentId)!;
                var changedDocument = renamedSolution.GetDocument(entry.ProjectionDocumentId)!;
                var changes = await changedDocument
                    .GetTextChangesAsync(originalDocument, cancellationToken)
                    .ConfigureAwait(false);
                if (!FecsxCSharpRenameEdits.TryMapChanges(entry.Projection, changes, out var sourceEdits))
                {
                    return;
                }

                if (sourceEdits.Count > 0)
                {
                    var changedSource = FecsxCSharpRenameEdits.Apply(entry.Source, sourceEdits);
                    result = result.WithAdditionalDocumentText(
                        entry.AdditionalDocumentId,
                        SourceText.From(changedSource));
                }
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _workspace.TryApplyChanges(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Rename is transactional; no authored documents are changed until every
            // projected edit has mapped successfully and the workspace accepts it.
        }
    }

    private async Task<ProjectedFecsxSolution?> CreateProjectedSolutionAsync(
        NavigationRequest request,
        CancellationToken cancellationToken)
    {
        var project = _roslynDocuments.FindOwningProject();
        if (project == null)
        {
            return null;
        }

        project = FecsxCSharpProject.WithoutSourceGenerator(project);

        var entries = new List<ProjectionEntry>();
        var projectedSolution = project.Solution;
        var index = 0;
        foreach (var additionalDocument in project.AdditionalDocuments.Where(document =>
            document.FilePath?.EndsWith(".fecsx", StringComparison.OrdinalIgnoreCase) == true))
        {
            var filePath = Path.GetFullPath(additionalDocument.FilePath!);
            var source = string.Equals(filePath, _filePath, StringComparison.OrdinalIgnoreCase)
                ? request.Source
                : (await additionalDocument.GetTextAsync(cancellationToken).ConfigureAwait(false)).ToString();
            var projection = FecsxCSharpProjector.Create(
                source,
                $"__FecsxEditorProjection{index++}");
            var projectionId = DocumentId.CreateNewId(project.Id);
            projectedSolution = projectedSolution.AddDocument(
                projectionId,
                $"__FecsxEditorProjection_{index}.cs",
                SourceText.From(projection.Text),
                filePath: filePath + ".editor.g.cs");
            entries.Add(new ProjectionEntry(
                additionalDocument.Id,
                projectionId,
                filePath,
                source,
                projection));
        }

        var activeEntry = entries.FirstOrDefault(entry =>
            string.Equals(entry.FilePath, _filePath, StringComparison.OrdinalIgnoreCase));
        if (activeEntry == null ||
            !activeEntry.Projection.TryMapSourceCaretPosition(
                request.SourcePosition,
                out var projectedPosition))
        {
            return null;
        }

        return new ProjectedFecsxSolution(
            projectedSolution,
            entries,
            activeEntry,
            projectedPosition);
    }

    private static bool TryResolveNavigationTarget(
        ISymbol symbol,
        IReadOnlyList<ProjectionEntry> entries,
        out string filePath,
        out LinePosition linePosition)
    {
        foreach (var location in symbol.Locations.Where(candidate => candidate.IsInSource))
        {
            var sourcePath = location.SourceTree?.FilePath;
            var entry = sourcePath == null
                ? null
                : entries.FirstOrDefault(candidate => string.Equals(
                    candidate.FilePath + ".editor.g.cs",
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase));
            if (entry == null ||
                !entry.Projection.TryMapProjectedSpan(
                    new FecsxTextSpan(location.SourceSpan.Start, location.SourceSpan.Length),
                    out var sourceSpan))
            {
                continue;
            }

            filePath = entry.FilePath;
            linePosition = SourceText.From(entry.Source).Lines.GetLinePosition(sourceSpan.Start);
            return true;
        }

        foreach (var location in symbol.Locations.Where(candidate => candidate.IsInSource))
        {
            var sourcePath = location.SourceTree?.FilePath;
            if (sourcePath == null || sourcePath.EndsWith(".editor.g.cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filePath = sourcePath;
            linePosition = location.GetLineSpan().StartLinePosition;
            return true;
        }

        filePath = string.Empty;
        linePosition = default;
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _textView.Closed -= OnClosed;
        _navigationCancellation.Cancel();
        _navigationCancellation.Dispose();
    }

    private void OnClosed(object sender, EventArgs e) => Dispose();

    private static bool IsGoToDefinition(Guid commandGroup, uint commandCount, OLECMD[] commands) =>
        commandCount == 1 &&
        commands.Length > 0 &&
        commandGroup == VSConstants.GUID_VSStandardCommandSet97 &&
        commands[0].cmdID == (uint)VSConstants.VSStd97CmdID.GotoDefn;

    private static bool IsRename(Guid commandGroup, uint commandCount, OLECMD[] commands) =>
        commandCount == 1 &&
        commands.Length > 0 &&
        commandGroup == VSConstants.VSStd2K &&
        (commands[0].cmdID == (uint)VSConstants.VSStd2KCmdID.RENAME ||
         commands[0].cmdID == (uint)VSConstants.VSStd2KCmdID.ECMD_RENAMESYMBOL);

    private sealed class NavigationRequest
    {
        public NavigationRequest(string source, int sourcePosition, string? componentTagName)
        {
            Source = source;
            SourcePosition = sourcePosition;
            ComponentTagName = componentTagName;
        }

        public string Source { get; }
        public int SourcePosition { get; }
        public string? ComponentTagName { get; }
    }

    private sealed class NavigationTarget
    {
        public NavigationTarget(string filePath, LinePosition linePosition)
        {
            FilePath = filePath;
            LinePosition = linePosition;
        }

        public string FilePath { get; }
        public LinePosition LinePosition { get; }
    }

    private sealed class ProjectionEntry
    {
        public ProjectionEntry(
            DocumentId additionalDocumentId,
            DocumentId projectionDocumentId,
            string filePath,
            string source,
            FecsxCSharpProjection projection)
        {
            AdditionalDocumentId = additionalDocumentId;
            ProjectionDocumentId = projectionDocumentId;
            FilePath = filePath;
            Source = source;
            Projection = projection;
        }

        public DocumentId AdditionalDocumentId { get; }
        public DocumentId ProjectionDocumentId { get; }
        public string FilePath { get; }
        public string Source { get; }
        public FecsxCSharpProjection Projection { get; }
    }

    private sealed class ProjectedFecsxSolution
    {
        public ProjectedFecsxSolution(
            Solution solution,
            IReadOnlyList<ProjectionEntry> entries,
            ProjectionEntry activeEntry,
            int projectedPosition)
        {
            Solution = solution;
            Entries = entries;
            ActiveEntry = activeEntry;
            ProjectedPosition = projectedPosition;
        }

        public Solution Solution { get; }
        public IReadOnlyList<ProjectionEntry> Entries { get; }
        public ProjectionEntry ActiveEntry { get; }
        public int ProjectedPosition { get; }
    }
}
