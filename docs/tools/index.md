# Tools Reference

126 MCP tools organized by category. Every tool is documented with parameters, examples, and real-world usage patterns.

## How Tools Work

**TIER1 tools (43 core)** — Always visible to your AI assistant.

**Category-gated tools** — Enable via `discover_tools(category, enable=True)` or through the Unity MCP Settings panel.

**Unknown tools** — Plugin-registered tools pass through automatically.

## Categories Overview

| Category | Tools | Purpose |
|----------|-------|---------|
| **Core** | 24 tools | Hierarchy inspection, component access, object CRUD, batch operations, diagnostics |
| **Scene** | Scene tools | Open/close/save scenes, take screenshots, checkpoint/diff states |
| **Objects** | 10 tools | Advanced object queries, find/filter, spatial context, collider validation |
| **Animation** | 4 tools | Animation clips, timelines, animator state machines, particles |
| **Assets** | 6 tools | Manage prefabs, materials, ScriptableObjects, project settings, dependencies |
| **Code Intel** | 5 tools | Symbol lookup, compile checking, semantic queries, error detection |
| **Runtime** | 8 tools | Runtime property modification, method invocation, playtest execution, physics queries |
| **UI** | 5 tools | Create UI elements, layout configuration, spatial context, intent-driven UI |
| **Advanced** | 15+ tools | Execution profiling, reference validation, auto-fix, spatial queries |
| **Session** | 11 tools | Save/load state snapshots, visual baselines, skill/template management |
| **Connection** | 2 tools | TCP status, reconnect, multi-instance management |

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
- [batch](batch.md#batch) — Do 2+ operations in one call (93% token savings!)

**Work with prefabs**
- [prefab (save)](assets.md#prefab) — Convert scene instance → prefab asset
- [prefab (edit)](assets.md#prefab) — Modify prefab without unpacking
- [prefab (apply/revert)](assets.md#prefab) — Push/discard instance changes

**Run tests & playtests**
- [run_playtest](runtime.md#run_playtest) — Execute DSL-based test scenarios
- [fuzz_playtest](runtime.md#fuzz_playtest) — Random stress testing
- [run_tests](scene.md#run_tests) — Execute NUnit tests
- [test_step](runtime.md#test_step) — Single assertion within a test

**Take screenshots**
- [screenshot](scene.md#screenshot) — Capture game view with annotations
- [screenshot_baseline](scene.md#screenshot_baseline) — Save reference image
- [screenshot_compare](scene.md#screenshot_compare) — Visual diff against baseline

**Debug problems**
- [doctor](diagnostics.md#doctor) — Health check with auto-fix
- [get_console](diagnostics.md#get_console) — Read console errors & warnings
- [get_compile_errors](diagnostics.md#get_compile_errors) — C# compile status
- [reconnect_unity](diagnostics.md#reconnect_unity) — Restart TCP connection

**Advanced: Animation & VFX**
- [animator_intent](../features/intent-tools.md#animator_intent) — Setup animation controller
- [vfx_intent](runtime.md#vfx_intent) — Natural language VFX control

**Advanced: Code analysis**
- [find_references](diagnostics.md#find_references) — Locate all usages of a symbol
- [compile_preflight](diagnostics.md#compile_preflight) — Validate C# before write
- [semantic_at](diagnostics.md#semantic_at) — Get type info at file position

## TIER1 Tools (Always Available)

**Core (24):**
- get_hierarchy, get_component, inspect, set_property, create_object, delete_object
- manage_component, batch, set_parent, scene, search_scene, editor
- get_console, get_compile_errors, get_enabled_tools, discover_tools
- do, ask, ask_user, permission_prompt
- reconnect_unity, list_connections, resolve_tool_schema, doctor

**Non-Core TIER1 (18):**
- screenshot, run_tests, setup_objects, set_properties, configure_objects
- find_references, compile_preflight, semantic_at, await_compile, sync_unity
- invoke_method, set_runtime_property, wait_until, move_to, query_state
- test_step, run_playtest, fuzz_playtest

## Enabling Tools by Category

To unlock advanced tools, enable the category:

```python
# Enable all Animation tools
await discover_tools("animation", enable=True)

# Enable Advanced tools (find_references, auto_fix, spatial queries, etc.)
await discover_tools("advanced", enable=True)

# Enable Asset tools (prefab, material, scriptable_object, etc.)
await discover_tools("asset", enable=True)
```

After enabling, the tools appear in your AI's tool list and become callable.

**Available categories:**
- `object` — Advanced object queries
- `animation` — Animation & timeline control
- `asset` — Prefab, material, ScriptableObject CRUD
- `advanced` — Roslyn code intel, reference validation, spatial queries
- `ui` — UI element creation & layout
- `runtime` — Runtime property access
- `session` — State snapshots, visual baselines, templates
- `connection` — TCP status, multi-instance management
- `debug` — Runtime debugging, watches, performance snapshots
- `profiling` — Frame stats, memory analysis, performance profiling
- `rendering` — LOD culling analysis, render optimization
- `perf` — Profiling & rendering combined
- `plugins` — Plugin-registered tools (auto-discovered)

## Batch: Combine Operations for Token Savings

Any read OR write can be batched. Combine 2+ operations into one call:

```python
# Before: 3 calls
await create_object(name="Player")
await set_property(path="Player", component="Transform", prop="position", value="0,1,0")
await get_component(path="Player", component="Transform")

# After: 1 batch call (text DSL format)
result = await batch("""
create_object name=Player
set_property path=Player component=Transform prop=position value=0,1,0
get_component path=Player component=Transform
""")
```

**Result:** 93% fewer tokens, same outcome.

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
   await discover_tools("advanced", enable=True)
   ```

2. Is the MCP connection alive?
   ```python
   await list_connections()
   ```

3. Check for plugin errors:
   ```python
   await get_console(level="Error,Exception,Assert")  # All problem levels (per PROBLEM_LEVELS convention)
   ```

4. Run diagnostics:
   ```python
   await doctor(fix=True)
   ```

## Next Steps

- **[Scene Tools](scene.md)** — Inspect and modify scenes
- **[Object Tools](objects.md)** — Create, edit, and manage GameObjects
- **[Batch Operations](batch.md)** — Combine multiple tools for token savings
- **[Animation Tools](animation.md)** — Animation clips, timelines, state machines
- **[Shader & Material Tools](shaders.md)** — Material properties and shader control
- **[UI Tools](ui.md)** — Create and layout UI elements
- **[Screenshot Tools](screenshots.md)** — Capture and compare visual states
- **[Component Tools](components.md)** — Component lifecycle and wiring
- **[PlayTest Tools](runtime.md)** — Automated testing with DSL
- **[Asset Tools](assets.md)** — Prefabs, materials, ScriptableObjects
- **[Diagnostics](diagnostics.md)** — Troubleshoot and debug

---

**Complete reference:** See [mcp-server.md](../../AI/mcp-server.md) in the architecture docs for implementation details.
