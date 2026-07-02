using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityMCP.Editor
{
    internal static partial class PlaytestRunner
    {
        static void ExecuteStep(PlaytestStep step, PlaytestConfig config, List<string> results,
            ref Phase phase, ref float phaseStart, ref int passed, ref int failed, int stepIdx, PlaytestState state)
        {
            var label = $"[{stepIdx + 1}]";
            switch (step.Type)
            {
                case StepType.Assert:
                    var (ap, ac, af) = PlaytestParser.ResolveQuery(step.Query, config);
                    try
                    {
                        var actual = ReadValue(ap, ac, af);
                        var ok = PlaytestParser.Compare(actual, step.Op, step.Value);
                        results.Add($"{label} ASSERT {step.Query}{step.Op}{step.Value} — {(ok ? "PASS" : "FAIL")} ({actual})");
                        if (ok) passed++; else failed++;
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} ASSERT {step.Query} — ERR: {e.Message}");
                        failed++;
                    }
                    phase = Phase.Done;
                    break;

                case StepType.AssertConsoleClean:
                    var logs = ConsoleCapture.GetLogs(20, "error");
                    // Filter out ignored patterns
                    if (!string.IsNullOrEmpty(logs) && step.Queries != null && step.Queries.Length > 0)
                    {
                        var logLines = logs.Split('\n');
                        var filtered = logLines.Where(l => !step.Queries.Any(p => l.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0));
                        logs = string.Join("\n", filtered).Trim();
                    }
                    var clean = string.IsNullOrEmpty(logs);
                    results.Add($"{label} ASSERT_CONSOLE_CLEAN — {(clean ? "PASS" : "FAIL")}" + (clean ? "" : $"\n{logs}"));
                    if (clean) passed++; else failed++;
                    phase = Phase.Done;
                    break;

                case StepType.Wait:
                    phase = Phase.WaitingDelay;
                    phaseStart = Time.realtimeSinceStartup;
                    break;

                case StepType.WaitUntil:
                    phase = Phase.WaitingPoll;
                    phaseStart = Time.realtimeSinceStartup;
                    break;

                case StepType.Move:
                    phase = Phase.Moving;
                    var charPath = step.Path ?? ResolveCharacterPath(config);
                    _moveTcs = new TaskCompletionSource<string>();
                    RuntimeHelper.MoveTo(charPath, $"{step.Position.x},{step.Position.y},{step.Position.z}", 15f, _moveTcs, config);
                    break;

                case StepType.Snapshot:
                    var queries = step.Queries?.Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q));
                    var snap = GameStateHelper.Snapshot(string.Join(",", queries ?? Array.Empty<string>()));
                    results.Add($"{label} SNAPSHOT\n{snap}");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.Invoke:
                    try
                    {
                        var r = RuntimeHelper.InvokeMethod(step.Path, step.Component, step.Method, step.Args);
                        results.Add($"{label} INVOKE {step.Method} — {r}");
                        passed++;
                    }
                    catch (Exception e) { results.Add($"{label} INVOKE {step.Method} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Set:
                    try
                    {
                        var r = RuntimeHelper.SetRuntimeProperty(step.Path, step.Component, step.Method, step.Args);
                        results.Add($"{label} SET {r}");
                        passed++;
                    }
                    catch (Exception e) { results.Add($"{label} SET — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Log:
                    results.Add($"{label} LOG {step.Message}");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.TimeScale:
                    SetTimeScale(step.Delay);
                    results.Add($"{label} TIMESCALE {step.Delay}");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.Teleport:
                    try
                    {
                        var tgo = ComponentSerializer.FindObject(step.Path);
                        if (tgo == null) throw new ArgumentException($"Object not found: {step.Path}");
                        tgo.transform.position = step.Position;
                        Physics.SyncTransforms();
                        results.Add($"{label} TELEPORT {step.Path} → {step.Position}");
                        passed++;
                    }
                    catch (Exception e) { results.Add($"{label} TELEPORT — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.AssertNear:
                    try
                    {
                        var goA = ComponentSerializer.FindObject(step.Path);
                        var goB = ComponentSerializer.FindObject(step.Value);
                        if (goA == null) throw new ArgumentException($"Object not found: {step.Path}");
                        if (goB == null) throw new ArgumentException($"Object not found: {step.Value}");
                        var dist = Vector3.Distance(goA.transform.position, goB.transform.position);
                        var nearOk = dist <= step.Delay;
                        results.Add($"{label} ASSERT_NEAR {step.Path} {step.Value} {step.Delay} — {(nearOk ? "PASS" : "FAIL")} (dist={dist.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)})");
                        if (nearOk) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_NEAR — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.AssertBatch:
                    try
                    {
                        int bPassed = 0, bFailed = 0;
                        var bDetails = new System.Text.StringBuilder();
                        for (int bi = 0; bi < step.Queries.Length; bi++)
                        {
                            var (bp, bc, bf) = PlaytestParser.ResolveQuery(step.Queries[bi], config);
                            try
                            {
                                var actual = ReadValue(bp, bc, bf);
                                var ok = PlaytestParser.Compare(actual, step.BatchOps[bi], step.BatchValues[bi]);
                                if (ok) bPassed++;
                                else { bFailed++; bDetails.AppendLine($"  {step.Queries[bi]} — FAIL ({actual})"); }
                            }
                            catch (Exception e) { bFailed++; bDetails.AppendLine($"  {step.Queries[bi]} — ERR: {e.Message}"); }
                        }
                        int total = step.Queries.Length;
                        var bResult = bFailed == 0 ? $"{label} ASSERT_BATCH {bPassed}/{total}" : $"{label} ASSERT_BATCH {bPassed}/{total}\n{bDetails.ToString().TrimEnd()}";
                        results.Add(bResult);
                        if (bFailed == 0) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_BATCH — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Capture:
                    try
                    {
                        var (cp, cc, cf) = PlaytestParser.ResolveQuery(step.Query, config);
                        var capVal = ReadValue(cp, cc, cf);
                        float.TryParse(capVal, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var capFloat);
                        state.Capture(step.Message, step.Query, capFloat);
                        results.Add($"{label} CAPTURE {step.Message}={capVal}");
                        passed++;
                    }
                    catch (Exception e) { results.Add($"{label} CAPTURE {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.AssertCaptured:
                    try
                    {
                        var capQuery = state.GetCapturedQuery(step.Message);
                        var (acP, acC, acF) = PlaytestParser.ResolveQuery(capQuery, config);
                        var curVal = ReadValue(acP, acC, acF);
                        float.TryParse(curVal, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var curFloat);
                        var ok = state.EvaluateCaptured(step.Message, step.Op, step.Args, step.Value, curFloat);
                        results.Add($"{label} ASSERT_CAPTURED {step.Message} {step.Op} — {(ok ? "PASS" : "FAIL")} (was={state.GetCapturedValue(step.Message)}, now={curVal})");
                        if (ok) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_CAPTURED {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Invariant:
                    state.RegisterInvariant(step.Query, step.Op, step.Value, step.RawLine);
                    results.Add($"{label} INVARIANT registered: {step.RawLine}");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.AssertConserved:
                    state.StartConserved(step.Queries, step.Delay, config,
                        q => { var (p, c, f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p, c, f); });
                    results.Add($"{label} ASSERT_CONSERVED registered");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.Simulate:
                    try
                    {
                        var simArgs = new SimulatorArgs
                        {
                            Duration = step.Timeout,
                            TimeScale = step.Delay > 0 ? step.Delay : 1f,
                            CharacterPath = step.Path,
                            Frequency = float.TryParse(step.Value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var freq) ? freq : 0f
                        };
                        if (simArgs.TimeScale != 1f) SetTimeScale(simArgs.TimeScale);
                        _activeSimulator = SimulatorRegistry.Create(step.SimulatorName, simArgs);
                        phase = Phase.Simulating;
                        phaseStart = UnityEngine.Time.realtimeSinceStartup;
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} SIMULATE {step.SimulatorName} — ERR: {e.Message}");
                        failed++;
                        phase = Phase.Done;
                    }
                    break;

                case StepType.Monitor:
                    if (string.IsNullOrEmpty(step.Query))
                    {
                        PlaytestMonitorRegistry.StopAll();
                        results.Add($"{label} MONITOR STOP");
                    }
                    else
                    {
                        var monResult = PlaytestMonitorRegistry.Start(step.Query);
                        results.Add($"{label} {monResult}");
                        if (monResult.StartsWith("Monitor not found", StringComparison.OrdinalIgnoreCase))
                            failed++;
                        else
                            passed++;
                    }
                    phase = Phase.Done;
                    break;

                case StepType.TraceFlow:
                    results.Add($"{label} TRACE_FLOW: not yet implemented — use ASSERT for field verification");
                    failed++;
                    phase = Phase.Done;
                    break;

                case StepType.AssertCta:
                {
                    GameObject ctaGo = null;
                    // Check config ctaPath first, then tag "CTA", then name pattern
                    if (config != null && !string.IsNullOrEmpty(config.ctaPath))
                        ctaGo = ComponentSerializer.FindObject(config.ctaPath);
                    if (ctaGo == null)
                        try { ctaGo = GameObject.FindWithTag("CTA"); } catch { /* tag not registered */ }
                    if (ctaGo == null)
                    {
                        // Search by name prefix (include inactive via Resources)
                        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                        {
                            if ((go.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                            if (go.name.StartsWith("CTA", StringComparison.OrdinalIgnoreCase))
                            { ctaGo = go; break; }
                        }
                    }
                    if (ctaGo == null)
                    {
                        results.Add($"{label} ASSERT_CTA {step.Op} — ERR: CTA object not found");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    bool ctaOk = ctaGo.activeInHierarchy;
                    if (ctaOk && step.Op == "CLICKABLE")
                    {
                        var btn = ctaGo.GetComponent<UnityEngine.UI.Button>();
                        ctaOk = btn == null || btn.interactable;
                    }
                    results.Add($"{label} ASSERT_CTA {step.Op} — {(ctaOk ? "PASS" : "FAIL")} ({ctaGo.name})");
                    if (ctaOk) passed++; else failed++;
                    phase = Phase.Done;
                    break;
                }

                case StepType.Click:
                {
                    var go = ComponentSerializer.FindObject(step.Path);
                    if (go == null)
                    {
                        results.Add($"{label} CLICK — ERR: object not found: {step.Path}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    if (!go.activeInHierarchy)
                    {
                        results.Add($"{label} CLICK — ERR: object inactive: {step.Path}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    var btn = go.GetComponent<UnityEngine.UI.Button>();
                    if (btn != null)
                    {
                        if (!btn.interactable)
                        {
                            results.Add($"{label} CLICK — ERR: button not interactable: {step.Path}");
                            failed++;
                            phase = Phase.Done;
                            break;
                        }
                        btn.onClick.Invoke();
                        results.Add($"{label} CLICK button: {step.Path}");
                        passed++;
                    }
                    else
                    {
                        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
                        if (handler != null)
                        {
                            ExecuteEvents.Execute(handler, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                            results.Add($"{label} CLICK handler: {step.Path}");
                            passed++;
                        }
                        else
                        {
                            results.Add($"{label} CLICK — FAIL: no Button or IPointerClickHandler on {step.Path}");
                            failed++;
                        }
                    }
                    if (step.Delay > 0)
                    {
                        phase = Phase.WaitingDelay;
                        phaseStart = Time.realtimeSinceStartup;
                    }
                    else
                    {
                        phase = Phase.Done;
                    }
                    break;
                }
            }
        }
    }
}
