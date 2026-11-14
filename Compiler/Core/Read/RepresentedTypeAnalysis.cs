using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.PossibleType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Value = Compiler.Read.RepresentedTypeFlowModels.Value;

namespace Compiler.Read
{
    internal static class RepresentedTypeAnalysis
    {
        internal sealed class Demand
        {
            public HashSet<TypeInfo> Types { get; } = new();
            public HashSet<TypeInfo> FieldTypes { get; } = new();
            public HashSet<TypeInfo> EnumTypes { get; } = new();
            public HashSet<PropertyInfo> ReadProperties { get; } = new();
            public HashSet<PropertyInfo> WrittenProperties { get; } = new();
            public HashSet<MethodInfo> InvokedMethods { get; } = new();
        }

        public static Demand FindDemand(ReaderContext context)
        {
            var decoders = context.OrderToOutput.Values
                .Select(value => value.MethodDecoder)
                .ToDictionary(decoder => decoder.MethodBase);
            var returns = decoders.Keys
                .OfType<MethodInfo>()
                .Where(method => !context.RuntimeOverrides.HasRuntimeOverride(method))
                .ToDictionary(method => method, _ => new Value());
            var arguments = decoders.Keys.ToDictionary(
                method => method,
                method => Enumerable.Range(0, method.GetParameters().Length + (method.IsStatic ? 0 : 1))
                    .Select(_ => new Value())
                    .ToArray());
            var fields = context.AllReferencedFields.Keys.ToDictionary(field => field, _ => new Value());
            var arrayElements = new Dictionary<Type, Value>();
            var runtimeReceivers = PossibleTypeAnalysis.FindVirtualReceivers(context);

            while (true)
            {
                var changed = false;
                foreach (var decoder in decoders.Values)
                {
                    if (context.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                    {
                        continue;
                    }
                    var result = RepresentedTypeMethodAnalyzer.Analyze(
                        decoder,
                        returns,
                        arguments[decoder.MethodBase],
                        fields,
                        arrayElements,
                        runtimeReceivers,
                        demand: null);
                    if (decoder.MethodBase is MethodInfo method
                        && returns.TryGetValue(method, out var returnValue))
                    {
                        changed |= returnValue.UnionWith(result.ReturnValue);
                    }
                    foreach (var call in result.Calls)
                    {
                        var called = GenericsHelper.GetPossiblyGenericReferenceFromMethod(call.Method);
                        if (!arguments.TryGetValue(called, out var targetArguments))
                        {
                            continue;
                        }
                        for (var index = 0; index < Math.Min(targetArguments.Length, call.Arguments.Length); index++)
                        {
                            changed |= targetArguments[index].UnionWith(call.Arguments[index]);
                        }
                    }
                    foreach (var write in result.FieldWrites)
                    {
                        var field = GenericsHelper.GetPossiblyGenericReferenceFromField(write.Key);
                        if (fields.TryGetValue(field, out var fieldValue))
                        {
                            changed |= fieldValue.UnionWith(write.Value);
                        }
                    }
                    foreach (var write in result.ArrayWrites)
                    {
                        if (!arrayElements.TryGetValue(write.Key, out var elementValue))
                        {
                            elementValue = new Value();
                            arrayElements.Add(write.Key, elementValue);
                        }
                        changed |= elementValue.UnionWith(write.Value);
                    }
                }
                if (!changed)
                {
                    break;
                }
            }

            var demand = new Demand();
            foreach (var decoder in decoders.Values.Where(decoder =>
                !context.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase)))
            {
                RepresentedTypeMethodAnalyzer.Analyze(
                    decoder,
                    returns,
                    arguments[decoder.MethodBase],
                    fields,
                    arrayElements,
                    runtimeReceivers,
                    demand);
            }
            return demand;
        }

    }
}
