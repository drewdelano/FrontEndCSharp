# FrontEndCSharp.Fecsx.Build

This package adds the FrontEndCSharp FECSX incremental source generator to a
C# project. Project `*.fecsx` files are registered automatically, and generated
C# is emitted beneath the project's `obj` directory for inspection and source
mapping.

FECSX components require a reference to the managed `Interop.JS.React` API.

FECSX attributes can follow either React-style camel casing or C#-style Pascal
casing. Set the convention in the consuming project:

```xml
<FrontEndCSharpFecsxAttributeCasing>camelCase</FrontEndCSharpFecsxAttributeCasing>
```

The other supported value is `PascalCase`. If the property is omitted,
`camelCase` is used. The configured convention controls completion and is
enforced at build time; an attribute using the other convention produces
`FECSX4006`.
