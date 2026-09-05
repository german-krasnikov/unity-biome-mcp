using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static partial class PlaytestRunner
    {
        internal enum Phase { Ready, LoadingFresh, Moving, WaitingDelay, WaitingPoll, Simulating, WaitingCapturedDelta, CapturingFrames, WaitingStable, WaitingMcp, Done }
        internal enum StepAdvanceDecision { Continue, JumpToTeardown, AbortRun }

        static PlaytestRunner()
        {
            _moveTcs = null;
            _mcpTcs = null;
            _activeSimulator = null;
            ReapOrphanedSentinels(); // C05: converts sentinels orphaned by a killed domain into ABORTED receipts
        }

        // B13: $RUN_ID is a reserved system VAL — a user script cannot redeclare it (that would
        // let a script silently fork the run's own identity via VAL's last-write-wins semantics).
        // Checked against the raw `script` argument only, before any system VAL is concatenated in,
        // so the system's own injected line is never mistaken for a user declaration.
        // Review note (B13, closed as comment only): this regex runs BEFORE any INCLUDE file is
        // expanded/concatenated in, so an INCLUDEd file that itself declares `VAL $RUN_ID ...` is
        // NOT caught here and can still override the system alias via VAL's last-write-wins
        // semantics — a pre-INCLUDE check is a known, deliberate gap, not an oversight. Tracked for
        // Wave D's parser unification (INCLUDE handling moves earlier in the pipeline there).
        static readonly Regex _reservedRunIdVal = new Regex(@"^\s*VAL\s+\$RUN_ID(\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        internal const int RunIdHexLength = 8;

        /// <summary>Generates a fresh lowercase-hex run id for the parallel-safe $RUN_ID VAL.</summary>
        internal static string CreateRunId() => Guid.NewGuid().ToString("N").Substring(0, RunIdHexLength);

        // requiresPlayMode: threaded by B05's caller-side header gate (CommandRouter.AsyncRunPlaytest).
        // Consumed in Tick()'s Play-mode abort below (B06) — the caller decides via the parsed
        // header; Run()/Tick() never re-parses it.
        // runId: B13 — caller-preallocated identity (e.g. E02's async dispatch); null generates one.
        public static void Run(string script, float globalTimeout, TaskCompletionSource<string> tcs,
            bool abortOnFail = false, bool snapshotOnFailure = false, bool fresh = false,
            bool strict = false, bool requiresPlayMode = true, string runId = null, string format = "text")
        {
            if (_isRunning) { tcs.TrySetResult("ERROR: Playtest already running. Wait for completion."); return; }
            if (_reservedRunIdVal.IsMatch(script))
            {
                tcs.TrySetResult("PARSE ERROR: VAL $RUN_ID is a reserved system alias and cannot be declared by user script");
                return;
            }
            _freshMode = fresh;
            _freshReloadDone = false;
            _freshLoadInProgress = false;
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
            // B13: $RUN_ID goes last, right before the (already-verified-clean) user script, so
            // last-write-wins can never let an earlier tag/config alias clobber it.
            var effectiveRunId = runId ?? CreateRunId();
            var runIdLine = $"VAL $RUN_ID {effectiveRunId}\n";
            var resolvedScript = tagLines + "\n" + cfgBlock + runIdLine + script;

            ParseResult parseResult = null;
            List<PlaytestStep> steps;
            PlaytestVarRegistry varRegistry;
            int setupEndIdx = 0;       // exclusive; 0 = no setup
            int teardownStartIdx = 0;  // inclusive; 0 = no teardown (set after building combined list)
            try
            {
                parseResult = PlaytestParser.Parse(resolvedScript, strict: strict);
                // Build combined list: [setup | main | teardown]
                var setupSteps    = parseResult.SetupSteps    ?? new List<PlaytestStep>();
                var mainSteps     = parseResult.Steps;
                var teardownSteps = parseResult.TeardownSteps ?? new List<PlaytestStep>();
                var allSteps = new List<PlaytestStep>(setupSteps.Count + mainSteps.Count + teardownSteps.Count);
                allSteps.AddRange(setupSteps);
                setupEndIdx = allSteps.Count;
                allSteps.AddRange(mainSteps);
                teardownStartIdx = allSteps.Count;
                allSteps.AddRange(teardownSteps);
                steps = allSteps;
                varRegistry = new PlaytestVarRegistry();
                if (parseResult.VarDefs != null)
                    foreach (var kv in parseResult.VarDefs)
                        varRegistry.Register(kv.Key, kv.Value);
            }
            catch (Exception e)
            {
                CompleteRunCleanup();
                tcs.TrySetResult($"PARSE ERROR: {e.Message}");
                return;
            }

            // C02: post-parse, pre-execution Biome command policy for MCP steps. Reuses the
            // existing parseResult.Errors short-circuit below — no dispatch, no isolation
            // group, no side effect happens for a rejected script.
            parseResult.Errors = PlaytestMcpPolicy.Validate(steps, parseResult.Errors, isEditModeRun: !requiresPlayMode);

            if (parseResult.Warnings != null)
                foreach (var w in parseResult.Warnings)
                    Debug.LogWarning($"[Playtest] {w}");

            if (parseResult.Errors != null)
            {
                CompleteRunCleanup();
                tcs.TrySetResult($"PARSE ERROR: {string.Join("; ", parseResult.Errors)}");
                return;
            }

            if (steps.Count == 0)
            {
                CompleteRunCleanup();
                tcs.TrySetResult("PLAYTEST: 0 steps (0s)");
                return;
            }

            bool globalAbort = abortOnFail || parseResult.HasGlobalAbort;
            bool snapOnFail = snapshotOnFailure;
            float defaultTimeout = parseResult.DefaultTimeout > 0 ? parseResult.DefaultTimeout : 5f;

            // B08: an Edit-mode run mutates persisted scene state directly (no Play-mode
            // reload to fall back on), so it gets an Undo group that abort/timeout/outer-catch
            // revert below. Play-mode runs keep groupId == -1 — RevertGroup is then a no-op.
            int groupId = -1;
            if (!requiresPlayMode)
                groupId = PlaytestIsolationScope.OpenGroup("MCP Playtest (Edit Mode)");

            var results = new List<string>();
            int stepIdx = 0;
            var phase = Phase.Ready;
            float phaseStart = 0;
            float testStart = Time.realtimeSinceStartup;
            int passed = 0, failed = 0;
            var state = new PlaytestState();
            DateTime stepStartUtc = DateTime.Now;
            PlaytestStep currentExpanded = null; // VAR-expanded clone of current step
            int failedBeforeStep = 0;           // captured before each step to detect setup failures
            int passedBeforeStep = 0;           // C07: EXPECT_FAIL's pre-step baseline for `passed`
            PlaytestRunState.Begin(effectiveRunId, stepStartUtc); // B14: observable slice
            WriteSentinel(effectiveRunId); // C05: reload sentinel — deleted by FinishRun() below
            var stepReceipts = new List<PlaytestStepReceipt>(); // B16: structured step ledger
            bool abortedRun = false; // B16: outer.teardown_ok is false only when this is set

            void FinishRun()
            {
                EditorApplication.update -= Tick;
                _isRunning = false;
                PlaytestRunState.Finish(passed, failed); // B14: observable slice
                var report = BuildReport(results, passed, failed, testStart);
                var stateReport = state.BuildReport();
                if (stateReport != null) report += "\n" + stateReport;

                // B16: canonical JSON is always built and persisted regardless of requested
                // format — only the RETURNED representation below depends on it.
                var elapsedSeconds = Time.realtimeSinceStartup - testStart;
                var sceneClean = requiresPlayMode || PlaytestIsolationScope.RefuseIfDirty() == null;
                var json = BuildJsonReport(effectiveRunId, stepReceipts, passed, failed, elapsedSeconds,
                    teardownOk: !abortedRun, sceneClean: sceneClean, textReport: report);
                var receiptPath = ProjectRelativePath(PlaytestReceiptStore.ReceiptPath(effectiveRunId));
                Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
                File.WriteAllText(receiptPath, json, new System.Text.UTF8Encoding(false));
                DeleteSentinel(effectiveRunId); // C05: run reached FinishRun() normally — no orphan to reap

                tcs.TrySetResult(format == "json" ? json : report);
            }

            void AdvanceStep()
            {
                var completedStep = steps[stepIdx];
                bool rawPassed = failed == failedBeforeStep; // step's own outcome, before inversion
                // C07: EXPECT_FAIL inversion runs BEFORE CheckStepConsoleErrors below, on
                // purpose — the console-error channel is structurally separate and must never
                // be inverted, so a step that expected-fails for its own DSL reason but also
                // logs a genuine Debug.LogError still reports that error as a real failure.
                if (completedStep.ExpectFail)
                    (passed, failed) = ApplyExpectFail(passedBeforeStep, failedBeforeStep, passed, failed, true);
                if (CheckStepConsoleErrors(completedStep, stepIdx, stepStartUtc, results))
                    failed++;
                // B16: one structured receipt per completed step, built at this single choke
                // point (every step-completion call site funnels through AdvanceStep) so every
                // step type gets a ledger entry without touching each phase's own results.Add
                // site. C07: expected_fail now reflects the real EXPECT_FAIL modifier.
                stepReceipts.Add(new PlaytestStepReceipt(
                    stepIdx, completedStep.Type.ToString(), (DateTime.Now - stepStartUtc).TotalMilliseconds,
                    completedStep.SourceFile, completedStep.SourceLine,
                    rawPassed: rawPassed, expectedFail: completedStep.ExpectFail));
                stepIdx++;
                var decision = DetermineStepAdvance(
                    globalAbort, failedBeforeStep, failed, stepIdx, setupEndIdx, teardownStartIdx);
                if (decision == StepAdvanceDecision.AbortRun)
                {
                    EditorApplication.isPlaying = false;
                    PlaytestIsolationScope.RevertGroup(groupId);
                    abortedRun = true; // B16: outer.teardown_ok reflects this
                    FinishRun();
                    return;
                }
                // Without global fail-fast, a setup failure skips main steps and runs teardown.
                if (decision == StepAdvanceDecision.JumpToTeardown)
                {
                    results.Add("--- SETUP FAILED: skipping main steps");
                    stepIdx = teardownStartIdx;
                }
                stepStartUtc = DateTime.Now;
                currentExpanded = null;
                phase = Phase.Ready;
                PlaytestRunState.Update(stepIdx, passed, failed); // B14: observable slice
                if (stepIdx >= steps.Count)
                    FinishRun();
            }

            void Tick()
            {
                try
                {
                if (requiresPlayMode && !EditorApplication.isPlaying)
                {
                    // B16: routed through FinishRun() (was a hand-inlined duplicate of its body)
                    // so the canonical JSON receipt is built/persisted here too, and B14's
                    // PlaytestRunState.Finish is no longer silently skipped on this abort path.
                    results.Add($"[{stepIdx + 1}] ABORTED: Play Mode stopped");
                    abortedRun = true;
                    FinishRun();
                    return;
                }

                if (Time.realtimeSinceStartup - testStart > globalTimeout)
                {
                    // B16: see note above — routed through FinishRun() instead of duplicating it.
                    if (globalAbort) EditorApplication.isPlaying = false;
                    PlaytestIsolationScope.RevertGroup(groupId);
                    results.Add($"[{stepIdx + 1}] ABORTED: global timeout {globalTimeout}s");
                    abortedRun = true;
                    FinishRun();
                    return;
                }

                // fresh mode: reload active scene before first step
                // G1: _freshLoadInProgress guards against calling LoadScene twice
                if (_freshMode && phase == Phase.Ready && stepIdx == 0 && !_freshReloadDone && !_freshLoadInProgress)
                {
                    _freshLoadInProgress = true;
                    PrepareForFreshLoad(); // P-109/P-291: stop monitors before scene objects are destroyed
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
                    { _freshLoadInProgress = false; phase = Phase.Ready; _freshReloadDone = true; stepStartUtc = DateTime.Now; }
                    else if (Time.realtimeSinceStartup - phaseStart > 10f)
                    { _freshLoadInProgress = false; phase = Phase.Ready; _freshReloadDone = true; stepStartUtc = DateTime.Now; }  // timeout — continue anyway
                    return;
                }

                // Check invariants and conserved constraints every tick.
                // P-291: skip during LoadingFresh — scene objects are destroyed; any
                // ReadValue call would throw MissingReferenceException.
                if (phase != Phase.LoadingFresh)
                {
                    state.CheckInvariants(config, Time.frameCount, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });
                    state.CheckConserved(config, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });
                }

                // P-305: poll running min/max trackers every tick (no-op when no trackers active)
                state.PollExtrema(q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });

                var step = steps[stepIdx];

                switch (phase)
                {
                    case Phase.Ready:
                        // C07: this is the one entry point every step type passes through before
                        // ExecuteStep can move it into any other phase (Moving/WaitingPoll/
                        // WaitingMcp/...), so capturing the before-step baseline here — instead of
                        // in AdvanceStep itself — makes it reachable from every phase path,
                        // including a step that only reaches AdvanceStep many ticks later via polling.
                        failedBeforeStep = failed; // capture before step so AdvanceStep can detect failures
                        passedBeforeStep = passed; // mirrors failedBeforeStep for EXPECT_FAIL's inversion
                        currentExpanded = varRegistry.HasAny ? varRegistry.ExpandStep(step) : step;
                        ExecuteStep(currentExpanded, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state, snapOnFail, effectiveRunId, varRegistry);
                        if (phase == Phase.Done) AdvanceStep();
                        break;

                    case Phase.Moving:
                        if (_moveTcs == null || !_moveTcs.Task.IsCompleted) return;
                        results.Add($"[{stepIdx + 1}] MOVE — {_moveTcs.Task.Result}");
                        passed++;
                        phase = Phase.Done;
                        AdvanceStep();
                        break;

                    case Phase.WaitingMcp:
                        if (_mcpTcs == null || !_mcpTcs.Task.IsCompleted) return;
                        var mcpResponse = _mcpTcs.Task.Result;
                        _mcpTcs = null;
                        ApplyMcpResult(currentExpanded ?? step, stepIdx, mcpResponse, results, varRegistry, ref passed, ref failed);
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
                        // G7: re-expand VAR runtime aliases each tick so WAIT_UNTIL sees live values.
                        var pollStep = varRegistry.HasAny ? varRegistry.ExpandStep(step) : (currentExpanded ?? step);
                        var pollLabel = pollStep.Type == StepType.Assert ? "ASSERT" : "WAIT_UNTIL";
                        float effectiveTimeout = pollStep.HasExplicitTimeout ? pollStep.Timeout : defaultTimeout;
                        if (effectiveTimeout <= 0f) effectiveTimeout = 5f; // guard: SET_DEFAULT_TIMEOUT 0
                        if (now - phaseStart > effectiveTimeout)
                        {
                            bool abortThis = ShouldStopPlayModeOnPollTimeout(pollStep.AbortOnFail, globalAbort);
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

                    case Phase.WaitingStable:
                    {
                        // P-110: poll the rolling window; succeed when range ≤ DELTA over the full OVER window
                        float wsNow = Time.realtimeSinceStartup;
                        float wsDelta = float.TryParse(step.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsedDelta) ? parsedDelta : 0f;
                        float wsWindow = step.Delay; // OVER duration
                        float wsTimeout = step.Timeout > 0 ? step.Timeout : 5f;

                        try
                        {
                            bool stable = state.PollStable(wsNow, wsDelta, wsWindow,
                                q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });
                            if (stable)
                            {
                                results.Add($"[{stepIdx + 1}] WAIT_STABLE {step.Query} DELTA {step.Value} OVER {wsWindow} — PASS ({(wsNow - phaseStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)");
                                passed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                            else if (wsNow - phaseStart > wsTimeout)
                            {
                                results.Add($"[{stepIdx + 1}] WAIT_STABLE {step.Query} — TIMEOUT after {wsTimeout}s (value still oscillating)" + FormatProvenance(step));
                                failed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                        }
                        catch { /* keep polling */ }
                        break;
                    }
                }
                }
                catch (Exception e)
                {
                    EditorApplication.update -= Tick;
                    _isRunning = false;
                    PlaytestIsolationScope.RevertGroup(groupId);
                    CompleteRunCleanup();
                    tcs.TrySetResult("ERROR: " + e.Message);
                }
            }

            EditorApplication.update += Tick;
        }

        static TaskCompletionSource<string> _moveTcs;
        static TaskCompletionSource<string> _mcpTcs;
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
        static bool _freshLoadInProgress; // G1: guard against calling LoadScene twice

        // ── Test hooks ───────────────────────────────────────────────────────

        /// <summary>
        /// Stop all active monitors before a fresh scene load.
        /// Called by Tick() immediately before SceneManager.LoadScene to prevent
        /// MissingReferenceException callbacks after GameObjects are destroyed (P-109/P-291).
        /// Internal so unit tests can verify it clears the monitor registry.
        /// </summary>
        internal static void PrepareForFreshLoad()
        {
            PlaytestMonitorRegistry.StopAll();
        }

        /// <summary>True when fresh mode should trigger a new scene load (all guards clear).</summary>
        internal static bool ShouldStartFreshLoad => _freshMode && !_freshReloadDone && !_freshLoadInProgress;

        /// <summary>Set fresh-mode state for unit testing without Play Mode.</summary>
        internal static void SetFreshTestState(bool freshMode, bool reloadDone, bool loadInProgress)
        {
            _freshMode = freshMode;
            _freshReloadDone = reloadDone;
            _freshLoadInProgress = loadInProgress;
        }

        internal static void CompleteRunCleanupForTests() => CompleteRunCleanup();
        // consecutive exceptions during WAIT_UNTIL polling — reset on success or new step
        static int _waitPollErrors;

        internal static StepAdvanceDecision DetermineStepAdvance(
            bool globalAbort, int failedBeforeStep, int failedAfterStep,
            int nextStepIndex, int setupEndIndex, int teardownStartIndex)
        {
            bool stepFailed = failedAfterStep > failedBeforeStep;
            if (globalAbort && stepFailed)
                return StepAdvanceDecision.AbortRun;
            if (stepFailed && setupEndIndex > 0 &&
                nextStepIndex <= setupEndIndex && nextStepIndex < teardownStartIndex)
                return StepAdvanceDecision.JumpToTeardown;
            return StepAdvanceDecision.Continue;
        }

        internal static bool ShouldStopPlayModeOnPollTimeout(bool stepAbortOnFail, bool globalAbort)
            => stepAbortOnFail || globalAbort;

        /// <summary>
        /// C06 — pure EXPECT_FAIL inversion. Compares the run's passed/failed counters before and
        /// after one step to see which counter the step itself moved, then (when expectFail is
        /// set) flips that single outcome: a raw failure becomes a pass, a raw pass becomes a
        /// failure (an expected-fail step that unexpectedly passes IS a failure). Neither counter
        /// moving (e.g. a step still polling) is left alone. No Editor context — same style as
        /// <see cref="DetermineStepAdvance"/>.
        /// </summary>
        internal static (int passed, int failed) ApplyExpectFail(
            int passedBefore, int failedBefore, int passedAfter, int failedAfter, bool expectFail)
        {
            if (!expectFail) return (passedAfter, failedAfter);
            if (passedAfter > passedBefore) return (passedAfter - 1, failedAfter + 1);
            if (failedAfter > failedBefore) return (passedAfter + 1, failedAfter - 1);
            return (passedAfter, failedAfter);
        }

        /// <summary>Execute a single synchronous step. Returns true if step completed (phase=Done), false if async.</summary>
        internal static bool ExecuteSyncStep(PlaytestStep step, PlaytestConfig config, List<string> results,
            ref int passed, ref int failed, int stepIdx, PlaytestState state = null, bool snapshotOnFailure = false,
            string runId = null, PlaytestVarRegistry varRegistry = null)
        {
            var phase = Phase.Done;
            float phaseStart = 0;
            ExecuteStep(step, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state ?? new PlaytestState(), snapshotOnFailure, runId, varRegistry);
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
                // TODO: replace with ComponentSerializer.FindObject(name) if bracket-named characters are ever needed
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

            // UIDocument (Variant B): comp="UIDocument", field packed as "selector|veField".
            // UIPanelHost.ResolveRoot is called inside UIElementHelper.ReadValue — supports both
            // UIDocument (6.0) and PanelRenderer (6.4+) transparently.
            if (comp == "UIDocument")
            {
                var veParts = field.Split('|');
                var selector = veParts[0];
                var veField = veParts.Length > 1 ? veParts[1] : "";
                return UIElementHelper.ReadValue(go, selector, veField);
            }

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
            var virt = RuntimeHelper.TryResolveVirtualField(c, field);
            if (virt != null) return virt;
            try { return RuntimeHelper.ReadFieldInternal(c, field); }
            catch { return RuntimeHelper.InvokeMethod(path, comp, field, ""); }
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
            CompleteRunCleanup();
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

        /// <summary>
        /// B16: canonical JSON receipt — schema_version/run_id/passed/failed/duration_seconds/
        /// steps (one PlaytestStepReceipt each) / outer (teardown_ok/scene_clean) / text_report
        /// (the exact legacy text, for the durable byte-exact representation). Hand-rolled
        /// StringBuilder, matching PlayerPlaytestReceipts.cs's style — no System.Text.Json.
        /// </summary>
        internal static string BuildJsonReport(string runId, List<PlaytestStepReceipt> steps, int passed,
            int failed, float elapsedSeconds, bool teardownOk, bool sceneClean, string textReport)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\"schema_version\":1,");
            sb.Append("\"run_id\":\"").Append(runId).Append("\",");
            sb.Append("\"passed\":").Append(passed).Append(',');
            sb.Append("\"failed\":").Append(failed).Append(',');
            sb.Append("\"duration_seconds\":")
              .Append(elapsedSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"steps\":[");
            for (var i = 0; i < steps.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(steps[i].ToJson());
            }
            sb.Append("],");
            sb.Append("\"outer\":{\"teardown_ok\":").Append(teardownOk ? "true" : "false")
              .Append(",\"scene_clean\":").Append(sceneClean ? "true" : "false").Append("},");
            sb.Append("\"text_report\":\"").Append(PlaytestStepReceipt.EscapeJsonString(textReport)).Append("\"");
            sb.Append('}');
            return sb.ToString();
        }

        static void CompleteRunCleanup()
        {
            _isRunning = false;
            SetTimeScale(1f);
            PlaytestMonitorRegistry.StopAll();
            _activeSimulator = null;
            _moveTcs = null;
            _mcpTcs = null;
            _cachedConfig = null;
            _freshMode = false;
            _freshReloadDone = false;
            _freshLoadInProgress = false;
            _waitPollErrors = 0;
            _captureLabel = null;
            _captureCamera = null;
            _captureMode = null;
            _captureInterval = 0f;
            _captureTarget = 0;
            _captureLastTime = 0f;
            _unchangedSince = -1f;
        }

        internal const int StepConsoleErrorMax = 3;

        internal static bool CheckStepConsoleErrors(PlaytestStep step, int stepIdx, DateTime stepStart, List<string> results)
        {
            if (step.Type == StepType.AssertConsoleClean) return false;
            var errors = ConsoleCapture.GetErrorsSince(stepStart, StepConsoleErrorMax);
            if (errors == null) return false;
            results.Add($"[{stepIdx + 1}] CONSOLE_ERR during {step.Type}: {errors}");
            return true;
        }
    }
}
