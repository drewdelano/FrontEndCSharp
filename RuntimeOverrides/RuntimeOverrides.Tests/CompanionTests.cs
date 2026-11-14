using FrontEndCSharp.RuntimeOverrides.Tests;
using Interop.JS.LucideIcons;
using Interop.JS.React;
using System.Reflection;

namespace FrontEndCSharp.RuntimeOverrides.ContractTests;

public sealed class CompanionTests
{
    public static TestCaseDataCollection<string, string, string, string> JavaScriptAdapterVersions => new()
    {
        { "Interop.JS.Shadcn.BaseUI", "Interop.JS.Shadcn.BaseUI.fecs", "@base-ui/react", "1.9.0" },
        { "Interop.JS.Shadcn.Cn", "Interop.JS.Shadcn.Cn.fecs", "cn", "0.5.2" },
        { "Interop.JS.LucideIcons", "Interop.JS.LucideIcons.fecs", "lucide-react", "1.72.0" },
        { "Interop.JS.MobX", "Interop.JS.MobX.fecs", "mobx", "7.0.6" },
        { "Interop.JS.MobX.React", "Interop.JS.MobX.React.fecs", "mobx-react-lite", "5.1.0" },
        { "Interop.JS.Motion", "Interop.JS.Motion.fecs", "motion", "14.4.0" },
        { "Interop.JS.React", "Interop.JS.React.fecs", "react", "19.3.1" },
        { "Interop.JS.Shadcn.Recharts", "Interop.JS.Shadcn.Recharts.fecs", "recharts", "3.8.0" },
    };

    public static TestCaseDataCollection<string, string, int, bool> Companions => new()
    {
        { "Interop.JS.Shadcn.BaseUI.fecs", "Interop.JS.Shadcn.BaseUI", 96, true },
        { "Interop.JS.Browser.fecs", "Interop.JS.Browser", 66, true },
        { "Interop.JS.Shadcn.Cn.fecs", "Interop.JS.Shadcn.Cn", 1, true },
        { "Interop.JS.LucideIcons.fecs", "Interop.JS.LucideIcons", 2032, true },
        { "Interop.JS.MobX.fecs", "Interop.JS.MobX", 8, true },
        { "Interop.JS.MobX.React.fecs", "Interop.JS.MobX.React", 2, true },
        { "Interop.JS.Motion.fecs", "Interop.JS.Motion", 8, true },
        { "Interop.JS.React.fecs", "Interop.JS.React", 11, true },
        { "Interop.JS.Shadcn.Recharts.fecs", "Interop.JS.Shadcn.Recharts", 7, true },
        { "FluentValidation.fecs", "FluentValidation", 1, false },
        { "Newtonsoft.Json.fecs", "Newtonsoft.Json", 5, false },
        { "System.Console.fecs", "System.Console", 10, false },
        { "System.Collections.Concurrent.fecs", "System.Collections.Concurrent", 3, false },
        { "System.Collections.fecs", "System.Collections", 8, false },
        { "System.Text.RegularExpressions.fecs", "System.Text.RegularExpressions", 7, false },
        { "System.ComponentModel.Annotations.fecs", "System.ComponentModel.Annotations", 9, false },
        { "System.Linq.Expressions.fecs", "System.Linq.Expressions", 12, false },
        { "System.Linq.fecs", "System.Linq", 3, false },
        { "System.Net.Http.fecs", "System.Net.Http", 77, false },
        { "System.Net.Http.Json.fecs", "System.Net.Http.Json", 4, false },
        { "System.Private.CoreLib.fecs", "System.Private.CoreLib", 635, false },
        { "System.Private.Uri.fecs", "System.Private.Uri", 2, false },
        { "System.Security.Cryptography.fecs", "System.Security.Cryptography", 3, false },
        { "System.Text.Json.fecs", "System.Text.Json", 11, false },
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
