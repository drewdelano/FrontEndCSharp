# Release Artifact Packaging and Publication

This guide is the repeatable staging and publication process for the repository's
NuGet packages and Visual Studio extensions. Packaging creates local evidence;
publishing is a separate operation that requires an explicit destination and
credentials.

## Timestamped artifact staging

Put each complete packaging run beneath a new repository-root directory named
with the local 24-hour time, `artifacts/yyyy-MM-dd-HH-mm`. Keep artifact families
separate so package counts and upload scopes remain auditable:

```text
artifacts/<timestamp>/
  nuget/
    overrides/
    internal/   # only when explicitly requested; publication destination is unresolved
  vsix/
```

The root `artifacts/` directory is gitignored. Never reuse or partially overwrite
an earlier timestamped run. After the clean-commit gates below pass, create a fresh
root and package all override projects from `Overrides.sln`:

```powershell
$artifactStamp = Get-Date -Format 'yyyy-MM-dd-HH-mm'
$artifactRoot = Join-Path (Get-Location) "artifacts\$artifactStamp"
$overrideStaging = Join-Path $artifactRoot 'nuget\overrides'
$vsixStaging = Join-Path $artifactRoot 'vsix'

if (Test-Path -LiteralPath $artifactRoot) {
    throw "Artifact root already exists: $artifactRoot"
}

New-Item -ItemType Directory -Path $overrideStaging, $vsixStaging | Out-Null
dotnet pack Overrides/Overrides.sln --configuration Release `
    --output $overrideStaging --verbosity minimal /nodeReuse:false

$overridePackages = Get-ChildItem -LiteralPath $overrideStaging -Filter '*.nupkg'
if ($overridePackages.Count -ne 34) {
    throw "Expected 34 override/runtime packages, found $($overridePackages.Count)."
}
dotnet run --project Debugger/Tests/Integration/FrontEndCSharp.Debugger.Integration.csproj -- `
    verify-nuget-package-metadata $overrideStaging 34
$overridePackages | Get-FileHash -Algorithm SHA256
```

Use `Debug` only for an explicitly requested local handoff. Anything intended for
upload must be rebuilt in `Release` from the source commit being published. The
34-package set includes `FrontEndCSharp.RuntimeOverrides`; test projects are
non-packable and are skipped. Every package must carry a non-placeholder
description, authors, tags, the repository MIT license expression, project and
repository URLs, and a packaged readme. Treat any missing-metadata advisory as a
failed release gate rather than publishing an incomplete permanent version.

The four standalone packages `FrontEndCSharp.FECS.Internals.Build`,
`FrontEndCSharp.FECS.Internals.Fecsx.Build`,
`FrontEndCSharp.FECS.Internals.Tailwind`, and
`FrontEndCSharp.FECS.Internals.Fecscn.Tool` are not part of the override batch.
Their shared internal namespace is agreed, but their publication destination is
unresolved. Do not publish them as part of the routine override workflow; when a
local package build is explicitly requested, stage all four under
`nuget/internal`. The remaining publication decision is tracked in
[`DISCUSSION.md`](../DISCUSSION.md).

## Visual Studio extensions

The debugger and FECSX editor have independent embedded versions and therefore
use separate GitHub releases and tags.

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
- The 34 Override packages publish to nuget.org through the manually dispatched
  `.github/workflows/publish-nuget.yml` workflow and NuGet Trusted Publishing.
  The workflow accepts only `master`, requires explicit confirmation, validates
  the complete package set before requesting its short-lived credential, and does
  not use a stored NuGet API key.
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
compiler payload, and the compiler payload nested inside
`FrontEndCSharp.FECS.Internals.Build`.

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

Copy them to the current timestamped `vsix` staging directory with versioned public
filenames. Compute SHA-256 after the copy and include each digest in its release
notes:

```powershell
Copy-Item Debugger/VisualStudio/bin/Release/net472/FrontEndCSharp.VisualStudio.vsix `
    (Join-Path $vsixStaging 'FrontEndCSharp.Debugger-<version>.vsix')
Copy-Item Compiler/Fecsx/FrontEndCSharp.Fecsx.VSIX/bin/Release/net472/FrontEndCSharp.Fecsx.VSIX.vsix `
    (Join-Path $vsixStaging 'FrontEndCSharp.FECSX-Editor-<version>.vsix')

Get-FileHash (Join-Path $vsixStaging '*.vsix') -Algorithm SHA256
```

Use a new or commit-specific staging directory when an existing file could be
mistaken for the current build. Do not overwrite an already published tag or asset
to disguise changed bytes; increment the embedded version instead.

## Publish the override packages

Configure the nuget.org Trusted Publishing policy with these exact GitHub values:

| Policy field | Value |
| --- | --- |
| Repository Owner | `drewdelano` |
| Repository | `FrontEndCSharp.Private` |
| Workflow File | `publish-nuget.yml` |
| Environment | leave empty |

The workflow uses the `drewdelano` nuget.org profile name and the official
[`NuGet/login`](https://github.com/NuGet/login) action following the
[NuGet Trusted Publishing guidance](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing).
NuGet issues a temporary API key from the workflow's GitHub OIDC identity; do not
add a long-lived NuGet API key to repository secrets.

Push the release-ready commit to `master`, then open **Actions → Publish override
NuGet packages → Run workflow**, select `master`, and enable `confirm_publish`.
The workflow rebuilds and validates the exact 34-package Override set, verifies
that none of its ID/version pairs already exists on nuget.org, retains the
validated `.nupkg` files as a GitHub Actions artifact, requests the temporary key
only after those gates pass, publishes the packages serially, and waits for every
published identity to become readable from nuget.org.

Before publishing, the workflow queries nuget.org for every staged package ID and
version. It pushes the 34 exact files from `nuget/overrides` serially, stops on the
first failure, and reads every package back from the source before declaring the
batch complete. Do not use `--skip-duplicate`: an existing version may contain
different bytes, and a changed package must receive a new version rather than
silently being treated as the staged artifact. Never place an API key in this
guide, a command line captured in logs, or release notes. The handoff must include
the source URL, package versions, local SHA-256 values, and remote verification
results.

## Create the prereleases

Confirm that each intended tag is unused with `gh release view <tag>`. Then create
one prerelease per extension, targeting the already-pushed `master` commit:

```powershell
gh release create debugger-v<version> `
    "$vsixStaging/FrontEndCSharp.Debugger-<version>.vsix" `
    --repo drewdelano/FrontEndCSharp.Private --target master --prerelease `
    --title "FrontEndCSharp Debugger <version>" `
    --notes "<summary, SHA-256, and unsigned-package warning>"

gh release create fecsx-v<version> `
    "$vsixStaging/FrontEndCSharp.FECSX-Editor-<version>.vsix" `
    --repo drewdelano/FrontEndCSharp.Private --target master --prerelease `
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
gh release view debugger-v<version> --repo drewdelano/FrontEndCSharp.Private `
    --json tagName,name,url,isPrerelease,targetCommitish,assets
gh release view fecsx-v<version> --repo drewdelano/FrontEndCSharp.Private `
    --json tagName,name,url,isPrerelease,targetCommitish,assets
```

Publication is complete only after GitHub reports each asset as uploaded with the
expected size and digest. Return the two release links, hashes, test status, and any
unsigned/private-repository caveats to the user.
