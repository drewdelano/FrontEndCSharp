using Interop.JS.LucideIcons;
using Interop.JS.React;
using System.Reflection;

namespace FrontEndCSharp.Overrides.ReactTests;

public sealed class CompanionTests
{
    public static TestCaseDataCollection<string, string, string, string> JavaScriptAdapterVersions => new()
    {
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.fecs", "@base-ui/react", "1.9.0" },
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn.fecs", "cn", "0.5.2" },
        { "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs", "lucide-react", "1.72.0" },
        { "FrontEndCSharp.Overrides.Interop.JS.MobX", "FrontEndCSharp.Overrides.Interop.JS.MobX.fecs", "mobx", "7.0.6" },
        { "FrontEndCSharp.Overrides.Interop.JS.MobX.React", "FrontEndCSharp.Overrides.Interop.JS.MobX.React.fecs", "mobx-react-lite", "5.1.0" },
        { "FrontEndCSharp.Overrides.Interop.JS.Motion", "FrontEndCSharp.Overrides.Interop.JS.Motion.fecs", "motion", "14.4.0" },
        { "FrontEndCSharp.Overrides.Interop.JS.React", "FrontEndCSharp.Overrides.Interop.JS.React.fecs", "react", "19.3.1" },
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts.fecs", "recharts", "3.8.0" },
    };

    public static TestCaseDataCollection<string, string, int, bool> Companions => new()
    {
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI.fecs", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.BaseUI", 96, true },
        { "FrontEndCSharp.Overrides.Interop.JS.Browser.fecs", "FrontEndCSharp.Overrides.Interop.JS.Browser", 66, true },
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn.fecs", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Cn", 1, true },
        { "FrontEndCSharp.Overrides.Interop.JS.LucideIcons.fecs", "FrontEndCSharp.Overrides.Interop.JS.LucideIcons", 2032, true },
        { "FrontEndCSharp.Overrides.Interop.JS.MobX.fecs", "FrontEndCSharp.Overrides.Interop.JS.MobX", 8, true },
        { "FrontEndCSharp.Overrides.Interop.JS.MobX.React.fecs", "FrontEndCSharp.Overrides.Interop.JS.MobX.React", 2, true },
        { "FrontEndCSharp.Overrides.Interop.JS.Motion.fecs", "FrontEndCSharp.Overrides.Interop.JS.Motion", 8, true },
        { "FrontEndCSharp.Overrides.Interop.JS.React.fecs", "FrontEndCSharp.Overrides.Interop.JS.React", 11, true },
        { "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts.fecs", "FrontEndCSharp.Overrides.Interop.JS.Shadcn.Recharts", 7, true },
        { "FrontEndCSharp.Overrides.FluentValidation.fecs", "FluentValidation", 1, false },
        { "FrontEndCSharp.Overrides.Newtonsoft.Json.fecs", "Newtonsoft.Json", 5, false },
        { "FrontEndCSharp.Overrides.System.Console.fecs", "System.Console", 10, false },
        { "FrontEndCSharp.Overrides.System.Collections.Concurrent.fecs", "System.Collections.Concurrent", 3, false },
        { "FrontEndCSharp.Overrides.System.Collections.fecs", "System.Collections", 8, false },
        { "FrontEndCSharp.Overrides.System.Text.RegularExpressions.fecs", "System.Text.RegularExpressions", 7, false },
        { "FrontEndCSharp.Overrides.System.ComponentModel.Annotations.fecs", "System.ComponentModel.Annotations", 9, false },
        { "FrontEndCSharp.Overrides.System.Linq.Expressions.fecs", "System.Linq.Expressions", 12, false },
        { "FrontEndCSharp.Overrides.System.Linq.fecs", "System.Linq", 3, false },
        { "FrontEndCSharp.Overrides.System.Net.Http.fecs", "System.Net.Http", 77, false },
        { "FrontEndCSharp.Overrides.System.Net.Http.Json.fecs", "System.Net.Http.Json", 4, false },
        { "FrontEndCSharp.Overrides.System.Private.CoreLib.fecs", "System.Private.CoreLib", 635, false },
        { "FrontEndCSharp.Overrides.System.Private.Uri.fecs", "System.Private.Uri", 2, false },
        { "FrontEndCSharp.Overrides.System.Security.Cryptography.fecs", "System.Security.Cryptography", 3, false },
        { "FrontEndCSharp.Overrides.System.Text.Json.fecs", "System.Text.Json", 11, false },
    };

    [TestCaseSource(nameof(Companions))]
    public void CompanionMatchesManagedAssembly(
        string companionAssemblyName,
        string targetAssemblyName,
        int expectedOverrideCount,
        bool targetAssemblyIsProjectReference) =>
        RuntimeOverrideCompanionContract.AssertValid(
            companionAssemblyName,
            targetAssemblyName,
            expectedOverrideCount,
            targetAssemblyIsProjectReference);

    [TestCaseSource(nameof(JavaScriptAdapterVersions))]
    public void JavaScriptAdapterProjectVersionsMatchPrimaryPackage(
        string managedAssemblyName,
        string companionAssemblyName,
        string primaryPackageName,
        string expectedVersion)
    {
        var managedAssembly = System.Reflection.Assembly.Load(managedAssemblyName);
        var companionAssembly = System.Reflection.Assembly.Load(companionAssemblyName);
        var expectedAssemblyVersion = Version.Parse(expectedVersion + ".0");
        var primaryPackage = Assert.Single(
            companionAssembly.GetCustomAttributes<FrontEndCSharp.RuntimeOverrides.JavaScriptPackageDependencyAttribute>(),
            dependency => string.Equals(
                dependency.PackageName,
                primaryPackageName,
                StringComparison.Ordinal));

        Assert.Equal(expectedAssemblyVersion, managedAssembly.GetName().Version);
        Assert.Equal(expectedAssemblyVersion, companionAssembly.GetName().Version);
        Assert.Equal(expectedVersion, primaryPackage.VersionRange);
    }

    [Test]
    public void LucideAdapterExposesCanonicalTypedIconFacades()
    {
        var iconFacades = new[]
        {
            typeof(LucideIcons.ActivityIcon),
            typeof(LucideIcons.CheckIcon),
            typeof(LucideIcons.ChevronDownIcon),
            typeof(LucideIcons.ChevronRightIcon),
            typeof(LucideIcons.ChevronUpIcon),
            typeof(LucideIcons.CloudLightningIcon),
            typeof(LucideIcons.CircleCheckIcon),
            typeof(LucideIcons.CrosshairIcon),
            typeof(LucideIcons.InfoIcon),
            typeof(LucideIcons.LoaderCircleIcon),
            typeof(LucideIcons.EllipsisIcon),
            typeof(LucideIcons.OctagonXIcon),
            typeof(LucideIcons.TriangleExclamationPointIcon),
            typeof(LucideIcons.XIcon),
        };

        Assert.Equal(14, iconFacades.Length);
        Assert.Equal(2032, typeof(LucideIcons).GetNestedTypes(BindingFlags.Public).Length);
        Assert.All(iconFacades, facade =>
        {
            var typedCreate = Assert.Single(
                facade.GetMethods(BindingFlags.Public | BindingFlags.Static),
                method => method.Name == "Create"
                    && method.GetParameters()[0].ParameterType == typeof(Action<LucideIconProps>));
            Assert.Equal(typeof(ReactElement), typedCreate.ReturnType);
            Assert.Equal(typeof(ReactNode[]), typedCreate.GetParameters()[1].ParameterType);
        });

        var props = new LucideIconProps
        {
            ClassName = "size-4",
            AriaHidden = true,
            Color = "currentColor",
            Size = 18,
            StrokeWidth = 1.5,
            AbsoluteStrokeWidth = true,
            DataSlot = "status-icon",
        };
        Assert.Equal("size-4", props.Properties["className"]);
        Assert.Equal(true, props.Properties["aria-hidden"]);
        Assert.Equal("currentColor", props.Properties["color"]);
        Assert.Equal(18d, props.Properties["size"]);
        Assert.Equal(1.5d, props.Properties["strokeWidth"]);
        Assert.Equal(true, props.Properties["absoluteStrokeWidth"]);
        Assert.Equal("status-icon", props.Properties["data-slot"]);

        Assert.DoesNotContain(
            typeof(LucideIcons).Assembly.GetExportedTypes().SelectMany(type => type.GetMethods()),
            method => method.ReturnType == typeof(ImportedReactComponent));
        Assert.DoesNotContain(
            typeof(LucideIcons).Assembly.GetExportedTypes(),
            type => type.Name.Contains("Dynamic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            new[] { "ActivitySquareIcon", "Loader2Icon", "MoreHorizontalIcon", "TriangleAlertIcon" },
            alias => typeof(LucideIcons).GetNestedType(alias, BindingFlags.Public) != null);
    }
}
