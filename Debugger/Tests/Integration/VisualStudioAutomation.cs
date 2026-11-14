using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Interop.UIAutomationClient;

namespace FrontEndCSharp.Debugger.Integration;

[SupportedOSPlatform("windows")]
internal static class VisualStudioAutomation
{
    public static string FindDevenv()
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        var installation = BoundedProcess.Run(
            vswhere,
            RepositoryLayout.Root,
            TimeSpan.FromSeconds(30),
            "-latest",
            "-products",
            "*",
            "-property",
            "installationPath").StandardOutput.Trim();
        var devenv = Path.Combine(installation, "Common7", "IDE", "devenv.exe");
        return File.Exists(devenv)
            ? devenv
            : throw new FileNotFoundException("devenv.exe was not found.", devenv);
    }

    public static string FindMSBuild()
    {
        var devenv = FindDevenv();
        var installation = Directory.GetParent(
            Directory.GetParent(
                Directory.GetParent(devenv)!.FullName)!.FullName)!.FullName;
        var msBuild = Path.Combine(
            installation,
            "MSBuild",
            "Current",
            "Bin",
            "MSBuild.exe");
        return File.Exists(msBuild)
            ? msBuild
            : throw new FileNotFoundException("MSBuild.exe was not found.", msBuild);
    }

    public static IReadOnlyList<int> GetHiveProcessIds(string rootSuffix)
    {
        var result = new List<int>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'devenv.exe'");
        foreach (var item in searcher.Get().Cast<ManagementObject>())
        {
            var commandLine = item["CommandLine"] as string;
            if (commandLine != null
                && Regex.IsMatch(
                    commandLine,
                    @"(?i)/RootSuffix\s+" + Regex.Escape(rootSuffix) + @"(?:\s|$)"))
            {
                result.Add(Convert.ToInt32(
                    item["ProcessId"],
                    System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        return result;
    }

    public static void CloseProcesses(IEnumerable<int> processIds)
    {
        var processes = processIds
            .Distinct()
            .Select(TryGetProcess)
            .Where(process => process != null)
            .Cast<Process>()
            .ToArray();
        foreach (var process in processes)
        {
            process.CloseMainWindow();
        }
        Thread.Sleep(TimeSpan.FromSeconds(3));
        foreach (var process in processes)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            process.WaitForExit();
            process.Dispose();
        }
    }

    public static dynamic WaitForDte(
        Process process,
        DateTime deadline,
        bool? acceptProjectSecurityWarnings = null)
    {
        while (DateTime.UtcNow < deadline)
        {
            ResolveSecurityWarning(process, acceptProjectSecurityWarnings);
            var dte = TryGetDte(process.Id);
            if (dte != null)
            {
                return dte;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException(
            $"Visual Studio did not publish its DTE object for process {process.Id}.");
    }

    public static void WaitForSolution(
        dynamic dte,
        Process process,
        DateTime deadline,
        bool? acceptProjectSecurityWarnings = null)
    {
        while (DateTime.UtcNow < deadline)
        {
            ResolveSecurityWarning(process, acceptProjectSecurityWarnings);
            try
            {
                if (dte.Solution.IsOpen)
                {
                    return;
                }
            }
            catch (COMException)
            {
            }
            catch (Exception exception) when (IsTransientComFailure(exception))
            {
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException("Visual Studio did not open the requested solution.");
    }

    public static void WaitForDocument(dynamic dte, string documentPath, DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            string? activePath = null;
            try
            {
                activePath = dte.ActiveDocument.FullName;
            }
            catch (COMException)
            {
            }
            catch (Exception exception) when (IsTransientComFailure(exception))
            {
            }
            if (string.Equals(activePath, documentPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Thread.Sleep(250);
        }
        throw new TimeoutException($"Visual Studio did not activate '{documentPath}'.");
    }

    public static VisualStudioAutomationElement? FindElement(
        Process process,
        string name,
        VisualStudioControlType controlType)
    {
        var automation = CreateUiAutomation();
        var condition = automation.CreateAndConditionFromArray(new[]
        {
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ProcessIdPropertyId, process.Id),
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_NamePropertyId, name),
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, (int)controlType),
        });
        var element = automation.GetRootElement().FindFirst(
            TreeScope.TreeScope_Descendants,
            condition);
        return element == null ? null : new VisualStudioAutomationElement(element);
    }

    public static VisualStudioAutomationElement? FindElementByAutomationId(
        Process process,
        string automationId,
        VisualStudioControlType controlType)
    {
        var automation = CreateUiAutomation();
        var condition = automation.CreateAndConditionFromArray(new[]
        {
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ProcessIdPropertyId, process.Id),
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_AutomationIdPropertyId, automationId),
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, (int)controlType),
        });
        var element = automation.GetRootElement().FindFirst(
            TreeScope.TreeScope_Descendants,
            condition);
        return element == null ? null : new VisualStudioAutomationElement(element);
    }

    public static IReadOnlyList<VisualStudioAutomationElement> FindElements(
        Process process,
        VisualStudioControlType controlType)
    {
        var automation = CreateUiAutomation();
        var condition = automation.CreateAndConditionFromArray(new[]
        {
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ProcessIdPropertyId, process.Id),
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ControlTypePropertyId, (int)controlType),
        });
        var elements = automation.GetRootElement().FindAll(
            TreeScope.TreeScope_Descendants,
            condition);
        var result = new List<VisualStudioAutomationElement>();
        for (var index = 0; index < elements.Length; index++)
        {
            result.Add(new VisualStudioAutomationElement(elements.GetElement(index)));
        }
        return result;
    }

    public static VisualStudioAutomationElement? FindEditWithValue(
        Process process,
        string value)
    {
        var automation = CreateUiAutomation();
        var condition = automation.CreateAndConditionFromArray(new[]
        {
            automation.CreatePropertyCondition(UIA_PropertyIds.UIA_ProcessIdPropertyId, process.Id),
            automation.CreatePropertyCondition(
                UIA_PropertyIds.UIA_ControlTypePropertyId,
                (int)VisualStudioControlType.Edit),
        });
        var edits = automation.GetRootElement().FindAll(
            TreeScope.TreeScope_Descendants,
            condition);
        for (var index = 0; index < edits.Length; index++)
        {
            var edit = edits.GetElement(index);
            try
            {
                var pattern = edit.GetCurrentPattern(
                    UIA_PatternIds.UIA_ValuePatternId)
                    as IUIAutomationValuePattern;
                if (pattern != null && pattern.CurrentValue == value)
                {
                    return new VisualStudioAutomationElement(edit);
                }
            }
            catch (COMException)
            {
            }
        }
        return null;
    }

    public static void Invoke(VisualStudioAutomationElement element) =>
        element.Invoke();

    public static void SetValue(
        VisualStudioAutomationElement element,
        string value) => element.SetValue(value);

    public static void Select(VisualStudioAutomationElement element) =>
        element.Select();

    public static void SetChecked(
        VisualStudioAutomationElement element,
        bool isChecked) => element.SetChecked(isChecked);

    public static void SendEscape() => SendVirtualKey(VirtualKeyEscape);

    public static void CommitSelectedCompletion() => SendVirtualKey(VirtualKeyReturn);

    public static void SendEnter() => SendVirtualKey(VirtualKeyReturn);

    public static void SendBackspace() => SendVirtualKey(VirtualKeyBackspace);

    public static void SendText(string text)
    {
        foreach (var character in text)
        {
            SendUnicodeCharacter(character);
        }
    }

    public static void BringToForeground(Process process) =>
        SetForegroundWindow(process.MainWindowHandle);

    public static void SaveWindowScreenshot(Process process, string path)
    {
        if (IsIconic(process.MainWindowHandle))
        {
            throw new InvalidOperationException("The Visual Studio window is minimized.");
        }
        if (!GetWindowRect(process.MainWindowHandle, out var rectangle))
        {
            throw new InvalidOperationException(
                "Could not read the Visual Studio window bounds.");
        }
        var width = rectangle.Right - rectangle.Left;
        var height = rectangle.Bottom - rectangle.Top;
        if (width < 400 || height < 300)
        {
            throw new InvalidOperationException(
                $"The Visual Studio window is too small to capture ({width} x {height}).");
        }
        using var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                rectangle.Left,
                rectangle.Top,
                0,
                0,
                bitmap.Size);
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    public static VisualStudioAutomationElement? FindProjectSecurityWarning(
        Process process) => FindElements(process, VisualStudioControlType.Window)
        .FirstOrDefault(element => element.Name.StartsWith(
            "Security Warning for ",
            StringComparison.Ordinal));

    public static void ResolveProjectSecurityWarning(
        Process process,
        bool acceptWarning)
    {
        var warning = FindProjectSecurityWarning(process);
        if (warning == null)
        {
            return;
        }
        if (!acceptWarning)
        {
            throw new InvalidOperationException(
                $"Visual Studio is waiting on '{warning.Name}'. Re-run with "
                + "--accept-project-security-warnings only for a repository you trust.");
        }
        ClearApplyToEveryProject(process);
        var open = FindElementByAutomationId(
            process,
            "1",
            VisualStudioControlType.Button);
        if (open == null || open.Name != "Open")
        {
            throw new InvalidOperationException(
                $"'{warning.Name}' did not expose the exact Open control.");
        }
        Invoke(open);
        Thread.Sleep(350);
    }

    private static void ResolveSecurityWarning(
        Process process,
        bool? acceptProjectSecurityWarnings)
    {
        if (acceptProjectSecurityWarnings.HasValue)
        {
            ResolveProjectSecurityWarning(
                process,
                acceptProjectSecurityWarnings.Value);
        }
        else
        {
            ResolveProjectSecurityWarning(process);
        }
    }

    private static void ResolveProjectSecurityWarning(Process process)
    {
        while (true)
        {
            var warning = FindElement(
                process,
                "Security Warning for FecsxRenameFixture",
                VisualStudioControlType.Window);
            if (warning == null)
            {
                return;
            }
            ClearApplyToEveryProject(process);
            var open = FindElement(process, "Open", VisualStudioControlType.Button)
                ?? throw new InvalidOperationException(
                    "The project security warning did not expose its Open button.");
            Invoke(open);
            Thread.Sleep(350);
        }
    }

    private static void ClearApplyToEveryProject(Process process)
    {
        var applyToEveryProject = FindElement(
            process,
            "Do this for every project in the current solution",
            VisualStudioControlType.CheckBox);
        applyToEveryProject?.SetChecked(false);
    }

    private static void SendVirtualKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            new NativeInput
            {
                Type = InputKeyboard,
                Value = new NativeInputUnion
                {
                    Keyboard = new NativeKeyboardInput { VirtualKey = virtualKey },
                },
            },
            new NativeInput
            {
                Type = InputKeyboard,
                Value = new NativeInputUnion
                {
                    Keyboard = new NativeKeyboardInput
                    {
                        VirtualKey = virtualKey,
                        Flags = KeyEventKeyUp,
                    },
                },
            },
        };
        var sent = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeInput>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                "SendInput could not send the requested key. Win32 error: "
                + Marshal.GetLastWin32Error());
        }
    }

    private static void SendUnicodeCharacter(char character)
    {
        var inputs = new[]
        {
            new NativeInput
            {
                Type = InputKeyboard,
                Value = new NativeInputUnion
                {
                    Keyboard = new NativeKeyboardInput
                    {
                        ScanCode = character,
                        Flags = KeyEventUnicode,
                    },
                },
            },
            new NativeInput
            {
                Type = InputKeyboard,
                Value = new NativeInputUnion
                {
                    Keyboard = new NativeKeyboardInput
                    {
                        ScanCode = character,
                        Flags = KeyEventUnicode | KeyEventKeyUp,
                    },
                },
            },
        };
        var sent = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeInput>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException(
                "SendInput could not type the requested character. Win32 error: "
                + Marshal.GetLastWin32Error());
        }
    }

    private static IUIAutomation CreateUiAutomation() => new CUIAutomation();

    private static object? TryGetDte(int processId)
    {
        IRunningObjectTable? table = null;
        IEnumMoniker? enumerator = null;
        try
        {
            Marshal.ThrowExceptionForHR(GetRunningObjectTable(0, out table));
            table.EnumRunning(out enumerator);
            var monikers = new IMoniker[1];
            while (enumerator.Next(1, monikers, IntPtr.Zero) == 0)
            {
                CreateBindCtx(0, out var context);
                try
                {
                    monikers[0].GetDisplayName(context, null, out var name);
                    if (name.Contains("VisualStudio.DTE", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(":" + processId, StringComparison.Ordinal))
                    {
                        table.GetObject(monikers[0], out var instance);
                        return instance;
                    }
                }
                finally
                {
                    Marshal.FinalReleaseComObject(context);
                }
            }
            return null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (enumerator != null)
            {
                Marshal.FinalReleaseComObject(enumerator);
            }
            if (table != null)
            {
                Marshal.FinalReleaseComObject(table);
            }
        }
    }

    private static Process? TryGetProcess(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static bool IsTransientComFailure(Exception exception)
    {
        const int retryLater = unchecked((int)0x8001010A);
        const int callRejected = unchecked((int)0x80010001);
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.HResult == retryLater
                || current.HResult == callRejected
                || current.Message.Contains(
                    "0x8001010A",
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains(
                    "0x80010001",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(
        int reserved,
        out IRunningObjectTable table);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx context);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        IntPtr window,
        out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        NativeInput[] inputs,
        int inputSize);

    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyBackspace = 0x08;
    private const ushort VirtualKeyReturn = 0x0D;
    private const ushort VirtualKeyEscape = 0x1B;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeHardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)] public NativeMouseInput Mouse;
        [FieldOffset(0)] public NativeKeyboardInput Keyboard;
        [FieldOffset(0)] public NativeHardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public NativeInputUnion Value;
    }
}

internal enum VisualStudioControlType
{
    Button = 50000,
    CheckBox = 50002,
    ComboBox = 50003,
    Edit = 50004,
    ListItem = 50007,
    List = 50008,
    MenuItem = 50011,
    Text = 50020,
    Custom = 50025,
    Pane = 50033,
    Window = 50032,
}

[SupportedOSPlatform("windows")]
internal sealed class VisualStudioAutomationElement
{
    private readonly IUIAutomationElement _element;

    public VisualStudioAutomationElement(IUIAutomationElement element) =>
        _element = element;

    public string Name => _element.CurrentName;
    public string AutomationId => _element.CurrentAutomationId;
    public bool IsOffscreen => _element.CurrentIsOffscreen != 0;
    public bool IsEnabled => _element.CurrentIsEnabled != 0;

    public void Invoke()
    {
        var pattern = (IUIAutomationInvokePattern)_element.GetCurrentPattern(
            UIA_PatternIds.UIA_InvokePatternId);
        pattern.Invoke();
    }

    public void SetValue(string value)
    {
        var pattern = (IUIAutomationValuePattern)_element.GetCurrentPattern(
            UIA_PatternIds.UIA_ValuePatternId);
        pattern.SetValue(value);
    }

    public void Select()
    {
        var pattern = (IUIAutomationSelectionItemPattern)_element.GetCurrentPattern(
            UIA_PatternIds.UIA_SelectionItemPatternId);
        pattern.Select();
    }

    public void SetChecked(bool isChecked)
    {
        var pattern = (IUIAutomationTogglePattern)_element.GetCurrentPattern(
            UIA_PatternIds.UIA_TogglePatternId);
        var currentlyChecked = (int)pattern.CurrentToggleState != 0;
        if (currentlyChecked != isChecked)
        {
            pattern.Toggle();
        }
    }
}
