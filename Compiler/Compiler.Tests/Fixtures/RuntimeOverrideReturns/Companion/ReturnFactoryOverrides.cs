using FrontEndCSharp.RuntimeOverrides;
using RuntimeOverrideReturns;

namespace RuntimeOverrideReturnsCompanion;

[RuntimeOverrideType(typeof(ReturnFactory))]
internal static class ReturnFactoryOverrides
{
    [JavaScriptOverride]
    [RuntimeOverrideReturnType(typeof(ReturnContractImplementation))]
    internal static IReturnContract CreateContract() => new ReturnContractImplementation();

    [JavaScriptOverride]
    [RuntimeOverrideReturnType(typeof(ChildReturn))]
    internal static ParentReturn CreateParent() => new ChildReturn();

    [JavaScriptOverride]
    internal static void Consume(
        [ManagedTypeAccess(
            ManagedTypeAccess.ReadPublicProperties,
            ExcludedTypes = new[] { typeof(string) })] object value) =>
        throw null!;

    [JavaScriptOverride]
    internal static void Inspect<
        [ManagedTypeAccess(
            ManagedTypeAccess.InstanceConstructors
            | ManagedTypeAccess.ReadPublicProperties,
            ExcludedTypes = new[] { typeof(string) })] T>() =>
        throw null!;

    [JavaScriptOverride]
    internal static void InspectString<
        [ManagedTypeAccess(ManagedTypeAccess.InstanceConstructors)] T>() =>
        throw null!;

    [JavaScriptOverride]
    internal static bool CreateOut(string selector, out IOutContract? value) =>
        AssignOut(selector, out value);

    [JavaScriptOverride]
    internal static void ReplaceRef(ref IRefContract? value) =>
        value = new WrittenRefContract();

    private static bool AssignOut(string selector, out IOutContract? value)
    {
        if (selector == null)
        {
            value = null;
            return false;
        }
        if (selector.Length == 0)
        {
            value = new FirstOutContract();
            return true;
        }
        value = new SecondOutContract();
        return true;
    }
}
