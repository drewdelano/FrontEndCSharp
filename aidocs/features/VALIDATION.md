# Validation

This guide defines the approved browser validation boundary. Validation support is
practical rather than exhaustive: ordinary form models and their managed validation
logic are the target, while server request binding, controllers, Razor, and client-side
jQuery adapters are not.

## Architecture

- `System.ComponentModel.Annotations.fecs` supplies the reflection-like orchestration
  that the desktop implementation obtains from `TypeDescriptor` and runtime internals.
- Application property accessors, custom `ValidationAttribute` constructors and
  `IsValid` overrides, and `IValidatableObject.Validate` remain managed MSIL.
- Runtime-override providers request package-neutral property, custom-attribute, and
  public-instance-method reachability. The compiler has no DataAnnotations policy.
- Attribute metadata records the exact constructor identity and argument types so an
  application attribute can be reconstructed without executing attributes in the
  compiler.

The companion exists because the .NET 8 `Validator` implementation reaches desktop
type descriptors, concurrent caches, monitors, globalization, spans, and native
runtime intrinsics before validating an ordinary model. This is the same narrow
boundary used by the JSON companions: public orchestration is adapted, while usable
application code is compiled.

## DataAnnotations surface

The verified entry points are all public `Validator` object, property, value, and
throwing APIs. Covered behavior includes `validateAllProperties`, inherited public
properties, display and explicit error messages, stable member names, null and
context-mismatch exceptions, malformed length declarations, custom managed
attributes, and `IValidatableObject` results.

The practical built-in attribute set is `Required`, `StringLength`, `MinLength`,
`MaxLength`, numeric and convertible `Range`, `RegularExpression`, `Compare`,
`EmailAddress`, `Phone`, `CreditCard`, and `Url`. Null values retain the normal
separation between `Required` and the other validators. Explicit value validation
currently accepts an attribute array, which is the normal C# call shape.

`ValidationContext.ObjectInstance`, `MemberName`, and display-name behavior used by
custom validators are preserved. The service-provider and mutable `Items` scenarios,
resource-based localization, `CustomValidationAttribute`, `EnumDataType`, file-name
validation, and arbitrary `IEnumerable<ValidationAttribute>` implementations are not
part of this practical surface. They must fail or be added with focused parity tests;
they must not silently approximate desktop behavior.

## React demo validation

The React demo intentionally uses `Validator.TryValidateObject` directly for its
flat, three-property `FocusIntentForm`, including its integer priority constrained
to 1 through 3. That keeps field errors tied to explicit
DataAnnotations attributes without introducing a second validation abstraction. Its
demo-local `FormField<TModel>.IdFor(model => model.Property)` helper derives
label/input IDs and
error lookups from direct property expressions; nested and non-property selectors
fail closed. This helper is a compile-time-safe naming convenience, not a recursive
validation abstraction. The controlled priority textbox retains raw input separately
from the integer model so an out-of-range value remains visible while the range error
is shown; submission performs the bounded text-to-integer mapping before validation.

## FluentValidation

FluentValidation 12.2.0 is pinned in the browser fixture and remains package-owned
managed MSIL. `FluentValidation.fecs` adapts only the package's environment-dependent
language-manager lookup; rules, validators, conditions, expression accessors,
failure construction, and asynchronous control flow execute from the package.

The practical verified surface includes synchronous and asynchronous validation,
built-in and custom rules, rule- and class-level cascade modes, `When` conditions,
child validators, `RuleForEach`, rule sets, dependent rules, nested/indexed property
paths, severity/error-code/custom-state metadata, cancellation, and the package's
failure when synchronous validation is attempted on an asynchronous rule. The
framework companions used by that path provide the required collection,
concurrent-collection, LINQ materialization, expression, and regular-expression
contracts without embedding FluentValidation policy in the compiler.

This is a representative browser contract rather than an exhaustive promise for
every validator, localization resource, extension package, or integration package.
ASP.NET Core dependency-injection registration and the obsolete synchronous
auto-validation pipeline remain outside scope. Any shared form-error projection is
optional and consumes FluentValidation's stable failures.

## Verification

The focused suites are:

- `Compiler/Compiler.Tests/ValidationIntegrationTests.cs` for DataAnnotations and
  runtime reachability;
- `RuntimeOverrides/RuntimeOverrides.Tests/CompanionTests.cs` for companion integrity;
- `Demo/FrontEndCSharp.ReactDemo.Tests` plus the FECSX React-demo Chromium smoke for
  direct-property selector behavior and the three-field managed form's invalid and
  valid paths; and
- `Compiler/Compiler.Tests/FluentValidationIntegrationTests.cs` and its pinned
  browser fixture for synchronous and asynchronous package behavior.

Every newly supported attribute or FluentValidation rule requires a public-contract
test.
Malformed and unsupported cases need deterministic failure coverage as well as happy
paths.
