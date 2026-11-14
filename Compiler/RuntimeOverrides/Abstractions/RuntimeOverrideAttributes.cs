using System;

namespace FrontEndCSharp.RuntimeOverrides;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class RuntimeOverrideAssemblyAttribute : Attribute
{
    public RuntimeOverrideAssemblyAttribute(string targetAssemblyName)
    {
        TargetAssemblyName = targetAssemblyName;
    }

    public string TargetAssemblyName { get; }
}

public enum JavaScriptPackageDependencyKind
{
    Dependency,
    PeerDependency,
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class JavaScriptPackageDependencyAttribute : Attribute
{
    public JavaScriptPackageDependencyAttribute(string packageName, string versionRange)
    {
        PackageName = packageName;
        VersionRange = versionRange;
    }

    public string PackageName { get; }

    public string VersionRange { get; }

    public JavaScriptPackageDependencyKind Kind { get; set; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RuntimeOverrideTypeAttribute : Attribute
{
    public RuntimeOverrideTypeAttribute(Type targetType)
    {
        TargetType = targetType;
    }

    public Type TargetType { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RuntimeOverrideImplementationTypeAttribute : Attribute
{
}

public enum RuntimeOverrideMemberKind
{
    StaticMethod,
    InstanceMethod,
    Constructor,
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class JavaScriptOverrideAttribute : Attribute
{
    public RuntimeOverrideMemberKind MemberKind { get; set; }

    public string? TargetMemberName { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class RuntimeOverrideReturnTypeAttribute : Attribute
{
    public RuntimeOverrideReturnTypeAttribute(Type implementationType)
    {
        ImplementationType = implementationType;
    }

    public RuntimeOverrideReturnTypeAttribute(int genericParameterIndex)
    {
        GenericParameterIndex = genericParameterIndex;
    }

    public Type? ImplementationType { get; }

    public int GenericParameterIndex { get; } = -1;
}

[Flags]
public enum ManagedTypeAccess
{
    None = 0,
    PublicParameterlessConstructor = 1,
    ReadPublicProperties = 2,
    WritePublicProperties = 4,
    ReadPublicFields = 8,
    WritePublicFields = 16,
    InstanceConstructors = 32,
    ReadNonPublicProperties = 64,
    WriteNonPublicProperties = 128,
    ReadNonPublicFields = 256,
    WriteNonPublicFields = 512,
    EnumMetadata = 1024,
    CustomAttributes = 2048,
    PublicInstanceMethods = 4096,
}

[AttributeUsage(
    AttributeTargets.GenericParameter | AttributeTargets.Parameter,
    AllowMultiple = false,
    Inherited = false)]
public sealed class ManagedTypeAccessAttribute : Attribute
{
    public ManagedTypeAccessAttribute(ManagedTypeAccess access)
    {
        Access = access;
    }

    public ManagedTypeAccess Access { get; }

    public Type? NonPublicMemberAttributeType { get; set; }

    public Type[] ExcludedTypes { get; set; } = Array.Empty<Type>();
}
