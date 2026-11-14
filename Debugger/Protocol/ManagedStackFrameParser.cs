using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal static class ManagedStackFrameParser
{
    internal const int MaxLogicalFramesPerStack = 1024;

    internal static string BuildParentIdentityExpression(int index, bool isSnapshot) =>
        string.Format(
            CultureInfo.InvariantCulture,
            isSnapshot
                ? "(()=>{{const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:globalThis.__FrontEndCSharpDebugger?.currentStop?.frame?.ctx;const frame=ctx?.asyncParents?.[{0}];return JSON.stringify([frame?.moduleId??'',frame?.methodToken??0,frame?.blockIndex??0,frame?.instructionIndex??0]);}})()"
                : "(()=>{{const ctx=typeof opts==='object'&&opts?.ctx?opts.ctx:globalThis.__FrontEndCSharpDebugger?.currentStop?.frame?.ctx;const frame=ctx?.debugFrames?.[{0}];return JSON.stringify([frame?.methodInfo?.debugModuleId??'',frame?.methodInfo?.debugMethodToken??0,frame?.currentBlockIndex??0,frame?.instructionIndex??0]);}})()",
            index);

    internal static string DescribeIdentityResult(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return "empty";
        }

        var text = result!.Trim();
        var shape = text[0] == '['
            ? "array"
            : text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"'
                ? "double-quoted"
                : text.Length > 1 && text[0] == '\'' && text[text.Length - 1] == '\''
                    ? "single-quoted"
                    : "other";
        return shape + "-length-" + text.Length.ToString(CultureInfo.InvariantCulture);
    }

    internal static ManagedRuntimeFrameSnapshot ParseFrameIdentity(
        string? result,
        out int asyncParentCount)
    {
        asyncParentCount = 0;
        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidDataException("The JavaScript debugger returned no nested frame identity.");
        }

        var text = result!.Trim();
        if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"')
        {
            text = JsonConvert.DeserializeObject<string>(text) ?? string.Empty;
        }
        else if (text.Length > 1 && text[0] == '\'' && text[text.Length - 1] == '\'')
        {
            text = text.Substring(1, text.Length - 2);
        }

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end < start)
        {
            throw new InvalidDataException("The nested frame identity is not an array.");
        }

        var identity = JArray.Parse(text.Substring(start, end - start + 1));
        if (identity.Count < 4)
        {
            throw new InvalidDataException("The nested frame identity is incomplete.");
        }

        asyncParentCount = identity.Count > 4
            ? Math.Max(0, identity[4]?.Value<int>() ?? 0)
            : 0;
        return new ManagedRuntimeFrameSnapshot
        {
            ModuleId = identity[0]?.Value<string>() ?? string.Empty,
            MethodToken = identity[1]?.Value<int>() ?? 0,
            BlockIndex = identity[2]?.Value<int>() ?? 0,
            InstructionIndex = identity[3]?.Value<int>() ?? 0
        };
    }

    internal static ManagedStackCounts ParseStackCounts(string? result)
    {
        var text = ParseEvaluateDisplayString(result);
        JArray counts;
        try
        {
            counts = JArray.Parse(text ?? string.Empty);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The JavaScript debugger returned invalid managed-stack counts.",
                exception);
        }

        var physicalCount = counts.Count > 0 ? counts[0]?.Value<int>() ?? -1 : -1;
        var asyncCount = counts.Count > 1 ? counts[1]?.Value<int>() ?? -1 : -1;
        if (physicalCount <= 0
            || asyncCount < 0
            || physicalCount + asyncCount > MaxLogicalFramesPerStack)
        {
            throw new InvalidDataException(
                "The JavaScript debugger returned out-of-range managed-stack counts.");
        }

        return new ManagedStackCounts(physicalCount, asyncCount);
    }

    internal static ManagedStopProbe? ParseStopProbe(string? result)
    {
        var text = ParseEvaluateDisplayString(result);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            var values = JArray.Parse(text!);
            if (values.Count < 7 || string.IsNullOrWhiteSpace(values[0]?.Value<string>()))
            {
                return null;
            }

            var moduleId = values[3]?.Value<string>();
            var frame = string.IsNullOrWhiteSpace(moduleId)
                ? null
                : new ManagedRuntimeFrameSnapshot
                {
                    ModuleId = moduleId!,
                    MethodToken = values[4]?.Value<int>() ?? 0,
                    BlockIndex = values[5]?.Value<int>() ?? 0,
                    InstructionIndex = values[6]?.Value<int>() ?? 0
                };
            return new ManagedStopProbe(
                values[0]!.Value<string>()!,
                values[1]?.Type == JTokenType.Null ? null : values[1]?.Value<string>(),
                values[2]?.Type == JTokenType.Null ? null : values[2]?.Value<string>(),
                frame);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ParseEvaluateDisplayString(string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        var text = result!.Trim();
        if (text.Length > 1 && text[0] == '"' && text[text.Length - 1] == '"')
        {
            try
            {
                return JsonConvert.DeserializeObject<string>(text);
            }
            catch (JsonException)
            {
                return text.Substring(1, text.Length - 2);
            }
        }
        if (text.Length > 1 && text[0] == '\'' && text[text.Length - 1] == '\'')
        {
            return text.Substring(1, text.Length - 2);
        }
        return string.Equals(text, "null", StringComparison.Ordinal) ? null : text;
    }
}
