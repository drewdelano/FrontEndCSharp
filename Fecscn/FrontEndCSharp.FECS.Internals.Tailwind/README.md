# FrontEndCSharp.FECS.Internals.Tailwind

Offline-after-restore Tailwind CSS v4 build integration for FrontEndCSharp. The
package scans application `.fecsx` and `.cs` sources, loads the default shadcn
theme and animations, and emits one tree-shaken CSS asset.

The FECSX editor embeds a compressed snapshot of Tailwind's pinned default
utility catalog, Tailwind-parsed functional roots, emitted CSS properties, compiled
arbitrary-value examples, the effective spacing value, responsive min/max media variants, and the
`hover:`, `focus:`, and `dark:` variants. The editor keeps variants and standalone
utilities exact, collapses parameterized utilities to entries such as `m-`, and
splits broad roots by the CSS property they emit. Repeated color scales become
families such as `bg-amber-`, while unrelated values such as `bg-auto` remain
separate. Exact defaults sort beside their value families, so `grow` is adjacent to
`grow-`. A selected family expands to only the concrete values Tailwind generated.
Family tooltips lead with a purpose-first explanation and show those exact values,
resolvable pixel equivalents, and valid arbitrary or custom-property examples. When
the public declaration is shared indirection, the tooltip selects the varying
declaration and presents it directly, such as `ring width: 2px`, `blur: var(--blur-sm)`,
or `border-width: 2px`.
Refresh the source artifact after changing the pinned
Tailwind version:

```powershell
dotnet run --project Fecscn/FrontEndCSharp.Tailwind.Tasks -- `
  generate-editor-catalog `
  Fecscn/FrontEndCSharp.FECS.Internals.Tailwind/JavaScriptPackages `
  Compiler/Fecsx/FrontEndCSharp.Fecsx.Editor/Assets/TailwindClassCatalog.tsv.gz
```

The generator is C#-owned and invokes the installed Tailwind package only for
its JavaScript design-system API. The catalog remains usable without Node.js or
network access inside Visual Studio.

Every successful project build also writes
`$(FrontEndCSharpTailwindEditorCatalog)` beneath the project's intermediate
directory. That catalog is generated from the same effective Tailwind v4 CSS
input used for compilation, including local `@import`/`@reference` files and
top-level `@theme` namespaces. Custom values such as `--drop-shadow-card` and
`--breakpoint-tablet` therefore become `drop-shadow-card`, `tablet:`, and
`max-tablet:` FECSX completions after the build. Source-only changes reuse the
catalog; theme, imported-theme, pinned-tool, or task-host changes regenerate it.
Legacy `tailwind.config.js` files are not read.
