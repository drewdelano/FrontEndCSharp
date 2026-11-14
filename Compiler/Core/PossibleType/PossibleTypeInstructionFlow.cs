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
    internal static class PossibleTypeInstructionFlow
    {
        internal static IEnumerable<int> GetSuccessors(
            int index,
            ILInstruction instruction,
            int instructionCount,
            IReadOnlyDictionary<int, int> offsetIndexes)
        {
            if (instruction.OpCode.FlowControl == FlowControl.Return
                || instruction.OpCode.FlowControl == FlowControl.Throw)
            {
                yield break;
            }
            if (instruction.Operand is int target
                && (instruction.OpCode.FlowControl == FlowControl.Branch
                    || instruction.OpCode.FlowControl == FlowControl.Cond_Branch)
                && offsetIndexes.TryGetValue(target, out var targetIndex))
            {
                yield return targetIndex;
            }
            else if (instruction.Operand is int[] targets)
            {
                foreach (var switchTarget in targets)
                {
                    if (offsetIndexes.TryGetValue(switchTarget, out var switchIndex))
                    {
                        yield return switchIndex;
                    }
                }
            }
            if (instruction.OpCode.FlowControl != FlowControl.Branch && index + 1 < instructionCount)
            {
                yield return index + 1;
            }
        }

        internal static IEnumerable<ExceptionHandlingClause> GetProtectingClauses(
            MethodBody methodBody,
            int instructionOffset)
            => methodBody.ExceptionHandlingClauses
                .Where(clause => clause.TryOffset <= instructionOffset
                    && instructionOffset < clause.TryOffset + clause.TryLength)
                .OrderBy(clause => clause.TryLength);

        internal static HashSet<Type> GetRuntimeExceptionTypes(OpCode opcode)
        {
            var exceptions = new HashSet<Type>();
            var name = opcode.Name ?? string.Empty;

            if (opcode == OpCodes.Callvirt || opcode == OpCodes.Ldvirtftn)
            {
                exceptions.Add(typeof(NullReferenceException));
            }

            if (opcode == OpCodes.Ldfld || opcode == OpCodes.Ldflda || opcode == OpCodes.Stfld)
            {
                exceptions.Add(typeof(NullReferenceException));
            }
            else if (opcode == OpCodes.Ldsfld || opcode == OpCodes.Stsfld)
            {
                exceptions.Add(typeof(TypeInitializationException));
            }

            if (opcode == OpCodes.Newarr)
            {
                exceptions.Add(typeof(OverflowException));
            }
            if (opcode == OpCodes.Ldlen
                || name.StartsWith("ldelem", StringComparison.Ordinal)
                || name.StartsWith("stelem", StringComparison.Ordinal))
            {
                exceptions.Add(typeof(NullReferenceException));
                if (opcode != OpCodes.Ldlen)
                {
                    exceptions.Add(typeof(IndexOutOfRangeException));
                }
                if (opcode == OpCodes.Stelem_Ref || opcode == OpCodes.Stelem)
                {
                    exceptions.Add(typeof(ArrayTypeMismatchException));
                }
            }

            if (opcode == OpCodes.Div || opcode == OpCodes.Div_Un
                || opcode == OpCodes.Rem || opcode == OpCodes.Rem_Un)
            {
                exceptions.Add(typeof(DivideByZeroException));
                exceptions.Add(typeof(OverflowException));
            }
            if (opcode == OpCodes.Add_Ovf || opcode == OpCodes.Add_Ovf_Un
                || opcode == OpCodes.Sub_Ovf || opcode == OpCodes.Sub_Ovf_Un
                || opcode == OpCodes.Mul_Ovf || opcode == OpCodes.Mul_Ovf_Un
                || name.StartsWith("conv.ovf.", StringComparison.Ordinal))
            {
                exceptions.Add(typeof(OverflowException));
            }
            if (opcode == OpCodes.Ckfinite)
            {
                exceptions.Add(typeof(ArithmeticException));
            }
            if (opcode == OpCodes.Castclass || opcode == OpCodes.Unbox
                || opcode == OpCodes.Unbox_Any)
            {
                exceptions.Add(typeof(InvalidCastException));
            }
            if (opcode == OpCodes.Unbox || opcode == OpCodes.Unbox_Any)
            {
                exceptions.Add(typeof(NullReferenceException));
            }

            return exceptions;
        }

        internal static FlowValue ConcreteDeclaredValue(Type type)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType()!;
            }
            var isConcrete = type == typeof(Type)
                    || typeof(MemberInfo).IsAssignableFrom(type)
                    || !type.IsInterface && !type.IsAbstract && !type.IsGenericParameter && !type.IsPointer;
            return isConcrete
                ? type.IsValueType
                    ? FlowValue.ForType(type)
                    : FlowValue.ForUnknownType(type)
                : new FlowValue();
        }

        internal static IReadOnlyList<ByRefParameterTypes> CreateByRefParameterTypes(
            MethodBase method,
            IReadOnlyDictionary<int, FlowValue> writes)
        {
            var parameterOffset = method.IsStatic ? 0 : 1;
            var parameters = method.GetParameters();
            return writes.OrderBy(pair => pair.Key).Select(pair =>
            {
                var parameterIndex = pair.Key - parameterOffset;
                if (parameterIndex < 0
                    || parameterIndex >= parameters.Length
                    || !parameters[parameterIndex].ParameterType.IsByRef)
                {
                    throw new InvalidDataException(
                        $"Possible-type analysis recorded an invalid by-ref argument index {pair.Key} for '{method}'.");
                }
                var elementType = parameters[parameterIndex].ParameterType.GetElementType()!;
                var writtenTypes = pair.Value.Types
                    .Where(type => type != typeof(KnownNull))
                    .ToHashSet();
                var incompatible = writtenTypes.FirstOrDefault(type =>
                    !elementType.IsAssignableFrom(type));
                if (incompatible != null)
                {
                    throw new InvalidDataException(
                        $"Method '{method}' writes incompatible type '{incompatible}' to by-ref parameter "
                        + $"'{parameters[parameterIndex].Name}' of type '{elementType}'.");
                }
                return new ByRefParameterTypes(
                    parameterIndex,
                    parameters[parameterIndex].IsOut,
                    writtenTypes,
                    pair.Value.Types.Contains(typeof(KnownNull)));
            }).ToArray();
        }

        internal static IReadOnlyCollection<Type> GetRuntimeOverrideDependencyReturnTypes(
            ReaderContext context,
            MethodInfo method)
        {
            if (!context.RuntimeOverrides.HasRuntimeOverride(method))
            {
                return Array.Empty<Type>();
            }
            return RuntimeOverrideTypeResolver.FindRuntimeOverrideReturnImplementations(
                context,
                method,
                method.ReturnType);
        }

        internal static IReadOnlyCollection<Type> GetRuntimeOverrideAwaitableResultTypes(
            ReaderContext context,
            MethodInfo method)
        {
            if (!context.RuntimeOverrides.HasRuntimeOverride(method))
            {
                return Array.Empty<Type>();
            }
            return context.RuntimeOverrides.GetAwaitableResultTypePatterns(method);
        }

        internal static IReadOnlyList<ByRefParameterTypes> GetByRefParameterTypes(
            ReaderContext context,
            MethodBase method,
            IReadOnlyDictionary<MethodBase, Dictionary<int, FlowValue>> ordinaryWrites)
        {
            if (context.RuntimeOverrides.HasRuntimeOverride(method))
            {
                return context.RuntimeOverrides.GetByRefParameterWrites(method)
                    .Select(write => new ByRefParameterTypes(
                        write.ParameterIndex,
                        write.IsOut,
                        write.Types.ToHashSet(),
                        write.IncludesNull))
                    .ToArray();
            }

            var normalized = GenericsHelper.GetPossiblyGenericReferenceFromMethod(method);
            if (!ordinaryWrites.TryGetValue(normalized, out var writes))
            {
                return Array.Empty<ByRefParameterTypes>();
            }
            var parameterOffset = normalized.IsStatic ? 0 : 1;
            var parameters = normalized.GetParameters();
            return writes.Select(write =>
            {
                var parameterIndex = write.Key - parameterOffset;
                return new ByRefParameterTypes(
                    parameterIndex,
                    parameters[parameterIndex].IsOut,
                    write.Value.Types
                        .Where(type => type != typeof(KnownNull))
                        .ToHashSet(),
                    write.Value.Types.Contains(typeof(KnownNull)));
            }).ToArray();
        }

        internal static bool IsTaskFromResult(MethodInfo method)
            => method.IsGenericMethod
                && method.Name == nameof(Task.FromResult)
                && method.DeclaringType == typeof(Task);

        internal static bool IsAwaitableCarrierMethod(MethodInfo method)
        {
            var definition = method.DeclaringType?.IsGenericType == true
                ? method.DeclaringType.GetGenericTypeDefinition()
                : method.DeclaringType;
            return definition == typeof(Task<>)
                    && (method.Name == "GetAwaiter"
                        || method.Name == "ConfigureAwait")
                || definition == typeof(ConfiguredTaskAwaitable<>)
                    && method.Name == "GetAwaiter";
        }

        internal static bool IsAwaitableResultExtractor(MethodInfo method)
        {
            var declaringType = method.DeclaringType;
            var definition = declaringType?.IsGenericType == true
                ? declaringType.GetGenericTypeDefinition()
                : declaringType;
            return definition == typeof(Task<>) && method.Name == "get_Result"
                || definition == typeof(TaskAwaiter<>) && method.Name == "GetResult"
                || definition?.FullName
                    == "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter"
                    && method.Name == "GetResult";
        }

        internal static bool IsGenericEnumerableGetEnumerator(MethodInfo method)
            => method.Name == nameof(IEnumerable<object>.GetEnumerator)
                && method.GetParameters().Length == 0
                && method.DeclaringType?.IsGenericType == true
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(IEnumerable<>);

        internal static bool IsGenericArrayEnumerator(Type type)
            => type.IsGenericType
                && type.GetGenericTypeDefinition().FullName == "System.SZGenericArrayEnumerator`1";

        internal static bool TryGetLocalIndex(OpCode opcode, object operand, out int index)
        {
            index = opcode.Name switch
            {
                "ldloc.0" or "stloc.0" => 0,
                "ldloc.1" or "stloc.1" => 1,
                "ldloc.2" or "stloc.2" => 2,
                "ldloc.3" or "stloc.3" => 3,
                _ => -1
            };
            if (index >= 0)
            {
                return true;
            }
            if ((opcode.Name!.StartsWith("ldloc", StringComparison.Ordinal)
                    || opcode.Name.StartsWith("stloc", StringComparison.Ordinal))
                && operand is LocalVariableInfo local)
            {
                index = local.LocalIndex;
                return true;
            }
            return false;
        }

        internal static bool TryGetArgumentIndex(
            OpCode opcode,
            object operand,
            out int index)
        {
            index = opcode.Name switch
            {
                "ldarg.0" => 0,
                "ldarg.1" => 1,
                "ldarg.2" => 2,
                "ldarg.3" => 3,
                _ => -1
            };
            if (index >= 0)
            {
                return true;
            }
            if ((opcode.Name!.StartsWith("ldarg", StringComparison.Ordinal)
                    || opcode.Name.StartsWith("starg", StringComparison.Ordinal))
                && operand is int argumentIndex)
            {
                index = argumentIndex;
                return true;
            }
            return false;
        }

        internal static bool IsByRefArgument(MethodBase method, int argumentIndex)
        {
            var parameterIndex = argumentIndex - (method.IsStatic ? 0 : 1);
            var parameters = method.GetParameters();
            return parameterIndex >= 0
                && parameterIndex < parameters.Length
                && parameters[parameterIndex].ParameterType.IsByRef;
        }

        internal static void ApplyStackBehaviour(Stack<FlowValue> stack, OpCode opcode)
        {
            Pop(stack, PopCount(opcode.StackBehaviourPop));
            var pushes = PushCount(opcode.StackBehaviourPush);
            for (var index = 0; index < pushes; index++)
            {
                stack.Push(new FlowValue());
            }
        }

        internal static int PopCount(StackBehaviour behavior) => behavior switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi
                or StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8
                or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi
                or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4
                or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref => 3,
            _ => 0
        };

        internal static int PushCount(StackBehaviour behavior) => behavior == StackBehaviour.Push0 ? 0 : 1;

        internal static FlowValue Pop(Stack<FlowValue> stack)
            => stack.Count == 0 ? new FlowValue() : stack.Pop();

        internal static void Pop(Stack<FlowValue> stack, int count)
        {
            while (count-- > 0)
            {
                Pop(stack);
            }
        }

        internal static FlowValue[] PopArguments(Stack<FlowValue> stack, int count)
        {
            var arguments = new FlowValue[count];
            for (var index = count - 1; index >= 0; index--)
            {
                arguments[index] = Pop(stack);
            }
            return arguments;
        }

    }
}
