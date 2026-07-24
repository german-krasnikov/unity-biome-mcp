---
description: Complete Unity Biome MCP reference — tool catalog, tier system, batch syntax
---

# Unity Biome MCP Tool Reference

142 public tools. 45 always-visible (15 CORE + 30 TIER1). All others gated via `discover_tools(category)`.
Categories: **SCENE**, **COMPONENTS**, **ASSETS**, **MEDIA**, **VERIFY**, **RUNTIME**, **TESTS**, **SYSTEM**.

---

## CORE Tools (15) — Always Available, Full Schema

| Tool | Purpose | Key Params |
|------|---------|------------|
| `apply_scene_change` | Execute planned mutations with post-verify | `plan_id` |
| `batch` | Run 2+ ops; `atomic=true` uses Unity Undo | `commands`, `on_error`, `atomic`, `validate_aliases` |
| `create_object` | Spawn GameObject | `name`, `parent`, `components`, `primitive`, `prefab_path` |
| `editor` | Play mode control / window open | `action` (state/play/pause/stop/select) |
| `execute_code` | Run C# in Editor | `code`, `timeout` |
| `get_compile_errors` | List C# compile errors | — |
| `get_component` | Read component values | `path`, `type`, `fields`, `compress` |
| `get_console` | Read Editor.log tail | `count`, `level`, `keyword` |
| `get_hierarchy` | Read scene tree | `depth`, `root`, `filter`, `summary`, `compress` |
| `inspect` | Batch-read N objects' components | `paths`, `components`, `fields`, `compress` |
| `manage_component` | Add/remove components | `path`, `type`, `action` (add/remove) |
| `resolve_scene_refs` | Resolve $alias /path t:Type refs | — |
| `scene_change_plan` | Preflight + checkpoint before mutations | — |
| `set_property` | Write component value — **value ALWAYS string** | `path`, `component`, `prop`, `value` |
| `verify_after_change` | 5-gate: compile→errors→console→tests→playtests | — |

---

## TIER1 Tools (30) — Always Available, Deferred Schema

| Tool | Cat | Purpose | Flags |
|------|-----|---------|-------|
| `alias_status` | SYSTEM | Alias cache state (loaded/count/source/stale) | |
| `ask` | SYSTEM | Query LLM about scene | direct_only |
| `ask_user` | SYSTEM | Prompt human for input | |
| `await_compile` | VERIFY | Block until compile done | direct_only |
| `compile_preflight` | VERIFY | Roslyn validate ~200ms | |
| `configure_objects` | SCENE | Batch configure components (JSON) | direct_only |
| `console_mark` | RUNTIME | Watermark for log slicing | direct_only |
| `delete_object` | SCENE | Remove GameObject | |
| `discover_tools` | SYSTEM | Get schemas; canonical order: SCENE/COMPONENTS/ASSETS/MEDIA/VERIFY/RUNTIME/TESTS/SYSTEM; `include_legacy=False` default; `structured=True` for typed response | direct_only |
| `get_console_since` | RUNTIME | Console entries after mark_id | direct_only |
| `get_test_results` | TESTS | Poll NUnit test status | |
| `lint_playtest` | TESTS | Static DSL preflight | |
| `lint_scene_refs` | VERIFY | 3-pass linter for refs in DSL/batch | |
| `mcp_status` | SYSTEM | Scene/compile/playmode/alias snapshot | direct_only |
| `permission_prompt` | SYSTEM | Gate sensitive ops | |
| `reconnect_unity` | SYSTEM | Reconnect TCP socket | |
| `release_smoke` | SYSTEM | status + aliases + compile gates in one call | direct_only |
| `resolve_tool_schema` | SYSTEM | Fetch deferred tool schema | direct_only |
| `run_playtest` | TESTS | Run playtest DSL | `script` OR `path`, `abort_on_fail` |
| `run_tests` | TESTS | NUnit tests — returns immediately, poll `get_test_results` | |
| `run_tests_wait` | TESTS | NUnit tests — blocks until done (2min cap) | direct_only |
| `scene` | SCENE | List/load/save/close scenes | `action`, `path` |
| `screenshot` | MEDIA | Capture frame | `width`, `height`, `camera`, `path` |
| `search_scene` | SCENE | Find objects by pattern | `query`, `root`, `limit` |
| `set_active` | SCENE | Toggle active flag | `path`, `active` |
| `set_parent` | SCENE | Reparent GameObject | `path`, `parent` |
| `setup_objects` | SCENE | Batch create + wire objects (JSON template) | direct_only |
| `sync_unity` | SYSTEM | Reload/restart | |
| `undo_last` | SYSTEM | Revert last N editor operations | `turns` |
| `validate_references` | VERIFY | Check all refs valid | |

---

## Tier2 — Category Gated (`discover_tools category="X"`)

### SCENE (30)
`autofit_collider`, `check_colliders`, `find_objects`, `get_components_list`, `get_object_detail`, `get_selection`, `get_spatial_context`, `get_unity_events`, `navmesh_query`*, `object_diff`, `ping_object`, `region_clear`, `rename_object`, `scene_diff`, `scene_environment`, `set_material`, `set_properties`*, `set_property_delta`, `set_sibling_index`, `spatial_query`, `transfer_object`
*(+configure_objects, delete_object, scene, search_scene, set_active, set_parent, setup_objects in TIER1; apply_scene_change, scene_change_plan in CORE)*

### COMPONENTS (4)
`auto_wire`, `references`, `unwire_event`, `wire_event`

### ASSETS (7)
`asset`, `material`, `material_audit`, `prefab`, `project_settings`, `scriptable_object`, `shader`

### MEDIA (14)
`animation`, `animator`, `analyze_lod_culling`, `create_ui`, `particle`, `render_analyze`, `screenshot_baseline`*, `screenshot_compare`*, `set_rect`, `timeline`, `ui_intent`*, `validate_layout`, `vfx_intent`*
*(+screenshot in TIER1)*

### VERIFY (10)
`diagnose`, `scan_scene`, `scene_health`, `serialized_field_rename_audit`
*(+4 in TIER1: await_compile, compile_preflight, lint_scene_refs, validate_references; +2 in CORE: resolve_scene_refs, verify_after_change)*

### RUNTIME (18)
`debug`*, `debug_animator`, `debug_physics`, `get_frame_stats`, `get_memory`, `get_metrics`*, `get_watches`, `invoke_method`, `move_to`, `profile`, `query_state`, `runtime_snapshot`, `set_runtime_property`, `snapshot`*, `wait_until`, `watch`*
*(+console_mark, get_console_since in TIER1)*

### TESTS (13)
`export_playtest_aliases_to_defs`, `get_test_count`, `get_test_progress`, `lint_playtest_suite`*, `run_playtest_suite`*, `sync_playtest_aliases_from_defs`, `test_step`, `validate_playtest_aliases`*
*(+run_tests, run_tests_wait, run_playtest, lint_playtest, get_test_results in TIER1)*

### SYSTEM (36)
`animator_intent`*, `auto_fix`, `budget_status`*, `checkpoint`, `do`*, `doctor`*, `fingerprint`, `get_capabilities`, `get_changes`, `get_enabled_tools`, `get_schema`, `list_connections`*, `list_skills`*, `list_templates`*, `load_session`, `menu`, `recompile`, `save_session`, `save_skill`, `save_template`, `apply_template`, `set_llm_config`, `smart_build`, `use_skill`
*(+alias_status, ask, ask_user, discover_tools, mcp_status, permission_prompt, reconnect_unity, release_smoke, resolve_tool_schema, sync_unity, undo_last in TIER1; execute_code in CORE)*

`*` = **direct_only**

---

## Flags

### direct_only (31) — typed MCP call only, NEVER inside `batch`
`animator_intent`, `ask`, `await_compile`, `budget_status`, `configure_objects`, `console_mark`, `debug`, `discover_tools`, `do`, `doctor`, `get_console_since`, `get_metrics`, `lint_playtest_suite`, `list_connections`, `list_skills`, `list_templates`, `mcp_status`, `navmesh_query`, `release_smoke`, `resolve_tool_schema`, `run_playtest_suite`, `run_tests_wait`, `screenshot_baseline`, `screenshot_compare`, `set_properties`, `setup_objects`, `snapshot`, `ui_intent`, `validate_playtest_aliases`, `vfx_intent`, `watch`

### runtime_only (11) — Play Mode required
`debug_animator`, `debug_physics`, `get_frame_stats`, `invoke_method`, `move_to`, `profile`, `query_state`, `run_playtest`, `set_runtime_property`, `test_step`, `wait_until`

---

## Key Patterns

- **batch-first**: always `batch` for 2+ ops; reduces round-trips; if ANY inner command returns `ok:false`, the outer batch envelope also returns `ok:false`
- **gated tools**: call `discover_tools(category="X")` before first use
- **log isolation**: `console_mark` → mutation → `get_console_since(mark_id)`
- **verify gate**: `verify_after_change` after multi-step mutations
- **test polling**: `run_tests` returns immediately → poll `get_test_results` every 5s; or use `run_tests_wait`
- **value always string**: `"5"`, `"true"`, `"(1,2,3)"`; path starts with `/`
- **inspect > get_component** for reading N objects at once

---

---

## Legacy Category Aliases (DeprecationWarning v0.84.0+)

`object`/`SCENE_EDIT`→`SCENE+COMPONENTS` | `animation`/`ui`/`vfx`/`rendering`→`MEDIA` | `asset`/`SHADERS_MATERIAL`→`ASSETS` | `runtime`/`UNIT_TESTS`→`RUNTIME+TESTS` | `profiling`/`perf`/`debug`→`RUNTIME` | `advanced`/`connection`/`session`/`plugins`/`meta`→`SYSTEM`
