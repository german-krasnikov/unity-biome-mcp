---
hide:
  - navigation
---

# MCP Tool Schema

> **163 registered tools** — auto-generated from server tool definitions.

> Quality: **83.7/100** avg score · [Glama](https://glama.ai/mcp/servers/german-krasnikov/unity-biome-mcp/schema)

## Overview

| Tool | Score | Risk | Description |
|------|-------|------|-------------|
| [`alias_status`](#alias_status) | 🟢 95/100 | 🟢 low | Check alias table health: loaded/empty/stale, sources, and total alias count. |
| [`analyze_lod_culling`](#analyze_lod_culling) | 🟢 89/100 | 🟢 low | LOD group coverage + occlusion culling analysis. |
| [`animation`](#animation) | 🟡 71/100 | 🟡 medium | Animate GameObject properties via AnimationClip. Use when you need to read or... |
| [`animator`](#animator) | 🟡 60/100 | 🔴 high | Animator Controller — state machine. Modifies animator assets. No confirmatio... |
| [`animator_intent`](#animator_intent) | 🟢 81/100 | 🟡 medium | Convert NL intent to Unity Animator Controller setup via DSL. |
| [`apply_scene_change`](#apply_scene_change) | 🟢 84/100 | 🔴 high | Execute scene mutations with atomic apply, post-verify, and optional save. |
| [`apply_template`](#apply_template) | 🟢 92/100 | 🟡 medium | Apply a scene template (.cs file from .claude/templates/). |
| [`ask`](#ask) | 🟢 93/100 | 🟢 low | Answer a read-only question about the Unity scene (AI-routed, not interactive... |
| [`ask_user`](#ask_user) | 🟢 93/100 | 🟢 low | Show a question card in Unity chat; wait for user answer (interactive UI — us... |
| [`asset`](#asset) | 🟡 74/100 | 🔴 high | Asset database. Creates, moves, or deletes assets. No confirmation required. ... |
| [`attach_uitk`](#attach_uitk) | 🟡 76/100 | 🔴 high | Attach UIDocument (or PanelRenderer on Unity 6.4+) to a GameObject (use for U... |
| [`auto_fix`](#auto_fix) | 🟢 95/100 | 🟢 low | Analyze recent Unity errors and ask MCP client sampling for a fix suggestion. |
| [`auto_wire`](#auto_wire) | 🟢 89/100 | 🔴 high | Fill null ObjectReference fields on a GameObject by matching field name or ty... |
| [`autofit_collider`](#autofit_collider) | 🟢 82/100 | 🟡 medium | Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule. |
| [`await_compile`](#await_compile) | 🟢 84/100 | 🟢 low | Block until Unity finishes compiling + reloading, then return compile errors. |
| [`bake`](#bake) | 🟢 92/100 | 🟢 low | Bake operations. |
| [`batch`](#batch) | 🟢 82/100 | 🔴 high | Execute multiple commands in one call. Use for 2+ ops — reads AND writes. com... |
| [`brief_build`](#brief_build) | 🟢 82/100 | 🔴 high | Use to get a snapshot of project state before starting work. |
| [`budget_status`](#budget_status) | 🟢 95/100 | 🟢 low | Returns Haiku cost: session/cap/day/skipped features. Text format. |
| [`build`](#build) | 🟢 90/100 | 🟡 medium | Build player. action: build. |
| [`cancel_test_run`](#cancel_test_run) | 🟢 83/100 | 🟡 medium | Request cancellation of one exact test run; cancellation is asynchronous. |
| [`check_colliders`](#check_colliders) | 🟢 90/100 | 🟡 medium | Check collider issues: triggers without Rigidbody, negative scale, micro coll... |
| [`checkpoint`](#checkpoint) | 🟡 78/100 | 🟡 medium | Create a named Undo checkpoint. Use before major scene changes. Allows rollba... |
| [`checkpoint_create`](#checkpoint_create) | 🟡 78/100 | 🟡 medium | Create a durable checkpoint before an agent turn. |
| [`checkpoint_restore`](#checkpoint_restore) | 🟢 91/100 | 🟡 medium | Restore files to their pre-turn state. |
| [`clear_held_types`](#clear_held_types) | 🟢 94/100 | 🟢 low | Clear all types held across execute_code persist_as calls. Use to free the he... |
| [`compile_preflight`](#compile_preflight) | 🟢 86/100 | 🟡 medium | Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — ca... |
| [`configure_objects`](#configure_objects) | 🟢 92/100 | 🟡 medium | Configure multiple objects at once. |
| [`console_mark`](#console_mark) | 🟢 88/100 | 🟢 low | Create a console watermark. Returns mark_id encoding current timestamp. |
| [`create_object`](#create_object) | 🟢 84/100 | 🟡 medium | Create new GameObject. components: comma-separated types to add on creation. ... |
| [`create_ui`](#create_ui) | 🟡 65/100 | 🟡 medium | Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image|T... |
| [`debug`](#debug) | 🟢 80/100 | 🟡 medium | AI-assisted scene debug: gather diagnostic context based on symptom (not comp... |
| [`debug_animator`](#debug_animator) | 🟢 89/100 | 🟡 medium | [Play Mode] Read Animator state: layers, transitions, parameters (use `debug`... |
| [`debug_physics`](#debug_physics) | 🟢 83/100 | 🟡 medium | [Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (us... |
| [`delete_object`](#delete_object) | 🟢 90/100 | 🔴 high | Delete GameObject by instance ID or scene path. Deletes scene objects. No con... |
| [`diagnose`](#diagnose) | 🟢 87/100 | 🟢 low | Read Unity compile/reload fact-signals atomically; returns typed verdict. For... |
| [`discover_tools`](#discover_tools) | 🟢 85/100 | 🟢 low | Find and enable tools by category. |
| [`do`](#do) | 🟢 83/100 | 🟡 medium | Convert natural language intent into Unity scene operations. Use when scene s... |
| [`doctor`](#doctor) | 🟡 79/100 | 🟡 medium | Run health diagnostics. fix=True removes safe stale port/lock files. |
| [`editor`](#editor) | 🟢 86/100 | 🟡 medium | Editor state/control. action: state|play|pause|stop|select|project_path|fast_... |
| [`end_write_session`](#end_write_session) | 🟡 79/100 | 🟡 medium | Release write session lock and trigger one domain reload. |
| [`execute_code`](#execute_code) | 🟢 82/100 | 🔴 high | Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile. |
| [`export_playtest_aliases_to_defs`](#export_playtest_aliases_to_defs) | 🟢 85/100 | 🟡 medium | Export PlaytestConfig.asset aliases to a readable .defs text file. |
| [`find_objects`](#find_objects) | 🟢 88/100 | 🟡 medium | Find objects by criteria. Use search_scene for complex queries. Does NOT supp... |
| [`fingerprint`](#fingerprint) | 🟢 85/100 | 🟡 medium | Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens. |
| [`get_capabilities`](#get_capabilities) | 🟢 95/100 | 🟢 low | Unity version, platform, render pipeline, scripting backend, and optional pac... |
| [`get_changes`](#get_changes) | 🟡 79/100 | 🟡 medium | Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo, |
| [`get_changeset`](#get_changeset) | 🟢 95/100 | 🟢 low | Return the current ChangeSet: accumulated mutations this session. |
| [`get_compile_errors`](#get_compile_errors) | 🟢 95/100 | 🟡 medium | Compilation errors with file:line:column. Not lost on Console.Clear(). Struct... |
| [`get_component`](#get_component) | 🟢 83/100 | 🟡 medium | Component properties as key-value. For MULTIPLE objects, use inspect(paths='a... |
| [`get_components_list`](#get_components_list) | 🟢 90/100 | 🟢 low | List all components on object by instance ID. |
| [`get_console`](#get_console) | 🟡 77/100 | 🟢 low | Recent console logs. For C# compile errors use get_compile_errors instead. ke... |
| [`get_console_since`](#get_console_since) | 🟢 84/100 | 🟢 low | Console entries after the watermark created by console_mark(). |
| [`get_enabled_tools`](#get_enabled_tools) | 🟢 95/100 | 🟢 low | List enabled tool names, comma-separated. |
| [`get_frame_stats`](#get_frame_stats) | 🟢 88/100 | 🟢 low | Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No se... |
| [`get_hierarchy`](#get_hierarchy) | 🟢 85/100 | 🔴 high | Scene hierarchy as text tree. For finding specific object by name/type use se... |
| [`get_memory`](#get_memory) | 🟢 88/100 | 🟢 low | Memory snapshot. |
| [`get_metrics`](#get_metrics) | 🟢 82/100 | 🔴 high | Returns telemetry snapshot. Clears counters when reset=True. No confirmation ... |
| [`get_object_detail`](#get_object_detail) | 🟢 90/100 | 🟢 low | Get ALL components with ALL values. Heavy. Use get_component for single compo... |
| [`get_schema`](#get_schema) | 🟢 89/100 | 🟢 low | Get all serialized fields of a component type with types. Use before set_prop... |
| [`get_selection`](#get_selection) | 🟢 95/100 | 🟡 medium | Currently selected GameObject: path and component list. |
| [`get_spatial_context`](#get_spatial_context) | 🟢 83/100 | 🟡 medium | Collider info + approach vectors + nearby objects within radius. Raycast in P... |
| [`get_test_count`](#get_test_count) | 🟢 95/100 | 🟢 low | Number of edit-mode and play-mode tests in the project. |
| [`get_test_progress`](#get_test_progress) | 🟢 89/100 | 🟢 low | Legacy progress facade. Pass run_id to correlate the response. |
| [`get_test_results`](#get_test_results) | 🟢 89/100 | 🟢 low | Legacy result facade. Pass run_id to prevent reading a stale latest run. |
| [`get_test_run`](#get_test_run) | 🟢 93/100 | 🟢 low | Return the durable JSON snapshot for one exact test run. |
| [`get_unity_events`](#get_unity_events) | 🟢 90/100 | 🟡 medium | List all UnityEvent persistent listeners in the active scene. |
| [`get_watches`](#get_watches) | 🟢 95/100 | 🟢 low | Get all active watches and recent log entries. |
| [`inspect`](#inspect) | 🟢 90/100 | 🟢 low | Get components for multiple objects at once. paths: comma-separated. componen... |
| [`inspect_uitk`](#inspect_uitk) | 🟢 87/100 | 🟡 medium | Inspect the VisualElement tree of a UIDocument or PanelRenderer panel |
| [`invoke_method`](#invoke_method) | 🟢 83/100 | 🟡 medium | [Play Mode] Call public method on a component via reflection. |
| [`lint_playtest`](#lint_playtest) | 🟢 84/100 | 🔴 high | Static validation for playtest DSL. Read-only — no scene changes. Returns war... |
| [`lint_playtest_suite`](#lint_playtest_suite) | 🟢 88/100 | 🟡 medium | Read-only preflight check across multiple .playtest files. |
| [`lint_scene_refs`](#lint_scene_refs) | 🟢 89/100 | 🔴 high | Read-only linter for scene references in DSL scripts or batch commands. |
| [`lint_ugui`](#lint_ugui) | 🟢 90/100 | 🟡 medium | Diagnose uGUI problems: missing EventSystem, Canvas without GraphicRaycaster.... |
| [`lint_uitk`](#lint_uitk) | 🟢 89/100 | 🟡 medium | Validate a UXML or USS file for structural errors and broken references |
| [`list_connections`](#list_connections) | 🟢 95/100 | 🟢 low | List Unity connection status. |
| [`list_events`](#list_events) | 🟢 86/100 | 🟡 medium | Read persistent listeners on a UnityEvent field. Use after wire_event to verify. |
| [`list_skills`](#list_skills) | 🟢 95/100 | 🟢 low | List all saved skills with descriptions and usage counts. |
| [`list_templates`](#list_templates) | 🟢 95/100 | 🟢 low | List available scene templates in .claude/templates/. |
| [`list_test_runs`](#list_test_runs) | 🟢 85/100 | 🟢 low | List recent durable test runs as JSON, newest first. |
| [`load_session`](#load_session) | 🟢 95/100 | 🟢 low | Load previous session context beside the current hierarchy. |
| [`manage_component`](#manage_component) | 🟡 77/100 | 🔴 high | Add or remove a component. Mutates scene. No confirmation required. action: '... |
| [`material`](#material) | 🟡 78/100 | 🟡 medium | Material asset management (for quick color change use `set_material`). action... |
| [`material_audit`](#material_audit) | 🟢 88/100 | 🟢 low | Material/texture scene-wide audit. |
| [`mcp_status`](#mcp_status) | 🟢 95/100 | 🟢 low | Compact MCP status: scene, dirty, play/compile state, port, alias count, vers... |
| [`menu`](#menu) | 🟢 84/100 | 🟡 medium | Execute or list Unity Editor menu items. action: execute|list. execute: run m... |
| [`move_to`](#move_to) | 🟢 81/100 | 🟡 medium | [Play Mode] Move character to position and wait for arrival. |
| [`navmesh_query`](#navmesh_query) | 🟡 70/100 | 🔴 high | NavMesh queries and management. Bakes or clears NavMesh data for bake/clear a... |
| [`object_diff`](#object_diff) | 🟢 81/100 | 🟡 medium | Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA... |
| [`package`](#package) | 🟢 88/100 | 🔴 high | Package manager. Adds or removes packages. No confirmation required. action: ... |
| [`particle`](#particle) | 🟡 76/100 | 🟡 medium | Particle System. action: get|create|set|apply|play|stop|pause. module=main|em... |
| [`permission_prompt`](#permission_prompt) | 🟡 76/100 | 🟢 low | Handle Claude permission prompts via MCP. |
| [`ping_object`](#ping_object) | 🟢 88/100 | 🟡 medium | Highlight object in Hierarchy and Project, and select it. |
| [`prefab`](#prefab) | 🟢 81/100 | 🔴 high | Prefab. Creates or modifies prefab assets. No confirmation required. action: ... |
| [`profile`](#profile) | 🟡 69/100 | 🟢 low | Profile CPU/GPU/memory over time. |
| [`project_settings`](#project_settings) | 🟢 86/100 | 🔴 high | Project settings. Modifies project settings when action=set. No confirmation ... |
| [`query_state`](#query_state) | 🟢 93/100 | 🟡 medium | [Play Mode] Snapshot multiple game values in one call. |
| [`recompile`](#recompile) | 🟢 85/100 | 🟡 medium | Trigger Unity to reimport C# scripts. Returns immediately; use await_compile ... |
| [`reconnect_unity`](#reconnect_unity) | 🟢 83/100 | 🟢 low | Reconnect to Unity. Port 0 or omitted = auto-discover from port files. |
| [`references`](#references) | 🟡 78/100 | 🟡 medium | References. action: get|find_to|remap. get: outgoing refs. find_to: reverse s... |
| [`region_clear`](#region_clear) | 🟢 82/100 | 🔴 high | Delete (or preview) all objects whose XZ pivot is inside the polygon region. ... |
| [`release_smoke`](#release_smoke) | 🟢 95/100 | 🟢 low | Run release readiness checks: status, aliases, compile. Returns PASS/FAIL sum... |
| [`rename_object`](#rename_object) | 🟢 87/100 | 🟡 medium | Rename a GameObject. Returns new scene path after rename. |
| [`render_analyze`](#render_analyze) | 🟢 90/100 | 🟡 medium | Rendering analysis. |
| [`resolve_scene_refs`](#resolve_scene_refs) | 🟢 93/100 | 🟡 medium | Read-only scene reference resolver. |
| [`resolve_test_request`](#resolve_test_request) | 🟢 93/100 | 🟢 low | Resolve a possibly lost start ACK without dispatching another test run. |
| [`resolve_tool_schema`](#resolve_tool_schema) | 🟢 92/100 | 🟢 low | Return full parameter schemas for deferred tools. tools=comma-separated names. |
| [`run_playtest`](#run_playtest) | 🟡 75/100 | 🔴 high | [Play Mode] Execute a playtest DSL script. Returns structured report (for NUn... |
| [`run_playtest_suite`](#run_playtest_suite) | 🟡 67/100 | 🔴 high | Run multiple .playtest files sequentially and return a compact matrix. |
| [`run_tests`](#run_tests) | 🟡 74/100 | 🟡 medium | Dispatch Unity tests and return their durable identity immediately. |
| [`run_tests_wait`](#run_tests_wait) | 🟡 68/100 | 🔴 high | Dispatch tests and wait for the exact run to become terminal. Dispatches test... |
| [`runtime_snapshot`](#runtime_snapshot) | 🟢 89/100 | 🟢 low | Snapshot all runtime objects of a given component type. Returns per-object fi... |
| [`save_session`](#save_session) | 🟢 94/100 | 🟢 low | Save current scene state to .claude/session-context.json for cold-start recov... |
| [`save_skill`](#save_skill) | 🟢 90/100 | 🟡 medium | Save a learned skill (C# code or batch commands) for reuse across sessions. |
| [`save_template`](#save_template) | 🟢 92/100 | 🟡 medium | Save C# code as a reusable scene template in .claude/templates/. |
| [`scan_scene`](#scan_scene) | 🟢 95/100 | 🟢 low | Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, can... |
| [`scene`](#scene) | 🟢 88/100 | 🟡 medium | Scene management. action: new|open|save|discard|open_additive|close|set_activ... |
| [`scene_change_plan`](#scene_change_plan) | 🟢 81/100 | 🟡 medium | Pre-flight + plan for safe scene edit. |
| [`scene_diff`](#scene_diff) | 🟢 95/100 | 🟢 low | Compare scene with last snapshot. First call saves snapshot. Returns diff: ad... |
| [`scene_environment`](#scene_environment) | 🟢 84/100 | 🟡 medium | Read/write scene environment: ambient light, fog, skybox, reflections. |
| [`scene_health`](#scene_health) | 🟢 88/100 | 🟢 low | Scene hierarchy/health audit. |
| [`screenshot`](#screenshot) | 🟡 65/100 | 🟡 medium | Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens... |
| [`screenshot_baseline`](#screenshot_baseline) | 🟡 75/100 | 🟡 medium | Save screenshot as baseline for visual regression. name: file-safe identifier. |
| [`screenshot_compare`](#screenshot_compare) | 🟡 68/100 | 🟡 medium | Compare current screenshot with saved baseline. |
| [`scriptable_object`](#scriptable_object) | 🟢 84/100 | 🟡 medium | ScriptableObject. action: create|get|set|list_types|find. create: type+path[+... |
| [`search_scene`](#search_scene) | 🟢 89/100 | 🟡 medium | Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, activ... |
| [`serialized_field_rename_audit`](#serialized_field_rename_audit) | 🟢 83/100 | 🟢 low | Audit [SerializeField] rename safety. |
| [`set_active`](#set_active) | 🟡 79/100 | 🟡 medium | Set GameObject active/inactive. |
| [`set_llm_config`](#set_llm_config) | 🟢 83/100 | 🟡 medium | Override LLM profiles for sampling features. Format: feature:model,turns,time... |
| [`set_material`](#set_material) | 🟡 76/100 | 🟡 medium | Set scene object material color (for full asset management use `material`). c... |
| [`set_parent`](#set_parent) | 🟡 79/100 | 🟡 medium | Reparent existing GameObject. parent=null → move to scene root. world_positio... |
| [`set_properties`](#set_properties) | 🟡 77/100 | 🟡 medium | Set multiple properties on ONE object. For multiple objects, use configure_ob... |
| [`set_property`](#set_property) | 🟡 76/100 | 🟡 medium | Set component property (Edit Mode, SerializedObject — for Play Mode use `invo... |
| [`set_property_delta`](#set_property_delta) | 🟡 75/100 | 🟡 medium | Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new. |
| [`set_rect`](#set_rect) | 🟡 72/100 | 🟡 medium | Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bott... |
| [`set_sibling_index`](#set_sibling_index) | 🟡 74/100 | 🟡 medium | Set sibling index of a GameObject within its parent. index=0 moves to first c... |
| [`setup_objects`](#setup_objects) | 🟢 83/100 | 🟡 medium | Create+configure multiple objects in one call. |
| [`shader`](#shader) | 🔴 59/100 | 🔴 high | Read or write shader assets (.shader / .shadergraph). Creates or modifies sha... |
| [`smart_build`](#smart_build) | 🟢 92/100 | 🟢 low | Build scene objects from natural language description using MCP sampling + ex... |
| [`snapshot`](#snapshot) | 🟢 85/100 | 🟡 medium | Capture or compare object state. |
| [`spatial_query`](#spatial_query) | 🟢 85/100 | 🟡 medium | Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|ra... |
| [`start_write_session`](#start_write_session) | 🟢 85/100 | 🟡 medium | Open a write session — lock assemblies + disable auto-refresh. |
| [`sync_playtest_aliases_from_defs`](#sync_playtest_aliases_from_defs) | 🟢 85/100 | 🟡 medium | Overwrite PlaytestConfig.asset aliases from a .defs text file. |
| [`sync_unity`](#sync_unity) | 🟡 73/100 | 🟡 medium | Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code... |
| [`test_step`](#test_step) | 🟡 71/100 | 🟡 medium | [Play Mode] Move character, snapshot state before/after, check console. |
| [`timeline`](#timeline) | 🟡 64/100 | 🟡 medium | Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinema... |
| [`transfer_object`](#transfer_object) | 🟢 85/100 | 🟡 medium | Move or copy a GameObject to another loaded scene. action: move|copy. |
| [`ui_intent`](#ui_intent) | 🟢 82/100 | 🟡 medium | Convert NL intent to Unity UI hierarchy. Templates bypass Haiku. |
| [`uitk_element`](#uitk_element) | 🟢 80/100 | 🔴 high | Mutate or query a VisualElement in a UIDocument or PanelRenderer host |
| [`uitk_file`](#uitk_file) | 🟡 67/100 | 🔴 high | Read or edit a UXML or USS asset file. |
| [`uitk_intent`](#uitk_intent) | 🟡 79/100 | 🔴 high | Generate a UXML + USS file pair from a natural-language UI description. |
| [`undo_last`](#undo_last) | 🟢 83/100 | 🟡 medium | Undo the last N AI turns in the Unity Undo stack. Default: 1. |
| [`unwire_event`](#unwire_event) | 🟢 86/100 | 🔴 high | Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation... |
| [`use_skill`](#use_skill) | 🟢 83/100 | 🟡 medium | Execute a previously saved skill. params: comma-separated key=value for subst... |
| [`validate_playtest_aliases`](#validate_playtest_aliases) | 🟢 86/100 | 🟡 medium | Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/... |
| [`validate_references`](#validate_references) | 🟢 82/100 | 🟡 medium | Validate all ObjectReference fields under path recursively. |
| [`validate_triggers`](#validate_triggers) | 🟢 83/100 | 🟡 medium | Check 3D trigger/collider overlaps. Warns if triggers closer than min_distanc... |
| [`verify_after_change`](#verify_after_change) | 🔴 57/100 | 🔴 high | Single verification gate after code/scene changes. |
| [`vfx_intent`](#vfx_intent) | 🟡 74/100 | 🟡 medium | Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely. |
| [`wait_until`](#wait_until) | 🟡 69/100 | 🟡 medium | [Play Mode] Poll field until it matches value (or timeout). |
| [`watch`](#watch) | 🟡 68/100 | 🔴 high | [Play Mode] Manage watches. Registers or removes watches. No confirmation req... |
| [`wire_event`](#wire_event) | 🟡 77/100 | 🔴 high | Wire UnityEvent persistent listener. Mutates scene. No confirmation required. |

---

## Tool Details

### `alias_status`

🟢 95/100 · Risk: 🟢 low

Check alias table health: loaded/empty/stale, sources, and total alias count.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `analyze_lod_culling`

🟢 89/100 · Risk: 🟢 low

LOD group coverage + occlusion culling analysis. focus: lod|culling|occlusion|null=all.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `focus` | any |  |  |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'focus' has no description.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `animation`

🟡 71/100 · Risk: 🟡 medium

Animate GameObject properties via AnimationClip. Use when you need to read or author keyframe animation on a specific object (not an Animator state machine — use `animator` for that, not this). action: get (list clips/keys) | create (new AnimationClip on object) | edit (add/replace keyframes) | preview (scrub to time) | add_event | remove_event | get_events | set_wrap (keys='loop'|'once'|'pingpong'|'clamp') | set_framerate (keys='30') | get_clip_path (returns asset path). clip=clip name, keys='t:0 v:(0,0,0); t:1 v:(0,2,0)', property=e.g. localPosition.x. component_type: Unity component to animate (default: Transform). Examples: Light, Camera, Rigidbody. binding_path: sub-object path for EditorCurveBinding (e.g. 'Head/Jaw'). Default '' = root. tangent: tangent mode for keyframes: auto (default) | smooth | linear | constant. function_name: method name for add_event. int_param/float_param/string_param: event parameters.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `binding_path` | any |  |  |
| `clip` | any |  |  |
| `clip_name` | any |  |  |
| `component_type` | any |  |  |
| `float_param` | any |  |  |
| `function_name` | any |  |  |
| `int_param` | any |  |  |
| `keys` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `property` | any |  |  |
| `string_param` | any |  |  |
| `tangent` | any |  |  |
| `time` | any |  |  |

<details>
<summary>17 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **info**: Free-form string parameter 'action' has no maxLength.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `animator`

🟡 60/100 · Risk: 🔴 high

Animator Controller — state machine. Modifies animator assets. No confirmation required. (use `animation` for keyframe clips, `timeline` for cinematics). action: get|add_param|add_state|add_transition|set_default|remove|add_blend_tree|edit_blend_tree|get_blend_tree|add_layer|remove_layer|rename_layer|set_layer_weight|set_layer_blending|set_state_speed|update_transition|set_avatar|rename_state|rename_param. params='Speed:float:0; Jump:trigger'. states='Idle:Idle.anim; Walk'. conditions='Speed>0.1; IsGrounded'. source/target=state names (*=AnyState). blend_type: 1d|2d_simple|2d_freeform|2d_cartesian|direct. param/param_y: blend parameters (auto-created as float if missing). children: '(1D) Idle:0; Walk:0.5; Run:1' or '(2D) Idle:0,0; Walk:0,1'. edit_action: add_child|remove_child|set_thresholds|set_param|set_type. layer: layer index (int) for add_state/add_transition/set_default, or name/index string for CRUD ops. weight: defaultWeight for add_layer/set_layer_weight (0.0–1.0). blending: Override|Additive for add_layer/set_layer_blending. value: speed multiplier for set_state_speed. avatar_path: asset path for set_avatar.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
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
| `name` | any |  | Name of the GameObject |
| `param` | any |  |  |
| `param_y` | any |  |  |
| `params` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `source` | any |  |  |
| `state` | any |  |  |
| `states` | any |  |  |
| `target` | any |  |  |
| `type` | any |  | Component type name (e.g. 'Rigidbody', 'BoxCollider') |
| `value` | any |  | New value to set |
| `weight` | any |  |  |

<details>
<summary>24 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
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
- **info**: Parameter 'blend_type' has no description.
- **info**: Parameter 'param' has no description.
- **info**: Parameter 'param_y' has no description.
- **info**: Parameter 'children' has no description.
- **info**: Parameter 'edit_action' has no description.
- **info**: Parameter 'layer' has no description.
- **info**: Parameter 'weight' has no description.
- **info**: Parameter 'blending' has no description.
- **info**: Parameter 'avatar_path' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 3643 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "title": "Type",
      "description": "Component type name (e.g. 'Rigidbody', 'BoxCollider')"
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
      "title": "Name",
      "description": "Name of the GameObject"
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
      "title": "Value",
      "description": "New value to set"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `animator_intent`

🟢 81/100 · Risk: 🟡 medium

Convert NL intent to Unity Animator Controller setup via DSL.  dry_run=True returns the batch plan without executing it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `intent` | string | ✓ |  |
| `target` | string | ✓ |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
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
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "target",
    "intent"
  ],
  "title": "animator_intentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `apply_scene_change`

🟢 84/100 · Risk: 🔴 high

Execute scene mutations with atomic apply, post-verify, and optional save. 1. Validate plan_id exists and not expired (TTL 600s) 2. Reject empty input and commands outside the Unity-Undo-safe scene allowlist 3. Execute an atomic, stop-on-error batch 4. On batch failure/rollback: stop without verification or save 5. If verify: require clean references and console before save 6. If save: save only after a successful batch and verification 7. Return applied, verified, and saved states separately Allowed commands: attach_uitk, auto_wire, autofit_collider, create_object, create_ui, delete_object, manage_component, rename_object, set_active, set_parent, set_property, set_property_delta, set_rect, set_sibling_index, unwire_event, wire_event. Use batch for all other command types.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `commands` | string | ✓ |  |
| `plan_id` | string | ✓ |  |
| `save` | boolean |  |  (default: `True`) |
| `verify` | boolean |  |  (default: `True`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Risky tool lacks a clear usage boundary.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `apply_template`

🟢 92/100 · Risk: 🟡 medium

Apply a scene template (.cs file from .claude/templates/). params: comma-separated key=value pairs for ${key} replacement. Example: apply_template('level_setup', 'player_pos=(0,0,0),count=3')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ | Saved scene-template identifier from .claude/templates (without .cs) |
| `params` | any |  |  |

<details>
<summary>4 quality issues</summary>

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
      "type": "string",
      "description": "Saved scene-template identifier from .claude/templates (without .cs)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `ask`

🟢 93/100 · Risk: 🟢 low

Answer a read-only question about the Unity scene (AI-routed, not interactive — use `ask_user` to show a UI card and wait for user input).  Routes to deterministic tool plans for common patterns, uses Haiku summarization for complex multi-tool results.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `question` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

- **info**: Parameter 'question' has no description.
- **info**: Free-form string parameter 'question' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `ask_user`

🟢 93/100 · Risk: 🟢 low

Show a question card in Unity chat; wait for user answer (interactive UI — use `ask` for read-only AI scene questions instead).  questions: JSON array matching AskUserQuestion schema:   [{"question":"...","header":"...","options":[{"label":"..."}],"multiSelect":false}] Returns JSON map of question→answer (or free text if Other field used). Use this instead of AskUserQuestion for in-Unity interactive prompts.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `questions` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

- **info**: Parameter 'questions' has no description.
- **info**: Free-form string parameter 'questions' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `asset`

🟡 74/100 · Risk: 🔴 high

Asset database. Creates, moves, or deletes assets. No confirmation required. action: find|get_info|create|move|validate_move|duplicate|delete|get_dependencies|find_dependents|import_settings|export_package|import_package|read_text|write_text|reimport. find: type+name+folder+labels. create: type=Folder|Material|PhysicMaterial|AnimatorController|ScriptableObject (class= required for SO). move/validate_move: source+dest (Assets/ paths). Moves .meta correctly. validate_move path_only=True: syntax check only, skips AssetDatabase folder existence check (preflight). get_dependencies: forward deps. find_dependents: reverse deps (who references this asset). export_package: path+output[+include_deps=false to skip deps]. import_package: path (filesystem). read_text: path. write_text: path+content. reimport: path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `class_name` | any |  |  |
| `content` | any |  |  |
| `dest` | any |  |  |
| `folder` | any |  |  |
| `include_deps` | boolean |  |  (default: `True`) |
| `labels` | any |  |  |
| `name` | any |  | Asset-name search filter used by find (not a GameObject name) |
| `output` | any |  |  |
| `path` | any |  | Action-specific project asset/package path (usually Assets/...; import_package accepts a package file path) |
| `path_only` | boolean |  |  (default: `False`) |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `recursive` | boolean |  |  (default: `False`) |
| `source` | any |  |  |
| `type` | any |  | Unity asset type filter or create kind (for example Material or ScriptableObject) |
| `value` | any |  | Import-setting/property value for the selected asset action |

<details>
<summary>14 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'folder' has no description.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'dest' has no description.
- **info**: Parameter 'recursive' has no description.
- **info**: Parameter 'labels' has no description.
- **info**: Parameter 'output' has no description.
- **info**: Parameter 'include_deps' has no description.
- **info**: Parameter 'content' has no description.
- **info**: Parameter 'class_name' has no description.
- **info**: Parameter 'path_only' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 2842 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Action-specific project asset/package path (usually Assets/...; import_package accepts a package file path)"
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
      "title": "Type",
      "description": "Unity asset type filter or create kind (for example Material or ScriptableObject)"
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
      "title": "Name",
      "description": "Asset-name search filter used by find (not a GameObject name)"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "Import-setting/property value for the selected asset action"
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
    },
    "path_only": {
      "default": false,
      "title": "Path Only",
      "type": "boolean"
    }
  },
  "required": [
    "action"
  ],
  "title": "assetArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `attach_uitk`

🟡 76/100 · Risk: 🔴 high

Attach UIDocument (or PanelRenderer on Unity 6.4+) to a GameObject (use for UI Toolkit runtime panels). Side effect: mutates the scene by adding one Undo-recorded UIDocument after validating every supplied asset; it does not create UXML or PanelSettings assets. path: scene path to the target GameObject. uxml: Assets/ path to .uxml VisualTreeAsset (optional; component added without VTA if omitted). panel_settings: optional Assets/ path to a PanelSettings asset; omitted leaves the field unset. sort_order: UIDocument.sortingOrder (default 0; ignored on Unity 6.4+ PanelRenderer). err: if UIDocument or PanelRenderer already present — remove it first or use inspect_uitk/uitk_element.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `panel_settings` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `sort_order` | any |  |  |
| `uxml` | any |  |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'uxml' has no description.
- **info**: Parameter 'panel_settings' has no description.
- **info**: Parameter 'sort_order' has no description.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "uxml": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Uxml"
    },
    "panel_settings": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Panel Settings"
    },
    "sort_order": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Sort Order"
    }
  },
  "required": [
    "path"
  ],
  "title": "attach_uitkArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `auto_fix`

🟢 95/100 · Risk: 🟢 low

Analyze recent Unity errors and ask MCP client sampling for a fix suggestion. This read-only tool does not edit files or apply the suggested change.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `auto_wire`

🟢 89/100 · Risk: 🔴 high

Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required. dry_run=true previews without applying. Returns wired/ambiguous/no-match summary.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>3 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "path"
  ],
  "title": "auto_wireArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `autofit_collider`

🟢 82/100 · Risk: 🟡 medium

Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `type` | string |  | Collider shape to fit: box\|sphere\|capsule (default: `box`) |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "type": {
      "default": "box",
      "title": "Type",
      "type": "string",
      "description": "Collider shape to fit: box|sphere|capsule"
    }
  },
  "required": [
    "path"
  ],
  "title": "autofit_colliderArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `await_compile`

🟢 84/100 · Risk: 🟢 low

Block until Unity finishes compiling + reloading, then return compile errors. Use after writing .cs files instead of sleep. Returns errors or 'compile clean (Xs)'. Handles domain reload disconnects transparently. timeout=0 → immediate check, no loop. Epoch-aware via sync_status when available (+10 from MAJOR-1); falls back to compile_status.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `expected_generation` | any |  |  |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `60.0`) |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'expected_generation' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "timeout": {
      "default": 60.0,
      "title": "Timeout",
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
    },
    "expected_generation": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Expected Generation"
    }
  },
  "title": "await_compileArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `bake`

🟢 92/100 · Risk: 🟢 low

Bake operations. target: lighting|occlusion. action (lighting): start(default)|status|cancel|clear|settings. action (occlusion): start(default)|status|clear. Poll status after start — lighting bake is async.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | any |  | Operation to perform — see tool docstring for allowed values |
| `target` | string | ✓ |  |

<details>
<summary>4 quality issues</summary>

- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
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
      "title": "Action",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    }
  },
  "required": [
    "target"
  ],
  "title": "bakeArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `batch`

🟢 82/100 · Risk: 🔴 high

Execute multiple commands in one call. Use for 2+ ops — reads AND writes. commands: one per line (cmd key=value). on_error: continue|stop. timeout: seconds (default 75). atomic: reverts Undo-recorded mutations on failure; external/file/asset/package/process effects may remain. defer_asset_import: wraps in StartAssetEditing/StopAssetEditing. PREFER over individual tool calls.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `atomic` | boolean |  | On failure, revert prior Undo-recorded Unity mutations; external/file/asset/package/process effects may remain (default: `False`) |
| `commands` | string | ✓ | One command per line (e.g. 'get_component path=/Player type=Transform') |
| `defer_asset_import` | boolean |  |  (default: `False`) |
| `on_error` | string |  | Error behavior: continue (default) \| stop — stop aborts remaining commands (default: `continue`) |
| `timeout` | number |  | Total timeout in seconds (default 75) (default: `75.0`) |
| `validate_aliases` | boolean |  | Dry-run alias validation before executing any mutations (default: `False`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'commands' has no maxLength.
- **info**: Free-form string parameter 'on_error' has no maxLength.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'defer_asset_import' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "commands": {
      "title": "Commands",
      "type": "string",
      "description": "One command per line (e.g. 'get_component path=/Player type=Transform')"
    },
    "on_error": {
      "default": "continue",
      "title": "On Error",
      "type": "string",
      "description": "Error behavior: continue (default) | stop \u2014 stop aborts remaining commands"
    },
    "timeout": {
      "default": 75.0,
      "title": "Timeout",
      "type": "number",
      "description": "Total timeout in seconds (default 75)"
    },
    "atomic": {
      "default": false,
      "title": "Atomic",
      "type": "boolean",
      "description": "On failure, revert prior Undo-recorded Unity mutations; external/file/asset/package/process effects may remain"
    },
    "validate_aliases": {
      "default": false,
      "title": "Validate Aliases",
      "type": "boolean",
      "description": "Dry-run alias validation before executing any mutations"
    },
    "defer_asset_import": {
      "default": false,
      "title": "Defer Asset Import",
      "type": "boolean"
    }
  },
  "required": [
    "commands"
  ],
  "title": "batchArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `brief_build`

🟢 82/100 · Risk: 🔴 high

Use to get a snapshot of project state before starting work. Returns compact text block with requested context within token budget. kinds: comma-separated subset of: console, compile_errors, hierarchy, selection, profiler budget: max token estimate (default 2000; 1 token ≈ 4 chars)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `budget` | integer |  |  (default: `2000`) |
| `kinds` | string |  |  (default: `console,compile_errors,hierarchy`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'kinds' has no description.
- **info**: Free-form string parameter 'kinds' has no maxLength.
- **info**: Parameter 'budget' has no description.
- **warning**: Numeric parameter 'budget' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "kinds": {
      "default": "console,compile_errors,hierarchy",
      "title": "Kinds",
      "type": "string"
    },
    "budget": {
      "default": 2000,
      "title": "Budget",
      "type": "integer"
    }
  },
  "title": "brief_buildArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `budget_status`

🟢 95/100 · Risk: 🟢 low

Returns Haiku cost: session/cap/day/skipped features. Text format.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `build`

🟢 90/100 · Risk: 🟡 medium

Build player. action: build. target: StandaloneWindows64|StandaloneOSX|Android|iOS|WebGL (default: active). scenes: comma-sep asset paths (default: Build Settings list). path: output path (default: Builds/<target>). dev: development build flag.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `dev` | boolean |  |  (default: `False`) |
| `path` | any |  | Player build output file or directory (default Builds/<target>) |
| `scenes` | any |  |  |
| `target` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'scenes' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Player build output file or directory (default Builds/<target>)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `cancel_test_run`

🟢 83/100 · Risk: 🟡 medium

Request cancellation of one exact test run; cancellation is asynchronous.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `check_colliders`

🟢 90/100 · Risk: 🟡 medium

Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>2 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    }
  },
  "title": "check_collidersArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `checkpoint`

🟡 78/100 · Risk: 🟡 medium

Create a named Undo checkpoint. Use before major scene changes. Allows rollback via Ctrl+Z in Unity.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `label` | string |  |  (default: `checkpoint`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `checkpoint_create`

🟡 78/100 · Risk: 🟡 medium

Create a durable checkpoint before an agent turn. paths: comma-separated file paths to snapshot. Empty = open dirty scenes.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `paths` | string |  |  (default: ``) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'paths' has no description.
- **info**: Free-form string parameter 'paths' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "paths": {
      "default": "",
      "title": "Paths",
      "type": "string"
    }
  },
  "title": "checkpoint_createArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `checkpoint_restore`

🟢 91/100 · Risk: 🟡 medium

Restore files to their pre-turn state. Tries Unity Undo first when domain stamp matches; falls back to file restore. MVP: after_refs always empty — no ChangeSet-based conflict detection yet.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `checkpoint_id` | string | ✓ |  |
| `force` | boolean |  |  (default: `False`) |

<details>
<summary>5 quality issues</summary>

- **info**: Parameter 'checkpoint_id' has no description.
- **info**: Free-form string parameter 'checkpoint_id' has no maxLength.
- **info**: Parameter 'force' has no description.
- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "checkpoint_id": {
      "title": "Checkpoint Id",
      "type": "string"
    },
    "force": {
      "default": false,
      "title": "Force",
      "type": "boolean"
    }
  },
  "required": [
    "checkpoint_id"
  ],
  "title": "checkpoint_restoreArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `clear_held_types`

🟢 94/100 · Risk: 🟢 low

Clear all types held across execute_code persist_as calls. Use to free the held-type store (~0 tokens).

<details>
<summary>2 quality issues</summary>

- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `compile_preflight`

🟢 86/100 · Risk: 🟡 medium

Validate C# WITHOUT writing/recompiling (Roslyn). Use before writing .cs — catches typos in ~200ms vs 30s recompile. file_path: Assets-relative. new_content: full file. Returns OK preflight (ms) / ERR preflight + diagnostics / [ROSLYN UNAVAILABLE].

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `file_path` | string | ✓ |  |
| `new_content` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **info**: Parameter 'file_path' has no description.
- **info**: Free-form string parameter 'file_path' has no maxLength.
- **warning**: Path-like parameter 'file_path' has no structural constraint.
- **info**: Parameter 'new_content' has no description.
- **info**: Free-form string parameter 'new_content' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `configure_objects`

🟢 92/100 · Risk: 🟡 medium

Configure multiple objects at once. Format: /Path component.prop=value [...] per line. Example: /NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100 /NPC2 Transform.m_LocalPosition=(3,0,0)

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `config` | string | ✓ |  |

<details>
<summary>4 quality issues</summary>

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `console_mark`

🟢 88/100 · Risk: 🟢 low

Create a console watermark. Returns mark_id encoding current timestamp. Pass to get_console_since() to retrieve only logs after this point. Pure Python — no TCP call.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `label` | string |  |  (default: ``) |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `create_object`

🟢 84/100 · Risk: 🟡 medium

Create new GameObject. components: comma-separated types to add on creation. primitive: Cube|Sphere|Cylinder|Capsule|Plane|Quad. prefab_path: instantiate from prefab asset. scene: create in named loaded scene (omit = active scene).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | any |  | Comma-separated component types to add on creation (e.g. 'Rigidbody,BoxCollider') |
| `name` | string | ✓ | Name for the new GameObject |
| `parent` | any |  | Scene path of the parent (omit = scene root) |
| `prefab_path` | any |  | Asset path to instantiate from prefab (e.g. Assets/Prefabs/Enemy.prefab) |
| `primitive` | any |  | Primitive mesh type: Cube\|Sphere\|Cylinder\|Capsule\|Plane\|Quad |
| `scene` | any |  | Target scene name when multiple scenes are loaded (omit = active scene) |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'name' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "name": {
      "title": "Name",
      "type": "string",
      "description": "Name for the new GameObject"
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
      "title": "Parent",
      "description": "Scene path of the parent (omit = scene root)"
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
      "title": "Components",
      "description": "Comma-separated component types to add on creation (e.g. 'Rigidbody,BoxCollider')"
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
      "title": "Primitive",
      "description": "Primitive mesh type: Cube|Sphere|Cylinder|Capsule|Plane|Quad"
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
      "title": "Prefab Path",
      "description": "Asset path to instantiate from prefab (e.g. Assets/Prefabs/Enemy.prefab)"
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
      "title": "Scene",
      "description": "Target scene name when multiple scenes are loaded (omit = active scene)"
    }
  },
  "required": [
    "name"
  ],
  "title": "create_objectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `create_ui`

🟡 65/100 · Risk: 🟡 medium

Create UI element with smart defaults. type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView. Auto-creates Canvas if needed. render_mode: SSO (ScreenSpaceOverlay, default)|SSC (ScreenSpaceCamera)|WorldSpace. font_min/font_max: enable TMP autoSizing for Text type.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `anchor` | any |  |  |
| `color` | any |  |  |
| `font_max` | any |  |  |
| `font_min` | any |  |  |
| `font_size` | any |  |  |
| `name` | any |  | Name of the GameObject |
| `parent` | any |  | Scene path to the parent GameObject |
| `pivot` | any |  |  |
| `pos` | any |  |  |
| `render_mode` | any |  |  |
| `size` | any |  |  |
| `text` | any |  |  |
| `type` | string | ✓ | uGUI element type: Canvas\|Panel\|Button\|Text\|Image\|Toggle\|Slider\|InputField\|ScrollView |

<details>
<summary>15 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **info**: Parameter 'anchor' has no description.
- **info**: Parameter 'pos' has no description.
- **info**: Parameter 'size' has no description.
- **info**: Parameter 'pivot' has no description.
- **info**: Parameter 'color' has no description.
- **warning**: Parameter 'text' has no description.
- **info**: Parameter 'font_size' has no description.
- **info**: Parameter 'render_mode' has no description.
- **info**: Parameter 'font_min' has no description.
- **info**: Parameter 'font_max' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string",
      "description": "uGUI element type: Canvas|Panel|Button|Text|Image|Toggle|Slider|InputField|ScrollView"
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
      "title": "Name",
      "description": "Name of the GameObject"
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
      "title": "Parent",
      "description": "Scene path to the parent GameObject"
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
    },
    "render_mode": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Render Mode"
    },
    "font_min": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Font Min"
    },
    "font_max": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Font Max"
    }
  },
  "required": [
    "type"
  ],
  "title": "create_uiArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `debug`

🟢 80/100 · Risk: 🟡 medium

AI-assisted scene debug: gather diagnostic context based on symptom (not compile/reload — use `diagnose` for that; not runtime state — use `debug_animator` or `debug_physics`).  symptom: Natural language description ("enemy doesn't move", "button not clickable") path: Optional target object path ("/Enemy_01") gather: Override comma-separated batch-safe tool names ("inspect,get_console")  Returns structured diagnostic text for LLM analysis.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `gather` | string |  |  (default: ``) |
| `path` | string |  | Scene path to target GameObject (e.g. /Parent/Child) (default: ``) |
| `symptom` | string |  |  (default: ``) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'symptom' has no description.
- **info**: Free-form string parameter 'symptom' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'gather' has no description.
- **info**: Free-form string parameter 'gather' has no maxLength.
- **warning**: outputSchema is missing.

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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "gather": {
      "default": "",
      "title": "Gather",
      "type": "string"
    }
  },
  "title": "debugArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `debug_animator`

🟢 89/100 · Risk: 🟡 medium

[Play Mode] Read Animator state: layers, transitions, parameters (use `debug` for scene; `diagnose` for compile). path: scene path to GameObject with Animator component.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>3 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    }
  },
  "required": [
    "path"
  ],
  "title": "debug_animatorArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `debug_physics`

🟢 83/100 · Risk: 🟡 medium

[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects (use `debug` for scene; `diagnose` for compile). path: scene path to GameObject. radius: overlap sphere radius for nearby detection (default 5m).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `radius` | number |  |  (default: `5.0`) |

<details>
<summary>5 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'radius' has no description.
- **warning**: Numeric parameter 'radius' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `delete_object`

🟢 90/100 · Risk: 🔴 high

Delete GameObject by instance ID or scene path. Deletes scene objects. No confirmation required. Provide one. force=True to delete non-empty containers.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `force` | boolean |  | True to delete non-empty container objects without error (default: `False`) |
| `id` | any |  | Instance ID of the GameObject to delete (from get_hierarchy) |
| `path` | any |  | Scene path of the GameObject to delete |

<details>
<summary>2 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: outputSchema is missing.

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
      "title": "Id",
      "description": "Instance ID of the GameObject to delete (from get_hierarchy)"
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
      "title": "Path",
      "description": "Scene path of the GameObject to delete"
    },
    "force": {
      "default": false,
      "title": "Force",
      "type": "boolean",
      "description": "True to delete non-empty container objects without error"
    }
  },
  "title": "delete_objectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `diagnose`

🟢 87/100 · Risk: 🟢 low

Read Unity compile/reload fact-signals atomically; returns typed verdict. For scene symptom analysis, use `debug`. For runtime component state, use `debug_animator` or `debug_physics`.  prev_mvid: MVID from before a sync operation. When provided, enables STALE-DOMAIN detection (unchanged MVID after intended recompile). Pass '' for standalone probing.  expected_compile: True when a compile was explicitly triggered (default). False for Bee cache-hit / will_compile=false / reverted-edit probes — prevents false STALE-DOMAIN on legitimately-frozen MVID (A5/G27).  Returns: CLEAN-LIVE / FAIL:<CS> / STALE-DOMAIN / WEDGE-ENGINE / WEDGE-STATE /          BUILD-FAILED-WEDGE / STALE-CACHE / TESTS-INVISIBLE / REBUILDING /          NO-OP / UNKNOWN

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `expected_compile` | boolean |  |  (default: `True`) |
| `prev_mvid` | string |  |  (default: ``) |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'prev_mvid' has no description.
- **info**: Free-form string parameter 'prev_mvid' has no maxLength.
- **info**: Parameter 'expected_compile' has no description.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `discover_tools`

🟢 85/100 · Risk: 🟢 low

Find and enable tools by category. Canonical 10: SCENE, COMPONENTS, ASSETS, UGUI, UITOOLKIT, MEDIA, VERIFY, RUNTIME, TESTS, SYSTEM. include_legacy=True adds legacy aliases (object, animation, etc.). structured=True adds surface/mutability info. enable=False to browse only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `category` | any |  |  |
| `enable` | boolean |  |  (default: `True`) |
| `include_legacy` | boolean |  |  (default: `False`) |
| `structured` | boolean |  |  (default: `False`) |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `do`

🟢 83/100 · Risk: 🟡 medium

Convert natural language intent into Unity scene operations. Use when scene structure unknown or task is ambiguous. NOT for targeted mutations on known objects — use batch directly.  Haiku generates a batch DSL plan, which is validated then executed. dry_run=True returns the plan without executing it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `intent` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool name 'do' is too generic for reliable tool selection.
- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
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
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "intent"
  ],
  "title": "doArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `doctor`

🟡 79/100 · Risk: 🟡 medium

Run health diagnostics. fix=True removes safe stale port/lock files.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fix` | boolean |  |  (default: `False`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `editor`

🟢 86/100 · Risk: 🟡 medium

Editor state/control. action: state|play|pause|stop|select|project_path|fast_play_mode|mutation_mode. select: path (single) or paths (comma-sep multi, e.g. "/Player,/Enemy,/NPC"). fast_play_mode/mutation_mode: enable='true'|'false' to toggle.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | `state` \| `play` \| `pause` \| `stop` \| `select` \| `project_path` \| `fast_play_mode` \| `mutation_mode` |  | Operation to perform — see tool docstring for allowed values (default: `state`) |
| `enable` | any |  |  |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `paths` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'paths' has no description.
- **info**: Parameter 'enable' has no description.
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
      "enum": [
        "state",
        "play",
        "pause",
        "stop",
        "select",
        "project_path",
        "fast_play_mode",
        "mutation_mode"
      ],
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
    },
    "enable": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Enable"
    }
  },
  "title": "editorArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `end_write_session`

🟡 79/100 · Risk: 🟡 medium

Release write session lock and trigger one domain reload. sync=True (default): waits for compile to finish before returning. sync=False: returns immediately after releasing the lock.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sync` | boolean |  |  (default: `True`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'sync' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "sync": {
      "default": true,
      "title": "Sync",
      "type": "boolean"
    }
  },
  "title": "end_write_sessionArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `execute_code`

🟢 82/100 · Risk: 🔴 high

Execute C# code in Unity Editor via Roslyn. 10-40x faster than recompile. Security uses a configurable source-pattern scan; the default AllowAll level skips it. Execution is not sandboxed. Bare statements are auto-wrapped in a static class — no boilerplate needed. persist_as stores the compiled types for reuse in the next execute_code call (Mutation Mode). Example: "var go = new GameObject(\"Test\"); return go.name;"

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ | C# code to execute in the Unity Editor context |
| `persist_as` | any |  |  |
| `undo_label` | string |  | Label for the Undo group entry (default 'execute_code') (default: `execute_code`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'code' has no maxLength.
- **info**: Free-form string parameter 'undo_label' has no maxLength.
- **info**: Parameter 'persist_as' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "code": {
      "title": "Code",
      "type": "string",
      "description": "C# code to execute in the Unity Editor context",
      "pattern": "^[\\s\\S]+$"
    },
    "undo_label": {
      "default": "execute_code",
      "title": "Undo Label",
      "type": "string",
      "description": "Label for the Undo group entry (default 'execute_code')"
    },
    "persist_as": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Persist As"
    }
  },
  "required": [
    "code"
  ],
  "title": "execute_codeArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `export_playtest_aliases_to_defs`

🟢 85/100 · Risk: 🟡 medium

Export PlaytestConfig.asset aliases to a readable .defs text file. asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset). defs: project-relative output path (default: Assets/PlaytestDefs/farm_core.defs).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `find_objects`

🟢 88/100 · Risk: 🟡 medium

Find objects by criteria. Use search_scene for complex queries. Does NOT support: parent, path, active/inactive filtering, regex. Only: name (substring), tag, layer, component (full namespace).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | any |  | Component type name on the target object |
| `layer` | any |  |  |
| `name` | any |  | Name of the GameObject |
| `tag` | any |  |  |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'tag' has no description.
- **info**: Parameter 'layer' has no description.
- **warning**: outputSchema is missing.

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
      "title": "Name",
      "description": "Name of the GameObject"
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
      "title": "Component",
      "description": "Component type name on the target object"
    }
  },
  "title": "find_objectsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `fingerprint`

🟢 85/100 · Risk: 🟡 medium

Scene state hash. Returns fp:XXXXXXXX. If unchanged, skip re-reading. ~5 tokens.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `depth` | integer |  | Maximum hierarchy depth to traverse (default: `3`) |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'depth' has no bounds.
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "depth": {
      "default": 3,
      "title": "Depth",
      "type": "integer",
      "description": "Maximum hierarchy depth to traverse"
    }
  },
  "title": "fingerprintArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_capabilities`

🟢 95/100 · Risk: 🟢 low

Unity version, platform, render pipeline, scripting backend, and optional packages available.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_changes`

🟡 79/100 · Risk: 🟡 medium

Get Unity editor changes since last call. Tracks: hierarchy changes, undo/redo, play mode, scene open/save, selection. Returns chronological event list or NO_CHANGES.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `clear` | boolean |  |  (default: `True`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_changeset`

🟢 95/100 · Risk: 🟢 low

Return the current ChangeSet: accumulated mutations this session. Use after any mutation sequence to review what changed.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_compile_errors`

🟢 95/100 · Risk: 🟡 medium

Compilation errors with file:line:column. Not lost on Console.Clear(). Structured, typed.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_component`

🟢 83/100 · Risk: 🟡 medium

Component properties as key-value. For MULTIPLE objects, use inspect(paths='a,b,c') instead — 1 call vs N. fields: comma-separated field names to keep (e.g. 'mass,position') — projects the result to save tokens; shows requested fields even at default values. Aliases: position, rotation, scale, mass, enabled, active, name. full=True: bypass distillation, return raw response. compress=True: strip default values before transfer. component: alias for type= (backward-compat with set_property naming). type= wins when both provided.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | any |  | Component type name on the target object |
| `compress` | boolean |  | Strip fields at their default value before TCP transfer — reduces response size (default: `False`) |
| `fields` | any |  | Comma-separated fields to return (e.g. 'mass,localPosition') — skips all others, saves tokens |
| `full` | boolean |  | Return raw uncompressed response (bypass distillation) (default: `False`) |
| `path` | string | ✓ | Scene path to the GameObject (e.g. /Player or /World/Enemy) |
| `type` | string |  | Component type name (e.g. 'Transform', 'Rigidbody', 'MeshRenderer') (default: ``) |

<details>
<summary>5 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to the GameObject (e.g. /Player or /World/Enemy)"
    },
    "type": {
      "default": "",
      "title": "Type",
      "type": "string",
      "description": "Component type name (e.g. 'Transform', 'Rigidbody', 'MeshRenderer')"
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
      "title": "Fields",
      "description": "Comma-separated fields to return (e.g. 'mass,localPosition') \u2014 skips all others, saves tokens"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean",
      "description": "Return raw uncompressed response (bypass distillation)"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean",
      "description": "Strip fields at their default value before TCP transfer \u2014 reduces response size"
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
      "title": "Component",
      "description": "Component type name on the target object"
    }
  },
  "required": [
    "path"
  ],
  "title": "get_componentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_components_list`

🟢 90/100 · Risk: 🟢 low

List all components on object by instance ID.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `id` | integer | ✓ | Instance ID of the GameObject (integer from get_hierarchy) |

<details>
<summary>2 quality issues</summary>

- **warning**: Numeric parameter 'id' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "id": {
      "title": "Id",
      "type": "integer",
      "description": "Instance ID of the GameObject (integer from get_hierarchy)"
    }
  },
  "required": [
    "id"
  ],
  "title": "get_components_listArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_console`

🟡 77/100 · Risk: 🟢 low

Recent console logs. For C# compile errors use get_compile_errors instead. keyword: case-insensitive substring filter. count_only: return N matches as string. since: only logs from last N seconds.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `count` | integer |  | Max log entries to return (default 10) (default: `10`) |
| `count_only` | boolean |  |  (default: `False`) |
| `first` | integer |  | Skip the first N entries (pagination offset) (default: `0`) |
| `keyword` | any |  |  |
| `level` | any |  | Filter by level: log\|warning\|error\|exception\|assert (omit = all) |
| `since` | any |  |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'count' has no bounds.
- **warning**: Numeric parameter 'first' has no bounds.
- **info**: Parameter 'keyword' has no description.
- **info**: Parameter 'count_only' has no description.
- **info**: Parameter 'since' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "count": {
      "default": 10,
      "title": "Count",
      "type": "integer",
      "description": "Max log entries to return (default 10)"
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
      "title": "Level",
      "description": "Filter by level: log|warning|error|exception|assert (omit = all)"
    },
    "first": {
      "default": 0,
      "title": "First",
      "type": "integer",
      "description": "Skip the first N entries (pagination offset)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_console_since`

🟢 84/100 · Risk: 🟢 low

Console entries after the watermark created by console_mark(). mark_id: string from console_mark() or bare float timestamp. level: optional filter ('error,exception,assert'). keyword: case-insensitive substring filter. count_only: return match count as string. count: max entries to return (default 500).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `count` | integer |  |  (default: `500`) |
| `count_only` | boolean |  |  (default: `False`) |
| `keyword` | any |  |  |
| `level` | any |  |  |
| `mark_id` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **info**: Parameter 'mark_id' has no description.
- **info**: Free-form string parameter 'mark_id' has no maxLength.
- **info**: Parameter 'level' has no description.
- **info**: Parameter 'count' has no description.
- **warning**: Numeric parameter 'count' has no bounds.
- **info**: Parameter 'keyword' has no description.
- **info**: Parameter 'count_only' has no description.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_enabled_tools`

🟢 95/100 · Risk: 🟢 low

List enabled tool names, comma-separated.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_frame_stats`

🟢 88/100 · Risk: 🟢 low

Current frame performance snapshot (fps, cpu, gpu, memory, draw calls). No session needed. include: narrow output — e.g. 'gc' for GC stats only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: ``) |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_hierarchy`

🟢 85/100 · Risk: 🔴 high

Scene hierarchy as text tree. For finding specific object by name/type use search_scene. Max 3000 nodes. Use filter/depth to narrow. Set components=true to see component types. Set compress=true to group repeated slots/points/meshes. Set summary=true for compact root-only counts (60-100 tokens). Set incremental=true to get NO_CHANGE if scene unchanged since last call. full=True: bypass distillation. scene: filter to a single scene by name (multi-scene only).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | boolean |  | Show component types next to each object (increases token cost) (default: `False`) |
| `compress` | boolean |  | Group repeated slot/point/mesh siblings to save tokens on dense scenes (default: `False`) |
| `depth` | integer |  | Traversal depth (default 2; increase for deeper trees) (default: `2`) |
| `filter` | any |  | Substring filter on GameObject name |
| `full` | boolean |  | Bypass distillation — return raw response (default: `False`) |
| `incremental` | boolean |  | Return NO_CHANGE if scene is unchanged since last call (saves tokens) (default: `False`) |
| `root` | any |  | Scene path to scope the hierarchy (omit = whole scene) |
| `scene` | any |  | Filter to a single scene by name (multi-scene only) |
| `summary` | boolean |  | Return compact root-only counts (~60-100 tokens) instead of the full tree (default: `False`) |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'depth' has no bounds.
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
      "type": "integer",
      "description": "Traversal depth (default 2; increase for deeper trees)"
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
      "title": "Root",
      "description": "Scene path to scope the hierarchy (omit = whole scene)"
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
      "title": "Filter",
      "description": "Substring filter on GameObject name"
    },
    "components": {
      "default": false,
      "title": "Components",
      "type": "boolean",
      "description": "Show component types next to each object (increases token cost)"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean",
      "description": "Group repeated slot/point/mesh siblings to save tokens on dense scenes"
    },
    "summary": {
      "default": false,
      "title": "Summary",
      "type": "boolean",
      "description": "Return compact root-only counts (~60-100 tokens) instead of the full tree"
    },
    "incremental": {
      "default": false,
      "title": "Incremental",
      "type": "boolean",
      "description": "Return NO_CHANGE if scene is unchanged since last call (saves tokens)"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean",
      "description": "Bypass distillation \u2014 return raw response"
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
      "title": "Scene",
      "description": "Filter to a single scene by name (multi-scene only)"
    }
  },
  "title": "get_hierarchyArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_memory`

🟢 88/100 · Risk: 🟢 low

Memory snapshot. include: all|textures|meshes|audio|gc — narrow the asset-type breakdown.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: `all`) |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_metrics`

🟢 82/100 · Risk: 🔴 high

Returns telemetry snapshot. Clears counters when reset=True. No confirmation required. format: text|json.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `format` | string |  |  (default: `text`) |
| `reset` | boolean |  |  (default: `False`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_object_detail`

🟢 90/100 · Risk: 🟢 low

Get ALL components with ALL values. Heavy. Use get_component for single component. full=True: bypass distillation.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `full` | boolean |  | Bypass distillation — return raw uncompressed response (default: `False`) |
| `id` | integer | ✓ | Instance ID of the GameObject (integer from get_hierarchy) |

<details>
<summary>2 quality issues</summary>

- **warning**: Numeric parameter 'id' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "id": {
      "title": "Id",
      "type": "integer",
      "description": "Instance ID of the GameObject (integer from get_hierarchy)"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean",
      "description": "Bypass distillation \u2014 return raw uncompressed response"
    }
  },
  "required": [
    "id"
  ],
  "title": "get_object_detailArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_schema`

🟢 89/100 · Risk: 🟢 low

Get all serialized fields of a component type with types. Use before set_property to know exact field names.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `type` | string | ✓ | Component type name (e.g. 'Rigidbody', 'BoxCollider') |

<details>
<summary>3 quality issues</summary>

- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string",
      "description": "Component type name (e.g. 'Rigidbody', 'BoxCollider')"
    }
  },
  "required": [
    "type"
  ],
  "title": "get_schemaArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_selection`

🟢 95/100 · Risk: 🟡 medium

Currently selected GameObject: path and component list.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_spatial_context`

🟢 83/100 · Risk: 🟡 medium

Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `radius` | number |  |  (default: `5.0`) |

<details>
<summary>5 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'radius' has no description.
- **warning**: Numeric parameter 'radius' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_test_count`

🟢 95/100 · Risk: 🟢 low

Number of edit-mode and play-mode tests in the project.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `get_test_progress`

🟢 89/100 · Risk: 🟢 low

Legacy progress facade. Pass run_id to correlate the response.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | any |  |  |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_test_results`

🟢 89/100 · Risk: 🟢 low

Legacy result facade. Pass run_id to prevent reading a stale latest run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | any |  |  |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_test_run`

🟢 93/100 · Risk: 🟢 low

Return the durable JSON snapshot for one exact test run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `run_id` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_unity_events`

🟢 90/100 · Risk: 🟡 medium

List all UnityEvent persistent listeners in the active scene. path: optional scene-path prefix filter (e.g. '/UI' to scan only the UI subtree).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>2 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    }
  },
  "title": "get_unity_eventsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `get_watches`

🟢 95/100 · Risk: 🟢 low

Get all active watches and recent log entries.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `inspect`

🟢 90/100 · Risk: 🟢 low

Get components for multiple objects at once. paths: comma-separated. components: comma-separated types (default: all). find_type: component type to find — populates paths automatically (replaces explicit paths). fields: comma-separated field names to keep across all objects — projects the result to save tokens. full=True: bypass distillation. compress=True: strip default values before transfer.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `components` | any |  | Comma-separated component types to read (omit = all) |
| `compress` | boolean |  | Strip fields at their default value before TCP transfer — reduces response size (default: `False`) |
| `fields` | any |  | Comma-separated field names to project across all objects — reduces tokens |
| `find_type` | any |  | Component type — auto-populates paths from all scene objects with this component |
| `full` | boolean |  | Return raw uncompressed response (bypass distillation) (default: `False`) |
| `paths` | any |  | Comma-separated scene paths (e.g. '/Player,/Enemy,/Camera') |

<details>
<summary>2 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: outputSchema is missing.

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
      "title": "Paths",
      "description": "Comma-separated scene paths (e.g. '/Player,/Enemy,/Camera')"
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
      "title": "Components",
      "description": "Comma-separated component types to read (omit = all)"
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
      "title": "Fields",
      "description": "Comma-separated field names to project across all objects \u2014 reduces tokens"
    },
    "full": {
      "default": false,
      "title": "Full",
      "type": "boolean",
      "description": "Return raw uncompressed response (bypass distillation)"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean",
      "description": "Strip fields at their default value before TCP transfer \u2014 reduces response size"
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
      "title": "Find Type",
      "description": "Component type \u2014 auto-populates paths from all scene objects with this component"
    }
  },
  "title": "inspectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `inspect_uitk`

🟢 87/100 · Risk: 🟡 medium

Inspect the VisualElement tree of a UIDocument or PanelRenderer panel (UI Toolkit only — use `get_component` for UIDocument component fields, use `get_hierarchy` for the scene GameObject tree, use `create_ui` for uGUI Canvas elements). Returns compact text tree with ~N refids; pass ~N to uitk_element as selector. path: scene path to UIDocument or PanelRenderer GameObject (e.g. /HUD), or 'scene' to list all. depth: max traversal depth (default 4; use selector to focus a subtree). selector: start tree from first matching element (name, .class, TypeName, ~refid). filter: show only elements whose name or classes contain this substring. show_unity_private: show #unity-* prefixed elements normally hidden by default. show_style: include non-default computed style values per element.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `depth` | any |  | Maximum hierarchy depth to traverse |
| `filter` | any |  | Substring filter to narrow results |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `selector` | any |  |  |
| `show_style` | any |  |  |
| `show_unity_private` | any |  |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'selector' has no description.
- **info**: Parameter 'show_unity_private' has no description.
- **info**: Parameter 'show_style' has no description.
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "depth": {
      "anyOf": [
        {
          "type": "integer"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Depth",
      "description": "Maximum hierarchy depth to traverse"
    },
    "selector": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Selector"
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
      "title": "Filter",
      "description": "Substring filter to narrow results"
    },
    "show_unity_private": {
      "anyOf": [
        {
          "type": "boolean"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Show Unity Private"
    },
    "show_style": {
      "anyOf": [
        {
          "type": "boolean"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Show Style"
    }
  },
  "title": "inspect_uitkArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `invoke_method`

🟢 83/100 · Risk: 🟡 medium

[Play Mode] Call public method on a component via reflection. args: comma-separated values matching method parameters. Example: invoke_method('/Player', 'PlayerController', 'MoveTo', '10,0,5')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `args` | string |  |  (default: ``) |
| `component` | string | ✓ | Component type name on the target object |
| `method` | string | ✓ |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>9 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `lint_playtest`

🟢 84/100 · Risk: 🔴 high

Static validation for playtest DSL. Read-only — no scene changes. Returns warnings list. Checks: $alias resolution, deprecated ALIAS, unimplemented steps, missing ASSERT_CONSOLE_CLEAN. path: project-relative path to .playtest file. script: inline DSL (mutually exclusive with path).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  | Project-relative path to a .playtest DSL file (mutually exclusive with script) |
| `script` | any |  |  |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema has properties but no required list.
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
      "title": "Path",
      "description": "Project-relative path to a .playtest DSL file (mutually exclusive with script)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `lint_playtest_suite`

🟢 88/100 · Risk: 🟡 medium

Read-only preflight check across multiple .playtest files. pattern: glob pattern (e.g. 'Playtests/*.playtest') or comma-separated list. suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment). Returns: aggregated lint report, one block per file.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `pattern` | any |  |  |
| `suite_path` | any |  |  |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'pattern' has no description.
- **info**: Parameter 'suite_path' has no description.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `lint_scene_refs`

🟢 89/100 · Risk: 🔴 high

Read-only linter for scene references in DSL scripts or batch commands. path: project-relative path to .playtest file. snippet: inline DSL or batch commands to lint (mutually exclusive with path). Checks: unresolved aliases, embedded aliases, missing objects, ambiguous names. Returns: 'OK: no issues' or severity-tagged issues (ERROR/WARN) with file:line:token.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | any |  | Project-relative path to a .playtest DSL file |
| `snippet` | any |  |  |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'snippet' has no description.
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
      "title": "Path",
      "description": "Project-relative path to a .playtest DSL file"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `lint_ugui`

🟢 90/100 · Risk: 🟡 medium

Diagnose uGUI problems: missing EventSystem, Canvas without GraphicRaycaster. Use when clicks miss or UI appears broken. Returns compact text: 'ok: 0 issues' or newline-separated warnings. root: scene path to root GameObject to scan (default: scan all loaded scenes).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `root` | any |  | Scene path to scope the tree (omit = whole scene) |

<details>
<summary>2 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
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
      "title": "Root",
      "description": "Scene path to scope the tree (omit = whole scene)"
    }
  },
  "title": "lint_uguiArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `lint_uitk`

🟢 89/100 · Risk: 🟡 medium

Validate a UXML or USS file for structural errors and broken references (use `get_compile_errors` for C# compile errors, use `verify_after_change` for multi-gate scene verification after mutations). Checks: A1 malformed UXML; A2 broken <Style src>; A3 missing <Template src>; A4 unnamed interactive elements; A5 duplicate USS selectors; A6 empty USS rules. fix: reserved for compatibility. True is unsupported and never changes the file. path: Assets/ path to UXML or USS file.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fix` | any |  |  |
| `path` | any |  | Assets/ path to the UXML or USS file to validate |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'fix' has no description.
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
      "title": "Path",
      "description": "Assets/ path to the UXML or USS file to validate"
    },
    "fix": {
      "anyOf": [
        {
          "type": "boolean"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Fix"
    }
  },
  "title": "lint_uitkArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `list_connections`

🟢 95/100 · Risk: 🟢 low

List Unity connection status.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `list_events`

🟢 86/100 · Risk: 🟡 medium

Read persistent listeners on a UnityEvent field. Use after wire_event to verify. Returns listener details: target path, method name, call state, arg type/value. event: serialized field name — same as wire_event 'event' param (e.g. 'onClick').

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string | ✓ | Component type name on the target object |
| `event` | string | ✓ |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'event' has no description.
- **info**: Free-form string parameter 'event' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
    },
    "event": {
      "title": "Event",
      "type": "string"
    }
  },
  "required": [
    "path",
    "component",
    "event"
  ],
  "title": "list_eventsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `list_skills`

🟢 95/100 · Risk: 🟢 low

List all saved skills with descriptions and usage counts.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `list_templates`

🟢 95/100 · Risk: 🟢 low

List available scene templates in .claude/templates/.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `list_test_runs`

🟢 85/100 · Risk: 🟢 low

List recent durable test runs as JSON, newest first.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | integer |  | Maximum number of results to return (default: `20`) |

<details>
<summary>3 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'limit' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "limit": {
      "default": 20,
      "title": "Limit",
      "type": "integer",
      "description": "Maximum number of results to return"
    }
  },
  "title": "list_test_runsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `load_session`

🟢 95/100 · Risk: 🟢 low

Load previous session context beside the current hierarchy.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `manage_component`

🟡 77/100 · Risk: 🔴 high

Add or remove a component. Mutates scene. No confirmation required. action: 'add' or 'remove' ONLY (no 'enable'/'disable' — use set_property with prop='m_Enabled' for that). type: short name (e.g. 'Button') or full namespace (e.g. 'UnityEngine.UI.Button').

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | 'add' or 'remove' — not 'enable'/'disable' (use set_property m_Enabled for that) |
| `path` | string | ✓ | Scene path to the GameObject |
| `type` | string | ✓ | Component type (short: 'Button' or full namespace: 'UnityEngine.UI.Button') |

<details>
<summary>7 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
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
      "type": "string",
      "description": "Scene path to the GameObject"
    },
    "type": {
      "title": "Type",
      "type": "string",
      "description": "Component type (short: 'Button' or full namespace: 'UnityEngine.UI.Button')"
    },
    "action": {
      "title": "Action",
      "type": "string",
      "description": "'add' or 'remove' \u2014 not 'enable'/'disable' (use set_property m_Enabled for that)"
    }
  },
  "required": [
    "path",
    "type",
    "action"
  ],
  "title": "manage_componentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `material`

🟡 78/100 · Risk: 🟡 medium

Material asset management (for quick color change use `set_material`). action: create|get|set|copy|list_properties|list_slots|get_errors|list_shaders|set_fields. create: path+shader. get/set: path (asset) or object_path (scene). copy: source+targets (comma-sep scene paths). slot: material slot index (default 0). list_slots: object_path. get_errors: path (shader asset). list_shaders: filter (optional name filter). set_fields: path+value (newline-separated prop=val). set target: shared|instance|asset (default shared).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `filter` | any |  | Substring filter to narrow results |
| `object_path` | any |  |  |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `shader` | any |  |  |
| `slot` | any |  |  |
| `source` | any |  |  |
| `target` | any |  |  |
| `targets` | any |  |  |
| `value` | any |  | New value to set |

<details>
<summary>10 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'object_path' has no description.
- **info**: Parameter 'shader' has no description.
- **info**: Parameter 'source' has no description.
- **info**: Parameter 'targets' has no description.
- **info**: Parameter 'slot' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
      "title": "Filter",
      "description": "Substring filter to narrow results"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `material_audit`

🟢 88/100 · Risk: 🟢 low

Material/texture scene-wide audit. action: summary|materials|textures|duplicates|compression|recommendations. platform: Android|iOS|Standalone|Default (for compression check).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string |  | Operation to perform — see tool docstring for allowed values (default: `summary`) |
| `platform` | any |  |  |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'platform' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "default": "summary",
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `mcp_status`

🟢 95/100 · Risk: 🟢 low

Compact MCP status: scene, dirty, play/compile state, port, alias count, version.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `menu`

🟢 84/100 · Risk: 🟡 medium

Execute or list Unity Editor menu items. action: execute|list. execute: run menu item by path. list: show sub-items (omit path for all roots). Note: Edit/ menu items not supported by Unity API.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `path` | any |  | Unity Editor menu-item path to execute, or submenu prefix to list |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'action' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Unity Editor menu-item path to execute, or submenu prefix to list"
    }
  },
  "required": [
    "action"
  ],
  "title": "menuArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `move_to`

🟢 81/100 · Risk: 🟡 medium

[Play Mode] Move character to position and wait for arrival. path: scene path to GO with movement component. position: x,y,z (e.g. '5,0,-3'). Returns 'arrived' or 'blocked'.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `position` | string | ✓ |  |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `15.0`) |

<details>
<summary>7 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'position' has no description.
- **info**: Free-form string parameter 'position' has no maxLength.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "position": {
      "title": "Position",
      "type": "string"
    },
    "timeout": {
      "default": 15.0,
      "title": "Timeout",
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
    }
  },
  "required": [
    "path",
    "position"
  ],
  "title": "move_toArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `navmesh_query`

🟡 70/100 · Risk: 🔴 high

NavMesh queries and management. Bakes or clears NavMesh data for bake/clear actions. No confirmation required. action: sample|path|raycast|bake|status|clear|get_settings|set_settings. sample: find nearest walkable point to center. path: calculate path from from_pos to to. raycast: NavMesh raycast from from_pos toward to. bake: build NavMesh (NavMeshSurface components or legacy NavMeshBuilder). status: triangulation stats (triangles, vertices, areas). clear: remove all baked NavMesh data. get_settings: list all NavMesh agent type settings. set_settings: update NavMeshSurface agent params (agentRadius/agentHeight/agentClimb/agentSlope).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
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
<summary>14 quality issues</summary>

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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `object_diff`

🟢 81/100 · Risk: 🟡 medium

Diff two GameObjects (components, properties, children). Cross-scene: 'SceneA:/Alice'.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path_a` | string | ✓ |  |
| `path_b` | string | ✓ |  |

<details>
<summary>7 quality issues</summary>

- **info**: Parameter 'path_a' has no description.
- **info**: Free-form string parameter 'path_a' has no maxLength.
- **warning**: Path-like parameter 'path_a' has no structural constraint.
- **info**: Parameter 'path_b' has no description.
- **info**: Free-form string parameter 'path_b' has no maxLength.
- **warning**: Path-like parameter 'path_b' has no structural constraint.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `package`

🟢 88/100 · Risk: 🔴 high

Package manager. Adds or removes packages. No confirmation required. action: list|search|add|remove. list: all installed packages. search: query required. add: name required, version optional. remove: name required.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `name` | any |  | Unity Package Manager package identifier for add/remove (for example com.unity.inputsystem) |
| `query` | any |  | Search query — see tool docstring for syntax |
| `version` | any |  |  |

<details>
<summary>4 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'version' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Name",
      "description": "Unity Package Manager package identifier for add/remove (for example com.unity.inputsystem)"
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
      "title": "Query",
      "description": "Search query \u2014 see tool docstring for syntax"
    }
  },
  "required": [
    "action"
  ],
  "title": "packageArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `particle`

🟡 76/100 · Risk: 🟡 medium

Particle System. action: get|create|set|apply|play|stop|pause. module=main|emission|shape|colorOverLifetime|sizeOverLifetime|velocityOverLifetime|noise|renderer|trails|collision|rotationOverLifetime. preset: fire|smoke|sparks|rain|snow|explosion|magic|dust|blood|trail.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `module` | any |  |  |
| `name` | any |  | Name of the GameObject |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `preset` | any |  |  |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `value` | any |  | New value to set |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'module' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "title": "Name",
      "description": "Name of the GameObject"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `permission_prompt`

🟡 76/100 · Risk: 🟢 low

Handle Claude permission prompts via MCP.  Registered as --permission-prompt-tool so Claude routes all permission checks here instead of blocking on stdin.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `input` | object | ✓ |  |
| `tool_name` | string | ✓ |  |
| `tool_use_id` | string | ✓ |  |

<details>
<summary>8 quality issues</summary>

- **info**: Parameter 'tool_name' has no description.
- **info**: Free-form string parameter 'tool_name' has no maxLength.
- **warning**: Parameter 'input' has no description.
- **warning**: Object schema does not declare properties.
- **warning**: Input object explicitly accepts arbitrary extra parameters.
- **info**: Parameter 'tool_use_id' has no description.
- **info**: Free-form string parameter 'tool_use_id' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `ping_object`

🟢 88/100 · Risk: 🟡 medium

Highlight object in Hierarchy and Project, and select it.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>4 quality issues</summary>

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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    }
  },
  "required": [
    "path"
  ],
  "title": "ping_objectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `prefab`

🟢 81/100 · Risk: 🔴 high

Prefab. Creates or modifies prefab assets. No confirmation required. action: save|create_variant|apply|revert|get_overrides|unpack|edit|instantiate. edit: asset_path + component + prop + value (set property on prefab asset). edit: asset_path + add_component or remove_component (manage components). save: path (scene) + asset_path [+ mode: new|overwrite (default)]. revert: scope: object (default)|children. get_overrides: format: text (default)|structured. create_variant: base_path + variant_path. instantiate: asset_path (instantiate prefab into active scene).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `add_component` | any |  |  |
| `asset_path` | any |  |  |
| `base_path` | any |  |  |
| `component` | any |  | Component type name on the target object |
| `format` | any |  |  |
| `mode` | any |  | Execution mode — see tool docstring for allowed values |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `recursive` | boolean |  |  (default: `False`) |
| `remove_component` | any |  |  |
| `scope` | any |  |  |
| `value` | any |  | New value to set |
| `variant_path` | any |  |  |

<details>
<summary>11 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'asset_path' has no description.
- **info**: Parameter 'base_path' has no description.
- **info**: Parameter 'variant_path' has no description.
- **info**: Parameter 'add_component' has no description.
- **info**: Parameter 'remove_component' has no description.
- **info**: Parameter 'recursive' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "title": "Component",
      "description": "Component type name on the target object"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
      "title": "Mode",
      "description": "Execution mode \u2014 see tool docstring for allowed values"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `profile`

🟡 69/100 · Risk: 🟢 low

Profile CPU/GPU/memory over time. action: start|stop|status|analyze|compare|list_sessions mode: burst (auto-stop after duration) | manual (explicit stop) | triggered (on spike) focus: narrow analyze output to gc|rendering|physics|cpu

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `compare_with` | string |  |  (default: ``) |
| `duration` | number |  |  (default: `5.0`) |
| `focus` | string |  |  (default: ``) |
| `mode` | string |  | Execution mode — see tool docstring for allowed values (default: `burst`) |
| `session` | string |  |  (default: ``) |
| `threshold_ms` | number |  |  (default: `33.3`) |

<details>
<summary>15 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'duration' has no description.
- **warning**: Numeric parameter 'duration' has no bounds.
- **info**: Parameter 'session' has no description.
- **info**: Free-form string parameter 'session' has no maxLength.
- **info**: Parameter 'compare_with' has no description.
- **info**: Free-form string parameter 'compare_with' has no maxLength.
- **info**: Parameter 'focus' has no description.
- **info**: Free-form string parameter 'focus' has no maxLength.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "type": "string",
      "description": "Execution mode \u2014 see tool docstring for allowed values"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `project_settings`

🟢 86/100 · Risk: 🔴 high

Project settings. Modifies project settings when action=set. No confirmation required. action: get|set. target: tags|layers|sorting_layers|quality|physics|time|player|graphics|audio|input. tags set: prop=remove value=<tag> to remove; else adds. quality set prop=currentLevel: calls SetQualityLevel(). player set prop=ScriptingBackend: needs build_target (Standalone|iOS|Android|etc) + value (Mono2x|IL2CPP).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `build_target` | any |  |  |
| `index` | any |  | Zero-based index |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `target` | string | ✓ |  |
| `value` | any |  | New value to set |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
      "title": "Index",
      "description": "Zero-based index"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `query_state`

🟢 93/100 · Risk: 🟡 medium

[Play Mode] Snapshot multiple game values in one call. queries: comma-separated 'path|component|field_or_method' triplets. Example: query_state('/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX')

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `queries` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

- **info**: Parameter 'queries' has no description.
- **info**: Free-form string parameter 'queries' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `recompile`

🟢 85/100 · Risk: 🟡 medium

Trigger Unity to reimport C# scripts. Returns immediately; use await_compile to block until done.

<details>
<summary>3 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: outputSchema is missing.

</details>

---

### `reconnect_unity`

🟢 83/100 · Risk: 🟢 low

Reconnect to Unity. Port 0 or omitted = auto-discover from port files.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `port` | integer |  |  (default: `0`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `references`

🟡 78/100 · Risk: 🟡 medium

References. action: get|find_to|remap. get: outgoing refs. find_to: reverse search. remap: remap refs.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `children` | boolean |  |  (default: `False`) |
| `depth` | integer |  | Maximum hierarchy depth to traverse (default: `1`) |
| `mappings` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `source` | any |  |  |
| `target` | any |  |  |

<details>
<summary>10 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'children' has no description.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "children": {
      "default": false,
      "title": "Children",
      "type": "boolean"
    },
    "depth": {
      "default": 1,
      "title": "Depth",
      "type": "integer",
      "description": "Maximum hierarchy depth to traverse"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `region_clear`

🟢 82/100 · Risk: 🔴 high

Delete (or preview) all objects whose XZ pivot is inside the polygon region. Deletes scene objects when dry_run=False. No confirmation required.  vertices: CSV polygon 'x1,z1;x2,z2;...' (>=3 pairs). dry_run: True = list objects that WOULD be deleted (safe default). False = delete them. filter: optional name-pattern substring; only matching objects are affected. cap: max objects processed (default 50, hard max 200).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cap` | integer |  |  (default: `50`) |
| `dry_run` | boolean |  | Preview changes without applying them (default: `True`) |
| `filter` | any |  | Substring filter to narrow results |
| `vertices` | string | ✓ |  |

<details>
<summary>6 quality issues</summary>

- **info**: Parameter 'vertices' has no description.
- **info**: Free-form string parameter 'vertices' has no maxLength.
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
      "type": "boolean",
      "description": "Preview changes without applying them"
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
      "title": "Filter",
      "description": "Substring filter to narrow results"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `release_smoke`

🟢 95/100 · Risk: 🟢 low

Run release readiness checks: status, aliases, compile. Returns PASS/FAIL summary.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `rename_object`

🟢 87/100 · Risk: 🟡 medium

Rename a GameObject. Returns new scene path after rename. path: current scene path, &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy). name: new name (non-empty). Note: all subsequent MCP calls must use the new path.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ | Name of the GameObject |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>5 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "name": {
      "title": "Name",
      "type": "string",
      "description": "Name of the GameObject"
    }
  },
  "required": [
    "path",
    "name"
  ],
  "title": "rename_objectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `render_analyze`

🟢 90/100 · Risk: 🟡 medium

Rendering analysis. action: stats|materials|shaders|lights|batching|overdraw|audit|compare         |frame_debug|shadow_audit|probe_audit|light_optimize stats: draw calls, batches, tris, verts, set-pass from UnityStats. batching: SRP Batcher / static / dynamic / GPU instancing analysis. audit: full rendering health check (all sections, brief). compare: diff against last baseline snapshot. frame_debug: per-draw-call data via FrameDebugger reflection (pauses rendering briefly). detail: brief (default) | full.  path: optional subtree root.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `baseline_id` | any |  |  |
| `detail` | string |  |  (default: `brief`) |
| `max_events` | any |  |  |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `resolve_scene_refs`

🟢 93/100 · Risk: 🟡 medium

Read-only scene reference resolver. refs: comma-separated list of $alias, /path, or t:Type tokens. fields: optional comma-separated field names to check existence on matched component. Returns one tab-aligned line per ref: OK|MISS|AMB + path + details.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `fields` | any |  | Comma-separated field names to project (reduces tokens) |
| `refs` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

- **info**: Parameter 'refs' has no description.
- **info**: Free-form string parameter 'refs' has no maxLength.
- **warning**: outputSchema is missing.

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
      "title": "Fields",
      "description": "Comma-separated field names to project (reduces tokens)"
    }
  },
  "required": [
    "refs"
  ],
  "title": "resolve_scene_refsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `resolve_test_request`

🟢 93/100 · Risk: 🟢 low

Resolve a possibly lost start ACK without dispatching another test run.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `request_id` | string | ✓ |  |

<details>
<summary>3 quality issues</summary>

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `resolve_tool_schema`

🟢 92/100 · Risk: 🟢 low

Return full parameter schemas for deferred tools. tools=comma-separated names.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tools` | string | ✓ |  |

<details>
<summary>4 quality issues</summary>

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `run_playtest`

🟡 75/100 · Risk: 🔴 high

[Play Mode] Execute a playtest DSL script. Returns structured report (for NUnit tests, use `run_tests`). Commands: MOVE TO x,y,z | WAIT n | WAIT_UNTIL query op value | ASSERT query op value | ASSERT_CONSOLE_CLEAN [IGNORE "pat"] | SNAPSHOT queries | INVOKE path comp method args | SET path comp field value | LOG msg | TIMESCALE n | ASSERT_CONSERVED SUM a+b OVER t | ASSERT_CTA VISIBLE|CLICKABLE | VAL name query | TELEPORT path x,y,z | ASSERT_BATCH...END | ASSERT_NEAR pathA pathB dist | INVARIANT query op value | SIMULATE name [DURATION n] [TIMESCALE n] | MONITOR name | TRACE_FLOW FROM a TO b FIELD f | CAPTURE label query | ASSERT_CAPTURED label INCREASED|DECREASED. defs: inline VAL definitions prepended to script. abort_on_fail=True: stop after the first failed step or automatic console failure; skip all remaining steps including teardown.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `abort_on_fail` | boolean |  | Stop after the first failed step or automatic console failure; remaining steps, including teardown, are skipped (default: `False`) |
| `after_hook` | any |  | DSL commands to run after Play Mode exits |
| `before_hook` | any |  | DSL commands to run before entering Play Mode |
| `defs` | any |  | Inline VAL definitions prepended to script (alias block) |
| `fresh` | boolean |  | Stop and restart Play Mode before running the script (default: `False`) |
| `path` | any |  | Path to .playtest DSL file on disk (mutually exclusive with script) |
| `script` | any |  | Inline DSL script (mutually exclusive with path) |
| `snapshot_on_failure` | boolean |  | Auto-capture screenshot on first ASSERT failure (default: `False`) |
| `timeout` | number |  | Max seconds to wait for the playtest to finish (default 120) (default: `120.0`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'timeout' has no bounds.
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
      "title": "Script",
      "description": "Inline DSL script (mutually exclusive with path)"
    },
    "timeout": {
      "default": 120.0,
      "title": "Timeout",
      "type": "number",
      "description": "Max seconds to wait for the playtest to finish (default 120)"
    },
    "abort_on_fail": {
      "default": false,
      "title": "Abort On Fail",
      "type": "boolean",
      "description": "Stop after the first failed step or automatic console failure; remaining steps, including teardown, are skipped"
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
      "title": "Defs",
      "description": "Inline VAL definitions prepended to script (alias block)"
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
      "title": "Path",
      "description": "Path to .playtest DSL file on disk (mutually exclusive with script)"
    },
    "snapshot_on_failure": {
      "default": false,
      "title": "Snapshot On Failure",
      "type": "boolean",
      "description": "Auto-capture screenshot on first ASSERT failure"
    },
    "fresh": {
      "default": false,
      "title": "Fresh",
      "type": "boolean",
      "description": "Stop and restart Play Mode before running the script"
    },
    "before_hook": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Before Hook",
      "description": "DSL commands to run before entering Play Mode"
    },
    "after_hook": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "After Hook",
      "description": "DSL commands to run after Play Mode exits"
    }
  },
  "title": "run_playtestArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `run_playtest_suite`

🟡 67/100 · Risk: 🔴 high

Run multiple .playtest files sequentially and return a compact matrix. Side effects: auto_play/restart_between may enter or restart Play Mode; stop_after exits Play Mode. No confirmation is requested. pattern: glob pattern (e.g. 'Playtests/*.playtest'), comma-separated list,          or newline-separated list of project-relative paths. suite_path: absolute path to a .suite file (lines = project-relative .playtest paths, # = comment). Exactly one of pattern or suite_path must be provided. stop_on_fail=True: abort suite after first failure. stop_after=True: exit Play Mode when suite completes. auto_play=True: enter Play Mode automatically if not already playing. restart_between=True: stop+play between each file to reset runtime state; with auto_play=True, also resets an already-running editor before file one. suite_timeout: total suite wall-clock deadline in seconds (default 300s). Lifecycle commands must return successfully and reach their observed state. A failed transition stops the suite and is reported as a failed row. Empty matches return a failing SUITE: 0/0 report. Output: SUITE: X/Y passed (Zs) terminal:true play_stopped:true/false + per-file lines.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `auto_play` | boolean |  |  (default: `False`) |
| `pattern` | any |  |  |
| `restart_between` | boolean |  |  (default: `False`) |
| `stop_after` | boolean |  |  (default: `True`) |
| `stop_on_fail` | boolean |  |  (default: `False`) |
| `suite_path` | any |  |  |
| `suite_timeout` | number |  |  (default: `300.0`) |
| `timeout_per_test` | number |  |  (default: `120.0`) |

<details>
<summary>13 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'pattern' has no description.
- **info**: Parameter 'suite_path' has no description.
- **info**: Parameter 'timeout_per_test' has no description.
- **warning**: Numeric parameter 'timeout_per_test' has no bounds.
- **info**: Parameter 'stop_on_fail' has no description.
- **info**: Parameter 'stop_after' has no description.
- **info**: Parameter 'auto_play' has no description.
- **info**: Parameter 'restart_between' has no description.
- **info**: Parameter 'suite_timeout' has no description.
- **warning**: Numeric parameter 'suite_timeout' has no bounds.
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
    },
    "suite_timeout": {
      "default": 300.0,
      "title": "Suite Timeout",
      "type": "number"
    }
  },
  "title": "run_playtest_suiteArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `run_tests`

🟡 74/100 · Risk: 🟡 medium

Dispatch Unity tests and return their durable identity immediately.  A successful response is ``tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched``. If transport fails after dispatch may have happened, the result is ``START-UNKNOWN`` with the same request_id; resolve it instead of retrying with a new identity.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filter` | any |  | NUnit filter expression (e.g. 'MyNamespace.MyTest') |
| `mode` | string |  | Test runner mode: EditMode or PlayMode (default: `EditMode`) |
| `request_id` | any |  | Caller-supplied idempotency ID — reuse to retry a failed dispatch without double-running |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
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
      "type": "string",
      "description": "Test runner mode: EditMode or PlayMode"
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
      "title": "Filter",
      "description": "NUnit filter expression (e.g. 'MyNamespace.MyTest')"
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
      "title": "Request Id",
      "description": "Caller-supplied idempotency ID \u2014 reuse to retry a failed dispatch without double-running"
    }
  },
  "title": "run_testsArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `run_tests_wait`

🟡 68/100 · Risk: 🔴 high

Dispatch tests and wait for the exact run to become terminal. Dispatches test run. No confirmation required.  Transport failures and domain reloads do not erase the last snapshot. A caller timeout is observational only: it returns ``TIMEOUT`` with request, run and snapshot data and never marks the Unity run complete. on_timeout: result starts with TIMEOUT|request_id=...|run_id=... — use run_id to resume polling via get_test_run without re-dispatching.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `filter` | string |  | NUnit filter expression (e.g. 'MyNamespace.MyTest') (default: ``) |
| `mode` | string |  | Test runner mode: EditMode or PlayMode (default: `EditMode`) |
| `poll_interval` | number |  | Seconds between status polls (default 5) (default: `5.0`) |
| `request_id` | any |  | Caller-supplied idempotency ID |
| `timeout` | number |  | Max seconds to wait for completion (default 900) (default: `900.0`) |

<details>
<summary>8 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **warning**: String parameter 'mode' appears categorical but has no enum.
- **info**: Free-form string parameter 'mode' has no maxLength.
- **info**: Free-form string parameter 'filter' has no maxLength.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **warning**: Numeric parameter 'poll_interval' has no bounds.
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
      "type": "string",
      "description": "Test runner mode: EditMode or PlayMode"
    },
    "filter": {
      "default": "",
      "title": "Filter",
      "type": "string",
      "description": "NUnit filter expression (e.g. 'MyNamespace.MyTest')"
    },
    "timeout": {
      "default": 900.0,
      "title": "Timeout",
      "type": "number",
      "description": "Max seconds to wait for completion (default 900)"
    },
    "poll_interval": {
      "default": 5.0,
      "title": "Poll Interval",
      "type": "number",
      "description": "Seconds between status polls (default 5)"
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
      "title": "Request Id",
      "description": "Caller-supplied idempotency ID"
    }
  },
  "title": "run_tests_waitArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `runtime_snapshot`

🟢 89/100 · Risk: 🟢 low

Snapshot all runtime objects of a given component type. Returns per-object field dump. type: component type name (e.g. 'Rigidbody', 'EnemyController'). name: optional name substring filter. component: component type to serialize (defaults to type). compress: strip default-value fields to reduce response size.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | any |  | Component type name on the target object |
| `compress` | boolean |  | Strip default values before TCP transfer to reduce response size (default: `False`) |
| `name` | any |  | Name of the GameObject |
| `type` | string | ✓ | Component type name (e.g. 'Rigidbody', 'BoxCollider') |

<details>
<summary>3 quality issues</summary>

- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string",
      "description": "Component type name (e.g. 'Rigidbody', 'BoxCollider')"
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
      "title": "Name",
      "description": "Name of the GameObject"
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
      "title": "Component",
      "description": "Component type name on the target object"
    },
    "compress": {
      "default": false,
      "title": "Compress",
      "type": "boolean",
      "description": "Strip default values before TCP transfer to reduce response size"
    }
  },
  "required": [
    "type"
  ],
  "title": "runtime_snapshotArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `save_session`

🟢 94/100 · Risk: 🟢 low

Save current scene state to .claude/session-context.json for cold-start recovery.

<details>
<summary>2 quality issues</summary>

- **warning**: outputSchema is missing.
- **info**: Tool appears read-only but does not declare readOnlyHint=true.

</details>

---

### `save_skill`

🟢 90/100 · Risk: 🟡 medium

Save a learned skill (C# code or batch commands) for reuse across sessions. name: skill identifier. description: what it does. code: C# or batch commands.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ | C# code or batch commands to save as a reusable skill |
| `description` | string | ✓ |  |
| `name` | string | ✓ | File-safe identifier for the learned skill |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'description' has no description.
- **info**: Free-form string parameter 'description' has no maxLength.
- **info**: Free-form string parameter 'code' has no maxLength.
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
      "type": "string",
      "description": "File-safe identifier for the learned skill"
    },
    "description": {
      "title": "Description",
      "type": "string"
    },
    "code": {
      "title": "Code",
      "type": "string",
      "description": "C# code or batch commands to save as a reusable skill",
      "pattern": "^[\\s\\S]+$"
    }
  },
  "required": [
    "name",
    "description",
    "code"
  ],
  "title": "save_skillArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `save_template`

🟢 92/100 · Risk: 🟡 medium

Save C# code as a reusable scene template in .claude/templates/.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `code` | string | ✓ | C# code or batch commands to save as a scene template |
| `name` | string | ✓ | File-safe identifier for the reusable scene template |

<details>
<summary>4 quality issues</summary>

- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Free-form string parameter 'code' has no maxLength.
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
      "type": "string",
      "description": "File-safe identifier for the reusable scene template"
    },
    "code": {
      "title": "Code",
      "type": "string",
      "description": "C# code or batch commands to save as a scene template",
      "pattern": "^[\\s\\S]+$"
    }
  },
  "required": [
    "name",
    "code"
  ],
  "title": "save_templateArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `scan_scene`

🟢 95/100 · Risk: 🟢 low

Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `scene`

🟢 88/100 · Risk: 🟡 medium

Scene management. action: new|open|save|discard|open_additive|close|set_active|list|save_copy. path: required for open/open_additive/close/set_active/save_copy. For save, omit it to save to the current path; an untitled scene requires a path. scene: save/discard/save_copy target when multiple scenes loaded (identifies by name). save_copy: writes current dirty state to path as backup; active scene reference unchanged. include_unsaved: always True — save_copy always captures current in-memory state.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation: new\|open\|save\|discard\|open_additive\|close\|set_active\|list |
| `include_unsaved` | boolean |  |  (default: `True`) |
| `path` | any |  | Asset path — required for open/save/open_additive/close/set_active |
| `scene` | any |  | Scene name for save/discard when multiple scenes are loaded |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'include_unsaved' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation: new|open|save|discard|open_additive|close|set_active|list"
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
      "title": "Path",
      "description": "Asset path \u2014 required for open/save/open_additive/close/set_active"
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
      "title": "Scene",
      "description": "Scene name for save/discard when multiple scenes are loaded"
    },
    "include_unsaved": {
      "default": true,
      "title": "Include Unsaved",
      "type": "boolean"
    }
  },
  "required": [
    "action"
  ],
  "title": "sceneArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `scene_change_plan`

🟢 81/100 · Risk: 🟡 medium

Pre-flight + plan for safe scene edit. 1. Check Play Mode — reject if playing (mutations blocked) 2. Check compile clean 3. Check console for errors 4. Resolve targets via resolve_scene_refs 5. Take checkpoint 6. Return plan_id + baseline status

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | False (default) = create plan_id. True = probe pre-flights only, no plan created. (default: `False`) |
| `goal` | string | ✓ |  |
| `targets` | string |  |  (default: ``) |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'goal' has no description.
- **info**: Free-form string parameter 'goal' has no maxLength.
- **info**: Parameter 'targets' has no description.
- **info**: Free-form string parameter 'targets' has no maxLength.
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
      "default": false,
      "title": "Dry Run",
      "type": "boolean",
      "description": "False (default) = create plan_id. True = probe pre-flights only, no plan created."
    }
  },
  "required": [
    "goal"
  ],
  "title": "scene_change_planArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `scene_diff`

🟢 95/100 · Risk: 🟢 low

Compare scene with last snapshot. First call saves snapshot. Returns diff: added/removed lines.

<details>
<summary>1 quality issues</summary>

- **warning**: outputSchema is missing.

</details>

---

### `scene_environment`

🟢 84/100 · Risk: 🟡 medium

Read/write scene environment: ambient light, fog, skybox, reflections. action: get|set. set requires prop and value. Props: ambientMode, ambientLight, ambientIntensity, ambientSkyColor, ambientEquatorColor, ambientGroundColor, fog, fogColor, fogMode, fogDensity, fogStartDistance, fogEndDistance, reflectionIntensity, reflectionBounces, subtractiveShadowColor, defaultReflectionResolution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string |  | Operation to perform — see tool docstring for allowed values (default: `get`) |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `value` | any |  | New value to set |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Object schema has properties but no required list.
- **info**: Free-form string parameter 'action' has no maxLength.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
    }
  },
  "title": "scene_environmentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `scene_health`

🟢 88/100 · Risk: 🟢 low

Scene hierarchy/health audit. focus: all | hierarchy | naming | duplicates | origins | missing | empty | disabled Returns severity-tagged findings: CRITICAL/WARNING/INFO/OK per check.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `focus` | string |  |  (default: `all`) |

<details>
<summary>4 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'focus' has no description.
- **info**: Free-form string parameter 'focus' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `screenshot`

🟡 65/100 · Risk: 🟡 medium

Capture screenshot (file path); describe= -> Haiku text (15-100x fewer tokens), raw=True forces path. camera: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game. angle (single_view): front|left|top|iso|ex,ey,ez. zoom: higher=closer. angles: per-view Euler "ex,ey,ez|..." (_=skip). supersample 1-4. offset/fixed_size: framing. highlight: paths[:#RRGGBB] for bbox. show_colliders: wireframes. annotation_id: frame + highlight annotation by id (auto sets camera=annotation_frame). warn:ScreenSpaceOverlay canvas not captured in Edit Mode (bypasses camera pipeline). Workarounds: switch to ScreenSpace-Camera, use Play Mode, or camera=scene_view.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `angle` | any |  | Euler angle for single_view: front\|left\|top\|iso\|ex,ey,ez |
| `angles` | any |  | Per-view Euler angles: 'ex,ey,ez\|...' (underscore = skip this view) |
| `annotation_id` | any |  | Frame + highlight annotation by ID (auto-sets camera=annotation_frame) |
| `camera` | any |  | View type: scene_view\|scene_view_frame\|multi_view\|single_view\|overview\|overview_game |
| `describe` | any |  | Haiku prompt for AI text description instead of returning file path (15-100x fewer tokens) |
| `fixed_size` | any |  | Fixed framing size override |
| `height` | integer |  | Image height in pixels (default 480) (default: `480`) |
| `highlight` | any |  | Comma-separated paths to highlight with bounding box (e.g. /Player:#FF0000) |
| `offset` | any |  | Framing offset vector |
| `output_path` | any |  | Unambiguous project-contained .png destination for every capture mode |
| `path` | any |  | For single_view/multi_view: target GameObject scene path; for standard captures: legacy project-contained .png destin... |
| `raw` | boolean |  | Force returning file path even when describe= is set (default: `False`) |
| `show_colliders` | any |  | Overlay collider wireframes on the screenshot |
| `supersample` | any |  | Anti-alias quality 1-4 (higher = sharper, slower) |
| `width` | integer |  | Image width in pixels (default 640) (default: `640`) |
| `zoom` | any |  | Zoom factor — higher = closer |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **warning**: Numeric parameter 'width' has no bounds.
- **warning**: Numeric parameter 'height' has no bounds.
- **warning**: outputSchema is missing.
- **warning**: Tool card is about 3457 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "width": {
      "default": 640,
      "title": "Width",
      "type": "integer",
      "description": "Image width in pixels (default 640)"
    },
    "height": {
      "default": 480,
      "title": "Height",
      "type": "integer",
      "description": "Image height in pixels (default 480)"
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
      "title": "Camera",
      "description": "View type: scene_view|scene_view_frame|multi_view|single_view|overview|overview_game"
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
      "title": "Path",
      "description": "For single_view/multi_view: target GameObject scene path; for standard captures: legacy project-contained .png destination (prefer output_path)"
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
      "title": "Output Path",
      "description": "Unambiguous project-contained .png destination for every capture mode"
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
      "title": "Describe",
      "description": "Haiku prompt for AI text description instead of returning file path (15-100x fewer tokens)"
    },
    "raw": {
      "default": false,
      "title": "Raw",
      "type": "boolean",
      "description": "Force returning file path even when describe= is set"
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
      "title": "Zoom",
      "description": "Zoom factor \u2014 higher = closer"
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
      "title": "Angles",
      "description": "Per-view Euler angles: 'ex,ey,ez|...' (underscore = skip this view)"
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
      "title": "Supersample",
      "description": "Anti-alias quality 1-4 (higher = sharper, slower)"
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
      "title": "Offset",
      "description": "Framing offset vector"
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
      "title": "Fixed Size",
      "description": "Fixed framing size override"
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
      "title": "Highlight",
      "description": "Comma-separated paths to highlight with bounding box (e.g. /Player:#FF0000)"
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
      "title": "Show Colliders",
      "description": "Overlay collider wireframes on the screenshot"
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
      "title": "Angle",
      "description": "Euler angle for single_view: front|left|top|iso|ex,ey,ez"
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
      "title": "Annotation Id",
      "description": "Frame + highlight annotation by ID (auto-sets camera=annotation_frame)"
    }
  },
  "title": "screenshotArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `screenshot_baseline`

🟡 75/100 · Risk: 🟡 medium

Save screenshot as baseline for visual regression. name: file-safe identifier.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `camera` | any |  |  |
| `height` | integer |  |  (default: `480`) |
| `name` | string |  | File-safe baseline identifier (not a GameObject name; no '/', '\', or '..') (default: `default`) |
| `width` | integer |  |  (default: `640`) |

<details>
<summary>9 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
      "type": "string",
      "description": "File-safe baseline identifier (not a GameObject name; no '/', '\\', or '..')"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `screenshot_compare`

🟡 68/100 · Risk: 🟡 medium

Compare current screenshot with saved baseline. mode: auto (pixel->escalate), pixel (local), structural (general),       targeted (needs question=), ui_layout|animation|color|position|regression. Model-assisted modes require configured sampling. Cached by image hashes.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `camera` | any |  |  |
| `height` | integer |  |  (default: `480`) |
| `mode` | string |  | Execution mode — see tool docstring for allowed values (default: `auto`) |
| `name` | string |  | File-safe saved-baseline identifier (not a GameObject name; no '/', '\', or '..') (default: `default`) |
| `question` | any |  |  |
| `width` | integer |  |  (default: `640`) |

<details>
<summary>12 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Parameter 'width' has no description.
- **warning**: Numeric parameter 'width' has no bounds.
- **info**: Parameter 'height' has no description.
- **warning**: Numeric parameter 'height' has no bounds.
- **info**: Parameter 'camera' has no description.
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
      "type": "string",
      "description": "File-safe saved-baseline identifier (not a GameObject name; no '/', '\\', or '..')"
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
      "type": "string",
      "description": "Execution mode \u2014 see tool docstring for allowed values"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `scriptable_object`

🟢 84/100 · Risk: 🟡 medium

ScriptableObject. action: create|get|set|list_types|find. create: type+path[+fields]. get/set: path. set/create fields: \n-separated prop=value pairs. get fields: comma-sep filter. find: type. list_types: filter.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `fields` | any |  | Comma-separated field names to project (reduces tokens) |
| `filter` | any |  | Substring filter to narrow results |
| `path` | any |  | Project asset path to the ScriptableObject .asset file |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `type` | any |  | Component type name (e.g. 'Rigidbody', 'BoxCollider') |
| `value` | any |  | New value to set |

<details>
<summary>4 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'action' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Project asset path to the ScriptableObject .asset file"
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
      "title": "Type",
      "description": "Component type name (e.g. 'Rigidbody', 'BoxCollider')"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
      "title": "Fields",
      "description": "Comma-separated field names to project (reduces tokens)"
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
      "title": "Filter",
      "description": "Substring filter to narrow results"
    }
  },
  "required": [
    "action"
  ],
  "title": "scriptable_objectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `search_scene`

🟢 89/100 · Risk: 🟡 medium

Search scene objects. Syntax: name text, t:Component, tag=Tag, layer=N, active=bool. Combine with spaces. root: scope search to subtree (path or None for whole scene). limit: max results (default 50; 0=unlimited). scene: filter to a single scene by name (multi-scene only).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `limit` | integer |  | Max results (default 50; 0 = unlimited) (default: `50`) |
| `query` | string | ✓ | Syntax: 'name text', 't:Component', 'tag=Tag', 'layer=N', 'active=bool' — combine with spaces |
| `root` | any |  | Scene path to scope the search (omit = whole scene) |
| `scene` | any |  | Filter to a single scene by name (multi-scene only) |

<details>
<summary>3 quality issues</summary>

- **info**: Free-form string parameter 'query' has no maxLength.
- **warning**: Numeric parameter 'limit' has no bounds.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "query": {
      "title": "Query",
      "type": "string",
      "description": "Syntax: 'name text', 't:Component', 'tag=Tag', 'layer=N', 'active=bool' \u2014 combine with spaces"
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
      "title": "Root",
      "description": "Scene path to scope the search (omit = whole scene)"
    },
    "limit": {
      "default": 50,
      "title": "Limit",
      "type": "integer",
      "description": "Max results (default 50; 0 = unlimited)"
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
      "title": "Scene",
      "description": "Filter to a single scene by name (multi-scene only)"
    }
  },
  "required": [
    "query"
  ],
  "title": "search_sceneArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `serialized_field_rename_audit`

🟢 83/100 · Risk: 🟢 low

Audit [SerializeField] rename safety. type: fully-qualified or simple component type name (e.g. 'MyNamespace.PlayerStats'). old_field: field name as it exists in serialized assets. new_field: renamed field name in current C# source. include: comma-separated scan targets (prefabs,scenes,scriptable_objects). Returns: has_formerly_serialized_as, stale_assets, safe_to_remove_attribute, recommended_actions.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `include` | string |  |  (default: `prefabs,scenes,scriptable_objects`) |
| `new_field` | string | ✓ |  |
| `old_field` | string | ✓ |  |
| `type` | string | ✓ | Component type name (e.g. 'Rigidbody', 'BoxCollider') |

<details>
<summary>9 quality issues</summary>

- **warning**: String parameter 'type' appears categorical but has no enum.
- **info**: Free-form string parameter 'type' has no maxLength.
- **info**: Parameter 'old_field' has no description.
- **info**: Free-form string parameter 'old_field' has no maxLength.
- **info**: Parameter 'new_field' has no description.
- **info**: Free-form string parameter 'new_field' has no maxLength.
- **info**: Parameter 'include' has no description.
- **info**: Free-form string parameter 'include' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "type": {
      "title": "Type",
      "type": "string",
      "description": "Component type name (e.g. 'Rigidbody', 'BoxCollider')"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_active`

🟡 79/100 · Risk: 🟡 medium

Set GameObject active/inactive.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `active` | boolean | ✓ | True to activate, False to deactivate |
| `path` | string | ✓ | Scene path to the GameObject |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to the GameObject"
    },
    "active": {
      "title": "Active",
      "type": "boolean",
      "description": "True to activate, False to deactivate"
    }
  },
  "required": [
    "path",
    "active"
  ],
  "title": "set_activeArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_llm_config`

🟢 83/100 · Risk: 🟡 medium

Override LLM profiles for sampling features. Format: feature:model,turns,timeout,max_tokens (one per line). Features: visual_verify, screenshot_describe, visual_diff, do_intent, ui_intent, vfx_intent, animator_intent, summarize, distiller.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `config` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'config' has no description.
- **info**: Free-form string parameter 'config' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_material`

🟡 76/100 · Risk: 🟡 medium

Set scene object material color (for full asset management use `material`). color: hex (#FF0000). shader: URP/Standard auto.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `color` | string | ✓ |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `shader` | any |  |  |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_parent`

🟡 79/100 · Risk: 🟡 medium

Reparent existing GameObject. parent=null → move to scene root. world_position_stays=True (default): preserves world transform. False: stays local to new parent.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `parent` | any |  | New parent scene path (omit or null = move to scene root) |
| `path` | string | ✓ | Scene path of the GameObject to reparent |
| `world_position_stays` | boolean |  | True (default) = preserve world transform; False = keep local transform relative to new parent (default: `True`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path of the GameObject to reparent"
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
      "title": "Parent",
      "description": "New parent scene path (omit or null = move to scene root)"
    },
    "world_position_stays": {
      "default": true,
      "title": "World Position Stays",
      "type": "boolean",
      "description": "True (default) = preserve world transform; False = keep local transform relative to new parent"
    }
  },
  "required": [
    "path"
  ],
  "title": "set_parentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_properties`

🟡 77/100 · Risk: 🟡 medium

Set multiple properties on ONE object. For multiple objects, use configure_objects instead. Format: component.prop=value per line or semicolon-separated. Example: Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `props` | string | ✓ |  |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_property`

🟡 76/100 · Risk: 🟡 medium

Set component property (Edit Mode, SerializedObject — for Play Mode use `invoke_method` or `execute_code`). find_type: component type — bulk-sets prop on all matching objects without specifying paths. For GO rename use rename_object(). ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy), or 'null'. dry_run=True shows what would change without applying. ref_component_type: when value is a plain scene path and the field expects a specific Component type (e.g. 'BoxCollider'), appends '::TypeName' to the value so C# resolves the correct component. Ignored when value already contains '::'. effects: mutates scene via SerializedObject, creates an undo entry. Verify with get_component after.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string |  | Component type (empty string = Transform) (default: ``) |
| `dry_run` | boolean |  | Show what would change without applying (safe preview) (default: `False`) |
| `find_type` | any |  | Component type — bulk-sets prop on ALL scene objects with this component (no path needed) |
| `path` | any |  | Scene path to the GameObject (e.g. /Player/Body) |
| `prop` | string |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') (default: ``) |
| `ref_component_type` | any |  |  |
| `value` | string |  | New value. ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), &ref... |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Free-form string parameter 'prop' has no maxLength.
- **info**: Free-form string parameter 'value' has no maxLength.
- **info**: Parameter 'ref_component_type' has no description.
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
      "title": "Path",
      "description": "Scene path to the GameObject (e.g. /Player/Body)"
    },
    "component": {
      "default": "",
      "title": "Component",
      "type": "string",
      "description": "Component type (empty string = Transform)"
    },
    "prop": {
      "default": "",
      "title": "Prop",
      "type": "string",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
    },
    "value": {
      "default": null,
      "title": "value",
      "type": "string",
      "description": "New value. ObjectReference: scene path (/Player), asset path (Assets/X.mat), sub-asset (Assets/X.fbx::ClipName), &ref (e.g. &1) or $hexId (legacy), #instanceID (legacy), or 'null'"
    },
    "dry_run": {
      "default": false,
      "title": "Dry Run",
      "type": "boolean",
      "description": "Show what would change without applying (safe preview)"
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
      "title": "Find Type",
      "description": "Component type \u2014 bulk-sets prop on ALL scene objects with this component (no path needed)"
    },
    "ref_component_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Ref Component Type"
    }
  },
  "title": "set_propertyArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_property_delta`

🟡 75/100 · Risk: 🟡 medium

Apply delta to numeric property. delta: +5, -0.5, (+1,2,0). Returns: old → new.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string | ✓ | Component type name on the target object |
| `delta` | string | ✓ |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `prop` | string | ✓ | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |

<details>
<summary>9 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Free-form string parameter 'prop' has no maxLength.
- **info**: Parameter 'delta' has no description.
- **info**: Free-form string parameter 'delta' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
    },
    "prop": {
      "title": "Prop",
      "type": "string",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_rect`

🟡 72/100 · Risk: 🟡 medium

Set RectTransform. anchor: stretch|center|top-left|top-right|bottom-left|bottom-right|etc. pos/size: (x,y). pos3: (x,y,z) sets anchoredPosition3D — use for WorldSpace canvases (wins over pos if both given).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `anchor` | any |  |  |
| `offset_max` | any |  |  |
| `offset_min` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `pivot` | any |  |  |
| `pos` | any |  |  |
| `pos3` | any |  |  |
| `size` | any |  |  |

<details>
<summary>12 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'anchor' has no description.
- **info**: Parameter 'pos' has no description.
- **info**: Parameter 'size' has no description.
- **info**: Parameter 'pivot' has no description.
- **info**: Parameter 'offset_min' has no description.
- **info**: Parameter 'offset_max' has no description.
- **info**: Parameter 'pos3' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
    },
    "pos3": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Pos3"
    }
  },
  "required": [
    "path"
  ],
  "title": "set_rectArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `set_sibling_index`

🟡 74/100 · Risk: 🟡 medium

Set sibling index of a GameObject within its parent. index=0 moves to first child.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `index` | integer | ✓ | Zero-based index |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "index": {
      "title": "Index",
      "type": "integer",
      "description": "Zero-based index"
    }
  },
  "required": [
    "path",
    "index"
  ],
  "title": "set_sibling_indexArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `setup_objects`

🟢 83/100 · Risk: 🟡 medium

Create+configure multiple objects in one call. One per line: name [primitive=X] [parent=Y] [pos=(x,y,z)] [components=A,B] Example: NPC1 primitive=Capsule pos=(1,0,0) components=Health

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `specs` | string | ✓ |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `shader`

🔴 59/100 · Risk: 🔴 high

Read or write shader assets (.shader / .shadergraph). Creates or modifies shader assets. No confirmation required. Use when you need to inspect shader properties, create a new shader from a preset or raw HLSL, change a shader property/keyword, or build/edit a Shader Graph node network. action: get (inspect path — shader name, properties, keywords) | create (new shader; preset=unlit|lit|transparent or code=HLSL string) | set (change prop+value or keyword+enabled on existing shader) | graph_get (read Shader Graph nodes/edges) | graph_create (new .shadergraph) | graph_node (add/remove/configure a node; node_type, node_id, node_action) | graph_edge (connect/disconnect slots; output_node/output_slot, input_node/input_slot, edge_action) | graph_get_layout (read node positions as compact text) | graph_set_layout (apply positions from layout text; layout=[id] x,y WxH lines) | graph_auto_layout (auto-arrange nodes by data-flow; h_gap, v_gap optional). For material shader assignment use `material` tool instead.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `code` | any |  |  |
| `default_value` | any |  |  |
| `edge_action` | any |  |  |
| `enabled` | any |  |  |
| `h_gap` | any |  |  |
| `input_node` | any |  |  |
| `input_slot` | any |  |  |
| `keyword` | any |  |  |
| `layout` | any |  |  |
| `name` | any |  | Action-specific Shader Graph property/node name (not a GameObject name) |
| `new_name` | any |  |  |
| `node_action` | any |  |  |
| `node_id` | any |  |  |
| `node_type` | any |  |  |
| `output_node` | any |  |  |
| `output_slot` | any |  |  |
| `path` | string | ✓ | Project asset path to a .shader or .shadergraph file |
| `preset` | any |  |  |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `reference_name` | any |  |  |
| `shader_name` | any |  | Shader declaration name used when creating shader source |
| `target` | any |  |  |
| `type` | any |  | Action-specific Shader Graph property value type (not a component type) |
| `v_gap` | any |  |  |
| `value` | any |  | New value to set |

<details>
<summary>25 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'preset' has no description.
- **info**: Parameter 'code' has no description.
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
- **info**: Parameter 'default_value' has no description.
- **info**: Parameter 'reference_name' has no description.
- **info**: Parameter 'new_name' has no description.
- **info**: Parameter 'layout' has no description.
- **info**: Parameter 'h_gap' has no description.
- **info**: Parameter 'v_gap' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 4014 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Project asset path to a .shader or .shadergraph file"
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
      "title": "Shader Name",
      "description": "Shader declaration name used when creating shader source"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
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
      "title": "Value",
      "description": "New value to set"
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
      "title": "Name",
      "description": "Action-specific Shader Graph property/node name (not a GameObject name)"
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
      "title": "Type",
      "description": "Action-specific Shader Graph property value type (not a component type)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `smart_build`

🟢 92/100 · Risk: 🟢 low

Build scene objects from natural language description using MCP sampling + execute_code.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `description` | string | ✓ |  |

<details>
<summary>4 quality issues</summary>

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `snapshot`

🟢 85/100 · Risk: 🟡 medium

Capture or compare object state.  path: Object path ("/Enemy_01") label: Snapshot label ("before", "after") compare: Label to diff against (empty = capture only)  Returns:     Capture: "snapshot 'label' saved (N fields)"     Compare: structured diff or error if compare label missing

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `compare` | string |  |  (default: ``) |
| `label` | string |  |  (default: `default`) |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>7 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'label' has no description.
- **info**: Free-form string parameter 'label' has no maxLength.
- **info**: Parameter 'compare' has no description.
- **info**: Free-form string parameter 'compare' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `spatial_query`

🟢 85/100 · Risk: 🟡 medium

Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|raycast|spatial_map|objects_in_polygon. nearest: find closest object (optionally filtered by component name). in_front_of: position in front of object at distance. objects_in_radius: list all objects within radius. path is optional when center='x,y,z' is given. bounds_info: detailed bounds/dimensions of object. raycast: cast ray from path/pos to target, returns hits sorted by distance. spatial_map: ASCII grid map of objects in XZ plane. cell_size in meters. objects_in_polygon: objects whose XZ pivot is inside a polygon. Provide either vertices='x1,z1;x2,z2;...' (>=3 pairs) or a previously defined region_id; supplied vertices are always validated. cap=max results (default 50).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `cap` | any |  |  |
| `cell_size` | any |  |  |
| `center` | any |  |  |
| `component` | any |  | Component type name on the target object |
| `distance` | any |  |  |
| `layer_mask` | any |  |  |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `radius` | any |  |  |
| `region_id` | any |  |  |
| `target` | any |  |  |
| `vertices` | any |  |  |

<details>
<summary>11 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target' has no description.
- **info**: Parameter 'distance' has no description.
- **info**: Parameter 'radius' has no description.
- **info**: Parameter 'cell_size' has no description.
- **info**: Parameter 'layer_mask' has no description.
- **info**: Parameter 'center' has no description.
- **info**: Parameter 'vertices' has no description.
- **info**: Parameter 'region_id' has no description.
- **info**: Parameter 'cap' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "title": "Component",
      "description": "Component type name on the target object"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `start_write_session`

🟢 85/100 · Risk: 🟡 medium

Open a write session — lock assemblies + disable auto-refresh. Call before writing multiple .cs files via asset(action='write_text'). All writes batch into one domain reload. Close with end_write_session(). Auto-releases after 120s watchdog if the session is not ended explicitly.

<details>
<summary>3 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: outputSchema is missing.

</details>

---

### `sync_playtest_aliases_from_defs`

🟢 85/100 · Risk: 🟡 medium

Overwrite PlaytestConfig.asset aliases from a .defs text file. defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs). asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset). Invalidates AliasExpander cache after sync. Not allowed in Play Mode.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>7 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `sync_unity`

🟡 73/100 · Risk: 🟡 medium

Unified Unity reload: trigger Refresh (+ optional Resolve), wait for new code to live.  resolve=True: call Client.Resolve() first (use after package.json change). bump=True: atomically increment plugin patch version BEFORE sync, implies resolve=True. Returns: 'sync clean' / compile errors / timeout message.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `bump` | boolean |  |  (default: `False`) |
| `resolve` | boolean |  |  (default: `False`) |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `120.0`) |

<details>
<summary>7 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'resolve' has no description.
- **info**: Parameter 'bump' has no description.
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
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
    }
  },
  "title": "sync_unityArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `test_step`

🟡 71/100 · Risk: 🟡 medium

[Play Mode] Move character, snapshot state before/after, check console. checks_before/after: comma-separated 'path|component|field' triplets. Returns structured BEFORE/MOVE/AFTER/CONSOLE report.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `checks_after` | string |  |  (default: ``) |
| `checks_before` | string |  |  (default: ``) |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `position` | string | ✓ |  |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `15.0`) |
| `wait_after` | number |  |  (default: `0.5`) |

<details>
<summary>13 quality issues</summary>

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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
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
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
    }
  },
  "required": [
    "path",
    "position"
  ],
  "title": "test_stepArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `timeline`

🟡 64/100 · Risk: 🟡 medium

Unity Timeline (PlayableDirector / TimelineAsset). Use for multi-track cinematic sequences mixing animation, audio, activation, and custom tracks — not for per-object keyframes (use `animation` for that). action: get | create | add_track (Animation|Audio|Activation|Signal|Control|Group) | remove_track | add_clip | remove_clip | set_binding | set_timing | mute | unmute | lock | unlock | rename_track | reorder_track | duplicate_clip | add_marker | remove_marker | set_track_offset | set_duration | add_sub_track | set_clip_in | get_bindings | preview. track=track name. index=target position for reorder_track. offset=time shift for duplicate_clip. value=offset mode (auto|transform|scene) for set_track_offset.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `asset_path` | any |  |  |
| `binding` | any |  |  |
| `blend_in` | any |  |  |
| `blend_out` | any |  |  |
| `clip` | any |  |  |
| `clip_in` | any |  |  |
| `director_path` | any |  |  |
| `duration` | any |  |  |
| `index` | any |  | Zero-based index |
| `name` | any |  | Action-specific Timeline track, clip, or marker name |
| `offset` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `start` | any |  |  |
| `time` | any |  |  |
| `track` | any |  |  |
| `track_type` | any |  |  |
| `tracks` | any |  |  |
| `value` | any |  | Action-specific Timeline value; set_track_offset accepts auto\|transform\|scene |

<details>
<summary>20 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
- **info**: Parameter 'clip_in' has no description.
- **info**: Parameter 'offset' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool card is about 2874 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Name",
      "description": "Action-specific Timeline track, clip, or marker name"
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
      "title": "Index",
      "description": "Zero-based index"
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
      "title": "Value",
      "description": "Action-specific Timeline value; set_track_offset accepts auto|transform|scene"
    }
  },
  "required": [
    "path",
    "action"
  ],
  "title": "timelineArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `transfer_object`

🟢 85/100 · Risk: 🟡 medium

Move or copy a GameObject to another loaded scene. action: move|copy. target_scene: destination scene name. Omit = same scene (copy = duplicate). parent: target parent path in destination scene. world_position_stays: preserve world transform (default True).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `parent` | any |  | Scene path to the parent GameObject |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `target_scene` | any |  |  |
| `world_position_stays` | boolean |  |  (default: `True`) |

<details>
<summary>7 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'target_scene' has no description.
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
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "action": {
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Parent",
      "description": "Scene path to the parent GameObject"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `ui_intent`

🟢 82/100 · Risk: 🟡 medium

Convert NL intent to Unity UI hierarchy. Templates bypass Haiku.  template: hud|menu|dialog|grid. dry_run=True skips execution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `intent` | string | ✓ |  |
| `parent` | any |  | Scene path to the parent GameObject |
| `template` | any |  |  |

<details>
<summary>6 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'template' has no description.
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
      "title": "Parent",
      "description": "Scene path to the parent GameObject"
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
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "intent"
  ],
  "title": "ui_intentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `uitk_element`

🟢 80/100 · Risk: 🔴 high

Mutate or query a VisualElement in a UIDocument or PanelRenderer host (use inspect_uitk to find elements first, then pass ~N ref for zero-token addressing; use set_property for serialized component fields on the UIDocument GameObject; use create_ui for uGUI Canvas elements). action: query (find elements) | get (read value/text) | set_style | add_class | remove_class | get_style | enable | disable. Element addressing priority: ref (~N from inspect_uitk) → name → selector (CSS class/type). path: scene path to UIDocument or PanelRenderer GameObject (e.g. /HUD). ref: ~N refid from inspect_uitk (highest priority, stale after re-inspect or domain reload). selector: CSS selector — .class-name, TypeName, or element name. name: element name (equivalent to bare name in selector). value: value to write (for set_style/add_class). property: CSS property name for set_style/get_style. class_name: USS class name for add_class/remove_class (no leading dot). warn: set_style/add_class/remove_class/enable/disable in Play Mode — change not persisted.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | `query` \| `get` \| `set_style` \| `add_class` \| `remove_class` \| `get_style` \| `enable` \| `disable` | ✓ | Operation to perform — see tool docstring for allowed values |
| `class_name` | any |  |  |
| `name` | any |  | VisualElement name used after ref and before selector in addressing priority |
| `path` | any |  | Scene path to target GameObject (e.g. /Parent/Child) |
| `property` | any |  |  |
| `ref` | any |  |  |
| `selector` | any |  |  |
| `value` | any |  | New value to set |

<details>
<summary>8 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'ref' has no description.
- **info**: Parameter 'selector' has no description.
- **info**: Parameter 'property' has no description.
- **info**: Parameter 'class_name' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "action": {
      "enum": [
        "query",
        "get",
        "set_style",
        "add_class",
        "remove_class",
        "get_style",
        "enable",
        "disable"
      ],
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
      "title": "Path",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "ref": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Ref"
    },
    "selector": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Selector"
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
      "title": "Name",
      "description": "VisualElement name used after ref and before selector in addressing priority"
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
      "title": "Value",
      "description": "New value to set"
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
  "title": "uitk_elementArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `uitk_file`

🟡 67/100 · Risk: 🔴 high

Read or edit a UXML or USS asset file. action=read is read-only. Side effect: every other action may create, replace, import, or restore a project file; review the target path before calling it. (UI Toolkit only — use `asset` for other Unity asset types, use `inspect_uitk` to inspect the live VisualElement tree at runtime, use `attach_uitk` to wire a UIDocument to a GameObject). action=read: return the file's UTF-8 text verbatim (no normalization). action=write: full file replace; validates and triggers AssetDatabase.ImportAsset. action=create_uxml: new UXML file with minimal template; content optional. action=create_uss: new empty USS file; content optional. action=set-attr: set attribute on UXML element by name (selector=name, attr=attr, value=val). action=add-class|remove-class: manage USS class on UXML element by name. action=add-element: append child (parent=name, tag=ui:Label, attrs='k=v ...'). action=remove-element: delete UXML element and children by name. action=set-rule: set CSS property in USS rule; creates rule if selector absent. action=remove-rule: delete USS rule block by exact selector string. action=revert: restore file to state before last write (single-level, cleared on domain reload). path: Assets/ path to .uxml or .uss file. Library/ and Packages/ are rejected.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | `read` \| `write` \| `create_uxml` \| `create_uss` \| `set-attr` \| `add-class` \| `remove-class` \| `add-element` \| `remove-element` \| `set-rule` \| `remove-rule` \| `revert` |  | Operation to perform — see tool docstring for allowed values (default: `read`) |
| `attr` | any |  |  |
| `attrs` | any |  |  |
| `cls` | any |  |  |
| `content` | any |  |  |
| `parent` | any |  | Scene path to the parent GameObject |
| `path` | string | ✓ | Assets/ path to a .uxml or .uss project file; Library/ and Packages/ are rejected |
| `prop` | any |  | Property name as shown in Inspector (e.g. 'mass', 'localPosition.x') |
| `selector` | any |  |  |
| `tag` | any |  |  |
| `value` | any |  | New value to set |

<details>
<summary>13 quality issues</summary>

- **warning**: Tool description is very long and may increase context cost or hide important constraints.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'content' has no description.
- **info**: Parameter 'selector' has no description.
- **info**: Parameter 'attr' has no description.
- **info**: Parameter 'cls' has no description.
- **info**: Parameter 'tag' has no description.
- **info**: Parameter 'attrs' has no description.
- **warning**: outputSchema is missing.
- **warning**: Tool appears destructive but lacks destructiveHint=true.
- **warning**: Tool card is about 2905 characters.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Assets/ path to a .uxml or .uss project file; Library/ and Packages/ are rejected"
    },
    "action": {
      "default": "read",
      "enum": [
        "read",
        "write",
        "create_uxml",
        "create_uss",
        "set-attr",
        "add-class",
        "remove-class",
        "add-element",
        "remove-element",
        "set-rule",
        "remove-rule",
        "revert"
      ],
      "title": "Action",
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
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
    "selector": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Selector"
    },
    "attr": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Attr"
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
      "title": "Value",
      "description": "New value to set"
    },
    "cls": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Cls"
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
      "title": "Parent",
      "description": "Scene path to the parent GameObject"
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
    "attrs": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Attrs"
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
      "title": "Prop",
      "description": "Property name as shown in Inspector (e.g. 'mass', 'localPosition.x')"
    }
  },
  "required": [
    "path"
  ],
  "title": "uitk_fileArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `uitk_intent`

🟡 79/100 · Risk: 🔴 high

Generate a UXML + USS file pair from a natural-language UI description. Side effect: unless dry_run=True, writes both project assets; attach_to also adds a UIDocument or PanelRenderer scene component. Completed steps are not rolled back if a later step fails. Failure output distinguishes retained files, confirmed Unity auto-reverts, and attempted files whose cleanup is uncertain. Without template, invokes configured Claude sampling and may consume provider quota. For uGUI use ui_intent. template: hud|menu|dialog|settings|editor_window bypasses Haiku entirely. name: base filename (e.g. "InventoryPanel" → InventoryPanel.uxml + .uss). path: output folder, default "Assets/UI". attach_to: scene path to UIDocument or PanelRenderer GameObject to wire after creation. dry_run: return UXML+USS text without writing files.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `attach_to` | any |  |  |
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `intent` | string | ✓ |  |
| `name` | string | ✓ | Base asset filename without extension (for <name>.uxml and <name>.uss) |
| `path` | string |  | Project asset output folder for the generated UXML/USS pair (default Assets/UI) (default: `Assets/UI`) |
| `template` | any |  |  |

<details>
<summary>9 quality issues</summary>

- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Free-form string parameter 'name' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Parameter 'attach_to' has no description.
- **info**: Parameter 'template' has no description.
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
    "name": {
      "title": "Name",
      "type": "string",
      "description": "Base asset filename without extension (for <name>.uxml and <name>.uss)"
    },
    "path": {
      "default": "Assets/UI",
      "title": "Path",
      "type": "string",
      "description": "Project asset output folder for the generated UXML/USS pair (default Assets/UI)"
    },
    "attach_to": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Attach To"
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
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "intent",
    "name"
  ],
  "title": "uitk_intentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `undo_last`

🟢 83/100 · Risk: 🟡 medium

Undo the last N AI turns in the Unity Undo stack. Default: 1. warn: file-system operations (asset creation/deletion via asset tool) are not reversed by undo. Only scene-object and component mutations are undoable.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `turns` | integer |  |  (default: `1`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `unwire_event`

🟢 86/100 · Risk: 🔴 high

Remove persistent listener(s) from UnityEvent. Mutates scene. No confirmation required. index: remove specific entry (0-based). Omit to clear all.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `component` | string | ✓ | Component type name on the target object |
| `event` | string | ✓ |  |
| `index` | any |  | Zero-based index |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'event' has no description.
- **info**: Free-form string parameter 'event' has no maxLength.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
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
      "title": "Index",
      "description": "Zero-based index"
    }
  },
  "required": [
    "path",
    "component",
    "event"
  ],
  "title": "unwire_eventArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `use_skill`

🟢 83/100 · Risk: 🟡 medium

Execute a previously saved skill. params: comma-separated key=value for substitution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `name` | string | ✓ | Saved learned-skill identifier from list_skills |
| `params` | any |  |  |

<details>
<summary>5 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
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
      "type": "string",
      "description": "Saved learned-skill identifier from list_skills"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `validate_playtest_aliases`

🟢 86/100 · Risk: 🟡 medium

Compare alias .defs text file vs PlaytestConfig.asset. Reports missing/extra/changed. defs: project-relative path to .defs file (default: Assets/PlaytestDefs/farm_core.defs). asset: asset path to PlaytestConfig (default: Assets/Configs/PlaytestConfig.asset). Returns 'ok: N aliases in sync' when identical, or a diff report.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `asset` | string |  |  (default: `Assets/Configs/PlaytestConfig.asset`) |
| `defs` | string |  |  (default: `Assets/PlaytestDefs/farm_core.defs`) |

<details>
<summary>6 quality issues</summary>

- **warning**: Object schema has properties but no required list.
- **info**: Parameter 'defs' has no description.
- **info**: Free-form string parameter 'defs' has no maxLength.
- **info**: Parameter 'asset' has no description.
- **info**: Free-form string parameter 'asset' has no maxLength.
- **warning**: outputSchema is missing.

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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `validate_references`

🟢 82/100 · Risk: 🟡 medium

Validate all ObjectReference fields under path recursively. Returns [ERROR]/[MISSING] for broken refs. Summary: "N ERROR, M OK". Use depth=1 for quick top-level scan, depth=3-5 for full subtree. verbose=True also shows [OK] lines (off by default to save tokens). ignore_optional=True skips fields marked [Optional] (reduces noise).

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `depth` | integer |  | Maximum hierarchy depth to traverse (default: `3`) |
| `ignore_optional` | boolean |  |  (default: `False`) |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `verbose` | boolean |  |  (default: `False`) |

<details>
<summary>6 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **warning**: Numeric parameter 'depth' has no bounds.
- **info**: Parameter 'verbose' has no description.
- **info**: Parameter 'ignore_optional' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "depth": {
      "default": 3,
      "title": "Depth",
      "type": "integer",
      "description": "Maximum hierarchy depth to traverse"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `validate_triggers`

🟢 83/100 · Risk: 🟡 medium

Check 3D trigger/collider overlaps. Warns if triggers closer than min_distance meters.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `min_distance` | number |  |  (default: `3.0`) |
| `root` | string |  | Scene path to scope the tree (omit = whole scene) (default: `/`) |

<details>
<summary>5 quality issues</summary>

- **warning**: Object schema has properties but no required list.
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
      "type": "string",
      "description": "Scene path to scope the tree (omit = whole scene)"
    },
    "min_distance": {
      "default": 3.0,
      "title": "Min Distance",
      "type": "number"
    }
  },
  "title": "validate_triggersArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `verify_after_change`

🔴 57/100 · Risk: 🔴 high

Single verification gate after code/scene changes. Gates are additive — only enabled ones run: 1. await_compile (always) 2. get_compile_errors (always) 3. get_console_since mark_id (if mark_id provided) 4. run_tests_wait mode filter (if run_tests_mode provided) 5. run_playtest_suite paths (if playtests provided) Returns PASS only when ALL enabled gates pass. Failure includes which gate failed and recommended next command.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `auto_play` | boolean |  |  (default: `False`) |
| `changed_files` | string |  |  (default: ``) |
| `mark_id` | string |  |  (default: ``) |
| `playtests` | string |  |  (default: ``) |
| `restart_between` | boolean |  |  (default: `False`) |
| `run_tests_mode` | string |  |  (default: ``) |
| `suite_timeout` | number |  |  (default: `300.0`) |
| `test_filter` | string |  |  (default: ``) |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `300.0`) |

<details>
<summary>19 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **warning**: Object schema has properties but no required list.
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
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'auto_play' has no description.
- **info**: Parameter 'restart_between' has no description.
- **info**: Parameter 'suite_timeout' has no description.
- **warning**: Numeric parameter 'suite_timeout' has no bounds.
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
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
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
    },
    "suite_timeout": {
      "default": 300.0,
      "title": "Suite Timeout",
      "type": "number"
    }
  },
  "title": "verify_after_changeArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `vfx_intent`

🟡 74/100 · Risk: 🟡 medium

Convert NL intent to Unity VFX setup. Presets bypass Haiku entirely.  kind: particle|auto (shader/material not yet implemented). dry_run=True skips execution.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `dry_run` | boolean |  | Preview changes without applying them (default: `False`) |
| `intent` | string | ✓ |  |
| `kind` | string |  |  (default: `auto`) |
| `target` | string | ✓ |  |

<details>
<summary>10 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Parameter 'target' has no description.
- **info**: Free-form string parameter 'target' has no maxLength.
- **info**: Parameter 'intent' has no description.
- **info**: Free-form string parameter 'intent' has no maxLength.
- **info**: Parameter 'kind' has no description.
- **warning**: String parameter 'kind' appears categorical but has no enum.
- **info**: Free-form string parameter 'kind' has no maxLength.
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
      "type": "boolean",
      "description": "Preview changes without applying them"
    }
  },
  "required": [
    "target",
    "intent"
  ],
  "title": "vfx_intentArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `wait_until`

🟡 69/100 · Risk: 🟡 medium

[Play Mode] Poll field until it matches value (or timeout). Python timeout = Unity timeout + 5s buffer. abort_on_fail=True: stops Play Mode on timeout.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `abort_on_fail` | boolean |  | Stop Play Mode if the comparison times out (default False) (default: `False`) |
| `component` | string | ✓ | Component type name on the target object |
| `field` | string | ✓ |  |
| `negate` | boolean |  |  (default: `False`) |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `timeout` | number |  | Seconds before giving up (default varies per tool) (default: `5.0`) |
| `value` | string | ✓ | Expected field value to compare against (this tool does not set the field) |

<details>
<summary>11 quality issues</summary>

- **warning**: Tool appears to have side effects but the description does not state them clearly.
- **warning**: Risky tool lacks a clear usage boundary.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
- **info**: Free-form string parameter 'component' has no maxLength.
- **info**: Parameter 'field' has no description.
- **info**: Free-form string parameter 'field' has no maxLength.
- **info**: Free-form string parameter 'value' has no maxLength.
- **warning**: Numeric parameter 'timeout' has no bounds.
- **info**: Parameter 'negate' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
    },
    "field": {
      "title": "Field",
      "type": "string"
    },
    "value": {
      "title": "Value",
      "type": "string",
      "description": "Expected field value to compare against (this tool does not set the field)"
    },
    "timeout": {
      "default": 5.0,
      "title": "Timeout",
      "type": "number",
      "description": "Seconds before giving up (default varies per tool)"
    },
    "negate": {
      "default": false,
      "title": "Negate",
      "type": "boolean"
    },
    "abort_on_fail": {
      "default": false,
      "title": "Abort On Fail",
      "type": "boolean",
      "description": "Stop Play Mode if the comparison times out (default False)"
    }
  },
  "required": [
    "path",
    "component",
    "field",
    "value"
  ],
  "title": "wait_untilArguments",
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `watch`

🟡 68/100 · Risk: 🔴 high

[Play Mode] Manage watches. Registers or removes watches. No confirmation required. action: add|remove|clear|reset. add: needs path/component/field. condition: '< 10','> 0','== null'. trigger_action: 'log' or 'pause'. remove/reset: needs watch_id.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `action` | string | ✓ | Operation to perform — see tool docstring for allowed values |
| `component` | string |  | Component type name on the target object (default: ``) |
| `condition` | string |  |  (default: ``) |
| `field` | string |  |  (default: ``) |
| `interval_ms` | integer |  |  (default: `500`) |
| `path` | string |  | Scene path to target GameObject (e.g. /Parent/Child) (default: ``) |
| `trigger_action` | string |  |  (default: `log`) |
| `watch_id` | string |  |  (default: ``) |

<details>
<summary>16 quality issues</summary>

- **info**: Free-form string parameter 'action' has no maxLength.
- **info**: Parameter 'watch_id' has no description.
- **info**: Free-form string parameter 'watch_id' has no maxLength.
- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
      "type": "string",
      "description": "Operation to perform \u2014 see tool docstring for allowed values"
    },
    "watch_id": {
      "default": "",
      "title": "Watch Id",
      "type": "string"
    },
    "path": {
      "default": "",
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "default": "",
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---

### `wire_event`

🟡 77/100 · Risk: 🔴 high

Wire UnityEvent persistent listener. Mutates scene. No confirmation required. path: object with the event. component: type owning the event field. event: serialized field name (e.g. 'onClick', '_onComplete'). target: scene path or asset path. Auto-resolves component owning the method. method: method name (e.g. 'SetActive', 'Play'). arg_type: void|bool|int|float|string|object. arg_value: required when arg_type != void. For object: scene path or asset path. target_component_type: narrow component search to this type (e.g. 'Animator'). parameter_types: comma-separated param types to resolve overloads (e.g. 'string').

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `arg_type` | string |  |  (default: `void`) |
| `arg_value` | any |  |  |
| `component` | string | ✓ | Component type name on the target object |
| `event` | string | ✓ |  |
| `method` | string | ✓ |  |
| `parameter_types` | any |  |  |
| `path` | string | ✓ | Scene path to target GameObject (e.g. /Parent/Child) |
| `target` | string | ✓ |  |
| `target_component_type` | any |  |  |

<details>
<summary>15 quality issues</summary>

- **info**: Free-form string parameter 'path' has no maxLength.
- **warning**: Path-like parameter 'path' has no structural constraint.
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
- **info**: Parameter 'target_component_type' has no description.
- **info**: Parameter 'parameter_types' has no description.
- **warning**: outputSchema is missing.

</details>

<details>
<summary>JSON Schema</summary>

```json
{
  "properties": {
    "path": {
      "title": "Path",
      "type": "string",
      "description": "Scene path to target GameObject (e.g. /Parent/Child)"
    },
    "component": {
      "title": "Component",
      "type": "string",
      "description": "Component type name on the target object"
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
    },
    "target_component_type": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Target Component Type"
    },
    "parameter_types": {
      "anyOf": [
        {
          "type": "string"
        },
        {
          "type": "null"
        }
      ],
      "default": null,
      "title": "Parameter Types"
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
  "type": "object",
  "additionalProperties": false
}
```

</details>

---
