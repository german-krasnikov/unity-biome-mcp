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
using UnityMCP.Editor.Roslyn;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Playtest.Core;

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
        // an opaque InvalidOperationException("Command not registered"). Source of truth is
        // the generated PythonOnlyTools.Names (PYTOOL lines of dsl/tools.biome).
        internal static readonly HashSet<string> _PythonOnlyTools = PythonOnlyTools.Names;

        // Feature 3: recent command history for smart checkpoint naming
        internal static readonly Queue<string> _recentCmds = new Queue<string>();

        private static void TrackCommand(string cmd)
        {
            _recentCmds.Enqueue(cmd);
            if (_recentCmds.Count > 3) _recentCmds.Dequeue();
        }

        // Feature 2: suggest next tool for mutating commands (NEXT lines of dsl/tools.biome, ToolUi.NextAfter)
        internal static string SuggestNext(string cmd) => ToolUi.NextAfter(cmd);

        // Returns error response string if a guard blocks the command, null otherwise.
        // Which guard stops a call, and in which order, is the decision guardVerdict (site top) of dsl/decisions/guards.logic; GuardFacts
        // states the world to it. What each verdict says to the caller is here. A verdict that has no answer here fails closed.
        private static string CheckGuards(string id, string cmd, string argsJson, string chatMode = "")
        {
            var authError = SessionAuthorization.Check(chatMode, cmd, argsJson);
            var verdict = Decisions.Guards.Policy.GuardVerdict(Decisions.GuardFacts.Top(
                ready: CommandRegistry.Ready, pythonOnly: _PythonOnlyTools.Contains(cmd), authDenied: authError != null,
                compiling: IsCompiling(), allowedDuringCompile: IsAllowedDuringCompile(cmd), playMode: IsPlayMode(),
                mutating: IsMutatingCommand(cmd, argsJson), allowedInPlay: IsAllowedMutationInPlayMode(cmd, argsJson),
                runtime: CommandRegistry.IsRuntime(cmd), readOnly: IsReadOnly(), alwaysAllowed: IsAlwaysAllowed(cmd),
                toolEnabled: IsToolEnabledFn(cmd)));
            switch (verdict)
            {
                case Decisions.Guards.Policy.GuardVerdictValues.Ok:
                    return null;
                case Decisions.Guards.Policy.GuardVerdictValues.Initializing:
                    return JsonHelper.FormatBusyResponse(id, "Server initializing. Retry in 2s.", 2000);
                case Decisions.Guards.Policy.GuardVerdictValues.PythonOnly:
                    return JsonHelper.FormatResponse(id, false, null,
                        $"'{cmd}' is a Python-only tool — use the MCP server (not direct TCP) to call it");
                case Decisions.Guards.Policy.GuardVerdictValues.AuthDenied:
                    return JsonHelper.FormatResponse(id, false, null, authError);
                case Decisions.Guards.Policy.GuardVerdictValues.Compiling:
                    return JsonHelper.FormatBusyResponse(id, "Unity is compiling. Retry in 5s.", 5000);
                case Decisions.Guards.Policy.GuardVerdictValues.PlayBlocked:
                    return JsonHelper.FormatResponse(id, false, null,
                        "Play mode active — changes will be lost. Stop play mode first.");
                case Decisions.Guards.Policy.GuardVerdictValues.NotInPlay:
                    return JsonHelper.FormatResponse(id, false, null, "Not in Play Mode. Use editor(action='play') first.");
                case Decisions.Guards.Policy.GuardVerdictValues.ReadOnlyBlocked:
                    return JsonHelper.FormatResponse(id, false, null,
                        $"READ_ONLY_BLOCKED: '{cmd}' is a mutating command — this worker is read-only");
                case Decisions.Guards.Policy.GuardVerdictValues.Disabled:
                    return JsonHelper.FormatResponse(id, false, null, $"Tool '{cmd}' is disabled in settings");
                default:
                    throw new InvalidOperationException($"guardVerdict gave '{verdict}' for the top site: CheckGuards has no answer for it");
            }
        }

        private static bool IsMutatingCommand(string cmd, string argsJson) =>
            CommandRegistry.IsMutating(cmd, argsJson);

        // These mutations intentionally target the live Play Mode context. Keep
        // this permission separate from read/write classification.
        internal static bool IsAllowedMutationInPlayMode(string cmd, string argsJson = null)
        {
            return CommandRegistry.IsPlayOk(cmd);
        }

        public static string Process(string json)
        {
            SceneContext.InvalidateCache();
            try
            {
                // id/cmd/op_id/retry_op_id are the outer TCP envelope's own keys (the {id,cmd,args}
                // wrapper), not a tool ARG declared in dsl/tools.biome -- no {Tool}Args class
                // exists for them (or could, since they are not scoped to any one tool). Keep raw
                // everywhere they are read from `json`, here and in Process/ProcessAsync's catch
                // blocks and in ProcessAsync below.
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

                var contractErr = ArgContract.Check(cmd, argsJson);
                if (contractErr != null) return JsonHelper.FormatResponse(id, false, null, contractErr);

                // undo: a mutating call opens an Undo group and is recorded, unless its registration says noUndo:
                // (a mutating command whose effect is no Undo-recorded scene change, step 7.2 M3.0).
                bool undo = IsMutatingCommand(cmd, argsJson) && !CommandRegistry.IsNoUndo(cmd);
                int groupId = -1;

                if (undo)
                    groupId = UndoGroupHelper.OpenNamedGroup($"MCP: {cmd}");
                else
                    UndoGroupHelper.SetCommandFallback(cmd);

                if (CommandRegistry.TryGetFileHandler(cmd, out var fileHandler))
                {
                    var result = fileHandler(id, argsJson);
                    if (undo)
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
                    if (undo) UndoGroupHelper.CloseNamedGroup(groupId);
                    throw;
                }

                if (undo)
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
                if (cls == "VALIDATION" || cls == "UNAVAILABLE")
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
                    var contractErr = ArgContract.Check(cmd, argsJson);
                    if (contractErr != null) { tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, contractErr)); return; }
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
                if (cls == "VALIDATION" || cls == "UNAVAILABLE")
                    Debug.LogWarning($"{BiomeLabel.Tag} {ErrorClassifier.FormatError(e)}");
                else
                    Debug.LogError($"{BiomeLabel.Tag} Command failed: {ErrorClassifier.FormatError(e)}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, ErrorClassifier.FormatError(e)));
            }
        }

        internal static void AsyncRunTests(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var mode = RunTestsArgs.Mode(argsJson);
            var group = RunTestsArgs.Group(argsJson);
            var filter = RunTestsArgs.Filter(argsJson);
            var requestId = RunTestsArgs.RequestId(argsJson);
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

        internal static void AsyncBuild(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // "build" is in ArgContract's enabled set, so CommandValidator.Validate already
            // rejected a missing "action" before this handler runs -- BuildArgs.Action's
            // RequireString never actually throws here.
            var action = BuildArgs.Action(argsJson);
            var target = BuildArgs.Target(argsJson);
            var scenes = BuildArgs.Scenes(argsJson);
            var path   = BuildArgs.Path(argsJson);
            var dev    = BuildArgs.Dev(argsJson);
            var inner  = new TaskCompletionSource<string>();
            BuildHelper.Execute(action, target, scenes, path, dev, inner);
            CompleteFromInner(id, inner.Task, tcs, "build",
                r => !r.StartsWith("err:"));
        }

        internal static void AsyncPackage(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // "package" is in ArgContract's enabled set -- required "action" already validated.
            var action  = PackageArgs.Action(argsJson);
            var name    = PackageArgs.Name(argsJson);
            var version = PackageArgs.Version(argsJson);
            var query   = PackageArgs.Query(argsJson);
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

        internal static void AsyncWaitUntil(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // "wait_until" is in ArgContract's enabled set -- required path/component/field/value
            // already validated, so WaitUntilArgs' RequireString accessors never throw here.
            var path = WaitUntilArgs.Path(argsJson);
            var component = WaitUntilArgs.Component(argsJson);
            var field = WaitUntilArgs.Field(argsJson);
            var value = WaitUntilArgs.Value(argsJson);
            // NOT WaitUntilArgs.Timeout: that non-nullable accessor's JsonHelper.ExtractFloat
            // fallback is a hardcoded 0f on missing input, not the DSL's `DEFAULT 5.0` -- keep
            // the local ExtractFloat(..., 5f) wrapper (see ProfileRecorder.cs for the same rule).
            var timeout = ExtractFloat(argsJson, "timeout", 5f);
            var negate = WaitUntilArgs.Negate(argsJson);
            var abortOnFail = WaitUntilArgs.AbortOnFail(argsJson);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.WaitUntil(path, component, field, value, timeout, negate, inner, abortOnFail);
            CompleteFromInner(id, inner.Task, tcs, "wait_until",
                r => !r.StartsWith("wait_until") && !r.StartsWith("err:"));
        }

        internal static void AsyncMoveTo(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // "move_to" is in ArgContract's enabled set -- required path/position already
            // validated, so MoveToArgs' RequireString accessors never throw here.
            var path = MoveToArgs.Path(argsJson);
            var position = MoveToArgs.Position(argsJson);
            // NOT MoveToArgs.Timeout -- same 0f-vs-DEFAULT-15.0 mismatch as AsyncWaitUntil above.
            var timeout = ExtractFloat(argsJson, "timeout", 15f);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.MoveTo(path, position, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "move_to",
                r => !r.StartsWith("Error:") && !r.StartsWith("err:"));
        }

        internal static void AsyncTestStep(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // "test_step" is in ArgContract's enabled set -- required path/position already
            // validated, so TestStepArgs' RequireString accessors never throw here.
            var path = TestStepArgs.Path(argsJson);
            var position = TestStepArgs.Position(argsJson);
            var checksBefore = TestStepArgs.ChecksBefore(argsJson) ?? "";
            var checksAfter = TestStepArgs.ChecksAfter(argsJson) ?? "";
            // NOT TestStepArgs.WaitAfter/Timeout -- same 0f-vs-DEFAULT mismatch as above.
            var waitAfter = ExtractFloat(argsJson, "wait_after", 0.5f);
            var timeout = ExtractFloat(argsJson, "timeout", 15f);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.TestStep(path, position, checksBefore, checksAfter, waitAfter, timeout, inner);
            CompleteFromInner(id, inner.Task, tcs, "test_step",
                r => !r.StartsWith("Error:") && !r.StartsWith("err:"));
        }

        // E02: everything run_playtest and start_playtest share — building the script from
        // path/script, and the header/dirty gates before either ever calls PlaytestRunner.Run.
        // Extracted from AsyncRunPlaytest (pure Extract-Method, no behavior change) so the two
        // entry points cannot drift on gate logic; they differ only in how they wire completion.
        private struct PlaytestRunRequest
        {
            internal string Script, Format, BeforeHook, AfterHook;
            internal float Timeout;
            internal bool AbortOnFail, SnapshotOnFailure, Fresh, Strict, RequiresPlayMode;
        }

        // Returns false when a gate already set tcs — caller must return immediately without
        // touching PlaytestRunner.
        private static bool TryBuildPlaytestRunRequest(string id, string argsJson,
            TaskCompletionSource<string> tcs, out PlaytestRunRequest request)
        {
            request = default;
            // Shared by both AsyncRunPlaytest (run_playtest) and AsyncStartPlaytest
            // (start_playtest) -- RunPlaytestArgs and StartPlaytestArgs are generated as two
            // separate classes (one per DSL tool block) but their accessor bodies are byte-
            // identical plain JsonHelper.ExtractString/ExtractFloat reads, so using RunPlaytestArgs
            // here for both callers extracts the same wire keys either way.
            var pathArg   = RunPlaytestArgs.Path(argsJson);
            var scriptArg = RunPlaytestArgs.Script(argsJson);

            if (pathArg != null && scriptArg != null)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: use path or script, not both"));
                return false;
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
                    return false;
                }
                if (!File.Exists(fullPath))
                {
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: file not found: " + pathArg));
                    return false;
                }
                script = File.ReadAllText(fullPath, Encoding.UTF8);
                var defs = RunPlaytestArgs.Defs(argsJson);
                if (defs != null) script = defs + "\n" + script;
            }
            else if (scriptArg != null)
            {
                script = scriptArg;
            }
            else
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: script or path required"));
                return false;
            }

            // B05: the Play-mode gate moved here, past parsing (registration no longer flags
            // run_playtest runtime:true). A header-less script keeps the exact legacy text
            // and timing (INV-005); "# @needs editmode" opts out, but never combined with a
            // fresh reload — fresh targets a Play session, which @needs editmode has none of.
            var header = PlaytestHeaderScanner.Scan(script);
            var fresh = RunPlaytestArgs.Fresh(argsJson);
            if (!header.NeedsEditmode && !EditorApplication.isPlaying)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null,
                    "Not in Play Mode. Use editor(action='play') first."));
                return false;
            }
            if (header.NeedsEditmode && fresh)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null,
                    "err: fresh is incompatible with @needs editmode"));
                return false;
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
                    return false;
                }
            }

            // NOT RunPlaytestArgs.Timeout -- same 0f-vs-DEFAULT-120.0 mismatch as AsyncWaitUntil.
            var timeout = ExtractFloat(argsJson, "timeout", 120f);
            if (timeout <= 0) timeout = 120f;
            request = new PlaytestRunRequest
            {
                Script = script,
                Timeout = timeout,
                AbortOnFail = RunPlaytestArgs.AbortOnFail(argsJson),
                SnapshotOnFailure = RunPlaytestArgs.SnapshotOnFailure(argsJson),
                Fresh = fresh,
                Strict = pathArg != null,
                RequiresPlayMode = !header.NeedsEditmode,
                // B16: caller-selectable response representation; canonical JSON is always
                // persisted regardless of this value (see PlaytestRunner.FinishRun).
                Format = RunPlaytestArgs.Format(argsJson) ?? "text",
                BeforeHook = RunPlaytestArgs.BeforeHook(argsJson),
                AfterHook = RunPlaytestArgs.AfterHook(argsJson),
            };
            return true;
        }

        internal static void AsyncRunPlaytest(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            if (!TryBuildPlaytestRunRequest(id, argsJson, tcs, out var req)) return;

            if (!string.IsNullOrEmpty(req.BeforeHook))
                CodeExecutor.Execute(req.BeforeHook, "MCP before_hook");

            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(req.Script, req.Timeout, inner, req.AbortOnFail, req.SnapshotOnFailure, req.Fresh,
                strict: req.Strict, requiresPlayMode: req.RequiresPlayMode, format: req.Format);

            // ContinueWith's default continuation runs on a ThreadPool thread.
            // MainThreadDispatcher.Enqueue is a thread-safe ConcurrentQueue.Enqueue, drained on
            // EditorApplication.update regardless of Editor focus (RELAY-FIX, commit 1bcc90b7).
            if (!string.IsNullOrEmpty(req.AfterHook))
            {
                var capturedHook = req.AfterHook;
                inner.Task.ContinueWith(_ =>
                    MainThreadDispatcher.Enqueue(() => CodeExecutor.Execute(capturedHook, "MCP after_hook")));
            }

            CompleteFromInner(id, inner.Task, tcs, "run_playtest",
                report => IsPlaytestSuccess(report, req.Format));
        }

        // E02: non-blocking dispatch — returns a "run_id=xxxxxxxx" sentinel immediately instead
        // of waiting for the whole playtest to finish. Shares every gate with AsyncRunPlaytest via
        // TryBuildPlaytestRunRequest; differs only in how it wires PlaytestRunner.Run's completion.
        private static void AsyncStartPlaytest(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            if (!TryBuildPlaytestRunRequest(id, argsJson, tcs, out var req)) return;

            var runId = PlaytestRunner.CreateRunId();
            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(req.Script, req.Timeout, inner, req.AbortOnFail, req.SnapshotOnFailure, req.Fresh,
                strict: req.Strict, requiresPlayMode: req.RequiresPlayMode, runId: runId, format: req.Format);

            // Run()'s early-return guards (already running / reserved $RUN_ID / parse error /
            // 0 steps) call inner.TrySetResult(...) synchronously before PlaytestRunState.Begin
            // or any artifact is ever touched — the freshly-generated runId was never real, so it
            // must never be surfaced here. Surface Run()'s own error text instead.
            if (inner.Task.IsCompleted)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, inner.Task.Result));
                return;
            }

            tcs.TrySetResult(BuildResponse(id, "run_id=" + runId));
        }

        // E03: compact poll — the read half of the async start/poll pair. First matches
        // PlaytestRunState.Current: still running → compact "phase=running|step=N/M|elapsed_ms=X"
        // sentinel; already terminal → E01's TerminalText, verbatim (never re-rendered through a
        // second formatter). Once in-memory state no longer matches (cleared by a domain reload,
        // or genuinely a different/older run), falls back to the canonical receipt on disk —
        // validated against a corrupt/mismatched run_id before trusting it. An id that is neither
        // the active run nor has a receipt is genuinely unknown and fails closed.
        private static void AsyncGetPlaytestRun(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var runId = GetPlaytestRunArgs.RunId(argsJson);
            if (string.IsNullOrEmpty(runId))
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, "err: run_id required"));
                return;
            }

            var current = PlaytestRunState.Current;
            if (current.RunId == runId)
            {
                if (current.Phase == PlaytestRunState.RunPhase.Running)
                {
                    var elapsedMs = (int)(DateTime.Now - current.StartUtc).TotalMilliseconds;
                    tcs.TrySetResult(BuildResponse(id,
                        $"phase=running|step={current.StepIndex + 1}/{current.TotalSteps}|elapsed_ms={elapsedMs}"));
                    return;
                }
                if (current.TerminalText != null)
                {
                    tcs.TrySetResult(BuildResponse(id, current.TerminalText));
                    return;
                }
                // Terminal in PlaytestRunState but TerminalText somehow unset — fall through to
                // the receipt below rather than trust a half-populated in-memory state.
            }

            var receiptPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));
            if (!File.Exists(receiptPath))
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, $"err: unknown playtest run_id: {runId}"));
                return;
            }

            // "run_id"/"text_report" here read the on-disk playtest receipt JSON, not this
            // command's own args -- there is no {Tool}Args accessor for a receipt file's fields.
            var json = File.ReadAllText(receiptPath, Encoding.UTF8);
            if (JsonHelper.ExtractString(json, "run_id") != runId)
            {
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, $"err: corrupt receipt for run_id: {runId}"));
                return;
            }
            tcs.TrySetResult(BuildResponse(id, JsonHelper.ExtractString(json, "text_report")));
        }

        // B17: the caller (AsyncRunPlaytest, above) already knows which representation it requested -- format is passed explicitly
        // (R-07 explicit-over-implicit). A format that is neither "text" nor "json" falls back to a `{`-sniff; the one production call
        // site never passes one. Whether a report passes is the decision passes of dsl/decisions/verdict.logic; what this plane reads
        // out of the report (the PLAYTEST: header at the very start, the ledger of B16, F7's fail-closed cases) is Decisions/PlaytestFacts.cs.
        private static bool IsPlaytestSuccess(string report, string format) =>
            Decisions.Verdict.Policy.Passes(Decisions.PlaytestFacts.Read(report, format));

        internal static void AsyncAskUser(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            // Finding 2 (PR-05 05.2): fail fast, BEFORE creating a PendingAskRegistry entry —
            // nobody can ever answer a question with no subscriber, so don't make the caller
            // wait out the 300s client timeout to learn that.
            if (OnAskUser == null)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} ask_user: no listener — is chat window open?");
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null,
                    "ask_user unavailable: no interaction provider registered (Chat window not open)"));
                return;
            }
            // ARG adoption (Step 4, Batch 8): intentionally NOT migrated to AskUserArgs.Questions.
            // "ask_user" is not in ArgContract's enabled set, so CommandValidator.Validate never
            // runs for it -- no required-arg gate protects this handler. AskUserArgs.Questions
            // calls JsonHelper.RequireString, which throws on a missing/empty key; this call site
            // instead needs the graceful `?? "[]"` fallback it already has. Swapping would turn a
            // missing "questions" key into an unhandled exception for a command with no gate to
            // prevent one from reaching here.
            var questionsJson = JsonHelper.ExtractString(argsJson, "questions") ?? "[]";
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
            ToolBindings.RegisterAll();
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

        // ExtractInt/ExtractFloat/ExtractVector3 (this method and the two below) ARE the
        // generic caller-supplied-default parsing layer -- the same role JsonHelper.ParseOpt*
        // plays for the generated {Tool}Args accessors, just with a non-null fallback instead of
        // returning null. Reused across many call sites with per-call-site defaults (e.g.
        // ExecSearchScene's limit=50 vs ExecSearchContext's limit=30); binding any one of them to
        // a single tool's accessor would remove the shared default-parsing behavior. Keep raw.
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
