using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol;
using Microsoft.VisualStudio.Shared.VSCodeDebugProtocol.Messages;

namespace FrontEndCSharp.DebugAdapter;

internal sealed class ManagedPauseRequestCoordinator
{
    private readonly ManagedPauseState state;
    private readonly object gate;
    private readonly Func<CdpDebugSession?> getSession;
    private readonly CancellationTokenSource lifetime;

    internal ManagedPauseRequestCoordinator(
        ManagedPauseState state,
        object gate,
        Func<CdpDebugSession?> getSession,
        CancellationTokenSource lifetime)
    {
        this.state = state;
        this.gate = gate;
        this.getSession = getSession;
        this.lifetime = lifetime;
    }

    internal StackTraceResponse ReadStackTrace()
    {
        lock (gate)
        {
            var frames = state.Frames.Select(frame =>
            {
                var source = frame.Location.SourcePath == null
                    ? null
                    : new Source
                    {
                        Name = Path.GetFileName(frame.Location.SourcePath),
                        Path = frame.Location.SourcePath
                    };
                return new StackFrame(
                    frame.Id,
                    frame.Location.MethodName,
                    frame.Location.Line,
                    frame.Location.Column)
                {
                    Source = source
                };
            }).ToList();
            return new StackTraceResponse(frames) { TotalFrames = frames.Count };
        }
    }

    internal ScopesResponse ReadScopes(ScopesArguments arguments)
    {
        lock (gate)
        {
            var frame = state.Frames.SingleOrDefault(item => item.Id == arguments.FrameId);
            if (frame == null)
            {
                throw new ProtocolException("The requested stack frame is no longer available.");
            }
            return new ScopesResponse([
                new Scope("Locals", frame.LocalsReference, expensive: false)
            ]);
        }
    }

    internal VariablesResponse ReadVariables(VariablesArguments arguments)
    {
        lock (gate)
        {
            if (!state.Variables.TryGetValue(arguments.VariablesReference, out var values))
            {
                throw new ProtocolException("The requested variables are no longer available.");
            }
            return new VariablesResponse(values.Select(value =>
                new Variable(
                    value.Name,
                    value.Value,
                    variablesReference: value.VariablesReference)
                {
                    Type = value.Type
                }).ToList());
        }
    }

    internal SetVariableResponse SetVariable(SetVariableArguments arguments)
    {
        RuntimeFrame frame;
        ProjectedManagedVariable variable;
        lock (gate)
        {
            if (!state.Variables.TryGetValue(arguments.VariablesReference, out var values)
                || !state.VariableFrames.TryGetValue(arguments.VariablesReference, out frame!))
            {
                throw new ProtocolException(
                    "The requested variables are stale or no longer available.");
            }
            variable = values.FirstOrDefault(item => string.Equals(
                item.Name,
                arguments.Name,
                StringComparison.Ordinal))
                ?? throw new ProtocolException(
                    $"The managed variable '{arguments.Name}' is not available in this container.");
            if (frame.IsLogical)
            {
                throw new ProtocolException("Async logical-frame snapshots are immutable.");
            }
            if (frame.CallFrameId == null || variable.Target == null)
            {
                throw new ProtocolException(
                    "This managed value does not have a writable live storage location.");
            }
        }

        ManagedValueSnapshot updated;
        try
        {
            updated = (getSession()
                    ?? throw new ProtocolException("The debug session is not running."))
                .SetVariableAsync(
                    frame.CallFrameId,
                    variable.Target,
                    arguments.Value,
                    lifetime.Token)
                .GetAwaiter().GetResult();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or InvalidDataException
            or OperationCanceledException)
        {
            throw new ProtocolException(
                exception.Message.Replace("Error: FrontEndCSharp: ", string.Empty));
        }

        lock (gate)
        {
            if (!state.Variables.TryGetValue(arguments.VariablesReference, out var current)
                || !ReferenceEquals(
                    frame,
                    state.VariableFrames.GetValueOrDefault(arguments.VariablesReference)))
            {
                throw new ProtocolException(
                    "The managed stack frame became stale while applying the assignment.");
            }
            state.Variables[arguments.VariablesReference] = current.Select(item =>
                ReferenceEquals(item, variable)
                    ? item with { Value = updated.Value, Type = updated.Type }
                    : item).ToList();
        }
        return new SetVariableResponse(updated.Value)
        {
            Type = updated.Type,
            VariablesReference = variable.VariablesReference
        };
    }

    internal EvaluateResponse Evaluate(EvaluateArguments arguments)
    {
        if (arguments.Context != EvaluateArguments.ContextValue.Hover)
        {
            throw new ProtocolException(
                "C# expression evaluation is not supported by the FrontEndCSharp debugger yet.");
        }
        if (arguments.FrameId is not int frameId)
        {
            throw new ProtocolException("Hover evaluation requires a managed stack frame.");
        }
        lock (gate)
        {
            var frame = state.Frames.SingleOrDefault(item => item.Id == frameId);
            if (frame == null
                || !state.Variables.TryGetValue(
                    frame.LocalsReference,
                    out var frameVariables))
            {
                throw new ProtocolException("The requested stack frame is no longer available.");
            }
            var value = frameVariables.FirstOrDefault(item => string.Equals(
                item.Name,
                arguments.Expression,
                StringComparison.Ordinal));
            if (value == null)
            {
                throw new ProtocolException(
                    $"The managed variable '{arguments.Expression}' is not available in this frame.");
            }
            return new EvaluateResponse(value.Value, value.VariablesReference)
            {
                Type = value.Type
            };
        }
    }

    internal ExceptionInfoResponse ReadExceptionInfo(ExceptionInfoArguments arguments)
    {
        lock (gate)
        {
            if (arguments.ThreadId != 1
                || state.ExceptionStage == null
                || state.ExceptionType == null)
            {
                throw new ProtocolException("No managed exception is currently suspended.");
            }
            var description =
                $"{state.ExceptionType} ({state.ExceptionStage} managed exception)";
            return new ExceptionInfoResponse(
                $"FrontEndCSharp/{state.ExceptionType}",
                state.ExceptionStage == "unhandled"
                    ? ExceptionBreakMode.Unhandled
                    : ExceptionBreakMode.Always)
            {
                Description = description,
                Details = new ExceptionDetails
                {
                    Message = description,
                    TypeName = state.ExceptionType,
                    FullTypeName = state.ExceptionType
                }
            };
        }
    }
}
