// Generated from dsl/tools.biome
// source-sha256=fb0ae2e48afd8305810f08991a9eb3150492943f27c500c18e93ef755b12c6ea
// Edit the source, then run biome generate.

namespace UnityMCP.Editor
{
    internal static partial class ToolBindings
    {
        internal static void RegisterAll()
        {
           CommandRegistry.Register("alias_status", CommandRouter.ExecAliasStatus, ToolMeta.Get("alias_status"));
           CommandRegistry.Register("analyze_lod_culling", CommandRouter.ExecAnalyzeLodCulling, ToolMeta.Get("analyze_lod_culling"));
           CommandRegistry.Register("animator", CommandRouter.ExecAnimatorConsolidated, ToolMeta.Get("animator"));
           CommandRegistry.Register("attach_uitk", CommandRouter.ExecAttachUITK, ToolMeta.Get("attach_uitk"));
           CommandRegistry.Register("auto_wire", CommandRouter.ExecAutoWire, ToolMeta.Get("auto_wire"));
           CommandRegistry.Register("autofit_collider", ColliderFitHelper.Execute, ToolMeta.Get("autofit_collider"));
           CommandRegistry.Register("bake", CommandRouter.ExecBake, ToolMeta.Get("bake"));
           CommandRegistry.RegisterAsync("build", CommandRouter.AsyncBuild, ToolMeta.Get("build"));
           CommandRegistry.Register("cancel_test_run", CommandRouter.ExecCancelTestRun, ToolMeta.Get("cancel_test_run"));
           CommandRegistry.Register("check_colliders", CommandRouter.ExecCheckColliders, ToolMeta.Get("check_colliders"));
           CommandRegistry.Register("checkpoint", CommandRouter.ExecCheckpoint, ToolMeta.Get("checkpoint"));
           CommandRegistry.Register("compile_preflight", CommandRouter.ExecCompilePreflight, ToolMeta.Get("compile_preflight"));
           CommandRegistry.Register("compile_status", CommandRouter.ExecCompileStatus, ToolMeta.Get("compile_status"));
           CommandRegistry.Register("create_object", CommandRouter.ExecCreateObject, ToolMeta.Get("create_object"));
           CommandRegistry.Register("create_ui", CommandRouter.ExecCreateUI, ToolMeta.Get("create_ui"));
           CommandRegistry.Register("debug_animator", CommandRouter.ExecDebugAnimator, ToolMeta.Get("debug_animator"));
           CommandRegistry.Register("debug_physics", CommandRouter.ExecDebugPhysics, ToolMeta.Get("debug_physics"));
           CommandRegistry.Register("delete_object", CommandRouter.ExecDeleteObject, ToolMeta.Get("delete_object"));
           CommandRegistry.Register("editor", CommandRouter.ExecEditor, ToolMeta.Get("editor"));
           CommandRegistry.Register("execute_code", CommandRouter.ExecExecuteCode, ToolMeta.Get("execute_code"));
           CommandRegistry.Register("export_playtest_aliases_to_defs", CommandRouter.ExecExportToDefs, ToolMeta.Get("export_playtest_aliases_to_defs"));
           CommandRegistry.Register("find_objects", CommandRouter.ExecFindObjects, ToolMeta.Get("find_objects"));
           CommandRegistry.Register("fingerprint", CommandRouter.ExecFingerprint, ToolMeta.Get("fingerprint"));
           CommandRegistry.Register("get_aliases", CommandRouter.ExecGetAliases, ToolMeta.Get("get_aliases"));
           CommandRegistry.Register("get_capabilities", CommandRouter.ExecGetCapabilities, ToolMeta.Get("get_capabilities"));
           CommandRegistry.Register("get_changes", CommandRouter.ExecGetChanges, ToolMeta.Get("get_changes"));
           CommandRegistry.Register("get_compile_errors", CommandRouter.ExecGetCompileErrors, ToolMeta.Get("get_compile_errors"));
           CommandRegistry.Register("get_components_list", CommandRouter.ExecGetComponentsList, ToolMeta.Get("get_components_list"));
           CommandRegistry.Register("get_console", CommandRouter.ExecGetConsole, ToolMeta.Get("get_console"));
           CommandRegistry.Register("get_enabled_tools", CommandRouter.ExecGetEnabledToolsCommand, ToolMeta.Get("get_enabled_tools"));
           CommandRegistry.Register("get_memory", CommandRouter.ExecGetMemory, ToolMeta.Get("get_memory"));
           CommandRegistry.Register("get_selection", CommandRouter.ExecGetSelection, ToolMeta.Get("get_selection"));
           CommandRegistry.Register("get_spatial_context", CommandRouter.ExecGetSpatialContext, ToolMeta.Get("get_spatial_context"));
           CommandRegistry.Register("get_status", CommandRouter.ExecGetStatus, ToolMeta.Get("get_status"));
           CommandRegistry.Register("get_test_count", CommandRouter.ExecGetTestCount, ToolMeta.Get("get_test_count"));
           CommandRegistry.Register("get_test_progress", CommandRouter.ExecGetTestProgress, ToolMeta.Get("get_test_progress"));
           CommandRegistry.Register("get_test_results", CommandRouter.ExecGetTestResults, ToolMeta.Get("get_test_results"));
           CommandRegistry.Register("get_unity_events", CommandRouter.ExecGetUnityEvents, ToolMeta.Get("get_unity_events"));
           CommandRegistry.Register("invoke_method", CommandRouter.ExecInvokeMethod, ToolMeta.Get("invoke_method"));
           CommandRegistry.Register("lint_playtest", CommandRouter.ExecLintPlaytest, ToolMeta.Get("lint_playtest"));
           CommandRegistry.Register("lint_scene_refs", CommandRouter.ExecLintSceneRefs, ToolMeta.Get("lint_scene_refs"));
           CommandRegistry.Register("lint_ugui", CommandRouter.ExecLintUGUI, ToolMeta.Get("lint_ugui"));
           CommandRegistry.Register("lint_uitk", CommandRouter.ExecLintUITK, ToolMeta.Get("lint_uitk"));
           CommandRegistry.Register("list_events", CommandRouter.ExecListEvents, ToolMeta.Get("list_events"));
           CommandRegistry.Register("list_test_runs", CommandRouter.ExecListTestRuns, ToolMeta.Get("list_test_runs"));
           CommandRegistry.Register("manage_component", CommandRouter.ExecManageComponent, ToolMeta.Get("manage_component"));
           CommandRegistry.RegisterAction("material", MaterialHelper.Execute, ToolMeta.Get("material"));
           CommandRegistry.Register("material_audit", CommandRouter.ExecMaterialAudit, ToolMeta.Get("material_audit"));
           CommandRegistry.Register("menu", CommandRouter.ExecMenu, ToolMeta.Get("menu"));
           CommandRegistry.RegisterAsync("move_to", CommandRouter.AsyncMoveTo, ToolMeta.Get("move_to"));
           CommandRegistry.Register("object_diff", CommandRouter.ExecObjectDiff, ToolMeta.Get("object_diff"));
           CommandRegistry.RegisterAsync("package", CommandRouter.AsyncPackage, ToolMeta.Get("package"));
           CommandRegistry.Register("particle", CommandRouter.ExecParticleConsolidated, ToolMeta.Get("particle"));
           CommandRegistry.Register("ping", CommandRouter.ExecPing, ToolMeta.Get("ping"));
           CommandRegistry.Register("ping_object", CommandRouter.ExecPingObject, ToolMeta.Get("ping_object"));
           CommandRegistry.RegisterAction("project_settings", ProjectSettingsHelper.Execute, ToolMeta.Get("project_settings"));
           CommandRegistry.Register("query_state", CommandRouter.ExecQueryState, ToolMeta.Get("query_state"));
           CommandRegistry.Register("recompile", CommandRouter.ExecRecompile, ToolMeta.Get("recompile"));
           CommandRegistry.Register("region_clear", CommandRouter.ExecRegionClear, ToolMeta.Get("region_clear"));
           CommandRegistry.Register("rename_object", CommandRouter.ExecRenameObject, ToolMeta.Get("rename_object"));
           CommandRegistry.Register("resolve_scene_refs", CommandRouter.ExecResolveSceneRefs, ToolMeta.Get("resolve_scene_refs"));
           CommandRegistry.Register("resolve_test_request", CommandRouter.ExecResolveTestRequest, ToolMeta.Get("resolve_test_request"));
           CommandRegistry.Register("runtime_snapshot", CommandRouter.ExecRuntimeSnapshot, ToolMeta.Get("runtime_snapshot"));
           CommandRegistry.Register("scan_scene", CommandRouter.ExecScanScene, ToolMeta.Get("scan_scene"));
           CommandRegistry.Register("scene", CommandRouter.ExecScene, ToolMeta.Get("scene"));
           CommandRegistry.Register("scene_diff", CommandRouter.ExecSceneDiff, ToolMeta.Get("scene_diff"));
           CommandRegistry.Register("scene_health", CommandRouter.ExecSceneHealth, ToolMeta.Get("scene_health"));
           CommandRegistry.RegisterAction("scriptable_object", ScriptableObjectHelper.Execute, ToolMeta.Get("scriptable_object"));
           CommandRegistry.Register("search_scene", CommandRouter.ExecSearchScene, ToolMeta.Get("search_scene"));
           CommandRegistry.Register("serialized_field_rename_audit", CommandRouter.ExecSerializedFieldRenameAudit, ToolMeta.Get("serialized_field_rename_audit"));
           CommandRegistry.Register("set_active", CommandRouter.ExecSetActive, ToolMeta.Get("set_active"));
           CommandRegistry.Register("set_parent", CommandRouter.ExecSetParent, ToolMeta.Get("set_parent"));
           CommandRegistry.Register("set_property_delta", CommandRouter.ExecSetPropertyDelta, ToolMeta.Get("set_property_delta"));
           CommandRegistry.Register("set_rect", CommandRouter.ExecSetRect, ToolMeta.Get("set_rect"));
           CommandRegistry.Register("set_sibling_index", CommandRouter.ExecSetSiblingIndex, ToolMeta.Get("set_sibling_index"));
           CommandRegistry.Register("spatial_query", CommandRouter.ExecSpatialQuery, ToolMeta.Get("spatial_query"));
           CommandRegistry.Register("sync_playtest_aliases_from_defs", CommandRouter.ExecSyncFromDefs, ToolMeta.Get("sync_playtest_aliases_from_defs"));
           CommandRegistry.RegisterAsync("test_step", CommandRouter.AsyncTestStep, ToolMeta.Get("test_step"));
           CommandRegistry.Register("transfer_object", CommandRouter.ExecTransferObject, ToolMeta.Get("transfer_object"));
           CommandRegistry.Register("uitk_element", CommandRouter.ExecUitkElement, ToolMeta.Get("uitk_element"));
           CommandRegistry.Register("undo_last", CommandRouter.ExecUndoLast, ToolMeta.Get("undo_last"));
           CommandRegistry.Register("unwire_event", CommandRouter.ExecUnwireEvent, ToolMeta.Get("unwire_event"));
           CommandRegistry.Register("validate_references", CommandRouter.ExecValidateReferences, ToolMeta.Get("validate_references"));
           CommandRegistry.Register("validate_triggers", CommandRouter.ExecValidateTriggers, ToolMeta.Get("validate_triggers"));
           CommandRegistry.RegisterAsync("wait_until", CommandRouter.AsyncWaitUntil, ToolMeta.Get("wait_until"));
           CommandRegistry.Register("wire_event", CommandRouter.ExecWireEvent, ToolMeta.Get("wire_event"));
        }
    }
}
