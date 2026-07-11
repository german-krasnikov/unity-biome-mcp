# MCP Tools Reference (139 total)

All tools organized by category. TIER1 tools (59) are always visible. Themed categories (Tier2) require `discover_tools(category)` to enable. Plugin tools discovered dynamically via auto-gating.

## TIER1 Tools (Always Visible, 59 total)

Essential read/scene, meta, connection, repair, plus high-value tools for screenshots, testing, runtime, and code intelligence.

| Tool | Purpose | Key Params | Category |
|------|---------|------------|----------|
| get_hierarchy | Read scene tree | summary, depth | scene read |
| get_component | Read component values | path, component, compress (strip defaults) | scene read |
| inspect | Batch-read N objects' components | query, filter, compress (strip defaults) | scene read |
| set_property | Write component value | path, component, prop, value | scene write |
| create_object | Spawn GameObject | name, parent, components | scene write |
| delete_object | Remove GameObject | path | scene write |
| manage_component | Add/remove components | path, action, component | scene write |
| batch | Run 2+ ops atomically; readonly batches exempt from blast-radius warnings | commands (JSON array), validate_aliases (dry-run alias check) | orchestration |
| scene | List/load/save scenes | action, name | scene meta |
| search_scene | Find objects by pattern | query, type | scene read |
| set_parent | Reparent GameObject | path, parent | scene write |
| get_console | Read Editor.log tail | lines, severity | diagnostics |
| console_mark | Create timestamp watermark for log slicing | label | diagnostics |
| get_console_since | Console entries after a watermark | mark_id, level, count | diagnostics |
| get_compile_errors | List C# compile errors | — | diagnostics |
| get_enabled_tools | List visible tools | — | meta |
| discover_tools | Get tool schemas | filter | meta |
| editor | Open Editor windows | window_type, path, action | editor control |
| do | Execute arbitrary code | prompt, context, action | code generation |
| ask | Query LLM about scene | query, context | LLM read |
| ask_user | Prompt human | question, options | human interaction |
| permission_prompt | Gate sensitive ops | operation, details | security |
| reconnect_unity | Reconnect TCP socket | port (auto-discover) | connection |
| list_connections | Show connection status with semantic states (connected/reconnecting/domain-reloading/disconnected) | — | connection |
| alias_status | Returns alias cache state | — (returns loaded/count/source/stale) | connection |
| set_client_label | Set human-readable label for this MCP client session | label | connection |
| resolve_tool_schema | Deferred schema fetch | tool_name | meta |
| doctor | Health diagnostics | fix (auto-fix stale PIDs) | diagnostics |
| screenshot | Capture frame | width, height, camera, path (output) | visual |
| run_tests | Execute NUnit tests | mode (EditMode/PlayMode), filter | testing |
| setup_objects | Batch create + wire objects | spec (JSON template array) | setup |
| set_properties | Batch set properties | objects_and_values (JSON) | setup |
| configure_objects | Batch configure components | objects_and_config (JSON) | setup |
| find_references | Locate usages of symbol | symbol_name, include_tests | code-intel |
| compile_preflight | Check compile readiness | fix (bool) | code-intel |
| semantic_at | Language server: definition/hover | path, line, col, action | code-intel |
| await_compile | Block until compile done | timeout | code-intel |
| sync_unity | Reload and restart | reason, wait (bool) | editor-control |
| invoke_method | Call method at runtime | path, component, method, args (JSON) | runtime |
| set_runtime_property | Set field/property at runtime | path, component, prop, value | runtime |
| wait_until | Busy-wait on condition | query, op, value, timeout | runtime |
| move_to | Pathfind + walk to position | path, dest_pos, speed, timeout | runtime |
| query_state | Read runtime GameObject state | path, queries (CSV) | runtime |
| test_step | Execute single DSL step | step (JSON), config | testing |
| run_playtest | Run playtest DSL script | script (DSL, mutually exclusive with path), path (file path relative to project root, optional), abort_on_fail | testing |
| run_playtest_file | Run single .playtest file by path | path, timeout, abort_on_fail, defs, snapshot_on_failure | testing |
| run_playtest_suite | Run multiple .playtest files sequentially; returns pass matrix | paths (glob/CSV), timeout_per_test, stop_on_fail, stop_after | testing |
| run_tests_wait | Synchronous NUnit test runner; blocks until done or timeout | mode, filter, timeout, poll_interval | testing |
| lint_playtest | Static DSL preflight — no runtime needed | path or script | testing |
| lint_playtest_suite | Lint all matched .playtest files; aggregated report | paths (glob/CSV) | testing |
| validate_playtest_aliases | Diff .defs text file vs PlaytestConfig.asset | defs, asset | testing |
| sync_playtest_aliases_from_defs | Import .defs → overwrite PlaytestConfig.asset aliases | defs, asset | testing |
| export_playtest_aliases_to_defs | Export PlaytestConfig.asset aliases → .defs text file | asset, defs | testing |
| resolve_scene_refs | Resolve $alias, /path, t:Type refs to scene paths | refs, fields | scene read |
| lint_scene_refs | 3-pass linter for scene refs in DSL/batch commands | path or snippet | scene read |
| scene_change_plan | Pre-flight gate + checkpoint before scene edits | goal, targets, dry_run | scene write |
| apply_scene_change | Execute planned mutations with post-verify and save | plan_id, commands, verify, save | scene write |
| verify_after_change | 5-gate pipeline: compile → errors → console → tests → playtests | changed_files, test_filter, run_tests_mode, playtests, mark_id, timeout | diagnostics |
| mcp_status | Compact scene/compile/play-mode/alias status snapshot | — | meta |

## SCENE_EDIT (9 tools)

Scene object manipulation: find, detail, components, active, rename, material, delta, diff, transfer. Note: `scene_change_plan`, `apply_scene_change`, `resolve_scene_refs`, `lint_scene_refs` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| find_objects | Search objects | query, type |
| get_object_detail | Get object state | path |
| get_components_list | List components on object | path |
| set_active | Toggle active flag | path, active |
| rename_object | Rename GameObject, returns new path | path, name |
| set_material | Assign material to object | path, material_path, slot |
| set_property_delta | Relative property change | path, component, prop, delta |
| object_diff | Compare two objects | path1, path2 |
| transfer_object | Move object between scenes | path, target_scene |

## COMPONENTS (3 tools)

Component event wiring: connect/disconnect event listeners.

| Tool | Purpose | Key Params |
|------|---------|------------|
| wire_event | Connect event to method | path, component, event, target_path, target_method |
| unwire_event | Disconnect event listener | path, component, event, target_path |
| auto_wire | Auto-wire compatible event | path, component, event |

## ANIMATION (4 tools)

Animation playback, timeline, animator state.

| Tool | Purpose | Key Params |
|------|---------|------------|
| animation | Play clip on Animator/Anim | path, clip_name, speed, loop |
| timeline | Control Timeline | path, action (play/pause/stop), time |
| animator | Get/set Animator parameters | path, param_name, param_type, value |
| particle | Emit/stop particles | path, action (play/stop/clear), count |

## SHADERS_MATERIAL (4 tools)

Shader, material, reference, and asset audit operations.

| Tool | Purpose | Key Params |
|------|---------|------------|
| shader | Find shader, list properties | name, action (find/get_props) |
| material | Assign/inspect material | path, material_path, slot |
| references | Find asset references | asset_path, include_indirect |
| material_audit | Audit material usage and performance | filter, fix (bool) |

## VFX (1 Tier2 tool)

Visual effects intent — AI-driven VFX tweaks.

| Tool | Purpose | Key Params |
|------|---------|------------|
| vfx_intent | AI vfx description → settings | target, intent, kind |

## UI (5 tools)

UI creation, rect layout, validation, spatial context.

| Tool | Purpose | Key Params |
|------|---------|------------|
| create_ui | Spawn UI elements | type, name, parent, rect |
| set_rect | Modify RectTransform | path, anchor, offset, size |
| validate_layout | Check UI constraints | path, fix (bool) |
| get_spatial_context | Proximity query | path, radius, layer_mask |
| ui_intent | AI ui description → components | parent, description, context |

## SCREENSHOTS (2 Tier2 tools)

Baseline diff and regression detection. Note: `screenshot` itself is TIER1 (see above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| screenshot_baseline | Save baseline for regression | name, width, height, camera |
| screenshot_compare | Diff baseline ↔ current | name, mode (auto/pixel/structural/targeted), question |

## UNIT_TESTS (1 Tier2 tool)

Test result polling. Note: `run_tests`, `run_tests_wait`, `run_playtest`, `run_playtest_file`, `run_playtest_suite`, `test_step`, `lint_playtest`, `lint_playtest_suite`, `validate_playtest_aliases`, `sync_playtest_aliases_from_defs`, `export_playtest_aliases_to_defs` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| get_test_results | Poll test status | — |

## RUNTIME (3 Tier2 tools)

Performance and debugging at runtime. Note: `invoke_method`, `set_runtime_property`, `wait_until`, `move_to`, `query_state` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| get_perf | Profiling data (draw calls, meshes, etc) | filter |
| debug_animator | Animator state inspection | path |
| debug_physics | Physics debugger | mode, layer_mask |

## ASSETS (4 tools)

Asset database: import/export, prefab, ScriptableObject, project settings.

| Tool | Purpose | Key Params |
|------|---------|------------|
| asset | Asset DB operations | action (find/get_info/create/move/duplicate/delete/import/export), path, type, name |
| prefab | Prefab lifecycle | action (save/create_variant/apply/revert), path, asset_path |
| scriptable_object | ScriptableObject create/read/write | action, type, path, values |
| project_settings | Project config | action (get/set), target (tags/layers/quality), prop, value |

## ADVANCED_CODE (10 Tier2 tools)

Code generation, refactoring, validation, and diagnostics. Note: `find_references`, `semantic_at`, `compile_preflight`, `await_compile`, `sync_unity`, `verify_after_change` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| execute_code | Run C# in Editor | code (C# method body), undo_label |
| recompile | Force script compilation | — |
| get_schema | Inspect class/type schema | type_name, include_bases |
| auto_fix | Apply code fix suggestion | file_path, fix_id |
| smart_build | Rebuild affected assemblies | affected_paths |
| checkpoint | Save named revision | name, description |
| validate_references | Check all refs valid | fix (bool) |
| undo_last | Revert last operation | steps |
| menu | Execute Editor menu item | menu_path |
| diagnose | Deep troubleshooting | system (compile/tcp/memory/reload) |

## SESSION_SKILLS (11 tools)

Persistent reusable skills, templates, session snapshots, change tracking, scenario files.

| Tool | Purpose | Key Params |
|------|---------|------------|
| save_skill | Store reusable C# or batch | name, description, code (auto-detects kind) |
| use_skill | Execute saved skill | name, params (key=value CSV; supports parenthesized values e.g. `pos=(0,5,0)`) |
| list_skills | Show all skills + usage | — |
| save_template | Store scene template | name, description, template_code |
| apply_template | Instantiate template | name, params (key=value CSV) |
| list_templates | Show all templates | — |
| fingerprint | Hash scene state | — |
| scene_diff | Compare two fingerprints | fp1, fp2 |
| get_changes | Log editor events since last call | clear (bool) |
| save_session | Snapshot hierarchy to .claude/session-context.json | — |
| load_session | Load + diff previous session | — |

## META (8 Tier2 tools)

Scene scanning, spatial queries, and config. Note: `setup_objects`, `set_properties`, `configure_objects`, `mcp_status` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| scan_scene | Audit for issues | checks (CSV: refs/colliders/physics/null_components) |
| check_colliders | Collision layer conflicts | fix (bool), path (optional, scans whole scene if omitted) |
| spatial_query | Radial/box search + filter | origin, radius, layer_mask, type_filter |
| region_clear | Clear region of GameObjects | region, layer_mask |
| navmesh_query | Pathfinding query | start_pos, end_pos, area_mask |
| scene_health | Comprehensive scene audit | — |
| set_llm_config | Store LLM settings | param, value |
| budget_status | Token usage tracking | — |

## DEBUG (8 Tier2 tools)

Session debugging, breakpoints, metric snapshots. Note: `console_mark`, `get_console_since`, `verify_after_change` are TIER1 (see TIER1 section above).

| Tool | Purpose | Key Params |
|------|---------|------------|
| debug | Session debugger | action (run/step/continue), bp_path |
| snapshot | Take memory snapshot | name, labels |
| watch_add | Add watch expression | expr, name |
| get_watches | Retrieve all watches | — |
| watch_remove | Delete watch | name |
| watch_clear | Clear all watches | — |
| watch_reset | Reset watch history | name |
| get_metrics | Profiling metrics | filter |

## PROFILING (3 Tier2 tools)

Performance profiling and frame analysis.

| Tool | Purpose | Key Params |
|------|---------|------------|
| get_frame_stats | Frame profiling data | mode (full/summary), frames |
| profile | CPU/GPU profiler control | action (start/stop/dump), target |
| get_memory | Memory profiling | detailed (bool) |

## RENDERING (2 Tier2 tools)

Rendering analysis and optimization.

| Tool | Purpose | Key Params |
|------|---------|------------|
| render_analyze | Rendering bottleneck analysis | — |
| analyze_lod_culling | LOD and culling audit | — |

## PLUGINS (0 tools by default)

Auto-gated category for external plugins. Tools registered via `@mcp.tool()` without `register_tools()` are automatically enrolled here and hidden by default.

## CONNECTION (0 tools, internal only)

Empty category — `list_connections` and `reconnect_unity` are in TIER1/CORE and always visible.

---

**See also:** AI/architecture.md (design), AI/mcp-server.md (protocol), .claude/skills/ (token patterns).
