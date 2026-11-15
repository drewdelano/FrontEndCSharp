using System.Reflection;
using Interop.JS.React;

namespace FrontEndCSharp.Overrides.ReactTests;

public sealed class ImportedReactComponentContractTests
{
    [Test]
    public void React_exposes_a_distinct_generic_imported_component_boundary()
    {
        var importedComponent = typeof(React).Assembly.GetType(
            "Interop.JS.React.ImportedReactComponent",
            throwOnError: false);

        Assert.NotNull(importedComponent);
        var createElement = Assert.Single(
            typeof(React).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name == nameof(React.CreateElement)
                && method.GetParameters().Length == 3
                && method.GetParameters()[0].ParameterType == importedComponent);
        Assert.Equal(typeof(ReactElement), createElement.ReturnType);
        Assert.Equal(typeof(ReactProps), createElement.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(ReactNode[]), createElement.GetParameters()[2].ParameterType);
    }
}
