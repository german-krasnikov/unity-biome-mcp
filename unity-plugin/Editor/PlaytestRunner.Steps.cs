using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static partial class PlaytestRunner
    {
        internal static void ExecuteStep(PlaytestStep step, PlaytestConfig config, List<string> results,
            ref Phase phase, ref float phaseStart, ref int passed, ref int failed, int stepIdx, PlaytestState state,
            bool snapshotOnFailure = false, string runId = null, PlaytestVarRegistry varRegistry = null)
        {
            var label = $"[{stepIdx + 1}]";
            switch (step.Type)
            {
                case StepType.Assert:
                    // Polling path: ASSERT ... TIMEOUT n → reuse WaitingPoll infrastructure
                    if (step.HasExplicitTimeout)
                    {
                        phase = Phase.WaitingPoll;
                        phaseStart = Time.realtimeSinceStartup;
                        _waitPollErrors = 0;
                        break;
                    }
                    try
                    {
                        // C03: an exact $name sigil that was captured by a prior MCP ... INTO
                        // step resolves to that captured value instead of a Unity path|comp|
                        // field query. A $name that is a live VAR binding is already expanded
                        // to path|comp|field by ExpandStep before ExecuteStep ever sees it, so
                        // this check never races existing VAR semantics.
                        string actual;
                        if (varRegistry != null && varRegistry.TryGetCaptured(step.Query, out var captured))
                        {
                            actual = captured;
                        }
                        else
                        {
                            var (ap, ac, af) = PlaytestParser.ResolveQuery(step.Query, config);
                            actual = ReadValue(ap, ac, af);
                        }
                        var ok = PlaytestParser.Compare(actual, step.Op, step.Value);
                        var asLabel = !string.IsNullOrEmpty(step.Message) ? $" [{step.Message}]" : "";
                        var assertLine = $"{label} ASSERT {step.Query}{step.Op}{step.Value} — {(ok ? "PASS" : "FAIL")} ({actual}){asLabel}";
                        if (!ok)
                        {
                            assertLine += FormatProvenance(step);
                            if (snapshotOnFailure) assertLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(assertLine);
                        if (ok) passed++; else failed++;
                    }
                    catch (Exception e)
                    {
                        var errLine = $"{label} ASSERT {step.Query} — ERR: {e.Message}" + FormatProvenance(step);
                        if (snapshotOnFailure) errLine += "\n" + BuildFailureSnapshot(step, config);
                        results.Add(errLine);
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
                    _waitPollErrors = 0;
                    break;

                case StepType.Move:
                {
                    phase = Phase.Moving;
                    var charPath = step.Path ?? ResolveCharacterPath(config);
                    _moveTcs = new TaskCompletionSource<string>();
                    Vector3 pos;
                    try
                    {
                        pos = step.RawPosition != null
                            ? PlaytestPositionResolver.Resolve(step.RawPosition)
                            : new Vector3(step.Position.x, step.Position.y, step.Position.z);
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} MOVE — ERR: {e.Message}");
                        failed++;
                        _moveTcs = null;
                        phase = Phase.Done;
                        break;
                    }
                    RuntimeHelper.MoveTo(charPath, $"{pos.x},{pos.y},{pos.z}",
                        step.Timeout > 0 ? step.Timeout : 15f, _moveTcs, config);
                    break;
                }

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

                case StepType.SetActive:
                    try
                    {
                        var go = ComponentSerializer.FindObject(step.Path);
                        if (go == null) throw new ArgumentException($"Object not found: {step.Path}");
                        bool active = ValueParser.ParseBool(step.Value);
                        go.SetActive(active);
                        results.Add($"{label} SET_ACTIVE {step.Path} → {(active ? "active" : "inactive")}");
                        passed++;
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} SET_ACTIVE — ERR: {e.Message}");
                        failed++;
                    }
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
                        tgo.transform.position = step.RawPosition != null
                            ? PlaytestPositionResolver.Resolve(step.RawPosition)
                            : new Vector3(step.Position.x, step.Position.y, step.Position.z);
                        Physics.SyncTransforms();
                        // G28: explicitly sync Rigidbody position so the physics world
                        // reflects the new location without waiting for the next FixedUpdate.
                        var rb = tgo.GetComponent<Rigidbody>();
                        if (rb != null) rb.position = tgo.transform.position;
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
                        var nearLine = $"{label} ASSERT_NEAR {step.Path} {step.Value} {step.Delay} — {(nearOk ? "PASS" : "FAIL")} (dist={dist.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)})";
                        if (!nearOk)
                        {
                            nearLine += FormatProvenance(step);
                            if (snapshotOnFailure) nearLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(nearLine);
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
                        string bResult;
                        if (bFailed == 0)
                            bResult = $"{label} ASSERT_BATCH {bPassed}/{total}";
                        else
                        {
                            bResult = $"{label} ASSERT_BATCH {bPassed}/{total}\n{bDetails.ToString().TrimEnd()}" + FormatProvenance(step);
                            if (snapshotOnFailure) bResult += "\n" + BuildFailureSnapshot(step, config);
                        }
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
                        state.Capture(step.Message, step.Query, capVal, capFloat);
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
                        var captLine = $"{label} ASSERT_CAPTURED {step.Message} {step.Op} — {(ok ? "PASS" : "FAIL")} (was={state.GetCapturedValue(step.Message)}, now={curVal})";
                        if (!ok)
                        {
                            captLine += FormatProvenance(step);
                            if (snapshotOnFailure) captLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(captLine);
                        if (ok) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_CAPTURED {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.AssertChanged:
                    try
                    {
                        var changedQuery = state.GetCapturedQuery(step.Message);
                        var (chP, chC, chF) = PlaytestParser.ResolveQuery(changedQuery, config);
                        var currentRaw = ReadValue(chP, chC, chF);
                        var changed = state.IsChanged(step.Message, currentRaw);
                        var chLine = $"{label} ASSERT_CHANGED {step.Message} — {(changed ? "PASS" : "FAIL")}" +
                                     $" (was={state.GetCapturedRaw(step.Message)}, now={currentRaw})";
                        if (!changed)
                        {
                            chLine += FormatProvenance(step);
                            if (snapshotOnFailure) chLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(chLine);
                        if (changed) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_CHANGED {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Invariant:
                    state.RegisterInvariant(step.Query, step.Op, step.Value, step.RawLine);
                    results.Add($"{label} INVARIANT registered: {step.RawLine}");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.AssertConserved:
                    float? conservedExpected = null;
                    if (!string.IsNullOrEmpty(step.Value) &&
                        float.TryParse(step.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var parsedExpected))
                        conservedExpected = parsedExpected;
                    state.StartConserved(step.Queries, step.Delay, config,
                        q => { var (p, c, f) = PlaytestParser.ResolveQuery(q, config); return ReadValue(p, c, f); },
                        expectedSum: conservedExpected);
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

                case StepType.Section:
                    results.Add($"--- {step.Message} ---");
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
                    // UIDocument branch: "/go|UIDocument|selector" form (Phase 2)
                    // Must precede uGUI path — |UIDocument| cannot appear in a bare GO path.
                    if (step.Path.Contains("|UIDocument|"))
                    {
                        int sep    = step.Path.IndexOf("|UIDocument|", System.StringComparison.Ordinal);
                        var goPath = step.Path[..sep];
                        var elemSel = step.Path[(sep + "|UIDocument|".Length)..];
                        var uitkGo = ComponentSerializer.FindObject(goPath);
                        if (uitkGo == null)
                        {
                            results.Add($"{label} CLICK — ERR: object not found: {goPath}");
                            failed++;
                            phase = Phase.Done;
                            break;
                        }
                        bool clicked = UIElementHelper.SimulateClick(uitkGo, elemSel);
                        if (clicked)
                        {
                            results.Add($"{label} CLICK UIDocument#{elemSel} on {goPath} — PASS");
                            passed++;
                        }
                        else
                        {
                            results.Add($"{label} CLICK — FAIL: no element '{elemSel}' in UIDocument on {goPath}. " +
                                        $"Call inspect_uitk(path=\"{goPath}\") to see available names.");
                            failed++;
                        }
                        if (step.Delay > 0) { phase = Phase.WaitingDelay; phaseStart = Time.realtimeSinceStartup; }
                        else phase = Phase.Done;
                        break;
                    }
                    // Existing uGUI path (unchanged) ──────────────────────────────────────
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

                case StepType.Fill:
                {
                    if (!step.Path.Contains("|UIDocument|"))
                    {
                        results.Add($"{label} FILL — ERR: only UIDocument|selector form supported. Got: {step.Path}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    int fillSep  = step.Path.IndexOf("|UIDocument|", System.StringComparison.Ordinal);
                    var fillGoPath  = step.Path[..fillSep];
                    var fillElemSel = step.Path[(fillSep + "|UIDocument|".Length)..];
                    var fillGo = ComponentSerializer.FindObject(fillGoPath);
                    if (fillGo == null)
                    {
                        results.Add($"{label} FILL — ERR: object not found: {fillGoPath}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    bool filled = UIElementHelper.FillText(fillGo, fillElemSel, step.Value ?? "");
                    if (filled)
                    {
                        results.Add($"{label} FILL {fillElemSel} = \"{step.Value}\" — PASS");
                        passed++;
                    }
                    else
                    {
                        results.Add($"{label} FILL — FAIL: no TextField '{fillElemSel}' in UIDocument on {fillGoPath}. " +
                                    $"Call inspect_uitk(path=\"{fillGoPath}\") to see available elements.");
                        failed++;
                    }
                    phase = Phase.Done;
                    break;
                }

                case StepType.Focus:
                {
                    if (!step.Path.Contains("|UIDocument|"))
                    {
                        results.Add($"{label} FOCUS — ERR: only UIDocument|selector form supported. Got: {step.Path}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    int focusSep  = step.Path.IndexOf("|UIDocument|", System.StringComparison.Ordinal);
                    var focusGoPath  = step.Path[..focusSep];
                    var focusElemSel = step.Path[(focusSep + "|UIDocument|".Length)..];
                    var focusGo = ComponentSerializer.FindObject(focusGoPath);
                    if (focusGo == null)
                    {
                        results.Add($"{label} FOCUS — ERR: object not found: {focusGoPath}");
                        failed++;
                        phase = Phase.Done;
                        break;
                    }
                    bool focused = UIElementHelper.FocusElement(focusGo, focusElemSel);
                    if (focused)
                    {
                        results.Add($"{label} FOCUS {focusElemSel} — PASS");
                        passed++;
                    }
                    else
                    {
                        results.Add($"{label} FOCUS — FAIL: element not found '{focusElemSel}' in UIDocument on {focusGoPath}");
                        failed++;
                    }
                    phase = Phase.Done;
                    break;
                }

                case StepType.AssertOneActive:
                    try
                    {
                        int activeCount = 0;
                        string firstActive = null;
                        foreach (var qPath in step.Queries)
                        {
                            var go = ComponentSerializer.FindObject(qPath);
                            if (go != null && go.activeInHierarchy)
                            {
                                activeCount++;
                                firstActive = firstActive ?? qPath;
                            }
                        }
                        bool oneActive = activeCount == 1;
                        var detail = oneActive
                            ? firstActive
                            : $"{activeCount}/{step.Queries.Length} active";
                        var aoaLine = $"{label} ASSERT_ONE_ACTIVE — {(oneActive ? "PASS" : "FAIL")} ({detail})";
                        if (!oneActive)
                        {
                            aoaLine += FormatProvenance(step);
                            if (snapshotOnFailure) aoaLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(aoaLine);
                        if (oneActive) passed++; else failed++;
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} ASSERT_ONE_ACTIVE — ERR: {e.Message}");
                        failed++;
                    }
                    phase = Phase.Done;
                    break;

                case StepType.WaitCaptured:
                    _unchangedSince = -1f; // reset stable-start tracker for UNCHANGED OVER
                    phase = Phase.WaitingCapturedDelta;
                    phaseStart = Time.realtimeSinceStartup;
                    break;

                case StepType.CaptureFrames:
                    _captureLabel = step.Message ?? $"frames_{stepIdx + 1}";
                    _captureInterval = Mathf.Max(0.016f, step.Delay);
                    _captureTarget = (int)step.Timeout;
                    _captureCamera = step.Component ?? "game";
                    _captureMode = step.Op ?? "strip";
                    state.InitFrames(_captureLabel);
                    _captureLastTime = -999f;
                    phase = Phase.CapturingFrames;
                    break;

                case StepType.AssertFramesDiffer:
                {
                    var frames = state.GetFrames(step.Message);
                    if (frames == null || frames.Count < 2)
                    {
                        results.Add($"{label} ASSERT_FRAMES_DIFFER {step.Message} — ERR: need ≥2 frames");
                        failed++; phase = Phase.Done; break;
                    }
                    var ok = FrameStitcher.AreFramesDifferent(frames);
                    results.Add($"{label} ASSERT_FRAMES_DIFFER {step.Message} — {(ok ? "PASS" : "FAIL")} ({frames.Count} frames)");
                    if (ok) passed++;
                    else { failed++; if (snapshotOnFailure) results.Add(BuildFailureSnapshot(step, config)); }
                    phase = Phase.Done;
                    break;
                }

                case StepType.AssertFramesStatic:
                {
                    var frames = state.GetFrames(step.Message);
                    if (frames == null || frames.Count < 2)
                    {
                        results.Add($"{label} ASSERT_FRAMES_STATIC {step.Message} — ERR: need ≥2 frames");
                        failed++; phase = Phase.Done; break;
                    }
                    var ok = !FrameStitcher.AreFramesDifferent(frames);
                    results.Add($"{label} ASSERT_FRAMES_STATIC {step.Message} — {(ok ? "PASS" : "FAIL")} ({frames.Count} frames)");
                    if (ok) passed++;
                    else { failed++; if (snapshotOnFailure) results.Add(BuildFailureSnapshot(step, config)); }
                    phase = Phase.Done;
                    break;
                }

                case StepType.WaitStable:
                    // P-110: register the rolling window in state, then wait via WaitingStable phase
                    state.StartStableWindow(step.Query);
                    phase = Phase.WaitingStable;
                    phaseStart = Time.realtimeSinceStartup;
                    break;

                case StepType.CaptureMin:
                    // P-305: register a running minimum tracker; polling happens in PollExtrema each tick
                    state.StartTrackMin(step.Message, step.Query);
                    results.Add($"{label} CAPTURE_MIN {step.Message} started (query={step.Query})");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.CaptureMax:
                    // P-305: register a running maximum tracker
                    state.StartTrackMax(step.Message, step.Query);
                    results.Add($"{label} CAPTURE_MAX {step.Message} started (query={step.Query})");
                    passed++;
                    phase = Phase.Done;
                    break;

                case StepType.AssertMin:
                    try
                    {
                        var minVal = state.GetMin(step.Message);
                        var minOk = PlaytestParser.Compare(
                            minVal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            step.Op, step.Value);
                        var minLine = $"{label} ASSERT_MIN {step.Message} {step.Op} {step.Value} — {(minOk ? "PASS" : "FAIL")} (min={minVal})";
                        if (!minOk)
                        {
                            minLine += FormatProvenance(step);
                            if (snapshotOnFailure) minLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(minLine);
                        if (minOk) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_MIN {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.AssertMax:
                    try
                    {
                        var maxVal = state.GetMax(step.Message);
                        var maxOk = PlaytestParser.Compare(
                            maxVal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            step.Op, step.Value);
                        var maxLine = $"{label} ASSERT_MAX {step.Message} {step.Op} {step.Value} — {(maxOk ? "PASS" : "FAIL")} (max={maxVal})";
                        if (!maxOk)
                        {
                            maxLine += FormatProvenance(step);
                            if (snapshotOnFailure) maxLine += "\n" + BuildFailureSnapshot(step, config);
                        }
                        results.Add(maxLine);
                        if (maxOk) passed++; else failed++;
                    }
                    catch (Exception e) { results.Add($"{label} ASSERT_MAX {step.Message} — ERR: {e.Message}"); failed++; }
                    phase = Phase.Done;
                    break;

                case StepType.Mcp:
                {
                    // C03 — executes the step through the real CommandRouter.ProcessAsync
                    // path, polled via Phase.WaitingMcp (mirrors StepType.Move/Phase.Moving
                    // exactly). ProcessAsync never throws past its own internal catch, so the
                    // try/catch below exists for C04's runtime denylist re-check (and any
                    // future dispatch-time exception) — not for ProcessAsync itself: an
                    // uncaught exception here would otherwise escape to Tick()'s outer catch
                    // and abort the whole run without teardown, instead of failing just this
                    // one step.
                    try
                    {
                        // C04 — belt and suspenders: re-check the same hard denylist
                        // Validate() enforces at compile time (C02), in case this step was
                        // constructed directly and never went through Validate/parsing.
                        if (PlaytestMcpPolicy.IsHardDenied(step.Method))
                            throw new InvalidOperationException(PlaytestMcpPolicy.HardDenialReason);

                        var envelope = BuildMcpEnvelope(runId, stepIdx, step.Method, step.Args);
                        _mcpTcs = new TaskCompletionSource<string>();
                        CommandRouter.ProcessAsync(envelope, _mcpTcs);
                        phase = Phase.WaitingMcp;
                    }
                    catch (Exception e)
                    {
                        results.Add($"{label} MCP {step.Method} — FAIL: {e.Message}");
                        failed++;
                        phase = Phase.Done;
                    }
                    break;
                }
            }
        }
    }
}
