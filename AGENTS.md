# FrontEndCSharp Agent Instructions

Before changing this repository, read all of the following:

1. [`README.md`](README.md) for the architecture, repository layout, and commands.
2. [`aidocs/README.md`](aidocs/README.md) for task-specific context routing.
3. [`aidocs/GOALS.md`](aidocs/GOALS.md) for scope, anti-goals, and implementation standards.
4. [`aidocs/WORKFLOW.md`](aidocs/WORKFLOW.md) for the repeatable development and regression process.
5. [`aidocs/TODO.md`](aidocs/TODO.md) for finite agreed work.
6. [`aidocs/DISCUSSION.md`](aidocs/DISCUSSION.md) for unresolved proposals and decisions.
7. [`aidocs/LESSONS_LEARNT.md`](aidocs/LESSONS_LEARNT.md) for prior mistakes and durable technical lessons.

Then read only the topic guides selected by the routing table in `aidocs/README.md`
for the code and contracts the task can change. Do not load unrelated feature
documents by default. Keep the core documents, routing table, and relevant topic
guides current as work and lessons are discovered.

## Non-negotiable workflow

- Work as a principal-level C# compiler developer and optimize for long-term correctness.
- Follow `aidocs/WORKFLOW.md` for implementation and verification.
- Preserve user changes and never edit generated output as source.
- Use C# for repository tests, integration harnesses, generators, validators, and automation. Do not add JavaScript or PowerShell tooling when C# can perform the work; JavaScript is reserved for the product runtime and selected companion implementations, with only narrowly justified tests whose subject cannot be exercised through a C# harness.
- Record agreed unfinished work in `aidocs/TODO.md` before changing focus. Record
  unresolved proposals or decisions in `aidocs/DISCUSSION.md`; do not add them to
  `TODO.md` until the user agrees to the direction.
- Run the scope checkpoint in `aidocs/WORKFLOW.md` before turning broad library,
  framework, subsystem, or multi-component work into TODO entries or implementation.
  Do not infer a compatibility program from the name of a library or API surface.
- Finish each response by stating what was accomplished or why it was impossible.
- Name every currently failing test in the final response; explicitly state when none fail.
- Check the preceding conversation, `aidocs/TODO.md`, and `aidocs/DISCUSSION.md`
  for known failures and unresolved decisions before starting new work.
