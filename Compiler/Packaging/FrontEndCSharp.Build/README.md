# FrontEndCSharp.Build

`FrontEndCSharp.Build` integrates FrontEndCSharp browser-output generation into an SDK-style ASP.NET Core web project's normal MSBuild lifecycle.

Add the package and identify the C# project whose compiled assembly should run in the browser:

```xml
<ItemGroup>
  <PackageReference Include="FrontEndCSharp.Build" Version="0.1.19" />
</ItemGroup>

<PropertyGroup>
  <FrontEndCSharpProject>$(MSBuildProjectDirectory)\..\FrontEnd.Program\FrontEnd.Program.csproj</FrontEndCSharpProject>
  <FrontEndCSharpOutputPath>$(MSBuildProjectDirectory)\wwwroot\js</FrontEndCSharpOutputPath>
</PropertyGroup>
```

The package carries the compiler payload and extracts it by content hash beneath a short local cache path. Consumers do not need a repository checkout or a path to `Compiler.csproj`.

Browser generation is content-incremental. After a successful build, the package records a fingerprint under `obj/FrontEndCSharp` covering deployed application/dependency assemblies, PDBs and manifests, companion assemblies, the compiler/runtime/bundler payload, and effective compiler configuration. A no-op build skips the compiler and bundler when `index.js`, `index.js.map`, and `msil.bin` are still present. State is committed only after the compiler's staged publication succeeds, so a failed generation cannot bless partial or stale output.

The Visual Studio debugger is distributed separately in the matching FrontEndCSharp VSIX. Install both artifacts at the same version when using managed browser debugging.
