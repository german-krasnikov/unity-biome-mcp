using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    // D05a — the per-line step-building loop, extracted out of PlaytestParser.cs's
    // Parse() method to keep both files under the csharp-unity.md <300-line business-logic
    // convention (>750-line hard cap for this split). Parse() (PlaytestParser.cs) still owns
    // header scan / INCLUDE / MACRO / CALL / VAL-sigil expansion, then delegates to BuildSteps()
    // here for the actual per-line -> PlaytestStep construction, exactly as before (pure
    // extract-method relocation, zero logic change — proven by an identical full Playtest
    // filter test count before/after this split).
    internal static partial class PlaytestParser
    {
        internal static void BuildSteps(
            string[] lines, List<SourcedLine> expandedSourced,
            out List<PlaytestStep> steps, out List<PlaytestStep> setupSteps, out List<PlaytestStep> teardownSteps,
            out Dictionary<string, string> varDefs, out bool hasGlobalAbort, out float defaultTimeout)
        {
            // Phase 1.1 + Phase 2: parse commands; collect VAR definitions
            varDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            steps = new List<PlaytestStep>();
            setupSteps = null;
            teardownSteps = null;
            string pendingLabel = null;
            bool pendingExpectFail = false; // C06: EXPECT_FAIL — pendingLabel-style single slot
            hasGlobalAbort = false;
            defaultTimeout = 0f;
            string currentSection = null;
            // Tracks current DSL block section: 0=Main, 1=Setup, 2=Teardown
            int parsingSection = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var sourced = expandedSourced[i]; // provenance for this line
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var tokens = SplitTokens(line);
                var cmd = tokens[0].ToUpperInvariant();
                if (cmd == "VAL") continue;   // skip VAL definitions (already processed in phase 0.7)
                if (cmd == "PATH_PREFIX") continue; // skip PATH_PREFIX directive (processed in phase 0.7)

                // Phase 1.1: collect VAR bindings
                if (cmd == "VAR")
                {
                    if (tokens.Length < 3)
                        throw new ArgumentException(
                            "VAR syntax: VAR $name @path|Comp|field\n" +
                            "Example: VAR $hp @/Player|Health|currentHp\n" +
                            "Example: VAR $pos @/Enemy|Transform|position");
                    var varName = tokens[1].TrimStart('$');
                    var query = tokens[2];
                    if (!query.StartsWith("@"))
                        throw new ArgumentException(
                            $"VAR '{tokens[1]}': query must start with '@'.\n" +
                            $"  Format: VAR $name @/path|ComponentName|fieldName\n" +
                            $"  Example: VAR $hp @/Player|Health|currentHp\n" +
                            $"  (Not dot notation — use pipes to separate path, component, field)");
                    // Validate pipe-separated format: @path|comp|field
                    var varParts = query.Substring(1).Split('|');
                    if (varParts.Length < 3)
                        throw new ArgumentException(
                            $"VAR '{tokens[1]}': needs 3 pipe-separated parts: path|comp|field (got {varParts.Length} in '{query}').\n" +
                            $"  Example: VAR $hp @/Player|Health|currentHp");
                    varDefs[varName] = query; // store raw @-query string
                    continue;
                }

                // Parsing uses the trimmed line, but the Composer needs the exact source
                // text to preserve recognised steps it cannot edit visually.
                var step = new PlaytestStep
                {
                    RawLine = sourced.Text,
                    ExpandedRawLine = line
                };

                switch (cmd)
                {
                    case "MOVE":
                        step.Type = StepType.Move;
                        int toIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TO");
                        if (toIdx < 0 || toIdx + 1 >= tokens.Length)
                            throw new ArgumentException("MOVE syntax: MOVE [path] TO x,y,z");
                        step.Path = toIdx > 1 ? tokens[1] : null;
                        SetPosition(step, tokens[toIdx + 1]);
                        break;

                    case "WAIT":
                        step.Type = StepType.Wait;
                        step.Delay = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        break;

                    case "WAIT_UNTIL":
                    {
                        // Bool sugar: WAIT_UNTIL $flag  or  WAIT_UNTIL !$flag
                        if (tokens.Length == 2)
                        {
                            var t1 = tokens[1];
                            if (!t1.StartsWith("$") && !t1.StartsWith("!$"))
                                throw new ArgumentException($"WAIT_UNTIL: unrecognised single-token form '{t1}' (expected $flag or !$flag)");
                            bool negated = t1.StartsWith("!");
                            step.Type = StepType.WaitUntil;
                            step.Query = negated ? t1.Substring(1) : t1;
                            step.Op = "==";
                            step.Value = negated ? "False" : "True";
                            break;
                        }
                        if (tokens.Length < 4) throw new ArgumentException($"WAIT_UNTIL requires path op value, got: '{line}'");
                        step.Type = StepType.WaitUntil;
                        var (wq, wop, wv, wnext) = ParseQOV(tokens, 1);
                        step.Query = wq; step.Op = wop; step.Value = wv;
                        var tiIdx = Array.FindIndex(tokens, wnext, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (tiIdx >= 0) { step.Timeout = float.Parse(tokens[tiIdx + 1], CultureInfo.InvariantCulture); step.HasExplicitTimeout = true; }
                        // AND/OR compound conditions + ABORT detection (inline, avoids false-positive on ABORT-as-value)
                        var xQ = new List<string>(); var xOps = new List<string>(); var xVals = new List<string>();
                        bool? isOr = null;
                        int xi = wnext;
                        while (xi < tokens.Length)
                        {
                            var tk = tokens[xi].ToUpperInvariant();
                            if (tk == "TIMEOUT") { xi += 2; continue; }
                            if (tk == "ABORT") { step.AbortOnFail = true; xi++; continue; }
                            if (tk == "AND" || tk == "OR")
                            {
                                bool thisOr = tk == "OR";
                                if (isOr == null) isOr = thisOr;
                                else if (isOr != thisOr) throw new ArgumentException("Cannot mix AND/OR in WAIT_UNTIL");
                                if (xi + 3 >= tokens.Length) throw new ArgumentException($"{tk} requires query op value");
                                var (cq, cop, cv, cnext) = ParseQOV(tokens, xi + 1);
                                xQ.Add(cq); xOps.Add(cop); xVals.Add(cv);
                                xi = cnext;
                            }
                            else xi++;
                        }
                        if (xQ.Count > 0)
                        {
                            step.Queries = xQ.ToArray(); step.BatchOps = xOps.ToArray(); step.BatchValues = xVals.ToArray();
                            step.IsOr = isOr == true;
                        }
                        break;
                    }

                    case "ASSERT":
                    {
                        // Bool sugar: ASSERT $name  or  ASSERT !$name  or  ASSERT ($a,$b)  or  ASSERT !($a,$b)
                        if (tokens.Length == 2)
                        {
                            var t1 = tokens[1];
                            bool negated = t1.StartsWith("!");
                            var inner = negated ? t1.Substring(1) : t1;
                            if (inner.StartsWith("(") && inner.EndsWith(")"))
                            {
                                var names = inner.Trim('(', ')').Split(',')
                                    .Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)).ToArray();
                                if (names.Length == 0) throw new ArgumentException("ASSERT group: empty list");
                                step.Type = StepType.AssertBatch;
                                step.Queries = names;
                                step.BatchOps = Enumerable.Repeat("==", names.Length).ToArray();
                                step.BatchValues = Enumerable.Repeat(negated ? "False" : "True", names.Length).ToArray();
                                break;
                            }
                            if (t1.StartsWith("$") || t1.StartsWith("!$"))
                            {
                                step.Type = StepType.Assert;
                                step.Query = negated ? t1.Substring(1) : t1;
                                step.Op = "==";
                                step.Value = negated ? "False" : "True";
                                break;
                            }
                            // Bool shorthand: ASSERT /path|comp|field (implied == True)
                            if (inner.StartsWith("/") || inner.StartsWith("#"))
                            {
                                step.Type = StepType.Assert;
                                step.Query = inner;
                                step.Op = "==";
                                step.Value = negated ? "False" : "True";
                                break;
                            }
                            throw new ArgumentException($"ASSERT: unrecognised single-token form '{t1}' (expected $name, !$name, /path, or ($a,$b,...))");
                        }
                        // Standard form: ASSERT query op value [TIMEOUT n] [AS label]
                        if (tokens.Length < 4) throw new ArgumentException($"ASSERT requires path op value, got: '{line}'");
                        step.Type = StepType.Assert;
                        var (aq, aop, av, anext) = ParseQOV(tokens, 1);
                        step.Query = aq; step.Op = aop; step.Value = av;
                        var tiIdxA = Array.FindIndex(tokens, anext, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (tiIdxA >= 0) { step.Timeout = float.Parse(tokens[tiIdxA + 1], CultureInfo.InvariantCulture); step.HasExplicitTimeout = true; }
                        var asIdx = Array.FindIndex(tokens, anext, t => t.ToUpperInvariant() == "AS");
                        if (asIdx >= 0)
                        {
                            var labelEnd = (tiIdxA >= 0 && tiIdxA > asIdx) ? tiIdxA : tokens.Length;
                            step.Message = string.Join(" ", tokens, asIdx + 1, labelEnd - asIdx - 1).Trim('"');
                        }
                        break;
                    }

                    case "ASSERT_CONSOLE_CLEAN":
                        step.Type = StepType.AssertConsoleClean;
                        // ASSERT_CONSOLE_CLEAN IGNORE "pattern1", "pattern2"
                        if (tokens.Length > 1 && tokens[1].ToUpperInvariant() == "IGNORE")
                        {
                            var rest = string.Join(" ", tokens, 2, tokens.Length - 2);
                            step.Queries = rest.Split(',')
                                .Select(p => p.Trim().Trim('"'))
                                .Where(p => !string.IsNullOrEmpty(p))
                                .ToArray();
                        }
                        break;

                    case "ASSERT_BATCH":
                        step.Type = StepType.AssertBatch;
                        var batchQueries = new List<string>();
                        var batchOps = new List<string>();
                        var batchValues = new List<string>();
                        i++;
                        bool batchFoundEnd = false;
                        while (i < lines.Length)
                        {
                            var bLine = lines[i].Trim();
                            if (bLine.ToUpperInvariant() == "END") { batchFoundEnd = true; break; }
                            if (!string.IsNullOrEmpty(bLine) && !bLine.StartsWith("#"))
                            {
                                var bt = SplitTokens(bLine);
                                if (bt.Length >= 4 && bt[0].ToUpperInvariant() == "ASSERT")
                                {
                                    var (bq, bop, bv, _) = ParseQOV(bt, 1);
                                    if (!string.IsNullOrEmpty(bop))
                                    { batchQueries.Add(bq); batchOps.Add(bop); batchValues.Add(bv); }
                                }
                            }
                            i++;
                        }
                        if (!batchFoundEnd)
                            throw new ArgumentException("ASSERT_BATCH block missing END terminator");
                        step.Queries = batchQueries.ToArray();
                        step.BatchOps = batchOps.ToArray();
                        step.BatchValues = batchValues.ToArray();
                        break;

                    case "ASSERT_NEAR":
                        // ASSERT_NEAR /A /B threshold
                        step.Type = StepType.AssertNear;
                        step.Path = tokens[1];
                        step.Value = tokens[2];
                        step.Delay = float.Parse(tokens[3], CultureInfo.InvariantCulture);
                        break;

                    case "TELEPORT":
                        // TELEPORT /path x,y,z  OR  TELEPORT /path @/Ref.position
                        step.Type = StepType.Teleport;
                        step.Path = tokens[1];
                        SetPosition(step, tokens[2]);
                        break;

                    case "SNAPSHOT":
                        step.Type = StepType.Snapshot;
                        step.Queries = string.Join(" ", tokens, 1, tokens.Length - 1).Split(',');
                        break;

                    case "INVOKE":
                        step.Type = StepType.Invoke;
                        step.Path = tokens[1]; step.Component = tokens[2]; step.Method = tokens[3];
                        // G2: join all remaining tokens so multi-arg calls like "42 true" are preserved
                        step.Args = tokens.Length > 4 ? string.Join(" ", tokens, 4, tokens.Length - 4) : "";
                        break;

                    case "SET_ACTIVE":
                        if (tokens.Length < 3)
                            throw new ArgumentException("SET_ACTIVE syntax: SET_ACTIVE /path true|false");
                        step.Type  = StepType.SetActive;
                        step.Path  = tokens[1];
                        step.Value = tokens[2];
                        break;

                    case "SET":
                        step.Type = StepType.Set;
                        step.Path = tokens[1]; step.Component = tokens[2]; step.Method = tokens[3]; step.Args = tokens[4];
                        break;

                    case "LOG":
                        step.Type = StepType.Log;
                        step.Message = string.Join(" ", tokens, 1, tokens.Length - 1);
                        break;

                    case "TIMESCALE":
                        step.Type = StepType.TimeScale;
                        step.Delay = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        break;

                    case "CAPTURE":
                        // CAPTURE label query  (query may have spaces if path has spaces)
                        step.Type = StepType.Capture;
                        step.Message = tokens[1];
                        step.Query = tokens.Length > 2 ? string.Join(" ", tokens, 2, tokens.Length - 2) : "";
                        break;

                    case "CAPTURE_FRAMES":
                    {
                        // CAPTURE_FRAMES n INTERVAL s [CAMERA name] [MODE strip|list] [LABEL name]
                        step.Type = StepType.CaptureFrames;
                        var cfCount = (int)float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        if (cfCount < 2) throw new ArgumentException($"CAPTURE_FRAMES: n must be >= 2, got {cfCount}");
                        step.Timeout = cfCount;
                        var ivIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "INTERVAL");
                        if (ivIdx < 0 || ivIdx + 1 >= tokens.Length)
                            throw new ArgumentException("CAPTURE_FRAMES requires INTERVAL parameter");
                        step.Delay = float.Parse(tokens[ivIdx + 1], CultureInfo.InvariantCulture);
                        var cfCamIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "CAMERA");
                        step.Component = cfCamIdx >= 0 ? tokens[cfCamIdx + 1] : "game";
                        var cfModeIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "MODE");
                        step.Op = cfModeIdx >= 0 ? tokens[cfModeIdx + 1].ToLowerInvariant() : "strip";
                        var cfLblIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "LABEL");
                        step.Message = cfLblIdx >= 0 ? tokens[cfLblIdx + 1] : null;
                        break;
                    }

                    case "ASSERT_FRAMES_DIFFER":
                        // ASSERT_FRAMES_DIFFER label
                        step.Type = StepType.AssertFramesDiffer;
                        step.Message = tokens[1];
                        break;

                    case "ASSERT_FRAMES_STATIC":
                        // ASSERT_FRAMES_STATIC label
                        step.Type = StepType.AssertFramesStatic;
                        step.Message = tokens[1];
                        break;

                    case "ASSERT_CAPTURED":
                        // ASSERT_CAPTURED label MODE [subOp value]
                        step.Type = StepType.AssertCaptured;
                        step.Message = tokens[1];
                        step.Op = tokens[2];
                        if (tokens.Length >= 5) { step.Args = tokens[3]; step.Value = tokens[4]; }
                        break;

                    case "ASSERT_CHANGED":
                        // ASSERT_CHANGED $label
                        step.Type = StepType.AssertChanged;
                        step.Message = tokens[1];
                        break;

                    case "WAIT_CAPTURED":
                    {
                        // WAIT_CAPTURED <label> INCREASED|DECREASED|UNCHANGED|INCREASED_BY|DECREASED_BY [subOp val] [TIMEOUT n] [OVER n]
                        if (tokens.Length < 3)
                            throw new ArgumentException("WAIT_CAPTURED syntax: WAIT_CAPTURED <label> INCREASED|DECREASED|UNCHANGED|INCREASED_BY|DECREASED_BY [subOp val] [TIMEOUT n] [OVER n]");
                        step.Type = StepType.WaitCaptured;
                        step.Message = tokens[1];
                        step.Op = tokens[2].ToUpperInvariant();
                        if ((step.Op == "INCREASED_BY" || step.Op == "DECREASED_BY") && tokens.Length >= 5
                            && tokens[3].ToUpperInvariant() != "TIMEOUT" && tokens[3].ToUpperInvariant() != "OVER")
                        {
                            step.Args = tokens[3];
                            step.Value = tokens[4];
                        }
                        var wcTiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (wcTiIdx >= 0) step.Timeout = float.Parse(tokens[wcTiIdx + 1], CultureInfo.InvariantCulture);
                        var wcOvIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (wcOvIdx >= 0) step.Delay = float.Parse(tokens[wcOvIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "INVARIANT":
                        // INVARIANT query op value
                        step.Type = StepType.Invariant;
                        var (iq, iop, iv, _) = ParseQOV(tokens, 1);
                        step.Query = iq; step.Op = iop; step.Value = iv;
                        break;

                    case "ASSERT_CONSERVED":
                    {
                        // ASSERT_CONSERVED SUM q1 + q2 [+ q3...] == CONSTANT OVER duration
                        step.Type = StepType.AssertConserved;
                        var queries = new List<string>();
                        int ti = 1; // skip SUM
                        if (ti < tokens.Length && tokens[ti].ToUpperInvariant() == "SUM") ti++;
                        // collect query names until == or OVER keyword
                        while (ti < tokens.Length)
                        {
                            var t = tokens[ti];
                            if (t == "+" || t.ToUpperInvariant() == "==" || t.ToUpperInvariant() == "CONSTANT" || t.ToUpperInvariant() == "OVER") { ti++; continue; }
                            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) { ti++; continue; }
                            // OVER keyword precedes duration
                            if (tokens[ti - 1].ToUpperInvariant() == "OVER") break;
                            queries.Add(t);
                            ti++;
                        }
                        step.Queries = queries.ToArray();
                        // find OVER duration
                        var overIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (overIdx >= 0 && overIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[overIdx + 1], CultureInfo.InvariantCulture);
                        // find == numeric RHS (e.g. == 100 OVER 5); skip CONSTANT keyword
                        var eqIdx = Array.FindIndex(tokens, t => t == "==");
                        if (eqIdx >= 0 && eqIdx + 1 < tokens.Length)
                        {
                            var rhsTok = tokens[eqIdx + 1].ToUpperInvariant() == "CONSTANT"
                                ? (eqIdx + 2 < tokens.Length ? tokens[eqIdx + 2] : null)
                                : tokens[eqIdx + 1];
                            if (rhsTok != null && float.TryParse(rhsTok, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                                step.Value = rhsTok;
                        }
                        break;
                    }

                    case "SIMULATE":
                    {
                        // SIMULATE name [DURATION n] [TIMESCALE n] [TARGET "path"] [FREQUENCY n]
                        step.Type = StepType.Simulate;
                        step.SimulatorName = tokens[1];
                        var durIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "DURATION");
                        if (durIdx >= 0) step.Timeout = float.Parse(tokens[durIdx + 1], CultureInfo.InvariantCulture);
                        var tsIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMESCALE");
                        if (tsIdx >= 0) step.Delay = float.Parse(tokens[tsIdx + 1], CultureInfo.InvariantCulture);
                        var tgtIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TARGET");
                        if (tgtIdx >= 0) step.Path = tokens[tgtIdx + 1].Trim('"');
                        var freqIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FREQUENCY");
                        if (freqIdx >= 0) step.Value = tokens[freqIdx + 1];
                        break;
                    }

                    case "MONITOR":
                    {
                        // MONITOR name  OR  MONITOR STOP
                        step.Type = StepType.Monitor;
                        if (tokens.Length > 1 && tokens[1].ToUpperInvariant() != "STOP")
                            step.Query = tokens[1];
                        break;
                    }

                    case "TRACE_FLOW":
                    {
                        // TRACE_FLOW FROM /path1 TO /path2 FIELD fieldName TIMEOUT n
                        step.Type = StepType.TraceFlow;
                        var fromIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FROM");
                        if (fromIdx >= 0) step.Path = tokens[fromIdx + 1];
                        var toIdxTf = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TO");
                        if (toIdxTf >= 0) step.Query = tokens[toIdxTf + 1];
                        var fieldIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "FIELD");
                        if (fieldIdx >= 0) step.Method = tokens[fieldIdx + 1];
                        var tfTiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (tfTiIdx >= 0) step.Timeout = float.Parse(tokens[tfTiIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "ASSERT_CTA":
                    {
                        // ASSERT_CTA VISIBLE  OR  ASSERT_CTA CLICKABLE
                        step.Type = StepType.AssertCta;
                        step.Op = tokens.Length > 1 ? tokens[1].ToUpperInvariant() : "VISIBLE";
                        break;
                    }

                    case "CLICK":
                    case "TAP":
                    {
                        if (tokens.Length < 2)
                            throw new ArgumentException("CLICK requires object path");
                        step.Type = StepType.Click;
                        step.Path = NormalizeUIHostPath(tokens[1]);
                        int waitIdx = Array.FindIndex(tokens, 2, t => t.ToUpperInvariant() == "WAIT");
                        if (waitIdx >= 0 && waitIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[waitIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "FILL":
                    {
                        if (tokens.Length < 2)
                            throw new ArgumentException("FILL requires a path");
                        step.Type = StepType.Fill;
                        step.Path = NormalizeUIHostPath(tokens[1]);
                        step.Value = tokens.Length > 2
                            ? string.Join(" ", tokens, 2, tokens.Length - 2)
                            : "";
                        break;
                    }

                    case "FOCUS":
                    {
                        if (tokens.Length < 2)
                            throw new ArgumentException("FOCUS requires a path");
                        step.Type = StepType.Focus;
                        step.Path = NormalizeUIHostPath(tokens[1]);
                        break;
                    }

                    case "MOVE_PATH":
                    {
                        BuildMovePathSteps(tokens, line, pendingLabel, steps);
                        pendingLabel = null;
                        continue; // skip the outer steps.Add(step)
                    }

                    case "SWEEP_PATH":
                    {
                        BuildSweepPathSteps(tokens, line, lines, ref i, pendingLabel, steps);
                        pendingLabel = null;
                        continue; // skip outer steps.Add(step)
                    }

                    case "COMPLETE_PURCHASE":
                    {
                        BuildCompletePurchaseSteps(tokens, line, lines, ref i, pendingLabel, steps);
                        pendingLabel = null;
                        continue; // skip outer steps.Add
                    }

                    case "INVOKE_REPEAT":
                    {
                        BuildInvokeRepeatSteps(tokens, line, lines, ref i, pendingLabel, steps);
                        pendingLabel = null;
                        continue; // skip outer steps.Add
                    }

                    case "SECTION":
                        step.Type = StepType.Section;
                        step.Message = string.Join(" ", tokens, 1, tokens.Length - 1).Trim('"');
                        currentSection = step.Message; // update so SECTION step and subsequent steps get this label
                        break;

                    case "DESC":
                        pendingLabel = string.Join(" ", tokens, 1, tokens.Length - 1).Trim('"');
                        continue; // no step emitted

                    case "ABORT_ON_FAIL":
                        hasGlobalAbort = true;
                        continue; // global directive — not emitted as a step

                    case "SET_DEFAULT_TIMEOUT":
                        if (tokens.Length < 2)
                            throw new ArgumentException("SET_DEFAULT_TIMEOUT requires a value (seconds)");
                        defaultTimeout = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        continue; // global directive — not emitted as a step

                    case "ASSERT_ONE_ACTIVE":
                        if (tokens.Length < 3)
                            throw new ArgumentException(
                                "ASSERT_ONE_ACTIVE requires at least 2 paths, e.g.:\n" +
                                "ASSERT_ONE_ACTIVE /Cam_Intro /Cam_Menu /Cam_Game");
                        step.Type = StepType.AssertOneActive;
                        step.Queries = tokens.Skip(1).ToArray();
                        break;

                    case "COMMENT":
                    {
                        // Block comment expanded from MACRO/FOR — skip until END_COMMENT
                        bool foundCEnd = false;
                        i++;
                        while (i < lines.Length)
                        {
                            if (lines[i].Trim().Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase)) { foundCEnd = true; break; }
                            i++;
                        }
                        if (!foundCEnd) throw new ArgumentException("COMMENT block missing END_COMMENT");
                        continue;
                    }

                    case "END_COMMENT":
                        continue; // stray END_COMMENT — silently skip

                    case "SETUP":
                        parsingSection = 1;
                        setupSteps = setupSteps ?? new List<PlaytestStep>();
                        continue; // section marker — no step emitted

                    case "SETUP_END":
                        parsingSection = 0; // G16: return to main section
                        continue;

                    case "TEARDOWN":
                        parsingSection = 2;
                        teardownSteps = teardownSteps ?? new List<PlaytestStep>();
                        continue; // section marker — no step emitted

                    case "TEARDOWN_END":
                        parsingSection = 0; // G16: return to main section
                        continue;

                    case "WAIT_STABLE":
                    {
                        // WAIT_STABLE /path|Comp|field DELTA d OVER t [TIMEOUT n]
                        // step.Query = field path, step.Value = DELTA, step.Delay = OVER window, step.Timeout = max wait
                        if (tokens.Length < 6) throw new ArgumentException("WAIT_STABLE syntax: WAIT_STABLE /path|Comp|field DELTA d OVER t [TIMEOUT n]");
                        step.Type = StepType.WaitStable;
                        step.Query = tokens[1];
                        var wsDelIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "DELTA");
                        if (wsDelIdx >= 0 && wsDelIdx + 1 < tokens.Length)
                            step.Value = tokens[wsDelIdx + 1];
                        var wsOvIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (wsOvIdx >= 0 && wsOvIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[wsOvIdx + 1], CultureInfo.InvariantCulture);
                        var wsTiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        if (wsTiIdx >= 0 && wsTiIdx + 1 < tokens.Length)
                            step.Timeout = float.Parse(tokens[wsTiIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "CAPTURE_MIN":
                    {
                        // CAPTURE_MIN $name /path|Comp|field OVER t
                        // step.Message = tracker name, step.Query = field path, step.Delay = OVER window
                        if (tokens.Length < 3) throw new ArgumentException("CAPTURE_MIN syntax: CAPTURE_MIN $name /path|Comp|field [OVER t]");
                        step.Type = StepType.CaptureMin;
                        step.Message = tokens[1].TrimStart('$');
                        step.Query = tokens[2];
                        var cmOvIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (cmOvIdx >= 0 && cmOvIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[cmOvIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "CAPTURE_MAX":
                    {
                        // CAPTURE_MAX $name /path|Comp|field OVER t
                        if (tokens.Length < 3) throw new ArgumentException("CAPTURE_MAX syntax: CAPTURE_MAX $name /path|Comp|field [OVER t]");
                        step.Type = StepType.CaptureMax;
                        step.Message = tokens[1].TrimStart('$');
                        step.Query = tokens[2];
                        var cxOvIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "OVER");
                        if (cxOvIdx >= 0 && cxOvIdx + 1 < tokens.Length)
                            step.Delay = float.Parse(tokens[cxOvIdx + 1], CultureInfo.InvariantCulture);
                        break;
                    }

                    case "ASSERT_MIN":
                    {
                        // ASSERT_MIN $name op value
                        if (tokens.Length < 4) throw new ArgumentException("ASSERT_MIN syntax: ASSERT_MIN $name op value");
                        step.Type = StepType.AssertMin;
                        step.Message = tokens[1].TrimStart('$');
                        step.Op = tokens[2];
                        step.Value = tokens[3];
                        break;
                    }

                    case "ASSERT_MAX":
                    {
                        // ASSERT_MAX $name op value
                        if (tokens.Length < 4) throw new ArgumentException("ASSERT_MAX syntax: ASSERT_MAX $name op value");
                        step.Type = StepType.AssertMax;
                        step.Message = tokens[1].TrimStart('$');
                        step.Op = tokens[2];
                        step.Value = tokens[3];
                        break;
                    }

                    case "MCP":
                        ParseMcpStep(step, line);
                        break;

                    case "EXPECT_FAIL":
                        SetPendingExpectFail(ref pendingExpectFail);
                        continue; // directive — no step emitted (mirrors DESC)

                    default:
                        throw new ArgumentException($"Unknown command: {cmd}");
                }
                step.SourceFile    = sourced.File;
                step.SourceLine    = sourced.Line;
                step.MacroStack    = sourced.MacroStack;
                step.SectionContext = currentSection;
                step.Label = pendingLabel;
                pendingLabel = null;
                ConsumePendingExpectFail(step, ref pendingExpectFail);
                switch (parsingSection)
                {
                    case 1: setupSteps.Add(step); break;
                    case 2: teardownSteps.Add(step); break;
                    default: steps.Add(step); break;
                }
            }
        }
    }
}
