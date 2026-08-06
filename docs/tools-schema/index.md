---
hide:
  - navigation
---

# MCP Tool Schema

> **148 registered tools** — auto-generated from server tool definitions.

> Quality: **72.8/100** avg score · [Glama](https://glama.ai/mcp/servers/german-krasnikov/unity-biome-mcp/schema)

## Overview

| Tool | Score | Risk | Description |
|------|-------|------|-------------|
| [`alias_status`](#alias_status) | 🟢 89/100 | 🟢 low | Check alias table health: loaded/empty/stale, sources, and total alias count. |
| [`analyze_lod_culling`](#analyze_lod_culling) | 🟢 83/100 | 🟢 low | LOD group coverage + occlusion culling analysis. |
| [`animation`](#animation) | 🟡 64/100 | 🟡 medium | Animate GameObject properties via AnimationClip. Use when you need to read or... |
| [`animator`](#animator) | 🔴 18/100 | 🔴 high | Animator Controller — state machine (use `animation` for keyframe clips, `tim... |
| [`animator_intent`](#animator_intent) | 🟡 75/100 | 🟡 medium | Convert NL intent to Unity Animator Controller setup via DSL. |
| [`apply_scene_change`](#apply_scene_change) | 🟡 74/100 | 🟡 medium | Execute scene mutations with pre-check, post-verify, and optional save. |
| [`apply_template`](#apply_template) | 🟢 82/100 | 🟡 medium | Apply a scene template (.cs file from .claude/templates/). |
| [`ask`](#ask) | 🟢 87/100 | 🟢 low | Answer a read-only question about the Unity scene (AI-routed, not interactive... |
| [`ask_user`](#ask_user) | 🟢 87/100 | 🟢 low | Show a question card in Unity chat; wait for user answer (interactive UI — us... |
| [`asset`](#asset) | 🔴 37/100 | 🔴 high | Asset database. action: find|get_info|create|move|validate_move|duplicate|del... |
| [`auto_fix`](#auto_fix) | 🟢 89/100 | 🟢 low | Auto-detect and fix Unity errors. Uses MCP sampling to ask Claude for fixes. |
| [`auto_wire`](#auto_wire) | 🟡 72/100 | 🟡 medium | Fill null ObjectReference fields on a GameObject by matching field name or ty... |
| [`autofit_collider`](#autofit_collider) | 🟡 71/100 | 🟡 medium | Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule. |
| [`await_compile`](#await_compile) | 🟡 78/100 | 🟢 low | Block until Unity finishes compiling + reloading, then return compile errors. |
| [`bake`](#bake) | 🟢 86/100 | 🟢 low | Bake operations. |
| [`batch`](#batch) | 🟡 68/100 | 🟡 medium | Execute multiple commands in one call. Use for 2+ ops — reads AND writes. com... |
| [`budget_status`](#budget_status) | 🟢 89/100 | 🟢 low | Returns Haiku cost: session/cap/day/skipped features. Text format. |
| [`build`](#build) | 🟢 83/100 | 🟡 medium | Build player. action: build. |
| [`cancel_test_run`](#cancel_test_run) | 🟡 78/100 | 🟡 medium | Request cancellation of one exact test run; cancellation is asynchronous. |
| [`check_colliders`](#check_colliders) | 🟢 83/100 | 🟡 medium | Check collider issues: triggers without Rigidbody, negative scale, micro coll... |
| [`checkpoint`](#checkpoint) | 🟡 73/100 | 🟡 medium | Create a named Undo checkpoint. Use before major scene changes. Allows rollba... |
| [`compile_preflight`](#compile_preflight) | 🟢 80/100 | 🟡 medium | Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — ca... |
| [`configure_objects`](#configure_objects) | 🟢 87/100 | 🟡 medium | Configure multiple objects at once. |
| [`console_mark`](#console_mark) | 🟡 73/100 | 🟡 medium | Create a console watermark. Returns mark_id encoding current timestamp. |
| [`create_object`](#create_object) | 🟡 69/100 | 🟡 medium | Create new GameObject. components: comma-separated types to add on creation. ... |
| [`create_ui`](#create_ui) | 🔴 52/100 | 🟡 medium | Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image. ... |
| [`debug`](#debug) | 🟡 73/100 | 🟡 medium | AI-assisted scene debug: gather diagnostic context based on symptom (not comp... |
| [`debug_animator`](#debug_animator) | 🟢 82/100 | 🟡 medium | [Play Mode] Read Animator state: layers, transitions, parameters (use `debug`... |
| [`debug_physics`](#debug_physics) | 🟡 76/100 | 🟡 medium | [Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (us... |
| [`delete_object`](#delete_object) | 🔴 53/100 | 🔴 high | Delete GameObject by instance ID or scene path. Provide one. force=True to de... |
| [`diagnose`](#diagnose) | 🟢 81/100 | 🟢 low | Read Unity compile/reload fact-signals atomically; returns typed verdict. For... |
| [`discover_tools`](#discover_tools) | 🟢 80/100 | 🟢 low | Find and enable tools by category. |
| [`do`](#do) | 🟡 77/100 | 🟡 medium | Convert natural language intent into Unity scene operations. Use when scene s... |
| [`doctor`](#doctor) | 🟡 74/100 | 🟡 medium | Run health diagnostics. Use fix=True to auto-repair safe issues. |
| [`editor`](#editor) | 🟢 80/100 | 🟡 medium | Editor state/control. action: state|play|pause|stop|select|project_path. |
| [`execute_code`](#execute_code) | 🟡 61/100 | 🟡 medium | Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile. |
| [`export_playtest_aliases_to_defs`](#export_playtest_aliases_to_defs) | 🟢 80/100 | 🟡 medium | Export PlaytestConfig.asset aliases to a readable .defs text file. |
| [`find_objects`](#find_objects) | 🟡 76/100 | 🟡 medium | Find objects by criteria. Use search_scene for complex queries. Does NOT supp... |
| [`fingerprint`](#fingerprint) | 🟡 77/100 | 🟡 medium | Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens. |
| [`get_capabilities`](#get_capabilities) | 🟢 89/100 | 🟢 low | Unity version, platform, render pipeline, scripting backend, and optional pac... |
| [`get_changes`](#get_changes) | 🟡 74/100 | 🟡 medium | Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo, |
| [`get_compile_errors`](#get_compile_errors) | 🟢 89/100 | 🟡 medium | Compilation errors with file:line:column. Not lost on Console.Clear(). Struct... |
| [`get_component`](#get_component) | 🟡 68/100 | 🟡 medium | Component properties as key-value. For MULTIPLE objects, use inspect(paths='a... |
| [`get_components_list`](#get_components_list) | 🟡 79/100 | 🟢 low | List all components on object by instance ID. |
| [`get_console`](#get_console) | 🟡 68/100 | 🟢 low | Recent console logs. For C# compile errors use get_compile_errors instead. ke... |
| [`get_console_since`](#get_console_since) | 🟡 78/100 | 🟢 low | Console entries after the watermark created by console_mark(). |
| [`get_enabled_tools`](#get_enabled_tools) | 🟢 89/100 | 🟢 low | List enabled tool names, comma-separated. |
| [`get_frame_stats`](#get_frame_stats) | 🟢 82/100 | 🟢 low | Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No se... |
| [`get_hierarchy`](#get_hierarchy) | 🟡 61/100 | 🟡 medium | Scene hierarchy as text tree. For finding specific object by name/type use se... |
| [`get_memory`](#get_memory) | 🟢 82/100 | 🟢 low | Memory snapshot. |
| [`get_metrics`](#get_metrics) | 🔴 57/100 | 🔴 high | Returns telemetry snapshot. format: text|json. reset=true clears counters ato... |
| [`get_object_detail`](#get_object_detail) | 🟡 78/100 | 🟢 low | Get ALL components with ALL values. Heavy. Use get_component for single compo... |
| [`get_schema`](#get_schema) | 🟡 78/100 | 🟢 low | Get all serialized fields of a component type with types. Use before set_prop... |
| [`get_selection`](#get_selection) | 🟢 89/100 | 🟡 medium | Currently selected GameObject: path and component list. |
| [`get_spatial_context`](#get_spatial_context) | 🟡 76/100 | 🟡 medium | Collider info + approach vectors + nearby objects within radius. Raycast in P... |
| [`get_test_count`](#get_test_count) | 🟢 89/100 | 🟢 low | Number of edit-mode and play-mode tests in the project. |
| [`get_test_progress`](#get_test_progress) | 🟡 74/100 | 🟡 medium | Legacy progress facade. Pass run_id to correlate the response. |
| [`get_test_results`](#get_test_results) | 🟡 74/100 | 🟡 medium | Legacy result facade. Pass run_id to prevent reading a stale latest run. |
| [`get_test_run`](#get_test_run) | 🟡 78/100 | 🟡 medium | Return the durable JSON snapshot for one exact test run. |
| [`get_unity_events`](#get_unity_events) | 🟢 83/100 | 🟡 medium | List all UnityEvent persistent listeners in the active scene. |
| [`get_watches`](#get_watches) | 🟢 89/100 | 🟢 low | Get all active watches and recent log entries. |
| [`inspect`](#inspect) | 🟡 78/100 | 🟢 low | Get components for multiple objects at once. paths: comma-separated. componen... |
| [`invoke_method`](#invoke_method) | 🟡 76/100 | 🟡 medium | [Play Mode] Call public method on a component via reflection. |
| [`lint_playtest`](#lint_playtest) | 🟡 78/100 | 🔴 high | Read-only preflight check on a .playtest file or inline script. |
| [`lint_playtest_suite`](#lint_playtest_suite) | 🟢 82/100 | 🟡 medium | Read-only preflight check across multiple .playtest files. |
| [`lint_scene_refs`](#lint_scene_refs) | 🟢 82/100 | 🔴 high | Read-only linter for scene references in DSL scripts or batch commands. |
| [`list_connections`](#list_connections) | 🟢 89/100 | 🟢 low | List Unity connection status. |
| [`list_skills`](#list_skills) | 🟢 89/100 | 🟢 low | List all saved skills with descriptions and usage counts. |
| [`list_templates`](#list_templates) | 🟢 89/100 | 🟢 low | List available scene templates in .claude/templates/. |
| [`list_test_runs`](#list_test_runs) | 🟡 78/100 | 🟢 low | List recent durable test runs as JSON, newest first. |
| [`load_session`](#load_session) | 🟢 89/100 | 🟢 low | Load previous session context beside the current hierarchy. |
| [`manage_component`](#manage_component) | 🔴 45/100 | 🔴 high | Add or remove a component. action: 'add' or 'remove' ONLY (no 'enable'/'disab... |
| [`material`](#material) | 🟡 64/100 | 🟡 medium | Material asset management (for quick color change use `set_material`). action... |
| [`material_audit`](#material_audit) | 🟢 81/100 | 🟢 low | Material/texture scene-wide audit. |
| [`mcp_status`](#mcp_status) | 🟢 89/100 | 🟢 low | Compact MCP status: scene, dirty, play/compile state, port, alias count. |
| [`menu`](#menu) | 🟡 77/100 | 🟡 medium | Execute or list Unity Editor menu items. action: execute|list. execute: run m... |
| [`move_to`](#move_to) | 🟡 74/100 | 🟡 medium | [Play Mode] Move character to position and wait for arrival. |
| [`navmesh_query`](#navmesh_query) | 🔴 44/100 | 🔴 high | NavMesh queries and management. action: sample|path|raycast|bake|status|clear... |
| [`object_diff`](#object_diff) | 🟡 75/100 | 🟡 medium | Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA... |
| [`package`](#package) | 🔴 56/100 | 🔴 high | Package manager. action: list|search|add|remove. |
| [`particle`](#particle) | 🔴 58/100 | 🟡 medium | Particle System. action: get|create|set|apply|play|stop|pause. module=main|em... |
| [`permission_prompt`](#permission_prompt) | 🟡 70/100 | 🟢 low | Handle Claude permission prompts via MCP. |
| [`ping_object`](#ping_object) | 🟢 82/100 | 🟡 medium | Highlight object in Hierarchy and Project, and select it. |
| [`prefab`](#prefab) | 🔴 46/100 | 🔴 high | Prefab. action: save|create_variant|apply|revert|get_overrides|unpack|edit. |
| [`profile`](#profile) | 🟡 62/100 | 🟢 low | Profile CPU/GPU/memory over time. |
| [`project_settings`](#project_settings) | 🔴 53/100 | 🔴 high | Project settings. action: get|set. target: tags|layers|sorting_layers|quality... |
| [`query_state`](#query_state) | 🟢 87/100 | 🟡 medium | [Play Mode] Snapshot multiple game values in one call. |
| [`recompile`](#recompile) | 🟢 80/100 | 🟡 medium | Trigger Unity to reimport C# scripts. Returns immediately; use await_compile ... |
| [`reconnect_unity`](#reconnect_unity) | 🟡 78/100 | 🟢 low | Reconnect to Unity. Port 0 or omitted = auto-discover from port files. |
| [`references`](#references) | 🟡 70/100 | 🟡 medium | References. action: get|find_to|remap. get: outgoing refs. find_to: reverse s... |
| [`region_clear`](#region_clear) | 🔴 55/100 | 🔴 high | Delete (or preview) all objects whose XZ pivot is inside the polygon region. |
| [`release_smoke`](#release_smoke) | 🟢 80/100 | 🟡 medium | Run release readiness checks: status, aliases, compile. Returns PASS/FAIL sum... |
| [`rename_object`](#rename_object) | 🟡 76/100 | 🟡 medium | Rename a GameObject. Returns new scene path after rename. |
| [`render_analyze`](#render_analyze) | 🟡 73/100 | 🟡 medium | Rendering analysis. |
| [`resolve_scene_refs`](#resolve_scene_refs) | 🟢 86/100 | 🟡 medium | Read-only scene reference resolver. |
| [`resolve_test_request`](#resolve_test_request) | 🟡 78/100 | 🟡 medium | Resolve a possibly lost start ACK without dispatching another test run. |
| [`resolve_tool_schema`](#resolve_tool_schema) | 🟢 87/100 | 🟢 low | Return full parameter schemas for deferred tools. tools=comma-separated names. |
| [`run_playtest`](#run_playtest) | 🟡 63/100 | 🔴 high | [Play Mode] Execute a playtest DSL script. Returns structured report (for NUn... |
| [`run_playtest_suite`](#run_playtest_suite) | 🔴 48/100 | 🔴 high | [Play Mode] Run multiple .playtest files sequentially and return a compact ma... |
| [`run_tests`](#run_tests) | 🟡 66/100 | 🟡 medium | Dispatch Unity tests and return their durable identity immediately. |
| [`run_tests_wait`](#run_tests_wait) | 🔴 38/100 | 🔴 high | Dispatch tests and wait for the exact run to become terminal. |
| [`runtime_snapshot`](#runtime_snapshot) | 🟡 71/100 | 🟢 low | Snapshot all runtime objects of a given component type. Returns per-object fi... |
| [`save_session`](#save_session) | 🟢 89/100 | 🟢 low | Save current scene state to .claude/session-context.json for cold-start recov... |
| [`save_skill`](#save_skill) | 🟡 64/100 | 🟢 low | Save a learned skill (C# code or batch commands) for reuse across sessions. |
| [`save_template`](#save_template) | 🟡 66/100 | 🟢 low | Save C# code as a reusable scene template in .claude/templates/. |
| [`scan_scene`](#scan_scene) | 🟢 89/100 | 🟢 low | Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, can... |
| [`scene`](#scene) | 🟢 81/100 | 🟡 medium | Scene management. action: new|open|save|discard|open_additive|close|set_activ... |
| [`scene_change_plan`](#scene_change_plan) | 🟡 75/100 | 🟡 medium | Pre-flight + plan for safe scene edit. |
| [`scene_diff`](#scene_diff) | 🟢 89/100 | 🟢 low | Compare scene with last snapshot. First call saves snapshot. Returns diff: ad... |
| [`scene_environment`](#scene_environment) | 🟡 72/100 | 🟡 medium | Read/write scene environment: ambient light, fog, skybox, reflections. |
| [`scene_health`](#scene_health) | 🟢 82/100 | 🟢 low | Scene hierarchy/health audit. |
| [`screenshot`](#screenshot) | 🔴 58/100 | 🟡 medium | Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens... |
| [`screenshot_baseline`](#screenshot_baseline) | 🟡 65/100 | 🟢 low | Save screenshot as baseline for visual regression. name: identifier for this ... |
| [`screenshot_compare`](#screenshot_compare) | 🔴 57/100 | 🟢 low | Compare current screenshot with saved baseline. |
| [`scriptable_object`](#scriptable_object) | 🟡 64/100 | 🟡 medium | ScriptableObject. action: create|get|set|list_types|find. create: type+path[+... |
| [`search_scene`](#search_scene) | 🟡 79/100 | 🟡 medium | Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, activ... |
| [`serialized_field_rename_audit`](#serialized_field_rename_audit) | 🟡 72/100 | 🟢 low | Audit [SerializeField] rename safety. |
| [`set_active`](#set_active) | 🟡 72/100 | 🟡 medium | Set GameObject active/inactive. |
| [`set_llm_config`](#set_llm_config) | 🟢 87/100 | 🟢 low | Override LLM profiles for sampling features. Format: feature:model,turns,time... |
| [`set_material`](#set_material) | 🟡 70/100 | 🟡 medium | Set scene object material color (for full asset management use `material`). c... |
| [`set_parent`](#set_parent) | 🟢 80/100 | 🟡 medium | Reparent existing GameObject. parent=null → move to scene root. world_positio... |
| [`set_properties`](#set_properties) | 🟡 71/100 | 🟡 medium | Set multiple properties on ONE object. For multiple objects, use configure_ob... |
| [`set_property`](#set_property) | 🟡 62/100 | 🟡 medium | Set component property (Edit Mode, SerializedObject — for Play Mode use `invo... |
| [`set_property_delta`](#set_property_delta) | 🟡 76/100 | 🟡 medium | Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new. |
| [`set_rect`](#set_rect) | 🟡 67/100 | 🟡 medium | Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bott... |
| [`set_sibling_index`](#set_sibling_index) | 🟡 67/100 | 🟡 medium | Set sibling index of a GameObject within its parent. index=0 moves to first c... |
| [`setup_objects`](#setup_objects) | 🟡 78/100 | 🟡 medium | Create+configure multiple objects in one call. |
| [`shader`](#shader) | 🔴 20/100 | 🔴 high | Read or write shader assets (.shader / .shadergraph). Use when you need to in... |
| [`smart_build`](#smart_build) | 🟢 87/100 | 🟢 low | Build scene objects from natural language description using MCP sampling + ex... |
| [`snapshot`](#snapshot) | 🟡 78/100 | 🟡 medium | Capture or compare object state. |
| [`spatial_query`](#spatial_query) | 🟡 76/100 | 🟡 medium | Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|ra... |
| [`sync_playtest_aliases_from_defs`](#sync_playtest_aliases_from_defs) | 🟢 80/100 | 🟡 medium | Overwrite PlaytestConfig.asset aliases from a .defs text file. |
| [`sync_unity`](#sync_unity) | 🟡 67/100 | 🟡 medium | Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code... |
| [`test_step`](#test_step) | 🟡 64/100 | 🟡 medium | [Play Mode] Move character, snapshot state before/after, check console. |
| [`timeline`](#timeline) | 🔴 51/100 | 🟡 medium | Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinema... |
| [`transfer_object`](#transfer_object) | 🟡 77/100 | 🟡 medium | Move or copy a GameObject to another loaded scene. action: move|copy. |
| [`ui_intent`](#ui_intent) | 🟡 75/100 | 🟡 medium | Convert NL intent to Unity UI hierarchy. Templates bypass Haiku. |
| [`undo_last`](#undo_last) | 🟡 78/100 | 🟢 low | Undo the last N AI turns in the Unity Undo stack. Default: 1. |
| [`unwire_event`](#unwire_event) | 🔴 53/100 | 🔴 high | Remove persistent listener(s) from UnityEvent. |
| [`use_skill`](#use_skill) | 🟡 73/100 | 🟡 medium | Execute a previously saved skill. params: comma-separated key=value for subst... |
| [`validate_layout`](#validate_layout) | 🟡 67/100 | 🟡 medium | Check trigger overlaps. Warns if triggers closer than min_distance meters. |
| [`validate_playtest_aliases`](#validate_playtest_aliases) | 🟢 80/100 | 🟡 medium | Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/... |
| [`validate_references`](#validate_references) | 🟡 74/100 | 🟡 medium | Validate all ObjectReference fields under path recursively. |
| [`verify_after_change`](#verify_after_change) | 🔴 59/100 | 🔴 high | Single verification gate after code/scene changes. |
| [`vfx_intent`](#vfx_intent) | 🟡 68/100 | 🟡 medium | Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely. |
| [`wait_until`](#wait_until) | 🟡 64/100 | 🟡 medium | [Play Mode] Poll field until it matches value (or timeout). |
| [`watch`](#watch) | 🔴 40/100 | 🔴 high | [Play Mode] Manage watches. action: add|remove|clear|reset. |
| [`wire_event`](#wire_event) | 🔴 52/100 | 🔴 high | Wire UnityEvent persistent listener. |

---

## Tool Details

### `alias_status`

🟢 89/100 · Risk: 🟢 low

Check alias table health: loaded/empty/stale, sources, and total alias count.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `analyze_lod_culling`

🟢 83/100 · Risk: 🟢 low

LOD group coverage + occlusion culling analysis.     focus: lod|culling|occlusion|null=all.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `focus` | any |  |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'focus' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "focus": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Focus"
    }
  },
  "title": "analyze_lod_cullingArguments",
  "type": "object"
}
```

</details>

---

### `animation`

🟡 64/100 · Risk: 🟡 medium

Animate GameObject properties via AnimationClip. Use when you need to read or author keyframe animation on a specific object (not an Animator state machine — use `animator` for that, not this).     action: get (list clips/keys) | create (new AnimationClip on object) | edit (add/replace keyframes) | preview (scrub to time) | add_event | remove_event | get_events | set_wrap (keys='loop'|'once'|'pingpong'|'clamp') | set_framerate (keys='30') | get_clip_path (returns asset path).     clip=clip name, keys='t:0 v:(0,0,0); t:1 v:(0,2,0)', property=e.g. localPosition.x.     component_type: Unity component to animate (default: Transform). Examples: Light, Camera, Rigidbody.     binding_path: sub-object path for EditorCurveBinding (e.g. 'Head/Jaw'). Default '' = root.     tangent: tangent mode for keyframes: auto (default) | smooth | linear | constant.     function_name: method name for add_event. int_param/float_param/string_param: event parameters.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `binding_path` | any |  |  |
| `clip` | any |  |  |
| `clip_name` | any |  |  |
| `component_type` | any |  |  |
| `float_param` | any |  |  |
| `function_name` | any |  |  |
| `int_param` | any |  |  |
| `keys` | any |  |  |
| `path` | string | ✓ |  |
| `property` | any |  |  |
| `string_param` | any |  |  |
| `tangent` | any |  |  |
| `time` | any |  |  |

<details>
<summary>20 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'clip' has no description.
- **info**: Parameter 'clip_name' has no description.
- **info**: Parameter 'property' has no description.
- **info**: Parameter 'keys' has no description.
- **info**: Parameter 'time' has no description.
- **info**: Parameter 'component_type' has no description.
- **info**: Parameter 'binding_path' has no description.
- **info**: Parameter 'tangent' has no description.
- **info**: Parameter 'function_name' has no description.
- **info**: Parameter 'int_param' has no description.
- **info**: Parameter 'float_param' has no description.
- **info**: Parameter 'string_param' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "title": "Path",
      "type": "string"
    },
    "clip": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Clip"
    },
    "clip_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Clip Name"
    },
    "property": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Property"
    },
    "keys": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Keys"
    },
    "time": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Time"
    },
    "component_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Component Type"
    },
    "binding_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Binding Path"
    },
    "tangent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Tangent"
    },
    "function_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Function Name"
    },
    "int_param": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Int Param"
    },
    "float_param": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Float Param"
    },
    "string_param": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "String Param"
    }
  },
  "required": [
    "action",
    "path"
  ],
  "title": "animationArguments",
  "type": "object"
}
```

</details>

---

### `animator`

🔴 18/100 · Risk: 🔴 high

Animator Controller — state machine (use `animation` for keyframe clips, `timeline` for cinematics). action: get|add_param|add_state|add_transition|set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|set_avatar|rename_state|rename_param.     params='Speed:float:0; Jump:trigger'. states='Idle:Idle.anim; Walk'.     conditions='Speed>0.1; IsGrounded'. source/target=state names (*=AnyState).     blend_type: 1d|2d_simple|2d_freeform|2d_cartesian|direct.     param/param_y: blend parameters (auto-created as float if missing).     children: '(1D) Idle:0; Walk:0.5; Run:1' or '(2D) Idle:0,0; Walk:0,1'.     edit_action: add_child|remove_child|set_thresholds|set_param|set_type.     layer: layer index (int) for add_state/add_transition/set_default, or name/index string for CRUD ops.     weight: defaultWeight for add_layer/set_layer_weight (0.0–1.0).     blending: Override|Additive for add_layer/set_layer_blending.     value: speed multiplier for set_state_speed. avatar_path: asset path for set_avatar.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `avatar_path` | any |  |  |
| `blend_type` | any |  |  |
| `blending` | any |  |  |
| `children` | any |  |  |
| `conditions` | any |  |  |
| `duration` | any |  |  |
| `edit_action` | any |  |  |
| `exit_time` | any |  |  |
| `has_exit_time` | any |  |  |
| `layer` | any |  |  |
| `name` | any |  |  |
| `param` | any |  |  |
| `param_y` | any |  |  |
| `params` | any |  |  |
| `path` | string | ✓ |  |
| `source` | any |  |  |
| `state` | any |  |  |
| `states` | any |  |  |
| `target` | any |  |  |
| `type` | any |  |  |
| `value` | any |  |  |
| `weight` | any |  |  |

<details>
<summary>32 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'state' has no description.
- **info**: Parameter 'states' has no description.
- **info**: Parameter 'params' has no description.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'conditions' has no description.
- **info**: Parameter 'duration' has no description.
- **info**: Parameter 'exit_time' has no description.
- **info**: Parameter 'has_exit_time' has no description.
- **warning**: Parameter 'type' has no description.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'blend_type' has no description.
- **info**: Parameter 'param' has no description.
- **info**: Parameter 'param_y' has no description.
- **info**: Parameter 'children' has no description.
- **info**: Parameter 'edit_action' has no description.
- **info**: Parameter 'layer' has no description.
- **info**: Parameter 'weight' has no description.
- **info**: Parameter 'blending' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'avatar_path' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 3257 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "title": "Path",
      "type": "string"
    },
    "state": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "State"
    },
    "states": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "States"
    },
    "params": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Params"
    },
    "source": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Source"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "conditions": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Conditions"
    },
    "duration": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Duration"
    },
    "exit_time": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Exit Time"
    },
    "has_exit_time": {
      "anyOf": [
        {
          "type": "boolean"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Has Exit Time"
    },
    "type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Type"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "blend_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Blend Type"
    },
    "param": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Param"
    },
    "param_y": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Param Y"
    },
    "children": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Children"
    },
    "edit_action": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Edit Action"
    },
    "layer": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Layer"
    },
    "weight": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Weight"
    },
    "blending": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Blending"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "avatar_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Avatar Path"
    }
  },
  "required": [
    "action",
    "path"
  ],
  "title": "animatorArguments",
  "type": "object"
}
```

</details>

---

### `animator_intent`

🟡 75/100 · Risk: 🟡 medium

Convert NL intent to Unity Animator Controller setup via DSL.      dry_run=True returns the batch plan without executing it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `False`) |
| `intent` | string | ✓ |  |
| `target` | string | ✓ |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "target": {
      "title": "Target",
      "type": "string"
    },
    "intent": {
      "title": "Intent",
      "type": "string"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "target",
    "intent"
  ],
  "title": "animator_intentArguments",
  "type": "object"
}
```

</details>

---

### `apply_scene_change`

🟡 74/100 · Risk: 🟡 medium

Execute scene mutations with pre-check, post-verify, and optional save.     1. Validate plan_id exists and not expired (TTL 600s)     2. Execute batch commands     3. If verify: validate_references + console check     4. If save: save scene     5. Return mutation summary

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `commands` | string | ✓ |  |
| `plan_id` | string | ✓ |  |
| `save` | boolean |  |  (default: `True`) |
| `verify` | boolean |  |  (default: `True`) |

<details>
<summary>10 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'plan_id' has no description.
- **info**: Free-form string parameter 'plan_id' has no maxLength.
- **info**: Parameter 'commands' has no description.
- **info**: Free-form string parameter 'commands' has no maxLength.
- **info**: Parameter 'verify' has no description.
- **info**: Parameter 'save' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "plan_id": {
      "title": "Plan Id",
      "type": "string"
    },
    "commands": {
      "title": "Commands",
      "type": "string"
    },
    "verify": {
      "default": true,
      "title": "Verify",
      "type": "boolean"
    },
    "save": {
      "default": true,
      "title": "Save",
      "type": "boolean"
    }
  },
  "required": [
    "plan_id",
    "commands"
  ],
  "title": "apply_scene_changeArguments",
  "type": "object"
}
```

</details>

---

### `apply_template`

🟢 82/100 · Risk: 🟡 medium

Apply a scene template (.cs file from .claude/templates/).     params: comma-separated key=value pairs for ${key} replacement.     Example: apply_template('level_setup', 'player_pos=(0,0,0),count=3')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ |  |
| `params` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'params' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string"
    },
    "params": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Params"
    }
  },
  "required": [
    "name"
  ],
  "title": "apply_templateArguments",
  "type": "object"
}
```

</details>

---

### `ask`

🟢 87/100 · Risk: 🟢 low

Answer a read-only question about the Unity scene (AI-routed, not interactive — use `ask_user` to show a UI card and wait for user input).      Routes to deterministic tool plans for common patterns,     uses Haiku summarization for complex multi-tool results.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `question` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'question' has no description.
- **info**: Free-form string parameter 'question' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "question": {
      "title": "Question",
      "type": "string"
    }
  },
  "required": [
    "question"
  ],
  "title": "askArguments",
  "type": "object"
}
```

</details>

---

### `ask_user`

🟢 87/100 · Risk: 🟢 low

Show a question card in Unity chat; wait for user answer (interactive UI — use `ask` for read-only AI scene questions instead).      questions: JSON array matching AskUserQuestion schema:       [{"question":"...","header":"...","options":[{"label":"..."}],"multiSelect":false}]     Returns JSON map of question→answer (or free text if Other field used).     Use this instead of AskUserQuestion for in-Unity interactive prompts.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `questions` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'questions' has no description.
- **info**: Free-form string parameter 'questions' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "questions": {
      "title": "Questions",
      "type": "string"
    }
  },
  "required": [
    "questions"
  ],
  "title": "ask_userArguments",
  "type": "object"
}
```

</details>

---

### `asset`

🔴 37/100 · Risk: 🔴 high

Asset database. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package|read_text|write_text|reimport. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial|AnimatorController|ScriptableObject (class= required for SO). move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. get_dependencies: forward deps. find_dependents: reverse deps (who references this asset). export_package: path+output[+include_deps=false to skip deps]. import_package: path (filesystem). read_text: path. write_text: path+content. reimport: path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `class_name` | any |  |  |
| `content` | any |  |  |
| `dest` | any |  |  |
| `folder` | any |  |  |
| `include_deps` | boolean |  |  (default: `True`) |
| `labels` | any |  |  |
| `name` | any |  |  |
| `output` | any |  |  |
| `path` | any |  |  |
| `prop` | any |  |  |
| `recursive` | boolean |  |  (default: `False`) |
| `source` | any |  |  |
| `type` | any |  |  |
| `value` | any |  |  |

<details>
<summary>21 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **warning**: Parameter 'type' has no description.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'folder' has no description.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'dest' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'recursive' has no description.
- **info**: Parameter 'labels' has no description.
- **info**: Parameter 'output' has no description.
- **info**: Parameter 'include_deps' has no description.
- **info**: Parameter 'content' has no description.
- **info**: Parameter 'class_name' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Type"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "folder": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Folder"
    },
    "source": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Source"
    },
    "dest": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Dest"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "recursive": {
      "default": false,
      "title": "Recursive",
      "type": "boolean"
    },
    "labels": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Labels"
    },
    "output": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Output"
    },
    "include_deps": {
      "default": true,
      "title": "Include Deps",
      "type": "boolean"
    },
    "content": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Content"
    },
    "class_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Class Name"
    }
  },
  "required": [
    "action"
  ],
  "title": "assetArguments",
  "type": "object"
}
```

</details>

---

### `auto_fix`

🟢 89/100 · Risk: 🟢 low

Auto-detect and fix Unity errors. Uses MCP sampling to ask Claude for fixes.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `auto_wire`

🟡 72/100 · Risk: 🟡 medium

Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects.     dry_run=true previews without applying. Returns wired/ambiguous/no-match summary.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `False`) |
| `path` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "path"
  ],
  "title": "auto_wireArguments",
  "type": "object"
}
```

</details>

---

### `autofit_collider`

🟡 71/100 · Risk: 🟡 medium

Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |
| `type` | string |  |  (default: `box`) |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "type": {
      "default": "box",
      "title": "Type",
      "type": "string"
    }
  },
  "required": [
    "path"
  ],
  "title": "autofit_colliderArguments",
  "type": "object"
}
```

</details>

---

### `await_compile`

🟡 78/100 · Risk: 🟢 low

Block until Unity finishes compiling + reloading, then return compile errors.     Use after writing .cs files instead of sleep. Returns errors or 'compile clean (Xs)'.     Handles domain reload disconnects transparently. timeout=0 → immediate check, no loop.     Epoch-aware via sync_status when available (+10 from MAJOR-1); falls back to compile_status.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `timeout` | number |  |  (default: `60.0`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "timeout": {
      "default": 60.0,
      "title": "Timeout",
      "type": "number"
    }
  },
  "title": "await_compileArguments",
  "type": "object"
}
```

</details>

---

### `bake`

🟢 86/100 · Risk: 🟢 low

Bake operations.     target: lighting|occlusion.     action (lighting): start(default)|status|cancel|clear|settings.     action (occlusion): start(default)|status|clear.     Poll status after start — lighting bake is async.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | any |  |  |
| `target` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'action' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "target": {
      "title": "Target",
      "type": "string"
    },
    "action": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Action"
    }
  },
  "required": [
    "target"
  ],
  "title": "bakeArguments",
  "type": "object"
}
```

</details>

---

### `batch`

🟡 68/100 · Risk: 🟡 medium

Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop (default continue). timeout: seconds (default 75). atomic: True reverts ALL prior ops on first failure (Unity Undo); execute_code fs side-effects NOT reverted. PREFER over individual tool calls.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `atomic` | boolean |  |  (default: `False`) |
| `commands` | string | ✓ |  |
| `on_error` | string |  |  (default: `continue`) |
| `timeout` | number |  |  (default: `75.0`) |
| `validate_aliases` | boolean |  |  (default: `False`) |

<details>
<summary>12 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'commands' has no description.
- **info**: Free-form string parameter 'commands' has no maxLength.
- **info**: Parameter 'on_error' has no description.
- **info**: Free-form string parameter 'on_error' has no maxLength.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'atomic' has no description.
- **info**: Parameter 'validate_aliases' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "commands": {
      "title": "Commands",
      "type": "string"
    },
    "on_error": {
      "default": "continue",
      "title": "On Error",
      "type": "string"
    },
    "timeout": {
      "default": 75.0,
      "title": "Timeout",
      "type": "number"
    },
    "atomic": {
      "default": false,
      "title": "Atomic",
      "type": "boolean"
    },
    "validate_aliases": {
      "default": false,
      "title": "Validate Aliases",
      "type": "boolean"
    }
  },
  "required": [
    "commands"
  ],
  "title": "batchArguments",
  "type": "object"
}
```

</details>

---

### `budget_status`

🟢 89/100 · Risk: 🟢 low

Returns Haiku cost: session/cap/day/skipped features. Text format.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `build`

🟢 83/100 · Risk: 🟡 medium

Build player. action: build.     target: StandaloneWindows64|StandaloneOSX|Android|iOS|WebGL (default: active).     scenes: comma-sep asset paths (default: Build Settings list).     path: output path (default: Builds/<target>).     dev: development build flag.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `dev` | boolean |  |  (default: `False`) |
| `path` | any |  |  |
| `scenes` | any |  |  |
| `target` | any |  |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'scenes' has no description.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'dev' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "scenes": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scenes"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "dev": {
      "default": false,
      "title": "Dev",
      "type": "boolean"
    }
  },
  "required": [
    "action"
  ],
  "title": "buildArguments",
  "type": "object"
}
```

</details>

---

### `cancel_test_run`

🟡 78/100 · Risk: 🟡 medium

Request cancellation of one exact test run; cancellation is asynchronous.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'run_id' has no description.
- **info**: Free-form string parameter 'run_id' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "run_id": {
      "title": "Run Id",
      "type": "string"
    }
  },
  "required": [
    "run_id"
  ],
  "title": "cancel_test_runArguments",
  "type": "object"
}
```

</details>

---

### `check_colliders`

🟢 83/100 · Risk: 🟡 medium

Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    }
  },
  "title": "check_collidersArguments",
  "type": "object"
}
```

</details>

---

### `checkpoint`

🟡 73/100 · Risk: 🟡 medium

Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `label` | string |  |  (default: `checkpoint`) |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'label' has no description.
- **info**: Free-form string parameter 'label' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "label": {
      "default": "checkpoint",
      "title": "Label",
      "type": "string"
    }
  },
  "title": "checkpointArguments",
  "type": "object"
}
```

</details>

---

### `compile_preflight`

🟢 80/100 · Risk: 🟡 medium

Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile.     file_path: Assets-relative. new_content: full file. Returns OK preflight (ms) / ERR preflight + diagnostics / [ROSLYN UNAVAILABLE].

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file_path` | string | ✓ |  |
| `new_content` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'file_path' has no description.
- **info**: Free-form string parameter 'file_path' has no maxLength.
- **warning**: Path-like parameter 'file_path' has no structural constraint.
- **info**: Parameter 'new_content' has no description.
- **info**: Free-form string parameter 'new_content' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "file_path": {
      "title": "File Path",
      "type": "string"
    },
    "new_content": {
      "title": "New Content",
      "type": "string"
    }
  },
  "required": [
    "file_path",
    "new_content"
  ],
  "title": "compile_preflightArguments",
  "type": "object"
}
```

</details>

---

### `configure_objects`

🟢 87/100 · Risk: 🟡 medium

Configure multiple objects at once.     Format: /Path component.prop=value [...] per line.     Example:     /NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100     /NPC2 Transform.m_LocalPosition=(3,0,0)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `config` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'config' has no description.
- **info**: Free-form string parameter 'config' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "config": {
      "title": "Config",
      "type": "string"
    }
  },
  "required": [
    "config"
  ],
  "title": "configure_objectsArguments",
  "type": "object"
}
```

</details>

---

### `console_mark`

🟡 73/100 · Risk: 🟡 medium

Create a console watermark. Returns mark_id encoding current timestamp.     Pass to get_console_since() to retrieve only logs after this point.     Pure Python — no TCP call.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `label` | string |  |  (default: ``) |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'label' has no description.
- **info**: Free-form string parameter 'label' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "label": {
      "default": "",
      "title": "Label",
      "type": "string"
    }
  },
  "title": "console_markArguments",
  "type": "object"
}
```

</details>

---

### `create_object`

🟡 69/100 · Risk: 🟡 medium

Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | any |  |  |
| `name` | string | ✓ |  |
| `parent` | any |  |  |
| `prefab_path` | any |  |  |
| `primitive` | any |  |  |
| `scene` | any |  |  |

<details>
<summary>11 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'parent' has no description.
- **info**: Parameter 'components' has no description.
- **info**: Parameter 'primitive' has no description.
- **info**: Parameter 'prefab_path' has no description.
- **info**: Parameter 'scene' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string"
    },
    "parent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parent"
    },
    "components": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Components"
    },
    "primitive": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Primitive"
    },
    "prefab_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prefab Path"
    },
    "scene": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scene"
    }
  },
  "required": [
    "name"
  ],
  "title": "create_objectArguments",
  "type": "object"
}
```

</details>

---

### `create_ui`

🔴 52/100 · Risk: 🟡 medium

Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image. Auto-creates Canvas if needed.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `anchor` | any |  |  |
| `color` | any |  |  |
| `font_size` | any |  |  |
| `name` | any |  |  |
| `parent` | any |  |  |
| `pivot` | any |  |  |
| `pos` | any |  |  |
| `size` | any |  |  |
| `text` | any |  |  |
| `type` | string | ✓ |  |

<details>
<summary>16 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'parent' has no description.
- **info**: Parameter 'anchor' has no description.
- **info**: Parameter 'pos' has no description.
- **info**: Parameter 'size' has no description.
- **info**: Parameter 'pivot' has no description.
- **info**: Parameter 'color' has no description.
- **warning**: Parameter 'text' has no description.
- **info**: Parameter 'font_size' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "parent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parent"
    },
    "anchor": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Anchor"
    },
    "pos": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pos"
    },
    "size": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Size"
    },
    "pivot": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pivot"
    },
    "color": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Color"
    },
    "text": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Text"
    },
    "font_size": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Font Size"
    }
  },
  "required": [
    "type"
  ],
  "title": "create_uiArguments",
  "type": "object"
}
```

</details>

---

### `debug`

🟡 73/100 · Risk: 🟡 medium

AI-assisted scene debug: gather diagnostic context based on symptom (not compile/reload — use `diagnose` for that; not runtime state — use `debug_animator` or `debug_physics`).      symptom: Natural language description ("enemy doesn't move", "button not clickable")     path: Optional target object path ("/Enemy_01")     gather: Override comma-separated tool names ("inspect,get_console,screenshot")      Returns structured diagnostic text for LLM analysis.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `gather` | string |  |  (default: ``) |
| `path` | string |  |  (default: ``) |
| `symptom` | string |  |  (default: ``) |

<details>
<summary>11 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'symptom' has no description.
- **info**: Free-form string parameter 'symptom' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'gather' has no description.
- **info**: Free-form string parameter 'gather' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "symptom": {
      "default": "",
      "title": "Symptom",
      "type": "string"
    },
    "path": {
      "default": "",
      "title": "Path",
      "type": "string"
    },
    "gather": {
      "default": "",
      "title": "Gather",
      "type": "string"
    }
  },
  "title": "debugArguments",
  "type": "object"
}
```

</details>

---

### `debug_animator`

🟢 82/100 · Risk: 🟡 medium

[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile).     path: scene path to GameObject with Animator component.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    }
  },
  "required": [
    "path"
  ],
  "title": "debug_animatorArguments",
  "type": "object"
}
```

</details>

---

### `debug_physics`

🟡 76/100 · Risk: 🟡 medium

[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile).     path: scene path to GameObject.     radius: overlap sphere radius for nearby detection (default 5m).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |
| `radius` | number |  |  (default: `5.0`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'radius' has no description.
- **warning**: Numeric parameter 'radius' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "radius": {
      "default": 5.0,
      "title": "Radius",
      "type": "number"
    }
  },
  "required": [
    "path"
  ],
  "title": "debug_physicsArguments",
  "type": "object"
}
```

</details>

---

### `delete_object`

🔴 53/100 · Risk: 🔴 high

Delete GameObject by instance ID or scene path. Provide one. force=True to delete non-empty containers.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `force` | boolean |  |  (default: `False`) |
| `id` | any |  |  |
| `path` | any |  |  |

<details>
<summary>9 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'id' has no description.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'force' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "id": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Id"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "force": {
      "default": false,
      "title": "Force",
      "type": "boolean"
    }
  },
  "title": "delete_objectArguments",
  "type": "object"
}
```

</details>

---

### `diagnose`

🟢 81/100 · Risk: 🟢 low

Read Unity compile/reload fact-signals atomically; returns typed verdict. For scene symptom analysis, use `debug`. For runtime component state, use `debug_animator` or `debug_physics`.      prev_mvid: MVID from before a sync operation. When provided, enables STALE-DOMAIN     detection (unchanged MVID after intended recompile). Pass '' for standalone probing.      expected_compile: True when a compile was explicitly triggered (default).     False for Bee cache-hit / will_compile=false / reverted-edit probes — prevents     false STALE-DOMAIN on legitimately-frozen MVID (A5/G27).      Returns: CLEAN-LIVE / FAIL:<CS> / STALE-DOMAIN / WEDGE-ENGINE / WEDGE-STATE /              BUILD-FAILED-WEDGE / STALE-CACHE / TESTS-INVISIBLE / REBUILDING /              NO-OP / UNKNOWN

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `expected_compile` | boolean |  |  (default: `True`) |
| `prev_mvid` | string |  |  (default: ``) |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'prev_mvid' has no description.
- **info**: Free-form string parameter 'prev_mvid' has no maxLength.
- **info**: Parameter 'expected_compile' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "prev_mvid": {
      "default": "",
      "title": "Prev Mvid",
      "type": "string"
    },
    "expected_compile": {
      "default": true,
      "title": "Expected Compile",
      "type": "boolean"
    }
  },
  "title": "diagnoseArguments",
  "type": "object"
}
```

</details>

---

### `discover_tools`

🟢 80/100 · Risk: 🟢 low

Find and enable tools by category.     Canonical 8: SCENE, COMPONENTS, ASSETS, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM.     include_legacy=True adds legacy aliases (object, animation, etc.).     structured=True adds surface/mutability info. enable=False to browse only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | any |  |  |
| `enable` | boolean |  |  (default: `True`) |
| `include_legacy` | boolean |  |  (default: `False`) |
| `structured` | boolean |  |  (default: `False`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'category' has no description.
- **info**: Parameter 'enable' has no description.
- **info**: Parameter 'include_legacy' has no description.
- **info**: Parameter 'structured' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "category": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Category"
    },
    "enable": {
      "default": true,
      "title": "Enable",
      "type": "boolean"
    },
    "include_legacy": {
      "default": false,
      "title": "Include Legacy",
      "type": "boolean"
    },
    "structured": {
      "default": false,
      "title": "Structured",
      "type": "boolean"
    }
  },
  "title": "discover_toolsArguments",
  "type": "object"
}
```

</details>

---

### `do`

🟡 77/100 · Risk: 🟡 medium

Convert natural language intent into Unity scene operations. Use when scene structure unknown or task is ambiguous. NOT for targeted mutations on known objects — use batch directly.      Haiku generates a batch DSL plan, which is validated then executed.     dry_run=True returns the plan without executing it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `False`) |
| `intent` | string | ✓ |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool name 'do' is too generic for reliable tool selection.
- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "intent": {
      "title": "Intent",
      "type": "string"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "intent"
  ],
  "title": "doArguments",
  "type": "object"
}
```

</details>

---

### `doctor`

🟡 74/100 · Risk: 🟡 medium

Run health diagnostics. Use fix=True to auto-repair safe issues.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fix` | boolean |  |  (default: `False`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'fix' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "fix": {
      "default": false,
      "title": "Fix",
      "type": "boolean"
    }
  },
  "title": "doctorArguments",
  "type": "object"
}
```

</details>

---

### `editor`

🟢 80/100 · Risk: 🟡 medium

Editor state/control. action: state|play|pause|stop|select|project_path.     select: path (single) or paths (comma-sep multi, e.g. "/Player,/Enemy,/NPC").

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string |  |  (default: `state`) |
| `path` | any |  |  |
| `paths` | any |  |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'paths' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "default": "state",
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "paths": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Paths"
    }
  },
  "title": "editorArguments",
  "type": "object"
}
```

</details>

---

### `execute_code`

🟡 61/100 · Risk: 🟡 medium

Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile.     Security: no System.IO, System.Net, System.Diagnostics.     Bare statements are auto-wrapped in a static class — no boilerplate needed.     Example: "var go = new GameObject(\"Test\"); return go.name;"

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ |  |
| `undo_label` | string |  |  (default: `execute_code`) |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'code' has no description.
- **info**: Free-form string parameter 'code' has no maxLength.
- **error**: Execution-like parameter 'code' accepts unconstrained free-form text.
- **info**: Parameter 'undo_label' has no description.
- **info**: Free-form string parameter 'undo_label' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "code": {
      "title": "Code",
      "type": "string"
    },
    "undo_label": {
      "default": "execute_code",
      "title": "Undo Label",
      "type": "string"
    }
  },
  "required": [
    "code"
  ],
  "title": "execute_codeArguments",
  "type": "object"
}
```

</details>

---

### `export_playtest_aliases_to_defs`

🟢 80/100 · Risk: 🟡 medium

Export PlaytestConfig.asset aliases to a readable .defs text file.     asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).     defs: project-relative output path (default: Assets/PlaytestDefs/farm_core.defs).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'asset' has no description.
- **info**: Free-form string parameter 'asset' has no maxLength.
- **info**: Parameter 'defs' has no description.
- **info**: Free-form string parameter 'defs' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "asset": {
      "default": "Assets/Configs/PlaytestConfig.asset",
      "title": "Asset",
      "type": "string"
    },
    "defs": {
      "default": "Assets/PlaytestDefs/farm_core.defs",
      "title": "Defs",
      "type": "string"
    }
  },
  "title": "export_playtest_aliases_to_defsArguments",
  "type": "object"
}
```

</details>

---

### `find_objects`

🟡 76/100 · Risk: 🟡 medium

Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | any |  |  |
| `layer` | any |  |  |
| `name` | any |  |  |
| `tag` | any |  |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'tag' has no description.
- **info**: Parameter 'layer' has no description.
- **info**: Parameter 'component' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "tag": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Tag"
    },
    "layer": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Layer"
    },
    "component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Component"
    }
  },
  "title": "find_objectsArguments",
  "type": "object"
}
```

</details>

---

### `fingerprint`

🟡 77/100 · Risk: 🟡 medium

Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `depth` | integer |  |  (default: `3`) |
| `path` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'depth' has no description.
- **warning**: Numeric parameter 'depth' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "depth": {
      "default": 3,
      "title": "Depth",
      "type": "integer"
    }
  },
  "title": "fingerprintArguments",
  "type": "object"
}
```

</details>

---

### `get_capabilities`

🟢 89/100 · Risk: 🟢 low

Unity version, platform, render pipeline, scripting backend, and optional packages available.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `get_changes`

🟡 74/100 · Risk: 🟡 medium

Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo,     play mode, scene open/save, selection. Returns chronological event list or NO_CHANGES.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `clear` | boolean |  |  (default: `True`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'clear' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "clear": {
      "default": true,
      "title": "Clear",
      "type": "boolean"
    }
  },
  "title": "get_changesArguments",
  "type": "object"
}
```

</details>

---

### `get_compile_errors`

🟢 89/100 · Risk: 🟡 medium

Compilation errors with file:line:column. Not lost on Console.Clear(). Structured, typed.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `get_component`

🟡 68/100 · Risk: 🟡 medium

Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N.     fields: comma-separated field names to keep (e.g. 'mass,position') — projects the result to save tokens; shows requested fields even at default values. Aliases: position, rotation, scale, mass, enabled, active, name.     full=True: bypass distillation, return raw response. compress=True: strip default values before transfer.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `compress` | boolean |  |  (default: `False`) |
| `fields` | any |  |  |
| `full` | boolean |  |  (default: `False`) |
| `path` | string | ✓ |  |
| `type` | string | ✓ |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **info**: Parameter 'fields' has no description.
- **info**: Parameter 'full' has no description.
- **info**: Parameter 'compress' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "type": {
      "title": "Type",
      "type": "string"
    },
    "fields": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fields"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean"
    }
  },
  "required": [
    "path",
    "type"
  ],
  "title": "get_componentArguments",
  "type": "object"
}
```

</details>

---

### `get_components_list`

🟡 79/100 · Risk: 🟢 low

List all components on object by instance ID.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | integer | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'id' has no description.
- **warning**: Numeric parameter 'id' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "id": {
      "title": "Id",
      "type": "integer"
    }
  },
  "required": [
    "id"
  ],
  "title": "get_components_listArguments",
  "type": "object"
}
```

</details>

---

### `get_console`

🟡 68/100 · Risk: 🟢 low

Recent console logs. For C# compile errors use get_compile_errors instead. keyword: case-insensitive substring filter. count_only: return N matches as string. since: only logs from last N seconds.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `count` | integer |  |  (default: `10`) |
| `count_only` | boolean |  |  (default: `False`) |
| `first` | integer |  |  (default: `0`) |
| `keyword` | any |  |  |
| `level` | any |  |  |
| `since` | any |  |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'count' has no description.
- **warning**: Numeric parameter 'count' has no bounds.
- **info**: Parameter 'level' has no description.
- **info**: Parameter 'first' has no description.
- **warning**: Numeric parameter 'first' has no bounds.
- **info**: Parameter 'keyword' has no description.
- **info**: Parameter 'count_only' has no description.
- **info**: Parameter 'since' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "count": {
      "default": 10,
      "title": "Count",
      "type": "integer"
    },
    "level": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Level"
    },
    "first": {
      "default": 0,
      "title": "First",
      "type": "integer"
    },
    "keyword": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Keyword"
    },
    "count_only": {
      "default": false,
      "title": "Count Only",
      "type": "boolean"
    },
    "since": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Since"
    }
  },
  "title": "get_consoleArguments",
  "type": "object"
}
```

</details>

---

### `get_console_since`

🟡 78/100 · Risk: 🟢 low

Console entries after the watermark created by console_mark().     mark_id: string from console_mark() or bare float timestamp.     level: optional filter ('error,exception,assert').     keyword: case-insensitive substring filter.     count_only: return match count as string.     count: max entries to return (default 500).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `count` | integer |  |  (default: `500`) |
| `count_only` | boolean |  |  (default: `False`) |
| `keyword` | any |  |  |
| `level` | any |  |  |
| `mark_id` | string | ✓ |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'mark_id' has no description.
- **info**: Free-form string parameter 'mark_id' has no maxLength.
- **info**: Parameter 'level' has no description.
- **info**: Parameter 'count' has no description.
- **warning**: Numeric parameter 'count' has no bounds.
- **info**: Parameter 'keyword' has no description.
- **info**: Parameter 'count_only' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "mark_id": {
      "title": "Mark Id",
      "type": "string"
    },
    "level": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Level"
    },
    "count": {
      "default": 500,
      "title": "Count",
      "type": "integer"
    },
    "keyword": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Keyword"
    },
    "count_only": {
      "default": false,
      "title": "Count Only",
      "type": "boolean"
    }
  },
  "required": [
    "mark_id"
  ],
  "title": "get_console_sinceArguments",
  "type": "object"
}
```

</details>

---

### `get_enabled_tools`

🟢 89/100 · Risk: 🟢 low

List enabled tool names, comma-separated.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `get_frame_stats`

🟢 82/100 · Risk: 🟢 low

Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No session needed.     include: narrow output — e.g. 'gc' for GC stats only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: ``) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "include": {
      "default": "",
      "title": "Include",
      "type": "string"
    }
  },
  "title": "get_frame_statsArguments",
  "type": "object"
}
```

</details>

---

### `get_hierarchy`

🟡 61/100 · Risk: 🟡 medium

Scene hierarchy as text tree. For finding specific object by name/type use search_scene. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call. full=True: bypass distillation. scene: filter to a single scene by name (multi-scene only).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | boolean |  |  (default: `False`) |
| `compress` | boolean |  |  (default: `False`) |
| `depth` | integer |  |  (default: `2`) |
| `filter` | any |  |  |
| `full` | boolean |  |  (default: `False`) |
| `incremental` | boolean |  |  (default: `False`) |
| `root` | any |  |  |
| `scene` | any |  |  |
| `summary` | boolean |  |  (default: `False`) |

<details>
<summary>15 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'depth' has no description.
- **warning**: Numeric parameter 'depth' has no bounds.
- **info**: Parameter 'root' has no description.
- **info**: Parameter 'filter' has no description.
- **info**: Parameter 'components' has no description.
- **info**: Parameter 'compress' has no description.
- **info**: Parameter 'summary' has no description.
- **info**: Parameter 'incremental' has no description.
- **info**: Parameter 'full' has no description.
- **info**: Parameter 'scene' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "depth": {
      "default": 2,
      "title": "Depth",
      "type": "integer"
    },
    "root": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Root"
    },
    "filter": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Filter"
    },
    "components": {
      "default": false,
      "title": "Components",
      "type": "boolean"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean"
    },
    "summary": {
      "default": false,
      "title": "Summary",
      "type": "boolean"
    },
    "incremental": {
      "default": false,
      "title": "Incremental",
      "type": "boolean"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean"
    },
    "scene": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scene"
    }
  },
  "title": "get_hierarchyArguments",
  "type": "object"
}
```

</details>

---

### `get_memory`

🟢 82/100 · Risk: 🟢 low

Memory snapshot.     include: all|textures|meshes|audio|gc — narrow the asset-type breakdown.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: `all`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "include": {
      "default": "all",
      "title": "Include",
      "type": "string"
    }
  },
  "title": "get_memoryArguments",
  "type": "object"
}
```

</details>

---

### `get_metrics`

🔴 57/100 · Risk: 🔴 high

Returns telemetry snapshot. format: text|json. reset=true clears counters atomically.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `format` | string |  |  (default: `text`) |
| `reset` | boolean |  |  (default: `False`) |

<details>
<summary>9 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'format' has no description.
- **info**: Free-form string parameter 'format' has no maxLength.
- **info**: Parameter 'reset' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "format": {
      "default": "text",
      "title": "Format",
      "type": "string"
    },
    "reset": {
      "default": false,
      "title": "Reset",
      "type": "boolean"
    }
  },
  "title": "get_metricsArguments",
  "type": "object"
}
```

</details>

---

### `get_object_detail`

🟡 78/100 · Risk: 🟢 low

Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `full` | boolean |  |  (default: `False`) |
| `id` | integer | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'id' has no description.
- **warning**: Numeric parameter 'id' has no bounds.
- **info**: Parameter 'full' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "id": {
      "title": "Id",
      "type": "integer"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean"
    }
  },
  "required": [
    "id"
  ],
  "title": "get_object_detailArguments",
  "type": "object"
}
```

</details>

---

### `get_schema`

🟡 78/100 · Risk: 🟢 low

Get all serialized fields of a component type with types. Use before set_property to know exact field names.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string"
    }
  },
  "required": [
    "type"
  ],
  "title": "get_schemaArguments",
  "type": "object"
}
```

</details>

---

### `get_selection`

🟢 89/100 · Risk: 🟡 medium

Currently selected GameObject: path and component list.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `get_spatial_context`

🟡 76/100 · Risk: 🟡 medium

Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |
| `radius` | number |  |  (default: `5.0`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'radius' has no description.
- **warning**: Numeric parameter 'radius' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "radius": {
      "default": 5.0,
      "title": "Radius",
      "type": "number"
    }
  },
  "required": [
    "path"
  ],
  "title": "get_spatial_contextArguments",
  "type": "object"
}
```

</details>

---

### `get_test_count`

🟢 89/100 · Risk: 🟢 low

Number of edit-mode and play-mode tests in the project.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `get_test_progress`

🟡 74/100 · Risk: 🟡 medium

Legacy progress facade. Pass run_id to correlate the response.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'run_id' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "run_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Run Id"
    }
  },
  "title": "get_test_progressArguments",
  "type": "object"
}
```

</details>

---

### `get_test_results`

🟡 74/100 · Risk: 🟡 medium

Legacy result facade. Pass run_id to prevent reading a stale latest run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'run_id' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "run_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Run Id"
    }
  },
  "title": "get_test_resultsArguments",
  "type": "object"
}
```

</details>

---

### `get_test_run`

🟡 78/100 · Risk: 🟡 medium

Return the durable JSON snapshot for one exact test run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'run_id' has no description.
- **info**: Free-form string parameter 'run_id' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "run_id": {
      "title": "Run Id",
      "type": "string"
    }
  },
  "required": [
    "run_id"
  ],
  "title": "get_test_runArguments",
  "type": "object"
}
```

</details>

---

### `get_unity_events`

🟢 83/100 · Risk: 🟡 medium

List all UnityEvent persistent listeners in the active scene.     path: optional scene-path prefix filter (e.g. '/UI' to scan only the UI subtree).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    }
  },
  "title": "get_unity_eventsArguments",
  "type": "object"
}
```

</details>

---

### `get_watches`

🟢 89/100 · Risk: 🟢 low

Get all active watches and recent log entries.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `inspect`

🟡 78/100 · Risk: 🟢 low

Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all).     find_type: component type to find — populates paths automatically (replaces explicit paths).     fields: comma-separated field names to keep across all objects — projects the result to save tokens. full=True: bypass distillation. compress=True: strip default values before transfer.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | any |  |  |
| `compress` | boolean |  |  (default: `False`) |
| `fields` | any |  |  |
| `find_type` | any |  |  |
| `full` | boolean |  |  (default: `False`) |
| `paths` | any |  |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'paths' has no description.
- **info**: Parameter 'components' has no description.
- **info**: Parameter 'fields' has no description.
- **info**: Parameter 'full' has no description.
- **info**: Parameter 'compress' has no description.
- **info**: Parameter 'find_type' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "paths": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Paths"
    },
    "components": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Components"
    },
    "fields": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fields"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean"
    },
    "find_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Find Type"
    }
  },
  "title": "inspectArguments",
  "type": "object"
}
```

</details>

---

### `invoke_method`

🟡 76/100 · Risk: 🟡 medium

[Play Mode] Call public method on a component via reflection.     args: comma-separated values matching method parameters.     Example: invoke_method('/Player', 'PlayerController', 'MoveTo', '10,0,5')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `args` | string |  |  (default: ``) |
| `component` | string | ✓ |  |
| `method` | string | ✓ |  |
| `path` | string | ✓ |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'method' has no description.
- **info**: Free-form string parameter 'method' has no maxLength.
- **info**: Parameter 'args' has no description.
- **info**: Free-form string parameter 'args' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "component": {
      "title": "Component",
      "type": "string"
    },
    "method": {
      "title": "Method",
      "type": "string"
    },
    "args": {
      "default": "",
      "title": "Args",
      "type": "string"
    }
  },
  "required": [
    "path",
    "component",
    "method"
  ],
  "title": "invoke_methodArguments",
  "type": "object"
}
```

</details>

---

### `lint_playtest`

🟡 78/100 · Risk: 🔴 high

Read-only preflight check on a .playtest file or inline script.     Checks: unresolved $alias, deprecated ALIAS, TRACE_FLOW (unimplemented), CALL unknown macro,     mixed AND/OR, no evidence commands, missing ASSERT_CONSOLE_CLEAN at end.     path: project-relative path to .playtest file.     script: inline DSL to lint (mutually exclusive with path).     Returns: OK or severity-tagged issues (ERROR/WARN/INFO) with file:line.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  |  |
| `script` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'script' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "script": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Script"
    }
  },
  "title": "lint_playtestArguments",
  "type": "object"
}
```

</details>

---

### `lint_playtest_suite`

🟢 82/100 · Risk: 🟡 medium

Read-only preflight check across multiple .playtest files.     pattern: glob pattern (e.g. 'Playtests/*.playtest') or comma-separated list.     suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment).     Returns: aggregated lint report, one block per file.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pattern` | any |  |  |
| `suite_path` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'pattern' has no description.
- **info**: Parameter 'suite_path' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "pattern": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pattern"
    },
    "suite_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Suite Path"
    }
  },
  "title": "lint_playtest_suiteArguments",
  "type": "object"
}
```

</details>

---

### `lint_scene_refs`

🟢 82/100 · Risk: 🔴 high

Read-only linter for scene references in DSL scripts or batch commands.     path: project-relative path to .playtest file.     snippet: inline DSL or batch commands to lint (mutually exclusive with path).     Checks: unresolved aliases, embedded aliases, missing objects, ambiguous names.     Returns: 'OK: no issues' or severity-tagged issues (ERROR/WARN) with file:line:token.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  |  |
| `snippet` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'snippet' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "snippet": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Snippet"
    }
  },
  "title": "lint_scene_refsArguments",
  "type": "object"
}
```

</details>

---

### `list_connections`

🟢 89/100 · Risk: 🟢 low

List Unity connection status.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `list_skills`

🟢 89/100 · Risk: 🟢 low

List all saved skills with descriptions and usage counts.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `list_templates`

🟢 89/100 · Risk: 🟢 low

List available scene templates in .claude/templates/.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `list_test_runs`

🟡 78/100 · Risk: 🟢 low

List recent durable test runs as JSON, newest first.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | integer |  |  (default: `20`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'limit' has no description.
- **warning**: Numeric parameter 'limit' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "limit": {
      "default": 20,
      "title": "Limit",
      "type": "integer"
    }
  },
  "title": "list_test_runsArguments",
  "type": "object"
}
```

</details>

---

### `load_session`

🟢 89/100 · Risk: 🟢 low

Load previous session context beside the current hierarchy.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `manage_component`

🔴 45/100 · Risk: 🔴 high

Add or remove a component. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button').

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `path` | string | ✓ |  |
| `type` | string | ✓ |  |

<details>
<summary>13 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "type": {
      "title": "Type",
      "type": "string"
    },
    "action": {
      "title": "Action",
      "type": "string"
    }
  },
  "required": [
    "path",
    "type",
    "action"
  ],
  "title": "manage_componentArguments",
  "type": "object"
}
```

</details>

---

### `material`

🟡 64/100 · Risk: 🟡 medium

Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path. get_errors: path (shader asset). list_shaders: filter (optional name filter). set_fields: path+value (newline-separated prop=val). set target: shared|instance|asset (default shared).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `filter` | any |  |  |
| `object_path` | any |  |  |
| `path` | any |  |  |
| `prop` | any |  |  |
| `shader` | any |  |  |
| `slot` | any |  |  |
| `source` | any |  |  |
| `target` | any |  |  |
| `targets` | any |  |  |
| `value` | any |  |  |

<details>
<summary>16 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'object_path' has no description.
- **info**: Parameter 'shader' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'targets' has no description.
- **info**: Parameter 'slot' has no description.
- **info**: Parameter 'filter' has no description.
- **info**: Parameter 'target' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "object_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Object Path"
    },
    "shader": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Shader"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "source": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Source"
    },
    "targets": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Targets"
    },
    "slot": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Slot"
    },
    "filter": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Filter"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    }
  },
  "required": [
    "action"
  ],
  "title": "materialArguments",
  "type": "object"
}
```

</details>

---

### `material_audit`

🟢 81/100 · Risk: 🟢 low

Material/texture scene-wide audit.     action: summary|materials|textures|duplicates|compression|recommendations.     platform: Android|iOS|Standalone|Default (for compression check).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string |  |  (default: `summary`) |
| `platform` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'platform' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "default": "summary",
      "title": "Action",
      "type": "string"
    },
    "platform": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Platform"
    }
  },
  "title": "material_auditArguments",
  "type": "object"
}
```

</details>

---

### `mcp_status`

🟢 89/100 · Risk: 🟢 low

Compact MCP status: scene, dirty, play/compile state, port, alias count.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `menu`

🟡 77/100 · Risk: 🟡 medium

Execute or list Unity Editor menu items. action: execute|list. execute: run menu item by path. list: show sub-items (omit path for all roots). Note: Edit/ menu items not supported by Unity API.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `path` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    }
  },
  "required": [
    "action"
  ],
  "title": "menuArguments",
  "type": "object"
}
```

</details>

---

### `move_to`

🟡 74/100 · Risk: 🟡 medium

[Play Mode] Move character to position and wait for arrival.     path: scene path to GO with movement component.     position: x,y,z (e.g. '5,0,-3'). Returns 'arrived' or 'blocked'.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |
| `position` | string | ✓ |  |
| `timeout` | number |  |  (default: `15.0`) |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'position' has no description.
- **info**: Free-form string parameter 'position' has no maxLength.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "position": {
      "title": "Position",
      "type": "string"
    },
    "timeout": {
      "default": 15.0,
      "title": "Timeout",
      "type": "number"
    }
  },
  "required": [
    "path",
    "position"
  ],
  "title": "move_toArguments",
  "type": "object"
}
```

</details>

---

### `navmesh_query`

🔴 44/100 · Risk: 🔴 high

NavMesh queries and management. action: sample|path|raycast|bake|status|clear|get_settings|set_settings.     sample: find nearest walkable point to center.     path: calculate path from from_pos to to.     raycast: NavMesh raycast from from_pos toward to.     bake: build NavMesh (NavMeshSurface components or legacy NavMeshBuilder).     status: triangulation stats (triangles, vertices, areas).     clear: remove all baked NavMesh data.     get_settings: list all NavMesh agent type settings.     set_settings: update NavMeshSurface agent params (agentRadius/agentHeight/agentClimb/agentSlope).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `agentClimb` | any |  |  |
| `agentHeight` | any |  |  |
| `agentRadius` | any |  |  |
| `agentSlope` | any |  |  |
| `area_mask` | integer |  |  (default: `-1`) |
| `center` | any |  |  |
| `from_pos` | any |  |  |
| `max_distance` | number |  |  (default: `5.0`) |
| `to` | any |  |  |

<details>
<summary>18 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'center' has no description.
- **info**: Parameter 'from_pos' has no description.
- **info**: Parameter 'to' has no description.
- **info**: Parameter 'max_distance' has no description.
- **warning**: Numeric parameter 'max_distance' has no bounds.
- **info**: Parameter 'area_mask' has no description.
- **warning**: Numeric parameter 'area_mask' has no bounds.
- **info**: Parameter 'agentRadius' has no description.
- **info**: Parameter 'agentHeight' has no description.
- **info**: Parameter 'agentClimb' has no description.
- **info**: Parameter 'agentSlope' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "center": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Center"
    },
    "from_pos": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "From Pos"
    },
    "to": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "To"
    },
    "max_distance": {
      "default": 5.0,
      "title": "Max Distance",
      "type": "number"
    },
    "area_mask": {
      "default": -1,
      "title": "Area Mask",
      "type": "integer"
    },
    "agentRadius": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Agentradius"
    },
    "agentHeight": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Agentheight"
    },
    "agentClimb": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Agentclimb"
    },
    "agentSlope": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Agentslope"
    }
  },
  "required": [
    "action"
  ],
  "title": "navmesh_queryArguments",
  "type": "object"
}
```

</details>

---

### `object_diff`

🟡 75/100 · Risk: 🟡 medium

Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path_a` | string | ✓ |  |
| `path_b` | string | ✓ |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path_a' has no description.
- **info**: Free-form string parameter 'path_a' has no maxLength.
- **warning**: Path-like parameter 'path_a' has no structural constraint.
- **info**: Parameter 'path_b' has no description.
- **info**: Free-form string parameter 'path_b' has no maxLength.
- **warning**: Path-like parameter 'path_b' has no structural constraint.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path_a": {
      "title": "Path A",
      "type": "string"
    },
    "path_b": {
      "title": "Path B",
      "type": "string"
    }
  },
  "required": [
    "path_a",
    "path_b"
  ],
  "title": "object_diffArguments",
  "type": "object"
}
```

</details>

---

### `package`

🔴 56/100 · Risk: 🔴 high

Package manager. action: list|search|add|remove.     list: all installed packages.     search: query required.     add: name required, version optional.     remove: name required.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `name` | any |  |  |
| `query` | any |  |  |
| `version` | any |  |  |

<details>
<summary>10 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'version' has no description.
- **info**: Parameter 'query' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "version": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Version"
    },
    "query": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Query"
    }
  },
  "required": [
    "action"
  ],
  "title": "packageArguments",
  "type": "object"
}
```

</details>

---

### `particle`

🔴 58/100 · Risk: 🟡 medium

Particle System. action: get|create|set|apply|play|stop|pause. module=main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime. preset: fire|smoke|sparks|rain|snow|explosion|magic|dust|blood|trail.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `module` | any |  |  |
| `name` | any |  |  |
| `path` | string | ✓ |  |
| `preset` | any |  |  |
| `prop` | any |  |  |
| `value` | any |  |  |

<details>
<summary>14 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'module' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'preset' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "title": "Path",
      "type": "string"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "module": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Module"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "preset": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Preset"
    }
  },
  "required": [
    "action",
    "path"
  ],
  "title": "particleArguments",
  "type": "object"
}
```

</details>

---

### `permission_prompt`

🟡 70/100 · Risk: 🟢 low

Handle Claude permission prompts via MCP.      Registered as --permission-prompt-tool so Claude routes all permission     checks here instead of blocking on stdin.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `input` | object | ✓ |  |
| `tool_name` | string | ✓ |  |
| `tool_use_id` | string | ✓ |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'tool_name' has no description.
- **info**: Free-form string parameter 'tool_name' has no maxLength.
- **warning**: Parameter 'input' has no description.
- **warning**: Object schema does not declare properties.
- **warning**: Input object explicitly accepts arbitrary extra parameters.
- **info**: Parameter 'tool_use_id' has no description.
- **info**: Free-form string parameter 'tool_use_id' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "tool_name": {
      "title": "Tool Name",
      "type": "string"
    },
    "input": {
      "additionalProperties": true,
      "title": "Input",
      "type": "object"
    },
    "tool_use_id": {
      "title": "Tool Use Id",
      "type": "string"
    }
  },
  "required": [
    "tool_name",
    "input",
    "tool_use_id"
  ],
  "title": "permission_promptArguments",
  "type": "object"
}
```

</details>

---

### `ping_object`

🟢 82/100 · Risk: 🟡 medium

Highlight object in Hierarchy and Project, and select it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    }
  },
  "required": [
    "path"
  ],
  "title": "ping_objectArguments",
  "type": "object"
}
```

</details>

---

### `prefab`

🔴 46/100 · Risk: 🔴 high

Prefab. action: save|create_variant|apply|revert|get_overrides|unpack|edit.     edit: asset_path + component + prop + value (set property on prefab asset).     edit: asset_path + add_component or remove_component (manage components).     save: path (scene) + asset_path [+ mode: new|overwrite (default)].     revert: scope: object (default)|children.     get_overrides: format: text (default)|structured.     create_variant: base_path + variant_path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `add_component` | any |  |  |
| `asset_path` | any |  |  |
| `base_path` | any |  |  |
| `component` | any |  |  |
| `format` | any |  |  |
| `mode` | any |  |  |
| `path` | any |  |  |
| `prop` | any |  |  |
| `recursive` | boolean |  |  (default: `False`) |
| `remove_component` | any |  |  |
| `scope` | any |  |  |
| `value` | any |  |  |
| `variant_path` | any |  |  |

<details>
<summary>20 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'asset_path' has no description.
- **info**: Parameter 'base_path' has no description.
- **info**: Parameter 'variant_path' has no description.
- **info**: Parameter 'component' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'add_component' has no description.
- **info**: Parameter 'remove_component' has no description.
- **info**: Parameter 'recursive' has no description.
- **info**: Parameter 'mode' has no description.
- **info**: Parameter 'scope' has no description.
- **info**: Parameter 'format' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "asset_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Asset Path"
    },
    "base_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Base Path"
    },
    "variant_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Variant Path"
    },
    "component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Component"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "add_component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Add Component"
    },
    "remove_component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Remove Component"
    },
    "recursive": {
      "default": false,
      "title": "Recursive",
      "type": "boolean"
    },
    "mode": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Mode"
    },
    "scope": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scope"
    },
    "format": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Format"
    }
  },
  "required": [
    "action"
  ],
  "title": "prefabArguments",
  "type": "object"
}
```

</details>

---

### `profile`

🟡 62/100 · Risk: 🟢 low

Profile CPU/GPU/memory over time.     action: start|stop|status|analyze|compare|list_sessions     mode: burst (auto-stop after duration) | manual (explicit stop) | triggered (on spike)     focus: narrow analyze output to gc|rendering|physics|cpu

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `compare_with` | string |  |  (default: ``) |
| `duration` | number |  |  (default: `5.0`) |
| `focus` | string |  |  (default: ``) |
| `mode` | string |  |  (default: `burst`) |
| `session` | string |  |  (default: ``) |
| `threshold_ms` | number |  |  (default: `33.3`) |

<details>
<summary>18 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'duration' has no description.
- **warning**: Numeric parameter 'duration' has no bounds.
- **info**: Parameter 'session' has no description.
- **info**: Free-form string parameter 'session' has no maxLength.
- **info**: Parameter 'compare_with' has no description.
- **info**: Free-form string parameter 'compare_with' has no maxLength.
- **info**: Parameter 'focus' has no description.
- **info**: Free-form string parameter 'focus' has no maxLength.
- **info**: Parameter 'mode' has no description.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
- **info**: Parameter 'threshold_ms' has no description.
- **warning**: Numeric parameter 'threshold_ms' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "duration": {
      "default": 5.0,
      "title": "Duration",
      "type": "number"
    },
    "session": {
      "default": "",
      "title": "Session",
      "type": "string"
    },
    "compare_with": {
      "default": "",
      "title": "Compare With",
      "type": "string"
    },
    "focus": {
      "default": "",
      "title": "Focus",
      "type": "string"
    },
    "mode": {
      "default": "burst",
      "title": "Mode",
      "type": "string"
    },
    "threshold_ms": {
      "default": 33.3,
      "title": "Threshold Ms",
      "type": "number"
    }
  },
  "required": [
    "action"
  ],
  "title": "profileArguments",
  "type": "object"
}
```

</details>

---

### `project_settings`

🔴 53/100 · Risk: 🔴 high

Project settings. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input.     tags set: prop=remove value=<tag> to remove; else adds.     quality set prop=currentLevel: calls SetQualityLevel().     player set prop=ScriptingBackend: needs build_target (Standalone|iOS|Android|etc) + value (Mono2x|IL2CPP).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `build_target` | any |  |  |
| `index` | any |  |  |
| `prop` | any |  |  |
| `target` | string | ✓ |  |
| `value` | any |  |  |

<details>
<summary>13 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'index' has no description.
- **info**: Parameter 'build_target' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "target": {
      "title": "Target",
      "type": "string"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "index": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Index"
    },
    "build_target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Build Target"
    }
  },
  "required": [
    "action",
    "target"
  ],
  "title": "project_settingsArguments",
  "type": "object"
}
```

</details>

---

### `query_state`

🟢 87/100 · Risk: 🟡 medium

[Play Mode] Snapshot multiple game values in one call.     queries: comma-separated 'path|component|field_or_method' triplets.     Example: query_state('/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `queries` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'queries' has no description.
- **info**: Free-form string parameter 'queries' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "queries": {
      "title": "Queries",
      "type": "string"
    }
  },
  "required": [
    "queries"
  ],
  "title": "query_stateArguments",
  "type": "object"
}
```

</details>

---

### `recompile`

🟢 80/100 · Risk: 🟡 medium

Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done.

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.

</details>

---

### `reconnect_unity`

🟡 78/100 · Risk: 🟢 low

Reconnect to Unity. Port 0 or omitted = auto-discover from port files.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `port` | integer |  |  (default: `0`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'port' has no description.
- **warning**: Numeric parameter 'port' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "port": {
      "default": 0,
      "title": "Port",
      "type": "integer"
    }
  },
  "title": "reconnect_unityArguments",
  "type": "object"
}
```

</details>

---

### `references`

🟡 70/100 · Risk: 🟡 medium

References. action: get|find_to|remap. get: outgoing refs. find_to: reverse search. remap: remap refs.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `children` | boolean |  |  (default: `False`) |
| `depth` | integer |  |  (default: `1`) |
| `mappings` | any |  |  |
| `path` | string | ✓ |  |
| `source` | any |  |  |
| `target` | any |  |  |

<details>
<summary>14 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'children' has no description.
- **info**: Parameter 'depth' has no description.
- **warning**: Numeric parameter 'depth' has no bounds.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'mappings' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "title": "Path",
      "type": "string"
    },
    "children": {
      "default": false,
      "title": "Children",
      "type": "boolean"
    },
    "depth": {
      "default": 1,
      "title": "Depth",
      "type": "integer"
    },
    "source": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Source"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "mappings": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Mappings"
    }
  },
  "required": [
    "action",
    "path"
  ],
  "title": "referencesArguments",
  "type": "object"
}
```

</details>

---

### `region_clear`

🔴 55/100 · Risk: 🔴 high

Delete (or preview) all objects whose XZ pivot is inside the polygon region.      vertices: CSV polygon 'x1,z1;x2,z2;...' (>=3 pairs).     dry_run: True = list objects that WOULD be deleted (safe default). False = delete them.     filter: optional name-pattern substring; only matching objects are affected.     cap: max objects processed (default 50, hard max 200).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cap` | integer |  |  (default: `50`) |
| `dry_run` | boolean |  |  (default: `True`) |
| `filter` | any |  |  |
| `vertices` | string | ✓ |  |

<details>
<summary>11 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'vertices' has no description.
- **info**: Free-form string parameter 'vertices' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **info**: Parameter 'filter' has no description.
- **info**: Parameter 'cap' has no description.
- **warning**: Numeric parameter 'cap' has no bounds.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "vertices": {
      "title": "Vertices",
      "type": "string"
    },
    "dry_run": {
      "default": true,
      "title": "Dry Run",
      "type": "boolean"
    },
    "filter": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Filter"
    },
    "cap": {
      "default": 50,
      "title": "Cap",
      "type": "integer"
    }
  },
  "required": [
    "vertices"
  ],
  "title": "region_clearArguments",
  "type": "object"
}
```

</details>

---

### `release_smoke`

🟢 80/100 · Risk: 🟡 medium

Run release readiness checks: status, aliases, compile. Returns PASS/FAIL summary.

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.

</details>

---

### `rename_object`

🟡 76/100 · Risk: 🟡 medium

Rename a GameObject. Returns new scene path after rename.     path: current scene path or #instanceID. name: new name (non-empty).     Note: all subsequent MCP calls must use the new path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ |  |
| `path` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "name": {
      "title": "Name",
      "type": "string"
    }
  },
  "required": [
    "path",
    "name"
  ],
  "title": "rename_objectArguments",
  "type": "object"
}
```

</details>

---

### `render_analyze`

🟡 73/100 · Risk: 🟡 medium

Rendering analysis.     action: stats|materials|shaders|lights|batching|overdraw|audit|compare             |frame_debug|shadow_audit|probe_audit|light_optimize     stats: draw calls, batches, tris, verts, set-pass from UnityStats.     batching: SRP Batcher / static / dynamic / GPU instancing analysis.     audit: full rendering health check (all sections, brief).     compare: diff against last baseline snapshot.     frame_debug: per-draw-call data via FrameDebugger reflection (pauses rendering briefly).     detail: brief (default) | full.  path: optional subtree root.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `baseline_id` | any |  |  |
| `detail` | string |  |  (default: `brief`) |
| `max_events` | any |  |  |
| `path` | any |  |  |

<details>
<summary>11 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'detail' has no description.
- **info**: Free-form string parameter 'detail' has no maxLength.
- **info**: Parameter 'baseline_id' has no description.
- **info**: Parameter 'max_events' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "detail": {
      "default": "brief",
      "title": "Detail",
      "type": "string"
    },
    "baseline_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Baseline Id"
    },
    "max_events": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Max Events"
    }
  },
  "required": [
    "action"
  ],
  "title": "render_analyzeArguments",
  "type": "object"
}
```

</details>

---

### `resolve_scene_refs`

🟢 86/100 · Risk: 🟡 medium

Read-only scene reference resolver.     refs: comma-separated list of $alias, /path, or t:Type tokens.     fields: optional comma-separated field names to check existence on matched component.     Returns one tab-aligned line per ref: OK|MISS|AMB + path + details.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fields` | any |  |  |
| `refs` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'refs' has no description.
- **info**: Free-form string parameter 'refs' has no maxLength.
- **info**: Parameter 'fields' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "refs": {
      "title": "Refs",
      "type": "string"
    },
    "fields": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fields"
    }
  },
  "required": [
    "refs"
  ],
  "title": "resolve_scene_refsArguments",
  "type": "object"
}
```

</details>

---

### `resolve_test_request`

🟡 78/100 · Risk: 🟡 medium

Resolve a possibly lost start ACK without dispatching another test run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `request_id` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'request_id' has no description.
- **info**: Free-form string parameter 'request_id' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "request_id": {
      "title": "Request Id",
      "type": "string"
    }
  },
  "required": [
    "request_id"
  ],
  "title": "resolve_test_requestArguments",
  "type": "object"
}
```

</details>

---

### `resolve_tool_schema`

🟢 87/100 · Risk: 🟢 low

Return full parameter schemas for deferred tools. tools=comma-separated names.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tools` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'tools' has no description.
- **info**: Free-form string parameter 'tools' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "tools": {
      "title": "Tools",
      "type": "string"
    }
  },
  "required": [
    "tools"
  ],
  "title": "resolve_tool_schemaArguments",
  "type": "object"
}
```

</details>

---

### `run_playtest`

🟡 63/100 · Risk: 🔴 high

[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit tests, use `run_tests`).     Commands: MOVE TO x,y,z | WAIT n | WAIT_UNTIL query op value | ASSERT query op value |     ASSERT_CONSOLE_CLEAN [IGNORE "pat"] | SNAPSHOT queries | INVOKE path comp method args |     SET path comp field value | LOG msg | TIMESCALE n | ASSERT_CONSERVED SUM a+b OVER t |     ASSERT_CTA VISIBLE|CLICKABLE | VAL name query | TELEPORT path x,y,z |     ASSERT_BATCH...END | ASSERT_NEAR pathA pathB dist | INVARIANT query op value |     SIMULATE name [DURATION n] [TIMESCALE n] | MONITOR name | TRACE_FLOW FROM a TO b FIELD f |     CAPTURE label query | ASSERT_CAPTURED label INCREASED|DECREASED.     defs: inline VAL definitions prepended to script.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `abort_on_fail` | boolean |  |  (default: `False`) |
| `defs` | any |  |  |
| `fresh` | boolean |  |  (default: `False`) |
| `path` | any |  |  |
| `script` | any |  |  |
| `snapshot_on_failure` | boolean |  |  (default: `False`) |
| `timeout` | number |  |  (default: `120.0`) |

<details>
<summary>13 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'script' has no description.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'abort_on_fail' has no description.
- **info**: Parameter 'defs' has no description.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'snapshot_on_failure' has no description.
- **info**: Parameter 'fresh' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "script": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Script"
    },
    "timeout": {
      "default": 120.0,
      "title": "Timeout",
      "type": "number"
    },
    "abort_on_fail": {
      "default": false,
      "title": "Abort On Fail",
      "type": "boolean"
    },
    "defs": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Defs"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "snapshot_on_failure": {
      "default": false,
      "title": "Snapshot On Failure",
      "type": "boolean"
    },
    "fresh": {
      "default": false,
      "title": "Fresh",
      "type": "boolean"
    }
  },
  "title": "run_playtestArguments",
  "type": "object"
}
```

</details>

---

### `run_playtest_suite`

🔴 48/100 · Risk: 🔴 high

[Play Mode] Run multiple .playtest files sequentially and return a compact matrix.     pattern: glob pattern (e.g. 'Playtests/*.playtest'), comma-separated list,              or newline-separated list of project-relative paths.     suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment).     Exactly one of pattern or suite_path must be provided.     stop_on_fail=True: abort suite after first failure.     stop_after=True: exit Play Mode when suite completes.     auto_play=True: enter Play Mode automatically if not already playing.     restart_between=True: stop+play between each file to reset runtime state.     Output: SUITE: X/Y passed (Zs) + per-file line + full failure details.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `auto_play` | boolean |  |  (default: `False`) |
| `pattern` | any |  |  |
| `restart_between` | boolean |  |  (default: `False`) |
| `stop_after` | boolean |  |  (default: `True`) |
| `stop_on_fail` | boolean |  |  (default: `False`) |
| `suite_path` | any |  |  |
| `timeout_per_test` | number |  |  (default: `120.0`) |

<details>
<summary>14 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'pattern' has no description.
- **info**: Parameter 'suite_path' has no description.
- **info**: Parameter 'timeout_per_test' has no description.
- **warning**: Numeric parameter 'timeout_per_test' has no bounds.
- **info**: Parameter 'stop_on_fail' has no description.
- **info**: Parameter 'stop_after' has no description.
- **info**: Parameter 'auto_play' has no description.
- **info**: Parameter 'restart_between' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "pattern": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pattern"
    },
    "suite_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Suite Path"
    },
    "timeout_per_test": {
      "default": 120.0,
      "title": "Timeout Per Test",
      "type": "number"
    },
    "stop_on_fail": {
      "default": false,
      "title": "Stop On Fail",
      "type": "boolean"
    },
    "stop_after": {
      "default": true,
      "title": "Stop After",
      "type": "boolean"
    },
    "auto_play": {
      "default": false,
      "title": "Auto Play",
      "type": "boolean"
    },
    "restart_between": {
      "default": false,
      "title": "Restart Between",
      "type": "boolean"
    }
  },
  "title": "run_playtest_suiteArguments",
  "type": "object"
}
```

</details>

---

### `run_tests`

🟡 66/100 · Risk: 🟡 medium

Dispatch Unity tests and return their durable identity immediately.      A successful response is     ``tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched``.     If transport fails after dispatch may have happened, the result is     ``START-UNKNOWN`` with the same request_id; resolve it instead of retrying     with a new identity.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filter` | any |  |  |
| `mode` | string |  |  (default: `EditMode`) |
| `request_id` | any |  |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'mode' has no description.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
- **info**: Parameter 'filter' has no description.
- **info**: Parameter 'request_id' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "mode": {
      "default": "EditMode",
      "title": "Mode",
      "type": "string"
    },
    "filter": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Filter"
    },
    "request_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Request Id"
    }
  },
  "title": "run_testsArguments",
  "type": "object"
}
```

</details>

---

### `run_tests_wait`

🔴 38/100 · Risk: 🔴 high

Dispatch tests and wait for the exact run to become terminal.      Transport failures and domain reloads do not erase the last snapshot. A     caller timeout is observational only: it returns ``TIMEOUT`` with request,     run and snapshot data and never marks the Unity run complete.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filter` | string |  |  (default: ``) |
| `mode` | string |  |  (default: `EditMode`) |
| `poll_interval` | number |  |  (default: `5.0`) |
| `request_id` | any |  |  |
| `timeout` | number |  |  (default: `900.0`) |

<details>
<summary>16 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'mode' has no description.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
- **info**: Parameter 'filter' has no description.
- **info**: Free-form string parameter 'filter' has no maxLength.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'poll_interval' has no description.
- **warning**: Numeric parameter 'poll_interval' has no bounds.
- **info**: Parameter 'request_id' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "mode": {
      "default": "EditMode",
      "title": "Mode",
      "type": "string"
    },
    "filter": {
      "default": "",
      "title": "Filter",
      "type": "string"
    },
    "timeout": {
      "default": 900.0,
      "title": "Timeout",
      "type": "number"
    },
    "poll_interval": {
      "default": 5.0,
      "title": "Poll Interval",
      "type": "number"
    },
    "request_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Request Id"
    }
  },
  "title": "run_tests_waitArguments",
  "type": "object"
}
```

</details>

---

### `runtime_snapshot`

🟡 71/100 · Risk: 🟢 low

Snapshot all runtime objects of a given component type. Returns per-object field dump.     type: component type name (e.g. 'Rigidbody', 'EnemyController').     name: optional name substring filter.     component: component type to serialize (defaults to type).     compress: strip default-value fields to reduce response size.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | any |  |  |
| `compress` | boolean |  |  (default: `False`) |
| `name` | any |  |  |
| `type` | string | ✓ |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'component' has no description.
- **info**: Parameter 'compress' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Component"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean"
    }
  },
  "required": [
    "type"
  ],
  "title": "runtime_snapshotArguments",
  "type": "object"
}
```

</details>

---

### `save_session`

🟢 89/100 · Risk: 🟢 low

Save current scene state to .claude/session-context.json for cold-start recovery.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `save_skill`

🟡 64/100 · Risk: 🟢 low

Save a learned skill (C# code or batch commands) for reuse across sessions.     name: skill identifier. description: what it does. code: C# or batch commands.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ |  |
| `description` | string | ✓ |  |
| `name` | string | ✓ |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'description' has no description.
- **info**: Free-form string parameter 'description' has no maxLength.
- **info**: Parameter 'code' has no description.
- **info**: Free-form string parameter 'code' has no maxLength.
- **error**: Execution-like parameter 'code' accepts unconstrained free-form text.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string"
    },
    "description": {
      "title": "Description",
      "type": "string"
    },
    "code": {
      "title": "Code",
      "type": "string"
    }
  },
  "required": [
    "name",
    "description",
    "code"
  ],
  "title": "save_skillArguments",
  "type": "object"
}
```

</details>

---

### `save_template`

🟡 66/100 · Risk: 🟢 low

Save C# code as a reusable scene template in .claude/templates/.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ |  |
| `name` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'code' has no description.
- **info**: Free-form string parameter 'code' has no maxLength.
- **error**: Execution-like parameter 'code' accepts unconstrained free-form text.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string"
    },
    "code": {
      "title": "Code",
      "type": "string"
    }
  },
  "required": [
    "name",
    "code"
  ],
  "title": "save_templateArguments",
  "type": "object"
}
```

</details>

---

### `scan_scene`

🟢 89/100 · Risk: 🟢 low

Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `scene`

🟢 81/100 · Risk: 🟡 medium

Scene management. action: new|open|save|discard|open_additive|close|set_active|list.     path: required for open/save/open_additive/close/set_active. list requires no path.     scene: save/discard target when multiple scenes loaded (identifies by name).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `path` | any |  |  |
| `scene` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'scene' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "scene": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scene"
    }
  },
  "required": [
    "action"
  ],
  "title": "sceneArguments",
  "type": "object"
}
```

</details>

---

### `scene_change_plan`

🟡 75/100 · Risk: 🟡 medium

Pre-flight + plan for safe scene edit.     1. Check compile clean     2. Check console for errors     3. Resolve targets via resolve_scene_refs     4. Take checkpoint     5. Return plan_id + baseline status

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `True`) |
| `goal` | string | ✓ |  |
| `targets` | string |  |  (default: ``) |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'goal' has no description.
- **info**: Free-form string parameter 'goal' has no maxLength.
- **info**: Parameter 'targets' has no description.
- **info**: Free-form string parameter 'targets' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "goal": {
      "title": "Goal",
      "type": "string"
    },
    "targets": {
      "default": "",
      "title": "Targets",
      "type": "string"
    },
    "dry_run": {
      "default": true,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "goal"
  ],
  "title": "scene_change_planArguments",
  "type": "object"
}
```

</details>

---

### `scene_diff`

🟢 89/100 · Risk: 🟢 low

Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines.

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `scene_environment`

🟡 72/100 · Risk: 🟡 medium

Read/write scene environment: ambient light, fog, skybox, reflections.     action: get|set. set requires prop and value.     Props: ambientMode, ambientLight, ambientIntensity, ambientSkyColor, ambientEquatorColor,     ambientGroundColor, fog, fogColor, fogMode, fogDensity, fogStartDistance, fogEndDistance,     reflectionIntensity, reflectionBounces, subtractiveShadowColor, defaultReflectionResolution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string |  |  (default: `get`) |
| `prop` | any |  |  |
| `value` | any |  |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "default": "get",
      "title": "Action",
      "type": "string"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    }
  },
  "title": "scene_environmentArguments",
  "type": "object"
}
```

</details>

---

### `scene_health`

🟢 82/100 · Risk: 🟢 low

Scene hierarchy/health audit.     focus: all | hierarchy | naming | duplicates | origins | missing | empty | disabled     Returns severity-tagged findings: CRITICAL/WARNING/INFO/OK per check.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `focus` | string |  |  (default: `all`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'focus' has no description.
- **info**: Free-form string parameter 'focus' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "focus": {
      "default": "all",
      "title": "Focus",
      "type": "string"
    }
  },
  "title": "scene_healthArguments",
  "type": "object"
}
```

</details>

---

### `screenshot`

🔴 58/100 · Risk: 🟡 medium

Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens), raw=True forces path.     camera: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game. angle (single_view): front|left|top|iso|ex,ey,ez.     zoom: higher=closer. angles: per-view Euler "ex,ey,ez|..." (_=skip). supersample 1-4. offset/fixed_size: framing.     highlight: paths[:#RRGGBB] for bbox. show_colliders: wireframes.     annotation_id: frame + highlight annotation by id (auto sets camera=annotation_frame).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `angle` | any |  |  |
| `angles` | any |  |  |
| `annotation_id` | any |  |  |
| `camera` | any |  |  |
| `describe` | any |  |  |
| `fixed_size` | any |  |  |
| `height` | integer |  |  (default: `480`) |
| `highlight` | any |  |  |
| `offset` | any |  |  |
| `output_path` | any |  |  |
| `path` | any |  |  |
| `raw` | boolean |  |  (default: `False`) |
| `show_colliders` | any |  |  |
| `supersample` | any |  |  |
| `width` | integer |  |  (default: `640`) |
| `zoom` | any |  |  |

<details>
<summary>22 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'width' has no description.
- **warning**: Numeric parameter 'width' has no bounds.
- **info**: Parameter 'height' has no description.
- **warning**: Numeric parameter 'height' has no bounds.
- **info**: Parameter 'camera' has no description.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'output_path' has no description.
- **info**: Parameter 'describe' has no description.
- **info**: Parameter 'raw' has no description.
- **info**: Parameter 'zoom' has no description.
- **info**: Parameter 'angles' has no description.
- **info**: Parameter 'supersample' has no description.
- **info**: Parameter 'offset' has no description.
- **info**: Parameter 'fixed_size' has no description.
- **info**: Parameter 'highlight' has no description.
- **info**: Parameter 'show_colliders' has no description.
- **info**: Parameter 'angle' has no description.
- **info**: Parameter 'annotation_id' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "width": {
      "default": 640,
      "title": "Width",
      "type": "integer"
    },
    "height": {
      "default": 480,
      "title": "Height",
      "type": "integer"
    },
    "camera": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Camera"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "output_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Output Path"
    },
    "describe": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Describe"
    },
    "raw": {
      "default": false,
      "title": "Raw",
      "type": "boolean"
    },
    "zoom": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Zoom"
    },
    "angles": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Angles"
    },
    "supersample": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Supersample"
    },
    "offset": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Offset"
    },
    "fixed_size": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fixed Size"
    },
    "highlight": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Highlight"
    },
    "show_colliders": {
      "anyOf": [
        {
          "type": "boolean"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Show Colliders"
    },
    "angle": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Angle"
    },
    "annotation_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Annotation Id"
    }
  },
  "title": "screenshotArguments",
  "type": "object"
}
```

</details>

---

### `screenshot_baseline`

🟡 65/100 · Risk: 🟢 low

Save screenshot as baseline for visual regression. name: identifier for this baseline.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `camera` | any |  |  |
| `height` | integer |  |  (default: `480`) |
| `name` | string |  |  (default: `default`) |
| `width` | integer |  |  (default: `640`) |

<details>
<summary>11 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'width' has no description.
- **warning**: Numeric parameter 'width' has no bounds.
- **info**: Parameter 'height' has no description.
- **warning**: Numeric parameter 'height' has no bounds.
- **info**: Parameter 'camera' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "default": "default",
      "title": "Name",
      "type": "string"
    },
    "width": {
      "default": 640,
      "title": "Width",
      "type": "integer"
    },
    "height": {
      "default": 480,
      "title": "Height",
      "type": "integer"
    },
    "camera": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Camera"
    }
  },
  "title": "screenshot_baselineArguments",
  "type": "object"
}
```

</details>

---

### `screenshot_compare`

🔴 57/100 · Risk: 🟢 low

Compare current screenshot with saved baseline.     mode: auto (pixel->escalate), pixel (free), structural (Haiku general),           targeted (needs question=), ui_layout|animation|color|position (specialized).     Cached by image hashes. Cost: structural ~$0.005.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `camera` | any |  |  |
| `height` | integer |  |  (default: `480`) |
| `mode` | string |  |  (default: `auto`) |
| `name` | string |  |  (default: `default`) |
| `question` | any |  |  |
| `width` | integer |  |  (default: `640`) |

<details>
<summary>15 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'width' has no description.
- **warning**: Numeric parameter 'width' has no bounds.
- **info**: Parameter 'height' has no description.
- **warning**: Numeric parameter 'height' has no bounds.
- **info**: Parameter 'camera' has no description.
- **info**: Parameter 'mode' has no description.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
- **info**: Parameter 'question' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "default": "default",
      "title": "Name",
      "type": "string"
    },
    "width": {
      "default": 640,
      "title": "Width",
      "type": "integer"
    },
    "height": {
      "default": 480,
      "title": "Height",
      "type": "integer"
    },
    "camera": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Camera"
    },
    "mode": {
      "default": "auto",
      "title": "Mode",
      "type": "string"
    },
    "question": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Question"
    }
  },
  "title": "screenshot_compareArguments",
  "type": "object"
}
```

</details>

---

### `scriptable_object`

🟡 64/100 · Risk: 🟡 medium

ScriptableObject. action: create|get|set|list_types|find. create: type+path[+fields]. get/set: path. set/create fields: \n-separated prop=value pairs. get fields: comma-sep filter. find: type. list_types: filter.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `fields` | any |  |  |
| `filter` | any |  |  |
| `path` | any |  |  |
| `prop` | any |  |  |
| `type` | any |  |  |
| `value` | any |  |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **warning**: Parameter 'type' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'fields' has no description.
- **info**: Parameter 'filter' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Type"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "fields": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fields"
    },
    "filter": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Filter"
    }
  },
  "required": [
    "action"
  ],
  "title": "scriptable_objectArguments",
  "type": "object"
}
```

</details>

---

### `search_scene`

🟡 79/100 · Risk: 🟡 medium

Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces.     root: scope search to subtree (path or None for whole scene).     limit: max results (default 50; 0=unlimited).     scene: filter to a single scene by name (multi-scene only).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | integer |  |  (default: `50`) |
| `query` | string | ✓ |  |
| `root` | any |  |  |
| `scene` | any |  |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'query' has no description.
- **info**: Free-form string parameter 'query' has no maxLength.
- **info**: Parameter 'root' has no description.
- **info**: Parameter 'limit' has no description.
- **warning**: Numeric parameter 'limit' has no bounds.
- **info**: Parameter 'scene' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "query": {
      "title": "Query",
      "type": "string"
    },
    "root": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Root"
    },
    "limit": {
      "default": 50,
      "title": "Limit",
      "type": "integer"
    },
    "scene": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Scene"
    }
  },
  "required": [
    "query"
  ],
  "title": "search_sceneArguments",
  "type": "object"
}
```

</details>

---

### `serialized_field_rename_audit`

🟡 72/100 · Risk: 🟢 low

Audit [SerializeField] rename safety.     type: fully-qualified or simple component type name (e.g. 'MyNamespace.PlayerStats').     old_field: field name as it exists in serialized assets.     new_field: renamed field name in current C# source.     include: comma-separated scan targets (prefabs,scenes,scriptable_objects).     Returns: has_formerly_serialized_as, stale_assets, safe_to_remove_attribute, recommended_actions.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: `prefabs,scenes,scriptable_objects`) |
| `new_field` | string | ✓ |  |
| `old_field` | string | ✓ |  |
| `type` | string | ✓ |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'type' has no description.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **info**: Parameter 'old_field' has no description.
- **info**: Free-form string parameter 'old_field' has no maxLength.
- **info**: Parameter 'new_field' has no description.
- **info**: Free-form string parameter 'new_field' has no maxLength.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string"
    },
    "old_field": {
      "title": "Old Field",
      "type": "string"
    },
    "new_field": {
      "title": "New Field",
      "type": "string"
    },
    "include": {
      "default": "prefabs,scenes,scriptable_objects",
      "title": "Include",
      "type": "string"
    }
  },
  "required": [
    "type",
    "old_field",
    "new_field"
  ],
  "title": "serialized_field_rename_auditArguments",
  "type": "object"
}
```

</details>

---

### `set_active`

🟡 72/100 · Risk: 🟡 medium

Set GameObject active/inactive.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `active` | boolean | ✓ |  |
| `path` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'active' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "active": {
      "title": "Active",
      "type": "boolean"
    }
  },
  "required": [
    "path",
    "active"
  ],
  "title": "set_activeArguments",
  "type": "object"
}
```

</details>

---

### `set_llm_config`

🟢 87/100 · Risk: 🟢 low

Override LLM profiles for sampling features. Format: feature:model,turns,timeout,max_tokens (one per line).     Features: visual_verify, screenshot_describe, visual_diff, do_intent, ui_intent, vfx_intent, animator_intent, summarize, distiller.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `config` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'config' has no description.
- **info**: Free-form string parameter 'config' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "config": {
      "title": "Config",
      "type": "string"
    }
  },
  "required": [
    "config"
  ],
  "title": "set_llm_configArguments",
  "type": "object"
}
```

</details>

---

### `set_material`

🟡 70/100 · Risk: 🟡 medium

Set scene object material color (for full asset management use `material`). color: hex (#FF0000). shader: URP/Standard auto.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `color` | string | ✓ |  |
| `path` | string | ✓ |  |
| `shader` | any |  |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'color' has no description.
- **info**: Free-form string parameter 'color' has no maxLength.
- **info**: Parameter 'shader' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "color": {
      "title": "Color",
      "type": "string"
    },
    "shader": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Shader"
    }
  },
  "required": [
    "path",
    "color"
  ],
  "title": "set_materialArguments",
  "type": "object"
}
```

</details>

---

### `set_parent`

🟢 80/100 · Risk: 🟡 medium

Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `parent` | any |  |  |
| `path` | string | ✓ |  |
| `world_position_stays` | boolean |  |  (default: `True`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'parent' has no description.
- **info**: Parameter 'world_position_stays' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "parent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parent"
    },
    "world_position_stays": {
      "default": true,
      "title": "World Position Stays",
      "type": "boolean"
    }
  },
  "required": [
    "path"
  ],
  "title": "set_parentArguments",
  "type": "object"
}
```

</details>

---

### `set_properties`

🟡 71/100 · Risk: 🟡 medium

Set multiple properties on ONE object. For multiple objects, use configure_objects instead.     Format: component.prop=value per line or semicolon-separated.     Example: Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ |  |
| `props` | string | ✓ |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'props' has no description.
- **info**: Free-form string parameter 'props' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "props": {
      "title": "Props",
      "type": "string"
    }
  },
  "required": [
    "path",
    "props"
  ],
  "title": "set_propertiesArguments",
  "type": "object"
}
```

</details>

---

### `set_property`

🟡 62/100 · Risk: 🟡 medium

Set component property (Edit Mode, SerializedObject — for Play Mode use `invoke_method` or `execute_code`).     find_type: component type — bulk-sets prop on all matching objects without specifying paths.     For GO rename use rename_object(). ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), #instanceID, or 'null'. dry_run=True shows what would change without applying.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string |  |  (default: ``) |
| `dry_run` | boolean |  |  (default: `False`) |
| `find_type` | any |  |  |
| `path` | any |  |  |
| `prop` | string |  |  (default: ``) |
| `value` | string |  |  |

<details>
<summary>14 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'prop' has no description.
- **info**: Free-form string parameter 'prop' has no maxLength.
- **warning**: Parameter 'value' has no description.
- **info**: Free-form string parameter 'value' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **info**: Parameter 'find_type' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "component": {
      "default": "",
      "title": "Component",
      "type": "string"
    },
    "prop": {
      "default": "",
      "title": "Prop",
      "type": "string"
    },
    "value": {
      "default": null,
      "title": "value",
      "type": "string"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    },
    "find_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Find Type"
    }
  },
  "title": "set_propertyArguments",
  "type": "object"
}
```

</details>

---

### `set_property_delta`

🟡 76/100 · Risk: 🟡 medium

Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string | ✓ |  |
| `delta` | string | ✓ |  |
| `path` | string | ✓ |  |
| `prop` | string | ✓ |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'prop' has no description.
- **info**: Free-form string parameter 'prop' has no maxLength.
- **info**: Parameter 'delta' has no description.
- **info**: Free-form string parameter 'delta' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "component": {
      "title": "Component",
      "type": "string"
    },
    "prop": {
      "title": "Prop",
      "type": "string"
    },
    "delta": {
      "title": "Delta",
      "type": "string"
    }
  },
  "required": [
    "path",
    "component",
    "prop",
    "delta"
  ],
  "title": "set_property_deltaArguments",
  "type": "object"
}
```

</details>

---

### `set_rect`

🟡 67/100 · Risk: 🟡 medium

Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bottom-right|etc. pos/size: (x,y).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `anchor` | any |  |  |
| `offset_max` | any |  |  |
| `offset_min` | any |  |  |
| `path` | string | ✓ |  |
| `pivot` | any |  |  |
| `pos` | any |  |  |
| `size` | any |  |  |

<details>
<summary>13 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'anchor' has no description.
- **info**: Parameter 'pos' has no description.
- **info**: Parameter 'size' has no description.
- **info**: Parameter 'pivot' has no description.
- **info**: Parameter 'offset_min' has no description.
- **info**: Parameter 'offset_max' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "anchor": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Anchor"
    },
    "pos": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pos"
    },
    "size": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Size"
    },
    "pivot": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pivot"
    },
    "offset_min": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Offset Min"
    },
    "offset_max": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Offset Max"
    }
  },
  "required": [
    "path"
  ],
  "title": "set_rectArguments",
  "type": "object"
}
```

</details>

---

### `set_sibling_index`

🟡 67/100 · Risk: 🟡 medium

Set sibling index of a GameObject within its parent. index=0 moves to first child.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `index` | integer | ✓ |  |
| `path` | string | ✓ |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'index' has no description.
- **warning**: Numeric parameter 'index' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "index": {
      "title": "Index",
      "type": "integer"
    }
  },
  "required": [
    "path",
    "index"
  ],
  "title": "set_sibling_indexArguments",
  "type": "object"
}
```

</details>

---

### `setup_objects`

🟡 78/100 · Risk: 🟡 medium

Create+configure multiple objects in one call.     One per line: name [primitive=X] [parent=Y] [pos=(x,y,z)] [components=A,B]     Example: NPC1 primitive=Capsule pos=(1,0,0) components=Health

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `specs` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'specs' has no description.
- **info**: Free-form string parameter 'specs' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "specs": {
      "title": "Specs",
      "type": "string"
    }
  },
  "required": [
    "specs"
  ],
  "title": "setup_objectsArguments",
  "type": "object"
}
```

</details>

---

### `shader`

🔴 20/100 · Risk: 🔴 high

Read or write shader assets (.shader / .shadergraph). Use when you need to inspect shader properties, create a new shader from a preset or raw HLSL, change a shader property/keyword, or build/edit a Shader Graph node network.     action: get (inspect path — shader name, properties, keywords) | create (new shader; preset=unlit|lit|transparent or code=HLSL string) | set (change prop+value or keyword+enabled on existing shader) | graph_get (read Shader Graph nodes/edges) | graph_create (new .shadergraph) | graph_node (add/remove/configure a node; node_type, node_id, node_action) | graph_edge (connect/disconnect slots; output_node/output_slot, input_node/input_slot, edge_action) | graph_get_layout (read node positions as compact text) | graph_set_layout (apply positions from layout text; layout=[id] x,y WxH lines) | graph_auto_layout (auto-arrange nodes by data-flow; h_gap, v_gap optional).     For material shader assignment use `material` tool instead.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `code` | any |  |  |
| `default_value` | any |  |  |
| `edge_action` | any |  |  |
| `enabled` | any |  |  |
| `h_gap` | any |  |  |
| `input_node` | any |  |  |
| `input_slot` | any |  |  |
| `keyword` | any |  |  |
| `layout` | any |  |  |
| `name` | any |  |  |
| `new_name` | any |  |  |
| `node_action` | any |  |  |
| `node_id` | any |  |  |
| `node_type` | any |  |  |
| `output_node` | any |  |  |
| `output_slot` | any |  |  |
| `path` | string | ✓ |  |
| `preset` | any |  |  |
| `prop` | any |  |  |
| `reference_name` | any |  |  |
| `shader_name` | any |  |  |
| `target` | any |  |  |
| `type` | any |  |  |
| `v_gap` | any |  |  |
| `value` | any |  |  |

<details>
<summary>34 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'preset' has no description.
- **info**: Parameter 'code' has no description.
- **info**: Parameter 'shader_name' has no description.
- **info**: Parameter 'prop' has no description.
- **warning**: Parameter 'value' has no description.
- **info**: Parameter 'keyword' has no description.
- **info**: Parameter 'enabled' has no description.
- **info**: Parameter 'node_type' has no description.
- **info**: Parameter 'node_id' has no description.
- **info**: Parameter 'node_action' has no description.
- **info**: Parameter 'output_node' has no description.
- **info**: Parameter 'output_slot' has no description.
- **info**: Parameter 'input_node' has no description.
- **info**: Parameter 'input_slot' has no description.
- **info**: Parameter 'edge_action' has no description.
- **warning**: Parameter 'name' has no description.
- **warning**: Parameter 'type' has no description.
- **info**: Parameter 'default_value' has no description.
- **info**: Parameter 'reference_name' has no description.
- **info**: Parameter 'new_name' has no description.
- **info**: Parameter 'layout' has no description.
- **info**: Parameter 'h_gap' has no description.
- **info**: Parameter 'v_gap' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 3365 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "title": "Path",
      "type": "string"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "preset": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Preset"
    },
    "code": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Code"
    },
    "shader_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Shader Name"
    },
    "prop": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Prop"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    },
    "keyword": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Keyword"
    },
    "enabled": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Enabled"
    },
    "node_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Node Type"
    },
    "node_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Node Id"
    },
    "node_action": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Node Action"
    },
    "output_node": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Output Node"
    },
    "output_slot": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Output Slot"
    },
    "input_node": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Input Node"
    },
    "input_slot": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Input Slot"
    },
    "edge_action": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Edge Action"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Type"
    },
    "default_value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Default Value"
    },
    "reference_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Reference Name"
    },
    "new_name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "New Name"
    },
    "layout": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Layout"
    },
    "h_gap": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "H Gap"
    },
    "v_gap": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "V Gap"
    }
  },
  "required": [
    "action",
    "path"
  ],
  "title": "shaderArguments",
  "type": "object"
}
```

</details>

---

### `smart_build`

🟢 87/100 · Risk: 🟢 low

Build scene objects from natural language description using MCP sampling + execute_code.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `description` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'description' has no description.
- **info**: Free-form string parameter 'description' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "description": {
      "title": "Description",
      "type": "string"
    }
  },
  "required": [
    "description"
  ],
  "title": "smart_buildArguments",
  "type": "object"
}
```

</details>

---

### `snapshot`

🟡 78/100 · Risk: 🟡 medium

Capture or compare object state.      path: Object path ("/Enemy_01")     label: Snapshot label ("before", "after")     compare: Label to diff against (empty = capture only)      Returns:         Capture: "snapshot 'label' saved (N fields)"         Compare: structured diff or error if compare label missing

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `compare` | string |  |  (default: ``) |
| `label` | string |  |  (default: `default`) |
| `path` | string | ✓ |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'label' has no description.
- **info**: Free-form string parameter 'label' has no maxLength.
- **info**: Parameter 'compare' has no description.
- **info**: Free-form string parameter 'compare' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "label": {
      "default": "default",
      "title": "Label",
      "type": "string"
    },
    "compare": {
      "default": "",
      "title": "Compare",
      "type": "string"
    }
  },
  "required": [
    "path"
  ],
  "title": "snapshotArguments",
  "type": "object"
}
```

</details>

---

### `spatial_query`

🟡 76/100 · Risk: 🟡 medium

Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|raycast|spatial_map|objects_in_polygon.     nearest: find closest object (optionally filtered by component name).     in_front_of: position in front of object at distance.     objects_in_radius: list all objects within radius. path is optional when center='x,y,z' is given.     bounds_info: detailed bounds/dimensions of object.     raycast: cast ray from path/pos to target, returns hits sorted by distance.     spatial_map: ASCII grid map of objects in XZ plane. cell_size in meters.     objects_in_polygon: objects whose XZ pivot is inside polygon. vertices='x1,z1;x2,z2;...' (>=3 pairs). cap=max results (default 50). region_id=optional tag forwarded to Unity (e.g. for named zones).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `cap` | any |  |  |
| `cell_size` | any |  |  |
| `center` | any |  |  |
| `component` | any |  |  |
| `distance` | any |  |  |
| `layer_mask` | any |  |  |
| `path` | any |  |  |
| `radius` | any |  |  |
| `region_id` | any |  |  |
| `target` | any |  |  |
| `vertices` | any |  |  |

<details>
<summary>16 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'distance' has no description.
- **info**: Parameter 'radius' has no description.
- **info**: Parameter 'component' has no description.
- **info**: Parameter 'cell_size' has no description.
- **info**: Parameter 'layer_mask' has no description.
- **info**: Parameter 'center' has no description.
- **info**: Parameter 'vertices' has no description.
- **info**: Parameter 'region_id' has no description.
- **info**: Parameter 'cap' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Path"
    },
    "target": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target"
    },
    "distance": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Distance"
    },
    "radius": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Radius"
    },
    "component": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Component"
    },
    "cell_size": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Cell Size"
    },
    "layer_mask": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Layer Mask"
    },
    "center": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Center"
    },
    "vertices": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Vertices"
    },
    "region_id": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Region Id"
    },
    "cap": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Cap"
    }
  },
  "required": [
    "action"
  ],
  "title": "spatial_queryArguments",
  "type": "object"
}
```

</details>

---

### `sync_playtest_aliases_from_defs`

🟢 80/100 · Risk: 🟡 medium

Overwrite PlaytestConfig.asset aliases from a .defs text file.     defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs).     asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).     Invalidates AliasExpander cache after sync. Not allowed in Play Mode.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'defs' has no description.
- **info**: Free-form string parameter 'defs' has no maxLength.
- **info**: Parameter 'asset' has no description.
- **info**: Free-form string parameter 'asset' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "defs": {
      "default": "Assets/PlaytestDefs/farm_core.defs",
      "title": "Defs",
      "type": "string"
    },
    "asset": {
      "default": "Assets/Configs/PlaytestConfig.asset",
      "title": "Asset",
      "type": "string"
    }
  },
  "title": "sync_playtest_aliases_from_defsArguments",
  "type": "object"
}
```

</details>

---

### `sync_unity`

🟡 67/100 · Risk: 🟡 medium

Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code to live.      resolve=True: call Client.Resolve() first (use after package.json change).     bump=True: atomically increment plugin patch version BEFORE sync, implies resolve=True.     Returns: 'sync clean' / compile errors / timeout message.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `bump` | boolean |  |  (default: `False`) |
| `resolve` | boolean |  |  (default: `False`) |
| `timeout` | number |  |  (default: `120.0`) |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'resolve' has no description.
- **info**: Parameter 'bump' has no description.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "resolve": {
      "default": false,
      "title": "Resolve",
      "type": "boolean"
    },
    "bump": {
      "default": false,
      "title": "Bump",
      "type": "boolean"
    },
    "timeout": {
      "default": 120.0,
      "title": "Timeout",
      "type": "number"
    }
  },
  "title": "sync_unityArguments",
  "type": "object"
}
```

</details>

---

### `test_step`

🟡 64/100 · Risk: 🟡 medium

[Play Mode] Move character, snapshot state before/after, check console.     checks_before/after: comma-separated 'path|component|field' triplets.     Returns structured BEFORE/MOVE/AFTER/CONSOLE report.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `checks_after` | string |  |  (default: ``) |
| `checks_before` | string |  |  (default: ``) |
| `path` | string | ✓ |  |
| `position` | string | ✓ |  |
| `timeout` | number |  |  (default: `15.0`) |
| `wait_after` | number |  |  (default: `0.5`) |

<details>
<summary>16 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'position' has no description.
- **info**: Free-form string parameter 'position' has no maxLength.
- **info**: Parameter 'checks_before' has no description.
- **info**: Free-form string parameter 'checks_before' has no maxLength.
- **info**: Parameter 'checks_after' has no description.
- **info**: Free-form string parameter 'checks_after' has no maxLength.
- **info**: Parameter 'wait_after' has no description.
- **warning**: Numeric parameter 'wait_after' has no bounds.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "position": {
      "title": "Position",
      "type": "string"
    },
    "checks_before": {
      "default": "",
      "title": "Checks Before",
      "type": "string"
    },
    "checks_after": {
      "default": "",
      "title": "Checks After",
      "type": "string"
    },
    "wait_after": {
      "default": 0.5,
      "title": "Wait After",
      "type": "number"
    },
    "timeout": {
      "default": 15.0,
      "title": "Timeout",
      "type": "number"
    }
  },
  "required": [
    "path",
    "position"
  ],
  "title": "test_stepArguments",
  "type": "object"
}
```

</details>

---

### `timeline`

🔴 51/100 · Risk: 🟡 medium

Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinematic sequences mixing animation, audio, activation, and custom tracks — not for per-object keyframes (use `animation` for that).     action: get | create | add_track (Animation|Audio|Activation|Signal|Control|Group) | remove_track | add_clip | remove_clip | set_binding | set_timing | mute | unmute | lock | unlock | rename_track | reorder_track | duplicate_clip | add_marker | remove_marker | set_track_offset | set_duration | add_sub_track | set_clip_in | get_bindings | preview.     track=track name. index=target position for reorder_track. offset=time shift for duplicate_clip. value=offset mode (auto|transform|scene) for set_track_offset.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `asset_path` | any |  |  |
| `binding` | any |  |  |
| `blend_in` | any |  |  |
| `blend_out` | any |  |  |
| `clip` | any |  |  |
| `clip_in` | any |  |  |
| `director_path` | any |  |  |
| `duration` | any |  |  |
| `index` | any |  |  |
| `name` | any |  |  |
| `offset` | any |  |  |
| `path` | string | ✓ |  |
| `start` | any |  |  |
| `time` | any |  |  |
| `track` | any |  |  |
| `track_type` | any |  |  |
| `tracks` | any |  |  |
| `value` | any |  |  |

<details>
<summary>25 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'track' has no description.
- **info**: Parameter 'track_type' has no description.
- **info**: Parameter 'clip' has no description.
- **info**: Parameter 'binding' has no description.
- **info**: Parameter 'start' has no description.
- **info**: Parameter 'duration' has no description.
- **info**: Parameter 'blend_in' has no description.
- **info**: Parameter 'blend_out' has no description.
- **info**: Parameter 'asset_path' has no description.
- **info**: Parameter 'director_path' has no description.
- **info**: Parameter 'tracks' has no description.
- **info**: Parameter 'time' has no description.
- **warning**: Parameter 'name' has no description.
- **info**: Parameter 'clip_in' has no description.
- **info**: Parameter 'index' has no description.
- **info**: Parameter 'offset' has no description.
- **warning**: Parameter 'value' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "action": {
      "title": "Action",
      "type": "string"
    },
    "track": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Track"
    },
    "track_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Track Type"
    },
    "clip": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Clip"
    },
    "binding": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Binding"
    },
    "start": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Start"
    },
    "duration": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Duration"
    },
    "blend_in": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Blend In"
    },
    "blend_out": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Blend Out"
    },
    "asset_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Asset Path"
    },
    "director_path": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Director Path"
    },
    "tracks": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Tracks"
    },
    "time": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Time"
    },
    "name": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Name"
    },
    "clip_in": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Clip In"
    },
    "index": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Index"
    },
    "offset": {
      "anyOf": [
        {
          "type": "number"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Offset"
    },
    "value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Value"
    }
  },
  "required": [
    "path",
    "action"
  ],
  "title": "timelineArguments",
  "type": "object"
}
```

</details>

---

### `transfer_object`

🟡 77/100 · Risk: 🟡 medium

Move or copy a GameObject to another loaded scene. action: move|copy.     target_scene: destination scene name. Omit = same scene (copy = duplicate).     parent: target parent path in destination scene.     world_position_stays: preserve world transform (default True).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `parent` | any |  |  |
| `path` | string | ✓ |  |
| `target_scene` | any |  |  |
| `world_position_stays` | boolean |  |  (default: `True`) |

<details>
<summary>11 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target_scene' has no description.
- **info**: Parameter 'parent' has no description.
- **info**: Parameter 'world_position_stays' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "action": {
      "title": "Action",
      "type": "string"
    },
    "target_scene": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target Scene"
    },
    "parent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parent"
    },
    "world_position_stays": {
      "default": true,
      "title": "World Position Stays",
      "type": "boolean"
    }
  },
  "required": [
    "path",
    "action"
  ],
  "title": "transfer_objectArguments",
  "type": "object"
}
```

</details>

---

### `ui_intent`

🟡 75/100 · Risk: 🟡 medium

Convert NL intent to Unity UI hierarchy. Templates bypass Haiku.      template: hud|menu|dialog|grid. dry_run=True skips execution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `False`) |
| `intent` | string | ✓ |  |
| `parent` | any |  |  |
| `template` | any |  |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'parent' has no description.
- **info**: Parameter 'template' has no description.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "intent": {
      "title": "Intent",
      "type": "string"
    },
    "parent": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parent"
    },
    "template": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Template"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "intent"
  ],
  "title": "ui_intentArguments",
  "type": "object"
}
```

</details>

---

### `undo_last`

🟡 78/100 · Risk: 🟢 low

Undo the last N AI turns in the Unity Undo stack. Default: 1.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `turns` | integer |  |  (default: `1`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'turns' has no description.
- **warning**: Numeric parameter 'turns' has no bounds.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "turns": {
      "default": 1,
      "title": "Turns",
      "type": "integer"
    }
  },
  "title": "undo_lastArguments",
  "type": "object"
}
```

</details>

---

### `unwire_event`

🔴 53/100 · Risk: 🔴 high

Remove persistent listener(s) from UnityEvent.     index: remove specific entry (0-based). Omit to clear all.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string | ✓ |  |
| `event` | string | ✓ |  |
| `index` | any |  |  |
| `path` | string | ✓ |  |

<details>
<summary>13 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'event' has no description.
- **info**: Free-form string parameter 'event' has no maxLength.
- **info**: Parameter 'index' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "component": {
      "title": "Component",
      "type": "string"
    },
    "event": {
      "title": "Event",
      "type": "string"
    },
    "index": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Index"
    }
  },
  "required": [
    "path",
    "component",
    "event"
  ],
  "title": "unwire_eventArguments",
  "type": "object"
}
```

</details>

---

### `use_skill`

🟡 73/100 · Risk: 🟡 medium

Execute a previously saved skill. params: comma-separated key=value for substitution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ |  |
| `params` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **warning**: Parameter 'name' has no description.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'params' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string"
    },
    "params": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Params"
    }
  },
  "required": [
    "name"
  ],
  "title": "use_skillArguments",
  "type": "object"
}
```

</details>

---

### `validate_layout`

🟡 67/100 · Risk: 🟡 medium

Check trigger overlaps. Warns if triggers closer than min_distance meters.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `min_distance` | number |  |  (default: `3.0`) |
| `root` | string |  |  (default: `/`) |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'root' has no description.
- **info**: Free-form string parameter 'root' has no maxLength.
- **info**: Parameter 'min_distance' has no description.
- **warning**: Numeric parameter 'min_distance' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "root": {
      "default": "/",
      "title": "Root",
      "type": "string"
    },
    "min_distance": {
      "default": 3.0,
      "title": "Min Distance",
      "type": "number"
    }
  },
  "title": "validate_layoutArguments",
  "type": "object"
}
```

</details>

---

### `validate_playtest_aliases`

🟢 80/100 · Risk: 🟡 medium

Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed.     defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs).     asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset).     Returns 'ok: N aliases in sync' when identical, or a diff report.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'defs' has no description.
- **info**: Free-form string parameter 'defs' has no maxLength.
- **info**: Parameter 'asset' has no description.
- **info**: Free-form string parameter 'asset' has no maxLength.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "defs": {
      "default": "Assets/PlaytestDefs/farm_core.defs",
      "title": "Defs",
      "type": "string"
    },
    "asset": {
      "default": "Assets/Configs/PlaytestConfig.asset",
      "title": "Asset",
      "type": "string"
    }
  },
  "title": "validate_playtest_aliasesArguments",
  "type": "object"
}
```

</details>

---

### `validate_references`

🟡 74/100 · Risk: 🟡 medium

Validate all ObjectReference fields under path recursively.     Returns [ERROR]/[MISSING] for broken refs. Summary: "N ERROR, M OK".     Use depth=1 for quick top-level scan, depth=3-5 for full subtree.     verbose=True also shows [OK] lines (off by default to save tokens).     ignore_optional=True skips fields marked [Optional] (reduces noise).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `depth` | integer |  |  (default: `3`) |
| `ignore_optional` | boolean |  |  (default: `False`) |
| `path` | string | ✓ |  |
| `verbose` | boolean |  |  (default: `False`) |

<details>
<summary>10 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'depth' has no description.
- **warning**: Numeric parameter 'depth' has no bounds.
- **info**: Parameter 'verbose' has no description.
- **info**: Parameter 'ignore_optional' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "depth": {
      "default": 3,
      "title": "Depth",
      "type": "integer"
    },
    "verbose": {
      "default": false,
      "title": "Verbose",
      "type": "boolean"
    },
    "ignore_optional": {
      "default": false,
      "title": "Ignore Optional",
      "type": "boolean"
    }
  },
  "required": [
    "path"
  ],
  "title": "validate_referencesArguments",
  "type": "object"
}
```

</details>

---

### `verify_after_change`

🔴 59/100 · Risk: 🔴 high

Single verification gate after code/scene changes.     Gates are additive — only enabled ones run:     1. await_compile (always)     2. get_compile_errors (always)     3. get_console_since mark_id (if mark_id provided)     4. run_tests_wait mode filter (if run_tests_mode provided)     5. run_playtest_suite paths (if playtests provided)     Returns PASS only when ALL enabled gates pass.     Failure includes which gate failed and recommended next command.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `changed_files` | string |  |  (default: ``) |
| `mark_id` | string |  |  (default: ``) |
| `playtests` | string |  |  (default: ``) |
| `run_tests_mode` | string |  |  (default: ``) |
| `test_filter` | string |  |  (default: ``) |
| `timeout` | number |  |  (default: `300.0`) |

<details>
<summary>17 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'changed_files' has no description.
- **info**: Free-form string parameter 'changed_files' has no maxLength.
- **info**: Parameter 'test_filter' has no description.
- **info**: Free-form string parameter 'test_filter' has no maxLength.
- **info**: Parameter 'run_tests_mode' has no description.
- **info**: Free-form string parameter 'run_tests_mode' has no maxLength.
- **info**: Parameter 'playtests' has no description.
- **info**: Free-form string parameter 'playtests' has no maxLength.
- **info**: Parameter 'mark_id' has no description.
- **info**: Free-form string parameter 'mark_id' has no maxLength.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "changed_files": {
      "default": "",
      "title": "Changed Files",
      "type": "string"
    },
    "test_filter": {
      "default": "",
      "title": "Test Filter",
      "type": "string"
    },
    "run_tests_mode": {
      "default": "",
      "title": "Run Tests Mode",
      "type": "string"
    },
    "playtests": {
      "default": "",
      "title": "Playtests",
      "type": "string"
    },
    "mark_id": {
      "default": "",
      "title": "Mark Id",
      "type": "string"
    },
    "timeout": {
      "default": 300.0,
      "title": "Timeout",
      "type": "number"
    }
  },
  "title": "verify_after_changeArguments",
  "type": "object"
}
```

</details>

---

### `vfx_intent`

🟡 68/100 · Risk: 🟡 medium

Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely.      kind: particle|auto (shader/material not yet implemented). dry_run=True skips execution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  |  (default: `False`) |
| `intent` | string | ✓ |  |
| `kind` | string |  |  (default: `auto`) |
| `target` | string | ✓ |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'kind' has no description.
- **warning**: String parameter 'kind' appears categorical but has no enum.
- **info**: Free-form string parameter 'kind' has no maxLength.
- **info**: Parameter 'dry_run' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "target": {
      "title": "Target",
      "type": "string"
    },
    "intent": {
      "title": "Intent",
      "type": "string"
    },
    "kind": {
      "default": "auto",
      "title": "Kind",
      "type": "string"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean"
    }
  },
  "required": [
    "target",
    "intent"
  ],
  "title": "vfx_intentArguments",
  "type": "object"
}
```

</details>

---

### `wait_until`

🟡 64/100 · Risk: 🟡 medium

[Play Mode] Poll field until it matches value (or timeout).     Python timeout = Unity timeout + 5s buffer.     abort_on_fail=True: stops Play Mode on timeout.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `abort_on_fail` | boolean |  |  (default: `False`) |
| `component` | string | ✓ |  |
| `field` | string | ✓ |  |
| `negate` | boolean |  |  (default: `False`) |
| `path` | string | ✓ |  |
| `timeout` | number |  |  (default: `5.0`) |
| `value` | string | ✓ |  |

<details>
<summary>16 quality issues</summary>

- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'field' has no description.
- **info**: Free-form string parameter 'field' has no maxLength.
- **warning**: Parameter 'value' has no description.
- **info**: Free-form string parameter 'value' has no maxLength.
- **info**: Parameter 'timeout' has no description.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'negate' has no description.
- **info**: Parameter 'abort_on_fail' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "component": {
      "title": "Component",
      "type": "string"
    },
    "field": {
      "title": "Field",
      "type": "string"
    },
    "value": {
      "title": "Value",
      "type": "string"
    },
    "timeout": {
      "default": 5.0,
      "title": "Timeout",
      "type": "number"
    },
    "negate": {
      "default": false,
      "title": "Negate",
      "type": "boolean"
    },
    "abort_on_fail": {
      "default": false,
      "title": "Abort On Fail",
      "type": "boolean"
    }
  },
  "required": [
    "path",
    "component",
    "field",
    "value"
  ],
  "title": "wait_untilArguments",
  "type": "object"
}
```

</details>

---

### `watch`

🔴 40/100 · Risk: 🔴 high

[Play Mode] Manage watches. action: add|remove|clear|reset.     add: needs path/component/field. condition: '< 10','> 0','== null'.     trigger_action: 'log' or 'pause'. remove/reset: needs watch_id.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ |  |
| `component` | string |  |  (default: ``) |
| `condition` | string |  |  (default: ``) |
| `field` | string |  |  (default: ``) |
| `interval_ms` | integer |  |  (default: `500`) |
| `path` | string |  |  (default: ``) |
| `trigger_action` | string |  |  (default: `log`) |
| `watch_id` | string |  |  (default: ``) |

<details>
<summary>22 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'action' has no description.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'watch_id' has no description.
- **info**: Free-form string parameter 'watch_id' has no maxLength.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'field' has no description.
- **info**: Free-form string parameter 'field' has no maxLength.
- **info**: Parameter 'condition' has no description.
- **info**: Free-form string parameter 'condition' has no maxLength.
- **info**: Parameter 'trigger_action' has no description.
- **info**: Free-form string parameter 'trigger_action' has no maxLength.
- **info**: Parameter 'interval_ms' has no description.
- **warning**: Numeric parameter 'interval_ms' has no bounds.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string"
    },
    "watch_id": {
      "default": "",
      "title": "Watch Id",
      "type": "string"
    },
    "path": {
      "default": "",
      "title": "Path",
      "type": "string"
    },
    "component": {
      "default": "",
      "title": "Component",
      "type": "string"
    },
    "field": {
      "default": "",
      "title": "Field",
      "type": "string"
    },
    "condition": {
      "default": "",
      "title": "Condition",
      "type": "string"
    },
    "trigger_action": {
      "default": "log",
      "title": "Trigger Action",
      "type": "string"
    },
    "interval_ms": {
      "default": 500,
      "title": "Interval Ms",
      "type": "integer"
    }
  },
  "required": [
    "action"
  ],
  "title": "watchArguments",
  "type": "object"
}
```

</details>

---

### `wire_event`

🔴 52/100 · Risk: 🔴 high

Wire UnityEvent persistent listener.     path: object with the event. component: type owning the event field.     event: serialized field name (e.g. 'onClick', '_onComplete').     target: scene path or asset path. Auto-resolves component owning the method.     method: method name (e.g. 'SetActive', 'Play').     arg_type: void|bool|int|float|string|object.     arg_value: required when arg_type != void. For object: scene path or asset path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `arg_type` | string |  |  (default: `void`) |
| `arg_value` | any |  |  |
| `component` | string | ✓ |  |
| `event` | string | ✓ |  |
| `method` | string | ✓ |  |
| `path` | string | ✓ |  |
| `target` | string | ✓ |  |

<details>
<summary>18 quality issues</summary>

- **error**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema does not state whether extra parameters are allowed.
- **info**: Parameter 'path' has no description.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'component' has no description.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'event' has no description.
- **info**: Free-form string parameter 'event' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'method' has no description.
- **info**: Free-form string parameter 'method' has no maxLength.
- **info**: Parameter 'arg_type' has no description.
- **info**: Free-form string parameter 'arg_type' has no maxLength.
- **info**: Parameter 'arg_value' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string"
    },
    "component": {
      "title": "Component",
      "type": "string"
    },
    "event": {
      "title": "Event",
      "type": "string"
    },
    "target": {
      "title": "Target",
      "type": "string"
    },
    "method": {
      "title": "Method",
      "type": "string"
    },
    "arg_type": {
      "default": "void",
      "title": "Arg Type",
      "type": "string"
    },
    "arg_value": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Arg Value"
    }
  },
  "required": [
    "path",
    "component",
    "event",
    "target",
    "method"
  ],
  "title": "wire_eventArguments",
  "type": "object"
}
```

</details>

---
