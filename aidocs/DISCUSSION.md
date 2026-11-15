# Discussion Topics

This file tracks unresolved questions, proposals, and design choices that need a
conversation before they become work. It is not an implementation checklist.

When a direction is agreed upon, remove the topic from this file and add the
resulting concrete, testable work to `TODO.md`. Record durable conclusions in the
relevant design document or `LESSONS_LEARNT.md`.

For broad library, framework, subsystem, or multi-component proposals, use the
scope checkpoint in `WORKFLOW.md`. Capture only the unresolved choice here; do not
expand a possible API surface into a speculative implementation plan.

## ReactDemo compiler optimization priority

The 2026-11-11 application profile in `engineering/PERFORMANCE.md` found two large,
independent costs: materializing 10,060 JavaScript package files before webpack and
allocating 22.51 GB while repeatedly cloning possible-type flow state. The documented
recommendation is to prototype source-map-aware package materialization first,
because it is a bounded output-pipeline change with a 6.8-second directly measured
bundled budget. Possible-type copy-on-write state has a larger theoretical ceiling
but a materially higher control-flow correctness risk. Decide which experiment, if
either, should become an implementation TODO; the investigation did not authorize
either change.

## Release ReactDemo browser execution

The Release artifact produced for the ReactDemo performance fixture compiles and has
stable managed graph/MSIL output, but real Chromium throws `TypeError: Unsigned
comparison requires integer or floating-point operands` before the Orbit ready
state. The standard Debug ReactDemo Chromium acceptance remains green. Decide
whether diagnosing this Release-only compiler/runtime correctness defect should be
added as separate work; it is not an optimization and was not changed as part of the
performance investigation.

## Internal NuGet publication destination

The routine release batch now has a defined boundary for the 34 packages produced
by `Overrides/Overrides.sln`, and the debugger and FECSX editor remain independently
versioned VSIX releases. The four standalone package identities are agreed:

- `FrontEndCSharp.FECS.Internals.Build`
- `FrontEndCSharp.FECS.Internals.Fecsx.Build`
- `FrontEndCSharp.FECS.Internals.Tailwind`
- `FrontEndCSharp.FECS.Internals.Fecscn.Tool`

Their publication destination is still unresolved. Decide the package source,
visibility, authentication mechanism, and whether the internal `fecscn` tool needs
to be published at all. Until those choices are agreed, local packaging under the
timestamped `nuget/internal` staging directory is the terminal step.
