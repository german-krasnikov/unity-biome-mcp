using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            if (!CommandRegistry.Ready)
                return JsonHelper.FormatBusyResponse(id, "Server initializing. Retry in 2s.", 2000);
            if (IsCompiling() && !IsAllowedDuringCompile(cmd))
                return JsonHelper.FormatBusyResponse(id, "Unity is compiling. Retry in 5s.", 5000);
            if (IsPlayMode() && IsMutatingCommand(cmd) && cmd != "set_parent")
                return JsonHelper.FormatResponse(id, false, null,
                    "Play mode active — changes will be lost. Stop play mode first.");
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
                argsJson = AliasExpander.ExpandJson(argsJson);  // expand $sigils in args

                bool mutating = IsMutatingCommand(cmd);
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
                    UndoGroupStack.Push(groupId);
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
                    return JsonHelper.FormatResponse(id, false, null, data);
                return BuildResponse(id, data, CommandRegistry.GetMaxResponseChars(cmd));
            }
            catch (Exception e)
            {
                var cls = ErrorClassifier.Classify(e);
                if (cls == "VALIDATION")
                    Debug.LogWarning($"[MCP] {ErrorClassifier.FormatError(e)}");
                else
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
                    argsJson = AliasExpander.ExpandJson(argsJson);  // expand $sigils in args
                    asyncHandler(id, argsJson, tcs);
                    return;
                }

                tcs.TrySetResult(Process(json));
            }
            catch (Exception e)
            {
                var cls = ErrorClassifier.Classify(e);
                if (cls == "VALIDATION")
                    Debug.LogWarning($"[MCP] {ErrorClassifier.FormatError(e)}");
                else
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
                var (ok, text) = TestRunner.FinishRun(result);
                tcs.TrySetResult(ok ? BuildResponse(id, text) : JsonHelper.FormatResponse(id, false, null, text));
            }, group, filter);
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

            var timeout = ExtractFloat(argsJson, "timeout", 120f);
            if (timeout <= 0) timeout = 120f;
            var abortOnFail = JsonHelper.ExtractString(argsJson, "abort_on_fail") == "true";
            var snapshotOnFailure = JsonHelper.ExtractString(argsJson, "snapshot_on_failure") == "true";
            var fresh = JsonHelper.ExtractString(argsJson, "fresh") == "true";
            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(script, timeout, inner, abortOnFail, snapshotOnFailure, fresh);
            CompleteFromInner(id, inner.Task, tcs, "run_playtest",
                IsPlaytestSuccess);
        }

        private static bool IsPlaytestSuccess(string report)
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
                Debug.LogWarning("[MCP] ask_user: no listener — is chat window open?");
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
