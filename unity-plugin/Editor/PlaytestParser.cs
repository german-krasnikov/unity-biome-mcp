using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal enum StepType { Move, Wait, WaitUntil, Assert, AssertConsoleClean, Snapshot, Invoke, Set, Log, TimeScale, Teleport, AssertBatch, AssertNear, Capture, AssertCaptured, Invariant, AssertConserved, Simulate, Monitor, TraceFlow, AssertCta, Click, Section, Desc, WaitCaptured, AssertOneActive, AssertChanged, CaptureFrames, AssertFramesDiffer, AssertFramesStatic, SetActive, Setup, Teardown, WaitStable, CaptureMin, CaptureMax, AssertMin, AssertMax, Fill, Focus }

    /// <summary>A script line with origin metadata (file, line number, macro call chain).</summary>
    internal struct SourcedLine
    {
        public string Text;
        public string File;        // null = main inline script
        public int    Line;        // 0-based line number in File (or inline script)
        public string[] MacroStack; // null = direct; ["outer","inner"] = nested call chain
    }

    [Serializable]
    internal class PlaytestStep
    {
        public StepType Type;
        public string Path;
        public Vector3 Position;
        public string RawPosition; // null = literal in Position; non-null = deferred @-expression
        public float Delay;
        public string Query;
        public string Op;
        public string Value;
        public float Timeout = 5f;
        public bool HasExplicitTimeout;   // true when TIMEOUT token was present in DSL
        public string Component;
        public string Method;
        public string Args;
        public string Message;
        public string[] Queries;
        public string RawLine;
        public string[] BatchOps;
        public string[] BatchValues;
        public string SimulatorName;
        public bool IsOr;        // true = OR logic for compound WAIT_UNTIL, false = AND
        public bool AbortOnFail; // true = stop Play Mode on timeout
        public string Label;     // set by preceding DESC line

        // Provenance — null when not tracked (inline scripts with no INCLUDE/MACRO)
        public string   SourceFile;     // origin file path; null = main inline script
        public int      SourceLine;     // 0-based line number in SourceFile (or inline script)
        public string[] MacroStack;     // null = direct; non-null = macro call chain, outermost first
        public string   SectionContext; // SECTION label active when this step was parsed; null = none

        // ── Semantic aliases — name the meaning per step type; no backing change ──
        internal float  WaitDuration     => Delay;
        internal float  TimeScaleValue   => Delay;
        internal float  NearThreshold    => Delay;
        internal string NearPath         => Value;
        internal float  SimulateScale    => Delay;
        internal float  SimulateDuration => Timeout;
        internal float  SimulateFrequency =>
            float.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        internal float  ClickPostWait    => Delay;
        internal string CaptureLabel     => Message;
        internal string DisplayText      => Message;

        // Shallow copy — arrays share references with original (by design)
        internal PlaytestStep ShallowClone() => new PlaytestStep
        {
            Type = Type, Path = Path, Position = Position, RawPosition = RawPosition, Delay = Delay,
            Query = Query, Op = Op, Value = Value, Timeout = Timeout, HasExplicitTimeout = HasExplicitTimeout,
            Component = Component, Method = Method, Args = Args,
            Message = Message, Queries = Queries, RawLine = RawLine,
            BatchOps = BatchOps, BatchValues = BatchValues,
            SimulatorName = SimulatorName, IsOr = IsOr,
            AbortOnFail = AbortOnFail, Label = Label,
            SourceFile = SourceFile, SourceLine = SourceLine,
            MacroStack = MacroStack, SectionContext = SectionContext
        };
    }

    // Carries both steps and var-bindings out of Parse() with zero breakage for existing callers.
    internal class ParseResult : IEnumerable<PlaytestStep>
    {
        public List<PlaytestStep> Steps;
        /// <summary>Steps in the SETUP block. Null if no SETUP block was declared.</summary>
        public List<PlaytestStep> SetupSteps;
        /// <summary>Steps in the TEARDOWN block. Null if no TEARDOWN block was declared.</summary>
        public List<PlaytestStep> TeardownSteps;
        /// <summary>Raw @-query strings keyed by VAR name (without $). Null if none declared.</summary>
        public Dictionary<string, string> VarDefs;
        /// <summary>Non-fatal parse warnings (e.g. unresolved $sigil typos). Null if none.</summary>
        public List<string> Warnings;
        /// <summary>VAL definitions keyed by name (without $). Null if none declared.</summary>
        public Dictionary<string, string> ValDefs;
        public bool HasGlobalAbort { get; set; }
        /// <summary>SET_DEFAULT_TIMEOUT value in seconds. 0 = not set (runner uses 5f fallback).</summary>
        public float DefaultTimeout { get; set; }

        public int Count => Steps.Count;
        public PlaytestStep this[int i] => Steps[i];
        public bool Exists(Predicate<PlaytestStep> match) => Steps.Exists(match);
        public int IndexOf(PlaytestStep item) => Steps.IndexOf(item);
        public IEnumerator<PlaytestStep> GetEnumerator() => Steps.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Steps.GetEnumerator();

        public static implicit operator List<PlaytestStep>(ParseResult r) => r.Steps;
    }

    /// <summary>Resolves INCLUDE directives — returns full file content as a string.</summary>
    internal delegate string IncludeResolver(string filename);

    internal static class PlaytestParser
    {
        // Matches $name sigils — ASCII-only names starting with letter or _
        internal static readonly Regex SigilRegex = new Regex(
            @"\$([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        // DSL keywords blocked as VAL values (prevent command injection via defs)
        internal static readonly HashSet<string> _DSL_KEYWORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "INVOKE", "MOVE", "MOVE_PATH", "SWEEP_PATH", "TELEPORT", "ASSERT", "ASSERT_CONSOLE_CLEAN",
            "ASSERT_BATCH", "ASSERT_NEAR", "ASSERT_CAPTURED", "ASSERT_CONSERVED", "ASSERT_CTA",
            "WAIT", "WAIT_UNTIL", "SNAPSHOT", "SET", "LOG", "TIMESCALE", "CAPTURE",
            "INVARIANT", "SIMULATE", "MONITOR", "TRACE_FLOW", "CLICK", "TAP",
            "SECTION", "DESC", "MACRO", "END_MACRO", "CALL", "INCLUDE", "ABORT_ON_FAIL",
            "VAL", "VAR", "WAIT_CAPTURED",
            "COMPLETE_PURCHASE", "INVOKE_REPEAT",
            "SET_DEFAULT_TIMEOUT", "ASSERT_ONE_ACTIVE",
            "PATH_PREFIX", "FOR", "END_FOR", "ASSERT_CHANGED",
            "CAPTURE_FRAMES", "ASSERT_FRAMES_DIFFER", "ASSERT_FRAMES_STATIC",
            "COMMENT", "END_COMMENT",
            "SET_ACTIVE",
            "SETUP", "SETUP_END", "TEARDOWN", "TEARDOWN_END",
            "WAIT_STABLE", "CAPTURE_MIN", "CAPTURE_MAX", "ASSERT_MIN", "ASSERT_MAX",
            "FILL", "FOCUS"
        };

        public static ParseResult Parse(string script, IncludeResolver resolver = null)
        {
            var rawLines = script.Split('\n');

            // Phase -1: expand INCLUDE directives (before any other processing)
            var sourcedLines = ExpandIncludes(rawLines, 0, resolver);

            // Phase 0: collect MACRO definitions
            var macros = new Dictionary<string, (string[] paramNames, SourcedLine[] body)>(StringComparer.OrdinalIgnoreCase);
            var cleanLines = new List<SourcedLine>();
            for (int i = 0; i < sourcedLines.Length; i++)
            {
                var t = sourcedLines[i].Text.Trim();
                if (t.StartsWith("MACRO ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts[1];
                    var paramNames = parts.Skip(2).ToArray();
                    var body = new List<SourcedLine>();
                    i++;
                    bool foundEnd = false;
                    while (i < sourcedLines.Length)
                    {
                        var bt = sourcedLines[i].Text.Trim();
                        if (bt.Equals("END_MACRO", StringComparison.OrdinalIgnoreCase)) { foundEnd = true; break; }
                        if (bt.StartsWith("MACRO ", StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Nested MACRO definitions are not supported");
                        // Strip COMMENT blocks from macro body
                        if (bt.Equals("COMMENT", StringComparison.OrdinalIgnoreCase) ||
                            bt.StartsWith("COMMENT ", StringComparison.OrdinalIgnoreCase))
                        {
                            i++;
                            bool foundCEnd = false;
                            while (i < sourcedLines.Length)
                            {
                                if (sourcedLines[i].Text.Trim().Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase)) { foundCEnd = true; break; }
                                i++;
                            }
                            if (!foundCEnd) throw new ArgumentException("COMMENT block missing END_COMMENT");
                            i++; // skip END_COMMENT line
                            continue;
                        }
                        if (bt.Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
                        body.Add(sourcedLines[i]);
                        i++;
                    }
                    if (!foundEnd) throw new ArgumentException($"MACRO '{name}' missing END_MACRO");
                    macros[name] = (paramNames, body.ToArray());
                    continue;
                }
                // skip stray END_MACRO (outside any MACRO block)
                if (t.Equals("END_MACRO", StringComparison.OrdinalIgnoreCase)) continue;
                // Strip COMMENT blocks (top-level)
                if (t.Equals("COMMENT", StringComparison.OrdinalIgnoreCase) ||
                    t.StartsWith("COMMENT ", StringComparison.OrdinalIgnoreCase))
                {
                    bool foundCEnd = false;
                    i++;
                    while (i < sourcedLines.Length)
                    {
                        if (sourcedLines[i].Text.Trim().Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase)) { foundCEnd = true; break; }
                        i++;
                    }
                    if (!foundCEnd) throw new ArgumentException("COMMENT block missing END_COMMENT");
                    continue; // outer for i++ moves past END_COMMENT
                }
                if (t.Equals("END_COMMENT", StringComparison.OrdinalIgnoreCase)) continue;
                cleanLines.Add(sourcedLines[i]);
            }

            // Phase 0.5: expand CALL directives (supports forward references and nesting)
            var expandedSourced = ExpandCalls(cleanLines, macros, 0);
            var lines = expandedSourced.Select(sl => sl.Text).ToArray();

            // Phase 0.7: collect VAL definitions and expand $sigils in all lines
            var collisionWarnings = new List<string>();
            var vals = CollectVals(lines, collisionWarnings);
            if (vals.Count > 0)
                lines = lines.Select(l => {
                    // For VAR declarations, preserve the $name token; only expand the @-query
                    var t = l.TrimStart();
                    if (t.StartsWith("VAR ", StringComparison.OrdinalIgnoreCase))
                    {
                        var tk = t.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                        if (tk.Length >= 3) return tk[0] + " " + tk[1] + " " + ExpandSigils(tk[2], vals);
                    }
                    return ExpandSigils(l, vals);
                }).ToArray();

            // Phase 1.1 + Phase 2: parse commands; collect VAR definitions
            var varDefs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var steps = new List<PlaytestStep>();
            List<PlaytestStep> setupSteps = null;
            List<PlaytestStep> teardownSteps = null;
            string pendingLabel = null;
            bool hasGlobalAbort = false;
            float defaultTimeout = 0f;
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

                var step = new PlaytestStep { RawLine = line };

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
                        step.Path = tokens[1];
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
                        step.Path = tokens[1];
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
                        step.Path = tokens[1];
                        break;
                    }

                    case "MOVE_PATH":
                    {
                        // MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]
                        var tiIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "TIMEOUT");
                        float pathTimeout = tiIdx >= 0 ? float.Parse(tokens[tiIdx + 1], CultureInfo.InvariantCulture) : 0f;
                        int endIdx = tiIdx >= 0 ? tiIdx : tokens.Length;
                        bool firstMove = true;
                        for (int ci = 1; ci < endIdx; ci++)
                        {
                            if (tokens[ci] == ">") continue;
                            var moveStep = new PlaytestStep
                            {
                                Type = StepType.Move,
                                Timeout = pathTimeout,
                                RawLine = line
                            };
                            SetPosition(moveStep, tokens[ci]);
                            if (firstMove) { moveStep.Label = pendingLabel; firstMove = false; }
                            steps.Add(moveStep);
                        }
                        pendingLabel = null;
                        continue; // skip the outer steps.Add(step)
                    }

                    case "SWEEP_PATH":
                    {
                        // SWEEP_PATH <charPath> DWELL <n>
                        //   x,y,z > x,y,z > ...
                        // UNTIL <query> <op> <val> [TIMEOUT n]
                        // Parse-time expansion → Move+Wait per waypoint, then WaitUntil
                        if (tokens.Length < 4)
                            throw new ArgumentException("SWEEP_PATH syntax: SWEEP_PATH <path> DWELL <n>");
                        var sweepPath = tokens[1];
                        var dwellIdx = Array.FindIndex(tokens, t => t.ToUpperInvariant() == "DWELL");
                        if (dwellIdx < 0 || dwellIdx + 1 >= tokens.Length)
                            throw new ArgumentException("SWEEP_PATH requires DWELL <seconds>");
                        float dwell = float.Parse(tokens[dwellIdx + 1], CultureInfo.InvariantCulture);

                        // Read waypoint lines until UNTIL or end-of-input
                        var waypointTokens = new List<string>();
                        string untilLine = null;
                        while (i + 1 < lines.Length)
                        {
                            var nextTrimmed = lines[i + 1].Trim();
                            if (string.IsNullOrEmpty(nextTrimmed) || nextTrimmed.StartsWith("#")) { i++; continue; }
                            if (nextTrimmed.StartsWith("UNTIL ", StringComparison.OrdinalIgnoreCase))
                            {
                                untilLine = nextTrimmed; i++;
                                break;
                            }
                            var firstWord = nextTrimmed.Split(new[] { ' ' }, 2, StringSplitOptions.None)[0].ToUpperInvariant();
                            if (_DSL_KEYWORDS.Contains(firstWord)) break;
                            waypointTokens.AddRange(nextTrimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
                            i++;
                        }
                        if (waypointTokens.Count == 0)
                            throw new ArgumentException("SWEEP_PATH: no waypoints found");

                        // Emit Move+Wait per waypoint
                        bool firstSweep = true;
                        foreach (var wt in waypointTokens)
                        {
                            if (wt == ">") continue;
                            var moveStep = new PlaytestStep { Type = StepType.Move, Path = sweepPath, RawLine = line };
                            SetPosition(moveStep, wt);
                            if (firstSweep) { moveStep.Label = pendingLabel; firstSweep = false; }
                            steps.Add(moveStep);
                            if (dwell > 0f)
                                steps.Add(new PlaytestStep { Type = StepType.Wait, Delay = dwell, RawLine = line });
                        }

                        // Emit WaitUntil from UNTIL clause
                        if (untilLine != null)
                        {
                            var ut = untilLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            // ut[0]="UNTIL" ut[1]=query ut[2]=op ut[3]=val [TIMEOUT n]
                            if (ut.Length < 4)
                                throw new ArgumentException("SWEEP_PATH UNTIL syntax: UNTIL <query> <op> <val> [TIMEOUT n]");
                            var untilStep = new PlaytestStep { Type = StepType.WaitUntil, RawLine = untilLine };
                            untilStep.Query = ut[1]; untilStep.Op = ut[2]; untilStep.Value = ut[3];
                            var utTiIdx = Array.FindIndex(ut, t => t.ToUpperInvariant() == "TIMEOUT");
                            if (utTiIdx >= 0) { untilStep.Timeout = float.Parse(ut[utTiIdx + 1], CultureInfo.InvariantCulture); untilStep.HasExplicitTimeout = true; }
                            steps.Add(untilStep);
                        }

                        pendingLabel = null;
                        continue; // skip outer steps.Add(step)
                    }

                    case "COMPLETE_PURCHASE":
                    {
                        // COMPLETE_PURCHASE <path> EXPECT
                        //   <q1>,<q2>,...
                        // TIMEOUT <n>
                        // Parse-time expansion → Invoke + compound WaitUntil
                        if (tokens.Length < 2) throw new ArgumentException("COMPLETE_PURCHASE syntax: COMPLETE_PURCHASE <path> EXPECT");
                        var cpPath = tokens[1];
                        steps.Add(new PlaytestStep
                        {
                            Type = StepType.Invoke, Path = cpPath,
                            Component = "PlacementPurchase", Method = "CompletePurchase",
                            Args = "", RawLine = line, Label = pendingLabel
                        });
                        pendingLabel = null;

                        var expectQueries = new List<string>();
                        float cpTimeout = 5f;
                        bool cpTimeoutExplicit = false;
                        while (i + 1 < lines.Length)
                        {
                            var nextT = lines[i + 1].Trim();
                            if (string.IsNullOrEmpty(nextT) || nextT.StartsWith("#")) { i++; continue; }
                            if (nextT.StartsWith("TIMEOUT ", StringComparison.OrdinalIgnoreCase))
                            {
                                cpTimeout = float.Parse(nextT.Split(new[] { ' ' }, 2)[1].Trim(), CultureInfo.InvariantCulture);
                                cpTimeoutExplicit = true;
                                i++;
                                break;
                            }
                            if (nextT.StartsWith("EXPECT ", StringComparison.OrdinalIgnoreCase))
                            {
                                var rest = nextT.Substring(7).Trim();
                                expectQueries.AddRange(rest.Split(',').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)));
                                i++;
                                continue;
                            }
                            // Plain comma-separated continuation line
                            expectQueries.AddRange(nextT.Split(',').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)));
                            i++;
                        }

                        if (expectQueries.Count > 0)
                        {
                            var wu = new PlaytestStep { Type = StepType.WaitUntil, Timeout = cpTimeout, HasExplicitTimeout = cpTimeoutExplicit, RawLine = line };
                            wu.Query = expectQueries[0]; wu.Op = "=="; wu.Value = "True";
                            if (expectQueries.Count > 1)
                            {
                                wu.Queries = expectQueries.Skip(1).ToArray();
                                wu.BatchOps = Enumerable.Repeat("==", expectQueries.Count - 1).ToArray();
                                wu.BatchValues = Enumerable.Repeat("True", expectQueries.Count - 1).ToArray();
                                wu.IsOr = false;
                            }
                            steps.Add(wu);
                        }
                        continue; // skip outer steps.Add
                    }

                    case "INVOKE_REPEAT":
                    {
                        // INVOKE_REPEAT <count> <path> <comp> <method> [args]
                        // [EXPECT <query> <op> <val> [TIMEOUT n]]
                        // Parse-time expansion → N Invoke steps + optional WaitUntil
                        if (tokens.Length < 5)
                            throw new ArgumentException("INVOKE_REPEAT syntax: INVOKE_REPEAT <count> <path> <comp> <method> [args]");
                        int repeatCount = int.Parse(tokens[1]);
                        var irPath = tokens[2]; var irComp = tokens[3]; var irMethod = tokens[4];
                        // G2: join all remaining tokens so multi-arg calls are preserved
                        var irArgs = tokens.Length > 5 ? string.Join(" ", tokens, 5, tokens.Length - 5) : "";

                        bool firstInvoke = true;
                        for (int ri = 0; ri < repeatCount; ri++)
                        {
                            var invStep = new PlaytestStep
                            {
                                Type = StepType.Invoke, Path = irPath, Component = irComp,
                                Method = irMethod, Args = irArgs, RawLine = line
                            };
                            if (firstInvoke) { invStep.Label = pendingLabel; firstInvoke = false; }
                            steps.Add(invStep);
                        }
                        pendingLabel = null;

                        // Read optional EXPECT line
                        while (i + 1 < lines.Length)
                        {
                            var nextT = lines[i + 1].Trim();
                            if (string.IsNullOrEmpty(nextT) || nextT.StartsWith("#")) { i++; continue; }
                            if (nextT.StartsWith("EXPECT ", StringComparison.OrdinalIgnoreCase))
                            {
                                var et = nextT.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (et.Length < 4) throw new ArgumentException("INVOKE_REPEAT EXPECT syntax: EXPECT <query> <op> <val> [TIMEOUT n]");
                                var wu = new PlaytestStep
                                {
                                    Type = StepType.WaitUntil, Query = et[1], Op = et[2], Value = et[3], RawLine = nextT
                                };
                                var etTiIdx = Array.FindIndex(et, t => t.ToUpperInvariant() == "TIMEOUT");
                                if (etTiIdx >= 0) { wu.Timeout = float.Parse(et[etTiIdx + 1], CultureInfo.InvariantCulture); wu.HasExplicitTimeout = true; }
                                steps.Add(wu);
                                i++;
                            }
                            break; // EXPECT is optional; stop after first non-blank non-comment line
                        }
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

                    default:
                        throw new ArgumentException($"Unknown command: {cmd}");
                }
                step.SourceFile    = sourced.File;
                step.SourceLine    = sourced.Line;
                step.MacroStack    = sourced.MacroStack;
                step.SectionContext = currentSection;
                step.Label = pendingLabel;
                pendingLabel = null;
                switch (parsingSection)
                {
                    case 1: setupSteps.Add(step); break;
                    case 2: teardownSteps.Add(step); break;
                    default: steps.Add(step); break;
                }
            }

            // Phase 0.8: warn on unresolved $sigils (always — even without any VAL/VAR defs)
            List<string> warnings = collisionWarnings.Count > 0 ? collisionWarnings : null;
            foreach (var expandedLine in lines)
            {
                var lt = expandedLine.Trim();
                if (lt.StartsWith("#") || lt.StartsWith("VAR ", StringComparison.OrdinalIgnoreCase) ||
                    lt.StartsWith("VAL ", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (Match m in SigilRegex.Matches(expandedLine))
                {
                    var sigil = m.Groups[1].Value;
                    if (!vals.ContainsKey(sigil) && !varDefs.ContainsKey(sigil))
                    {
                        warnings = warnings ?? new List<string>();
                        var _candidates = vals.Keys.Concat(varDefs.Keys);
                        var _suggestion = StringDistance.ClosestMatch(sigil, _candidates);
                        var _hint = _suggestion != null ? $" Did you mean ${_suggestion}?" : " (typo in VAL/VAR name?)";
                        warnings.Add($"Unresolved $sigil: ${sigil}{_hint}");
                    }
                }
            }

            return new ParseResult
            {
                Steps = steps,
                SetupSteps = setupSteps,
                TeardownSteps = teardownSteps,
                VarDefs = varDefs.Count > 0 ? varDefs : null,
                ValDefs = vals.Count > 0 ? vals : null,
                Warnings = warnings,
                HasGlobalAbort = hasGlobalAbort,
                DefaultTimeout = defaultTimeout
            };
        }

        // ── Phase helpers ──────────────────────────────────────────────────────────

        // Phase -1: expand INCLUDE directives recursively; each line carries provenance (file, line number)
        internal static SourcedLine[] ExpandIncludes(string[] lines, int depth, IncludeResolver resolver, string sourceFile = null)
        {
            if (depth > 5) throw new ArgumentException("INCLUDE depth exceeded (max 5)");
            var result = new List<SourcedLine>();
            for (int idx = 0; idx < lines.Length; idx++)
            {
                var line = lines[idx];
                var t = line.Trim();
                if (!t.StartsWith("INCLUDE ", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new SourcedLine { Text = line, File = sourceFile, Line = idx });
                    continue;
                }
                var filename = t.Substring(8).Trim().Trim('"');
                if (filename.Contains("..") || System.IO.Path.IsPathRooted(filename))
                    throw new ArgumentException($"INCLUDE '{filename}': path traversal not allowed");
                // Canonicalize to block symlink traversal outside PlaytestDefs/
                if (resolver == null)
                {
                    var basePath = System.IO.Path.GetFullPath("Assets/PlaytestDefs/").TrimEnd(
                        System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                    var fullPath = System.IO.Path.GetFullPath(
                        System.IO.Path.Combine("Assets/PlaytestDefs/", filename));
                    if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"INCLUDE '{filename}': path outside PlaytestDefs/");
                }
                string content;
                try
                {
                    if (resolver != null)
                        content = resolver(filename);
                    else
                        content = System.IO.File.ReadAllText($"Assets/PlaytestDefs/{filename}");
                }
                catch (Exception e) { throw new ArgumentException($"INCLUDE '{filename}': {e.Message}", e); }
                var included = content.Split('\n');
                result.AddRange(ExpandIncludes(included, depth + 1, resolver, filename));
            }
            return result.ToArray();
        }

        // Phase 0.7: collect VAL definitions with topo-sort cycle detection
        internal static Dictionary<string, string> CollectVals(string[] lines, List<string> warnings = null)
        {
            // First pass: gather raw unexpanded values
            var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (!t.StartsWith("VAL ", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = t.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    throw new ArgumentException($"VAL syntax: VAL $name value (got '{t}')");
                var firstWord = parts[2].Split(new[] { ' ' }, 2)[0];
                if (_DSL_KEYWORDS.Contains(firstWord))
                    throw new ArgumentException(
                        $"VAL '{parts[1]}': value cannot start with a DSL keyword (got '{firstWord}'). " +
                        $"Tip: wrap in quotes if it's a literal string value.");
                var key = parts[1].TrimStart('$');
                if (warnings != null && raw.ContainsKey(key))
                    warnings.Add($"Alias collision: local VAL ${key} shadows earlier definition (was '{raw[key]}', now '{parts[2]}')");
                raw[key] = parts[2];
            }
            if (raw.Count == 0) return raw;

            // Phase 0.7.1: collect PATH_PREFIX (first occurrence wins), apply to path VAL values
            // PATH_PREFIX applies across all INCLUDE-expanded lines; first occurrence wins.
            string pathPrefix = null;
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (!t.StartsWith("PATH_PREFIX ", StringComparison.OrdinalIgnoreCase)) continue;
                pathPrefix = t.Substring("PATH_PREFIX ".Length).Trim().TrimEnd('/');
                break;
            }
            if (pathPrefix != null)
            {
                foreach (var key in raw.Keys.ToList())
                {
                    if (raw[key].StartsWith("/"))
                        raw[key] = pathPrefix + raw[key];
                }
            }

            // Topo DFS: expand transitively, detect cycles
            var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(string name)
            {
                if (expanded.ContainsKey(name)) return;
                if (!visiting.Add(name))
                    throw new ArgumentException($"VAL cycle detected: ${name}");
                var val = raw[name];
                val = SigilRegex.Replace(val, m => {
                    var dep = m.Groups[1].Value;
                    if (!raw.ContainsKey(dep)) return m.Value; // unknown sigil — leave intact
                    Visit(dep);
                    return expanded[dep];
                });
                expanded[name] = val;
                visiting.Remove(name);
            }

            foreach (var name in raw.Keys) Visit(name);
            return expanded;
        }

        // Phase 0.7: expand $sigils in a single line using collected vals
        internal static string ExpandSigils(string line, Dictionary<string, string> vals)
        {
            if (vals == null || vals.Count == 0) return line;
            return SigilRegex.Replace(line, m => {
                var name = m.Groups[1].Value;
                return vals.TryGetValue(name, out var v) ? v : m.Value; // unknown → leave for VAR
            });
        }

        static List<SourcedLine> ExpandCalls(List<SourcedLine> lines,
            Dictionary<string, (string[] paramNames, SourcedLine[] body)> macros,
            int depth, string[] callStack = null)
        {
            if (depth > 10) throw new ArgumentException("MACRO recursion depth exceeded (max 10)");
            var result = new List<SourcedLine>();
            for (int ei = 0; ei < lines.Count; ei++)
            {
                var sourced = lines[ei];
                var t = sourced.Text.Trim();

                // ── FOR $var IN start..end ──────────────────────────────────────
                if (t.StartsWith("FOR ", StringComparison.OrdinalIgnoreCase))
                {
                    var forParts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (forParts.Length < 4 || !forParts[2].Equals("IN", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"FOR syntax: FOR $var IN start..end (got '{t}')");
                    var iterVar = forParts[1];
                    var rangeParts = forParts[3].Split(new[] { ".." }, StringSplitOptions.None);
                    if (rangeParts.Length != 2 ||
                        !int.TryParse(rangeParts[0], out int rangeStart) ||
                        !int.TryParse(rangeParts[1], out int rangeEnd))
                        throw new ArgumentException($"FOR range must be integer..integer (got '{forParts[3]}')");
                    if ((long)rangeEnd - (long)rangeStart > 10000)
                        throw new ArgumentException($"FOR range too large (max 10000 iterations)");

                    // Collect body until matching END_FOR (handle nested FOR)
                    var forBody = new List<SourcedLine>();
                    ei++;
                    bool foundEndFor = false;
                    int nestDepth = 0;
                    while (ei < lines.Count)
                    {
                        var bt = lines[ei].Text.Trim()
                            .Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[0];
                        if (bt.Equals("FOR", StringComparison.OrdinalIgnoreCase)) nestDepth++;
                        else if (bt.Equals("END_FOR", StringComparison.OrdinalIgnoreCase))
                        {
                            if (nestDepth == 0) { foundEndFor = true; break; }
                            nestDepth--;
                        }
                        forBody.Add(lines[ei]);
                        ei++;
                    }
                    if (!foundEndFor) throw new ArgumentException("FOR block missing END_FOR");

                    for (int iter = rangeStart; iter < rangeEnd; iter++)
                    {
                        var forExpanded = new List<SourcedLine>();
                        foreach (var bodyLine in forBody)
                        {
                            var sub = ReplaceWholeWord(bodyLine.Text, iterVar, iter.ToString());
                            forExpanded.Add(new SourcedLine
                            {
                                Text = sub,
                                File = bodyLine.File,
                                Line = bodyLine.Line,
                                MacroStack = bodyLine.MacroStack == null
                                    ? new[] { $"FOR:{iter}" }
                                    : bodyLine.MacroStack.Concat(new[] { $"FOR:{iter}" }).ToArray()
                            });
                        }
                        result.AddRange(ExpandCalls(forExpanded, macros, depth + 1, callStack));
                    }
                    continue;
                }

                // ── CALL macroName [args...] ────────────────────────────────────
                if (!t.StartsWith("CALL ", StringComparison.OrdinalIgnoreCase)) { result.Add(sourced); continue; }
                {
                    var parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var name = parts[1];
                    if (!macros.TryGetValue(name, out var macro))
                        throw new ArgumentException($"Unknown macro: {name}");
                    var callArgs = parts.Skip(2).ToArray();
                    if (callArgs.Length < macro.paramNames.Length)
                        throw new ArgumentException($"CALL {name}: expected {macro.paramNames.Length} args, got {callArgs.Length}");

                    var newStack = callStack == null
                        ? new[] { name }
                        : callStack.Concat(new[] { name }).ToArray();

                    var expanded = new List<SourcedLine>();
                    foreach (var bodyLine in macro.body)
                    {
                        var sub = bodyLine.Text;
                        for (int j = 0; j < macro.paramNames.Length; j++)
                            sub = ReplaceWholeWord(sub, macro.paramNames[j], callArgs[j]);
                        expanded.Add(new SourcedLine
                        {
                            Text = sub,
                            File = bodyLine.File,
                            Line = bodyLine.Line,
                            MacroStack = newStack
                        });
                    }
                    result.AddRange(ExpandCalls(expanded, macros, depth + 1, newStack));
                }
            }
            return result;
        }

        // Deferred position: @-expression stored as RawPosition; literal parsed into Position
        static void SetPosition(PlaytestStep step, string token)
        {
            if (token.StartsWith("@"))
                step.RawPosition = token;
            else
            {
                var f = ValueParser.ParseFloats(token, 3);
                step.Position = new Vector3(f[0], f[1], f[2]);
            }
        }

        // Replace whole-word occurrences of 'word' in a line (avoids partial matches)
        internal static string ReplaceWholeWord(string line, string word, string replacement)
        {
            if (string.IsNullOrEmpty(word)) return line;
            int idx = 0;
            while ((idx = line.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                char prevCh = idx > 0 ? line[idx - 1] : '\0';
                bool startOk = idx == 0 || !char.IsLetterOrDigit(prevCh) && prevCh != '$'
                    && (prevCh != '_' || word.Length > 0 && word[0] == '$');
                bool endOk = idx + word.Length >= line.Length ||
                             !char.IsLetterOrDigit(line[idx + word.Length]) && line[idx + word.Length] != '_';
                if (startOk && endOk)
                {
                    line = line.Substring(0, idx) + replacement + line.Substring(idx + word.Length);
                    idx += replacement.Length;
                }
                else idx++;
            }
            return line;
        }

        internal static (string path, string comp, string field) ResolveQuery(string query, PlaytestConfig config)
        {
            if (config != null)
            {
                var alias = config.FindAlias(query);
                if (alias != null) return (alias.path, alias.component, alias.field);
            }
            var parts = query.Split('|');
            if (parts.Length >= 4 && parts[1].Trim() == "UIDocument")
                return (parts[0].Trim(), "UIDocument", parts[2].Trim() + "|" + parts[3].Trim());
            if (parts.Length >= 3) return (parts[0].Trim(), parts[1].Trim(), parts[2].Trim());
            if (parts.Length == 2) return (parts[0].Trim(), parts[1].Trim(), "");
            return (query, "", "");
        }

        // ── Tokenizer: bracket/quote-aware split ───────────────────────────────

        internal static string[] SplitTokens(string line)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            bool inQuote = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\\' && i + 1 < line.Length && (line[i + 1] == '"' || line[i + 1] == '['))
                { sb.Append(line[++i]); continue; }
                if (c == '"' && depth == 0) { inQuote = !inQuote; }
                else if (c == '[' && !inQuote) { depth++; sb.Append(c); }
                else if (c == ']' && !inQuote && depth > 0) { depth--; sb.Append(c); }
                else if (c == ' ' && !inQuote && depth == 0)
                { if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); } }
                else sb.Append(c);
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens.ToArray();
        }

        // keywords that end the value in ASSERT/WAIT_UNTIL/INVARIANT
        private static readonly HashSet<string> _Ops = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
            { "==", "!=", ">=", "<=", ">", "<", "contains" };
        private static readonly HashSet<string> _StopKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "TIMEOUT", "AS", "AND", "OR", "ABORT" };

        // Returns (query, op, value, nextIdx-after-value).
        // nextIdx is the index of the first stop-keyword (or tokens.Length if none).
        // op == "" when no operator token is found (bool shorthand).
        private static (string q, string op, string v, int next) ParseQOV(string[] tokens, int start)
        {
            for (int i = start + 1; i < tokens.Length; i++)
            {
                if (!_Ops.Contains(tokens[i])) continue;
                var q = string.Join(" ", tokens, start, i - start);
                // Value is at least 1 token; stop-keywords checked from i+2 onward
                // so `ASSERT /x == TIMEOUT` correctly yields value="TIMEOUT"
                int vEnd = i + 2;
                while (vEnd < tokens.Length && !_StopKeywords.Contains(tokens[vEnd].ToUpperInvariant()))
                    vEnd++;
                var v = vEnd > i + 1 ? string.Join(" ", tokens, i + 1, vEnd - i - 1) : "";
                return (q, tokens[i], v, vEnd);
            }
            // No operator found — bool shorthand (path only)
            return (string.Join(" ", tokens, start, tokens.Length - start), "", "", tokens.Length);
        }

        internal static bool Compare(string actual, string op, string expected)
        {
            op = op.ToLowerInvariant();
            if (float.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var aF) &&
                float.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var eF))
            {
                return op switch
                {
                    "==" => Math.Abs(aF - eF) < 0.001f,
                    "!=" => Math.Abs(aF - eF) >= 0.001f,
                    ">" => aF > eF,
                    ">=" => aF >= eF,
                    "<" => aF < eF,
                    "<=" => aF <= eF,
                    _ => throw new ArgumentException($"Unknown operator: {op}")
                };
            }
            return op switch
            {
                "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
                "contains" => actual?.Contains(expected) == true,
                _ => throw new ArgumentException($"Operator '{op}' requires numeric values")
            };
        }

    }
}
