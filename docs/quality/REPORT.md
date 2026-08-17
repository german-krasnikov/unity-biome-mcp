# Quality Report

> Auto-generated on **2026-08-17** from commit `0f56eb6` (v1.41.0)

## Project Overview

| Metric | Value |
|--------|-------|
| Version | v1.41.0 |
| Commit | `0f56eb6` |
| Date | 2026-08-17 |
| MCP Tools | 160 |

## Test Results

| Suite | Passed | Failed | Skipped | Total | Status |
|-------|--------|--------|---------|-------|--------|
| Python Server (3.12) | 5746 | 0 | 3 | 5749 | ✅ |
| Python Install | 76 | 0 | 0 | 76 | ✅ |
| Python Scripts | 939 | 0 | 0 | 939 | ✅ |
| C# EditMode (Linux) | 8188 | 0 | 178 | 8366 | ✅ |
| C# EditMode (Windows) | 8074 | 0 | 292 | 8366 | ✅ |
| C# EditMode (macOS) | 8188 | 0 | 178 | 8366 | ✅ |

## Tool Quality

| Linter | Errors | Warnings | Score |
|--------|--------|----------|-------|
| mcp-tool-card-linter | 4 | 412 | 83.44/100 |

### Per-Tool Scores

<details>
<summary>160 tools scored (click to expand)</summary>

| Tool | Score | Errors | Warnings | Risk |
|------|-------|--------|----------|------|
| `shader` | 59 | 0 | 4 | high |
| `animator` | 60 | 0 | 4 | high |
| `timeline` | 64 | 0 | 4 | medium |
| `verify_after_change` | 64 | 0 | 5 | high |
| `create_ui` | 65 | 0 | 5 | medium |
| `screenshot` | 65 | 0 | 7 | medium |
| `uitk_file` | 67 | 0 | 5 | high |
| `execute_code` | 68 | 1 | 3 | high |
| `run_tests_wait` | 68 | 0 | 6 | high |
| `screenshot_compare` | 68 | 0 | 5 | medium |
| `watch` | 68 | 0 | 4 | high |
| `lint_playtest` | 69 | 1 | 3 | high |
| `profile` | 69 | 0 | 4 | low |
| `wait_until` | 69 | 0 | 5 | medium |
| `navmesh_query` | 70 | 0 | 4 | high |
| `animation` | 71 | 0 | 3 | medium |
| `test_step` | 71 | 0 | 4 | medium |
| `set_rect` | 72 | 0 | 4 | medium |
| `run_playtest_suite` | 73 | 0 | 4 | high |
| `sync_unity` | 73 | 0 | 5 | medium |
| `asset` | 74 | 0 | 3 | high |
| `run_tests` | 74 | 0 | 5 | medium |
| `set_sibling_index` | 74 | 0 | 5 | medium |
| `vfx_intent` | 74 | 0 | 4 | medium |
| `run_playtest` | 75 | 0 | 5 | high |
| `save_skill` | 75 | 1 | 1 | medium |
| `screenshot_baseline` | 75 | 0 | 4 | medium |
| `set_property_delta` | 75 | 0 | 4 | medium |
| `attach_uitk` | 76 | 0 | 4 | high |
| `particle` | 76 | 0 | 4 | medium |
| `permission_prompt` | 76 | 0 | 4 | low |
| `set_material` | 76 | 0 | 4 | medium |
| `set_property` | 76 | 0 | 4 | medium |
| `get_console` | 77 | 0 | 4 | low |
| `manage_component` | 77 | 0 | 4 | high |
| `save_template` | 77 | 1 | 1 | medium |
| `set_properties` | 77 | 0 | 4 | medium |
| `wire_event` | 77 | 0 | 2 | high |
| `checkpoint` | 78 | 0 | 4 | medium |
| `checkpoint_create` | 78 | 0 | 4 | medium |
| `material` | 78 | 0 | 3 | medium |
| `references` | 78 | 0 | 3 | medium |
| `doctor` | 79 | 0 | 4 | medium |
| `get_changes` | 79 | 0 | 4 | medium |
| `set_active` | 79 | 0 | 4 | medium |
| `set_parent` | 79 | 0 | 4 | medium |
| `uitk_intent` | 79 | 0 | 3 | high |
| `debug` | 80 | 0 | 3 | medium |
| `uitk_element` | 80 | 0 | 3 | high |
| `animator_intent` | 81 | 0 | 3 | medium |
| `move_to` | 81 | 0 | 3 | medium |
| `object_diff` | 81 | 0 | 3 | medium |
| `prefab` | 81 | 0 | 2 | high |
| `scene_change_plan` | 81 | 0 | 3 | medium |
| `autofit_collider` | 82 | 0 | 3 | medium |
| `brief_build` | 82 | 0 | 3 | high |
| `get_metrics` | 82 | 0 | 3 | high |
| `region_clear` | 82 | 0 | 3 | high |
| `ui_intent` | 82 | 0 | 3 | medium |
| `validate_references` | 82 | 0 | 3 | medium |
| `batch` | 83 | 0 | 3 | high |
| `cancel_test_run` | 83 | 0 | 3 | medium |
| `debug_physics` | 83 | 0 | 3 | medium |
| `do` | 83 | 0 | 3 | medium |
| `get_component` | 83 | 0 | 3 | medium |
| `get_spatial_context` | 83 | 0 | 3 | medium |
| `invoke_method` | 83 | 0 | 2 | medium |
| `reconnect_unity` | 83 | 0 | 3 | low |
| `serialized_field_rename_audit` | 83 | 0 | 2 | low |
| `set_llm_config` | 83 | 0 | 3 | medium |
| `setup_objects` | 83 | 0 | 3 | medium |
| `undo_last` | 83 | 0 | 3 | medium |
| `use_skill` | 83 | 0 | 3 | medium |
| `validate_triggers` | 83 | 0 | 3 | medium |
| `apply_scene_change` | 84 | 0 | 2 | high |
| `create_object` | 84 | 0 | 3 | medium |
| `get_console_since` | 84 | 0 | 2 | low |
| `menu` | 84 | 0 | 3 | medium |
| `scene_environment` | 84 | 0 | 3 | medium |
| `scriptable_object` | 84 | 0 | 3 | medium |
| `await_compile` | 85 | 0 | 3 | low |
| `discover_tools` | 85 | 0 | 2 | low |
| `export_playtest_aliases_to_defs` | 85 | 0 | 2 | medium |
| `fingerprint` | 85 | 0 | 3 | medium |
| `get_hierarchy` | 85 | 0 | 3 | high |
| `list_test_runs` | 85 | 0 | 3 | low |
| `recompile` | 85 | 0 | 3 | medium |
| `snapshot` | 85 | 0 | 2 | medium |
| `spatial_query` | 85 | 0 | 1 | medium |
| `sync_playtest_aliases_from_defs` | 85 | 0 | 2 | medium |
| `transfer_object` | 85 | 0 | 2 | medium |
| `compile_preflight` | 86 | 0 | 2 | medium |
| `list_events` | 86 | 0 | 2 | medium |
| `project_settings` | 86 | 0 | 2 | high |
| `unwire_event` | 86 | 0 | 2 | high |
| `validate_playtest_aliases` | 86 | 0 | 2 | medium |
| `diagnose` | 87 | 0 | 2 | low |
| `editor` | 87 | 0 | 2 | medium |
| `inspect_uitk` | 87 | 0 | 2 | medium |
| `rename_object` | 87 | 0 | 2 | medium |
| `console_mark` | 88 | 0 | 2 | low |
| `find_objects` | 88 | 0 | 2 | medium |
| `get_frame_stats` | 88 | 0 | 2 | low |
| `get_memory` | 88 | 0 | 2 | low |
| `lint_playtest_suite` | 88 | 0 | 2 | medium |
| `material_audit` | 88 | 0 | 2 | low |
| `package` | 88 | 0 | 2 | high |
| `ping_object` | 88 | 0 | 2 | medium |
| `scene` | 88 | 0 | 2 | medium |
| `scene_health` | 88 | 0 | 2 | low |
| `analyze_lod_culling` | 89 | 0 | 2 | low |
| `auto_wire` | 89 | 0 | 2 | high |
| `debug_animator` | 89 | 0 | 2 | medium |
| `get_schema` | 89 | 0 | 2 | low |
| `get_test_progress` | 89 | 0 | 2 | low |
| `get_test_results` | 89 | 0 | 2 | low |
| `lint_scene_refs` | 89 | 0 | 2 | high |
| `lint_uitk` | 89 | 0 | 2 | medium |
| `runtime_snapshot` | 89 | 0 | 2 | low |
| `search_scene` | 89 | 0 | 2 | medium |
| `build` | 90 | 0 | 1 | medium |
| `check_colliders` | 90 | 0 | 2 | medium |
| `delete_object` | 90 | 0 | 2 | high |
| `get_components_list` | 90 | 0 | 2 | low |
| `get_object_detail` | 90 | 0 | 2 | low |
| `get_unity_events` | 90 | 0 | 2 | medium |
| `inspect` | 90 | 0 | 2 | low |
| `lint_ugui` | 90 | 0 | 2 | medium |
| `render_analyze` | 90 | 0 | 1 | medium |
| `checkpoint_restore` | 91 | 0 | 1 | medium |
| `apply_template` | 92 | 0 | 1 | medium |
| `bake` | 92 | 0 | 1 | low |
| `configure_objects` | 92 | 0 | 1 | medium |
| `resolve_tool_schema` | 92 | 0 | 1 | low |
| `smart_build` | 92 | 0 | 1 | low |
| `ask` | 93 | 0 | 1 | low |
| `ask_user` | 93 | 0 | 1 | low |
| `get_test_run` | 93 | 0 | 1 | low |
| `query_state` | 93 | 0 | 1 | medium |
| `resolve_scene_refs` | 93 | 0 | 1 | medium |
| `resolve_test_request` | 93 | 0 | 1 | low |
| `save_session` | 94 | 0 | 1 | low |
| `alias_status` | 95 | 0 | 1 | low |
| `auto_fix` | 95 | 0 | 1 | low |
| `budget_status` | 95 | 0 | 1 | low |
| `get_capabilities` | 95 | 0 | 1 | low |
| `get_changeset` | 95 | 0 | 1 | low |
| `get_compile_errors` | 95 | 0 | 1 | medium |
| `get_enabled_tools` | 95 | 0 | 1 | low |
| `get_selection` | 95 | 0 | 1 | medium |
| `get_test_count` | 95 | 0 | 1 | low |
| `get_watches` | 95 | 0 | 1 | low |
| `list_connections` | 95 | 0 | 1 | low |
| `list_skills` | 95 | 0 | 1 | low |
| `list_templates` | 95 | 0 | 1 | low |
| `load_session` | 95 | 0 | 1 | low |
| `mcp_status` | 95 | 0 | 1 | low |
| `release_smoke` | 95 | 0 | 1 | low |
| `scan_scene` | 95 | 0 | 1 | low |
| `scene_diff` | 95 | 0 | 1 | low |

</details>
