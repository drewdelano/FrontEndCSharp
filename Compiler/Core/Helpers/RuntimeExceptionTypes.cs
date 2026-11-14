using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Compiler.Helpers
{
    internal static class RuntimeExceptionTypes
    {
        public static IReadOnlyList<TypeInfo> RequiredMetadata { get; } = new[]
        {
            (TypeInfo)typeof(Exception),
            (TypeInfo)typeof(SystemException),
            (TypeInfo)typeof(MissingMemberException),
            (TypeInfo)typeof(OperationCanceledException),
            (TypeInfo)typeof(AggregateException),
            (TypeInfo)typeof(ArgumentException),
            (TypeInfo)typeof(ArgumentNullException),
            (TypeInfo)typeof(ArgumentOutOfRangeException),
            (TypeInfo)typeof(ArithmeticException),
            (TypeInfo)typeof(ArrayTypeMismatchException),
            (TypeInfo)typeof(DivideByZeroException),
            (TypeInfo)typeof(IndexOutOfRangeException),
            (TypeInfo)typeof(InvalidCastException),
            (TypeInfo)typeof(MissingFieldException),
            (TypeInfo)typeof(MissingMethodException),
            (TypeInfo)typeof(NullReferenceException),
            (TypeInfo)typeof(OverflowException),
            (TypeInfo)typeof(TaskCanceledException),
            (TypeInfo)typeof(TypeInitializationException)
        };
    }
}
