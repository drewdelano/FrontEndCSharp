# Compiler performance workspace

This directory owns the generated evidence for the compiler performance harness.
The harness writes its generated application to `fixture`, compiled browser output
to `output`, and per-run JSON evidence to `reports`. Local CPU traces and preserved
A/B compiler payloads also belong here when a performance investigation needs them.

Generated contents are intentionally ignored. Stable conclusions, reproduction
commands, graph metrics, artifact hashes, and accepted medians remain documented in
[`aidocs/engineering/PERFORMANCE.md`](../../../aidocs/engineering/PERFORMANCE.md).

Run the 256-page benchmark from the repository root with:

```powershell
dotnet run --project Compiler/Compiler.Tests/Integration/FrontEndCSharp.Compiler.Integration.csproj `
    --configuration Release -- compiler-performance --pages 256 --samples 5 `
    --label current
```

Pass `--workspace <path>` only when a comparison requires an isolated workspace.
