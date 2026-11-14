using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

[Export(typeof(IWpfTextViewCreationListener))]
[ContentType(FecsxContentType.Name)]
[TextViewRole(PredefinedTextViewRoles.Editable)]
internal sealed class FecsxTextMateScopeProbeProvider : IWpfTextViewCreationListener
{
    internal const string LogEnvironmentVariable = "FRONTENDCSHARP_FECSX_TEXTMATE_LOG";
    private const string ScopeTagTypeName =
        "Microsoft.VisualStudio.TextMate.Core.Editor.Classification.ITMScopeTag";
    private readonly IBufferTagAggregatorFactoryService _aggregators;

    [ImportingConstructor]
    public FecsxTextMateScopeProbeProvider(IBufferTagAggregatorFactoryService aggregators)
    {
        _aggregators = aggregators;
    }

    public void TextViewCreated(IWpfTextView textView)
    {
        var logPath = Environment.GetEnvironmentVariable(LogEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return;
        }

        textView.Properties.GetOrCreateSingletonProperty(
            () => CreateCapture(textView, Path.GetFullPath(logPath)));
    }

    private IDisposable CreateCapture(IWpfTextView textView, string logPath)
    {
        var scopeTagType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(ScopeTagTypeName, throwOnError: false))
            .FirstOrDefault(type => type != null)
            ?? Type.GetType(
                ScopeTagTypeName + ", Microsoft.VisualStudio.TextMate.Core",
                throwOnError: true)!;
        var method = GetType().GetMethod(
            nameof(CreateCaptureCore),
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IDisposable)method.MakeGenericMethod(scopeTagType).Invoke(
            this,
            new object[] { textView, logPath })!;
    }

    private IDisposable CreateCaptureCore<T>(IWpfTextView textView, string logPath)
        where T : ITag =>
        new FecsxTextMateScopeCapture<T>(
            textView,
            _aggregators.CreateTagAggregator<T>(textView.TextBuffer),
            _aggregators.CreateTagAggregator<ClassificationTag>(textView.TextBuffer),
            logPath);
}

internal sealed class FecsxTextMateScopeCapture<T> : IDisposable where T : ITag
{
    private readonly IWpfTextView _view;
    private readonly ITagAggregator<T> _aggregator;
    private readonly ITagAggregator<ClassificationTag> _classifications;
    private readonly string _logPath;
    private readonly PropertyInfo _scopeNameProperty;
    private readonly DispatcherTimer _timer;
    private string? _lastFingerprint;
    private int _stablePasses;
    private bool _disposed;

    public FecsxTextMateScopeCapture(
        IWpfTextView view,
        ITagAggregator<T> aggregator,
        ITagAggregator<ClassificationTag> classifications,
        string logPath)
    {
        _view = view;
        _aggregator = aggregator;
        _classifications = classifications;
        _logPath = logPath;
        _scopeNameProperty = typeof(T).GetProperty("ScopeName")
            ?? throw new InvalidOperationException(
                $"TextMate scope tag '{typeof(T).FullName}' exposes no ScopeName property.");
        _view.Closed += OnClosed;
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            OnTick,
            _view.VisualElement.Dispatcher);
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _view.Closed -= OnClosed;
        _aggregator.Dispose();
        _classifications.Dispose();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        var snapshot = _view.TextSnapshot;
        var records = _aggregator
            .GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length))
            .SelectMany(tag => tag.Span.GetSpans(snapshot).Select(span => (
                Start: span.Start.Position,
                Length: span.Length,
                ScopeName: (string?)_scopeNameProperty.GetValue(tag.Tag) ?? string.Empty)))
            .Where(record => record.Length > 0 && record.ScopeName.Length > 0)
            .Distinct()
            .OrderBy(record => record.Start)
            .ThenBy(record => record.Length)
            .ThenBy(record => record.ScopeName, StringComparer.Ordinal)
            .ToArray();
        var classifications = _classifications
            .GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length))
            .SelectMany(tag => tag.Span.GetSpans(snapshot).Select(span => (
                Start: span.Start.Position,
                Length: span.Length,
                Classification: tag.Tag.ClassificationType.Classification)))
            .Where(record => record.Length > 0 && record.Classification.Length > 0)
            .Distinct()
            .OrderBy(record => record.Start)
            .ThenBy(record => record.Length)
            .ThenBy(record => record.Classification, StringComparer.Ordinal)
            .ToArray();
        if (records.Length == 0 || classifications.Length == 0)
        {
            return;
        }

        var fingerprint = string.Join(
            "\n",
            records.Select(record =>
                $"S:{record.Start}:{record.Length}:{record.ScopeName}")
                .Concat(classifications.Select(record =>
                    $"C:{record.Start}:{record.Length}:{record.Classification}")));
        if (!string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal))
        {
            _lastFingerprint = fingerprint;
            _stablePasses = 0;
            return;
        }
        if (++_stablePasses < 4)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        var lines = records.Select(record => string.Join(
                "\t",
                "TextMateScope",
                record.Start.ToString(CultureInfo.InvariantCulture),
                record.Length.ToString(CultureInfo.InvariantCulture),
                record.ScopeName))
            .Concat(classifications.Select(record => string.Join(
                "\t",
                "Classification",
                record.Start.ToString(CultureInfo.InvariantCulture),
                record.Length.ToString(CultureInfo.InvariantCulture),
                record.Classification)))
            .Append(string.Join(
                "\t",
                "TextMateScopeCaptureComplete",
                snapshot.Length.ToString(CultureInfo.InvariantCulture),
                records.Length.ToString(CultureInfo.InvariantCulture),
                classifications.Length.ToString(CultureInfo.InvariantCulture)));
        File.WriteAllLines(_logPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _timer.Stop();
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

}
