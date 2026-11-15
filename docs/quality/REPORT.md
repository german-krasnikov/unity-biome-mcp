# Quality Report

> Auto-generated on **2026-11-15** from commit `dd564da` (v3.0.0)

## Project Overview

| Metric | Value |
|--------|-------|
| Version | v3.0.0 |
| Commit | `dd564da` |
| Date | 2026-11-15 |
| MCP Tools | 160 |

## Test Results

| Suite | Passed | Failed | Skipped | Total | Status |
|-------|--------|--------|---------|-------|--------|
| Python Server (3.14) | 7186 | 0 | 3 | 7189 | ✅ |
| Python Install | 93 | 0 | 0 | 93 | ✅ |
| Python Scripts | 1607 | 0 | 1 | 1608 | ✅ |
| C# EditMode (Linux) | 9479 | 0 | 188 | 9667 | ✅ |
| C# EditMode (Windows) | 9364 | 0 | 303 | 9667 | ✅ |
| C# EditMode (macOS) | 9479 | 0 | 188 | 9667 | ✅ |

## Tool Quality

| Linter | Errors | Warnings | Score |
|--------|--------|----------|-------|
| mcp-tool-card-linter | 0 | 434 | 82.69/100 |

### Per-Tool Scores

<details>
<summary>160 tools scored (click to expand)</summary>

| Tool | Score | Errors | Warnings | Risk |
|------|-------|--------|----------|------|
| `animator` | 55 | 0 | 5 | high |
| `verify_after_change` | 57 | 0 | 6 | high |
| `shader` | 59 | 0 | 4 | high |
| `run_playtest_suite` | 61 | 0 | 6 | high |
| `create_ui` | 65 | 0 | 5 | medium |
| `screenshot` | 65 | 0 | 7 | medium |
| `uitk_file` | 67 | 0 | 5 | high |
| `run_tests_wait` | 68 | 0 | 6 | high |
| `screenshot_compare` | 68 | 0 | 5 | medium |
| `watch` | 68 | 0 | 4 | high |
| `profile` | 69 | 0 | 4 | low |
| `timeline` | 69 | 0 | 3 | medium |
| `wait_until` | 69 | 0 | 5 | medium |
| `animation` | 70 | 0 | 3 | medium |
| `navmesh_query` | 70 | 0 | 4 | high |
| `test_step` | 71 | 0 | 4 | medium |
| `set_property` | 72 | 0 | 5 | medium |
| `set_rect` | 72 | 0 | 4 | medium |
| `run_playtest` | 73 | 0 | 5 | high |
| `validate_triggers` | 73 | 0 | 5 | medium |
| `run_tests` | 74 | 0 | 5 | medium |
| `set_sibling_index` | 74 | 0 | 5 | medium |
| `vfx_intent` | 74 | 0 | 4 | medium |
| `get_hierarchy` | 75 | 0 | 5 | high |
| `screenshot_baseline` | 75 | 0 | 4 | medium |
| `set_property_delta` | 75 | 0 | 4 | medium |
| `attach_uitk` | 76 | 0 | 4 | high |
| `get_console` | 76 | 0 | 4 | low |
| `particle` | 76 | 0 | 4 | medium |
| `set_material` | 76 | 0 | 4 | medium |
| `editor` | 77 | 0 | 4 | medium |
| `manage_component` | 77 | 0 | 4 | high |
| `set_properties` | 77 | 0 | 4 | medium |
| `wire_event` | 77 | 0 | 2 | high |
| `checkpoint` | 78 | 0 | 4 | medium |
| `checkpoint_create` | 78 | 0 | 4 | medium |
| `console_mark` | 78 | 0 | 4 | medium |
| `material` | 78 | 0 | 3 | medium |
| `references` | 78 | 0 | 3 | medium |
| `asset` | 79 | 0 | 2 | high |
| `debug` | 79 | 0 | 3 | medium |
| `doctor` | 79 | 0 | 4 | medium |
| `get_changes` | 79 | 0 | 4 | medium |
| `get_test_progress` | 79 | 0 | 4 | medium |
| `get_test_results` | 79 | 0 | 4 | medium |
| `set_active` | 79 | 0 | 4 | medium |
| `set_parent` | 79 | 0 | 4 | medium |
| `uitk_intent` | 79 | 0 | 3 | high |
| `object_diff` | 80 | 0 | 3 | medium |
| `permission_prompt` | 80 | 0 | 3 | low |
| `prefab` | 80 | 0 | 2 | high |
| `render_analyze` | 80 | 0 | 3 | medium |
| `uitk_element` | 80 | 0 | 3 | high |
| `animator_intent` | 81 | 0 | 3 | medium |
| `brief_build` | 81 | 0 | 3 | high |
| `move_to` | 81 | 0 | 3 | medium |
| `scene_change_plan` | 81 | 0 | 3 | medium |
| `unwire_event` | 81 | 0 | 3 | high |
| `validate_references` | 81 | 0 | 3 | medium |
| `autofit_collider` | 82 | 0 | 3 | medium |
| `debug_physics` | 82 | 0 | 3 | medium |
| `get_component` | 82 | 0 | 3 | medium |
| `get_metrics` | 82 | 0 | 3 | high |
| `get_spatial_context` | 82 | 0 | 3 | medium |
| `region_clear` | 82 | 0 | 3 | high |
| `serialized_field_rename_audit` | 82 | 0 | 2 | low |
| `sync_unity` | 82 | 0 | 3 | medium |
| `ui_intent` | 82 | 0 | 3 | medium |
| `await_compile` | 83 | 0 | 3 | low |
| `batch` | 83 | 0 | 3 | high |
| `cancel_test_run` | 83 | 0 | 3 | medium |
| `do` | 83 | 0 | 3 | medium |
| `execute_code` | 83 | 0 | 3 | high |
| `get_console_since` | 83 | 0 | 2 | low |
| `get_test_run` | 83 | 0 | 3 | high |
| `invoke_method` | 83 | 0 | 2 | medium |
| `reconnect_unity` | 83 | 0 | 3 | low |
| `resolve_test_request` | 83 | 0 | 3 | medium |
| `set_llm_config` | 83 | 0 | 3 | medium |
| `setup_objects` | 83 | 0 | 3 | medium |
| `undo_last` | 83 | 0 | 3 | medium |
| `use_skill` | 83 | 0 | 3 | medium |
| `apply_scene_change` | 84 | 0 | 2 | high |
| `create_object` | 84 | 0 | 3 | medium |
| `fingerprint` | 84 | 0 | 3 | medium |
| `list_test_runs` | 84 | 0 | 3 | low |
| `menu` | 84 | 0 | 3 | medium |
| `scene_environment` | 84 | 0 | 3 | medium |
| `scriptable_object` | 84 | 0 | 3 | medium |
| `snapshot` | 84 | 0 | 2 | medium |
| `spatial_query` | 84 | 0 | 1 | medium |
| `compile_preflight` | 85 | 0 | 2 | medium |
| `delete_object` | 85 | 0 | 3 | high |
| `discover_tools` | 85 | 0 | 2 | low |
| `export_playtest_aliases_to_defs` | 85 | 0 | 2 | medium |
| `lint_playtest` | 85 | 0 | 3 | high |
| `list_events` | 85 | 0 | 2 | medium |
| `recompile` | 85 | 0 | 3 | medium |
| `release_smoke` | 85 | 0 | 3 | medium |
| `sync_playtest_aliases_from_defs` | 85 | 0 | 2 | medium |
| `transfer_object` | 85 | 0 | 2 | medium |
| `validate_playtest_aliases` | 85 | 0 | 2 | medium |
| `diagnose` | 86 | 0 | 2 | low |
| `inspect_uitk` | 86 | 0 | 2 | medium |
| `project_settings` | 86 | 0 | 2 | high |
| `find_objects` | 87 | 0 | 2 | medium |
| `get_frame_stats` | 87 | 0 | 2 | low |
| `get_memory` | 87 | 0 | 2 | low |
| `lint_playtest_suite` | 87 | 0 | 2 | medium |
| `material_audit` | 87 | 0 | 2 | low |
| `rename_object` | 87 | 0 | 2 | medium |
| `scene_health` | 87 | 0 | 2 | low |
| `analyze_lod_culling` | 88 | 0 | 2 | low |
| `debug_animator` | 88 | 0 | 2 | medium |
| `get_schema` | 88 | 0 | 2 | low |
| `lint_scene_refs` | 88 | 0 | 2 | high |
| `lint_uitk` | 88 | 0 | 2 | medium |
| `package` | 88 | 0 | 2 | high |
| `ping_object` | 88 | 0 | 2 | medium |
| `runtime_snapshot` | 88 | 0 | 2 | low |
| `scene` | 88 | 0 | 2 | medium |
| `search_scene` | 88 | 0 | 2 | medium |
| `auto_wire` | 89 | 0 | 2 | high |
| `check_colliders` | 89 | 0 | 2 | medium |
| `get_components_list` | 89 | 0 | 2 | low |
| `get_object_detail` | 89 | 0 | 2 | low |
| `get_unity_events` | 89 | 0 | 2 | medium |
| `inspect` | 89 | 0 | 2 | low |
| `lint_ugui` | 89 | 0 | 2 | medium |
| `build` | 90 | 0 | 1 | medium |
| `save_skill` | 90 | 0 | 1 | medium |
| `checkpoint_restore` | 91 | 0 | 1 | medium |
| `apply_template` | 92 | 0 | 1 | medium |
| `ask` | 92 | 0 | 1 | low |
| `ask_user` | 92 | 0 | 1 | low |
| `bake` | 92 | 0 | 1 | low |
| `configure_objects` | 92 | 0 | 1 | medium |
| `query_state` | 92 | 0 | 1 | medium |
| `resolve_scene_refs` | 92 | 0 | 1 | medium |
| `resolve_tool_schema` | 92 | 0 | 1 | low |
| `save_template` | 92 | 0 | 1 | medium |
| `smart_build` | 92 | 0 | 1 | low |
| `alias_status` | 94 | 0 | 1 | low |
| `auto_fix` | 94 | 0 | 1 | low |
| `budget_status` | 94 | 0 | 1 | low |
| `get_capabilities` | 94 | 0 | 1 | low |
| `get_changeset` | 94 | 0 | 1 | low |
| `get_compile_errors` | 94 | 0 | 1 | medium |
| `get_enabled_tools` | 94 | 0 | 1 | low |
| `get_selection` | 94 | 0 | 1 | medium |
| `get_test_count` | 94 | 0 | 1 | low |
| `get_watches` | 94 | 0 | 1 | low |
| `list_connections` | 94 | 0 | 1 | low |
| `list_skills` | 94 | 0 | 1 | low |
| `list_templates` | 94 | 0 | 1 | low |
| `load_session` | 94 | 0 | 1 | low |
| `mcp_status` | 94 | 0 | 1 | low |
| `save_session` | 94 | 0 | 1 | low |
| `scan_scene` | 94 | 0 | 1 | low |
| `scene_diff` | 94 | 0 | 1 | low |

</details>
