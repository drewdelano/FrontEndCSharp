using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: RuntimeOverrideAssembly("System.Linq.Expressions")]

namespace FrontEndCSharp.SystemLinqExpressionsRuntimeOverrides;

[RuntimeOverrideType(typeof(Expression))]
internal static class ExpressionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_NodeType")]
    internal static ExpressionType NodeType(Expression @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static string ToString(Expression @this) => throw null!;

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern ParameterExpression Parameter(Type type, string? name);

    [JavaScriptOverride]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MemberExpression Property(Expression? expression, MethodInfo propertyAccessor);

    [JavaScriptOverride]
    internal static Expression<TDelegate> Lambda<TDelegate>(
        Expression body,
        ParameterExpression[]? parameters) => throw null!;
}

[RuntimeOverrideType(typeof(LambdaExpression))]
internal static class LambdaExpressionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Body")]
    [RuntimeOverrideReturnType(typeof(MemberExpression))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Expression Body(LambdaExpression @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_ReturnType")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Type ReturnType(LambdaExpression @this);
}

[RuntimeOverrideType(typeof(MemberExpression))]
internal static class MemberExpressionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_NodeType")]
    internal static ExpressionType NodeType(MemberExpression @this) => throw null!;

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Member")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern MemberInfo Member(MemberExpression @this);

    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Expression")]
    [RuntimeOverrideReturnType(typeof(ParameterExpression))]
    [RuntimeOverrideReturnType(typeof(MemberExpression))]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern Expression? Expression(MemberExpression @this);
}

[RuntimeOverrideType(typeof(ParameterExpression))]
internal static class ParameterExpressionOverrides
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod, TargetMemberName = "get_Name")]
    [MethodImpl(MethodImplOptions.InternalCall)]
    internal static extern string? Name(ParameterExpression @this);
}

[RuntimeOverrideType(typeof(Expression<>))]
internal static class GenericExpressionOverrides<TDelegate>
    where TDelegate : Delegate
{
    [JavaScriptOverride(MemberKind = RuntimeOverrideMemberKind.InstanceMethod)]
    internal static TDelegate Compile(Expression<TDelegate> expression) => throw null!;
}
