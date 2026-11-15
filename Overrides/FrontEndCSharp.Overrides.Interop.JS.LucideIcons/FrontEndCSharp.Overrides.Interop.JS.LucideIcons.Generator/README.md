# Lucide icon catalog generator

This C# tool regenerates the managed facades, typed companion providers, static
JavaScript imports, and override manifest from the pinned `lucide-react@1.72.0`
ESM icon modules.

It treats modules that call `createLucideIcon` as canonical, then removes icon
components marked `@deprecated` in the package declarations. Modules that only
re-export another module are aliases and are deliberately excluded. The generator
also requires one unique upstream icon-data name per generated facade and fails if
the pinned canonical or deprecated counts change.

Run it from the repository root after installing the pinned npm package into a
temporary directory:

```powershell
dotnet run --project Overrides/FrontEndCSharp.Overrides.Interop.JS.LucideIcons/FrontEndCSharp.Overrides.Interop.JS.LucideIcons.Generator/FrontEndCSharp.Overrides.Interop.JS.LucideIcons.Generator.csproj -- `
    <node_modules/lucide-react/dist/esm/icons> `
    Overrides/FrontEndCSharp.Overrides.Interop.JS.LucideIcons
```

Files beneath each adapter's `Generated` directory and `method.*.js` files beneath
the companion's `Overrides` directory are generated output and must not be edited
by hand.
