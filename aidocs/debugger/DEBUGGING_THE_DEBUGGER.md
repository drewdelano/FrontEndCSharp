# Debugging the FrontEndCSharp Debugger

This document describes the focused C# tools used to debug, diagnose, and verify
the FrontEndCSharp debugger itself. Each command owns its processes, has a finite
timeout, and asserts the smallest observable contract for its layer. A standalone
DAP pass is not presented as proof of Visual Studio composition;
`verify-visual-studio-f5` is the physical composition gate.

## Command ownership

`Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj` owns the
repository's debugger and Visual Studio orchestration:

| Command | Contract |
| --- | --- |
| `verify-vsix-build-modes` | Build the debugger VSIX from clean state, build it again as a no-op, rebuild it, and require verified matching compiler-bundler content in the compiler output, direct VSIX payload, and nested build package. |
| `verify-visual-studio-f5` | Launch the selected Visual Studio hive, preserve startup and breakpoint state, start standard F5, navigate the debugger-owned browser, require a stop at the exact authored C# file and line, capture evidence, restore state, and close owned processes. |
| `debug-adapter-smoke` | Verify synchronous DAP behavior, web-host lifetime, owned/external browser modes, attach-only sessions, foreign CDP pauses, browser crashes, FECSX source mapping, variables, and cleanup. |
| `debug-adapter-async-smoke` | Verify async breakpoint binding, logical stacks, locals, stepping, exceptions, timeouts, and cleanup. |
| `debug-adapter-edit-and-continue-smoke` | Verify live updates, rejection of torn/stale updates, breakpoint rebinding, locals, and post-update stepping. |
| `debugger-adapter-failure` | Require a failed DAP response for missing launch input; process exit with a pending request is a failure. |
| `debugger-performance` | Measure real-browser debugger hot-boundary behavior and assert the recorded result contract. |
| `measure-f5-startup` | Parse Visual Studio/adapter traces, calculate milestones, enforce cold/warm thresholds, and append a JSONL baseline. |
| `install-vsix debugger` | Build, uninstall/install, refresh the hive, and verify the installed debugger registration and payload. |
| `capture-editor-window` | Capture a bounded Visual Studio editor window with visible-content checks and exact process ownership. |

The former general-purpose PowerShell state machine, Pester suite, COM worker,
fault drivers, artifact replay system, and retention command were removed. Their
debugger behavior is covered by the focused DAP/CDP commands above; VSIX behavior
is covered by `install-vsix`; editor evidence is covered by
`capture-editor-window`; trace reporting is covered by `measure-f5-startup`; and
the unique standard-F5 composition requirement is covered by
`verify-visual-studio-f5`. Historical local artifacts are diagnostics, not a
current acceptance contract.

## Physical Visual Studio F5 gate

Run with the target hive closed. The command refuses to attach to an existing
instance unless `--close-existing` is explicitly supplied. That option closes
the selected hive and therefore must not be used when it contains unsaved work.

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  verify-visual-studio-f5 FrontEndCSharp.sln `
  Demo/FrontEndCSharp.ReactDemo/DebuggerSteppingFixture.cs 14 `
  "$env:TEMP/FrontEndCSharp/DebuggerIntegration/VisualStudioF5" `
  --startup-project Demo/FrontEndCSharp.Demo/FrontEndCSharp.Demo.csproj `
  --navigation-url https://localhost:7120/react `
  --accept-project-security-warnings `
  --timeout 300
```

The command:

1. Starts one owned Visual Studio process in the requested hive.
2. Waits for the solution and build system to become callable.
3. Snapshots the startup-project selection and every breakpoint's enabled state.
4. Disables unrelated breakpoints and creates or enables the requested scenario
   breakpoint.
5. Starts standard F5 and navigates only a browser launched with Visual Studio's
   `--remote-debugging-pipe` profile.
6. Requires break mode at the exact active document and authored line.
7. Writes `result.json` and `visual-studio-managed-breakpoint.png`.
8. Stops debugging, removes its breakpoint or restores the existing one, restores
   every breakpoint enabled state and the startup project, and closes its owned
   Visual Studio process.

Project-security warnings are rejected by default. The opt-in switch is appropriate
only for a solution and projects already verified to be inside the trusted
repository checkout.

## Adapter matrix

The synchronous smoke accepts focused mode flags. Run the modes relevant to the
changed ownership boundary; changes to shared adapter/browser lifetime code require
the complete matrix.

```powershell
$browser = 'C:\Program Files\Google\Chrome\Application\chrome.exe'
$project = 'Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj'

dotnet run --project $project -- debug-adapter-smoke $browser
dotnet run --project $project -- debug-adapter-smoke $browser --web-host
dotnet run --project $project -- debug-adapter-smoke $browser --crash-browser
dotnet run --project $project -- debug-adapter-smoke $browser --foreign-pause
dotnet run --project $project -- debug-adapter-smoke $browser --attach-only
dotnet run --project $project -- debug-adapter-smoke $browser --fecsx
dotnet run --project $project -- debug-adapter-async-smoke $browser
dotnet run --project $project -- debug-adapter-edit-and-continue-smoke $browser
dotnet run --project $project -- debugger-adapter-failure `
  "$env:TEMP/FrontEndCSharp/adapter-failure.json"
```

Every browser/process command identifies the process it launched, applies bounded
protocol waits, and terminates only that owned process tree. The FECSX mode requires
authored `.fecsx` source locations rather than generated projection paths.

## Extension mutation

Installing or removing a VSIX changes machine state. Use the dedicated command so
the build, uninstall/install, hive refresh, and installed-payload verification stay
one bounded operation:

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
  install-vsix debugger Exp
```

The installer command verifies the exact hive registration and installed payload;
a successful installer exit alone is insufficient.

## Evidence and privacy

Screenshots and trace files are local diagnostics and may contain source, managed
values, paths, or credentials. Commands never upload them. Inspect them before
sharing or committing them. Process dumps are not part of the current C# workflow.
Temporary output retention is operator-controlled; repository tooling does not
silently delete historical directories.
