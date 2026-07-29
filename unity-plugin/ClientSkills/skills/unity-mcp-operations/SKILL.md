---
name: unity-mcp-operations
description: Use when operating Unity Biome MCP: selecting or discovering tools, batching compatible commands, resolving schemas, reducing response size, or recovering a connection or session.
---

# Unity MCP Operations

Use this skill as the shared operating contract for every Unity Biome MCP task.
The live server schema and the current Unity state are authoritative.

## Core Workflow

1. Confirm the server and Editor state with `mcp_status` when connectivity or
   mode is uncertain.
2. Read the smallest useful scope. Start with a hierarchy summary, targeted
   `inspect`, or one component rather than a full-scene dump.
3. Enable a gated category with its uppercase canonical name:
   `SCENE`, `COMPONENTS`, `ASSETS`, `MEDIA`, `VERIFY`, `RUNTIME`, `TESTS`, or
   `SYSTEM`.
4. Call `resolve_tool_schema(tools="<name>")` before relying on a parameter you
   have not used in the current session. Do not reconstruct signatures from
   memory.
5. Mark the console before mutations when new errors would matter.
6. Choose the narrowest aggregate tool that expresses the operation.
7. Mutate, verify the changed state, and report evidence rather than a list of
   calls.

Before rebuilding a known repeated workflow, call `list_skills()`. Reuse a
matching learned MCP skill with `use_skill(...)` when its stored contract still
matches the current scene and schema.

## Routing

| Need | Prefer |
|---|---|
| Same component across multiple objects | `inspect(paths=..., components=..., fields=...)` |
| Create several ordinary objects | `setup_objects(specs=...)` as a standalone call |
| Set properties across several objects | `configure_objects(config=...)` as a standalone call |
| Set several properties on one object | `set_properties(path=..., props=...)` |
| Several compatible synchronous Unity commands | `batch(...)` |
| NUnit run with completion result | `run_tests_wait(...)` as a standalone call |
| Repeatable Play Mode scenario | `run_playtest(...)` or `run_playtest_suite(...)` |
| Stable repeated batch sequence | `save_skill(...)`, then `use_skill(...)` |
| Stable parameterized scene scaffold | `save_template(...)`, then `apply_template(...)` |
| Unknown or gated capability | `discover_tools(...)`, then `resolve_tool_schema(...)` |

Do not infer batchability from `direct_only=False`. Some asynchronous and
special-dispatch commands are still rejected by Unity's batch executor.

## Capability Map

| Category | Route |
|---|---|
| `CORE` | this skill or `unity-scene-authoring` |
| `SCENE` | `unity-scene-authoring`, `unity-ui-authoring`, or `unity-physics-spatial` |
| `COMPONENTS` | `unity-scene-authoring` |
| `ASSETS` | `unity-assets-prefabs` or `unity-materials-shaders` |
| `MEDIA` | `unity-animation`, `unity-ui-authoring`, `unity-materials-shaders`, or `unity-particles-vfx` |
| `VERIFY` | `unity-csharp-editing`, `unity-testing-verification`, or `unity-diagnostics-performance` |
| `RUNTIME` | `unity-testing-verification`, `unity-diagnostics-performance`, or `unity-physics-spatial` |
| `TESTS` | `unity-testing-verification` |
| `SYSTEM` | this skill, `unity-csharp-editing`, or `unity-diagnostics-performance` |

For a full capability audit, call `discover_tools(enable=False)` once. Enable
only the required uppercase category, then request all uncertain schemas in one
call:

```text
resolve_tool_schema(tools="tool_a,tool_b,tool_c")
```

This is the coverage mechanism for the complete current tool surface, including
new and plugin-provided tools. Do not replace it with a copied inventory.

## Connection And Capability Recovery

Use one probe for the current question:

| Need | Tool |
|---|---|
| Connection, ports, mode, or version | `mcp_status` |
| Reconnect after transport loss | `reconnect_unity` |
| Tool enablement state | `get_enabled_tools` |
| Sync or reload state | `sync_status` |
| Current full capability surface | `discover_tools(enable=False)` |
| Several uncertain contracts | one `resolve_tool_schema(tools="...")` call |

Do not enable all categories as a recovery step. If the Editor is compiling or
reloading, follow the returned state instead of retrying mutations.

Use natural-language intent tools only for drafts:

- `ask(question=...)` for a compact read-only answer;
- `do(intent=..., dry_run=True)` for a proposed general mutation;
- `ui_intent(intent=..., parent=..., dry_run=True)`;
- `animator_intent(target=..., intent=..., dry_run=True)`;
- `vfx_intent(target=..., intent=..., dry_run=True)`.

Inspect the dry-run result and switch to precise typed tools for exact values.

## Response Discipline

- Request only fields needed for the decision.
- Prefer summaries and incremental reads before deep inspection.
- Keep exact failure lines, expected values, actual values, and provenance.
- Compress successful evidence; do not probabilistically summarize failures.
- Never claim a typed outer `ok` field. Most MCP responses are text or tool
  errors.

## Stop Conditions

Stop and diagnose when:

- the Editor is compiling or reloading;
- a required target resolves ambiguously;
- the live schema differs from the planned call;
- a mutation reports a partial failure;
- verification contains `ERR`, `FAIL`, `TIMEOUT`, or `BLOCKED`;
- an MCP retry would repeat the same arguments without new evidence.

## References

- Read [batching.md](references/batching.md) before combining commands or using
  aliases, learned MCP skills, or templates.
- Read
  [transactions-and-verification.md](references/transactions-and-verification.md)
  before multi-object, destructive, code, or test-backed changes.
- Read [session-and-reuse.md](references/session-and-reuse.md) for incremental
  scene checks, cold-start recovery, visual baselines, learned skills, and
  templates.
