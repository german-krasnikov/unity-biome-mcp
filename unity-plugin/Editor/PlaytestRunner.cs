using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static partial class PlaytestRunner
    {
        enum Phase { Ready, LoadingFresh, Moving, WaitingDelay, WaitingPoll, Simulating, WaitingCapturedDelta, CapturingFrames, Done }

        static PlaytestRunner()
        {
            _moveTcs = null;
            _activeSimulator = null;
        }

        public static void Run(string script, float globalTimeout, TaskCompletionSource<string> tcs,
            bool abortOnFail = false, bool snapshotOnFailure = false, bool fresh = false)
        {
            if (_isRunning) { tcs.TrySetResult("ERROR: Playtest already running. Wait for completion."); return; }
            _freshMode = fresh;
            _freshReloadDone = false;
            _isRunning = true;

            var guids = AssetDatabase.FindAssets("t:PlaytestConfig");
            PlaytestConfig config = null;
            if (guids.Length > 0)
                config = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            _cachedConfig = config; // cache for SetTimeScale (avoids repeated AssetDatabase.FindAssets)

            // Prepend Unity tags as VAL definitions (parse-time alias injection)
            var tagLines = string.Join("\n",
                UnityEditorInternal.InternalEditorUtility.tags
                    .Select(tag => $"VAL ${tag.Replace(" ", "_")} {tag}"));
            // Inject PlaytestConfig aliases before user script so INCLUDE/later VAL can override (last-write-wins)
            var cfgBlock = config?.aliases?.Count > 0
                ? PlaytestAliasHelpers.FormatVALBlock(config.aliases) + "\n"
                : "";
            var resolvedScript = tagLines + "\n" + cfgBlock + script;

            ParseResult parseResult = null;
            List<PlaytestStep> steps;
            PlaytestVarRegistry varRegistry;
            try
            {
                parseResult = PlaytestParser.Parse(resolvedScript);
                steps = parseResult.Steps;
                varRegistry = new PlaytestVarRegistry();
                if (parseResult.VarDefs != null)
                    foreach (var kv in parseResult.VarDefs)
                        varRegistry.Register(kv.Key, kv.Value);
            }
            catch (Exception e) { _isRunning = false; tcs.TrySetResult($"PARSE ERROR: {e.Message}"); return; }

            if (parseResult.Warnings != null)
                foreach (var w in parseResult.Warnings)
                    Debug.LogWarning($"[Playtest] {w}");

            if (steps.Count == 0) { _isRunning = false; tcs.TrySetResult("PLAYTEST: 0 steps (0s)"); return; }

            bool globalAbort = abortOnFail || parseResult.HasGlobalAbort;
            bool snapOnFail = snapshotOnFailure;
            float defaultTimeout = parseResult.DefaultTimeout > 0 ? parseResult.DefaultTimeout : 5f;

            var results = new List<string>();
            int stepIdx = 0;
            var phase = Phase.Ready;
            float phaseStart = 0;
            float testStart = Time.realtimeSinceStartup;
            int passed = 0, failed = 0;
            var state = new PlaytestState();
            DateTime stepStartUtc = DateTime.Now;
            PlaytestStep currentExpanded = null; // VAR-expanded clone of current step

            void AdvanceStep()
            {
                CheckStepConsoleErrors(steps[stepIdx], stepIdx, stepStartUtc, results);
                stepIdx++;
                stepStartUtc = DateTime.Now;
                currentExpanded = null;
                phase = Phase.Ready;
                if (stepIdx >= steps.Count)
                {
                    EditorApplication.update -= Tick;
                    _isRunning = false;
                    var report = BuildReport(results, passed, failed, testStart);
                    var stateReport = state.BuildReport();
                    if (stateReport != null) report += "\n" + stateReport;
                    tcs.TrySetResult(report);
                }
            }

            void Tick()
            {
                try
                {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Tick;
                    _isRunning = false;
                    results.Add($"[{stepIdx + 1}] ABORTED: Play Mode stopped");
                    tcs.TrySetResult(BuildReport(results, passed, failed, testStart));
                    return;
                }

                if (Time.realtimeSinceStartup - testStart > globalTimeout)
                {
                    EditorApplication.update -= Tick;
                    _isRunning = false;
                    if (globalAbort) EditorApplication.isPlaying = false;
                    results.Add($"[{stepIdx + 1}] ABORTED: global timeout {globalTimeout}s");
                    tcs.TrySetResult(BuildReport(results, passed, failed, testStart));
                    return;
                }

                // fresh mode: reload active scene before first step
                if (_freshMode && phase == Phase.Ready && stepIdx == 0 && !_freshReloadDone)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                        UnityEngine.SceneManagement.LoadSceneMode.Single);
                    phase = Phase.LoadingFresh;
                    phaseStart = Time.realtimeSinceStartup;
                    return;
                }
                if (phase == Phase.LoadingFresh)
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isLoaded)
                    { phase = Phase.Ready; _freshReloadDone = true; }
                    else if (Time.realtimeSinceStartup - phaseStart > 10f)
                    { phase = Phase.Ready; _freshReloadDone = true; }  // timeout — continue anyway
                    return;
                }

                // Check invariants and conserved constraints every tick
                state.CheckInvariants(config, Time.frameCount, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });
                state.CheckConserved(config, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });

                var step = steps[stepIdx];

                switch (phase)
                {
                    case Phase.Ready:
                        currentExpanded = varRegistry.HasAny ? varRegistry.ExpandStep(step) : step;
                        ExecuteStep(currentExpanded, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state, snapOnFail);
                        if (phase == Phase.Done) AdvanceStep();
                        break;

                    case Phase.Moving:
                        if (_moveTcs == null || !_moveTcs.Task.IsCompleted) return;
                        results.Add($"[{stepIdx + 1}] MOVE — {_moveTcs.Task.Result}");
                        passed++;
                        phase = Phase.Done;
                        AdvanceStep();
                        break;

                    case Phase.WaitingDelay:
                        if (Time.realtimeSinceStartup - phaseStart >= step.Delay)
                        {
                            results.Add($"[{stepIdx + 1}] WAIT {step.Delay}s — done");
                            passed++;
                            phase = Phase.Done;
                            AdvanceStep();
                        }
                        break;

                    case Phase.WaitingPoll:
                        float now = Time.realtimeSinceStartup;
                        var pollStep = currentExpanded ?? step; // use VAR-expanded version
                        var pollLabel = pollStep.Type == StepType.Assert ? "ASSERT" : "WAIT_UNTIL";
                        float effectiveTimeout = pollStep.HasExplicitTimeout ? pollStep.Timeout : defaultTimeout;
                        if (effectiveTimeout <= 0f) effectiveTimeout = 5f; // guard: SET_DEFAULT_TIMEOUT 0
                        if (now - phaseStart > effectiveTimeout)
                        {
                            bool abortThis = pollStep.AbortOnFail || globalAbort;
                            if (abortThis) EditorApplication.isPlaying = false;
                            string lastVal = "?";
                            try { var (lp, lc, lf) = PlaytestParser.ResolveQuery(pollStep.Query, config); lastVal = ReadValue(lp, lc, lf); } catch { }
                            var waitLine = $"[{stepIdx + 1}] {pollLabel} {pollStep.Query}{pollStep.Op}{pollStep.Value} — TIMEOUT after {effectiveTimeout}s (last: {lastVal})" + FormatProvenance(pollStep);
                            if (snapOnFail) waitLine += "\n" + BuildFailureSnapshot(pollStep, config);
                            results.Add(waitLine);
                            failed++;
                            phase = Phase.Done;
                            AdvanceStep();
                            return;
                        }
                        try
                        {
                            var (p, c, f) = PlaytestParser.ResolveQuery(pollStep.Query, config);
                            var actual = ReadValue(p, c, f);
                            bool met = EvalCompound(
                                PlaytestParser.Compare(actual, pollStep.Op, pollStep.Value),
                                pollStep.Queries, pollStep.BatchOps, pollStep.BatchValues, pollStep.IsOr,
                                q => { var (cp, cc, cf) = PlaytestParser.ResolveQuery(q, config); return ReadValue(cp, cc, cf); });
                            if (met)
                            {
                                _waitPollErrors = 0;
                                var logic = pollStep.Queries != null ? (pollStep.IsOr ? " (OR)" : " (AND)") : "";
                                results.Add($"[{stepIdx + 1}] {pollLabel} {pollStep.Query}{pollStep.Op}{pollStep.Value}{logic} — PASS ({(now - phaseStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)");
                                passed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (++_waitPollErrors >= 3)
                            {
                                var msg = ex.Message.Length > 120 ? ex.Message.Substring(0, 120) + "..." : ex.Message;
                                results.Add($"[{stepIdx + 1}] {pollLabel} {pollStep.Query}{pollStep.Op}{pollStep.Value} — ERROR after 3 consecutive exceptions: {ex.GetType().Name}: {msg}" + FormatProvenance(pollStep));
                                failed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                        }
                        break;

                    case Phase.Simulating:
                        float simNow = Time.realtimeSinceStartup;
                        bool simDone = false;
                        try { simDone = _activeSimulator?.Tick() ?? true; } catch { simDone = true; }
                        if (simDone || simNow - phaseStart >= step.Timeout)
                        {
                            var simReport = _activeSimulator?.Report() ?? "";
                            results.Add($"[{stepIdx + 1}] SIMULATE {step.SimulatorName} — done ({(simNow - phaseStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s){(simReport.Length > 0 ? " " + simReport : "")}");
                            _activeSimulator = null;
                            passed++;
                            phase = Phase.Done;
                            AdvanceStep();
                        }
                        break;

                    case Phase.WaitingCapturedDelta:
                    {
                        float wcdNow = Time.realtimeSinceStartup;
                        var wcdStep = currentExpanded ?? step;
                        try
                        {
                            var capQuery = state.GetCapturedQuery(wcdStep.Message);
                            var capBase = state.GetCapturedValue(wcdStep.Message);
                            var (wp, wc, wf) = PlaytestParser.ResolveQuery(capQuery, config);
                            var curStr = ReadValue(wp, wc, wf);
                            float.TryParse(curStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var curFloat);
                            bool met = EvalCapturedDelta(wcdStep.Op, wcdStep.Args, wcdStep.Value,
                                capBase, curFloat, ref _unchangedSince, wcdNow, wcdStep.Delay);
                            if (met)
                            {
                                results.Add($"[{stepIdx + 1}] WAIT_CAPTURED {wcdStep.Message} {wcdStep.Op} — PASS " +
                                    $"({(wcdNow - phaseStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s, " +
                                    $"was={capBase}, now={curStr})");
                                passed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                            else if (wcdNow - phaseStart > wcdStep.Timeout)
                            {
                                var wcdLine = $"[{stepIdx + 1}] WAIT_CAPTURED {wcdStep.Message} {wcdStep.Op} — TIMEOUT after {wcdStep.Timeout}s " +
                                    $"(was={capBase}, now={curStr})";
                                if (snapOnFail) wcdLine += "\n" + BuildFailureSnapshot(wcdStep, config);
                                results.Add(wcdLine);
                                failed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                        }
                        catch { /* keep polling */ }
                        break;
                    }

                    case Phase.CapturingFrames:
                    {
                        float cfNow = Time.realtimeSinceStartup;
                        int captured = state.GetFrameCount(_captureLabel);
                        bool firstFrame = captured == 0;
                        if (firstFrame || cfNow - _captureLastTime >= _captureInterval)
                        {
                            var path = ScreenshotCapture.CaptureToFile(cameraName: _captureCamera);
                            state.AddFrame(_captureLabel, path);
                            _captureLastTime = cfNow;
                            captured++;
                        }
                        if (captured >= _captureTarget)
                        {
                            var allPaths = state.GetFrames(_captureLabel);
                            string output = _captureMode == "strip"
                                ? FrameStitcher.StitchHorizontal(allPaths)
                                : string.Join(", ", allPaths);
                            results.Add($"[{stepIdx + 1}] CAPTURE_FRAMES {_captureLabel} ({_captureTarget}x{_captureInterval}s) → {output}");
                            passed++;
                            phase = Phase.Done;
                            AdvanceStep();
                        }
                        break;
                    }
                }
                }
                catch (Exception e)
                {
                    EditorApplication.update -= Tick;
                    _isRunning = false;
                    tcs.TrySetResult("ERROR: " + e.Message);
                }
            }

            EditorApplication.update += Tick;
        }

        static TaskCompletionSource<string> _moveTcs;
        static IPlaytestSimulator _activeSimulator;
        static bool _isRunning;
        // CAPTURE_FRAMES state
        static string _captureLabel;
        static float  _captureInterval;
        static int    _captureTarget;
        static float  _captureLastTime;
        static string _captureCamera;
        static string _captureMode;
        // Cached once per Run() to avoid AssetDatabase.FindAssets on every SetTimeScale call
        static PlaytestConfig _cachedConfig;
        // Tracks stable-start time for UNCHANGED OVER — reset each WaitingCapturedDelta entry
        static float _unchangedSince = -1f;
        // fresh mode — reload active scene before first step
        static bool _freshMode;
        static bool _freshReloadDone;
        // consecutive exceptions during WAIT_UNTIL polling — reset on success or new step
        static int _waitPollErrors;

        /// <summary>Execute a single synchronous step. Returns true if step completed (phase=Done), false if async.</summary>
        internal static bool ExecuteSyncStep(PlaytestStep step, PlaytestConfig config, List<string> results,
            ref int passed, ref int failed, int stepIdx, PlaytestState state = null, bool snapshotOnFailure = false)
        {
            var phase = Phase.Done;
            float phaseStart = 0;
            ExecuteStep(step, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state ?? new PlaytestState(), snapshotOnFailure);
            return phase == Phase.Done;
        }

        internal static string ResolveCharacterPath(PlaytestConfig config)
        {
            // Explicit config path takes priority
            if (config != null && !string.IsNullOrEmpty(config.characterPath))
                return config.characterPath;

            // Search scene for common character names
            foreach (var name in new[] { "Player", "GridPlayer", "Character", "Hero" })
            {
                var go = GameObject.Find(name);
                if (go != null) return "/" + name;
            }

            // Fall back to first object with the move component type
            var moveComp = config?.moveComponent;
            if (string.IsNullOrEmpty(moveComp)) return "/Player";
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.GetComponent(moveComp) != null)
                    return "/" + go.name;
            }

            return "/Player"; // last resort
        }

        internal static string ReadValue(string path, string comp, string field)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new ArgumentException($"Object not found: {path}");

            // GameObject property shorthands — no component lookup needed.
            // Form 1: /Path|activeSelf       → comp=activeSelf, field=""
            // Form 2: /Path|GameObject|activeSelf → comp=GameObject, field=activeSelf
            var goProp = (comp == "GameObject") ? field : comp;
            switch (goProp)
            {
                case "activeSelf":
                case "active":            return go.activeSelf.ToString().ToLowerInvariant();
                case "activeInHierarchy": return go.activeInHierarchy.ToString().ToLowerInvariant();
                case "tag":               return go.tag;
                case "layer":             return go.layer.ToString();
                case "name":              return go.name;
            }

            var c = RuntimeHelper.FindComponentInternal(go, comp);
            if (c == null) throw new ArgumentException($"Component not found: {comp}");
            var virt = ResolveVirtualField(c, field);
            if (virt != null) return virt;
            try { return RuntimeHelper.ReadFieldInternal(c, field); }
            catch { return RuntimeHelper.InvokeMethod(path, comp, field, ""); }
        }

        // Virtual "synthetic fields" on well-known components that have no matching C# property.
        private static string ResolveVirtualField(UnityEngine.Component comp, string field)
        {
            if (comp is UnityEngine.Animator anim)
            {
                if (field == "currentState" || field == "stateName")
                {
                    // ponytail: returns active clip name, not state machine node name; rename to currentClip if users confuse them
                    var clips = anim.GetCurrentAnimatorClipInfo(0);
                    return clips.Length > 0 ? clips[0].clip.name : "none";
                }
            }
            if (comp is UnityEngine.Rigidbody rb)
            {
                if (field == "speed")
#if UNITY_6000_0_OR_NEWER
                    return rb.linearVelocity.magnitude.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
#else
                    return rb.velocity.magnitude.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
#endif
            }
            if (comp is UnityEngine.Rigidbody2D rb2d)
            {
                if (field == "speed")
#if UNITY_6000_0_OR_NEWER
                    return rb2d.linearVelocity.magnitude.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
#else
                    return rb2d.velocity.magnitude.ToString("G4", System.Globalization.CultureInfo.InvariantCulture);
#endif
            }
            return null; // not a virtual field — fall through to ReadFieldInternal
        }

        static void SetTimeScale(float scale)
        {
            var cfg = _cachedConfig; // use config cached at Run() start — no AssetDatabase lookup
            if (cfg != null && !string.IsNullOrEmpty(cfg.timeScaleClass) && !string.IsNullOrEmpty(cfg.timeScaleProperty))
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType(cfg.timeScaleClass);
                    if (type == null) continue;
                    var prop = type.GetProperty(cfg.timeScaleProperty,
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null) { prop.SetValue(null, scale); return; }
                }
            }
            Time.timeScale = scale;
        }

        /// <summary>Evaluate primary + extra conditions with AND/OR reduction. Testable without runtime.</summary>
        internal static bool EvalCompound(bool primary, string[] queries, string[] ops, string[] vals,
            bool isOr, Func<string, string> readFn)
        {
            if (queries == null) return primary;
            if (!isOr && !primary) return false;  // AND: primary false → done
            if (isOr && primary) return true;      // OR: primary true → done
            bool met = primary;
            for (int i = 0; i < queries.Length; i++)
            {
                bool cond = PlaytestParser.Compare(readFn(queries[i]), ops[i], vals[i]);
                met = isOr ? met || cond : met && cond;
                if (!isOr && !met) return false;  // AND short-circuit
                if (isOr && met) return true;      // OR short-circuit
            }
            return met;
        }

        /// <summary>Pure delta evaluator for WAIT_CAPTURED. ref unchangedSince tracks stable duration for UNCHANGED OVER.</summary>
        internal static bool EvalCapturedDelta(string mode, string subOp, string threshold,
            float baseline, float current, ref float unchangedSince, float now, float overDuration)
        {
            switch (mode)
            {
                case "INCREASED":  return current > baseline;
                case "DECREASED":  return current < baseline;
                case "UNCHANGED":
                    if (current != baseline) { unchangedSince = -1f; return false; }
                    if (unchangedSince < 0f) unchangedSince = now;
                    return overDuration > 0f ? (now - unchangedSince >= overDuration) : true;
                case "INCREASED_BY":
                case "DECREASED_BY":
                    float delta = mode == "INCREASED_BY" ? (current - baseline) : (baseline - current);
                    return !string.IsNullOrEmpty(threshold) && !string.IsNullOrEmpty(subOp)
                        ? PlaytestParser.Compare(delta.ToString("F4", System.Globalization.CultureInfo.InvariantCulture), subOp, threshold)
                        : delta > 0f;
                default:
                    throw new ArgumentException($"Unknown WAIT_CAPTURED mode: {mode}");
            }
        }

        /// <summary>Formats provenance block for failure lines. Returns "" when no provenance is set.</summary>
        internal static string FormatProvenance(PlaytestStep step)
        {
            if (step.SourceFile == null && step.MacroStack == null && step.SectionContext == null)
                return "";
            var sb = new System.Text.StringBuilder();
            if (step.SourceFile != null)
                sb.Append($"\nsource: {step.SourceFile}:{step.SourceLine + 1}");
            if (step.MacroStack?.Length > 0)
                sb.Append($"\nmacro: {string.Join(" -> ", step.MacroStack)}");
            if (step.SectionContext != null)
                sb.Append($"\nsection: {step.SectionContext}");
            return sb.ToString();
        }

        internal static string BuildReport(List<string> results, int passed, int failed, float startTime)
        {
            SetTimeScale(1f);
            var monitorReport = PlaytestMonitorRegistry.BuildReport();
            PlaytestMonitorRegistry.StopAll();
            var elapsed = Time.realtimeSinceStartup - startTime;
            var header = $"PLAYTEST: {passed}/{passed + failed} ({elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)";

            bool hasMonitor = !string.IsNullOrEmpty(monitorReport);
            if (failed == 0 && !results.Exists(r => r.Contains("SNAPSHOT") || r.Contains("ABORTED") || r.Contains("CONSOLE_ERR") || r.StartsWith("---")))
                return hasMonitor ? header + " OK\n" + monitorReport : header + " OK";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(header);
            foreach (var r in results)
                if (r.Contains("FAIL") || r.Contains("ERR") || r.Contains("TIMEOUT") ||
                    r.Contains("SNAPSHOT") || r.Contains("LOG") || r.Contains("ABORTED") ||
                    r.StartsWith("---"))
                    sb.AppendLine(r);
            if (hasMonitor) sb.AppendLine(monitorReport);
            return sb.ToString().TrimEnd();
        }

        internal const int StepConsoleErrorMax = 3;

        internal static void CheckStepConsoleErrors(PlaytestStep step, int stepIdx, DateTime stepStart, List<string> results)
        {
            if (step.Type == StepType.AssertConsoleClean) return;
            var errors = ConsoleCapture.GetErrorsSince(stepStart, StepConsoleErrorMax);
            if (errors != null)
                results.Add($"[{stepIdx + 1}] CONSOLE_ERR during {step.Type}: {errors}");
        }
    }
}
