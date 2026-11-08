# Tools Reference

Tools are organized into a 13-tool Core and ten task-oriented categories. Use the live catalog to discover the tools available in your installed version.

## How Tools Work

**TIER1 tools** — Always visible to your AI assistant.

**Category-gated tools** — Enable via `discover_tools(category, enable=True)` or through the Unity Biome MCP Settings panel.

**Custom plugin categories** — Tools registered to an explicit custom category
are hidden until that category is enabled. A plugin that omits category
registration does not receive isolated category gating.

## Categories Overview

| Category | Purpose |
|----------|---------|
| **Core** | 13 always-visible tools for hierarchy inspection, component access, object changes, batching, and verification |
| **SCENE** | Scene and object lifecycle, hierarchy queries, spatial context, and scene changes |
| **COMPONENTS** | Component references and event wiring |
| **ASSETS** | Prefabs, materials, shaders, ScriptableObjects, and project settings |
| **UGUI** | Canvas-based UI creation, layout, event validation, and uGUI authoring |
| **UITOOLKIT** | UI Toolkit (UXML/USS) inspection, validation, and VisualElement manipulation |
| **MEDIA** | Animation, Timeline, particles, screenshots, rendering |
| **VERIFY** | Compile checks, scene validation, diagnostics, and post-change verification |
| **RUNTIME** | Play Mode state, methods, watches, debugging, and profiling |
| **TESTS** | Unity tests, Playtest execution, linting, and alias synchronization |
| **SYSTEM** | Connection, sessions, skills, permissions, intent tools, and maintenance |

## Quick Links by Task

### I want to...

**Inspect my scene**
- [get_hierarchy](scene.md#get_hierarchy) — Tree view of all GameObjects
- [search_scene](scene.md#search_scene) — Find objects by name/component/tag
- [get_component](objects.md#get_component) — Read component properties

**Create & modify objects**
- [create_object](objects.md#create_object) — Spawn new GameObjects
- [set_property](objects.md#set_property) — Change component values
- [manage_component](objects.md#manage_component) — Add/remove components
- [batch](batch.md#batch) — Run compatible operations in one call

**Work with prefabs**
- [prefab (save)](assets.md#prefab) — Convert scene instance → prefab asset
- [prefab (edit)](assets.md#prefab) — Modify prefab without unpacking
- [prefab (apply/revert)](assets.md#prefab) — Push/discard instance changes

**Run tests & playtests**
- [run_playtest](../features/playtest.md#run_playtest-parameters) — Execute DSL-based test scenarios
- [run_tests](tests.md#run_tests) — Low-level nonblocking NUnit dispatch
- [run_tests_wait](tests.md#run_tests_wait) — Preferred interactive correlated NUnit runner
- [get_test_run](tests.md#get_test_run) — Poll one exact durable NUnit run
- [resolve_test_request](tests.md#resolve_test_request) — Resolve a lost start acknowledgment
- [cancel_test_run](tests.md#cancel_test_run) — Cancel one exact durable NUnit run
- [list_test_runs](tests.md#list_test_runs) — Inspect recent durable NUnit runs
- [test_step](runtime.md#test_step) — Single assertion within a test

**Take screenshots**
- [screenshot](screenshots.md#screenshot) — Capture game view with annotations
- [screenshot_baseline](screenshots.md#screenshot_baseline) — Save reference image
- [screenshot_compare](screenshots.md#screenshot_compare) — Visual diff against baseline

**Debug & Verify**
- [doctor](diagnostics.md#doctor) — Health check with optional stale-file cleanup
- [verify_after_change](diagnostics.md#verify_after_change) — 5-gate verification (compile, errors, console, tests, playtests)
- [scan_scene](spatial.md#scan_scene) — Scene infrastructure audit
- [scene_health](diagnostics.md#scene_health) — Hierarchy and health checks
- [validate_references](diagnostics.md#validate_references) — ObjectReference field validation
- [resolve_scene_refs](diagnostics.md#resolve_scene_refs) — Resolve paths and aliases
- [lint_scene_refs](diagnostics.md#lint_scene_refs) — Lint DSL and batch references
- [get_console](diagnostics.md#get_console) — Read console errors & warnings
- [get_compile_errors](diagnostics.md#get_compile_errors) — C# compile status
- [reconnect_unity](diagnostics.md#reconnect_unity) — Restart TCP connection

**Advanced: Animation & VFX**
- [animator_intent](../features/intent-tools.md) — Setup animation controller
- [vfx_intent](../features/intent-tools.md) — Natural language VFX control

**Advanced: Code analysis**
- [compile_preflight](diagnostics.md#compile_preflight) — Validate C# before write
- [execute_code](../features/code-execution.md#basic-usage) — Run bounded C# in Unity

## TIER1 Tools (Always Available)

**Core (13):**
- get_hierarchy, get_component, inspect, set_property, create_object
- manage_component, batch, editor, get_console, get_compile_errors
- execute_code, compile_preflight, mcp_status

**Other TIER1 tools:** Run `discover_tools(enable=False, structured=True)` and look for entries tagged `tier1`. This keeps the reference aligned with the installed version.

## Cataloged Tools

These tools are declared once in the repository's tool catalog (`dsl/tools.biome`), and this table is printed from it. **Plane** is
where the tool runs: `unity` (the Editor handles the command) or `python` (the MCP server alone). **Effect**: `RO` read-only, `RW`
writes, `RW_IDEM` writes and is safe to repeat, `DEL` destructive. `discover_tools(enable=False, structured=True)` stays the live
reference for every tool.

<!-- biome:begin catalog -->
| Tool | Plane | Category | Effect |
|---|---|---|---|
| `alias_status` | unity | SYSTEM | RO |
| `analyze_lod_culling` | unity | MEDIA | RO |
| `animation` | unity | MEDIA | RW |
| `animator` | unity | MEDIA | RW |
| `animator_intent` | python | SYSTEM | RW |
| `apply_scene_change` | python | SCENE | RW |
| `apply_template` | python | SYSTEM | RW |
| `ask` | python | SYSTEM | RO |
| `ask_user` | unity | SYSTEM | RO |
| `asset` | unity | ASSETS | RW |
| `attach_uitk` | unity | UITOOLKIT | RW |
| `auto_fix` | python | SYSTEM | RO |
| `auto_wire` | unity | COMPONENTS | RW |
| `autofit_collider` | unity | SCENE | RW |
| `await_compile` | python | VERIFY | RO |
| `bake` | unity | ASSETS | RW |
| `batch` | unity | CORE | RW |
| `brief_build` | python | SYSTEM | RO |
| `budget_status` | python | SYSTEM | RO |
| `build` | unity | SYSTEM | RW |
| `cancel_test_run` | unity | TESTS | RW_IDEM |
| `check_colliders` | unity | SCENE | RO |
| `checkpoint` | unity | SYSTEM | RW |
| `checkpoint_create` | python | SYSTEM | RW |
| `checkpoint_restore` | python | SYSTEM | DEL |
| `compile_preflight` | unity | VERIFY | RO |
| `configure_objects` | python | SCENE | RW |
| `console_mark` | python | RUNTIME | RO |
| `create_object` | unity | CORE | RW |
| `create_ui` | unity | UGUI | RW |
| `debug` | python | RUNTIME | RO |
| `debug_animator` | unity | RUNTIME | RO |
| `debug_physics` | unity | RUNTIME | RO |
| `delete_object` | unity | SCENE | DEL |
| `diagnose` | unity | VERIFY | RO |
| `discover_tools` | python | SYSTEM | RO |
| `do` | python | SYSTEM | RW |
| `doctor` | python | SYSTEM | RW |
| `editor` | unity | CORE | RW |
| `execute_code` | unity | SYSTEM | RW |
| `export_playtest_aliases_to_defs` | unity | TESTS | RW |
| `find_objects` | unity | SCENE | RO |
| `fingerprint` | unity | SYSTEM | RO |
| `get_capabilities` | unity | SYSTEM | RO |
| `get_changes` | unity | SYSTEM | RW |
| `get_changeset` | python | VERIFY | RO |
| `get_compile_errors` | unity | CORE | RO |
| `get_component` | unity | CORE | RO |
| `get_components_list` | unity | SCENE | RO |
| `get_console` | unity | CORE | RO |
| `get_console_since` | python | RUNTIME | RO |
| `get_enabled_tools` | unity | SYSTEM | RO |
| `get_frame_stats` | unity | RUNTIME | RO |
| `get_hierarchy` | unity | CORE | RO |
| `get_memory` | unity | RUNTIME | RO |
| `get_metrics` | python | RUNTIME | RW |
| `get_object_detail` | unity | SCENE | RO |
| `get_schema` | unity | SYSTEM | RO |
| `get_selection` | unity | SCENE | RO |
| `get_spatial_context` | unity | SCENE | RO |
| `get_test_count` | unity | TESTS | RO |
| `get_test_progress` | unity | TESTS | RO |
| `get_test_results` | unity | TESTS | RO |
| `get_test_run` | unity | TESTS | RO |
| `get_unity_events` | unity | SCENE | RO |
| `get_watches` | unity | RUNTIME | RO |
| `inspect` | unity | CORE | RO |
| `inspect_uitk` | unity | UITOOLKIT | RO |
| `invoke_method` | unity | RUNTIME | RW |
| `lint_playtest` | unity | TESTS | RO |
| `lint_playtest_suite` | python | TESTS | RO |
| `lint_scene_refs` | unity | VERIFY | RO |
| `lint_ugui` | unity | UGUI | RO |
| `lint_uitk` | unity | UITOOLKIT | RO |
| `list_connections` | python | SYSTEM | RO |
| `list_events` | unity | COMPONENTS | RO |
| `list_skills` | python | SYSTEM | RO |
| `list_templates` | python | SYSTEM | RO |
| `list_test_runs` | unity | TESTS | RO |
| `load_session` | python | SYSTEM | RO |
| `manage_component` | unity | CORE | RW |
| `material` | unity | ASSETS | RW |
| `material_audit` | unity | ASSETS | RO |
| `mcp_status` | python | SYSTEM | RO |
| `menu` | unity | SYSTEM | RW |
| `move_to` | unity | RUNTIME | RW |
| `navmesh_query` | python | SCENE | RW |
| `object_diff` | unity | SCENE | RO |
| `package` | unity | ASSETS | RW |
| `particle` | unity | MEDIA | RW |
| `permission_prompt` | python | SYSTEM | RO |
| `ping_object` | unity | SCENE | RO |
| `prefab` | unity | ASSETS | RW |
| `profile` | unity | RUNTIME | RW |
| `project_settings` | unity | ASSETS | RW_IDEM |
| `query_state` | unity | RUNTIME | RO |
| `recompile` | unity | SYSTEM | RW |
| `reconnect_unity` | python | SYSTEM | RO |
| `references` | unity | COMPONENTS | RW |
| `region_clear` | unity | SCENE | RW |
| `release_smoke` | python | SYSTEM | RO |
| `rename_object` | unity | SCENE | RW_IDEM |
| `render_analyze` | unity | MEDIA | RO |
| `resolve_scene_refs` | unity | VERIFY | RO |
| `resolve_test_request` | unity | TESTS | RO |
| `resolve_tool_schema` | python | SYSTEM | RO |
| `run_playtest` | unity | TESTS | RW |
| `run_playtest_suite` | python | TESTS | RW |
| `run_tests` | unity | TESTS | RW_IDEM |
| `run_tests_wait` | python | TESTS | RW |
| `runtime_snapshot` | unity | RUNTIME | RO |
| `save_session` | python | SYSTEM | RW |
| `save_skill` | python | SYSTEM | RW |
| `save_template` | python | SYSTEM | RW_IDEM |
| `scan_scene` | unity | VERIFY | RO |
| `scene` | unity | SCENE | DEL |
| `scene_change_plan` | python | SCENE | RW |
| `scene_diff` | unity | SCENE | RO |
| `scene_environment` | unity | SCENE | RW |
| `scene_health` | unity | VERIFY | RO |
| `screenshot` | unity | MEDIA | RW |
| `screenshot_baseline` | python | MEDIA | RW |
| `screenshot_compare` | python | MEDIA | RW |
| `scriptable_object` | unity | ASSETS | RW |
| `search_scene` | unity | SCENE | RO |
| `serialized_field_rename_audit` | unity | VERIFY | RO |
| `set_active` | unity | SCENE | RW_IDEM |
| `set_material` | unity | SCENE | RW_IDEM |
| `set_parent` | unity | SCENE | RW_IDEM |
| `set_properties` | python | SCENE | RW |
| `set_property` | unity | CORE | RW_IDEM |
| `set_property_delta` | unity | SCENE | RW |
| `set_rect` | unity | UGUI | RW_IDEM |
| `set_sibling_index` | unity | SCENE | RW |
| `setup_objects` | python | SCENE | RW |
| `shader` | unity | ASSETS | RW |
| `smart_build` | python | SYSTEM | RW |
| `snapshot` | python | RUNTIME | RO |
| `spatial_query` | unity | SCENE | RO |
| `sync_playtest_aliases_from_defs` | unity | TESTS | RW |
| `test_step` | unity | TESTS | RW |
| `timeline` | unity | MEDIA | RW |
| `transfer_object` | unity | SCENE | RW |
| `ui_intent` | python | UGUI | RW |
| `uitk_element` | unity | UITOOLKIT | RW |
| `uitk_file` | unity | UITOOLKIT | RW |
| `uitk_intent` | python | UITOOLKIT | RW |
| `undo_last` | unity | SYSTEM | RW |
| `unwire_event` | unity | COMPONENTS | DEL |
| `use_skill` | python | SYSTEM | RW |
| `validate_playtest_aliases` | unity | TESTS | RO |
| `validate_references` | unity | VERIFY | RO |
| `validate_triggers` | unity | SCENE | RO |
| `verify_after_change` | python | VERIFY | RW |
| `vfx_intent` | python | MEDIA | RW |
| `wait_until` | unity | RUNTIME | RW_IDEM |
| `wire_event` | unity | COMPONENTS | RW |
<!-- biome:end catalog -->

## Enabling Tools by Category

To unlock advanced tools, enable the category:

```python
# Enable Canvas-based (uGUI) UI tools
await discover_tools("UGUI", enable=True)

# Enable UI Toolkit (UXML/USS) tools
await discover_tools("UITOOLKIT", enable=True)

# Enable animation, Timeline, particles, screenshots, rendering
await discover_tools("MEDIA", enable=True)

# Enable validation and compile tools
await discover_tools("VERIFY", enable=True)

# Enable asset tools (prefab, material, scriptable_object, etc.)
await discover_tools("ASSETS", enable=True)
```

After enabling, the tools appear in the advertised tool list. Category gating
controls discovery and context budget; it is not authorization, and a client
that already knows a hidden Python tool name may still call it. Use the
[security controls](../settings.md#tools-and-permissions) when access must be
restricted.

**Available categories:**
- `SCENE`
- `COMPONENTS`
- `ASSETS`
- `UGUI` (Canvas-based UI)
- `UITOOLKIT` (UI Toolkit / UXML)
- `MEDIA`
- `VERIFY`
- `RUNTIME`
- `TESTS`
- `SYSTEM`

Run `discover_tools(enable=False, structured=True)` to inspect the current catalog, including each tool's supported surfaces. Legacy category aliases remain available with `include_legacy=True`.

Custom plugin categories use the same session gate. For example, enable a
plugin registered as `my_plugin` with
`discover_tools(category="my_plugin", enable=True)`.

## Batch: Combine Operations for Token Savings

Only tools reported with `surfaces=direct,batch` can be batched. Direct-only tools must be called through their typed MCP interface.

```python
# Before: 3 calls
await create_object(name="Player")
await set_property(path="Player", component="Transform", prop="position", value="0,1,0")
await get_component(path="Player", type="Transform")

# After: 1 batch call (text DSL format)
result = await batch("""
create_object name=Player
set_property path=Player component=Transform prop=position value=0,1,0
get_component path=Player type=Transform
""")
```

Use `discover_tools(enable=False, structured=True)` as the source of truth for
batch eligibility. See [Batch Reference](batch.md) for command syntax, failure
handling, and rollback behavior.

## Tool Status & Discovery

Check which tools are currently enabled:

```python
# Get all enabled tools in current session
await get_enabled_tools()

# Auto-discover available tools
await discover_tools()
```

This helps your AI assistant optimize its decision tree — it only offers tools that are actually available in your project.

## Troubleshooting: "Tool not found"

**Before opening an issue:**

1. Is the tool's category enabled?
   ```python
catalog = await discover_tools(enable=False, structured=True)
# Find the tool's category in the catalog, then enable it:
await discover_tools("MEDIA", enable=True)
   ```

2. Is the MCP connection alive?
   ```python
await list_connections()
   ```

3. Check for plugin errors:
   ```python
await get_console(level="Error,Exception,Assert")
   ```

4. Run diagnostics:
   ```python
await doctor(fix=True)
   ```

## Next Steps

- **[Scene Tools](scene.md)** — Inspect and modify scenes
- **[Object Tools](objects.md)** — Create, edit, and manage GameObjects
- **[Testing Tools](tests.md)** — Run and manage NUnit tests
- **[Spatial Tools](spatial.md)** — Analyze geometry, colliders, and layout
- **[Batch Operations](batch.md)** — Combine multiple tools for token savings
- **[Animation Tools](animation.md)** — Animation clips, timelines, state machines
- **[Shader & Material Tools](shaders.md)** — Material properties and shader control
- **[UI Tools](ui.md)** — Create and layout UI elements
- **[Screenshot Tools](screenshots.md)** — Capture and compare visual states
- **[Component Tools](components.md)** — Component lifecycle and wiring
- **[Playtest Guide](../features/playtest.md)** — Automated scenarios and DSL reference
- **[Asset Tools](assets.md)** — Prefabs, materials, ScriptableObjects
- **[Diagnostics](diagnostics.md)** — Troubleshoot and debug
- **[System & Orchestration](system.md)** — Discover, synchronize, recover, and coordinate

---

**Live reference:** Run `discover_tools(enable=False, structured=True)`, then use `resolve_tool_schema(tools="tool_name")` for the installed tool's current parameters.
