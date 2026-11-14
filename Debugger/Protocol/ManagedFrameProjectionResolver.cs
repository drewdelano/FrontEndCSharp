using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal static class ManagedFrameProjectionResolver
{
    internal static ManagedFrameProjection? Resolve(
        ManagedRuntimeFrameSnapshot? frame,
        IManagedBreakpointResolver resolver,
        IEnumerable<string> sourcePaths)
    {
        if (frame == null)
        {
            return null;
        }
        try
        {
            var inspection = resolver.ResolveFrame(frame);
            if (string.IsNullOrWhiteSpace(inspection.SourcePath))
            {
                return null;
            }
            return Create(frame, inspection);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception is InvalidDataException
            || exception is ArgumentException
            || exception is IOException
            || exception is UnauthorizedAccessException)
        {
            foreach (var sourcePath in sourcePaths)
            {
                try
                {
                    var inspection = resolver.ResolveFrame(sourcePath, frame);
                    if (string.IsNullOrWhiteSpace(inspection.SourcePath))
                    {
                        continue;
                    }
                    return Create(frame, inspection);
                }
                catch (Exception fallbackException) when (
                    fallbackException is InvalidOperationException
                    || fallbackException is InvalidDataException
                    || fallbackException is ArgumentException
                    || fallbackException is IOException
                    || fallbackException is UnauthorizedAccessException)
                {
                    continue;
                }
            }
        }
        return null;
    }

    private static ManagedFrameProjection Create(
        ManagedRuntimeFrameSnapshot frame,
        ManagedFrameInspection inspection) => new(
        inspection.MethodName,
        new BreakpointBinding(
            new JObject
            {
                ["name"] = Path.GetFileName(inspection.SourcePath),
                ["path"] = inspection.SourcePath
            },
            ManagedBreakpointResolution.Resolved(
                frame.ModuleId,
                frame.MethodToken,
                frame.BlockIndex,
                frame.InstructionIndex,
                inspection.Line,
                inspection.Column)));
}
