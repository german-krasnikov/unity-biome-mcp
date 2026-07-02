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
        enum Phase { Ready, Moving, WaitingDelay, WaitingPoll, Simulating, Done }

        static PlaytestRunner()
        {
            _moveTcs = null;
            _activeSimulator = null;
        }

        public static void Run(string script, float globalTimeout, TaskCompletionSource<string> tcs)
        {
            if (_isRunning) { tcs.TrySetResult("ERROR: Playtest already running. Wait for completion."); return; }
            _isRunning = true;

            var guids = AssetDatabase.FindAssets("t:PlaytestConfig");
            PlaytestConfig config = null;
            if (guids.Length > 0)
                config = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            List<PlaytestStep> steps;
            try { steps = PlaytestParser.Parse(script); }
            catch (Exception e) { _isRunning = false; tcs.TrySetResult($"PARSE ERROR: {e.Message}"); return; }

            if (steps.Count == 0) { _isRunning = false; tcs.TrySetResult("PLAYTEST: 0 steps (0s)"); return; }

            var results = new List<string>();
            int stepIdx = 0;
            var phase = Phase.Ready;
            float phaseStart = 0;
            float testStart = Time.realtimeSinceStartup;
            int passed = 0, failed = 0;
            var state = new PlaytestState();
            DateTime stepStartUtc = DateTime.Now;

            void AdvanceStep()
            {
                CheckStepConsoleErrors(steps[stepIdx], stepIdx, stepStartUtc, results);
                stepIdx++;
                stepStartUtc = DateTime.Now;
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
                    results.Add($"[{stepIdx + 1}] ABORTED: global timeout {globalTimeout}s");
                    tcs.TrySetResult(BuildReport(results, passed, failed, testStart));
                    return;
                }

                // Check invariants and conserved constraints every tick
                state.CheckInvariants(config, Time.frameCount, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });
                state.CheckConserved(config, q => { var (p,c,f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p,c,f); });

                var step = steps[stepIdx];

                switch (phase)
                {
                    case Phase.Ready:
                        ExecuteStep(step, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state);
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
                        if (now - phaseStart > step.Timeout)
                        {
                            results.Add($"[{stepIdx + 1}] WAIT_UNTIL {step.Query}{step.Op}{step.Value} — TIMEOUT after {step.Timeout}s");
                            failed++;
                            phase = Phase.Done;
                            AdvanceStep();
                            return;
                        }
                        try
                        {
                            var (p, c, f) = PlaytestParser.ResolveQuery(step.Query, config);
                            var actual = ReadValue(p, c, f);
                            if (PlaytestParser.Compare(actual, step.Op, step.Value))
                            {
                                results.Add($"[{stepIdx + 1}] WAIT_UNTIL {step.Query}{step.Op}{step.Value} — PASS ({(now - phaseStart).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)");
                                passed++;
                                phase = Phase.Done;
                                AdvanceStep();
                            }
                        }
                        catch { /* keep polling */ }
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

        /// <summary>Execute a single synchronous step. Returns true if step completed (phase=Done), false if async.</summary>
        internal static bool ExecuteSyncStep(PlaytestStep step, PlaytestConfig config, List<string> results,
            ref int passed, ref int failed, int stepIdx, PlaytestState state = null)
        {
            var phase = Phase.Done;
            float phaseStart = 0;
            ExecuteStep(step, config, results, ref phase, ref phaseStart, ref passed, ref failed, stepIdx, state ?? new PlaytestState());
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
            var c = RuntimeHelper.FindComponentInternal(go, comp);
            if (c == null) throw new ArgumentException($"Component not found: {comp}");
            try { return RuntimeHelper.ReadFieldInternal(c, field); }
            catch { return RuntimeHelper.InvokeMethod(path, comp, field, ""); }
        }

        static void SetTimeScale(float scale)
        {
            var guids = AssetDatabase.FindAssets("t:PlaytestConfig");
            if (guids.Length > 0)
            {
                var cfg = AssetDatabase.LoadAssetAtPath<PlaytestConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
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
            }
            Time.timeScale = scale;
        }

        internal static string BuildReport(List<string> results, int passed, int failed, float startTime)
        {
            SetTimeScale(1f);
            var monitorReport = PlaytestMonitorRegistry.BuildReport();
            PlaytestMonitorRegistry.StopAll();
            var elapsed = Time.realtimeSinceStartup - startTime;
            var header = $"PLAYTEST: {passed}/{passed + failed} ({elapsed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s)";

            bool hasMonitor = !string.IsNullOrEmpty(monitorReport);
            if (failed == 0 && !results.Exists(r => r.Contains("SNAPSHOT") || r.Contains("ABORTED") || r.Contains("CONSOLE_ERR")))
                return hasMonitor ? header + " OK\n" + monitorReport : header + " OK";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(header);
            foreach (var r in results)
                if (r.Contains("FAIL") || r.Contains("ERR") || r.Contains("TIMEOUT") ||
                    r.Contains("SNAPSHOT") || r.Contains("LOG") || r.Contains("ABORTED"))
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
