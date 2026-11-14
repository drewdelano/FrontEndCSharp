using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using FrontEndCSharp.Fecsx.Editor;
using FrontEndCSharp.Fecsx.Syntax;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using VsCompletionItem = Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data.CompletionItem;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IAsyncCompletionCommitManagerProvider))]
[Name("FECSX C# completion commit manager")]
[ContentType(FecsxContentType.Name)]
internal sealed class FecsxCSharpCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
{
    public IAsyncCompletionCommitManager GetOrCreate(ITextView textView) =>
        textView.Properties.GetOrCreateSingletonProperty(() =>
            new FecsxCSharpCompletionCommitManager(textView));
}

internal sealed class FecsxCSharpCompletionCommitManager : IAsyncCompletionCommitManager
{
    private const string DiagnosticsEnvironmentVariable = "FRONTENDCSHARP_FECSX_COMPLETION_LOG";
    private readonly ITextView _textView;

    public FecsxCSharpCompletionCommitManager(ITextView textView) =>
        _textView = textView ?? throw new ArgumentNullException(nameof(textView));

    public IEnumerable<char> PotentialCommitCharacters => Array.Empty<char>();

    public bool ShouldCommitCompletion(
        IAsyncCompletionSession session,
        SnapshotPoint location,
        char typedChar,
        CancellationToken cancellationToken) => false;

    public CommitResult TryCommit(
        IAsyncCompletionSession session,
        ITextBuffer buffer,
        VsCompletionItem item,
        char typedChar,
        CancellationToken cancellationToken)
    {
        Trace("TryCommit: " + item.DisplayText + ", character: " + (int)typedChar);
        if (item.Properties.TryGetProperty(
                FecsxCSharpCompletionSource.TailwindItemKey,
                out FecsxCSharpCompletionSource.TailwindCompletionData tailwindData))
        {
            return CommitTailwind(buffer, tailwindData);
        }

        if (!item.Properties.TryGetProperty(
                FecsxCSharpCompletionSource.RoslynItemKey,
                out FecsxCSharpCompletionSource.RoslynCompletionData data))
        {
            Trace("Unhandled: the selected item is not a FECSX Roslyn item.");
            return CommitResult.Unhandled;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(buffer, data.SourceSnapshot.TextBuffer))
        {
            Trace("Handled without edits: the commit buffer differs from the source buffer.");
            return CommitResult.Handled;
        }

        var service = CompletionService.GetService(data.Document);
        if (service == null)
        {
            Trace("Handled without edits: Roslyn did not provide a completion service.");
            return CommitResult.Handled;
        }

        var change = ThreadHelper.JoinableTaskFactory.Run(() =>
            service.GetChangeAsync(
                data.Document,
                data.Item,
                cancellationToken: cancellationToken));
        var projectedEdits = GetProjectedEdits(change);
        if (!FecsxCSharpCompletionEdits.TryGetPrimaryEdit(
                projectedEdits,
                new FecsxTextSpan(data.Item.Span.Start, data.Item.Span.Length),
                out var primaryProjectedEdit) ||
            primaryProjectedEdit == null)
        {
            Trace("Handled without edits: Roslyn's edit set did not identify one primary completion edit.");
            return CommitResult.Handled;
        }

        var projectedCaretPosition = change.NewPosition ?? MapOriginalPositionToPostEdit(
            projectedEdits,
            primaryProjectedEdit.Span.End);
        if (!data.Projection.TryMapProjectedEdits(
                projectedEdits,
                projectedCaretPosition,
                out var sourceEdits,
                out var sourceCaretPosition))
        {
            Trace("Handled without edits: the projected edits or caret did not map to FECSX.");
            return CommitResult.Handled;
        }

        if (!data.Projection.TryMapProjectedEdits(
                new[] { primaryProjectedEdit },
                out var primarySourceEdits))
        {
            Trace("Handled without edits: Roslyn's primary edit did not map to FECSX.");
            return CommitResult.Handled;
        }

        if (primarySourceEdits.Count != 1)
        {
            Trace("Handled without edits: Roslyn's primary edit did not produce exactly one FECSX edit.");
            return CommitResult.Handled;
        }

        if (!TryTranslateEdits(data.SourceSnapshot, buffer.CurrentSnapshot, sourceEdits, out var currentEdits))
        {
            Trace("Handled without edits: the complete FECSX edit set did not translate to the current snapshot.");
            return CommitResult.Handled;
        }

        if (!TryTranslateEdits(
                data.SourceSnapshot,
                buffer.CurrentSnapshot,
                primarySourceEdits,
                out var currentPrimaryEdits))
        {
            Trace("Handled without edits: the primary FECSX edit did not translate to the current snapshot.");
            return CommitResult.Handled;
        }

        if (HasOverlappingEdits(currentEdits))
        {
            Trace("Handled without edits: the translated FECSX edits overlap.");
            return CommitResult.Handled;
        }

        int? currentCaretPosition = null;
        if (sourceCaretPosition is int originalCaretPosition)
        {
            var originalPrimaryEnd = MapOriginalPositionToPostEdit(
                sourceEdits,
                primarySourceEdits[0].Span.End);
            var currentPrimaryEnd = MapOriginalPositionToPostEdit(
                currentEdits,
                currentPrimaryEdits[0].Span.End);
            currentCaretPosition = currentPrimaryEnd + originalCaretPosition - originalPrimaryEnd;
        }

        using (var edit = buffer.CreateEdit())
        {
            foreach (var sourceEdit in currentEdits.OrderByDescending(sourceEdit => sourceEdit.Span.Start))
            {
                if (!edit.Replace(
                        new Span(sourceEdit.Span.Start, sourceEdit.Span.Length),
                        sourceEdit.NewText))
                {
                    edit.Cancel();
                    Trace("Handled without edits: the Visual Studio text edit rejected a replacement.");
                    return CommitResult.Handled;
                }
            }

            edit.Apply();
        }

        if (currentCaretPosition is int caretPosition)
        {
            var snapshot = buffer.CurrentSnapshot;
            var boundedPosition = Math.Max(0, Math.Min(caretPosition, snapshot.Length));
            _textView.Caret.MoveTo(new SnapshotPoint(snapshot, boundedPosition));
        }

        Trace("Handled: applied " + currentEdits.Count + " FECSX edit(s).");
        return CommitResult.Handled;
    }

    private CommitResult CommitTailwind(
        ITextBuffer buffer,
        FecsxCSharpCompletionSource.TailwindCompletionData data)
    {
        if (!ReferenceEquals(buffer, data.SourceSnapshot.TextBuffer))
        {
            Trace("Handled without edits: the Tailwind completion buffer differs from the source buffer.");
            return CommitResult.Handled;
        }

        var originalSpan = new SnapshotSpan(
            data.SourceSnapshot,
            data.ReplacementSpan.Start,
            data.ReplacementSpan.Length);
        var currentSpan = originalSpan.TranslateTo(buffer.CurrentSnapshot, SpanTrackingMode.EdgeInclusive);
        ITextSnapshot appliedSnapshot;
        using (var edit = buffer.CreateEdit())
        {
            if (!edit.Replace(currentSpan.Span, data.InsertText))
            {
                edit.Cancel();
                Trace("Handled without edits: the Tailwind completion replacement was rejected.");
                return CommitResult.Handled;
            }
            appliedSnapshot = edit.Apply();
        }

        var caretPosition = currentSpan.Start.Position + data.InsertText.Length - data.CaretOffsetFromEnd;
        _textView.Caret.MoveTo(new SnapshotPoint(appliedSnapshot, caretPosition));
        return CommitResult.Handled;
    }

    private static void Trace(string message)
    {
        var path = Environment.GetEnvironmentVariable(DiagnosticsEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
        }
        catch
        {
            // Diagnostics must never affect completion behavior.
        }
    }

    private static IReadOnlyList<FecsxTextEdit> GetProjectedEdits(CompletionChange change)
    {
        var changes = change.TextChanges.IsDefaultOrEmpty
            ? ImmutableArray.Create(change.TextChange)
            : change.TextChanges;
        return changes
            .Select(textChange => new FecsxTextEdit(
                new FecsxTextSpan(textChange.Span.Start, textChange.Span.Length),
                textChange.NewText ?? string.Empty))
            .ToArray();
    }

    private static int MapOriginalPositionToPostEdit(
        IReadOnlyList<FecsxTextEdit> edits,
        int originalPosition)
    {
        var mapped = originalPosition;
        foreach (var edit in edits.OrderBy(edit => edit.Span.Start))
        {
            if (originalPosition < edit.Span.Start)
            {
                break;
            }

            if (originalPosition <= edit.Span.End)
            {
                return mapped - Math.Min(originalPosition - edit.Span.Start, edit.Span.Length) + edit.NewText.Length;
            }

            mapped += edit.NewText.Length - edit.Span.Length;
        }

        return mapped;
    }

    private static bool TryTranslateEdits(
        ITextSnapshot sourceSnapshot,
        ITextSnapshot currentSnapshot,
        IReadOnlyList<FecsxTextEdit> sourceEdits,
        out IReadOnlyList<FecsxTextEdit> currentEdits)
    {
        if (!ReferenceEquals(sourceSnapshot.TextBuffer, currentSnapshot.TextBuffer))
        {
            currentEdits = Array.Empty<FecsxTextEdit>();
            return false;
        }

        var translated = new FecsxTextEdit[sourceEdits.Count];
        for (var index = 0; index < sourceEdits.Count; index++)
        {
            var sourceEdit = sourceEdits[index];
            var trackingMode = sourceEdit.Span.Length == 0
                ? SpanTrackingMode.EdgeExclusive
                : SpanTrackingMode.EdgeInclusive;
            var currentSpan = new SnapshotSpan(
                    sourceSnapshot,
                    sourceEdit.Span.Start,
                    sourceEdit.Span.Length)
                .TranslateTo(currentSnapshot, trackingMode);
            translated[index] = new FecsxTextEdit(
                new FecsxTextSpan(currentSpan.Start.Position, currentSpan.Length),
                sourceEdit.NewText);
        }

        currentEdits = translated;
        return true;
    }

    private static bool HasOverlappingEdits(IReadOnlyList<FecsxTextEdit> edits)
    {
        var ordered = edits.OrderBy(edit => edit.Span.Start).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Span.Start < ordered[index - 1].Span.End)
            {
                return true;
            }
        }

        return false;
    }
}
