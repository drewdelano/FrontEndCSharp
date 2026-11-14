using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Compiler.Read
{
    internal static class RepresentedTypeFlowModels
    {
        internal sealed class Value
        {
            public HashSet<TypeInfo> Types { get; } = new();
            public HashSet<MethodBase> Methods { get; } = new();
            public HashSet<MemberInfo> Members { get; } = new();
            public HashSet<Type> ArrayTypes { get; } = new();
            public HashSet<TypeInfo> ExpressionTypes { get; } = new();
            public HashSet<PropertyInfo> ExpressionMembers { get; } = new();
            public HashSet<TypeInfo> ExpressionReceiverTypes { get; } = new();
            public HashSet<TypeInfo> LambdaReturnTypes { get; } = new();
            public HashSet<TypeInfo> LambdaBodyTypes { get; } = new();
            public HashSet<PropertyInfo> LambdaBodyMembers { get; } = new();
            public HashSet<TypeInfo> LambdaBodyReceiverTypes { get; } = new();

            public Value Clone()
            {
                var clone = new Value();
                clone.Types.UnionWith(Types);
                clone.Methods.UnionWith(Methods);
                clone.Members.UnionWith(Members);
                clone.ArrayTypes.UnionWith(ArrayTypes);
                clone.ExpressionTypes.UnionWith(ExpressionTypes);
                clone.ExpressionMembers.UnionWith(ExpressionMembers);
                clone.ExpressionReceiverTypes.UnionWith(ExpressionReceiverTypes);
                clone.LambdaReturnTypes.UnionWith(LambdaReturnTypes);
                clone.LambdaBodyTypes.UnionWith(LambdaBodyTypes);
                clone.LambdaBodyMembers.UnionWith(LambdaBodyMembers);
                clone.LambdaBodyReceiverTypes.UnionWith(LambdaBodyReceiverTypes);
                return clone;
            }

            public bool UnionWith(Value other)
            {
                var count = Count;
                Types.UnionWith(other.Types);
                Methods.UnionWith(other.Methods);
                Members.UnionWith(other.Members);
                ArrayTypes.UnionWith(other.ArrayTypes);
                ExpressionTypes.UnionWith(other.ExpressionTypes);
                ExpressionMembers.UnionWith(other.ExpressionMembers);
                ExpressionReceiverTypes.UnionWith(other.ExpressionReceiverTypes);
                LambdaReturnTypes.UnionWith(other.LambdaReturnTypes);
                LambdaBodyTypes.UnionWith(other.LambdaBodyTypes);
                LambdaBodyMembers.UnionWith(other.LambdaBodyMembers);
                LambdaBodyReceiverTypes.UnionWith(other.LambdaBodyReceiverTypes);
                return count != Count;
            }

            private int Count => Types.Count + Methods.Count + Members.Count + ArrayTypes.Count
                + ExpressionTypes.Count + ExpressionMembers.Count + ExpressionReceiverTypes.Count
                + LambdaReturnTypes.Count + LambdaBodyTypes.Count + LambdaBodyMembers.Count
                + LambdaBodyReceiverTypes.Count;
        }

        internal sealed class Result
        {
            public Value ReturnValue { get; } = new();
            public List<CallFlow> Calls { get; } = new();
            public Dictionary<FieldInfo, Value> FieldWrites { get; } = new();
            public Dictionary<Type, Value> ArrayWrites { get; } = new();
        }

        internal sealed record class CallFlow(MethodBase Method, Value[] Arguments);

        internal sealed class State
        {
            public State(Stack<Value> stack, Value[] locals)
            {
                Stack = stack;
                Locals = locals;
            }

            public Stack<Value> Stack { get; }
            public Value[] Locals { get; }

            public State Clone()
                => new(
                    new Stack<Value>(Stack.Reverse().Select(value => value.Clone())),
                    Locals.Select(value => value.Clone()).ToArray());

            public bool Merge(State incoming)
            {
                if (Stack.Count != incoming.Stack.Count || Locals.Length != incoming.Locals.Length)
                {
                    throw new InvalidOperationException("Represented-type flow reached an invalid IL stack merge.");
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
                return changed;
            }
        }
    }
}
