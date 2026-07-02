using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Compilation;
using UnityEngine;

[assembly: InternalsVisibleTo("UnityMCP.TestProject")]

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        // Fired when ask_user command arrives; MCPChatWindow subscribes to show AskUserCard.
        public static event System.Action<string, string> OnAskUser;  // (requestId, questionsJson)

        // Testable compilation state (defaults to real MCPServer state).
        // Two-layer check:
        //   1. MCPServer.IsReallyCompiling — authoritative flag set by compilationStarted/Finished events.
        //      Never stays latched after domain reload (unlike EditorApplication.isCompiling on Windows).
        //   2. CompileElapsedSeconds < 120s — wedge guard: treat >120s latched compiling as done.

        // Production lambda — saved separately so tests can restore it via DefaultIsCompiling
        // instead of reconstructing the lambda by hand.
        internal static readonly Func<bool> DefaultIsCompiling = () =>
        {
            if (!MCPServer.IsReallyCompiling) return false;
            return MCPServer.CompileElapsedSeconds < 120.0;
        };

        internal static Func<bool> IsCompiling = DefaultIsCompiling;
        internal static Func<bool> IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
        internal static Func<string, bool> IsToolEnabledFn = MCPSettings.IsToolEnabled;

        // Feature 3: recent command history for smart checkpoint naming
        internal static readonly Queue<string> _recentCmds = new Queue<string>();

        private static void TrackCommand(string cmd)
        {
            _recentCmds.Enqueue(cmd);
            if (_recentCmds.Count > 3) _recentCmds.Dequeue();
        }

        // Feature 2: suggest next tool for mutating commands
        internal static string SuggestNext(string cmd) => cmd switch
        {
            "set_property" => "get_console level=Error",
            "create_object" => "get_hierarchy depth=1",
            "wire_event" => "validate_references",
            "unwire_event" => "get_component",
            "manage_component" => "get_components_list",
            "delete_object" => "get_hierarchy depth=1",
            "set_parent" => "get_hierarchy depth=1",
            "batch" => "get_console level=Error",
            _ => null
        };

        // Returns error response string if a guard blocks the command, null otherwise.
        private static string CheckGuards(string id, string cmd)
        {
            if (IsCompiling() && !IsAllowedDuringCompile(cmd))
                return JsonHelper.FormatBusyResponse(id, "Unity is compiling. Retry in 5s.", 5000);
            if (IsPlayMode() && IsMutatingCommand(cmd))
                return JsonHelper.FormatResponse(id, false, null, "Play mode active — changes will be lost. Stop play mode first.");
            if (!IsPlayMode() && CommandRegistry.IsRuntime(cmd))
                return JsonHelper.FormatResponse(id, false, null, "Not in Play Mode. Use editor(action='play') first.");
            if (!IsAlwaysAllowed(cmd) && !IsToolEnabledFn(cmd))
                return JsonHelper.FormatResponse(id, false, null, $"Tool '{cmd}' is disabled in settings");
            return null;
        }

        // editor excluded: play/stop/select don't corrupt scene data
        private static bool IsMutatingCommand(string cmd) => CommandRegistry.IsMutating(cmd);

        public static string Process(string json)
        {
            SceneContext.InvalidateCache();
            try
            {
                var id = JsonHelper.ExtractString(json, "id");
                var cmd = JsonHelper.ExtractString(json, "cmd");

                var guard = CheckGuards(id, cmd);
                if (guard != null) return guard;

                var argsJson = JsonHelper.ExtractObject(json, "args");

                UndoGroupHelper.SetCommandFallback(cmd);

                if (cmd == "screenshot")
                {
                    var result = BuildScreenshotResponse(id, argsJson);
                    UndoGroupHelper.EndGroup();
                    return result;
                }

                if (cmd == "run_tests")
                {
                    UndoGroupHelper.EndGroup();
                    return JsonHelper.FormatResponse(id, false, null, "run_tests requires async dispatch — use ProcessAsync");
                }

                TrackCommand(cmd);
                var before = DateTime.Now;
                var data = ExecuteCommand(cmd, argsJson);
                UndoGroupHelper.EndGroup();
                // Issue 27 (Step 6): execute_code is non-mutating by registration but can still
                // throw/log a runtime exception inside Roslyn-executed code — surface it too.
                if (IsMutatingCommand(cmd) || cmd == "execute_code")
                {
                    var errors = ConsoleCapture.GetErrorsSince(before);
                    if (errors != null) data += "\n⚠ CONSOLE ERRORS:\n" + errors;
                    var suggestion = SuggestNext(cmd);
                    if (suggestion != null) data += $"\n[next: {suggestion}]";
                }
                return BuildResponse(id, data, CommandRegistry.GetMaxResponseChars(cmd));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCP] Command failed: {ErrorClassifier.FormatError(e)}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                return JsonHelper.FormatResponse(id, false, null, ErrorClassifier.FormatError(e));
            }
        }

        public static void ProcessAsync(string json, TaskCompletionSource<string> tcs)
        {
            SceneContext.InvalidateCache();
            try
            {
                var cmd = JsonHelper.ExtractString(json, "cmd");
                var id = JsonHelper.ExtractString(json, "id");

                if (CommandRegistry.HasAsyncHandler(cmd, out var asyncHandler))
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    UndoGroupHelper.SetCommandFallback(cmd);
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    asyncHandler(id, argsJson, tcs);
                    return;
                }

                tcs.TrySetResult(Process(json));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCP] Command failed: {ErrorClassifier.FormatError(e)}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, ErrorClassifier.FormatError(e)));
            }
        }

        private static void AsyncRunTests(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var mode = JsonHelper.ExtractString(argsJson, "mode");
            var group = JsonHelper.ExtractString(argsJson, "group");
            var filter = JsonHelper.ExtractString(argsJson, "filter");
            TestRunner.Execute(mode, result =>
            {
                UndoGroupHelper.EndGroup();
                if (result.StartsWith("Error:"))
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, result.Substring(7)));
                else
                    tcs.TrySetResult(BuildResponse(id, result));
            }, group, filter);
        }

        // Bridges an inner async Task<string> to the outer TCS, formatting a fault
        // uniformly as "{label} error: ...". Collapses 4x identical ContinueWith copy-paste.
        private static void CompleteFromInner(string id, Task<string> inner, TaskCompletionSource<string> tcs, string label)
        {
            inner.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted
                    ? BuildResponse(id, $"{label} error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}")
                    : BuildResponse(id, t.Result)));
        }

        private static void AsyncWaitUntil(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var component = JsonHelper.ExtractString(argsJson, "component");
            var field = JsonHelper.ExtractString(argsJson, "field");
            var value = JsonHelper.ExtractString(argsJson, "value");
            var timeout = ExtractFloat(argsJson, "timeout", 5f);
            var negate = JsonHelper.ExtractString(argsJson, "negate") == "true";
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.WaitUntil(path, component, field, value, timeout, negate, inner);
            CompleteFromInner(id, inner.Task, tcs, "wait_until");
        }

        private static void AsyncMoveTo(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var position = JsonHelper.ExtractString(argsJson, "position");
            var timeout = ExtractFloat(argsJson, "timeout", 15f);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.MoveTo(path, position, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "move_to");
        }

        private static void AsyncTestStep(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var position = JsonHelper.ExtractString(argsJson, "position");
            var checksBefore = JsonHelper.ExtractString(argsJson, "checks_before") ?? "";
            var checksAfter = JsonHelper.ExtractString(argsJson, "checks_after") ?? "";
            var waitAfter = ExtractFloat(argsJson, "wait_after", 0.5f);
            var timeout = ExtractFloat(argsJson, "timeout", 15f);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.TestStep(path, position, checksBefore, checksAfter, waitAfter, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "test_step");
        }

        private static void AsyncRunPlaytest(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var script = JsonHelper.ExtractString(argsJson, "script");
            var timeout = ExtractFloat(argsJson, "timeout", 120f);
            if (timeout <= 0) timeout = 120f;
            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(script, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "run_playtest");
        }

        private static void AsyncAskUser(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var questionsJson = JsonHelper.ExtractString(argsJson, "questions") ?? "[]";
            var requestId = System.Guid.NewGuid().ToString("N");
            PendingAskRegistry.Register(requestId);
            if (OnAskUser == null)
                Debug.LogWarning("[MCP] ask_user: no listener — is chat window open?");
            OnAskUser?.Invoke(requestId, questionsJson);
            var askTcs = PendingAskRegistry.GetTcs(requestId);
            askTcs.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted || t.IsCanceled
                    ? BuildResponse(id, "{\"cancelled\":true}")
                    : BuildResponse(id, t.Result)));
        }

        internal static string ExecuteCommand(string cmd, string args)
        {
            return CommandRegistry.Execute(cmd, args);
        }

        internal static void RegisterAll()
        {
            CommandRegistry.Clear();

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

            // Read (non-mutating)
            CommandRegistry.Register("get_hierarchy", ExecGetHierarchy,
                required: "", optional: "depth,root,filter,components,summary,incremental,scene",
                maxResponseChars: 50000);
            CommandRegistry.Register("get_component", ExecGetComponent,
                required: "path,type", optional: "");
            CommandRegistry.Register("get_components_list", ExecGetComponentsList,
                required: "id", optional: "");
            CommandRegistry.Register("get_object_detail", ExecGetObjectDetail,
                required: "id", optional: "");
            CommandRegistry.Register("find_objects", ExecFindObjects,
                required: "", optional: "name,tag,layer,component");
            CommandRegistry.Register("get_console", ExecGetConsole,
                required: "", optional: "count,level,first,keyword,count_only,since", allowedDuringCompile: true,
                maxResponseChars: 20000);
            CommandRegistry.Register("clear_console", _ => { ConsoleCapture.Clear(); return "ok"; },
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("get_compile_errors", _ => CompileErrorCapture.GetErrors(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("compile_status", _ => CompileNotifier.GetStatus(),
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("diagnose", args => DiagnoseCommand.Execute(args),
                required: "", optional: "",  // C8: read-only multi-signal snapshot
                alwaysAllowed: true, allowedDuringCompile: true);
            // sync/sync_status: unified reload API (v0.21)
            CommandRegistry.Register("sync",        args => SyncHelper.TriggerSync(
                JsonHelper.ExtractString(args, "resolve") == "true"),
                required: "", optional: "resolve");
            CommandRegistry.Register("sync_status", _ => SyncHelper.GetSyncStatus(),
                required: "", optional: "", allowedDuringCompile: true);
            // screenshot is intercepted in Process/ProcessAsync for file response formatting;
            // registered here only for IsRegistered/IsMutating queries
            CommandRegistry.Register("screenshot", _ => throw new InvalidOperationException("screenshot intercepted before ExecuteCommand"),
                required: "", optional: "width,height,camera,path,supersample,angles,zoom,offset,fixed_size,highlight,show_colliders,angle,annotation_id",
                specialDispatch: true, allowedDuringCompile: true);
            CommandRegistry.Register("recompile", _ => { UnityEditor.AssetDatabase.Refresh(); return "ok"; },
                required: "", optional: "");
            // G11: force_refresh — CLASS-A recovery sequence for file: UPM packages.
            // 1. ImportPackageSources: targeted ImportAsset bypasses dead directory-monitor.
            // 2. Refresh: whole-DB rescan as fallback for multi-file/asmdef edits.
            // 3. RequestScriptCompilation(None): compile ingested source (CleanBuildCache has 6.x no-op bug).
            // 4. StartTickPump: nudge backgrounded editor to start compiling.
            CommandRegistry.Register("force_refresh", _ =>
            {
                SyncHelper.Ops.ImportPackageSources();
                SyncHelper.Ops.Refresh();
                SyncHelper.Ops.RequestScriptCompilation(RequestScriptCompilationOptions.None);
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
                JsonHelper.ExtractString(args, "pathA"),
                JsonHelper.ExtractString(args, "pathB")),
                required: "pathA,pathB", optional: "");
            CommandRegistry.Register("editor", ExecEditor,
                required: "", optional: "action,path");
            CommandRegistry.Register("ping_object", args =>
                EditorStateHelper.PingObject(JsonHelper.ExtractString(args, "path")),
                required: "path", optional: "");
            CommandRegistry.Register("get_selection", _ => EditorStateHelper.GetSelection(),
                required: "", optional: "");
            CommandRegistry.Register("inspect", ExecInspect,
                required: "paths", optional: "components");
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
            CommandRegistry.Register("undo_last", args =>
            {
                var turns = ExtractInt(args, "turns", 1);
                return UndoGroupStack.RevertLast(turns);
            }, mutating: true, required: "", optional: "turns");
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
            CommandRegistry.Register("render_analyze", args => RenderAnalyzer.Execute(args),
                required: "", optional: "action,path,detail,baseline_id,max_events");
            CommandRegistry.Register("check_colliders", args => ColliderChecker.Check(
                JsonHelper.ExtractString(args, "path")),
                required: "path", optional: "");
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
            CommandRegistry.RegisterAsync("run_tests", AsyncRunTests, required: "", optional: "mode,filter,group");
            CommandRegistry.RegisterAsync("ask_user", AsyncAskUser, required: "", optional: "questions",
                alwaysAllowed: true, allowedDuringCompile: true);  // UI-only card, no assembly access
            CommandRegistry.Register("get_test_results", _ => TestRunner.GetResults(),
                required: "", optional: "", allowedDuringCompile: true);  // P1: SessionState-only read
            CommandRegistry.Register("get_test_count", _ => TestRunner.GetTestCount(),
                required: "", optional: "", allowedDuringCompile: true);  // discovery-only, no test run

            // Runtime (Play Mode only)
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
            CommandRegistry.RegisterAsync("wait_until", AsyncWaitUntil, runtime: true,
                required: "path,component,field,value", optional: "timeout,negate");
            CommandRegistry.RegisterAsync("move_to", AsyncMoveTo, runtime: true,
                required: "path,position", optional: "timeout");
            CommandRegistry.RegisterAsync("test_step", AsyncTestStep, runtime: true,
                required: "path,position", optional: "checks_before,checks_after,wait_after,timeout");
            CommandRegistry.RegisterAsync("run_playtest", AsyncRunPlaytest, runtime: true,
                required: "script", optional: "timeout");
            CommandRegistry.Register("query_state", args => GameStateHelper.Snapshot(
                JsonHelper.ExtractString(args, "queries")), runtime: true,
                required: "queries", optional: "");
            CommandRegistry.Register("get_perf", _ => ProfilerHelper.GetSnapshot(), runtime: true,
                required: "", optional: "");
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

            // Write (mutating)
            CommandRegistry.Register("create_object", ExecCreateObject, mutating: true,
                required: "name", optional: "parent,components,primitive,prefab_path,scene");
            CommandRegistry.Register("transfer_object", ExecTransferObject, mutating: true,
                required: "path,action", optional: "target_scene,parent,world_position_stays");
            CommandRegistry.Register("delete_object", ExecDeleteObject, mutating: true,
                required: "", optional: "id,path,force");
            CommandRegistry.Register("set_property", ExecSetProperty, mutating: true,
                required: "path,component,prop,value", optional: "dry_run");
            CommandRegistry.Register("set_property_delta", ExecSetPropertyDelta, mutating: true,
                required: "path,component,prop,delta", optional: "");
            CommandRegistry.Register("scene_diff", _ => SceneDiffHelper.Diff(),
                required: "", optional: "");
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
            // color is optional: null/omitted leaves the material's existing color untouched
            // (ObjectManager.SetMaterial tolerates a null color, applying only shader if given).
            CommandRegistry.Register("set_material", args => ObjectManager.SetMaterial(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "color"),
                JsonHelper.ExtractString(args, "shader")), mutating: true,
                required: "path", optional: "color,shader");
            CommandRegistry.Register("batch", args => {
                var timeoutStr = JsonHelper.ExtractString(args, "timeout_ms");
                int timeoutMs = 25000;
                if (timeoutStr != null) int.TryParse(timeoutStr, out timeoutMs);
                bool atomic = JsonHelper.ExtractString(args, "atomic") == "true";
                return BatchHelper.Execute(
                    JsonHelper.ExtractString(args, "commands"),
                    JsonHelper.ExtractString(args, "on_error") ?? "continue",
                    timeoutMs, atomic);
            }, mutating: false, required: "commands", optional: "on_error,timeout_ms,atomic",
               allowedDuringCompile: true);
            CommandRegistry.Register("scene", ExecScene, mutating: true,
                required: "action", optional: "path,scene");
            CommandRegistry.Register("animation", ExecAnimationConsolidated, mutating: true,
                required: "action,path", optional: "clip,clip_name,property,keys,time,component_type");
            CommandRegistry.Register("timeline", ExecTimelineConsolidated, mutating: true,
                required: "action", optional: "path,track,track_type,clip,binding,start,duration,blend_in,blend_out,asset_path,director_path,tracks,time");
            CommandRegistry.Register("references", ExecReferencesConsolidated, mutating: true,
                required: "action", optional: "path,children,depth,source,target,mappings");
            CommandRegistry.Register("create_ui", ExecCreateUI, mutating: true,
                required: "type", optional: "name,parent,anchor,pos,size,pivot,color,text,fontSize");
            CommandRegistry.Register("set_rect", ExecSetRect, mutating: true,
                required: "path", optional: "anchor,pos,size,pivot,offsetMin,offsetMax");
            CommandRegistry.Register("animator", ExecAnimatorConsolidated, mutating: true,
                required: "action,path", optional: "state,states,params,source,target,conditions,duration,exit_time,has_exit_time,type,name,blend_type,param,param_y,children,edit_action");
            CommandRegistry.Register("particle", ExecParticleConsolidated, mutating: true,
                required: "action,path", optional: "name,module,prop,value,preset");
            CommandRegistry.Register("shader", ExecShaderConsolidated, mutating: true,
                required: "action,path", optional: "target,preset,code,shader_name,prop,value,keyword,enabled,node_type,node_id,node_action,output_node,output_slot,input_node,input_slot,edge_action");
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
                optional: "path,object_path,shader,prop,value,source,targets,slot");
            CommandRegistry.RegisterAction("prefab", PrefabHelper.Execute, mutating: true,
                optional: "path,asset_path,base_path,variant_path,recursive,component,prop,value,add_component,remove_component");
            CommandRegistry.RegisterAction("scriptable_object", ScriptableObjectHelper.Execute, mutating: true,
                optional: "path,type,prop,value,filter");
            CommandRegistry.RegisterAction("scene_environment", EnvironmentHelper.Execute, mutating: true,
                optional: "prop,value");

            // Spatial queries (read-only)
            CommandRegistry.Register("spatial_query", args => SpatialHelper.Execute(args),
                required: "action", optional: "path,target,distance,radius,component,cell_size,layer_mask,center,vertices,region_id,cap");
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
            CommandRegistry.Register("navmesh", NavMeshHelper.Execute,
                required: "action", optional: "center,max_distance,area_mask,from,to");
#endif

            // Spatial mutation: region_clear (delete objects inside polygon)
            CommandRegistry.Register("region_clear", args => SpatialHelper.RegionClear(args), mutating: true,
                required: "vertices", optional: "dry_run,filter,cap");

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
            CommandRegistry.Register("compile_preflight", args => CompilePreflightCommand.Execute(args),
                required: "file_path,new_content", optional: "",
                allowedDuringCompile: true);  // Roslyn in-process — safe during Unity compile

            // Watch system (Phase 3)
            WatchCommandHandler.RegisterAll();

            PluginRegistry.RegisterAllPlugins();

            // Eager-populate after ALL tools are registered (including plugins).
            // This is the correct site: RegisterAll is the last step in CommandRegistry.InitDefaults
            // and is always called on the main thread — safe to read EditorPrefs here.
            _enabledToolsCache = ExecGetEnabledTools();
        }

        // internal (not private) so UnityMCP.Editor.Tests can call directly for seam tests.
        // Task 3.2 (ROI reliability sprint): the hard TEXT_THRESHOLD file-offload check must
        // run BEFORE Truncate() — otherwise a command with a maxResponseChars soft limit gets
        // its data cut down to that limit even when it should have been preserved in full via
        // file offload. Data under the threshold is unaffected: it still gets soft-truncated.
        internal static string BuildResponse(string id, string data, int maxResponseChars = 0)
        {
            if (data != null && data.Length > FileOutputHelper.TEXT_THRESHOLD)
            {
                var filePath = FileOutputHelper.WriteText(data);
                return JsonHelper.FormatFileResponse(id, filePath);
            }
            data = ResponseGovernance.Truncate(data, maxResponseChars);
            return JsonHelper.FormatResponse(id, true, data, null);
        }

        // Commands that bypass MCPSettings.IsToolEnabled check.
        // Both flags now live on the registration itself (CommandRegistry.Entry) so a rename
        // in RegisterAll() cannot silently desync the guard (DRY audit issues-23-29 Cat.1).
        // Note: "get_version" has no registration at all — MCPServer.cs intercepts it before it
        // ever reaches CommandRouter (fast-path, emits MVID stamp). See GetVersion_NotRegistered_In_CommandRegistry.
        internal static bool IsAlwaysAllowed(string cmd) => CommandRegistry.IsAlwaysAllowed(cmd);

        internal static bool IsAllowedDuringCompile(string cmd) => CommandRegistry.IsAllowedDuringCompile(cmd);

        private static int ExtractInt(string json, string key, int defaultVal)
        {
            var val = JsonHelper.ExtractString(json, key);
            if (val == null) return defaultVal;
            return int.TryParse(val, out var result) ? result : defaultVal;
        }

        // internal (not private): reused outside CommandRouter's partial-class parts,
        // e.g. WatchCommandHandler — collapses the "parse float arg with default" copy-paste.
        internal static float ExtractFloat(string json, string key, float defaultVal)
        {
            var val = JsonHelper.ExtractString(json, key);
            if (val == null) return defaultVal;
            return float.TryParse(val, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultVal;
        }

        private static string GetCapabilities()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"unity:{Application.unityVersion}");
            sb.AppendLine($"platform:{Application.platform}");
            sb.AppendLine($"scriptingBackend:{UnityEditor.PlayerSettings.GetScriptingBackend(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup)}");
            sb.AppendLine("packages:");
#if UNITYMCP_HAS_CINEMACHINE
            sb.AppendLine("  cinemachine:true");
#endif
#if UNITYMCP_HAS_URP
            sb.AppendLine("  urp:true");
#endif
#if UNITYMCP_HAS_HDRP
            sb.AppendLine("  hdrp:true");
#endif
#if UNITYMCP_HAS_INPUT_SYSTEM
            sb.AppendLine("  inputSystem:true");
#endif
#if UNITYMCP_HAS_POST_PROCESSING
            sb.AppendLine("  postProcessing:true");
#endif
#if UNITYMCP_HAS_TMP
            sb.AppendLine("  textMeshPro:true");
#endif
#if UNITYMCP_HAS_AI_NAVIGATION
            sb.AppendLine("  aiNavigation:true");
#endif
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            sb.AppendLine($"renderPipeline:{(rp != null ? rp.GetType().Name : "built-in")}");
            return sb.ToString().TrimEnd();
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            PluginRegistry.OnDomainReload();
            // Do NOT populate here: [InitializeOnLoadMethod] fires before CommandRegistry.static ctor
            // (which calls RegisterAll). Populating now yields an empty/partial tool list.
            // Instead, RegisterAll() eagerly populates at its end (after all tools are registered).
        }

    }
}
