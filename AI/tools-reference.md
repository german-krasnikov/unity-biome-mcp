# MCP Tools Reference

Design standards: `AI/api-design-standards.md`

Tool index organized by category. TIER1 tools (47 always-visible) require no `discover_tools`. Tier2 tools require `discover_tools(category)` first. Plugin tools are discovered dynamically.

Parameter lists are intentionally not duplicated here because the MCP schemas are the runtime contract. Resolve the current schema before calling an unfamiliar tool:

```python
await resolve_tool_schema(tools="get_component,inspect")
await discover_tools(category="SCENE", enable=False, structured=True)
```

`resolve_tool_schema` accepts a comma-separated `tools` string and returns the captured full descriptions and parameter schemas. `discover_tools` browses or enables categories; it is not the parameter-schema source.

**direct_only tools** cannot be used inside `batch` commands — call them as typed MCP tools directly. Affects: `animator_intent`, `ask`, `await_compile`, `budget_status`, `configure_objects`, `console_mark`, `debug`, `discover_tools`, `do`, `doctor`, `get_console_since`, `get_metrics`, `lint_playtest_suite`, `list_connections`, `list_skills`, `list_templates`, `mcp_status`, `navmesh_query`, `release_smoke`, `resolve_tool_schema`, `run_playtest_suite`, `run_tests_wait`, `screenshot_baseline`, `screenshot_compare`, `set_properties`, `setup_objects`, `snapshot`, `ui_intent`, `validate_playtest_aliases`, `vfx_intent`, `watch`.

**v0.84.0 breaking changes:** `create_ui`/`set_rect` params renamed (`fontSize`→`font_size`, `offsetMin`→`offset_min`, `offsetMax`→`offset_max`); `object_diff` params renamed (`pathA`→`path_a`, `pathB`→`path_b`).

## CORE Tools (15 — always visible, zero setup)

The minimum 15 tools needed for any Unity task. Always visible, no gating.

| Tool | Purpose |
| ------ | --------- |
| get_hierarchy | Read scene tree |
| get_component | Read component values |
| inspect | Batch-read N objects' components |
| set_property | Write component value |
| create_object | Spawn GameObject |
| manage_component | Add/remove components |
| batch | Run compatible operations sequentially; `atomic=true` reverts Undo-recorded Unity changes on failure |
| get_console | Read Editor.log tail |
| get_compile_errors | List C# compile errors |
| editor | Open Editor windows / control play mode |
| apply_scene_change | Execute planned mutations with post-verify and save |
| execute_code | Run C# in Editor |
| resolve_scene_refs | Resolve $alias, /path, t:Type refs to scene paths |
| scene_change_plan | Pre-flight gate + checkpoint before scene edits |
| verify_after_change | 5-gate pipeline: compile → errors → console → tests → playtests |

## Non-Core TIER1 Tools (32 — always visible)

Together with CORE (15), these make the 47 always-visible TIER1 tools.

| Tool | Purpose | Category |
| ------ | --------- | ---------- |
| delete_object | Remove GameObject | SCENE |
| set_parent | Reparent GameObject | SCENE |
| set_active | Toggle active flag | SCENE |
| scene | List/load/save scenes | SCENE |
| search_scene | Find objects by pattern | SCENE |
| configure_objects | Batch configure components | SCENE |
| setup_objects | Batch create + wire objects | SCENE |
| screenshot | Capture frame | MEDIA |
| get_console_since | Console entries after a watermark | RUNTIME |
| console_mark | Create timestamp watermark for log slicing | RUNTIME |
| await_compile | Block until compile done | VERIFY |
| compile_preflight | Check compile readiness | VERIFY |
| validate_references | Check all refs valid | VERIFY |
| lint_scene_refs | 3-pass linter for scene refs in DSL/batch commands | VERIFY |
| run_tests | Low-level nonblocking durable NUnit dispatch | TESTS |
| run_tests_wait | Preferred interactive correlated NUnit runner | TESTS |
| run_playtest | Run playtest DSL script | TESTS |
| lint_playtest | Static DSL preflight — no runtime needed | TESTS |
| get_test_results | Legacy diagnostic result facade; not a verdict | TESTS |
| get_test_run | Read one exact durable test-run snapshot | TESTS |
| resolve_test_request | Resolve a lost start ACK by request identity | TESTS |
| discover_tools | Browse or enable a category | SYSTEM |
| mcp_status | Compact scene/compile/play-mode/alias status snapshot | SYSTEM |
| alias_status | Returns alias cache state (loaded/count/source/stale) | SYSTEM |
| release_smoke | Run status + aliases + compile gates in one call | SYSTEM |
| ask | Query LLM about scene | SYSTEM |
| ask_user | Prompt human | SYSTEM |
| permission_prompt | Gate sensitive ops | SYSTEM |
| reconnect_unity | Reconnect TCP socket | SYSTEM |
| resolve_tool_schema | Deferred schema fetch | SYSTEM |
| sync_unity | Reload and restart | SYSTEM |
| undo_last | Revert last N editor operations | SYSTEM |

---

## Tier2 Categories (require `discover_tools(category)`)

### SCENE (30 tools)

Scene manipulation beyond CORE/TIER1 basics.

| Tool | Purpose |
| ------ | --------- |
| find_objects | Search objects by query |
| get_object_detail | Detailed object state |
| get_components_list | List components on object |
| get_selection | Current editor selection |
| get_unity_events | Returns all UnityEvent fields on a component with target paths |
| get_spatial_context | Proximity query |
| set_properties | Batch set properties |
| set_material | Assign material to object |
| set_property_delta | Relative property change |
| set_sibling_index | Change sibling order |
| set_active | (also TIER1) Toggle active flag |
| rename_object | Rename GameObject, returns new path |
| object_diff | Compare two objects |
| transfer_object | Move object between scenes |
| ping_object | Flash object in hierarchy |
| autofit_collider | Auto-fit collider bounds |
| check_colliders | Collision layer conflicts |
| spatial_query | Radial/box search + filter |
| region_clear | Clear region of GameObjects |
| navmesh_query | Pathfinding query |
| scene_environment | Get/set scene lighting/environment |
| scene_diff | Compare the current hierarchy with the previous `scene_diff()` snapshot |

### COMPONENTS (4 tools)

Component event wiring.

| Tool | Purpose |
| ------ | --------- |
| wire_event | Connect event to method |
| unwire_event | Disconnect event listener |
| auto_wire | Auto-wire compatible fields by type |
| references | Find asset references |

### ASSETS (9 tools)

Asset database: import/export, prefab, ScriptableObject, project settings.

| Tool | Purpose |
| ------ | --------- |
| asset | Asset DB operations |
| prefab | Prefab lifecycle |
| scriptable_object | ScriptableObject create/read/write |
| project_settings | Project config |
| shader | Find shader, list properties |
| material | Assign/inspect material |
| material_audit | Audit material usage and performance |
| bake | Lighting and occlusion bake operations |
| package | PackageManager operations (list/search/add/remove) |

### MEDIA (14 tools)

Visual output, UI, animations, VFX, rendering analysis.

| Tool | Purpose |
| ------ | --------- |
| screenshot_baseline | Save baseline for regression |
| screenshot_compare | Diff baseline ↔ current |
| animation | Play clip on Animator |
| timeline | Control Timeline |
| animator | Get/set Animator parameters |
| particle | Emit/stop particles |
| create_ui | Spawn UI elements |
| set_rect | Modify RectTransform |
| validate_layout | Check UI constraints |
| ui_intent | AI ui description → components |
| vfx_intent | AI vfx description → settings |
| render_analyze | Rendering bottleneck analysis (9 actions) |
| analyze_lod_culling | LOD and culling audit |

### VERIFY (10 tools)

Compile, references, lint, scene audit.

| Tool | Purpose |
| ------ | --------- |
| scan_scene | Audit for issues |
| scene_health | Comprehensive scene audit |
| diagnose | Deep troubleshooting |
| serialized_field_rename_audit | Scan prefabs/scenes/SOs for stale YAML after field rename |

*(Plus 6 in TIER1: await_compile, compile_preflight, validate_references, lint_scene_refs, resolve_scene_refs, verify_after_change)*

### RUNTIME (18 tools)

Play Mode operations, performance, debugging, watches.

| Tool | Purpose |
| ------ | --------- |
| invoke_method | Call method at runtime |
| set_runtime_property | Set field/property at runtime |
| wait_until | Busy-wait on condition |
| move_to | Pathfind + walk to position |
| query_state | Read runtime GameObject state |
| get_frame_stats | Instant performance snapshot |
| profile | CPU/GPU profiler control |
| get_memory | Memory profiling |
| debug_animator | Animator state inspection |
| debug_physics | Physics debugger |
| debug | Session debugger |
| snapshot | Take memory snapshot |
| watch | Watch expression lifecycle |
| get_watches | Retrieve all watches |
| get_metrics | Profiling metrics |
| runtime_snapshot | Take runtime memory snapshot |

*(Plus console_mark + get_console_since in TIER1)*

### TESTS (11 tools)

NUnit, playtest suites, alias sync.

| Tool | Purpose |
| ------ | --------- |
| run_playtest_suite | Run multiple .playtest files sequentially |
| test_step | Execute single DSL step |
| lint_playtest_suite | Lint all matched .playtest files; aggregated report |
| validate_playtest_aliases | Diff .defs text file vs PlaytestConfig.asset |
| sync_playtest_aliases_from_defs | Import .defs → overwrite PlaytestConfig.asset aliases |
| export_playtest_aliases_to_defs | Export PlaytestConfig.asset aliases → .defs text file |
| get_test_count | Count available NUnit tests |
| cancel_test_run | Cancel one exact durable run |
| list_test_runs | List recent durable runs |
| get_test_progress | Legacy diagnostic progress facade |

*(Plus run_tests, run_tests_wait, run_playtest, lint_playtest, get_test_results, get_test_run, resolve_test_request in TIER1)*

### SYSTEM (37 tools)

Meta, session skills, templates, config, code tools.

| Tool | Purpose |
| ------ | --------- |
| save_skill | Store reusable C# or batch |
| use_skill | Execute saved skill |
| list_skills | Show all skills + usage |
| save_template | Store scene template |
| apply_template | Instantiate template |
| list_templates | Show all templates |
| fingerprint | Hash scene state |
| get_changes | Log editor events since last call |
| save_session | Snapshot hierarchy to .claude/session-context.json |
| load_session | Show the saved and current session hierarchies |
| recompile | Force script compilation |
| get_schema | Inspect class/type schema |
| auto_fix | Apply code fix suggestion |
| smart_build | Rebuild affected assemblies |
| build | Player builder (async, multiplatform) |
| checkpoint | Save named revision |
| menu | Execute Editor menu item |
| get_capabilities | List all registered C# commands |
| budget_status | Token usage tracking |
| set_llm_config | Store LLM settings |
| list_connections | Show connection status with semantic states |
| doctor | Health diagnostics |
| get_enabled_tools | List visible tools |
| animator_intent | AI animator description → controller |
| do | Execute arbitrary code via AI intent (direct_only — cannot use in batch) |

*(Plus alias_status, ask, ask_user, discover_tools, execute_code, mcp_status, permission_prompt, reconnect_unity, release_smoke, resolve_tool_schema, sync_unity, undo_last in TIER1)*

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
