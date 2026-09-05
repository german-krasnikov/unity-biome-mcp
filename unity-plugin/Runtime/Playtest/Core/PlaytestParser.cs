using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Playtest.Core
{
    public enum StepType { Move, Wait, WaitUntil, Assert, AssertConsoleClean, Snapshot, Invoke, Set, Log, TimeScale, Teleport, AssertBatch, AssertNear, Capture, AssertCaptured, Invariant, AssertConserved, Simulate, Monitor, TraceFlow, AssertCta, Click, Section, Desc, WaitCaptured, AssertOneActive, AssertChanged, CaptureFrames, AssertFramesDiffer, AssertFramesStatic, SetActive, Setup, Teardown, WaitStable, CaptureMin, CaptureMax, AssertMin, AssertMax, Fill, Focus, Mcp }

    /// <summary>A script line with origin metadata (file, line number, macro call chain).</summary>
    public struct SourcedLine
    {
        public string Text;
        public string File;        // null = main inline script
        public int    Line;        // 0-based line number in File (or inline script)
        public string[] MacroStack; // null = direct; ["outer","inner"] = nested call chain
    }

    [Serializable]
    public class PlaytestStep
    {
        public StepType Type;
        public string Path;
        public Float3 Position;
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
        // Parse-time-expanded form of RawLine. The Composer uses this only when a
        // static VAL was resolved but its declaration is not represented as a step.
        public string ExpandedRawLine;
        public string[] BatchOps;
        public string[] BatchValues;
        public string SimulatorName;
        public bool IsOr;        // true = OR logic for compound WAIT_UNTIL, false = AND
        public bool AbortOnFail; // true = stop Play Mode on timeout
        public string Label;     // set by preceding DESC line
        public string ResultVar; // MCP ... INTO $name — capture name without '$'; null = no capture
        public bool ExpectFail;  // C06: set by a preceding EXPECT_FAIL line; inverts this step's pass/fail

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
            ExpandedRawLine = ExpandedRawLine,
            BatchOps = BatchOps, BatchValues = BatchValues,
            SimulatorName = SimulatorName, IsOr = IsOr,
            AbortOnFail = AbortOnFail, Label = Label, ResultVar = ResultVar, ExpectFail = ExpectFail,
            SourceFile = SourceFile, SourceLine = SourceLine,
            MacroStack = MacroStack, SectionContext = SectionContext
        };
    }

    // Carries both steps and var-bindings out of Parse() with zero breakage for existing callers.
    public class ParseResult : IEnumerable<PlaytestStep>
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
        /// <summary>Fatal parse errors that block execution (e.g. unresolved sigil in strict mode).
        /// Non-null means PlaytestRunner must abort before executing any steps.</summary>
        public List<string> Errors;
        /// <summary>VAL definitions keyed by name (without $). Null if none declared.</summary>
        public Dictionary<string, string> ValDefs;
        public bool HasGlobalAbort { get; set; }
        /// <summary>SET_DEFAULT_TIMEOUT value in seconds. 0 = not set (runner uses 5f fallback).</summary>
        public float DefaultTimeout { get; set; }
        /// <summary>Parsed `# @directive` header metadata (B03/B04). Never null — a header-less
        /// script still gets a defaulted <see cref="PlaytestHeader"/> (no NPE trap for consumers).</summary>
        public PlaytestHeader Header { get; set; }

        public int Count => Steps.Count;
        public PlaytestStep this[int i] => Steps[i];
        public bool Exists(Predicate<PlaytestStep> match) => Steps.Exists(match);
        public int IndexOf(PlaytestStep item) => Steps.IndexOf(item);
        public IEnumerator<PlaytestStep> GetEnumerator() => Steps.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => Steps.GetEnumerator();

        public static implicit operator List<PlaytestStep>(ParseResult r) => r.Steps;
    }

    /// <summary>Resolves INCLUDE directives — returns full file content as a string.</summary>
    public delegate string IncludeResolver(string filename);

    public static partial class PlaytestParser
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

        public static ParseResult Parse(string script, IncludeResolver resolver = null, bool strict = false)
        {
            var rawLines = script.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            // Phase -2: scan `# @directive` header lines, pre-INCLUDE (B03/B04).
            var header = ScanHeader(script);

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

            BuildSteps(lines, expandedSourced,
                out var steps, out var setupSteps, out var teardownSteps,
                out var varDefs, out var hasGlobalAbort, out var defaultTimeout);

            // Phase 0.75 (B09 / INV-022): reject Play-bound verbs under `@needs editmode` —
            // a compile error, not a runtime failure. Lives in PlaytestParser.Directives.cs (R-04).
            List<string> errors = RejectPlayBoundVerbsUnderEditmode(header, steps, setupSteps, teardownSteps);

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
                        var _candidates = vals.Keys.Concat(varDefs.Keys);
                        var _suggestion = StringDistance.ClosestMatch(sigil, _candidates);
                        var _hint = _suggestion != null ? $" Did you mean ${_suggestion}?" : " (typo in VAL/VAR name?)";
                        if (strict)
                        {
                            errors = errors ?? new List<string>();
                            errors.Add($"Unresolved $sigil: ${sigil}{_hint}");
                        }
                        else
                        {
                            warnings = warnings ?? new List<string>();
                            warnings.Add($"Unresolved $sigil: ${sigil}{_hint}");
                        }
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
                Errors = errors,
                HasGlobalAbort = hasGlobalAbort,
                DefaultTimeout = defaultTimeout,
                Header = header
            };
        }
    }
}
