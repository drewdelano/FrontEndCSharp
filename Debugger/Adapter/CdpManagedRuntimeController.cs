using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class CdpManagedRuntimeController
{
    private readonly CdpConnection cdp;
    private readonly string generatedOutputPath;

    internal CdpManagedRuntimeController(
        CdpConnection cdp,
        string generatedOutputPath)
    {
        this.cdp = cdp;
        this.generatedOutputPath = generatedOutputPath;
    }

    internal async Task<ManagedValueSnapshot> SetVariableAsync(
        string sessionId,
        string callFrameId,
        ManagedAssignmentTarget target,
        string value,
        CancellationToken cancellationToken)
    {
        var targetJson = JsonSerializer.Serialize(target, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var valueJson = JsonSerializer.Serialize(value);
        var evaluation = await cdp.SendAsync("Debugger.evaluateOnCallFrame", new
        {
            callFrameId,
            expression = CdpManagedExpressionBuilder.BuildAssignment(targetJson, valueJson),
            returnByValue = true
        }, sessionId, cancellationToken);
        if (evaluation.TryGetProperty("exceptionDetails", out var exception))
        {
            var message = exception.TryGetProperty("exception", out var exceptionValue)
                && exceptionValue.TryGetProperty("description", out var description)
                    ? description.GetString()
                    : exception.GetRawText();
            throw new InvalidOperationException(message ?? "The managed assignment failed.");
        }
        return evaluation.GetProperty("result").GetProperty("value")
            .Deserialize<ManagedValueSnapshot>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("CDP returned an empty managed assignment result.");
    }

    internal async Task<RuntimeUpdateAcknowledgement> ApplyUpdateAsync(
        string sessionId,
        string relativeModulePath,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        var normalized = relativeModulePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0
            || normalized.Split('/').Any(segment =>
                segment == "" || segment == "." || segment == ".."))
        {
            throw new ArgumentException(
                "The update module path must remain inside generated output.",
                nameof(relativeModulePath));
        }
        var updatePath = Path.GetFullPath(Path.Combine(
            generatedOutputPath,
            normalized.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(generatedOutputPath, updatePath);
        if (relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relative)
            || !File.Exists(updatePath))
        {
            throw new FileNotFoundException(
                "The update module is outside generated output or does not exist.",
                updatePath);
        }
        var updateExpression = await File.ReadAllTextAsync(updatePath, cancellationToken);
        var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(updateExpression)))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The update code hash '{actualHash}' does not match manifest hash '{expectedHash}'.");
        }
        var evaluatedExpression =
            $"((__frontEndCSharpUpdateHash) => ({updateExpression}))({JsonSerializer.Serialize(expectedHash)})";
        var evaluation = await cdp.SendAsync("Runtime.evaluate", new
        {
            expression = evaluatedExpression,
            returnByValue = true
        }, sessionId, cancellationToken);
        if (evaluation.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException(
                $"The runtime rejected the update: {exception.GetRawText()}");
        }
        var acknowledgement = evaluation.GetProperty("result").GetProperty("value")
            .Deserialize<RuntimeUpdateAcknowledgement>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        return acknowledgement
            ?? throw new InvalidDataException("The runtime returned an empty update acknowledgement.");
    }

    internal async Task SetExceptionBreakpointsAsync(
        string sessionId,
        IReadOnlyList<string> filters,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(filters);
        var evaluation = await cdp.SendAsync("Runtime.evaluate", new
        {
            expression = $$"""
                (() => {
                    if (globalThis.__FrontEndCSharpDebugger) {
                        globalThis.__FrontEndCSharpDebugger.setExceptionBreakpoints({{serialized}});
                        return;
                    }
                    globalThis.__FrontEndCSharpDebuggerInitialConfiguration.exceptionFilters = {{serialized}};
                })()
                """
        }, sessionId, cancellationToken);
        if (evaluation.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException(
                $"The runtime rejected the exception breakpoint configuration: {exception.GetRawText()}");
        }
    }

    internal async Task SetBreakpointsAsync(
        string sessionId,
        IReadOnlyList<ManagedBreakpoint> locations,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(locations, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var evaluation = await cdp.SendAsync("Runtime.evaluate", new
        {
            expression = $$"""
                (() => {
                    if (globalThis.__FrontEndCSharpDebugger) {
                        globalThis.__FrontEndCSharpDebugger.setBreakpoints({{serialized}});
                        return;
                    }
                    globalThis.__FrontEndCSharpDebuggerInitialConfiguration.breakpoints = {{serialized}};
                })()
                """,
            returnByValue = true
        }, sessionId, cancellationToken);
        if (evaluation.TryGetProperty("exceptionDetails", out var exception))
        {
            throw new InvalidOperationException(
                $"The runtime rejected the rebound breakpoints: {exception.GetRawText()}");
        }
    }
}
