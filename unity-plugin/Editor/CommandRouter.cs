using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityMCP.Editor.TestRuns;

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
        internal static Func<bool> IsReadOnly = () => PortFileManager.ReadOnly;
        internal static Func<string, bool> IsToolEnabledFn = MCPSettings.IsToolEnabled;

        // P-322: dedup registry — prevents re-executing retried mutations after lost ACK.
        internal static DedupRegistry _dedupRegistry = new DedupRegistry();

        // T15: thread-local pending receipt — set by handlers, consumed by BuildResponse().
        [System.ThreadStatic]
        private static MutationReceipt? _pendingReceipt;

        internal static void PendReceipt(MutationReceipt r) => _pendingReceipt = r;

        // Last command name for UI display (MCPStatusWindow shows it when connected).
        internal static string LastCommandName { get; set; } = "";

        // Tools handled entirely by the Python MCP server — no C# handler exists.
        // Direct TCP callers that send these commands get an actionable error instead of
        // an opaque InvalidOperationException("Command not registered").
        internal static readonly HashSet<string> _PythonOnlyTools = new HashSet<string>
        {
            "animator_intent", "apply_scene_change", "apply_template", "ask",
            "auto_fix", "await_compile", "brief_build", "budget_status",
            "checkpoint_create", "checkpoint_restore", "configure_objects",
            "console_mark", "debug", "discover_tools", "do", "doctor",
            "get_changeset", "get_console_since", "get_metrics", "lint_playtest_suite",
            "list_connections", "list_skills", "list_templates", "load_session",
            "mcp_status", "navmesh_query", "permission_prompt", "reconnect_unity",
            "release_smoke", "resolve_tool_schema", "run_playtest_suite",
            "run_tests_wait", "save_session", "save_skill", "save_template",
            "scene_change_plan", "screenshot_baseline", "screenshot_compare",
            "set_llm_config", "set_properties", "setup_objects", "smart_build",
            "snapshot", "sync_unity", "ui_intent", "uitk_intent", "use_skill", "verify_after_change",
            "vfx_intent", "watch",
        };

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
        private static string CheckGuards(string id, string cmd, string argsJson, string chatMode = "")
        {
            if (!CommandRegistry.Ready)
                return JsonHelper.FormatBusyResponse(id, "Server initializing. Retry in 2s.", 2000);
            if (_PythonOnlyTools.Contains(cmd))
                return JsonHelper.FormatResponse(id, false, null,
                    $"'{cmd}' is a Python-only tool — use the MCP server (not direct TCP) to call it");
            var authError = SessionAuthorization.Check(chatMode, cmd, argsJson);
            if (authError != null)
                return JsonHelper.FormatResponse(id, false, null, authError);
            if (IsCompiling() && !IsAllowedDuringCompile(cmd))
                return JsonHelper.FormatBusyResponse(id, "Unity is compiling. Retry in 5s.", 5000);
            if (IsPlayMode() && IsMutatingCommand(cmd, argsJson)
                && !IsAllowedMutationInPlayMode(cmd, argsJson))
                return JsonHelper.FormatResponse(id, false, null,
                    "Play mode active — changes will be lost. Stop play mode first.");
            if (!IsPlayMode() && CommandRegistry.IsRuntime(cmd))
                return JsonHelper.FormatResponse(id, false, null, "Not in Play Mode. Use editor(action='play') first.");
            if (IsReadOnly() && IsMutatingCommand(cmd, argsJson))
                return JsonHelper.FormatResponse(id, false, null,
                    $"READ_ONLY_BLOCKED: '{cmd}' is a mutating command — this worker is read-only");
            if (!IsAlwaysAllowed(cmd) && !IsToolEnabledFn(cmd))
                return JsonHelper.FormatResponse(id, false, null, $"Tool '{cmd}' is disabled in settings");
            return null;
        }

        // editor excluded: play/stop/select don't corrupt scene data
        private static bool IsMutatingCommand(string cmd, string argsJson) =>
            CommandRegistry.IsMutating(cmd, argsJson);

        // These mutations intentionally target the live Play Mode context. Keep
        // this permission separate from read/write classification.
        internal static bool IsAllowedMutationInPlayMode(string cmd, string argsJson = null)
        {
            if (cmd == "set_parent" || cmd == "execute_code" || cmd == "screenshot" ||
                cmd == "wait_until")
                return true;
            if (cmd == "editor")
                // Toggling Source Patch intent touches no scene data — never
                // blocked by "changes will be lost, stop play mode first".
                return JsonHelper.ExtractString(argsJson, "action") == "mutation_mode";
            if (cmd != "profile") return false;
            var action = JsonHelper.ExtractString(argsJson, "action");
            return action == "start" || action == "stop";
        }

        public static string Process(string json)
        {
            SceneContext.InvalidateCache();
            try
            {
                var id = JsonHelper.ExtractString(json, "id");
                var cmd = JsonHelper.ExtractString(json, "cmd");
                LastCommandName = cmd ?? "";

                var retryOpId = JsonHelper.ExtractString(json, "retry_op_id");
                if (retryOpId != null)
                {
                    var cachedResult = _dedupRegistry.TryGetResult(retryOpId);
                    if (cachedResult != null)
                    {
                        // P-322 / MCP-IDEMP-026: inject dedup_applied flag for transparency.
                        var dedupJson = cachedResult.StartsWith("{")
                            ? cachedResult
                            : JsonHelper.FormatResponse(id, true, cachedResult, null);
                        UnityEngine.Debug.Assert(dedupJson.EndsWith("}"), $"Unexpected JSON tail in dedup response: ...{dedupJson[^Math.Min(20, dedupJson.Length)..]}");
                        return dedupJson.Substring(0, dedupJson.Length - 1) + ",\"dedup_applied\":true}";
                    }
                }

                var opId = JsonHelper.ExtractString(json, "op_id");

                var argsJson = JsonHelper.ExtractObject(json, "args");
                argsJson = AliasExpander.ExpandJson(argsJson);  // expand $sigils in args

                var guard = CheckGuards(id, cmd, argsJson);
                if (guard != null) return guard;

                bool mutating = IsMutatingCommand(cmd, argsJson);
                int groupId = -1;

                if (mutating)
                    groupId = UndoGroupHelper.OpenNamedGroup($"MCP: {cmd}");
                else
                    UndoGroupHelper.SetCommandFallback(cmd);

                if (CommandRegistry.TryGetFileHandler(cmd, out var fileHandler))
                {
                    var result = fileHandler(id, argsJson);
                    if (mutating)
                        UndoGroupHelper.CloseNamedGroup(groupId);
                    else
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
                string data;
                bool batchHasErrors = false;
                try
                {
                    data = ExecuteCommand(cmd, argsJson);
                    if (cmd == "batch") batchHasErrors = BatchHelper.HasErrors(data);
                }
                catch
                {
                    if (mutating) UndoGroupHelper.CloseNamedGroup(groupId);
                    throw;
                }

                if (mutating)
                {
                    UndoGroupHelper.CloseNamedGroup(groupId);
                    UndoGroupStack.Push(groupId, mutations: 1);
                    ChangeWatcher.RecordMutation($"MCP_{cmd.ToUpper()}");
                    var errors = ConsoleCapture.GetErrorsSince(before);
                    if (errors != null) data += "\n⚠ CONSOLE ERRORS:\n" + errors;
                    var suggestion = SuggestNext(cmd);
                    if (suggestion != null) data += $"\n[next: {suggestion}]";
                }
                else
                {
                    UndoGroupHelper.EndGroup();
                }
                if (batchHasErrors)
                {
                    var errorResponse = JsonHelper.FormatResponse(id, false, null, data);
                    if (opId != null)
                        _dedupRegistry.TryRegister(opId, errorResponse);
                    _pendingReceipt = null;
                    return errorResponse;
                }
                var response = BuildResponse(id, data, CommandRegistry.GetMaxResponseChars(cmd));
                if (opId != null)
                    _dedupRegistry.TryRegister(opId, response);
                return response;
            }
            catch (Exception e)
            {
                var cls = ErrorClassifier.Classify(e);
                if (cls == "VALIDATION")
                    Debug.LogWarning($"{BiomeLabel.Tag} {ErrorClassifier.FormatError(e)}");
                else
                    Debug.LogError($"{BiomeLabel.Tag} Command failed: {ErrorClassifier.FormatError(e)}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                var response = JsonHelper.FormatResponse(id, false, null, ErrorClassifier.FormatError(e));
                var opId = JsonHelper.ExtractString(json, "op_id");
                if (opId != null)
                    _dedupRegistry.TryRegister(opId, response);
                _pendingReceipt = null;
                return response;
            }
        }

        public static void ProcessAsync(string json, TaskCompletionSource<string> tcs, string chatMode = "")
        {
            SceneContext.InvalidateCache();
            try
            {
                var cmd = JsonHelper.ExtractString(json, "cmd");
                var id = JsonHelper.ExtractString(json, "id");
                LastCommandName = cmd ?? "";

                if (CommandRegistry.HasAsyncHandler(cmd, out var asyncHandler))
                {
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    argsJson = AliasExpander.ExpandJson(argsJson);  // expand $sigils in args
                    var guard = CheckGuards(id, cmd, argsJson, chatMode);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    UndoGroupHelper.SetCommandFallback(cmd);
                    asyncHandler(id, argsJson, tcs);
                    return;
                }

                var syncArgsJson = JsonHelper.ExtractObject(json, "args");
                syncArgsJson = AliasExpander.ExpandJson(syncArgsJson);
                var syncGuard = CheckGuards(id, cmd, syncArgsJson, chatMode);
                if (syncGuard != null) { tcs.TrySetResult(syncGuard); return; }
                tcs.TrySetResult(Process(json));
            }
            catch (Exception e)
            {
                var cls = ErrorClassifier.Classify(e);
                if (cls == "VALIDATION")
                    Debug.LogWarning($"{BiomeLabel.Tag} {ErrorClassifier.FormatError(e)}");
                else
                    Debug.LogError($"{BiomeLabel.Tag} Command failed: {ErrorClassifier.FormatError(e)}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, ErrorClassifier.FormatError(e)));
            }
        }

        private static void AsyncRunTests(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var mode = JsonHelper.ExtractString(argsJson, "mode");
            var group = JsonHelper.ExtractString(argsJson, "group");
            var filter = JsonHelper.ExtractString(argsJson, "filter");
            var requestId = JsonHelper.ExtractString(argsJson, "request_id");
            var selection = new TestRunSelection(
                TestRunSelectionArgs.ParseList(argsJson, "categories"),
                TestRunSelectionArgs.ParseList(argsJson, "assemblies"),
                TestRunSelectionArgs.ParseList(argsJson, "tests"));
            TestRunner.ExecuteWithSelection(mode, result =>
            {
                var (ok, text) = TestRunner.FinishRun(result);
                tcs.TrySetResult(ok ? BuildResponse(id, text) : JsonHelper.FormatResponse(id, false, null, text));
            }, group, filter, requestId, selection);
        }

        private static void AsyncBuild(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var action = JsonHelper.ExtractString(argsJson, "action");
            var target = JsonHelper.ExtractString(argsJson, "target");
            var scenes = JsonHelper.ExtractString(argsJson, "scenes");
            var path   = JsonHelper.ExtractString(argsJson, "path");
            var dev    = JsonHelper.ExtractString(argsJson, "dev") == "true";
            var inner  = new TaskCompletionSource<string>();
            BuildHelper.Execute(action, target, scenes, path, dev, inner);
            CompleteFromInner(id, inner.Task, tcs, "build",
                r => !r.StartsWith("err:"));
        }

        private static void AsyncPackage(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var action  = JsonHelper.ExtractString(argsJson, "action");
            var name    = JsonHelper.ExtractString(argsJson, "name");
            var version = JsonHelper.ExtractString(argsJson, "version");
            var query   = JsonHelper.ExtractString(argsJson, "query");
            var inner   = new TaskCompletionSource<string>();
            MainThreadDispatcher.Enqueue(() =>
                PackageManagerHelper.Execute(action, name, version, query, inner));
            CompleteFromInner(id, inner.Task, tcs, "package",
                r => !r.StartsWith("err:"));
        }

        // P0-50: internal/direct-only — never MCP-decorated, never batchable
        // (RegisterAsync alone makes CommandRegistry.IsBatchable return false).
        // Params are exactly path+content per §3.2 "only path, content,
        // operation/project identity" — the existing TCP envelope (id/op_id)
        // plus this connection's single-Editor binding already carry
        // operation/project identity, so no extra param is added here.
        private static void AsyncSourcePatchWrite(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            tcs.TrySetResult(BuildResponse(id, SourcePatchHost.WriteText(argsJson)));
        }

        // Bridges an inner async Task<string> to the outer TCS, formatting a fault
        // uniformly as "{label} error: ...". Collapses 4x identical ContinueWith copy-paste.
        // isSuccess: optional predicate on the result string; null = always success.
        private static void CompleteFromInner(string id, Task<string> inner, TaskCompletionSource<string> tcs, string label, Func<string, bool> isSuccess = null)
        {
            inner.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    var errMsg = t.Exception?.InnerException?.Message ?? t.Exception?.Message;
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, $"{label} error: {errMsg}"));
                }
                else if (isSuccess != null && !isSuccess(t.Result))
                {
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, t.Result));
                }
                else
                {
                    tcs.TrySetResult(BuildResponse(id, t.Result));
                }
            });
        }

        private static void AsyncWaitUntil(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var component = JsonHelper.ExtractString(argsJson, "component");
            var field = JsonHelper.ExtractString(argsJson, "field");
            var value = JsonHelper.ExtractString(argsJson, "value");
            var timeout = ExtractFloat(argsJson, "timeout", 5f);
            var negate = JsonHelper.ExtractString(argsJson, "negate") == "true";
            var abortOnFail = JsonHelper.ExtractString(argsJson, "abort_on_fail") == "true";
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.WaitUntil(path, component, field, value, timeout, negate, inner, abortOnFail);
            CompleteFromInner(id, inner.Task, tcs, "wait_until",
                r => !r.StartsWith("wait_until") && !r.StartsWith("err:"));
        }

        private static void AsyncMoveTo(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var position = JsonHelper.ExtractString(argsJson, "position");
            var timeout = ExtractFloat(argsJson, "timeout", 15f);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.MoveTo(path, position, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "move_to",
                r => !r.StartsWith("Error:") && !r.StartsWith("err:"));
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
            CompleteFromInner(id, inner.Task, tcs, "test_step",
                r => !r.StartsWith("Error:") && !r.StartsWith("err:"));
        }

        private static void AsyncRunPlaytest(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var pathArg   = JsonHelper.ExtractString(argsJson, "path");
            var scriptArg = JsonHelper.ExtractString(argsJson, "script");

            if (pathArg != null && scriptArg != null)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: use path or script, not both"));
                return;
            }

            string script;
            if (pathArg != null)
            {
                var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", pathArg));
                var projectRootNoSlash = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var projectRoot = projectRootNoSlash + Path.DirectorySeparatorChar;
                if (fullPath != projectRootNoSlash && !fullPath.StartsWith(projectRoot, StringComparison.Ordinal))
                {
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: path must be inside project"));
                    return;
                }
                if (!File.Exists(fullPath))
                {
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: file not found: " + pathArg));
                    return;
                }
                script = File.ReadAllText(fullPath, Encoding.UTF8);
                var defs = JsonHelper.ExtractString(argsJson, "defs");
                if (defs != null) script = defs + "\n" + script;
            }
            else if (scriptArg != null)
            {
                script = scriptArg;
            }
            else
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: script or path required"));
                return;
            }

            // B05: the Play-mode gate moved here, past parsing (registration no longer flags
            // run_playtest runtime:true). A header-less script keeps the exact legacy text
            // and timing (INV-005); "# @needs editmode" opts out, but never combined with a
            // fresh reload — fresh targets a Play session, which @needs editmode has none of.
            var header = PlaytestHeaderScanner.Scan(script);
            var fresh = JsonHelper.ExtractString(argsJson, "fresh") == "true";
            if (!header.NeedsEditmode && !EditorApplication.isPlaying)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null,
                    "Not in Play Mode. Use editor(action='play') first."));
                return;
            }
            if (header.NeedsEditmode && fresh)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null,
                    "err: fresh is incompatible with @needs editmode"));
                return;
            }
            if (header.NeedsEditmode)
            {
                // B07: an Edit-mode script mutates persisted scene state directly (no
                // Play-mode reload isolation), so refuse before Run() if any loaded
                // scene is dirty.
                var dirtyError = PlaytestIsolationScope.RefuseIfDirty();
                if (dirtyError != null)
                {
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, dirtyError));
                    return;
                }
            }

            var timeout = ExtractFloat(argsJson, "timeout", 120f);
            if (timeout <= 0) timeout = 120f;
            var abortOnFail = JsonHelper.ExtractString(argsJson, "abort_on_fail") == "true";
            var snapshotOnFailure = JsonHelper.ExtractString(argsJson, "snapshot_on_failure") == "true";
            var beforeHook = JsonHelper.ExtractString(argsJson, "before_hook");
            var afterHook = JsonHelper.ExtractString(argsJson, "after_hook");
            // B16: caller-selectable response representation; canonical JSON is always persisted
            // regardless of this value (see PlaytestRunner.FinishRun).
            var format = JsonHelper.ExtractString(argsJson, "format") ?? "text";

            if (!string.IsNullOrEmpty(beforeHook))
                CodeExecutor.Execute(beforeHook, "MCP before_hook");

            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(script, timeout, inner, abortOnFail, snapshotOnFailure, fresh,
                strict: pathArg != null, requiresPlayMode: !header.NeedsEditmode, format: format);

            // ContinueWith's default continuation runs on a ThreadPool thread.
            // MainThreadDispatcher.Enqueue is a thread-safe ConcurrentQueue.Enqueue, drained on
            // EditorApplication.update regardless of Editor focus (RELAY-FIX, commit 1bcc90b7).
            if (!string.IsNullOrEmpty(afterHook))
            {
                var capturedHook = afterHook;
                inner.Task.ContinueWith(_ =>
                    MainThreadDispatcher.Enqueue(() => CodeExecutor.Execute(capturedHook, "MCP after_hook")));
            }

            CompleteFromInner(id, inner.Task, tcs, "run_playtest",
                report => IsPlaytestSuccess(report, format));
        }

        // B17: the caller (AsyncRunPlaytest, above) already knows which representation it
        // requested — format is passed explicitly (R-07 explicit-over-implicit) rather than
        // re-derived here. The `{`-sniff below survives only as a fallback for a legacy/unknown
        // format argument; it is never reached from the one production call site, which always
        // resolves format to "text" or "json" before calling in (line ~493).
        private static bool IsPlaytestSuccess(string report, string format)
        {
            if (string.IsNullOrEmpty(report)) return false;
            if (format == "json") return IsPlaytestSuccessFromLedger(report);
            if (format == "text") return IsPlaytestSuccessFromText(report);
            return report.TrimStart().StartsWith("{", StringComparison.Ordinal)
                ? IsPlaytestSuccessFromLedger(report)
                : IsPlaytestSuccessFromText(report);
        }

        // B16's canonical JSON receipt: "outer":{"teardown_ok":...} plus one
        // {"ok":true/false,...} entry per step (PlaytestStepReceipt.ToJson). A step's `ok` is the
        // ledger fact — never re-derived from scanning report text (that was the B17 bug: a
        // step's source_file containing " OK" made the legacy substring check say "pass").
        private static bool IsPlaytestSuccessFromLedger(string report)
        {
            var outer = JsonHelper.ExtractObject(report, "outer");
            if (JsonHelper.ExtractString(outer, "teardown_ok") != "true") return false;

            var stepsArray = JsonHelper.ExtractArray(report, "steps");
            var pos = 0;
            string stepJson;
            while ((stepJson = JsonHelper.ExtractNextArrayObject(stepsArray, ref pos)) != null)
            {
                if (JsonHelper.ExtractString(stepJson, "ok") != "true") return false;
            }
            return true;
        }

        // INV-005 / v1 §41: the legacy text scan. Untouched by B17 — deleting it is gated on
        // Player/Wave-D parity, not this item.
        private static bool IsPlaytestSuccessFromText(string report)
        {
            if (string.IsNullOrEmpty(report)) return false;
            if (report.Contains(" OK")) return true;
            if (!report.StartsWith("PLAYTEST:", StringComparison.Ordinal)) return false;

            var firstLineEnd = report.IndexOf('\n');
            var firstLine = firstLineEnd >= 0 ? report.Substring(0, firstLineEnd) : report;
            var countStart = "PLAYTEST:".Length;
            while (countStart < firstLine.Length && char.IsWhiteSpace(firstLine[countStart]))
                countStart++;
            var countEnd = firstLine.IndexOf(' ', countStart);
            if (countEnd < 0) return false;

            var counts = firstLine.Substring(countStart, countEnd - countStart).Split('/');
            if (counts.Length != 2) return false;
            return int.TryParse(counts[0], out var passed)
                && int.TryParse(counts[1], out var total)
                && total > 0
                && passed == total;
        }

        private static void AsyncAskUser(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var questionsJson = JsonHelper.ExtractString(argsJson, "questions") ?? "[]";
            if (OnAskUser == null)
                Debug.LogWarning($"{BiomeLabel.Tag} ask_user: no listener — is chat window open?");
            // PendingAskRegistry.Ask never returns "Error:"/"err:" strings (cancelled → {"cancelled":true}),
            // but the predicate is safe and consistent with test_step/move_to.
            CompleteFromInner(id, PendingAskRegistry.Ask(questionsJson, OnAskUser), tcs, "ask_user",
                r => !r.StartsWith("Error:") && !r.StartsWith("err:"));
        }

        internal static string ExecuteCommand(string cmd, string args)
        {
            return CommandRegistry.Execute(cmd, args);
        }

        // B1 (review sprint v0.70): was a ~340-line God Method wiring all 93 commands inline.
        // Split into 4 themed bucket methods (CommandRouter.Registration.cs) by guard-flag
        // semantics, matching CheckGuards' own precedence order. Snapshot-guarded in full by
        // CommandRegistryCompletenessTests; per-bucket coverage in CommandRouterRegistrationTests.
        internal static void RegisterAll()
        {
            CommandRegistry.Clear();
            RegisterMetaCommands();
            RegisterReadCommands();
            RegisterMutatingCommands();
            RegisterAsyncCommands();

            // Watch system (Phase 3)
            WatchCommandHandler.RegisterAll();

            PluginRegistry.RegisterAllPlugins();

            // Eager-populate after ALL tools are registered (including plugins).
            // This is the correct site: RegisterAll is the last step in CommandRegistry.InitDefaults
            // and is always called on the main thread — safe to read EditorPrefs here.
            _enabledToolsCache = ExecGetEnabledTools();
            // Signal that all commands are registered — CheckGuards uses this to gate dispatch.
            CommandRegistry.Ready = true;
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
                _pendingReceipt = null;  // clear: file-offload drops receipt
                var filePath = FileOutputHelper.WriteText(data);
                return JsonHelper.FormatFileResponse(id, filePath);
            }
            data = ResponseGovernance.Truncate(data, maxResponseChars);
            var receiptJson = _pendingReceipt?.ToJson();
            _pendingReceipt = null;  // always clear after consuming
            return JsonHelper.FormatResponseWithReceipt(id, true, data, null, receiptJson);
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

        // C4 (review sprint v0.70): DRYs the comma-split-3-floats-with-fallback parse
        // previously duplicated verbatim in ObjectHandlers.cs's multi_view/single_view
        // screenshot branches. Never throws -- returns defaultVal on any parse failure
        // (NOT a drop-in for ValueParser.ParseVector3, which throws on malformed input).
        internal static Vector3 ExtractVector3(string json, string key, Vector3 defaultVal)
        {
            var s = JsonHelper.ExtractString(json, key);
            if (s == null) return defaultVal;
            var parts = s.Split(',');
            if (parts.Length == 3 &&
                float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                return new Vector3(x, y, z);
            return defaultVal;
        }

        private static string GetCapabilities()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"unity:{Application.unityVersion}");
            sb.AppendLine($"platform:{Application.platform}");
            sb.AppendLine($"scriptingBackend:{UnityEditor.PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(UnityEditor.EditorUserBuildSettings.selectedBuildTargetGroup))}");
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
            sb.AppendLine("mutating_cmds:" + string.Join(",",
                CommandRegistry.GetAllCommands().Where(c => CommandRegistry.IsMutating(c)).OrderBy(c => c)));
            sb.AppendLine("runtime_cmds:" + string.Join(",",
                CommandRegistry.GetAllCommands().Where(c => CommandRegistry.IsRuntime(c)).OrderBy(c => c)));
            sb.AppendLine($"plugin_version:{BiomeVersion.Plugin}");
            sb.AppendLine($"protocol:{BiomeVersion.Protocol}");
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
