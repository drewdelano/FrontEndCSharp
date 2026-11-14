using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FrontEndCSharp.VisualStudio;

internal sealed class VisualStudioBrowserSelectionService
{
    internal const string BrowserDebugOverrideEnvironmentVariable =
        "FRONTENDCSHARP_BROWSER_DEBUG_OVERRIDE";

    private const string AssemblyName = "Microsoft.VisualStudio.Web.Application";
    private const string RegisteredServiceTypeName =
        "Microsoft.VisualStudio.Web.Application.IBrowserDebugTargetSelectionService";
    private const string ServiceTypeName =
        "Microsoft.VisualStudio.Web.Application.IBrowserDebugTargetSelectionService2";
    private readonly object service;
    private readonly MethodInfo getBrowserInformation;
    private readonly MethodInfo selectBrowser;
    private readonly PropertyInfo selectedItem;
    private readonly PropertyInfo webBrowsers;

    private VisualStudioBrowserSelectionService(
        object service,
        MethodInfo getBrowserInformation,
        MethodInfo selectBrowser,
        PropertyInfo selectedItem,
        PropertyInfo webBrowsers)
    {
        this.service = service;
        this.getBrowserInformation = getBrowserInformation;
        this.selectBrowser = selectBrowser;
        this.selectedItem = selectedItem;
        this.webBrowsers = webBrowsers;
    }

    internal static VisualStudioBrowserSelectionService? TryCreate(out string reason)
    {
        try
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => string.Equals(
                    candidate.GetName().Name,
                    AssemblyName,
                    StringComparison.Ordinal));
            if (assembly == null)
            {
                var assemblyPath = Path.Combine(
                    Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                    AssemblyName + ".dll");
                if (!File.Exists(assemblyPath))
                {
                    reason = "live-browser-selection-assembly-not-found";
                    return null;
                }
                assembly = Assembly.LoadFrom(assemblyPath);
            }

            var registeredServiceType = assembly.GetType(
                RegisteredServiceTypeName,
                throwOnError: false);
            var serviceType = assembly.GetType(ServiceTypeName, throwOnError: false);
            var informationType = assembly.GetType(
                "Microsoft.VisualStudio.Web.Application.IBrowserInformation",
                throwOnError: false);
            if (registeredServiceType == null
                || serviceType == null
                || informationType == null)
            {
                reason = "live-browser-selection-types-not-found";
                return null;
            }

            var instance = Package.GetGlobalService(registeredServiceType);
            var getInformation = serviceType.GetMethod("GetBrowserInformation", Type.EmptyTypes);
            var select = serviceType.GetMethod("SelectBrowser", new[] { typeof(string) });
            var selected = informationType.GetProperty("SelectedItem");
            var browsers = informationType.GetProperty("WebBrowsers");
            if (instance == null
                || getInformation == null
                || select == null
                || selected == null
                || browsers == null)
            {
                reason = "live-browser-selection-registered-service-unavailable";
                return null;
            }

            reason = "live-browser-selection-service-found";
            return new VisualStudioBrowserSelectionService(
                instance,
                getInformation,
                select,
                selected,
                browsers);
        }
        catch (Exception exception)
        {
            reason = "live-browser-selection-service-failed:" + DescribeException(exception);
            return null;
        }
    }

    private static string DescribeException(Exception exception)
    {
        var root = exception.GetBaseException();
        var message = root.Message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        return exception.GetType().Name
            + "/"
            + root.GetType().Name
            + ":"
            + message;
    }

    internal bool TryGetBrowserInformation(
        out string? selection,
        out IReadOnlyList<string> browsers,
        out string reason)
    {
        selection = null;
        browsers = Array.Empty<string>();
        try
        {
            var information = getBrowserInformation.Invoke(service, null);
            selection = information == null
                ? null
                : selectedItem.GetValue(information) as string;
            browsers = information == null
                ? Array.Empty<string>()
                : (webBrowsers.GetValue(information) as IEnumerable<string>)?.ToArray()
                    ?? Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(selection))
            {
                reason = "live-browser-selection-item-unavailable";
                return false;
            }

            reason = "live-browser-selection-item:" + selection;
            return true;
        }
        catch (Exception exception)
        {
            reason = "live-browser-selection-read-failed:" + exception.GetType().Name;
            return false;
        }
    }

    internal bool TrySelectBrowser(string selection, out string reason)
    {
        try
        {
            selectBrowser.Invoke(service, new object[] { selection });
            reason = "live-browser-selection-updated:" + selection;
            return true;
        }
        catch (Exception exception)
        {
            reason = "live-browser-selection-update-failed:" + exception.GetType().Name;
            return false;
        }
    }
}
