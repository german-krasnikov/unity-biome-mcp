---
name: unity-csharp-editing
description: Use when creating or changing Unity C# files, preflighting code, waiting for compilation, inspecting schemas, or renaming serialized fields.
---

# Unity C# Editing

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. This consumer skill covers
code changes in the user's Unity project. It does not describe Unity Biome MCP
plugin internals.

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
6. Run focused NUnit tests with `run_tests_wait`.
7. Check the console delta from a pre-change mark.

Use `await_compile` only when compilation was already started by another
action. It does not trigger refresh or compilation.

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
