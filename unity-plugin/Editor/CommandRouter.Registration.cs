using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using UnityMCP.Editor.Roslyn;
namespace UnityMCP.Editor
{
    // RegisterAll() split into 4 themed bucket methods (B1, review sprint v0.70).
    // The original RegisterAll() had grown into a ~340-line God Method wiring all 93 commands
    // inline. Split by guard-flag semantics (matches CheckGuards' own precedence order):
    // meta (always-allowed escape hatches) -> read (non-mutating) -> mutating -> async.
    // Every Register/RegisterAction/RegisterAsync call below is moved verbatim from the
    // original RegisterAll() — no flags, params, or handlers were changed by this split.
    //
    // A5 prep: every inline lambda handler below was extracted to a named static method
    // (grouped by section at the bottom of this file) so the HANDLER DSL generator can take
    // a method reference for every command. No flags, params, or handler logic changed.
    public static partial class CommandRouter
    {
        internal static void RegisterMetaCommands()
        {
            // Meta (non-mutating) — always allowed + allowed during compile: these are the
            // escape-hatch/discovery commands that must work regardless of settings or wedge state.
            // C7: "get_version" fast-path is in MCPServer.cs:293 (emits MVID stamp).
            // The VersionTracker delegation here was dead code — MCPServer intercepts get_version
            // before it reaches CommandRouter. Removed so get_version unambiguously means MVID stamp.
            CommandRegistry.Register("get_disabled_tools", ExecGetDisabledToolsCommand, required: "", optional: "",
                alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("set_tool_catalog", ExecSetToolCatalog,
                required: "", optional: "catalog", alwaysAllowed: true, allowedDuringCompile: true);
            // screenshot is intercepted in Process/ProcessAsync for file response formatting;
            // registered here only for IsRegistered/IsMutating queries
            CommandRegistry.Register("screenshot", ExecScreenshotIntercepted,
                mutating: true,
                required: "", optional: "width,height,camera,path,output_path,supersample,angles,zoom,offset,fixed_size,highlight,show_colliders,angle,annotation_id,describe,raw",
                fileHandler: BuildScreenshotResponse,
                specialDispatch: true, allowedDuringCompile: true, playOk: true);
            // T5 recovery: play+stop cycle forces domain reload without going through SecurityScan.
            // allowedDuringCompile=true so it works in compile-latch state.
            // Entering Play Mode triggers a domain reload in this project (full domain
            // reload, not the fast-enter-play-mode option), which wipes any delayCall/update
            // subscription registered inline here (RELAY-FIX, commit 1bcc90b7) — the pending
            // stop/start survives via SessionState instead, consumed by PlayModeEpochTracker,
            // whose static-ctor subscription re-arms on every domain reload.
            CommandRegistry.Register("force_play_stop", ExecForcePlayStop,
                required: "", optional: "", alwaysAllowed: true, allowedDuringCompile: true);
            CommandRegistry.Register("set_client_label", ExecSetClientLabel,
                required: "label", optional: "", alwaysAllowed: true, allowedDuringCompile: true);
        }

        // Commands with a HANDLER line in dsl/tools.biome are registered by ToolBindings.RegisterAll (Generated/ToolBindings.g.cs).
        // Only the ones the catalog cannot express stay here: no tools.biome block, screenshot (fileHandler), batch (a dispatcher,
        // mutating: false), navmesh (UNITY_MODULE_AI only).
        internal static void RegisterReadCommands()
        {
            CommandRegistry.Register("clear_console", ExecClearConsole,
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("console_clear_buffer", ExecConsoleClearBuffer,
                required: "", optional: "", allowedDuringCompile: true);
            // sync/sync_status: unified reload API (v0.21)
            // N1b: 'sync' triggers a real mutation (SyncHelper.Ops.Refresh +
            // RequestScriptCompilation via TriggerSync) — mutating/notBatchable
            // were missing their explicit flags (both defaulted false), so the
            // read-only gate and IsBatchable never saw this command as unsafe.
            CommandRegistry.Register("sync", ExecSync,
                mutating: true, notBatchable: true, required: "", optional: "resolve");
            CommandRegistry.Register("sync_status", ExecSyncStatus,
                required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("warm_type_cache", ExecWarmTypeCache,
                required: "", optional: "", allowedDuringCompile: false);
            // G11: force_refresh — CLASS-A recovery sequence for file: UPM packages.
            // 1. ImportPackageSources: targeted ImportAsset bypasses dead directory-monitor.
            // 2. Refresh: whole-DB rescan as fallback for multi-file/asmdef edits.
            // 3. RequestScriptCompilation(None): compile ingested source (CleanBuildCache has 6.x no-op bug).
            // 4. StartTickPump: nudge backgrounded editor to start compiling.
            // N1b: same gap as 'sync' above (real mutation, flags defaulted
            // false). allowedDuringCompile stays true (G11: must work when
            // wedged) -- that axis is independent of read-only/batch safety.
            // No alwaysAllowed: server/tests/test_reload_ladder.py's
            // test_send_with_fallback_does_not_catch_plain_tool_error already
            // pins READ_ONLY_BLOCKED on force_refresh as expected propagation,
            // not a case the recovery ladder silently works around.
            CommandRegistry.Register("force_refresh", ExecForceRefresh,
                mutating: true, notBatchable: true, required: "", optional: "", allowedDuringCompile: true);
            CommandRegistry.Register("search_context", ExecSearchContext,
                optional: "query,limit,types",
                maxResponseChars: 30000);
            CommandRegistry.Register("checkpoint_undo_restore", ExecCheckpointUndoRestore,
                required: "group_id,domain_stamp", optional: "");
            // NOTE: NOT runtime:true — sessions persist after Play Mode ends
            CommandRegistry.Register("get_profile_context", ExecGetProfileContext, required: "", optional: "session");
            // batch stays here (mutating:false) — it's a dispatcher; its DSL sub-commands are
            // separately guarded/mutating-flagged by CommandRegistry when each one executes.
            CommandRegistry.Register("batch", ExecBatch, mutating: false, required: "commands", optional: "on_error,timeout,atomic,validate_aliases",
               allowedDuringCompile: true);

            // Spatial queries (read-only)
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
            CommandRegistry.Register("navmesh", NavMeshHelper.Execute,
                mutating: true,
                required: "action", optional: "center,max_distance,area_mask,from,to,agentRadius,agentHeight,agentClimb,agentSlope");
#endif
        }

        internal static void RegisterMutatingCommands()
        {
            // Runtime (Play Mode only) — mutate runtime GameObject/component state directly. runtime: true routes them through the
            // Play-Mode-only guard. Both are mutating as in Python (step 7.2), with playOk (they must run in Play Mode) and noUndo
            // (runtime changes are no Undo-recorded scene change).
            CommandRegistry.Register("set_runtime_property", ExecSetRuntimeProperty, runtime: true,
                mutating: true, playOk: true, noUndo: true,
                required: "path,component,field,value", optional: "");
        }

        // P0-50/E02/E03: these three are declared INTERNAL (not TOOL) in dsl/tools.biome —
        // the generator does not emit Register* calls for INTERNAL blocks, so they stay
        // hand-registered here (commit 7527deb8 removed them along with the 29 generated
        // ones; ToolBindings.g.cs never picked them back up, leaving them unregistered).
        internal static void RegisterAsyncCommands()
        {
            // E02: internal wire command (not @mcp.tool() until E04) — non-blocking playtest
            // dispatch, paired with get_playtest_run's poll. Same gates as run_playtest.
            CommandRegistry.RegisterAsync("start_playtest", AsyncStartPlaytest, runtime: false,
                mutating: true, playOk: true,
                required: "", optional: "script,path,defs,timeout,abort_on_fail,snapshot_on_failure,fresh,before_hook,after_hook,format");
            // E03: internal wire command (not @mcp.tool() until E04) — compact poll for
            // start_playtest. allowedDuringCompile mirrors get_test_run so polling survives an
            // unrelated compile.
            CommandRegistry.RegisterAsync("get_playtest_run", AsyncGetPlaytestRun,
                required: "run_id", optional: "", allowedDuringCompile: true);
            CommandRegistry.RegisterAsync("source_patch_write", AsyncSourcePatchWrite,
                mutating: true, required: "path,content", optional: "");
        }

        // ============================================================
        // Extracted command handlers (A5 prep). Grouped to mirror the
        // Register*Commands buckets above. Pure mechanical extraction
        // from the inline lambdas that used to sit in the Register calls —
        // no handler logic, flags, or params were changed.
        // ============================================================

        // ---- Meta command handlers ----

        internal static string ExecPing(string args) => "pong";

        internal static string ExecGetCapabilities(string args) => GetCapabilities();

        internal static string ExecGetEnabledToolsCommand(string args) => ExecGetEnabledTools();

        private static string ExecGetDisabledToolsCommand(string args) => ExecGetDisabledTools();

        // "set_tool_catalog" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome -- no
        // generated accessor exists to migrate to.
        private static string ExecSetToolCatalog(string args)
        {
            var json = JsonHelper.ExtractString(args, "catalog");
            if (!string.IsNullOrEmpty(json)) MCPSettings.SetCatalog(json);
            return "ok";
        }

        private static string ExecScreenshotIntercepted(string args) =>
            throw new InvalidOperationException("screenshot intercepted before ExecuteCommand");

        internal static string ExecDiagnose(string args) => DiagnoseCommand.Execute(args);

        private static string ExecForcePlayStop(string args)
        {
            var blocked = SyncHelper.ReloadBlockReason(false);
            if (!string.IsNullOrEmpty(blocked)) return "blocked|reason=" + blocked;
            if (EditorApplication.isCompiling)
            {
                SessionState.SetBool(PlayModeEpochTracker.PendingPlayStartKey, true);
                PlayModeEpochTracker.WaitForCompileThenEnterPlayMode();
                return "play_stop queued (waiting for compile)";
            }
            // Direct (non-compiling) entry: EditorApplication.isCompiling being false does
            // not guarantee isPlaying = true actually succeeds (leftover compile errors from
            // a finished build, or an in-progress unrelated transition can silently refuse
            // it) — go through the same reload-survival helper the compiling branch above
            // uses, so a refused entry here cannot strand PendingPlayStopKey either (C1 r6 #2).
            PlayModeEpochTracker.EnterPlayModeWithPendingStop();
            return "play_stop triggered";
        }

        // "set_client_label" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome -- no
        // generated accessor exists to migrate to.
        private static string ExecSetClientLabel(string args)
        {
            var label = JsonHelper.ExtractString(args, "label");
            if (!string.IsNullOrEmpty(label))
            {
                MCPServer._mainSlot.Label = label;
                MainThreadDispatcher.Enqueue(() => Debug.Log($"{BiomeLabel.Tag} Client identified as: {label}"));
            }
            return "ok";
        }

        internal static string ExecGetStatus(string args)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"scene={scene.name}");
            sb.AppendLine($"dirty={scene.isDirty}");
            sb.AppendLine($"playing={UnityEditor.EditorApplication.isPlaying}");
            sb.AppendLine($"compiling={UnityEditor.EditorApplication.isCompiling}");
            sb.AppendLine($"port={MCPServer.ServerPort}");
            sb.AppendLine($"aliases={AliasExpander.CountConfigAliases()}");
            sb.AppendLine($"readOnly={IsReadOnly()}");
            sb.AppendLine($"plugin_version={BiomeVersion.Plugin}");
            sb.AppendLine($"protocol={BiomeVersion.Protocol}");
            sb.AppendLine(SourcePatchModePolicy.StatusProjection());
            return sb.ToString().TrimEnd();
        }

        // ---- Read command handlers ----

        internal static string ExecGetAliases(string args) => GetAliasesText();

        internal static string ExecListPlaytestFiles(string args)
        {
            var pattern = ListPlaytestFilesArgs.Pattern(args) ?? "Playtests/*.playtest";
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
        }

        internal static string ExecLintPlaytest(string args)
        {
            var path   = LintPlaytestArgs.Path(args);
            var script = LintPlaytestArgs.Script(args);
            if (path != null && script != null) return "err: use path or script, not both";
            string result;
            if (path != null)        result = PlaytestLinter.LintFile(path);
            else if (script != null) result = PlaytestLinter.LintScript(script);
            else return "err: path or script required";
            if (result.StartsWith("ERROR") || result.Contains("\nERROR"))
                throw new InvalidOperationException(result);
            return result;
        }

        internal static string ExecLintSceneRefs(string args)
        {
            var path    = LintSceneRefsArgs.Path(args);
            var snippet = LintSceneRefsArgs.Snippet(args);
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
        }

        internal static string ExecResolveSceneRefs(string args)
        {
            var refs = ResolveSceneRefsArgs.Refs(args);
            if (string.IsNullOrEmpty(refs)) return "err: refs required";
            var results = SceneRefResolver.ResolveMany(refs, ResolveSceneRefsArgs.Fields(args));
            return SceneRefResolver.FormatResults(results);
        }

        private static string ExecClearConsole(string args) { ConsoleCapture.Clear(); return "ok"; }

        private static string ExecConsoleClearBuffer(string args) { ConsoleCapture.ClearDroppedCount(); return "ok"; }

        internal static string ExecGetCompileErrors(string args) => CompileErrorCapture.GetErrors();

        internal static string ExecCompileStatus(string args) =>
            $"{CompileNotifier.GetStatus()}|reload={SyncHelper.SyncState}";

        // "sync" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome -- no generated accessor
        // exists to migrate to.
        private static string ExecSync(string args) => SyncHelper.TriggerSync(
            JsonHelper.ExtractString(args, "resolve") == "true");

        private static string ExecSyncStatus(string args) => SyncHelper.GetSyncStatus();

        internal static string ExecRecompile(string args)
        {
            var blocked = SyncHelper.ReloadBlockReason(false);
            if (!string.IsNullOrEmpty(blocked)) return "blocked|reason=" + blocked;
            UnityEditor.AssetDatabase.Refresh();
            return "ok";
        }

        private static string ExecWarmTypeCache(string args)
        {
            var count = UnityEditor.TypeCache.GetTypesDerivedFrom<Component>().Count;
            return $"ok:types={count}";
        }

        private static string ExecForceRefresh(string args)
        {
            var blocked = SyncHelper.ReloadBlockReason(false);
            if (!string.IsNullOrEmpty(blocked)) return "blocked|reason=" + blocked;
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
            // Defer reload request — calling RequestScriptReload synchronously triggers
            // immediate domain reload that wipes pending callbacks (B3 fix).
            // MainThreadDispatcher (EditorApplication.update-driven) reaches a backgrounded
            // Editor reliably (RELAY-FIX, commit 1bcc90b7); its next-tick guarantee comes
            // from Drain's snapshot-count pass, not a self-unsubscribing one-shot.
            MainThreadDispatcher.Enqueue(() =>
            {
                if (!EditorApplication.isCompiling && string.IsNullOrEmpty(SyncHelper.ReloadBlockReason(false)))
                    EditorUtility.RequestScriptReload();
            });
            InternalEditorUtility.RepaintAllViews();
            SyncHelper.Ops.StartTickPump();
            return "force_refresh triggered";
        }

        internal static string ExecSearchScene(string args) => SearchHelper.Search(
            SearchSceneArgs.Query(args),
            SearchSceneArgs.Root(args),
            // NOT SearchSceneArgs.Limit: that non-nullable int accessor's JsonHelper.ExtractInt
            // fallback is a hardcoded 0 on missing input, not this call site's 50 -- keep the
            // hand-parsed default (same rule as the float DEFAULT mismatches elsewhere in this file).
            int.TryParse(JsonHelper.ExtractString(args, "limit") ?? "50",
                out var sl) ? sl : 50,
            SearchSceneArgs.Scene(args));

        // "search_context" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome (hand-registered,
        // not catalog-driven) -- no generated accessor exists to migrate to.
        private static string ExecSearchContext(string args) => SearchHelper.SearchContext(
            JsonHelper.ExtractString(args, "query") ?? "",
            int.TryParse(JsonHelper.ExtractString(args, "limit") ?? "30", out var scl) ? scl : 30,
            JsonHelper.ExtractString(args, "types"));

        internal static string ExecObjectDiff(string args) => ObjectDiffHelper.Diff(
            ObjectDiffArgs.PathA(args),
            ObjectDiffArgs.PathB(args));

        internal static string ExecPingObject(string args) =>
            EditorStateHelper.PingObject(PingObjectArgs.Path(args));

        internal static string ExecGetSelection(string args) => EditorStateHelper.GetSelection();

        internal static string ExecValidateReferences(string args) => ValidateReferencesHelper.Validate(
            ValidateReferencesArgs.Path(args),
            ExtractInt(args, "depth", 3),
            ValidateReferencesArgs.IgnoreOptional(args),
            ValidateReferencesArgs.Verbose(args));

        internal static string ExecCheckpoint(string args)
        {
            var label = CheckpointArgs.Label(args);
            if (string.IsNullOrEmpty(label) || label == "checkpoint")
                label = $"before_{_recentCmds.Count}_{string.Join("_", _recentCmds)}";
            UndoGroupHelper.BeginGroup($"AI: {label}");
            var groupId = UndoGroupHelper.CurrentGroupId;
            var domainStamp = SyncHelper.CurrentDomainStamp;
            return $"Checkpoint: {label}\ngroup_id={groupId}\ndomain_stamp={domainStamp}";
        }

        // "checkpoint_undo_restore" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome --
        // no generated accessor exists to migrate to.
        private static string ExecCheckpointUndoRestore(string args)
        {
            var groupIdStr = JsonHelper.ExtractString(args, "group_id");
            var storedStamp = JsonHelper.ExtractString(args, "domain_stamp");
            if (!int.TryParse(groupIdStr, out var groupId) || groupId < 0)
                return "err=invalid_group_id";
            if (storedStamp != SyncHelper.CurrentDomainStamp)
                return "stale_domain";
            UndoGroupHelper.RevertToBeforeGroup(groupId);
            return "ok";
        }

        internal static string ExecValidateTriggers(string args) => LayoutValidator.Validate(
            ValidateTriggersArgs.Root(args) ?? "/",
            // NOT ValidateTriggersArgs.MinDistance: same 0f-vs-custom-default mismatch as the
            // ExtractFloat-backed accessors elsewhere in this file -- keep the hand-parsed 3f.
            float.TryParse(JsonHelper.ExtractString(args, "min_distance") ?? "3",
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var md) ? md : 3f);

        internal static string ExecGetSpatialContext(string args) => LayoutValidator.GetSpatialContext(
            GetSpatialContextArgs.Path(args),
            // NOT GetSpatialContextArgs.Radius: same 0f-vs-custom-default (5f) mismatch.
            float.TryParse(JsonHelper.ExtractString(args, "radius") ?? "5",
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 5f);

        internal static string ExecFingerprint(string args) => FingerprintHelper.Fingerprint(
            FingerprintArgs.Path(args),
            ExtractInt(args, "depth", 3));

        internal static string ExecScanScene(string args) => ScanHelper.Scan();

        internal static string ExecRenderAnalyze(string args)
        {
            var r = RenderAnalyzer.Execute(args);
            if (r.StartsWith("err:", System.StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(r.Substring(4).Trim());
            return r;
        }

        internal static string ExecCheckColliders(string args) => ColliderChecker.Check(
            CheckCollidersArgs.Path(args));

        internal static string ExecMaterialAudit(string args) => MaterialAuditHelper.Execute(args);

        internal static string ExecSceneHealth(string args) =>
            SceneHealthAnalyzer.Analyze(SceneHealthArgs.Focus(args) ?? "all");

        internal static string ExecAnalyzeLodCulling(string args) => LodCullingAnalyzer.Analyze(
            AnalyzeLodCullingArgs.Focus(args));

        // NOT GetSchemaArgs.Type: "get_schema" is not in ArgContract's enabled set, so
        // CommandValidator.Validate never runs for it -- no required-arg gate protects this
        // handler. GetSchemaArgs.Type calls JsonHelper.RequireString, which would throw on a
        // missing/empty "type" where this call site today just passes null through to
        // SchemaHelper.GetSchema. Keep raw.
        internal static string ExecGetSchema(string args) => SchemaHelper.GetSchema(
            JsonHelper.ExtractString(args, "type"));

        // NOT GetChangesArgs.Clear: dsl/tools.biome declares `ARG clear bool DEFAULT True`, but
        // the generated bool accessor always reads key == "true", which defaults to false when
        // the key is absent -- it cannot encode a DEFAULT True. This call site's `!= "false"` is
        // the correct true-by-default reading. Keep raw.
        internal static string ExecGetChanges(string args) => ChangeWatcher.GetChanges(
            JsonHelper.ExtractString(args, "clear") != "false");

        internal static string ExecResolveTestRequest(string args) => TestRunner.ResolveRequest(
                ResolveTestRequestArgs.RequestId(args));

        // NOT GetTestRunArgs.RunId: "get_test_run" is not in ArgContract's enabled set, so no
        // required-arg gate protects this handler -- RequireString would throw where this call
        // site today passes a possibly-null run_id through to TestRunner.GetRun. Keep raw.
        internal static string ExecGetTestRun(string args) => TestRunner.GetRun(
                JsonHelper.ExtractString(args, "run_id"),
                GetTestRunArgs.Compact(args));

        internal static string ExecListTestRuns(string args) => TestRunner.ListRuns(
                ExtractInt(args, "limit", 20));

        internal static string ExecCancelTestRun(string args) => TestRunner.CancelRun(
                CancelTestRunArgs.RunId(args));

        internal static string ExecGetTestResults(string args) => TestRunner.GetResults(
                GetTestResultsArgs.RunId(args));

        internal static string ExecGetTestProgress(string args) => TestRunner.GetProgress(
                GetTestProgressArgs.RunId(args));

        internal static string ExecGetTestCount(string args) => TestRunner.GetTestCount();

        internal static string ExecQueryState(string args) => GameStateHelper.Snapshot(
            QueryStateArgs.Queries(args));

        internal static string ExecGetFrameStats(string args) => ProfilerHelper.GetFrameStats();

        // "get_profile_context" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome -- no
        // generated accessor exists to migrate to.
        private static string ExecGetProfileContext(string args)
        {
            var sid = JsonHelper.ExtractString(args, "session");
            return sid != null
                ? Profiling.ProfileContextSerializer.Get(sid)
                : Profiling.ProfileContextSerializer.GetLatest();
        }

        internal static string ExecDebugAnimator(string args)
        {
            var go = ComponentSerializer.FindObjectOrThrow(DebugAnimatorArgs.Path(args));
            var animator = go.GetComponent<Animator>();
            if (animator == null) throw new ArgumentException("No Animator on " + go.name);
            return AnimatorHelper.GetState(animator);
        }

        internal static string ExecDebugPhysics(string args)
        {
            var go = ComponentSerializer.FindObjectOrThrow(DebugPhysicsArgs.Path(args));
            float radius = JsonHelper.ExtractFloat(args, "radius");
            return PhysicsHelper.GetState(go, radius > 0f ? radius : 5f);
        }

        internal static string ExecGetMemory(string args) =>
            MemoryHelper.GetSnapshot(GetMemoryArgs.Include(args) ?? "all");

        internal static string ExecSceneDiff(string args) => SceneDiffHelper.Diff();

        private static string ExecBatch(string args)
        {
            // timeout_ms has no BatchArgs accessor: "batch" is registered with a hand-written
            // required/optional contract that deliberately omits timeout_ms (ArgContract.cs's own
            // comment documents why "batch" is not in the enabled set), so the generated catalog
            // never declares this field either. Keep raw.
            var timeoutStr = JsonHelper.ExtractString(args, "timeout_ms");
            int timeoutMs = 25000;
            if (timeoutStr != null) int.TryParse(timeoutStr, out timeoutMs);
            bool atomic = BatchArgs.Atomic(args);
            bool validateAliases = BatchArgs.ValidateAliases(args);
            return BatchHelper.Execute(
                // NOT BatchArgs.Commands: "batch" is not in ArgContract's enabled set, so no
                // required-arg gate protects this handler -- RequireString would throw where this
                // call site today passes a possibly-null commands string through. Keep raw.
                JsonHelper.ExtractString(args, "commands"),
                BatchArgs.OnError(args) ?? "continue",
                timeoutMs, atomic, validateAliases);
        }

        internal static string ExecSpatialQuery(string args) => SpatialHelper.Execute(args);

        internal static string ExecExecuteCode(string args) => CodeExecutor.Execute(
            ExecuteCodeArgs.Code(args),
            ExecuteCodeArgs.UndoLabel(args) ?? "execute_code");

        internal static string ExecCompilePreflight(string args)
        {
            var r = CompilePreflightCommand.Execute(
                CompilePreflightArgs.FilePath(args),
                CompilePreflightArgs.NewContent(args));
            if (r.StartsWith("ERR")) throw new InvalidOperationException(r);
            return r;
        }

        internal static string ExecSerializedFieldRenameAudit(string args) =>
            SerializedFieldRenameAudit.Execute(args);

        // ---- Mutating command handlers ----

        internal static string ExecUndoLast(string args)
        {
            var turns = ExtractInt(args, "turns", 1);
            return UndoGroupStack.RevertLast(turns);
        }

        internal static string ExecInvokeMethod(string args) => RuntimeHelper.InvokeMethod(
            InvokeMethodArgs.Path(args),
            InvokeMethodArgs.Component(args),
            InvokeMethodArgs.Method(args),
            InvokeMethodArgs.Args(args));

        // "set_runtime_property" has no TOOL/PYTOOL/INTERNAL block in dsl/tools.biome -- no
        // generated accessor exists to migrate to (same gap as NavMeshHelper.cs).
        private static string ExecSetRuntimeProperty(string args) => RuntimeHelper.SetRuntimeProperty(
            JsonHelper.ExtractString(args, "path"),
            JsonHelper.ExtractString(args, "component"),
            JsonHelper.ExtractString(args, "field"),
            JsonHelper.ExtractString(args, "value"));

        internal static string ExecSetActive(string args)
        {
            var path   = SetActiveArgs.Path(args);
            var active = SetActiveArgs.Active(args);
            var result = ObjectManager.SetActive(path, active);
            // T16: pend receipt for set_active
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "modify", TargetType = "scene_object",
                Prop = "active", Before = (!active).ToString(), After = active.ToString(),
                Reversible = true
            });
            return result;
        }

        internal static string ExecWireEvent(string args)
        {
            var path            = WireEventArgs.Path(args);
            var component       = WireEventArgs.Component(args);
            var eventName       = WireEventArgs.Event(args);
            var target          = WireEventArgs.Target(args);
            var method          = WireEventArgs.Method(args);
            var argType         = WireEventArgs.ArgType(args) ?? "void";
            var argValue        = WireEventArgs.ArgValue(args);
            var targetCompType  = WireEventArgs.TargetComponentType(args);
            var paramTypes      = WireEventArgs.ParameterTypes(args);
            var result = ObjectManager.WireEvent(path, component, eventName, target, method,
                argType, argValue, targetCompType, paramTypes);
            // T16: pend receipt for wire_event
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "modify", TargetType = "component",
                Prop = eventName, After = "wired", Reversible = false
            });
            return result;
        }

        internal static string ExecUnwireEvent(string args) => ObjectManager.UnwireEvent(
            UnwireEventArgs.Path(args),
            UnwireEventArgs.Component(args),
            UnwireEventArgs.Event(args),
            // NOT UnwireEventArgs.Index: ObjectManager.UnwireEvent's 4th parameter is a string
            // (it hand-parses internally); the generated accessor returns int? via ParseOptInt,
            // a type mismatch. Keep raw.
            JsonHelper.ExtractString(args, "index"));

        internal static string ExecListEvents(string args) => ObjectManager.ListEvents(
            ListEventsArgs.Path(args),
            ListEventsArgs.Component(args),
            ListEventsArgs.Event(args));

        // parent is optional: null/omitted unparents to scene root (ObjectManager.SetParent
        // tolerates a null newParent — this is a valid, intentional operation, not an error).
        internal static string ExecSetParent(string args)
        {
            var path              = SetParentArgs.Path(args);
            var newParent         = SetParentArgs.Parent(args);
            // NOT SetParentArgs.WorldPositionStays: dsl/tools.biome declares
            // `ARG world_position_stays bool DEFAULT True`, but the generated bool accessor
            // pattern always defaults to false when absent -- it cannot encode DEFAULT True.
            // This call site's `!= "false"` is the correct true-by-default reading. Keep raw.
            var worldPositionStays = JsonHelper.ExtractString(args, "world_position_stays") != "false";
            // Capture old parent before mutation
            var go        = ComponentSerializer.FindObject(path);
            var oldParent = go?.transform.parent != null
                ? ComponentSerializer.GetPath(go.transform.parent.gameObject)
                : null;
            var result = ObjectManager.SetParent(path, newParent, worldPositionStays);
            // T16: pend receipt for set_parent
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "modify", TargetType = "scene_object",
                Prop = "parent", Before = oldParent, After = newParent, Reversible = true
            });
            return result;
        }

        internal static string ExecRenameObject(string args) => ObjectManager.RenameObject(
            RenameObjectArgs.Path(args),
            RenameObjectArgs.Name(args));

        internal static string ExecSetSiblingIndex(string args) => ObjectManager.SetSiblingIndex(
            SetSiblingIndexArgs.Path(args),
            // NOT SetSiblingIndexArgs.Index: that non-nullable int accessor's
            // JsonHelper.ExtractInt silently falls back to 0 on malformed input, while this call
            // site's int.Parse throws (surfaced as an error response by Process()'s catch).
            // Keep raw to preserve the throw-on-malformed behavior.
            int.Parse(JsonHelper.ExtractString(args, "index") ?? "0"));

        // NOT SetMaterialArgs.Path/.Color: "set_material" is not in ArgContract's enabled set,
        // so no required-arg gate protects this handler -- RequireString would throw where this
        // call site today passes possibly-null path/color through to ObjectManager.SetMaterial.
        // Keep raw. Shader is optional either way, so it migrates safely.
        internal static string ExecSetMaterial(string args) => ObjectManager.SetMaterial(
            JsonHelper.ExtractString(args, "path"),
            JsonHelper.ExtractString(args, "color"),
            SetMaterialArgs.Shader(args));

        internal static string ExecBake(string args)
        {
            var act = BakeArgs.Action(args) ?? "start";
            return BakeHelper.Execute(act, args);
        }

        internal static string ExecRegionClear(string args) => SpatialHelper.RegionClear(args);
    }
}
