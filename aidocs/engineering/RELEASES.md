# GitHub VSIX Releases

This guide is the repeatable publication process for the repository's Visual
Studio extensions. The debugger and FECSX editor have independent embedded
versions and therefore use separate GitHub releases and tags.

| Extension | Version source | Tag pattern | Release artifact |
| --- | --- | --- | --- |
| FrontEndCSharp Debugger | `Debugger/VisualStudio/source.extension.vsixmanifest` | `debugger-v<version>` | `FrontEndCSharp.Debugger-<version>.vsix` |
| FrontEndCSharp FECSX Editor | `Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX/source.extension.vsixmanifest` | `fecsx-v<version>` | `FrontEndCSharp.FECSX-Editor-<version>.vsix` |

Do not force both extensions into one version or one release. Read the `Identity`
version from each source manifest and make the tag, title, asset name, and release
notes agree with that embedded version.

Every distinct VSIX payload handed off for installation must have a new,
monotonically increasing `Identity` version. Do not rebuild changed extension
bytes under an existing version: Visual Studio's Extension Manager otherwise
cannot distinguish the installed payload from an older build. This applies to
local installation handoffs as well as published GitHub releases.

## Current publication boundary

- The repository is private, so its releases are available only to users who can
  access the repository.
- The VSIX files are currently unsigned. Publish them as prereleases and state that
  Visual Studio may show an unverified-publisher warning. If signing is introduced,
  make signature verification an explicit gate and update this guide.
- GitHub authentication must have repository write access. Check `gh auth status`
  and `gh repo view --json viewerPermission,isPrivate` without exposing tokens.
- Never publish an existing Debug build or a VSIX produced before the current source
  commit. Generated `bin` output is local evidence, not a source artifact.

## Prepare the commit

Start from a clean worktree. Run the required tests and package-specific gates,
commit the release-ready source, and push it before creating tags or releases:

```powershell
git status --short
dotnet test Compiler/Compiler.Tests/Compiler.Tests.csproj
dotnet test Debugger/Tests/Debugger.Tests.csproj
git push origin master
git rev-list --left-right --count origin/master...HEAD
```

The final command must report `0 0`. Do not create a release tag that points at an
unpushed or different commit.

## Build and verify Release artifacts

The debugger gate cleans Release output, then verifies fresh, no-op, and rebuild
packaging. It checks `JsBundler/index.cjs` in compiler output, the debugger's direct
compiler payload, and the compiler payload nested inside `FrontEndCSharp.Build`.

```powershell
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-vsix-build-modes .

dotnet build Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX/FrontEndCSharp.Fecsx.VSIX.csproj `
    --configuration Release --no-restore --target:Rebuild
```

The resulting source artifacts are:

```text
Debugger/VisualStudio/bin/Release/net472/FrontEndCSharp.VisualStudio.vsix
Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX/bin/Release/net472/FrontEndCSharp.Fecsx.VSIX.vsix
```

Copy them to a temporary staging directory with versioned public filenames. Compute
SHA-256 after the copy and include each digest in its release notes:

```powershell
$releaseStaging = Join-Path $env:TEMP 'FrontEndCSharp-GitHub-Releases'
New-Item -ItemType Directory -Force -Path $releaseStaging | Out-Null

Copy-Item Debugger/VisualStudio/bin/Release/net472/FrontEndCSharp.VisualStudio.vsix `
    (Join-Path $releaseStaging 'FrontEndCSharp.Debugger-<version>.vsix')
Copy-Item Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX/bin/Release/net472/FrontEndCSharp.Fecsx.VSIX.vsix `
    (Join-Path $releaseStaging 'FrontEndCSharp.FECSX-Editor-<version>.vsix')

Get-FileHash (Join-Path $releaseStaging '*.vsix') -Algorithm SHA256
```

Use a new or commit-specific staging directory when an existing file could be
mistaken for the current build. Do not overwrite an already published tag or asset
to disguise changed bytes; increment the embedded version instead.

## Create the prereleases

Confirm that each intended tag is unused with `gh release view <tag>`. Then create
one prerelease per extension, targeting the already-pushed `master` commit:

```powershell
gh release create debugger-v<version> `
    "$releaseStaging/FrontEndCSharp.Debugger-<version>.vsix" `
    --repo drewdelano/FrontEndCSharp --target master --prerelease `
    --title "FrontEndCSharp Debugger <version>" `
    --notes "<summary, SHA-256, and unsigned-package warning>"

gh release create fecsx-v<version> `
    "$releaseStaging/FrontEndCSharp.FECSX-Editor-<version>.vsix" `
    --repo drewdelano/FrontEndCSharp --target master --prerelease `
    --title "FrontEndCSharp FECSX Editor <version>" `
    --notes "<summary, SHA-256, and unsigned-package warning>"
```

If one release fails, inspect it before retrying. Do not blindly rerun both commands
after one has succeeded.

## Verify GitHub, not only the upload command

Read both releases back from GitHub and compare the remote asset name, byte count,
and `sha256:` digest with the staged file. Also confirm `isPrerelease`, tag, title,
and target commit:

```powershell
gh release view debugger-v<version> --repo drewdelano/FrontEndCSharp `
    --json tagName,name,url,isPrerelease,targetCommitish,assets
gh release view fecsx-v<version> --repo drewdelano/FrontEndCSharp `
    --json tagName,name,url,isPrerelease,targetCommitish,assets
```

Publication is complete only after GitHub reports each asset as uploaded with the
expected size and digest. Return the two release links, hashes, test status, and any
unsigned/private-repository caveats to the user.
