using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FrontEndCSharp.Debugging;

internal sealed class ManagedVariableResponseCoordinator
{
    private readonly ManagedVariableState state;
    private readonly Action<string>? diagnosticSink;

    internal ManagedVariableResponseCoordinator(
        ManagedVariableState state,
        Action<string>? diagnosticSink)
    {
        this.state = state;
        this.diagnosticSink = diagnosticSink;
    }

    internal bool TryTransformAdapterResponse(JObject message, out string transformed)
    {
        transformed = message.ToString(Formatting.None);
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int setVariableSequence
            && state.PendingSetErrors.TryGetValue(setVariableSequence, out var setVariableError))
        {
            state.PendingSetErrors.Remove(setVariableSequence);
            message["command"] = "setVariable";
            message["success"] = false;
            message["message"] = setVariableError;
            message.Remove("body");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int inspectionSequence
            && state.PendingInspections.TryGetValue(inspectionSequence, out var immutableInspection))
        {
            state.PendingInspections.Remove(inspectionSequence);
            transformed = TransformInspectionResponse(message, immutableInspection);
            return true;
        }
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "evaluate", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int hoverSequence
            && state.PendingHovers.TryGetValue(hoverSequence, out var immutableHover))
        {
            state.PendingHovers.Remove(hoverSequence);
            if (message.Value<bool?>("success") == true
                && message["body"] is JObject hoverBody
                && string.Equals(
                    hoverBody.Value<string>("type"),
                    "undefined",
                    StringComparison.Ordinal))
            {
                message["success"] = false;
                message["message"] = "The managed expression is not available in the current frame.";
                message.Remove("body");
                diagnosticSink?.Invoke("hover-response-unavailable");
                transformed = message.ToString(Formatting.None);
                return true;
            }
            var variablesReference = message["body"]?.Value<int?>("variablesReference") ?? 0;
            if (variablesReference > 0)
            {
                state.LiveReferences.Add(variablesReference);
                state.StaleReferences.Remove(variablesReference);
                if (immutableHover)
                {
                    state.ImmutableReferences.Add(variablesReference);
                }
                var type = message["body"]?.Value<string>("type");
                if (!string.IsNullOrWhiteSpace(type))
                {
                    var managedType = FormatManagedTypeName(type!);
                    message["body"]!["type"] = managedType;
                    message["body"]!["result"] = "{" + managedType + "}";
                }
            }
            diagnosticSink?.Invoke("hover-response-projected");
            transformed = message.ToString(Formatting.None);
            return true;
        }
        if (string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            && string.Equals(message.Value<string>("command"), "variables", StringComparison.Ordinal)
            && message.Value<int?>("request_seq") is int variablesSequence
            && state.PendingVariableRequests.TryGetValue(variablesSequence, out var parentReference))
        {
            state.PendingVariableRequests.Remove(variablesSequence);
            transformed = TransformManagedVariablesResponse(message, parentReference);
            return true;
        }
        return false;
    }

    private static DapTransformDispatch ClientOnly(string payload) =>
        new DapTransformDispatch(new[] { payload }, Array.Empty<string>());

    internal bool TryBeginManagedScalarMaterialization(
        JObject message,
        Func<int> allocateSequence,
        out DapTransformDispatch dispatch)
    {
        dispatch = ClientOnly(message.ToString(Formatting.None));
        if (!string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            || !string.Equals(message.Value<string>("command"), "variables", StringComparison.Ordinal)
            || message.Value<int?>("request_seq") is not int requestSequence
            || !state.PendingVariableRequests.TryGetValue(requestSequence, out var parentReference))
        {
            return false;
        }

        state.PendingVariableRequests.Remove(requestSequence);
        TransformManagedVariablesResponse(message, parentReference);
        var scalarVariables = (message["body"]?["variables"] as JArray)?
            .Children<JObject>()
            .Where(variable =>
                (variable.Value<int?>("variablesReference") ?? 0) > 0
                && IsManagedScalarType(variable.Value<string>("type")))
            .ToList() ?? new List<JObject>();
        if (scalarVariables.Count == 0)
        {
            dispatch = ClientOnly(message.ToString(Formatting.None));
            return true;
        }

        var materialization = new PendingManagedScalarMaterialization(
            message,
            scalarVariables.Count);
        var adapterPayloads = new List<string>(scalarVariables.Count);
        foreach (var variable in scalarVariables)
        {
            var sequence = allocateSequence();
            state.PendingScalarValues[sequence] = new PendingManagedScalarValue(
                materialization,
                variable);
            adapterPayloads.Add(new JObject
            {
                ["seq"] = sequence,
                ["type"] = "request",
                ["command"] = "variables",
                ["arguments"] = new JObject
                {
                    ["variablesReference"] = variable.Value<int>("variablesReference")
                }
            }.ToString(Formatting.None));
        }
        diagnosticSink?.Invoke("managed-scalar-materialization-started");
        dispatch = new DapTransformDispatch(Array.Empty<string>(), adapterPayloads);
        return true;
    }

    internal bool TryCompleteManagedScalarMaterialization(
        JObject message,
        out DapTransformDispatch dispatch)
    {
        dispatch = new DapTransformDispatch(Array.Empty<string>(), Array.Empty<string>());
        if (!string.Equals(message.Value<string>("type"), "response", StringComparison.Ordinal)
            || !string.Equals(message.Value<string>("command"), "variables", StringComparison.Ordinal)
            || message.Value<int?>("request_seq") is not int requestSequence
            || !state.PendingScalarValues.TryGetValue(requestSequence, out var pendingScalar))
        {
            return false;
        }

        state.PendingScalarValues.Remove(requestSequence);
        var child = (message["body"]?["variables"] as JArray)?
            .Children<JObject>()
            .FirstOrDefault(variable => !string.Equals(
                variable.Value<string>("name"),
                "[[Prototype]]",
                StringComparison.Ordinal));
        if (message.Value<bool?>("success") == true && child !=  null)
        {
            pendingScalar.Variable["value"] = child["value"]?.DeepClone();
            pendingScalar.Variable["variablesReference"] =
                child.Value<int?>("variablesReference") ?? 0;
        }

        pendingScalar.Materialization.Remaining--;
        if (pendingScalar.Materialization.Remaining > 0)
        {
            return true;
        }

        diagnosticSink?.Invoke("managed-scalar-materialization-complete");
        dispatch = ClientOnly(pendingScalar.Materialization.Response.ToString(Formatting.None));
        return true;
    }

    private static bool IsManagedScalarType(string? type) => type switch
    {
        "bool" or "byte" or "sbyte" or "char" or "decimal" or "double" or
        "float" or "int" or "uint" or "long" or "ulong" or "short" or
        "ushort" or "string" => true,
        _ => false
    };

    private string TransformManagedVariablesResponse(JObject message, int parentReference)
    {
        if (message["body"]?["variables"] is not JArray variables)
        {
            return message.ToString(Formatting.None);
        }
        foreach (var prototype in variables.Children<JObject>()
            .Where(variable => string.Equals(
                variable.Value<string>("name"),
                "[[Prototype]]",
                StringComparison.Ordinal))
            .ToList())
        {
            prototype.Remove();
        }
        foreach (var returnValue in variables.Children<JObject>()
            .Where(IsVisualStudioReturnValue)
            .ToList())
        {
            returnValue.Remove();
        }
        var parentIsArray = state.ArrayReferences.Contains(parentReference);
        if (parentIsArray)
        {
            foreach (var length in variables.Children<JObject>()
                .Where(variable => string.Equals(
                    variable.Value<string>("name"),
                    "length",
                    StringComparison.Ordinal))
                .ToList())
            {
                length.Remove();
            }
        }
        foreach (var variable in variables.Children<JObject>())
        {
            var runtimeName = variable.Value<string>("name");
            var name = runtimeName;
            int? arrayLength = null;
            if (!string.IsNullOrEmpty(name))
            {
                var separatorIndex = name!.LastIndexOf(
                    ManagedVariableExpressionBuilder.ManagedTypeSeparator);
                if (separatorIndex >= 0)
                {
                    var managedMetadata = name.Substring(separatorIndex + 1);
                    var arrayLengthIndex = managedMetadata.LastIndexOf(
                        ManagedVariableExpressionBuilder.ManagedArrayLengthSeparator);
                    var managedType = arrayLengthIndex >= 0
                        ? managedMetadata.Substring(0, arrayLengthIndex)
                        : managedMetadata;
                    if (arrayLengthIndex >= 0
                        && int.TryParse(
                            managedMetadata.Substring(arrayLengthIndex + 1),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var parsedArrayLength))
                    {
                        arrayLength = parsedArrayLength;
                    }
                    name = name.Substring(0, separatorIndex);
                    if (!string.IsNullOrWhiteSpace(managedType))
                    {
                        variable["type"] = FormatManagedTypeName(managedType);
                    }
                }
                var storageIndex = name.LastIndexOf(
                    ManagedVariableExpressionBuilder.ManagedStorageSeparator);
                if (storageIndex >= 0)
                {
                    var storageKind = name.Substring(storageIndex + 1);
                    name = name.Substring(0, storageIndex);
                    if (!string.IsNullOrWhiteSpace(storageKind))
                    {
                        variable["__fecsStorageKind"] = storageKind;
                    }
                }
                variable["name"] = name;
                if (parentIsArray
                    && int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    name = "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                    variable["name"] = name;
                }
                var backingField = System.Text.RegularExpressions.Regex.Match(
                    name,
                    "^<(?<name>.+)>k__BackingField$");
                if (backingField.Success)
                {
                    name = backingField.Groups["name"].Value;
                    variable["name"] = name;
                    var presentationHint = variable["presentationHint"] as JObject ?? new JObject();
                    presentationHint["kind"] = "property";
                    variable["presentationHint"] = presentationHint;
                }
            }

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(runtimeName))
            {
                state.RuntimeNames[new ManagedVariableIdentity(parentReference, name!)] = runtimeName!;
            }

            var variablesReference = variable.Value<int?>("variablesReference") ?? 0;
            var type = variable.Value<string>("type");
            if (arrayLength.HasValue && !string.IsNullOrWhiteSpace(type))
            {
                var elementType = type!.EndsWith("[]", StringComparison.Ordinal)
                    ? type.Substring(0, type.Length - 2)
                    : type;
                variable["value"] = "{" + elementType + "["
                    + arrayLength.Value.ToString(CultureInfo.InvariantCulture) + "]}";
                if (arrayLength.Value == 0)
                {
                    variable["variablesReference"] = 0;
                }
                else if (variablesReference > 0)
                {
                    state.ArrayReferences.Add(variablesReference);
                }
            }
            else if (variablesReference > 0
                && !string.IsNullOrWhiteSpace(type)
                && !string.Equals(type, "Object", StringComparison.Ordinal)
                && !string.Equals(type, "Array", StringComparison.Ordinal))
            {
                variable["value"] = "{" + type + "}";
            }
        }
        foreach (var variablesReference in variables.Children<JObject>()
            .Select(variable => variable.Value<int?>("variablesReference") ?? 0)
            .Where(reference => reference > 0))
        {
            state.LiveReferences.Add(variablesReference);
            state.StaleReferences.Remove(variablesReference);
            if (state.ImmutableReferences.Contains(parentReference))
            {
                state.ImmutableReferences.Add(variablesReference);
            }
        }
        diagnosticSink?.Invoke("variables-response-projected");
        return message.ToString(Formatting.None);
    }

    private static string FormatManagedTypeName(string typeName)
    {
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            return FormatManagedTypeName(typeName.Substring(0, typeName.Length - 2)) + "[]";
        }
        return typeName switch
        {
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.Char" => "char",
            "System.Decimal" => "decimal",
            "System.Double" => "double",
            "System.Int16" => "short",
            "System.Int32" => "int",
            "System.Int64" => "long",
            "System.Object" => "object",
            "System.SByte" => "sbyte",
            "System.Single" => "float",
            "System.String" => "string",
            "System.UInt16" => "ushort",
            "System.UInt32" => "uint",
            "System.UInt64" => "ulong",
            _ => typeName.Split('.').Last()
        };
    }

    private static bool IsVisualStudioReturnValue(JObject variable) =>
        variable["presentationHint"]?["vsAttributes"] is JArray attributes
        && attributes.Values<string>().Any(attribute => string.Equals(
            attribute,
            "returnValue",
            StringComparison.Ordinal));

    private string TransformInspectionResponse(JObject message, bool immutable)
    {
        message["command"] = "scopes";
        try
        {
            if (message.Value<bool?>("success") != true)
            {
                throw new InvalidOperationException(
                    "The JavaScript debugger could not evaluate the managed locals object.");
            }
            var variablesReference = message["body"]?.Value<int?>("variablesReference") ?? 0;
            if (variablesReference <= 0)
            {
                throw new InvalidDataException(
                    "The JavaScript debugger did not return a live managed locals object.");
            }
            state.LiveReferences.Add(variablesReference);
            state.StaleReferences.Remove(variablesReference);
            if (immutable)
            {
                state.ImmutableReferences.Add(variablesReference);
            }
            message["success"] = true;
            message["body"] = new JObject
            {
                ["scopes"] = new JArray(CreateScope("Locals", variablesReference))
            };
            message.Remove("message");
            diagnosticSink?.Invoke("scopes-response-projected");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            || exception is InvalidDataException
            || exception is ArgumentException)
        {
            message["success"] = false;
            message["message"] = "FrontEndCSharp could not inspect the active managed frame.";
            message.Remove("body");
            diagnosticSink?.Invoke(exception switch
            {
                InvalidDataException _ => "scopes-response-failed-data",
                ArgumentException _ => "scopes-response-failed-argument",
                _ => "scopes-response-failed-state"
            });
        }
        return message.ToString(Formatting.None);
    }

    private static JObject CreateScope(string name, int variablesReference) => new JObject
    {
        ["name"] = name,
        ["variablesReference"] = variablesReference,
        ["expensive"] = false
    };


}
