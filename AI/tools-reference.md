# MCP Tools Reference

All tools organized by category. TIER1 tools (46 always-visible) require no `discover_tools`. Tier2 tools require `discover_tools(category)` first. Plugin tools discovered dynamically.

**v0.84.0 breaking changes:** `create_ui`/`set_rect` params renamed (`fontSize`→`font_size`, `offsetMin`→`offset_min`, `offsetMax`→`offset_max`); `object_diff` params renamed (`pathA`→`path_a`, `pathB`→`path_b`).

## CORE Tools (11 — always visible, zero setup)

The minimum 11 tools needed for any Unity task. Always visible, no gating.

| Tool | Purpose | Key Params |
|------|---------|------------|
| get_hierarchy | Read scene tree | summary, depth |
| get_component | Read component values | path, component, fields (projection), compress (strip defaults) |
| inspect | Batch-read N objects' components | query, filter, fields, compress |
| set_property | Write component value | path, component, prop, value |
| create_object | Spawn GameObject | name, parent, components |
| manage_component | Add/remove components | path, action, component |
| batch | Run 2+ ops atomically; readonly batches skip blast-radius | commands (JSON array), validate_aliases |
| get_console | Read Editor.log tail | lines, severity |
| get_compile_errors | List C# compile errors | — |
| editor | Open Editor windows / control play mode | window_type, path, action |
| do | Execute arbitrary code via AI intent | prompt, context, action |

## TIER1 Tools (46 total — always visible)

Includes CORE (11) + individually promoted tools below.

| Tool | Purpose | Key Params | Category |
|------|---------|------------|----------|
| delete_object | Remove GameObject | path | SCENE |
| set_parent | Reparent GameObject | path, parent | SCENE |
| set_active | Toggle active flag | path, active | SCENE |
| scene | List/load/save scenes | action, name | SCENE |
| search_scene | Find objects by pattern | query, type | SCENE |
| configure_objects | Batch configure components | objects_and_config (JSON) | SCENE |
| setup_objects | Batch create + wire objects | spec (JSON template array) | SCENE |
| scene_change_plan | Pre-flight gate + checkpoint before scene edits | goal, targets, dry_run | SCENE |
| apply_scene_change | Execute planned mutations with post-verify and save | plan_id, commands, verify, save | SCENE |
| screenshot | Capture frame | width, height, camera, path (output) | MEDIA |
| get_console_since | Console entries after a watermark | mark_id, level, count | RUNTIME |
| console_mark | Create timestamp watermark for log slicing | label | RUNTIME |
| await_compile | Block until compile done | timeout | VERIFY |
| compile_preflight | Check compile readiness | fix (bool) | VERIFY |
| validate_references | Check all refs valid | fix (bool) | VERIFY |
| lint_scene_refs | 3-pass linter for scene refs in DSL/batch commands | path or snippet | VERIFY |
| resolve_scene_refs | Resolve $alias, /path, t:Type refs to scene paths | refs, fields | VERIFY |
| verify_after_change | 5-gate pipeline: compile → errors → console → tests → playtests | changed_files, test_filter, run_tests_mode, playtests, mark_id, timeout | VERIFY |
| run_tests | Execute NUnit tests | mode (EditMode/PlayMode), filter | TESTS |
| run_tests_wait | Synchronous NUnit test runner; blocks until done or timeout | mode, filter, timeout, poll_interval | TESTS |
| run_playtest | Run playtest DSL script | script (DSL) or path (file path), abort_on_fail | TESTS |
| lint_playtest | Static DSL preflight — no runtime needed | path or script | TESTS |
| get_test_results | Poll test status | — | TESTS |
| get_enabled_tools | List visible tools | — | SYSTEM |
| discover_tools | Get tool schemas / enable a category | filter | SYSTEM |
| mcp_status | Compact scene/compile/play-mode/alias status snapshot | — | SYSTEM |
| alias_status | Returns alias cache state (loaded/count/source/stale) | — | SYSTEM |
| release_smoke | Run status + aliases + compile gates in one call | — | SYSTEM |
| ask | Query LLM about scene | query, context | SYSTEM |
| ask_user | Prompt human | question, options | SYSTEM |
| permission_prompt | Gate sensitive ops | operation, details | SYSTEM |
| reconnect_unity | Reconnect TCP socket | port (auto-discover) | SYSTEM |
| resolve_tool_schema | Deferred schema fetch | tool_name | SYSTEM |
| doctor | Health diagnostics | fix (auto-fix stale PIDs) | SYSTEM |
| execute_code | Run C# in Editor | code (C# method body), undo_label | SYSTEM |
| sync_unity | Reload and restart | reason, wait (bool) | SYSTEM |
| undo_last | Revert last N editor operations | steps | SYSTEM |

---

## Tier2 Categories (require `discover_tools(category)`)

### SCENE (29 tools)

Scene manipulation beyond CORE/TIER1 basics.

| Tool | Purpose | Key Params |
|------|---------|------------|
| find_objects | Search objects by query | query, type |
| get_object_detail | Detailed object state | path |
| get_components_list | List components on object | path |
| get_selection | Current editor selection | — |
| get_spatial_context | Proximity query | path, radius, layer_mask |
| set_properties | Batch set properties | objects_and_values (JSON) |
| set_material | Assign material to object | path, material_path, slot |
| set_property_delta | Relative property change | path, component, prop, delta |
| set_sibling_index | Change sibling order | path, index |
| set_active | (also TIER1) Toggle active flag | path, active |
| rename_object | Rename GameObject, returns new path | path, name |
| object_diff | Compare two objects | path_a, path_b |
| transfer_object | Move object between scenes | path, target_scene |
| ping_object | Flash object in hierarchy | path |
| autofit_collider | Auto-fit collider bounds | path |
| check_colliders | Collision layer conflicts | fix (bool), path (optional) |
| spatial_query | Radial/box search + filter | origin, radius, layer_mask, type_filter |
| region_clear | Clear region of GameObjects | region, layer_mask |
| navmesh_query | Pathfinding query | start_pos, end_pos, area_mask |
| scene_environment | Get/set scene lighting/environment | action, property, value |
| scene_diff | Compare two fingerprints | fp1, fp2 |

### COMPONENTS (4 tools)

Component event wiring.

| Tool | Purpose | Key Params |
|------|---------|------------|
| wire_event | Connect event to method | path, component, event, target_path, target_method |
| unwire_event | Disconnect event listener | path, component, event, target_path |
| auto_wire | Auto-wire compatible fields by type | path, component, event |
| references | Find asset references | asset_path, include_indirect |

### ASSETS (7 tools)

Asset database: import/export, prefab, ScriptableObject, project settings.

| Tool | Purpose | Key Params |
|------|---------|------------|
| asset | Asset DB operations | action (find/get_info/create/move/duplicate/delete/import/export), path, type, name |
| prefab | Prefab lifecycle | action (save/create_variant/apply/revert), path, asset_path |
| scriptable_object | ScriptableObject create/read/write | action, type, path, values |
| project_settings | Project config | action (get/set), target (tags/layers/quality), prop, value |
| shader | Find shader, list properties | name, action (find/get_props) |
| material | Assign/inspect material | path, material_path, slot |
| material_audit | Audit material usage and performance | filter, fix (bool) |

### MEDIA (14 tools)

Visual output, UI, animations, VFX, rendering analysis.

| Tool | Purpose | Key Params |
|------|---------|------------|
| screenshot_baseline | Save baseline for regression | name, width, height, camera |
| screenshot_compare | Diff baseline ↔ current | name, mode (auto/pixel/structural/targeted), question |
| animation | Play clip on Animator | path, clip_name, speed, loop |
| timeline | Control Timeline | path, action (play/pause/stop), time |
| animator | Get/set Animator parameters | path, param_name, param_type, value |
| particle | Emit/stop particles | path, action (play/stop/clear), count |
| create_ui | Spawn UI elements | type, name, parent, rect |
| set_rect | Modify RectTransform | path, anchor, offset_min, offset_max, size, font_size |
| validate_layout | Check UI constraints | path, fix (bool) |
| ui_intent | AI ui description → components | parent, description, context |
| vfx_intent | AI vfx description → settings | target, intent, kind |
| render_analyze | Rendering bottleneck analysis (9 actions) | action (stats/overdraw/materials/shaders/batching/lights/shadow_audit/probe_audit/frame_debug) |
| analyze_lod_culling | LOD and culling audit | — |

### VERIFY (9 tools)

Compile, references, lint, scene audit.

| Tool | Purpose | Key Params |
|------|---------|------------|
| scan_scene | Audit for issues | checks (CSV: refs/colliders/physics/null_components) |
| scene_health | Comprehensive scene audit | — |
| diagnose | Deep troubleshooting | system (compile/tcp/memory/reload) |

*(Plus 6 in TIER1: await_compile, compile_preflight, validate_references, lint_scene_refs, resolve_scene_refs, verify_after_change)*

### RUNTIME (18 tools)

Play Mode operations, performance, debugging, watches.

| Tool | Purpose | Key Params |
|------|---------|------------|
| invoke_method | Call method at runtime | path, component, method, args (JSON) |
| set_runtime_property | Set field/property at runtime | path, component, prop, value |
| wait_until | Busy-wait on condition | query, op, value, timeout |
| move_to | Pathfind + walk to position | path, dest_pos, speed, timeout |
| query_state | Read runtime GameObject state | path, queries (CSV) |
| get_frame_stats | Instant performance snapshot | include= (field filter) |
| get_perf | **REMOVED** (v0.85.1) — use get_frame_stats | filter |
| profile | CPU/GPU profiler control | action (start/stop/dump/analyze/compare), target |
| get_memory | Memory profiling | detailed (bool) |
| debug_animator | Animator state inspection | path |
| debug_physics | Physics debugger | mode, layer_mask |
| debug | Session debugger | action (run/step/continue), bp_path |
| snapshot | Take memory snapshot | name, labels |
| watch | Watch expression lifecycle | action (add/get/remove/clear/reset), expr, name |
| get_watches | Retrieve all watches | — |
| get_metrics | Profiling metrics | filter |

*(Plus console_mark + get_console_since in TIER1)*

### TESTS (14 tools)

NUnit, playtest suites, alias sync.

| Tool | Purpose | Key Params |
|------|---------|------------|
| run_playtest_file | **REMOVED** (v0.85.1) — use `run_playtest(path=...)` | path, timeout, abort_on_fail, defs, snapshot_on_failure |
| run_playtest_suite | Run multiple .playtest files sequentially | paths (glob/CSV), timeout_per_test, stop_on_fail, auto_play |
| test_step | Execute single DSL step | step (JSON), config |
| lint_playtest_suite | Lint all matched .playtest files; aggregated report | paths (glob/CSV) |
| validate_playtest_aliases | Diff .defs text file vs PlaytestConfig.asset | defs, asset |
| sync_playtest_aliases_from_defs | Import .defs → overwrite PlaytestConfig.asset aliases | defs, asset |
| export_playtest_aliases_to_defs | Export PlaytestConfig.asset aliases → .defs text file | asset, defs |
| get_test_count | Count available NUnit tests | filter |
| get_test_progress | Poll test run progress | — |

*(Plus run_tests, run_tests_wait, run_playtest, lint_playtest, get_test_results in TIER1)*

### SYSTEM (35 tools)

Meta, session skills, templates, config, code tools.

| Tool | Purpose | Key Params |
|------|---------|------------|
| save_skill | Store reusable C# or batch | name, description, code |
| use_skill | Execute saved skill | name, params (key=value CSV) |
| list_skills | Show all skills + usage | — |
| save_template | Store scene template | name, description, template_code |
| apply_template | Instantiate template | name, params (key=value CSV) |
| list_templates | Show all templates | — |
| fingerprint | Hash scene state | — |
| get_changes | Log editor events since last call | clear (bool) |
| save_session | Snapshot hierarchy to .claude/session-context.json | — |
| load_session | Load + diff previous session | — |
| recompile | Force script compilation | — |
| get_schema | Inspect class/type schema | type_name, include_bases |
| auto_fix | Apply code fix suggestion | file_path, fix_id |
| smart_build | Rebuild affected assemblies | affected_paths |
| checkpoint | Save named revision | name, description |
| menu | Execute Editor menu item | menu_path |
| get_capabilities | List all registered C# commands | — |
| budget_status | Token usage tracking | — |
| set_llm_config | Store LLM settings | param, value |
| list_connections | Show connection status with semantic states | — |
| find_references | Locate usages of symbol | symbol_name, include_tests |
| semantic_at | Language server: definition/hover | path, line, col, action |
| animator_intent | AI animator description → controller | target, intent |

*(Plus alias_status, release_smoke, ask, ask_user, permission_prompt, reconnect_unity, resolve_tool_schema, doctor, execute_code, discover_tools, get_enabled_tools, mcp_status, sync_unity, undo_last in TIER1)*

---

## Legacy Category Aliases (DeprecationWarning in v0.84.0)

Old category names still work but emit a warning. Use canonical names instead:

| Old name | Canonical |
|----------|-----------|
| `object` / `SCENE_EDIT` | `SCENE` + `COMPONENTS` |
| `animation` / `ANIMATION` | `MEDIA` |
| `ui` / `UI` | `MEDIA` |
| `vfx` / `VFX` | `MEDIA` |
| `rendering` / `RENDERING` | `MEDIA` |
| `asset` / `SHADERS_MATERIAL` | `ASSETS` |
| `runtime` / `UNIT_TESTS` | `RUNTIME` + `TESTS` |
| `profiling` / `PROFILING` / `perf` | `RUNTIME` |
| `debug` / `DEBUG` | `RUNTIME` |
| `advanced` / `ADVANCED_CODE` | `SYSTEM` + `VERIFY` |
| `connection` | `SYSTEM` |
| `session` / `SESSION_SKILLS` | `SYSTEM` |
| `plugins` / `PLUGINS` | `SYSTEM` |
| `meta` / `META` | `SCENE` + `SYSTEM` |

---

**See also:** AI/architecture.md (design), AI/mcp-server.md (protocol), .claude/skills/token-optimization.md (batch patterns).
