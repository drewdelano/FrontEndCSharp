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
    internal sealed class PossibleTypeExceptionFlow
    {
        private readonly MethodDecoder _decoder;
        private readonly IReadOnlyDictionary<MethodBase, HashSet<Type>> _exceptions;
        private readonly Func<MethodBase, bool> _hasRuntimeOverride;
        private readonly IReadOnlyDictionary<int, int> _offsetIndexes;
        private readonly LinearResult _result;
        private readonly PossibleTypeStateMerger _stateMerger;

        internal PossibleTypeExceptionFlow(
            MethodDecoder decoder,
            IReadOnlyDictionary<MethodBase, HashSet<Type>> exceptions,
            Func<MethodBase, bool> hasRuntimeOverride,
            IReadOnlyDictionary<int, int> offsetIndexes,
            LinearResult result,
            PossibleTypeStateMerger stateMerger)
        {
            _decoder = decoder;
            _exceptions = exceptions;
            _hasRuntimeOverride = hasRuntimeOverride;
            _offsetIndexes = offsetIndexes;
            _result = result;
            _stateMerger = stateMerger;
        }

        internal void AddCalledExceptions(
            MethodBase called,
            FlowValue receiver,
            int instructionOffset,
            HashSet<Type> target)
        {
            if (called is MethodInfo virtualMethod
                && receiver != null
                && virtualMethod.IsVirtual)
            {
                var concreteReceivers = receiver.Types.Where(type =>
                    type != typeof(KnownNull)
                    && (!type.IsInterface && !type.IsAbstract
                        || type == typeof(Type)
                        || typeof(MemberInfo).IsAssignableFrom(type))).ToArray();
                foreach (var receiverType in concreteReceivers)
                {
                    var dispatched = GenericsHelper.GetPossiblyGenericReferenceFromMethod(
                        PossibleTypeAnalysis.ResolveVirtualTarget(receiverType, virtualMethod));
                    if (_exceptions.TryGetValue(dispatched, out var dispatchedExceptions))
                    {
                        target.UnionWith(dispatchedExceptions);
                    }
                }
                if (concreteReceivers.Length == 0)
                {
                    var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(called);
                    if (_exceptions.TryGetValue(normalized, out var calledExceptions))
                    {
                        target.UnionWith(calledExceptions);
                    }
                }
            }
            else
            {
                var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(called);
                if (_exceptions.TryGetValue(normalized, out var calledExceptions))
                {
                    target.UnionWith(calledExceptions);
                }
            }

            if (_hasRuntimeOverride(called) && _decoder.MethodBody != null)
            {
                foreach (var clause in GetProtectingClauses(_decoder.MethodBody, instructionOffset))
                {
                    if (clause.Flags == ExceptionHandlingClauseOptions.Clause
                        && clause.CatchType != null)
                    {
                        target.Add(clause.CatchType);
                    }
                }
            }
        }

        internal void PropagateExceptions(
            ILInstruction throwingInstruction,
            HashSet<Type> thrownTypes,
            FlowValue[] currentLocals,
            FlowValue[] currentArgumentValues,
            Dictionary<int, FlowValue> currentByRefWrites,
            Dictionary<FieldLocation, FlowValue> currentFields,
            Dictionary<ArrayLocation, FlowValue> currentArrayElements)
        {
            var clauses = _decoder.MethodBody == null
                ? []
                : GetProtectingClauses(_decoder.MethodBody, throwingInstruction.ILOffset).ToArray();
            foreach (var thrownType in thrownTypes)
            {
                var definitelyCaught = false;
                foreach (var clauseGroup in clauses.GroupBy(clause =>
                    new ExceptionRegion(clause.TryOffset, clause.TryLength)))
                {
                    foreach (var clause in clauseGroup)
                    {
                        var singleType = new HashSet<Type> { thrownType };
                        if (clause.Flags == ExceptionHandlingClauseOptions.Filter)
                        {
                            MergeExceptionState(clause.FilterOffset, singleType, pushException: true);
                            MergeExceptionState(clause.HandlerOffset, singleType, pushException: true);
                            continue;
                        }
                        if (clause.Flags == ExceptionHandlingClauseOptions.Clause
                            && clause.CatchType != null)
                        {
                            if (!clause.CatchType.IsAssignableFrom(thrownType))
                            {
                                continue;
                            }
                            MergeExceptionState(clause.HandlerOffset, singleType, pushException: true);
                            definitelyCaught = true;
                            break;
                        }
                        MergeExceptionState(clause.HandlerOffset, singleType, pushException: false);
                    }
                    if (definitelyCaught)
                    {
                        break;
                    }
                }
                if (!definitelyCaught)
                {
                    _result.EscapingExceptions.Add(thrownType);
                }
            }

            void MergeExceptionState(int targetOffset, HashSet<Type> exceptionTypes, bool pushException)
            {
                if (exceptionTypes.Count == 0
                    || !_offsetIndexes.TryGetValue(targetOffset, out var targetIndex))
                {
                    return;
                }
                var exceptionStack = new Stack<FlowValue>();
                if (pushException)
                {
                    exceptionStack.Push(FlowValue.ForTypes(
                        exceptionTypes,
                        hasUnknownAllocation: true));
                }
                _stateMerger.Merge(
                    targetIndex,
                    new FlowState(
                        exceptionStack,
                        currentLocals.Select(value => value.Clone()).ToArray(),
                        currentArgumentValues?.Select(value => value.Clone()).ToArray(),
                        new HashSet<Type>(exceptionTypes),
                        currentByRefWrites?.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value.Clone()),
                        currentFields?.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value.Clone()),
                        currentArrayElements?.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Value.Clone())));
            }
        }

    }

    internal sealed class PossibleTypeStateMerger
    {
        private readonly IDictionary<int, FlowState> _states;
        private readonly Queue<int> _pending;

        internal PossibleTypeStateMerger(
            IDictionary<int, FlowState> states,
            Queue<int> pending)
        {
            _states = states;
            _pending = pending;
        }

        internal void Merge(int index, FlowState incoming)
        {
            if (!_states.TryGetValue(index, out var existing))
            {
                _states.Add(index, incoming.Clone());
                _pending.Enqueue(index);
            }
            else if (existing.Merge(incoming))
            {
                _pending.Enqueue(index);
            }
        }
    }
}
