---
description: MCP efficiency patterns — batch-first rule, tool gating, inspect vs get_component
---

# Unity Efficiency: Batch-First Interaction

## Rule

**ALWAYS use `batch` for 2+ Unity operations** — reads AND writes. Individual tool calls only for single operations.

## Why

Each MCP tool call costs ~150 tokens overhead + latency. Batch costs ~150 tokens for ALL operations combined.

## Patterns

### 1. Scene Exploration (reads)
```
batch(commands="""
get_hierarchy depth=2
get_console count=5
search_scene query="t:Camera"
""")
```

### 2. Multi-Object Inspection
```
# inspect — best for same component across multiple objects
inspect(paths="/Player,/Enemy,/Camera", components="Transform", fields="position,rotation")

# compress=true strips Unity default values before TCP
inspect(paths="/Player,/Enemy", components="Health", compress=true)
```

### 3. Multi-Property Write: configure_objects (TIER1, direct_only)
```
# Single call to set many properties across objects (no batch needed)
# configure_objects is direct_only — NEVER inside batch()
configure_objects(objects_and_config=[
    {"path": "/Player", "components": {"Health": {"max": 200, "current": 200}}},
    {"path": "/Enemy",  "components": {"Health": {"max": 50,  "current": 50}}},
])
```

### 4. Create + Configure: setup_objects (TIER1, direct_only)
```
# setup_objects is direct_only — NEVER inside batch()
setup_objects(spec=[
    {"name": "Turret", "parent": "/Defenses",
     "components": ["MeshRenderer", "Turret"],
     "config": {"Turret": {"range": 10, "damage": 5}}}
])
```

### 5. Post-Mutation Verification
```
# verify_after_change replaces 5 manual verification steps
console_mark(label="before_edit")
# ... mutations ...
verify_after_change(mark_id="<mark_id>")
# → "PASS: compile + errors_clean + console_clean + tests(42/42)"
```

### 6. Quick Scene Overview
```
get_hierarchy(summary=true)              # ~20 tokens — counts only
get_hierarchy(depth=2)                   # top 2 levels
get_hierarchy(root="/Environment", compress=true)  # groups repeated siblings
```

### 7. Field Projection (batch DSL)
```
batch(commands="get_component path=/Player type=Transform fields=position,rotation
inspect paths=/A,/B components=Rigidbody compress=true")
```

### 8. Debug Session
```
batch(commands="""
get_console count=20
editor action=state
search_scene query="t:Light active=false"
""")
```

## Tool Gating

**Always visible: 15 CORE + 30 TIER1 = 45 tools total.**

**CORE (15):** `apply_scene_change`, `batch`, `create_object`, `editor`, `execute_code`, `get_compile_errors`, `get_component`, `get_console`, `get_hierarchy`, `inspect`, `manage_component`, `resolve_scene_refs`, `scene_change_plan`, `set_property`, `verify_after_change`

**TIER1 (30, always visible):** `alias_status`, `ask`, `ask_user`, `await_compile`, `compile_preflight`, `configure_objects`, `console_mark`, `delete_object`, `discover_tools`, `get_console_since`, `get_test_results`, `lint_playtest`, `lint_scene_refs`, `mcp_status`, `permission_prompt`, `reconnect_unity`, `release_smoke`, `resolve_tool_schema`, `run_playtest`, `run_tests`, `run_tests_wait`, `scene`, `screenshot`, `search_scene`, `set_active`, `set_parent`, `setup_objects`, `sync_unity`, `undo_last`, `validate_references`

**Gated: MUST call `discover_tools(category="X")` first:**

```
discover_tools(category="SCENE")       # spatial tools, find_objects, set_material, scene_diff
discover_tools(category="COMPONENTS")  # auto_wire, wire_event, unwire_event
discover_tools(category="ASSETS")      # asset, material, prefab, scriptable_object, shader
discover_tools(category="MEDIA")       # animation, animator, timeline, particle, UI, VFX
discover_tools(category="VERIFY")      # diagnose, scan_scene, scene_health, serialized_field_rename_audit
discover_tools(category="RUNTIME")     # debug, get_frame_stats, profile, watch, invoke_method
discover_tools(category="TESTS")       # test_step, get_test_count, get_test_progress, lint_playtest_suite
discover_tools(category="SYSTEM")      # auto_fix, smart_build, sessions, skills/templates
```

Calling a gated tool without discover → `tool not found` error.

## Direct-Only Tools (NEVER use in batch)

31 tools are `direct_only` — rejected by `batch()` with error. Call as standalone MCP tool only.

**Note:** `configure_objects` and `setup_objects` are TIER1 but also `direct_only` — never nest them inside `batch()`.

`animator_intent`, `ask`, `await_compile`, `budget_status`, `configure_objects`, `console_mark`, `debug`, `discover_tools`, `do`, `doctor`, `get_console_since`, `get_metrics`, `lint_playtest_suite`, `list_connections`, `list_skills`, `list_templates`, `mcp_status`, `navmesh_query`, `release_smoke`, `resolve_tool_schema`, `run_playtest_suite`, `run_tests_wait`, `screenshot_baseline`, `screenshot_compare`, `set_properties`, `setup_objects`, `snapshot`, `ui_intent`, `validate_playtest_aliases`, `vfx_intent`, `watch`

## Batch Envelope Result (v0.92)

When ANY inner command returns `ok:false`, the outer batch response also returns `ok:false`. Always check the `ok` field of the batch response to confirm all operations succeeded.

## Anti-Patterns

| Bad | Good |
|-----|------|
| 3x `get_component` | `inspect(paths=..., components=...)` |
| N sequential `set_property` on same object | `configure_objects(...)` |
| `recompile → sleep → get_console` | `await_compile` |
| 5 manual verify steps after mutation | `verify_after_change(mark_id=...)` |
| `batch(commands="configure_objects\n...")` | `configure_objects(...)` is direct_only, standalone only |
| `batch(commands="setup_objects\n...")` | `setup_objects(...)` is direct_only, standalone only |
| Gated tool without discover_tools | `discover_tools(category="X")` first |
