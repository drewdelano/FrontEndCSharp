using System.Text.Json;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpManagedPauseReader
{
    private readonly CdpConnection cdp;
    private readonly string expectedBuildId;

    internal CdpManagedPauseReader(CdpConnection cdp, string expectedBuildId)
    {
        this.cdp = cdp;
        this.expectedBuildId = expectedBuildId;
    }

    internal async Task<CdpManagedPauseCapture> CaptureAsync(
        JsonElement parameters,
        string sessionId)
    {
        var managedOwnershipConfirmed = false;
        try
        {
            if (!await IsManagedStopAsync(sessionId))
            {
                return CdpManagedPauseCapture.NotManaged();
            }
            managedOwnershipConfirmed = true;
            var exceptionSnapshot = await ReadExceptionSnapshotAsync(sessionId);
            if (exceptionSnapshot != null)
            {
                foreach (var frame in exceptionSnapshot.Frames)
                {
                    frame.StopReason = "exception";
                    frame.ExceptionStage = exceptionSnapshot.ExceptionStage;
                    frame.ExceptionType = exceptionSnapshot.ExceptionType;
                }
                return CdpManagedPauseCapture.Captured(exceptionSnapshot.Frames);
            }

            var frames = new List<CdpManagedFrame>();
            foreach (var callFrame in parameters.GetProperty("callFrames").EnumerateArray()
                .Where(frame => frame.GetProperty("functionName").GetString() == "ExecuteMSIL"))
            {
                var evaluation = await cdp.SendAsync("Debugger.evaluateOnCallFrame", new
                {
                    callFrameId = callFrame.GetProperty("callFrameId").GetString(),
                    expression = CdpManagedExpressionBuilder.ManagedFrame,
                    returnByValue = true
                }, sessionId, cancellationToken: default);
                if (evaluation.TryGetProperty("exceptionDetails", out var exception))
                {
                    throw new InvalidOperationException(
                        $"Unable to inspect managed frame: {exception.GetRawText()}");
                }
                var value = evaluation.GetProperty("result").GetProperty("value");
                var frame = value.Deserialize<CdpManagedFrame>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidDataException("CDP returned an empty managed frame.");
                frame.CallFrameId = callFrame.GetProperty("callFrameId").GetString();
                frames.Add(frame);
            }
            if (frames.Count == 0)
            {
                return CdpManagedPauseCapture.MissingFrames();
            }

            var asyncParents = frames[0].AsyncParents;
            frames.ForEach(frame => frame.AsyncParents = []);
            foreach (var parent in asyncParents)
            {
                parent.IsLogical = true;
                frames.Add(parent);
            }
            return CdpManagedPauseCapture.Captured(frames);
        }
        catch (Exception exception)
        {
            return CdpManagedPauseCapture.Failed(
                managedOwnershipConfirmed,
                exception);
        }
    }

    private async Task<bool> IsManagedStopAsync(string sessionId)
    {
        var buildId = JsonSerializer.Serialize(expectedBuildId);
        var evaluation = await cdp.SendAsync("Runtime.evaluate", new
        {
            expression = $"(() => {{ const debuggerState = globalThis.__FrontEndCSharpDebugger; return debuggerState?.updateState?.baselineBuildId === {buildId} && debuggerState.currentStop !== null; }})()",
            returnByValue = true,
            throwOnSideEffect = true
        }, sessionId);
        if (evaluation.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException(
                $"Unable to classify a Chromium pause: {exception.GetRawText()}");
        }
        var result = evaluation.GetProperty("result");
        return result.TryGetProperty("value", out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private async Task<CdpExceptionSnapshot?> ReadExceptionSnapshotAsync(string sessionId)
    {
        var evaluation = await cdp.SendAsync("Runtime.evaluate", new
        {
            expression = "(() => { const stop = globalThis.__FrontEndCSharpDebugger.currentStop; return stop?.reason === 'exception' ? ({ exceptionStage: stop.exceptionStage, exceptionType: stop.exceptionType, frames: stop.frames }) : null; })()",
            returnByValue = true,
            throwOnSideEffect = true
        }, sessionId);
        var result = evaluation.GetProperty("result");
        if (!result.TryGetProperty("value", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return value.Deserialize<CdpExceptionSnapshot>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}

internal sealed class CdpManagedPauseCapture
{
    private CdpManagedPauseCapture(
        bool ownershipConfirmed,
        IReadOnlyList<CdpManagedFrame> frames,
        bool framesMissing,
        Exception? error)
    {
        OwnershipConfirmed = ownershipConfirmed;
        Frames = frames;
        FramesMissing = framesMissing;
        Error = error;
    }

    internal bool OwnershipConfirmed { get; }
    internal IReadOnlyList<CdpManagedFrame> Frames { get; }
    internal bool FramesMissing { get; }
    internal Exception? Error { get; }

    internal static CdpManagedPauseCapture NotManaged() =>
        new(false, Array.Empty<CdpManagedFrame>(), false, null);

    internal static CdpManagedPauseCapture Captured(IReadOnlyList<CdpManagedFrame> frames) =>
        new(true, frames, false, null);

    internal static CdpManagedPauseCapture MissingFrames() =>
        new(true, Array.Empty<CdpManagedFrame>(), true, null);

    internal static CdpManagedPauseCapture Failed(bool ownershipConfirmed, Exception error) =>
        new(ownershipConfirmed, Array.Empty<CdpManagedFrame>(), false, error);
}
