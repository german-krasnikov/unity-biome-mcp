---
name: unity-csharp-editing
description: Use when creating or changing Unity C# files, preflighting code, waiting for compilation, writing focused tests, or renaming serialized fields.
---

# Unity C# Editing

Read `.claude/skills/unity-mcp-operations/SKILL.md` once if it is not already
loaded. This skill covers code in the consumer's Unity project; follow that
project's assemblies, test framework, fixture hierarchy, and style.

## Workflow

1. Read the complete target file, direct callers, and nearby tests.
2. Mark the console before editing.
3. Prepare the complete proposed file content.
4. Preflight it:

```text
compile_preflight(
  file_path="Assets/Scripts/FeatureController.cs",
  new_content="<complete proposed file>"
)
```

5. Write only after preflight succeeds or explicitly reports that Roslyn is
   unavailable.
6. Call `sync_unity(timeout=60)` once to trigger refresh and wait for the new
   domain.
7. Run focused tests with one correlated
   `run_tests_wait(mode="EditMode", filter="...")` call.
8. Check the console delta and inspect any serialized data affected by the
   change.

Use `await_compile` only when compilation was already started by another
action. It waits and reports; it does not trigger refresh or compilation.

## Test Changes

- Match the project's installed Unity Test Framework version and existing
  fixture conventions.
- Keep tests independent and register or restore every resource they own.
- Use bounded waits and await all asynchronous work before the test ends.
- Prefer a narrow EditMode test for deterministic editor or serialization
  behavior; use PlayMode only when runtime frames or engine behavior matter.
- Do not save user scenes, delete unowned assets, or hide cleanup failures.
- Retain exact failing test names, expected and actual values, and stack traces.

Do not copy Unity Biome MCP's repository-only fixtures, worker attributes, or CI
commands into a consumer project. They are implementation details of this
package's own repository, not a public testing framework.

## Execute Code

Use `execute_code` only for bounded Editor automation, never as a substitute for
maintainable source. Its convenience transforms hoist user `using` directives,
inject `using Object = UnityEngine.Object;` when needed, and rewrite a bare
`return;` for the generated execution wrapper.

Filesystem and external-process side effects from `execute_code` are not part
of scene Undo. Keep them outside atomic scene transactions.

## Serialized Field Rename

```text
serialized_field_rename_audit(
  type="FeatureController",
  old_field="oldValue",
  new_field="value"
)
```

Use the result to decide whether `FormerlySerializedAs` is required and when it
is safe to remove. Reinspect serialized values after the domain reload.

## Rules

- Prefer normal file edits for persistent code.
- Do not call parameterless `compile_preflight`.
- Keep compiler file paths and line numbers intact.
- Never accept tests from a stale domain after compilation failed or timed out.
- Do not hand-roll `run_tests` plus a polling loop for an ordinary run.
- Do not claim Play Mode acceptance from compilation or EditMode tests.
- Stop when the project cannot compile cleanly or the focused test result is
  incomplete.
