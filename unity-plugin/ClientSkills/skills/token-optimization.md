---
name: token-optimization
description: Token optimization patterns for the Unity MCP project — batch-first rule (always use batch for 2+ ops), inspect tool for multi-object reads, C#-side field projection/compression (fields=/compress= params), tool gating via discover_tools (SCENE/COMPONENTS/ASSETS/MEDIA/VERIFY/RUNTIME/TESTS/SYSTEM categories), response format comparisons (text vs JSON savings), hierarchy serialization rules, and token budget benchmarks by scenario. Load when planning multi-step MCP operations, choosing between individual vs batched tool calls, or when response size or tool-count efficiency matters.
user-invocable: false
---

# Token Optimization & Batch-First Patterns

## Response Format Comparison

| Format | Example | Tokens | vs JSON |
|--------|---------|--------|---------|
| JSON full | `{"path":"/a/b.py","type":"file"}` | ~40 | baseline |
| JSON minimal | `["/a/b.py"]` | ~15 | -62% |
| Text tree | `Main Camera [Camera]` | ~8 | **-80%** |
| Newline list | `a.py\nb.py` | ~5 | **-87%** |

## Hierarchy Format Rules

- Transform ALWAYS omitted (100% have it)
- Inactive → `!` suffix
- Components → `[Type1, Type2]`
- Indent = tree connectors (`├─`, `└─`, `│`) per depth level

## Key Principle

**Serialize to text in C#** (Unity side), not in Python. Avoids transferring large JSON over TCP.

## Token Budget per Scenario

| Scenario | Text Format | JSON | Savings |
|----------|-------------|------|---------|
| 50 objects hierarchy | ~350 | ~4000 | 11x |
| 10 component props | ~100 | ~400 | 4x |
| 5 console errors | ~100 | ~200 | 2x |
| 5-tool batch | ~400 (vs ~2000) | - | **5x** |
| Screenshot analysis | ~50 (text) | ~100k (base64) | **2000x** |
| Debug session total | ~550 | ~4600 | **8x** |

## Tool Gating (Capability-Based Visibility)

**Always visible: 15 CORE + 30 TIER1 = 45 tools.** Gated tools REQUIRE `discover_tools("category")` first.

**CORE (15):** `apply_scene_change`, `batch`, `create_object`, `editor`, `execute_code`, `get_compile_errors`, `get_component`, `get_console`, `get_hierarchy`, `inspect`, `manage_component`, `resolve_scene_refs`, `scene_change_plan`, `set_property`, `verify_after_change`

_Canonical category names: `SCENE`, `COMPONENTS`, `ASSETS`, `MEDIA`, `VERIFY`, `RUNTIME`, `TESTS`, `SYSTEM`. Lowercase aliases still work._

| Category | Tools | When to enable |
|----------|-------|---------------|
| `SCENE` | configure_objects *(direct_only)*, setup_objects *(direct_only)*, find_objects, get_object_detail, get_components_list, set_active, set_material, set_properties, set_property_delta, scene_diff, spatial/collider tools | Scene/object inspection and mutation |
| `COMPONENTS` | auto_wire, references, wire_event, unwire_event | Component reference/event wiring |
| `ASSETS` | asset, material, material_audit, prefab, scriptable_object, project_settings, shader | Asset management |
| `MEDIA` | animation, timeline, animator, particle, create_ui, set_rect, validate_layout, screenshot_baseline/compare, render_analyze, analyze_lod_culling, ui_intent, vfx_intent | Animation, UI, VFX, screenshots, render checks |
| `VERIFY` | await_compile, compile_preflight, verify_after_change, lint_scene_refs, resolve_scene_refs, diagnose, scan_scene, scene_health, validate_references | Post-change verification |
| `RUNTIME` | console_mark, get_console_since, invoke_method, set_runtime_property, wait_until, move_to, query_state, debug/debug_animator/debug_physics, get_frame_stats/get_memory, profile, watch/get_watches, snapshot | Play Mode runtime and profiling |
| `TESTS` | run_playtest, run_playtest_suite, run_tests, run_tests_wait, lint_playtest, lint_playtest_suite, test_step, alias import/export/validate, get_test_count/progress/results | Playtest DSL & NUnit runner |
| `SYSTEM` | ask/ask_user, discover/status/schema, reconnect/sync, sessions, skills/templates, execute_code, auto_fix, smart_build, capabilities, checkpoint, undo_last | MCP status, session, code, and meta workflows |

## Field Projection & Compression (fields= / compress=)

Two more token-savers on top of batch-first — cut the response BEFORE it crosses TCP.

| Param | Tools | Effect |
|-------|-------|--------|
| `fields=name1,name2` | `get_component`, `inspect` | Keep only named fields — biggest single win on wide components |
| `compress=true` | `get_component`, `inspect` | Strip fields at Unity defaults (`0`, `false`, `(0,0,0)`, `Untagged`, ...) |
| `compress=true` | `get_hierarchy` | Groups repeated siblings → `[12x slot]`, `[8x point]` (Python-side, different algorithm) |

`fields` and `compress` are mutually exclusive; `fields` takes precedence.

```
# fields — keep only what you need out of a wide component dump
get_component(path="/Player", type="Rigidbody", fields="mass,drag")

# compress — strip Unity defaults (reduces noise on wide components)
inspect(paths="/Player,/Enemy", components="Health", compress=true)
get_component(path="/Player", type="Transform", compress=true)

# both work inline on batch DSL lines too
batch(commands="get_component path=/Player type=Transform fields=position,rotation
inspect paths=/A,/B components=Rigidbody compress=true")

# get_hierarchy compress is unrelated — groups repeated siblings, Python-side
get_hierarchy(root="/Environment", compress=true)
```

`fields` aliases (case-insensitive): position, rotation, scale, mass, enabled, active, name, tag, layer.

## Batch-First Rule

**ALWAYS prefer `batch` for 2+ operations** — both reads AND writes.

```
# BAD: 3 separate tool calls (~450 tokens overhead)
get_component path=/A type=Transform
get_component path=/B type=Transform
get_component path=/C type=Transform

# GOOD: 1 batch read (~90 tokens overhead)
batch commands="get_component path=/A type=Transform
get_component path=/B type=Transform
get_component path=/C type=Transform"

# GOOD: batch write + verify
batch commands="set_property path=/Player component=Health prop=max value=200
get_component path=/Player type=Health"
```

For multi-object component reads, prefer `inspect` over batch.

## Batch Guard: direct_only Tools Blocked

31 tools are `direct_only` — rejected by `batch()` with error. Always call them as standalone MCP tools.

**Note:** `configure_objects` and `setup_objects` are TIER1 but also `direct_only` — never nest inside `batch()`.

`animator_intent`, `ask`, `await_compile`, `budget_status`, `configure_objects`, `console_mark`, `debug`, `discover_tools`, `do`, `doctor`, `get_console_since`, `get_metrics`, `lint_playtest_suite`, `list_connections`, `list_skills`, `list_templates`, `mcp_status`, `navmesh_query`, `release_smoke`, `resolve_tool_schema`, `run_playtest_suite`, `run_tests_wait`, `screenshot_baseline`, `screenshot_compare`, `set_properties`, `setup_objects`, `snapshot`, `ui_intent`, `validate_playtest_aliases`, `vfx_intent`, `watch`

**Batch envelope (v0.92):** when ANY inner command returns `ok:false`, the outer batch response also returns `ok:false`. Check `ok` on the batch result, not just individual command results.

## See Also

- `.claude/skills/unity-efficiency.md` — batch-first patterns, inspect, tool gating workflows
- `.claude/skills/unity-mcp-reference.md` — complete tool signatures, batch syntax
