using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FrontEndCSharp.Fecsx;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using RoslynCompletionItem = Microsoft.CodeAnalysis.Completion.CompletionItem;
using VsCompletionContext = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionContext;
using VsCompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IAsyncCompletionSourceProvider))]
[Name("FECSX C# completion")]
[ContentType(FecsxContentType.Name)]
internal sealed class FecsxCSharpCompletionSourceProvider : IAsyncCompletionSourceProvider
{
    private readonly ITextDocumentFactoryService _documents;
    private readonly IServiceProvider _serviceProvider;

    [ImportingConstructor]
    public FecsxCSharpCompletionSourceProvider(
        ITextDocumentFactoryService documents,
        [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
    {
        _documents = documents;
        _serviceProvider = serviceProvider;
    }

    public IAsyncCompletionSource GetOrCreate(ITextView textView) =>
        textView.Properties.GetOrCreateSingletonProperty(() => Create(textView.TextBuffer));

    private IAsyncCompletionSource Create(ITextBuffer buffer)
    {
        if (!_documents.TryGetTextDocument(buffer, out var textDocument))
        {
            return EmptyAsyncCompletionSource.Instance;
        }

        var workspace = FecsxRoslynDocumentService.GetVisualStudioWorkspace(_serviceProvider);
        return workspace == null
            ? EmptyAsyncCompletionSource.Instance
            : new FecsxCSharpCompletionSource(buffer, new FecsxRoslynDocumentService(workspace, textDocument.FilePath));
    }
}

internal sealed class FecsxCSharpCompletionSource : IAsyncCompletionSource
{
    internal const string RoslynItemKey = "FECSX Roslyn completion item";
    internal const string TailwindItemKey = "FECSX Tailwind completion item";
    private static readonly ImageElement TailwindImage = new(
        new ImageId(new Guid("dd37ad22-31f8-4dde-b7bd-5d1424d89a93"), 2),
        "Tailwind CSS");
    private readonly FecsxRoslynDocumentService _documents;
    private readonly FecsxEditorService _editor = new FecsxEditorService();

    public FecsxCSharpCompletionSource(ITextBuffer buffer, FecsxRoslynDocumentService documents)
    {
        _documents = documents;
    }

    public CompletionStartData InitializeCompletion(
        Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger,
        SnapshotPoint triggerLocation,
        CancellationToken cancellationToken)
    {
        var snapshot = triggerLocation.Snapshot;
        var tailwindSpan = _documents.IsTailwindAvailable()
            ? _editor.GetTailwindClassCompletionSpan(snapshot.GetText(), triggerLocation.Position)
            : null;
        if (tailwindSpan != null)
        {
            return new CompletionStartData(
                CompletionParticipation.ExclusivelyProvidesItems,
                new SnapshotSpan(snapshot, tailwindSpan.Value.Start, tailwindSpan.Value.Length));
        }

        var projection = FecsxCSharpProjector.Create(snapshot.GetText());
        if (!projection.TryMapSourceCaretPosition(triggerLocation.Position, out _))
        {
            var tagSpan = _editor.GetOpeningTagNameCompletionSpan(snapshot.GetText(), triggerLocation.Position);
            if (tagSpan !=  null)
            {
                return new CompletionStartData(
                    CompletionParticipation.ExclusivelyProvidesItems,
                    new SnapshotSpan(snapshot, tagSpan.Value.Start, tagSpan.Value.Length));
            }

            var attributeSpan = _editor.GetAttributeNameCompletionSpan(snapshot.GetText(), triggerLocation.Position);
            return attributeSpan == null
                ? CompletionStartData.DoesNotParticipateInCompletion
                : new CompletionStartData(
                    CompletionParticipation.ExclusivelyProvidesItems,
                    new SnapshotSpan(snapshot, attributeSpan.Value.Start, attributeSpan.Value.Length));
        }

        var start = triggerLocation.Position;
        while (start > 0 && IsIdentifierCharacter(snapshot[start - 1]))
        {
            start--;
        }

        var end = triggerLocation.Position;
        while (end < snapshot.Length && IsIdentifierCharacter(snapshot[end]))
        {
            end++;
        }

        return new CompletionStartData(
            CompletionParticipation.ExclusivelyProvidesItems,
            new SnapshotSpan(snapshot, start, end - start));
    }

    public async Task<VsCompletionContext> GetCompletionContextAsync(
        IAsyncCompletionSession session,
        Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger,
        SnapshotPoint triggerLocation,
        SnapshotSpan applicableToSpan,
        CancellationToken cancellationToken)
    {
        var snapshot = triggerLocation.Snapshot;
        var tailwindSpan = _documents.IsTailwindAvailable()
            ? _editor.GetTailwindClassCompletionSpan(snapshot.GetText(), triggerLocation.Position)
            : null;
        if (tailwindSpan != null)
        {
            return GetTailwindCompletionContext(snapshot, triggerLocation.Position);
        }

        var projectedDocument = _documents.Create(snapshot.GetText());
        if (projectedDocument == null)
        {
            return VsCompletionContext.Empty;
        }

        var tagSpan = _editor.GetOpeningTagNameCompletionSpan(snapshot.GetText(), triggerLocation.Position);
        if (tagSpan !=  null)
        {
            return await GetTagCompletionContextAsync(
                projectedDocument.ComponentProject,
                snapshot,
                tagSpan.Value,
                cancellationToken).ConfigureAwait(false);
        }

        var attributeSpan = _editor.GetAttributeNameCompletionSpan(snapshot.GetText(), triggerLocation.Position);
        if (attributeSpan !=  null)
        {
            return await GetAttributeCompletionContextAsync(
                projectedDocument.ComponentProject,
                snapshot,
                attributeSpan.Value,
                cancellationToken).ConfigureAwait(false);
        }

        if (!projectedDocument.Projection.TryMapSourceCaretPosition(triggerLocation.Position, out var projectedPosition))
        {
            return VsCompletionContext.Empty;
        }

        var service = CompletionService.GetService(projectedDocument.Document);
        if (service == null)
        {
            return VsCompletionContext.Empty;
        }

        var list = await service.GetCompletionsAsync(
            projectedDocument.Document,
            projectedPosition,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (list == null)
        {
            return VsCompletionContext.Empty;
        }

        var items = ImmutableArray.CreateBuilder<VsCompletionItem>(list.ItemsList.Count);
        foreach (var roslynItem in list.ItemsList)
        {
            var replacement = applicableToSpan;
            if (projectedDocument.Projection.TryMapProjectedSpan(
                    new FecsxTextSpan(roslynItem.Span.Start, roslynItem.Span.Length),
                    out var sourceSpan) &&
                sourceSpan.End <= snapshot.Length)
            {
                replacement = new SnapshotSpan(snapshot, sourceSpan.Start, sourceSpan.Length);
            }

            var item = new VsCompletionItem(roslynItem.DisplayText, this, replacement);
            item.Properties.AddProperty(
                RoslynItemKey,
                new RoslynCompletionData(
                    projectedDocument.Document,
                    roslynItem,
                    snapshot,
                    projectedDocument.Projection));
            items.Add(item);
        }

        return new VsCompletionContext(items.MoveToImmutable());
    }

    private VsCompletionContext GetTailwindCompletionContext(ITextSnapshot snapshot, int position)
    {
        var completions = _editor.GetTailwindClassCompletions(
            snapshot.GetText(),
            position,
            _documents.GetTailwindEditorCatalog());
        var items = ImmutableArray.CreateBuilder<VsCompletionItem>(completions.Count);
        foreach (var completion in completions)
        {
            var replacement = new SnapshotSpan(
                snapshot,
                completion.ReplacementSpan.Start,
                completion.ReplacementSpan.Length);
            var item = new VsCompletionItem(
                completion.DisplayText,
                this,
                TailwindImage,
                ImmutableArray<CompletionFilter>.Empty,
                "Tailwind CSS",
                completion.InsertText,
                completion.SortText,
                completion.DisplayText,
                completion.DisplayText,
                ImmutableArray<ImageElement>.Empty,
                ImmutableArray<char>.Empty,
                replacement,
                false,
                false);
            item.Properties.AddProperty(
                TailwindItemKey,
                new TailwindCompletionData(
                    snapshot,
                    completion));
            items.Add(item);
        }
        return new VsCompletionContext(items.MoveToImmutable());
    }

    private async Task<VsCompletionContext> GetTagCompletionContextAsync(
        Microsoft.CodeAnalysis.Project componentProject,
        ITextSnapshot snapshot,
        FecsxTextSpan sourceSpan,
        CancellationToken cancellationToken)
    {
        var replacement = new SnapshotSpan(snapshot, sourceSpan.Start, sourceSpan.Length);
        var items = ImmutableArray.CreateBuilder<VsCompletionItem>();
        foreach (var intrinsic in IntrinsicElementSchema.Elements)
        {
            items.Add(new VsCompletionItem(intrinsic.TagName, this, replacement));
        }

        var compilation = await componentProject.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation !=  null)
        {
            var catalog = FecsxComponentCatalog.Create(compilation);
            foreach (var componentName in catalog.ComponentNames)
            {
                items.Add(new VsCompletionItem(componentName, this, replacement));
            }
        }

        return new VsCompletionContext(items.ToImmutable());
    }

    private async Task<VsCompletionContext> GetAttributeCompletionContextAsync(
        Microsoft.CodeAnalysis.Project componentProject,
        ITextSnapshot snapshot,
        FecsxTextSpan sourceSpan,
        CancellationToken cancellationToken)
    {
        var compilation = await componentProject.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        var metadata = compilation == null ? null : FecsxComponentCatalog.Create(compilation);
        var completions = _editor.GetAttributeCompletions(
            snapshot.GetText(),
            sourceSpan.End,
            metadata,
            FecsxProjectConfiguration.GetAttributeCasing(componentProject));
        var items = ImmutableArray.CreateBuilder<VsCompletionItem>(completions.Count);
        foreach (var completion in completions)
        {
            var replacement = new SnapshotSpan(
                snapshot,
                completion.ReplacementSpan.Start,
                completion.ReplacementSpan.Length);
            items.Add(new VsCompletionItem(
                completion.DisplayText,
                this,
                ImageElement.Empty,
                ImmutableArray<CompletionFilter>.Empty,
                string.Empty,
                completion.InsertText,
                completion.DisplayText,
                completion.DisplayText,
                completion.DisplayText,
                ImmutableArray<ImageElement>.Empty,
                ImmutableArray<char>.Empty,
                replacement,
                false,
                false));
        }

        return new VsCompletionContext(items.ToImmutable());
    }

    public async Task<object> GetDescriptionAsync(
        IAsyncCompletionSession session,
        VsCompletionItem item,
        CancellationToken cancellationToken)
    {
        if (item.Properties.TryGetProperty(TailwindItemKey, out TailwindCompletionData tailwind))
        {
            return tailwind.Description;
        }

        if (!item.Properties.TryGetProperty(RoslynItemKey, out RoslynCompletionData data))
        {
            return string.Empty;
        }

        var service = CompletionService.GetService(data.Document);
        if (service == null)
        {
            return string.Empty;
        }

        var description = await service.GetDescriptionAsync(
            data.Document,
            data.Item,
            cancellationToken).ConfigureAwait(false);
        return description?.Text ?? string.Empty;
    }

    private static bool IsIdentifierCharacter(char value) =>
        value == '_' || value == '@' || char.IsLetterOrDigit(value);

    internal sealed class RoslynCompletionData
    {
        public RoslynCompletionData(
            Microsoft.CodeAnalysis.Document document,
            RoslynCompletionItem item,
            ITextSnapshot sourceSnapshot,
            FecsxCSharpProjection projection)
        {
            Document = document;
            Item = item;
            SourceSnapshot = sourceSnapshot;
            Projection = projection;
        }

        public Microsoft.CodeAnalysis.Document Document { get; }
        public RoslynCompletionItem Item { get; }
        public ITextSnapshot SourceSnapshot { get; }
        public FecsxCSharpProjection Projection { get; }
    }

    internal sealed class TailwindCompletionData
    {
        public TailwindCompletionData(
            ITextSnapshot sourceSnapshot,
            FecsxTailwindCompletionItem completion)
        {
            SourceSnapshot = sourceSnapshot;
            Completion = completion;
        }

        public ITextSnapshot SourceSnapshot { get; }
        public FecsxTailwindCompletionItem Completion { get; }
        public FecsxTextSpan ReplacementSpan => Completion.ReplacementSpan;
        public string InsertText => Completion.InsertText;
        public string Description => Completion.Description;
        public int CaretOffsetFromEnd => Completion.CaretOffsetFromEnd;
    }

}

internal sealed class EmptyAsyncCompletionSource : IAsyncCompletionSource
{
    public static readonly EmptyAsyncCompletionSource Instance = new EmptyAsyncCompletionSource();

    private EmptyAsyncCompletionSource()
    {
    }

    public CompletionStartData InitializeCompletion(
        Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger,
        SnapshotPoint triggerLocation,
        CancellationToken cancellationToken) => CompletionStartData.DoesNotParticipateInCompletion;

    public Task<VsCompletionContext> GetCompletionContextAsync(
        IAsyncCompletionSession session,
        Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionTrigger trigger,
        SnapshotPoint triggerLocation,
        SnapshotSpan applicableToSpan,
        CancellationToken cancellationToken) => Task.FromResult(VsCompletionContext.Empty);

    public Task<object> GetDescriptionAsync(
        IAsyncCompletionSession session,
        VsCompletionItem item,
        CancellationToken cancellationToken) => Task.FromResult<object>(string.Empty);
}
