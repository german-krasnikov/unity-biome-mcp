---
name: unity-csharp-editing
description: Use when creating or changing Unity C# files, preflighting code, waiting for compilation, inspecting schemas, or renaming serialized fields.
---

# Unity C# Editing

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. This consumer skill covers
code changes in the user's Unity project. It does not describe Unity Biome MCP
plugin internals.

When the change creates or modifies a test, also read
`.claude/skills/unity-testing-verification/references/test-authoring.md` before
editing. It requires the common test-base hierarchy, native NUnit/UTF
attributes, Task-first async code, and exact run correlation.
The conversion rule is hard: a new or modified test may not use `[UnityTest]`
or return `IEnumerator`; rewrite it as `[Test] async Task` and await Unity
operations. In UTF 1.6 EditMode UI tests, wait with the common-base
`WaitForEditorUpdatesAsync`; `Awaitable.NextFrameAsync` depends on the runtime
player loop and is forbidden there. PlayMode runtime frame boundaries may use
the matching Unity `Awaitable` API.

## Workflow

1. Read the complete target file and nearby tests.
2. Prepare the complete proposed content.
3. Run:

```text
compile_preflight(
  file_path="Assets/Scripts/FeatureController.cs",
  new_content="<complete proposed file>"
)
```

4. Write only after preflight succeeds or explicitly reports Roslyn
   unavailable.
5. Run `sync_unity(timeout=60)` once to trigger refresh and wait for the edited
   code to become live.
6. In an ordinary consumer project, run focused NUnit tests with one correlated
   `run_tests_wait(mode="EditMode", filter="...")` call. Do not hand-roll
   `run_tests` plus polling. When changing the Unity Biome MCP repository itself,
   use `run_unity_tests.py` with `EditMode`, the already-connected repository
   test `--project`, the focused `--filter`, `--timeout 1800`, and `--json`
   instead. Do not open a second Unity Editor for an ordinary run; disposable
   workers are reserved for explicitly destructive acceptance lanes.
7. Check the console delta from a pre-change mark.

Use `await_compile` only when compilation was already started by another
action. It does not trigger refresh or compilation.

## Execute Code Helpers

When using `execute_code`, these convenience transforms are applied:

- `return;` is automatically rewritten as `return null;` for consistency with
  void-context return expectations.
- User `using` directives are automatically hoisted to scope top.
- `using Object = UnityEngine.Object;` is automatically injected for common
  reference disambiguation.

## Serialized Field Rename

```text
serialized_field_rename_audit(
  type="FeatureController",
  old_field="oldValue",
  new_field="value"
)
```

Use the result to decide whether `FormerlySerializedAs` is required and when it
is safe to remove.

## Rules

- Prefer normal file edits for persistent project code.
- Use `execute_code` only for bounded Editor automation, not as a substitute
  for maintainable source.
- Do not call parameterless `compile_preflight`.
- Keep full compiler diagnostics; do not summarize away file and line data.
- Never accept tests from a stale domain when compilation failed or timed out.
- Verify serialized data after field or type changes.

Bad: invoke `compile_preflight` without the required path and complete proposed
content, then assume `await_compile` starts a compile.

Good: preflight the full file, write, then use one `sync_unity` call.
