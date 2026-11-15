# FrontEndCSharp override package

This package belongs to the FrontEndCSharp browser compiler's managed interop and
runtime-override package family. Its NuGet description identifies the managed API
or runtime surface supplied by this specific package.

Packages whose IDs end in `.fecs` carry compiler/runtime override resources.
Managed interop packages provide the corresponding C# API surface. Applications
should reference the packages selected by their FrontEndCSharp template or build
integration rather than loading package payloads directly.

The package version is part of the public compatibility contract and must be
incremented whenever its published contents change.
