# Tools Reference

Tools are organized into a 13-tool Core and ten task-oriented categories. Use the live catalog to discover the tools available in your installed version.

## How Tools Work

**TIER1 tools** — Always visible to your AI assistant.

**Category-gated tools** — Enable via `discover_tools(category, enable=True)` or through the Unity Biome MCP Settings panel.

**Unknown tools** — Plugin-registered tools pass through automatically.

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

After enabling, the tools appear in your AI's tool list and become callable.

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

See [Batch Reference](batch.md) for all batch-eligible commands.

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
