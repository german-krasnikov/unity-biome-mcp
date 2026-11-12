// PT-6/PT-7 (Plans/DSL/EVAL3_UNITY_MCP_PLAYTEST.md plan table): StepBuilder reads a command via
// PlaytestReader and fills a PlaytestStep from its slots — the "expected" side of the reader-vs-
// hand-parser differential (ReaderDifferentialTests.cs). Adapted from
// Plans/DSL/EVAL3_PROTOTYPES/playtest/proto_builder/StepBuilder.cs (E-2 measurement: 308/309 base
// scripts equal against PlaytestParser.BuildSteps). Test-lane only — zero production lines — until
// PT-8 flips the runtime default (plan §4).
//
// PT-6 registers the single-line, single-slot-fill constructs and the directives. PT-7 adds the
// multi-term query constructs (ASSERT, WAIT_UNTIL, ASSERT_CONSOLE_CLEAN, ASSERT_CONSERVED),
// ASSERT_BATCH, MCP/MCP_INTO and the lookahead D-heads (MOVE_PATH, SWEEP_PATH, COMPLETE_PURCHASE,
// INVOKE_REPEAT), plus the real Rules.Check (PT_MIXED/PT_CHAIN/PT_BRACKET).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace UnityMCP.Playtest.Core
{
    /// <summary>Accumulates steps/vars/flags while StepBuilder.Build walks a script — the builder's
    /// counterpart to the local variables PlaytestParser.BuildSteps carries through its loop.</summary>
    internal sealed class BuildState
    {
        public readonly List<PlaytestStep> Steps = new List<PlaytestStep>();
        public List<PlaytestStep> Setup, Teardown;
        public readonly Dictionary<string, string> Vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool GlobalAbort, ExpectFail;
        public float DefaultTimeout;
        public string Label, Section;
        public int Part;   // 0 main, 1 SETUP, 2 TEARDOWN
        public string[] Lines;
        public int I;      // index of the line the current command used (D-heads advance it further)

        public void Add(PlaytestStep s, SourcedLine src, string trimmed)
        {
            s.RawLine = src.Text; s.ExpandedRawLine = trimmed;
            s.SourceFile = src.File; s.SourceLine = src.Line; s.MacroStack = src.MacroStack; s.SectionContext = Section;
            s.Label = Label; Label = null;
            PlaytestParser.ConsumePendingExpectFail(s, ref ExpectFail);
            (Part == 1 ? Setup : Part == 2 ? Teardown : Steps).Add(s);
        }
    }

    /// <summary>A command as the builder reads it: slot values by name, normalized the way
    /// PlaytestParser's hand tokenizer did (quotes stripped through SplitTokens).</summary>
    internal sealed class Use
    {
        private readonly SlotBindings _b;
        public Use(SlotBindings b) { _b = b; }
        public bool Has(string k) => _b.Slots.ContainsKey(k);
        public bool Flag(string k) => _b.Flags.Contains(k);
        public string Raw(string k) => _b.Slots.TryGetValue(k, out var v) ? v.Raw : null;
        public float Num(string k) => float.Parse(Raw(k), CultureInfo.InvariantCulture);
        public string Norm(string k) => Has(k) ? string.Join(" ", PlaytestParser.SplitTokens(Raw(k))) : null;
        public string Unq(string k) { if (!Has(k)) return null; var t = PlaytestParser.SplitTokens(Raw(k)); return t.Length == 0 ? "" : t[0]; }
        public string[] Toks(string k) => Has(k) ? PlaytestParser.SplitTokens(Raw(k)) : new string[0];
    }

    /// <summary>What the grammar table alone cannot say: a fourth AND/OR term, a mixed AND+OR
    /// chain, or a name cut at an operator word inside brackets (surface.mjs `rule`). Stubbed to
    /// "no extra rule" for PT-6 — StepBuilder.Build calls it unconditionally; PT-7 fills it in.</summary>
    internal static class Rules
    {
        public static string Check(LineUse u) => null;
    }

    internal static class StepBuilder
    {
        internal delegate void Handler(Use u, PlaytestStep s, BuildState st);
        private static readonly Dictionary<string, Handler> Steps = new Dictionary<string, Handler>();
        private static readonly Dictionary<string, Action<Use, BuildState>> Directives = new Dictionary<string, Action<Use, BuildState>>();

        private static void Step(string construct, StepType type, Action<Use, PlaytestStep> fill) => Steps[construct] = (u, s, st) => { s.Type = type; fill(u, s); };

        static StepBuilder()
        {
            Step("PT_WAIT", StepType.Wait, (u, s) => s.Delay = u.Num("seconds"));
            Step("PT_TIMESCALE", StepType.TimeScale, (u, s) => s.Delay = u.Num("scale"));
            Step("PT_LOG", StepType.Log, (u, s) => s.Message = u.Norm("message") ?? "");
            Step("PT_INVOKE", StepType.Invoke, (u, s) => { s.Path = u.Unq("path"); s.Component = u.Unq("component"); s.Method = u.Unq("method"); s.Args = u.Norm("args") ?? ""; });
            Step("PT_SET", StepType.Set, (u, s) => { s.Path = u.Unq("path"); s.Component = u.Unq("component"); s.Method = u.Unq("field"); s.Args = u.Unq("value"); });
            Step("PT_SET_ACTIVE", StepType.SetActive, (u, s) => { s.Path = u.Unq("path"); s.Value = u.Unq("active"); });
            Step("PT_SNAPSHOT", StepType.Snapshot, (u, s) => s.Queries = (u.Norm("queries") ?? "").Split(','));
            Step("PT_CLICK", StepType.Click, Click);
            Step("PT_TAP", StepType.Click, Click);
            Step("PT_FILL", StepType.Fill, (u, s) => { s.Path = PlaytestParser.NormalizeUIHostPath(u.Unq("path")); s.Value = u.Norm("value") ?? ""; });
            Step("PT_FOCUS", StepType.Focus, (u, s) => s.Path = PlaytestParser.NormalizeUIHostPath(u.Unq("path")));
            Step("PT_ASSERT_NEAR", StepType.AssertNear, (u, s) => { s.Path = u.Unq("path1"); s.Value = u.Unq("path2"); s.Delay = u.Num("threshold"); });
            Step("PT_ASSERT_CTA", StepType.AssertCta, (u, s) => s.Op = u.Has("state") ? u.Unq("state").ToUpperInvariant() : "VISIBLE");
            Step("PT_ASSERT_ONE_ACTIVE", StepType.AssertOneActive, (u, s) => s.Queries = new[] { u.Unq("path1"), u.Unq("path2") }.Concat(u.Toks("more")).ToArray());
            Step("PT_MONITOR", StepType.Monitor, (u, s) => { var q = u.Unq("query"); if (q != null && q.ToUpperInvariant() != "STOP") s.Query = q; });
            Step("PT_CAPTURE", StepType.Capture, (u, s) => { s.Message = u.Unq("label"); s.Query = u.Norm("query"); });
            Step("PT_CAPTURE_MIN", StepType.CaptureMin, Tracker);
            Step("PT_CAPTURE_MAX", StepType.CaptureMax, Tracker);
            Step("PT_ASSERT_CHANGED", StepType.AssertChanged, (u, s) => s.Message = u.Unq("label"));
            Step("PT_ASSERT_FRAMES_DIFFER", StepType.AssertFramesDiffer, (u, s) => s.Message = u.Unq("label"));
            Step("PT_ASSERT_FRAMES_STATIC", StepType.AssertFramesStatic, (u, s) => s.Message = u.Unq("label"));
            Step("PT_ASSERT_CAPTURED", StepType.AssertCaptured, (u, s) => { s.Message = u.Unq("label"); s.Op = u.Unq("mode"); if (u.Has("subop")) { s.Args = u.Unq("subop"); s.Value = u.Unq("value"); } });
            Step("PT_ASSERT_MIN", StepType.AssertMin, Bound);
            Step("PT_ASSERT_MAX", StepType.AssertMax, Bound);
            Step("PT_INVARIANT", StepType.Invariant, (u, s) => { s.Query = u.Norm("query"); s.Op = u.Raw("op"); s.Value = u.Norm("value"); });
            Step("PT_CAPTURE_FRAMES", StepType.CaptureFrames, (u, s) =>
            {
                s.Timeout = (int)u.Num("count"); s.Delay = u.Num("interval");
                s.Component = u.Has("camera") ? u.Unq("camera") : "game"; s.Op = u.Has("mode") ? u.Unq("mode").ToLowerInvariant() : "strip"; s.Message = u.Unq("label");
            });
            Step("PT_WAIT_CAPTURED", StepType.WaitCaptured, (u, s) =>
            {
                s.Message = u.Unq("label"); s.Op = u.Unq("mode").ToUpperInvariant();
                if ((s.Op == "INCREASED_BY" || s.Op == "DECREASED_BY") && u.Has("subop")) { s.Args = u.Unq("subop"); s.Value = u.Unq("value"); }
                if (u.Has("timeout") || u.Has("timeout2")) s.Timeout = u.Has("timeout") ? u.Num("timeout") : u.Num("timeout2");
                if (u.Has("over")) s.Delay = u.Num("over");
            });
            Step("PT_WAIT_STABLE", StepType.WaitStable, (u, s) => { s.Query = u.Unq("query"); s.Value = u.Unq("delta"); s.Delay = u.Num("over"); if (u.Has("timeout")) s.Timeout = u.Num("timeout"); });
            Step("PT_SIMULATE", StepType.Simulate, (u, s) =>
            {
                s.SimulatorName = u.Unq("name");
                if (u.Has("duration")) s.Timeout = u.Num("duration"); if (u.Has("timescale")) s.Delay = u.Num("timescale");
                if (u.Has("target")) s.Path = u.Unq("target").Trim('"'); if (u.Has("frequency")) s.Value = u.Unq("frequency");
            });
            Step("PT_TRACE_FLOW", StepType.TraceFlow, (u, s) => { s.Path = u.Unq("from"); s.Query = u.Unq("to"); s.Method = u.Unq("field"); if (u.Has("timeout")) s.Timeout = u.Num("timeout"); });
            Step("PT_MOVE", StepType.Move, (u, s) => { s.Path = u.Has("path") ? u.Unq("path") : null; PlaytestParser.SetPositionProxy(s, u.Unq("to")); });
            Step("PT_TELEPORT", StepType.Teleport, (u, s) => { s.Path = u.Unq("path"); PlaytestParser.SetPositionProxy(s, u.Unq("to")); });
            Step("PT_SECTION", StepType.Section, (u, s) => s.Message = (u.Norm("name") ?? "").Trim('"'));

            Directives["PT_DESC"] = (u, st) => st.Label = (u.Norm("label") ?? "").Trim('"');
            Directives["PT_ABORT_ON_FAIL"] = (u, st) => st.GlobalAbort = true;
            Directives["PT_EXPECT_FAIL"] = (u, st) => PlaytestParser.SetPendingExpectFail(ref st.ExpectFail);
            Directives["PT_SET_DEFAULT_TIMEOUT"] = (u, st) => st.DefaultTimeout = u.Num("seconds");
            Directives["PT_SETUP"] = (u, st) => { st.Part = 1; st.Setup = st.Setup ?? new List<PlaytestStep>(); };
            Directives["PT_SETUP_END"] = (u, st) => st.Part = 0;
            Directives["PT_TEARDOWN"] = (u, st) => { st.Part = 2; st.Teardown = st.Teardown ?? new List<PlaytestStep>(); };
            Directives["PT_TEARDOWN_END"] = (u, st) => st.Part = 0;
            Directives["PT_VAR"] = (u, st) => st.Vars[u.Raw("name").TrimStart('$')] = u.Raw("query");
        }

        private static void Click(Use u, PlaytestStep s) { s.Path = PlaytestParser.NormalizeUIHostPath(u.Unq("path")); if (u.Has("wait")) s.Delay = u.Num("wait"); }
        private static void Tracker(Use u, PlaytestStep s) { s.Message = u.Unq("name").TrimStart('$'); s.Query = u.Unq("query"); if (u.Has("over")) s.Delay = u.Num("over"); }
        private static void Bound(Use u, PlaytestStep s) { s.Message = u.Unq("name").TrimStart('$'); s.Op = u.Raw("op"); s.Value = u.Unq("value"); }

        /// <summary>The build loop: PlaytestParser.BuildSteps' counterpart over the reader. PT-6
        /// covers the constructs registered above; a construct PT-7 has not added yet throws
        /// InvalidOperationException("no builder for " + construct) — ReaderDifferentialTests
        /// names exactly which ones are still pending.</summary>
        internal static void Build(string[] lines, SourcedLine[] sourced, out List<PlaytestStep> steps, out List<PlaytestStep> setup, out List<PlaytestStep> teardown,
            out Dictionary<string, string> vars, out bool abort, out float defaultTimeout)
        {
            var st = new BuildState { Lines = lines };
            var nos = Enumerable.Range(1, lines.Length).ToArray();
            var diags = new List<PlaytestDiagnostic>();
            var i = 0;
            while (i < lines.Length)
            {
                var use = PlaytestReader.Read(lines, nos, ref i, diags);
                if (diags.Count > 0) throw new ArgumentException(diags[0].Code + " " + diags[0].Line + ":" + diags[0].Col + ": " + diags[0].Text);
                if (use == null) continue;
                var c = use.Form.Construct; var u = new Use(use.Head); st.I = use.Index;
                var rule = Rules.Check(use); if (rule != null) throw new ArgumentException(rule + " " + (use.Index + 1) + ":" + (use.Col + 1));
                if (Directives.TryGetValue(c, out var d)) { d(u, st); continue; }
                if (c == "PT_VAL" || c == "PT_PATH_PREFIX" || c == "PT_INCLUDE") continue; // text-to-text phases, done before the reader
                if (!Steps.TryGetValue(c, out var h)) throw new InvalidOperationException("no builder for " + c);
                var step = new PlaytestStep();
                h(u, step, st);
                if (c == "PT_SECTION") st.Section = step.Message;
                st.Add(step, sourced[use.Index], lines[use.Index].Trim());
            }
            steps = st.Steps; setup = st.Setup; teardown = st.Teardown; vars = st.Vars; abort = st.GlobalAbort; defaultTimeout = st.DefaultTimeout;
        }
    }
}
