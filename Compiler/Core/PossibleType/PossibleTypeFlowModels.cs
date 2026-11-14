using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using FrontEndCSharp.RuntimeOverrides;
using static Compiler.PossibleType.PossibleTypeInstructionFlow;

namespace Compiler.PossibleType
{
    internal sealed class LinearResult
    {
        public HashSet<int> ReachableOffsets { get; } = new();
        public FlowValue ReturnValue { get; } = new();
        public HashSet<Type> EscapingExceptions { get; } = new();
        public Dictionary<LocalVirtualCallSite, FlowValue> VirtualReceivers { get; } = new();
        public List<PossibleTypeCallFlow> Calls { get; } = new();
        public Dictionary<FieldLocation, FlowValue> FieldWrites { get; } = new();
        public Dictionary<ArrayLocation, FlowValue> ArrayWrites { get; } = new();
        public Dictionary<int, FlowValue> ByRefWrites { get; } = new();
    }

    internal sealed record PossibleTypeCallFlow(MethodBase Method, FlowValue[] Arguments);

    internal readonly record struct AllocationSite(MethodBase Method, int Offset, Type Type);

    internal readonly record struct FieldLocation(FieldInfo Field, AllocationSite? Allocation);

    internal readonly record struct ArrayLocation(Type ArrayType, AllocationSite? Allocation);

    internal readonly record struct LocalVirtualCallSite(int Offset, MethodInfo Method);

    internal readonly record struct VirtualCallSite(
        MethodBase Caller,
        int Offset,
        MethodInfo Method);

    internal sealed record CallArgumentTypes(
        IReadOnlySet<Type> Types,
        IReadOnlySet<Type> UnknownAllocationTypes)
    {
        public bool HasUnknownAllocation => UnknownAllocationTypes.Count > 0;
    }

    internal sealed record ByRefParameterTypes(
        int ParameterIndex,
        bool IsOut,
        IReadOnlySet<Type> Types,
        bool IncludesNull);

    internal sealed class KnownNull
    {
    }


    internal sealed class FlowValue
    {
        private HashSet<StorageReference> references;

        public HashSet<Type> Types { get; } = new();
        public HashSet<AllocationSite> Allocations { get; } = new();
        public HashSet<Type> UnknownAllocationTypes { get; } = new();
        public HashSet<Type> AwaitableResultTypes { get; } = new();
        public IEnumerable<StorageReference> References =>
            references ?? Enumerable.Empty<StorageReference>();
        public bool HasUnknownAllocation => UnknownAllocationTypes.Count > 0;
        public bool IsEmpty => Types.Count == 0
            && Allocations.Count == 0
            && !HasUnknownAllocation;

        public static FlowValue ForType(Type type)
        {
            var value = new FlowValue();
            value.Types.Add(type);
            return value;
        }

        public static FlowValue ForTypes(
            IEnumerable<Type> types,
            bool hasUnknownAllocation = false)
        {
            var value = new FlowValue();
            value.Types.UnionWith(types);
            if (hasUnknownAllocation)
            {
                value.UnknownAllocationTypes.UnionWith(value.Types.Where(type =>
                    !type.IsValueType && type != typeof(KnownNull)));
            }
            return value;
        }

        public static FlowValue ForUnknownType(Type type)
        {
            var value = ForType(type);
            if (!type.IsValueType && type != typeof(KnownNull))
            {
                value.UnknownAllocationTypes.Add(type);
            }
            return value;
        }

        public static FlowValue ForAllocation(Type type, MethodBase method, int offset)
        {
            var value = ForType(type);
            value.Allocations.Add(new AllocationSite(method, offset, type));
            return value;
        }

        public FlowValue Clone()
        {
            var value = new FlowValue();
            value.Types.UnionWith(Types);
            value.Allocations.UnionWith(Allocations);
            value.UnknownAllocationTypes.UnionWith(UnknownAllocationTypes);
            value.AwaitableResultTypes.UnionWith(AwaitableResultTypes);
            if (references != null)
            {
                value.references = new HashSet<StorageReference>(references);
            }
            return value;
        }

        public FlowValue Filter(Type targetType)
        {
            var value = new FlowValue();
            value.Types.UnionWith(Types.Where(type =>
                type == typeof(KnownNull) || targetType.IsAssignableFrom(type)));
            value.Allocations.UnionWith(Allocations.Where(allocation =>
                targetType.IsAssignableFrom(allocation.Type)));
            value.UnknownAllocationTypes.UnionWith(UnknownAllocationTypes.Where(type =>
                targetType.IsAssignableFrom(type)));
            value.AwaitableResultTypes.UnionWith(AwaitableResultTypes);
            if (references != null)
            {
                value.references = new HashSet<StorageReference>(references);
            }
            return value;
        }

        public bool UnionWith(FlowValue other)
        {
            var changed = Types.UnionWithChanged(other.Types);
            changed |= Allocations.UnionWithChanged(other.Allocations);
            changed |= UnknownAllocationTypes.UnionWithChanged(other.UnknownAllocationTypes);
            changed |= AwaitableResultTypes.UnionWithChanged(other.AwaitableResultTypes);
            if (other.references != null)
            {
                references ??= new HashSet<StorageReference>();
                changed |= references.UnionWithChanged(other.references);
            }
            return changed;
        }

        public void AddReference(StorageReference reference)
        {
            references ??= new HashSet<StorageReference>();
            references.Add(reference);
        }

        public static FlowValue ForReference(StorageReference reference)
        {
            var value = new FlowValue();
            value.AddReference(reference);
            return value;
        }
    }

    internal enum StorageKind
    {
        Local,
        Argument,
        Field,
        ArrayElement,
    }

    internal readonly record struct StorageReference(
        StorageKind Kind,
        int Index,
        FieldLocation Field,
        ArrayLocation Array)
    {
        public static StorageReference ForLocal(int index)
            => new(StorageKind.Local, index, default, default);

        public static StorageReference ForArgument(int index)
            => new(StorageKind.Argument, index, default, default);

        public static StorageReference ForField(FieldLocation field)
            => new(StorageKind.Field, -1, field, default);

        public static StorageReference ForArrayElement(ArrayLocation array)
            => new(StorageKind.ArrayElement, -1, default, array);
    }

    internal sealed class FlowState
    {
        public FlowState(
            Stack<FlowValue> stack,
            FlowValue[] locals,
            FlowValue[] arguments,
            HashSet<Type> currentExceptions = null,
            Dictionary<int, FlowValue> byRefWrites = null,
            Dictionary<FieldLocation, FlowValue> fields = null,
            Dictionary<ArrayLocation, FlowValue> arrayElements = null)
        {
            Stack = stack;
            Locals = locals;
            Arguments = arguments;
            CurrentExceptions = currentExceptions ?? new HashSet<Type>();
            ByRefWrites = byRefWrites;
            Fields = fields;
            ArrayElements = arrayElements;
        }

        public Stack<FlowValue> Stack { get; }
        public FlowValue[] Locals { get; }
        public FlowValue[] Arguments { get; }
        public HashSet<Type> CurrentExceptions { get; }
        public Dictionary<int, FlowValue> ByRefWrites { get; }
        public Dictionary<FieldLocation, FlowValue> Fields { get; }
        public Dictionary<ArrayLocation, FlowValue> ArrayElements { get; }

        public FlowState Clone()
            => new(
                new Stack<FlowValue>(Stack.Reverse().Select(value => value.Clone())),
                Locals.Select(value => value.Clone()).ToArray(),
                Arguments?.Select(value => value.Clone()).ToArray(),
                new HashSet<Type>(CurrentExceptions),
                ByRefWrites?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
                Fields?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
                ArrayElements?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()));

        public bool Merge(FlowState incoming)
        {
            if (Stack.Count != incoming.Stack.Count
                || Locals.Length != incoming.Locals.Length
                || (Arguments == null) != (incoming.Arguments == null)
                || Arguments != null && Arguments.Length != incoming.Arguments.Length
                || (ByRefWrites == null) != (incoming.ByRefWrites == null)
                || (Fields == null) != (incoming.Fields == null)
                || (ArrayElements == null) != (incoming.ArrayElements == null))
            {
                throw new InvalidOperationException("Possible-type flow reached an invalid IL stack merge.");
            }
            var changed = false;
            var currentStack = Stack.Reverse().ToArray();
            var incomingStack = incoming.Stack.Reverse().ToArray();
            for (var index = 0; index < currentStack.Length; index++)
            {
                changed |= currentStack[index].UnionWith(incomingStack[index]);
            }
            for (var index = 0; index < Locals.Length; index++)
            {
                changed |= Locals[index].UnionWith(incoming.Locals[index]);
            }
            for (var index = 0; index < (Arguments?.Length ?? 0); index++)
            {
                changed |= Arguments[index].UnionWith(incoming.Arguments[index]);
            }
            changed |= CurrentExceptions.UnionWithChanged(incoming.CurrentExceptions);
            foreach (var write in incoming.ByRefWrites
                ?? Enumerable.Empty<KeyValuePair<int, FlowValue>>())
            {
                if (!ByRefWrites.TryGetValue(write.Key, out var value))
                {
                    ByRefWrites.Add(write.Key, write.Value.Clone());
                    changed = true;
                }
                else
                {
                    changed |= value.UnionWith(write.Value);
                }
            }
            if (Fields != null)
            {
                changed |= MergeStorage(Fields, incoming.Fields);
            }
            if (ArrayElements != null)
            {
                changed |= MergeStorage(ArrayElements, incoming.ArrayElements);
            }
            return changed;

            static bool MergeStorage<TKey>(
                Dictionary<TKey, FlowValue> target,
                IReadOnlyDictionary<TKey, FlowValue> source)
            {
                var storageChanged = false;
                foreach (var entry in source)
                {
                    if (!target.TryGetValue(entry.Key, out var value))
                    {
                        target.Add(entry.Key, entry.Value.Clone());
                        storageChanged = true;
                    }
                    else
                    {
                        storageChanged |= value.UnionWith(entry.Value);
                    }
                }
                return storageChanged;
            }
        }
    }

    internal static class PossibleTypeCollectionExtensions
    {
        internal static bool UnionWithChanged<T>(
            this HashSet<T> target,
            IEnumerable<T> values)
        {
            var count = target.Count;
            target.UnionWith(values);
            return target.Count != count;
        }
    }

    internal sealed record class ExceptionRegion(int TryOffset, int TryLength);
}
