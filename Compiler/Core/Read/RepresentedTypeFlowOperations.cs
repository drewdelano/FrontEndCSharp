using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Compiler.DTOs;
using CallFlow = Compiler.Read.RepresentedTypeFlowModels.CallFlow;
using Result = Compiler.Read.RepresentedTypeFlowModels.Result;
using State = Compiler.Read.RepresentedTypeFlowModels.State;
using Value = Compiler.Read.RepresentedTypeFlowModels.Value;

namespace Compiler.Read
{
    internal static class RepresentedTypeFlowOperations
    {
        internal static Value[] NewValues(int count)
            => Enumerable.Range(0, count).Select(_ => new Value()).ToArray();

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

        internal static bool TryGetArgumentIndex(OpCode opcode, object operand, out int index)
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

        internal static Value Pop(Stack<Value> stack) => stack.Count == 0 ? new Value() : stack.Pop();

        internal static void Pop(Stack<Value> stack, int count)
        {
            while (count-- > 0)
            {
                Pop(stack);
            }
        }

        internal static Value[] PopArguments(Stack<Value> stack, int count)
        {
            var arguments = new Value[count];
            for (var index = count - 1; index >= 0; index--)
            {
                arguments[index] = Pop(stack);
            }
            return arguments;
        }

    }
}
