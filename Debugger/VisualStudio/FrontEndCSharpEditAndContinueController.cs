using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FrontEndCSharp.VisualStudio;

internal sealed class FrontEndCSharpEditAndContinueController : IDisposable
{
    private const string ManifestSuffix = ".frontendcsharp.debug.json";

    private readonly FrontEndCSharpPackage package;
    private readonly DTE2 dte;
    private readonly VisualStudioDebuggerOutput output;
    private EditAndContinueClient? client;
    private string? compilerPath;
    private string? projectPath;
    private string? generatedOutputPath;
    private string? currentManifestPath;
    private string? sessionDirectory;
    private string? pipe;
    private string? token;

    internal FrontEndCSharpEditAndContinueController(
        FrontEndCSharpPackage package,
        DTE2 dte,
        VisualStudioDebuggerOutput output)
    {
        this.package = package;
        this.dte = dte;
        this.output = output;
    }

    public void Dispose() => Reset();

    internal void Reset()
    {
        client?.Dispose();
        client = null;
        compilerPath = null;
        projectPath = null;
        generatedOutputPath = null;
        currentManifestPath = null;
        sessionDirectory = null;
        pipe = null;
        token = null;
    }

    internal void Start(
        EditAndContinueClient client,
        string compilerPath,
        string projectPath,
        string generatedOutputPath,
        string currentManifestPath,
        string sessionDirectory,
        string pipe,
        string token)
    {
        Reset();
        this.client = client;
        this.compilerPath = compilerPath;
        this.projectPath = projectPath;
        this.generatedOutputPath = generatedOutputPath;
        this.currentManifestPath = currentManifestPath;
        this.sessionDirectory = sessionDirectory;
        this.pipe = pipe;
        this.token = token;
    }

    internal async Task ApplyAsync()
    {
        EditHostResponse? prepared = null;
        var runtimeApplied = false;
        try
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (dte.Debugger.CurrentMode != dbgDebugMode.dbgBreakMode)
            {
                throw new InvalidOperationException(
                    "Apply Code Changes is available only while stopped in a FrontEndCSharp debug session.");
            }
            if (client == null
                || compilerPath == null
                || projectPath == null
                || generatedOutputPath == null
                || currentManifestPath == null
                || sessionDirectory == null
                || pipe == null
                || token == null)
            {
                throw new InvalidOperationException(
                    "Start this session with Debug with FrontEndCSharp before applying code changes.");
            }

            output.WriteLine("Preparing Roslyn Edit and Continue deltas...");
            prepared = await client.RequestAsync("prepare", generation: 0);
            if (!prepared.Success)
            {
                throw new InvalidOperationException(prepared.Error);
            }
            var candidateOutput = Path.Combine(
                sessionDirectory,
                $"generation-{prepared.Generation}",
                "candidate-js");
            var compile = await DebuggerProcessRunner.RunAsync(
                compilerPath,
                new[] { projectPath, candidateOutput, prepared.AssemblyPath },
                Path.GetDirectoryName(projectPath)!);
            output.Write(compile);
            DebuggerProcessRunner.EnsureSuccess("translate the updated compilation", compile);

            var candidateManifest = Path.Combine(
                Path.GetDirectoryName(prepared.AssemblyPath)!,
                Path.GetFileNameWithoutExtension(prepared.AssemblyPath) + ManifestSuffix);
            var updateManifest = Path.Combine(
                Path.GetDirectoryName(prepared.AssemblyPath)!,
                "update" + ManifestSuffix);
            var translation = await DebuggerProcessRunner.RunAsync(
                compilerPath,
                new[]
                {
                    "--create-debug-update",
                    currentManifestPath,
                    candidateManifest,
                    generatedOutputPath,
                    candidateOutput,
                    prepared.Generation.ToString(CultureInfo.InvariantCulture),
                    string.Join(",", prepared.UpdatedMethodTokens.Select(value =>
                        value.ToString(CultureInfo.InvariantCulture))),
                    updateManifest
                },
                Path.GetDirectoryName(projectPath)!);
            output.Write(translation);
            DebuggerProcessRunner.EnsureSuccess("create the atomic debugger update", translation);
            var translationJson = translation.StandardOutput
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Last();
            var update = EditAndContinueClient.Deserialize<UpdateTranslationResponse>(translationJson);
            var applied = await EditAndContinueClient.ApplyAsync(
                pipe,
                token,
                update.UpdateModulePath,
                update.DebugManifestPath);
            if (!applied.Success)
            {
                throw new InvalidOperationException(applied.Error);
            }
            runtimeApplied = true;
            var committed = await client.RequestAsync("commit", prepared.Generation);
            if (!committed.Success)
            {
                throw new InvalidOperationException(
                    "The runtime accepted the update, but the Roslyn baseline could not be committed: "
                    + committed.Error);
            }
            currentManifestPath = update.DebugManifestPath;
            output.WriteLine(
                $"Applied code changes as generation {applied.Generation} ({applied.Hash}).");
        }
        catch (Exception exception)
        {
            if (prepared?.Success == true && client !=  null)
            {
                try
                {
                    await client.RequestAsync("discard", prepared.Generation);
                }
                catch (Exception discardException)
                {
                    output.WriteLine(
                        $"WARNING: Could not discard the prepared Roslyn update: {discardException.Message}");
                }
            }
            output.WriteLine($"EDIT AND CONTINUE ERROR: {exception.Message}");
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (runtimeApplied && dte.Debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
            {
                output.WriteLine(
                    "The browser accepted the update but the Roslyn baseline did not commit. Stopping the session to avoid divergent generations.");
                dte.Debugger.Stop(WaitForDesignMode: true);
            }
            VsShellUtilities.ShowMessageBox(
                package,
                exception.Message,
                "FrontEndCSharp Edit and Continue",
                OLEMSGICON.OLEMSGICON_WARNING,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }
    }
}
