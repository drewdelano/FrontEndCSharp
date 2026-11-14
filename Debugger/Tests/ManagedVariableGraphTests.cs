using FrontEndCSharp.DebugAdapter;

namespace Debugger.Tests;

public sealed class ManagedVariableGraphTests
{
    [Test]
    public void MergesArgumentsAndLocalsAndProjectsRecursiveManagedObjects()
    {
        var nextReference = 0;
        var projection = ManagedVariableGraph.Project(
            [new ManagedValueSnapshot("System.String[]", "{System.String[]}")],
            [new ManagedValueSnapshot("ComplexType", "{ComplexType}", objectId: 7)],
            [
                new ManagedObjectSnapshot(7,
                [
                    new ManagedNamedValueSnapshot(
                        "<One>k__BackingField",
                        "System.Int32",
                        "1")
                    {
                        Target = new ManagedAssignmentTarget
                        {
                            Kind = "argument",
                            Index = 0,
                            Path = ["<One>k__BackingField"]
                        }
                    },
                    new ManagedNamedValueSnapshot(
                        "Self",
                        "ComplexType",
                        "{ComplexType}",
                        objectId: 7)
                ])
            ],
            () => ++nextReference,
            argumentNames: ["args"],
            localNames: ["complex"],
            argumentTargets: [new ManagedAssignmentTarget { Kind = "argument", Index = 0 }],
            localTargets: [new ManagedAssignmentTarget { Kind = "local", Index = 1 }]);

        var root = projection.Variables[projection.ScopeReference];
        Assert.Equal(["args", "complex"], root.Select(variable => variable.Name));
        Assert.Equal("System.String[]", root[0].Type);
        Assert.Equal("ComplexType", root[1].Type);
        Assert.NotEqual(0, root[1].VariablesReference);

        var complex = projection.Variables[root[1].VariablesReference];
        Assert.Equal(
            ["One", "<One>k__BackingField", "Self"],
            complex.Select(variable => variable.Name));
        Assert.Equal("1", complex[0].Value);
        Assert.Equal("argument", root[0].Target?.Kind);
        Assert.Equal(1, root[1].Target?.Index);
        Assert.Equal(["<One>k__BackingField"], complex[0].Target?.Path);
        Assert.Same(complex[0].Target, complex[1].Target);
        Assert.Equal(root[1].VariablesReference, complex[2].VariablesReference);
    }
}
