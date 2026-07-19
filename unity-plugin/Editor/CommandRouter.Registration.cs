using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
namespace UnityMCP.Editor
{
    // RegisterAll() split into 4 themed bucket methods (B1, review sprint v0.70).
    // The original RegisterAll() had grown into a ~340-line God Method wiring all 93 commands
    // inline. Split by guard-flag semantics (matches CheckGuards' own precedence order):
    // meta (always-allowed escape hatches) -> read (non-mutating) -> mutating -> async.
    // Every Register/RegisterAction/RegisterAsync call below is moved verbatim from the
    // original RegisterAll() — no flags, params, or handlers were changed by this split.
    public static partial class CommandRouter
    {
        internal static void RegisterMetaCommands()
        {
            // Meta (non-mutating) — always allowed + allowed during compile: these are the
            // escape-hatch/discovery commands that must work regardless of settings or wedge state.
            CommandRegistry.Register("ping", _ => "pong", required: "", optional: "",
                alwaysAllowed: true, allowedDuringCompile: true);
            // C7: "get_version" fast-path is in MCPServer.cs:293 (emits MVID stamp).
            // The VersionTracker delegation here was dead code — MCPServer intercepts get_version
            // before it reaches CommandRouter. Removed so get_version unambiguously means MVID stamp.
            CommandRegistry.Register("get_capabilities", _ => GetCapabilities(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("get_enabled_tools", _ => ExecGetEnabledTools(), required: "", optional: "",
                alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("get_disabled_tools", _ => ExecGetDisabledTools(), required: "", optional: "",
                alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("set_tool_catalog", args =>
            {
                var json = JsonHelper.ExtractString(args, "catalog");
                if (!string.IsNullOrEmpty(json)) MCPSettings.SetCatalog(json);
                return "ok";
            }, required: "", optional: "catalog", alwaysAllowed: true, allowedDuringCompile: true);
            // screenshot is intercepted in Process/ProcessAsync for file response formatting;
            // registered here only for IsRegistered/IsMutating queries
            CommandRegistry.Register("screenshot", _ => throw new InvalidOperationException("screenshot intercepted before ExecuteCommand"),
                required: "", optional: "width,height,camera,path,supersample,angles,zoom,offset,fixed_size,highlight,show_colliders,angle,annotation_id",
                fileHandler: BuildScreenshotResponse,
                specialDispatch: true, allowedDuringCompile: true);
            CommandRegistry.Register("diagnose", args => DiagnoseCommand.Execute(args),
                required: "", optional: "",  // C8: read-only multi-signal snapshot
                alwaysAllowed: true, allowedDuringCompile: true);
            // T5 recovery: play+stop cycle forces domain reload without going through SecurityScan.
            // allowedDuringCompile=true so it works in compile-latch state.
            // play→stop queued via delayCall; command returns immediately.
            CommandRegistry.Register("force_play_stop", _ =>
            {
                if (EditorApplication.isCompiling)
                {
                    EditorApplication.delayCall += () => {
                        EditorApplication.isPlaying = true;
                        EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
                    };
                    return "play_stop queued (waiting for compile)";
                }
                EditorApplication.isPlaying = true;
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
                return "play_stop triggered";
            }, required: "", optional: "", alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("set_client_label", args =>
            {
                var label = JsonHelper.ExtractString(args, "label");
                if (!string.IsNullOrEmpty(label))
                {
                    MCPServer._mainSlot.Label = label;
                    MainThreadDispatcher.Enqueue(() => Debug.Log($"[MCP] Client identified as: {label}"));
                }
                return "ok";
            }, required: "label", optional: "", alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("get_status", _ =>
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"scene={scene.name}");
                sb.AppendLine($"dirty={scene.isDirty}");
                sb.AppendLine($"playing={UnityEditor.EditorApplication.isPlaying}");
                sb.AppendLine($"compiling={UnityEditor.EditorApplication.isCompiling}");
                sb.AppendLine($"port={MCPServer.ServerPort}");
                sb.AppendLine($"aliases={AliasExpander.CountConfigAliases()}");
                return sb.ToString().TrimEnd();
            }, required: "", optional: "", alwaysAllowed: true, allowedDuringCompile: true);
        }

        internal static void RegisterReadCommands()
        {
            // Read (non-mutating)
            CommandRegistry.Register("get_hierarchy", ExecGetHierarchy,
                required: "", optional: "depth,root,filter,components,summary,incremental,scene,compress",
                maxResponseChars: 50000);
            CommandRegistry.Register("get_aliases", _ => GetAliasesText(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("list_playtest_files", args =>
            {
                var pattern = JsonHelper.ExtractString(args, "pattern") ?? "Playtests/*.playtest";
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var absDir = Path.GetFullPath(Path.Combine(projectRoot, Path.GetDirectoryName(pattern) ?? ""));
                if (!absDir.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && absDir != projectRoot)
                    return "err: path must be inside project";
                if (!Directory.Exists(absDir)) return "err: directory not found: " + absDir;
                var files = Directory.GetFiles(absDir, Path.GetFileName(pattern))
                    .Select(f => f.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar)
                                  .Replace(Path.DirectorySeparatorChar, '/'))
                    .OrderBy(f => f)
                    .ToArray();
                return files.Length == 0 ? "no files" : string.Join("\n", files);
            }, required: "", optional: "pattern", allowedDuringCompile: true);
            CommandRegistry.Register("lint_playtest", args =>
            {
                var path   = JsonHelper.ExtractString(args, "path");
                var script = JsonHelper.ExtractString(args, "script");
                if (path != null && script != null) return "err: use path or script, not both";
                string result;
                if (path != null)        result = PlaytestLinter.LintFile(path);
                else if (script != null) result = PlaytestLinter.LintScript(script);
                else return "err: path or script required";
                if (result.StartsWith("ERROR") || result.Contains("\nERROR"))
                    throw new InvalidOperationException(result);
                return result;
            }, required: "", optional: "path,script", allowedDuringCompile: true);
            CommandRegistry.Register("lint_scene_refs", args =>
            {
                var path    = JsonHelper.ExtractString(args, "path");
                var snippet = JsonHelper.ExtractString(args, "snippet");
                if (path != null && snippet != null) return "err: path and snippet are mutually exclusive";
                string script;
                if (path != null)
                {
                    var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
                    if (!File.Exists(fullPath)) return $"err: file not found: {path}";
                    script = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                }
                else if (snippet != null) script = snippet;
                else return "err: path or snippet required";
                var issues = SceneRefLinter.LintScript(script);
                return issues.Count == 0 ? "OK: no issues" : SceneRefLinter.FormatReport(path ?? "<snippet>", issues);
            }, required: "", optional: "path,snippet");
            CommandRegistry.Register("alias_status", ExecAliasStatus,
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("validate_playtest_aliases", ExecValidateAliases,
                required: "defs", optional: "asset", allowedDuringCompile: true);
            CommandRegistry.Register("sync_playtest_aliases_from_defs", ExecSyncFromDefs,
                required: "", optional: "defs,asset");
            CommandRegistry.Register("export_playtest_aliases_to_defs", ExecExportToDefs,
                required: "", optional: "asset,defs");
            CommandRegistry.Register("get_component", ExecGetComponent,
                required: "path,type", optional: "fields,compress");
            CommandRegistry.Register("get_components_list", ExecGetComponentsList,
                required: "id", optional: "");
            CommandRegistry.Register("get_object_detail", ExecGetObjectDetail,
                required: "id", optional: "");
            CommandRegistry.Register("find_objects", ExecFindObjects,
                required: "", optional: "name,tag,layer,component");
            CommandRegistry.Register("resolve_scene_refs", args =>
            {
                var refs = JsonHelper.ExtractString(args, "refs");
                if (string.IsNullOrEmpty(refs)) return "err: refs required";
                var results = SceneRefResolver.ResolveMany(refs, JsonHelper.ExtractString(args, "fields"));
                return SceneRefResolver.FormatResults(results);
            }, required: "refs", optional: "fields");
            CommandRegistry.Register("get_console", ExecGetConsole,
                required: "", optional: "count,level,first,keyword,count_only,since", allowedDuringCompile: true,
                maxResponseChars: 20000);
            CommandRegistry.Register("clear_console", _ => { ConsoleCapture.Clear(); return "ok"; },
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("get_compile_errors", _ => CompileErrorCapture.GetErrors(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("compile_status",
                _ => $"{CompileNotifier.GetStatus()}|reload={SyncHelper.SyncState}",
                required: "", optional: "", allowedDuringCompile: true);
            // sync/sync_status: unified reload API (v0.21)
            CommandRegistry.Register("sync",        args => SyncHelper.TriggerSync(
                JsonHelper.ExtractString(args, "resolve") == "true"),
                required: "", optional: "resolve");
            CommandRegistry.Register("sync_status", _ => SyncHelper.GetSyncStatus(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("recompile", _ => { UnityEditor.AssetDatabase.Refresh(); return "ok"; },
                required: "", optional: "");
            // G11: force_refresh — CLASS-A recovery sequence for file: UPM packages.
            // 1. ImportPackageSources: targeted ImportAsset bypasses dead directory-monitor.
            // 2. Refresh: whole-DB rescan as fallback for multi-file/asmdef edits.
            // 3. RequestScriptCompilation(None): compile ingested source (CleanBuildCache has 6.x no-op bug).
            // 4. StartTickPump: nudge backgrounded editor to start compiling.
            CommandRegistry.Register("force_refresh", _ =>
            {
                if (SessionState.GetBool("MCP_ReloadGuardLocked", false))
                {
                    SessionState.EraseBool("MCP_ReloadGuardLocked");
                    try { EditorApplication.UnlockReloadAssemblies(); } catch { }
                    try { AssetDatabase.AllowAutoRefresh(); } catch { }
                    try { AssetDatabase.Refresh(); } catch { }
                }
                SyncHelper.Ops.ImportPackageSources();
                SyncHelper.Ops.Refresh();
                SyncHelper.Ops.RequestScriptCompilation(RequestScriptCompilationOptions.None);
                if (!EditorApplication.isCompiling)
                    EditorUtility.RequestScriptReload();
                InternalEditorUtility.RepaintAllViews();
                SyncHelper.Ops.StartTickPump();
                return "force_refresh triggered";
            }, required: "", optional: "", allowedDuringCompile: true);  // G11: must work when wedged
            CommandRegistry.Register("search_scene", args => SearchHelper.Search(
                JsonHelper.ExtractString(args, "query"),
                JsonHelper.ExtractString(args, "root"),
                int.TryParse(JsonHelper.ExtractString(args, "limit") ?? "50",
                    out var sl) ? sl : 50,
                JsonHelper.ExtractString(args, "scene")),
                required: "query", optional: "root,limit,scene",
                maxResponseChars: 30000);
            CommandRegistry.Register("object_diff", args => ObjectDiffHelper.Diff(
                JsonHelper.ExtractString(args, "path_a"),
                JsonHelper.ExtractString(args, "path_b")),
                required: "path_a,path_b", optional: "");
            CommandRegistry.Register("editor", ExecEditor,
                required: "", optional: "action,path");
            CommandRegistry.Register("ping_object", args =>
                EditorStateHelper.PingObject(JsonHelper.ExtractString(args, "path")),
                required: "path", optional: "");
            CommandRegistry.Register("get_selection", _ => EditorStateHelper.GetSelection(),
                required: "", optional: "");
            CommandRegistry.Register("inspect", ExecInspect,
                required: "", optional: "paths,find_type,components,fields,compress,type");
            CommandRegistry.Register("get_unity_events", ExecGetUnityEvents,
                required: "", optional: "path");
            CommandRegistry.Register("runtime_snapshot", ExecRuntimeSnapshot,
                required: "type", optional: "name,component,compress",
                maxResponseChars: 50000);
            CommandRegistry.Register("validate_references", args => ValidateReferencesHelper.Validate(
                JsonHelper.ExtractString(args, "path"),
                ExtractInt(args, "depth", 3),
                JsonHelper.ExtractString(args, "ignore_optional") == "true",
                JsonHelper.ExtractString(args, "verbose") == "true"),
                required: "path", optional: "depth,ignore_optional,verbose");
            CommandRegistry.Register("checkpoint", args =>
            {
                var label = JsonHelper.ExtractString(args, "label");
                if (string.IsNullOrEmpty(label) || label == "checkpoint")
                    label = $"before_{_recentCmds.Count}_{string.Join("_", _recentCmds)}";
                UndoGroupHelper.BeginGroup($"AI: {label}");
                return $"Checkpoint: {label}";
            }, required: "", optional: "label");
            CommandRegistry.Register("validate_layout", args => LayoutValidator.Validate(
                JsonHelper.ExtractString(args, "root") ?? "/",
                float.TryParse(JsonHelper.ExtractString(args, "min_distance") ?? "3",
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var md) ? md : 3f),
                required: "", optional: "root,min_distance");
            CommandRegistry.Register("get_spatial_context", args => LayoutValidator.GetSpatialContext(
                JsonHelper.ExtractString(args, "path"),
                float.TryParse(JsonHelper.ExtractString(args, "radius") ?? "5",
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 5f),
                required: "path", optional: "radius");
            CommandRegistry.Register("fingerprint", args => FingerprintHelper.Fingerprint(
                JsonHelper.ExtractString(args, "path"),
                ExtractInt(args, "depth", 3)),
                required: "path", optional: "depth");
            CommandRegistry.Register("scan_scene", _ => ScanHelper.Scan(),
                required: "", optional: "");
            CommandRegistry.Register("render_analyze", args =>
            {
                var r = RenderAnalyzer.Execute(args);
                if (r.StartsWith("err:", System.StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(r.Substring(4).Trim());
                return r;
            }, required: "", optional: "action,path,detail,baseline_id,max_events");
            CommandRegistry.Register("check_colliders", args => ColliderChecker.Check(
                JsonHelper.ExtractString(args, "path")),
                required: "", optional: "path");
            CommandRegistry.Register("material_audit", args => MaterialAuditHelper.Execute(args),
                required: "", optional: "action,platform");
            CommandRegistry.Register("scene_health", args =>
                SceneHealthAnalyzer.Analyze(JsonHelper.ExtractString(args, "focus") ?? "all"),
                required: "", optional: "focus");
            CommandRegistry.Register("analyze_lod_culling", args => LodCullingAnalyzer.Analyze(
                JsonHelper.ExtractString(args, "focus")),
                required: "", optional: "focus");
            CommandRegistry.Register("get_schema", args => SchemaHelper.GetSchema(
                JsonHelper.ExtractString(args, "type")),
                required: "", optional: "type");
            CommandRegistry.Register("get_changes", args => ChangeWatcher.GetChanges(
                JsonHelper.ExtractString(args, "clear") != "false"),
                required: "", optional: "clear");
            CommandRegistry.Register("get_test_results", _ => TestRunner.GetResults(),
                required: "", optional: "", allowedDuringCompile: true);  // P1: SessionState-only read
            CommandRegistry.Register("get_test_progress", _ => TestRunner.GetProgress(),
                required: "", optional: "", allowedDuringCompile: true);  // SessionState-only read, safe during reload
            CommandRegistry.Register("get_test_count", _ => TestRunner.GetTestCount(),
                required: "", optional: "", allowedDuringCompile: true);  // discovery-only, no test run

            // Runtime (Play Mode only), read-only queries
            CommandRegistry.Register("query_state", args => GameStateHelper.Snapshot(
                JsonHelper.ExtractString(args, "queries")), runtime: true,
                required: "queries", optional: "");
            CommandRegistry.Register("get_frame_stats", _ => ProfilerHelper.GetFrameStats(), runtime: true,
                required: "", optional: "");
            CommandRegistry.RegisterAction("profile", Profiling.ProfileRecorder.Dispatch, runtime: true,
                required: "", optional: "mode,duration,session,focus,compare_with");
            CommandRegistry.Register("debug_animator", args =>
            {
                var go = ComponentSerializer.FindObjectOrThrow(JsonHelper.ExtractString(args, "path"));
                var animator = go.GetComponent<Animator>();
                if (animator == null) throw new ArgumentException("No Animator on " + go.name);
                return AnimatorHelper.GetState(animator);
            }, runtime: true, required: "path", optional: "");
            CommandRegistry.Register("debug_physics", args =>
            {
                var go = ComponentSerializer.FindObjectOrThrow(JsonHelper.ExtractString(args, "path"));
                float radius = JsonHelper.ExtractFloat(args, "radius");
                return PhysicsHelper.GetState(go, radius > 0f ? radius : 5f);
            }, runtime: true, required: "path", optional: "radius");
            CommandRegistry.Register("get_memory", args =>
                MemoryHelper.GetSnapshot(JsonHelper.ExtractString(args, "include") ?? "all"),
                required: "", optional: "include");
            CommandRegistry.Register("scene_diff", _ => SceneDiffHelper.Diff(),
                required: "", optional: "");
            // batch stays here (mutating:false) — it's a dispatcher; its DSL sub-commands are
            // separately guarded/mutating-flagged by CommandRegistry when each one executes.
            CommandRegistry.Register("batch", args => {
                var timeoutStr = JsonHelper.ExtractString(args, "timeout_ms");
                int timeoutMs = 25000;
                if (timeoutStr != null) int.TryParse(timeoutStr, out timeoutMs);
                bool atomic = JsonHelper.ExtractString(args, "atomic") == "true";
                bool validateAliases = JsonHelper.ExtractString(args, "validate_aliases") == "true";
                return BatchHelper.Execute(
                    JsonHelper.ExtractString(args, "commands"),
                    JsonHelper.ExtractString(args, "on_error") ?? "continue",
                    timeoutMs, atomic, validateAliases);
            }, mutating: false, required: "commands", optional: "on_error,timeout_ms,atomic,validate_aliases",
               allowedDuringCompile: true);

            // Spatial queries (read-only)
            CommandRegistry.Register("spatial_query", args => SpatialHelper.Execute(args),
                required: "action", optional: "path,target,distance,radius,component,cell_size,layer_mask,center,vertices,region_id,cap");
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
            CommandRegistry.Register("navmesh", NavMeshHelper.Execute,
                required: "action", optional: "center,max_distance,area_mask,from,to");
#endif

            // Code execution via Roslyn (non-mutating: allowed in Play Mode).
            // execute_code only ever accepts "code" (required) + "undo_label" (optional) —
            // a structured contract, not free-form (Issue 23 review M7). The code BODY the
            // user submits is arbitrary C#, but the command's own args are fixed.
            CommandRegistry.Register("execute_code", args => CodeExecutor.Execute(
                JsonHelper.ExtractString(args, "code"),
                JsonHelper.ExtractString(args, "undo_label") ?? "execute_code"),
                required: "code", optional: "undo_label",
                allowedDuringCompile: true);  // T2.5: ReloadGuard probe must work when wedged
            // Both file_path and new_content are required — a preflight check needs the file
            // and the candidate content to validate (Issue 23 review M8).
            CommandRegistry.Register("compile_preflight", args =>
            {
                var r = CompilePreflightCommand.Execute(args);
                if (r.StartsWith("ERR")) throw new InvalidOperationException(r);
                return r;
            }, required: "file_path,new_content", optional: "",
               allowedDuringCompile: true);  // Roslyn in-process — safe during Unity compile
        }

        internal static void RegisterMutatingCommands()
        {
            CommandRegistry.Register("undo_last", args =>
            {
                var turns = ExtractInt(args, "turns", 1);
                return UndoGroupStack.RevertLast(turns);
            }, mutating: true, required: "", optional: "turns");

            // Runtime (Play Mode only) — mutate runtime GameObject/component state directly
            // (not flagged mutating: true — runtime: true already routes them through the
            // Play-Mode-only guard; preserved as-is from the pre-split registration).
            CommandRegistry.Register("invoke_method", args => RuntimeHelper.InvokeMethod(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "method"),
                JsonHelper.ExtractString(args, "args")), runtime: true,
                required: "path,component,method", optional: "args");
            CommandRegistry.Register("set_runtime_property", args => RuntimeHelper.SetRuntimeProperty(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "field"),
                JsonHelper.ExtractString(args, "value")), runtime: true,
                required: "path,component,field,value", optional: "");

            // Write (mutating)
            CommandRegistry.Register("create_object", ExecCreateObject, mutating: true,
                required: "name", optional: "parent,components,primitive,prefab_path,scene");
            CommandRegistry.Register("transfer_object", ExecTransferObject, mutating: true,
                required: "path,action", optional: "target_scene,parent,world_position_stays");
            CommandRegistry.Register("delete_object", ExecDeleteObject, mutating: true,
                required: "", optional: "id,path,force");
            CommandRegistry.Register("set_property", ExecSetProperty, mutating: true,
                required: "", optional: "path,find_type,component,prop,value,dry_run");
            CommandRegistry.Register("set_property_delta", ExecSetPropertyDelta, mutating: true,
                required: "path,component,prop,delta", optional: "");
            CommandRegistry.Register("set_active", args => ObjectManager.SetActive(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "active") == "true"), mutating: true,
                required: "path,active", optional: "");
            CommandRegistry.Register("wire_event", args => ObjectManager.WireEvent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "event"),
                JsonHelper.ExtractString(args, "target"),
                JsonHelper.ExtractString(args, "method"),
                JsonHelper.ExtractString(args, "arg_type") ?? "void",
                JsonHelper.ExtractString(args, "arg_value")), mutating: true,
                required: "path,component,event,target,method", optional: "arg_type,arg_value");
            CommandRegistry.Register("unwire_event", args => ObjectManager.UnwireEvent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "event"),
                JsonHelper.ExtractString(args, "index")), mutating: true,
                required: "path,component,event", optional: "index");
            CommandRegistry.Register("auto_wire", ExecAutoWire, mutating: true,
                required: "path", optional: "dry_run");
            CommandRegistry.Register("manage_component", ExecManageComponent, mutating: true,
                required: "path,type,action", optional: "");
            CommandRegistry.Register("autofit_collider", ColliderFitHelper.Execute, mutating: true,
                required: "path", optional: "type");
            // parent is optional: null/omitted unparents to scene root (ObjectManager.SetParent
            // tolerates a null newParent — this is a valid, intentional operation, not an error).
            CommandRegistry.Register("set_parent", args => ObjectManager.SetParent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "parent"),
                JsonHelper.ExtractString(args, "world_position_stays") != "false"), mutating: true,
                required: "path", optional: "parent,world_position_stays");
            CommandRegistry.Register("rename_object", args => ObjectManager.RenameObject(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "name")), mutating: true,
                required: "path,name", optional: "");
            CommandRegistry.Register("set_sibling_index", args => ObjectManager.SetSiblingIndex(
                JsonHelper.ExtractString(args, "path"),
                int.Parse(JsonHelper.ExtractString(args, "index") ?? "0")), mutating: true,
                required: "path,index", optional: "");
            // color is optional: null/omitted leaves the material's existing color untouched
            // (ObjectManager.SetMaterial tolerates a null color, applying only shader if given).
            CommandRegistry.Register("set_material", args => ObjectManager.SetMaterial(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "color"),
                JsonHelper.ExtractString(args, "shader")), mutating: true,
                required: "path", optional: "color,shader");
            CommandRegistry.Register("scene", ExecScene, mutating: true,
                required: "action", optional: "path,scene");
            CommandRegistry.Register("animation", ExecAnimationConsolidated, mutating: true,
                required: "action,path", optional: "clip,clip_name,property,keys,time,component_type,binding_path,tangent,function_name,int_param,float_param,string_param");
            CommandRegistry.Register("timeline", ExecTimelineConsolidated, mutating: false,
                required: "action", optional: "path,track,track_type,clip,binding,start,duration,blend_in,blend_out,asset_path,director_path,tracks,time,name,clip_in,index,offset,value");
            CommandRegistry.Register("references", ExecReferencesConsolidated, mutating: true,
                required: "action", optional: "path,children,depth,source,target,mappings");
            CommandRegistry.Register("create_ui", ExecCreateUI, mutating: true,
                required: "type", optional: "name,parent,anchor,pos,size,pivot,color,text,font_size");
            CommandRegistry.Register("set_rect", ExecSetRect, mutating: true,
                required: "path", optional: "anchor,pos,size,pivot,offset_min,offset_max");
            CommandRegistry.Register("animator", ExecAnimatorConsolidated, mutating: true,
                required: "action,path", optional: "state,states,params,source,target,conditions,duration,exit_time,has_exit_time,type,name,blend_type,param,param_y,children,edit_action,layer,weight,blending,value,avatar_path");
            CommandRegistry.Register("particle", ExecParticleConsolidated, mutating: true,
                required: "action,path", optional: "name,module,prop,value,preset");
            CommandRegistry.Register("shader", ExecShaderConsolidated, mutating: true,
                required: "action,path", optional: "target,preset,code,shader_name,prop,value,keyword,enabled,node_type,node_id,node_action,output_node,output_slot,input_node,input_slot,edge_action,name,type,default_value,reference_name,new_name");
            CommandRegistry.Register("menu", ExecMenu, mutating: true,
                required: "action", optional: "path");

            // Action-based (Phase 26, mutating). Per-action params genuinely vary (e.g. asset's
            // create/move/delete each need different fields) — flat contract is intentionally
            // loose here (required: "action" only, everything else optional). See Issue 23 plan.
            CommandRegistry.RegisterAction("asset", AssetDatabaseHelper.Execute, mutating: true,
                optional: "path,type,name,folder,source,dest,prop,value,recursive,labels,output,include_deps");
            CommandRegistry.RegisterAction("project_settings", ProjectSettingsHelper.Execute, mutating: true,
                required: "target", optional: "prop,value,index");
            CommandRegistry.RegisterAction("material", MaterialHelper.Execute, mutating: true,
                optional: "path,object_path,shader,prop,value,source,targets,slot,filter");
            CommandRegistry.RegisterAction("prefab", PrefabHelper.Execute, mutating: true,
                optional: "path,asset_path,base_path,variant_path,recursive,component,prop,value,add_component,remove_component");
            CommandRegistry.RegisterAction("scriptable_object", ScriptableObjectHelper.Execute, mutating: true,
                optional: "path,type,prop,value,filter,fields");
            CommandRegistry.RegisterAction("scene_environment", EnvironmentHelper.Execute, mutating: true,
                optional: "prop,value");

            // Spatial mutation: region_clear (delete objects inside polygon)
            CommandRegistry.Register("region_clear", args => SpatialHelper.RegionClear(args), mutating: true,
                required: "vertices", optional: "dry_run,filter,cap");
        }

        internal static void RegisterAsyncCommands()
        {
            CommandRegistry.RegisterAsync("run_tests", AsyncRunTests, required: "", optional: "mode,filter,group");
            CommandRegistry.RegisterAsync("ask_user", AsyncAskUser, required: "", optional: "questions",
                alwaysAllowed: true, allowedDuringCompile: true);  // UI-only card, no assembly access
            CommandRegistry.RegisterAsync("wait_until", AsyncWaitUntil, runtime: true,
                required: "path,component,field,value", optional: "timeout,negate,abort_on_fail");
            CommandRegistry.RegisterAsync("move_to", AsyncMoveTo, runtime: true,
                required: "path,position", optional: "timeout");
            CommandRegistry.RegisterAsync("test_step", AsyncTestStep, runtime: true,
                required: "path,position", optional: "checks_before,checks_after,wait_after,timeout");
            CommandRegistry.RegisterAsync("run_playtest", AsyncRunPlaytest, runtime: true,
                required: "", optional: "script,path,defs,timeout,abort_on_fail,snapshot_on_failure,fresh");
        }
    }
}
