// PT-6/PT-7 (Plans/DSL/EVAL3_UNITY_MCP_PLAYTEST.md plan table, E-2 measurement): the reader
// (PlaytestReader + StepBuilder, StepBuilder.cs) against the hand parser (PlaytestParser.BuildSteps)
// on the committed reader.rows corpus (PT-5, RowsTests.cs) — same scripts RowsTests.cs already
// proves the reader *reads* correctly; here both sides build full PlaytestStep objects and must
// agree field by field, or the disagreement must land in a named, counted class (ClassifyStricter /
// ClassifyFieldDiff below) — plan §6 Q1/Q2 open questions for the owner, not bugs in this harness.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace UnityMCP.Playtest.Core.PureTests
{
    [TestFixture]
    public class ReaderDifferentialTests
    {
        private static readonly FieldInfo[] StepFields = typeof(PlaytestStep)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();

        // Pre-reader text-to-text phases (FOR/MACRO/INCLUDE/VAL/...): expanded before BuildSteps and
        // StepBuilder.Build ever see a line, so a raw script carrying one is out of scope for this
        // per-command differential (mirrors proto_builder/Diff.cs's `Pre` set).
        private static readonly HashSet<string> PreReaderPhases = new HashSet<string>(StringComparer.Ordinal)
        {
            "FOR", "END_FOR", "COMMENT", "END_COMMENT", "MACRO", "END_MACRO", "CALL", "INCLUDE", "VAL", "PATH_PREFIX",
        };

        // PT-7 registered every remaining construct (ASSERT/WAIT_UNTIL/ASSERT_CONSOLE_CLEAN/
        // ASSERT_CONSERVED/ASSERT_BATCH/MCP/MCP_INTO/the D-heads) and filled in Rules.Check — so
        // "no builder for X" should never happen again. Left empty, not deleted: a construct that
        // regresses to unregistered now falls straight into `unexpected` below instead of being
        // silently swallowed (double-red — see Build_EveryRow's doc comment).
        private static readonly HashSet<string> PendingConstructs = new HashSet<string>(StringComparer.Ordinal);

        private const string NoBuilderPrefix = "InvalidOperationException:no builder for ";

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            for (var depth = 0; depth < 12 && dir != null; depth++, dir = dir.Parent)
                if (Directory.Exists(Path.Combine(dir.FullName, "unity-plugin")) && Directory.Exists(Path.Combine(dir.FullName, "dsl", "constructs")))
                    return dir.FullName;
            throw new InvalidOperationException("Could not locate repo root (unity-plugin/ + dsl/constructs/ sentinels) walking up from " + TestContext.CurrentContext.TestDirectory);
        }

        private static string Unesc(string text) => Regex.Replace(text, @"\\u([0-9a-f]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());

        private static string FirstWord(string line) { var t = line.Trim(); return t.Length == 0 ? "" : Regex.Match(t, @"^\S+").Value; }

        private static bool HasPreReaderPhase(string script) =>
            script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Any(l => PreReaderPhases.Contains(FirstWord(l)));

        // dsl/playtest/reader.rows "src"/"ex" scripts: the committed base corpus (PT-5) — excludes
        // the del/dup/swap/.../bom/crlf mutant rows, which belong to the reader-vs-grammar
        // measurement (RowsTests.cs), not this hand-parser-vs-builder differential.
        private static List<string> LoadScripts()
        {
            var path = Path.Combine(RepoRoot(), "dsl", "playtest", "reader.rows");
            var shape = new Regex(@"^(\S+) ""([^""]*)"" (.+)$");
            var scripts = new List<string>();
            var seen = new HashSet<string>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var m = shape.Match(line);
                if (!m.Success || (m.Groups[1].Value != "src" && m.Groups[1].Value != "ex")) continue;
                var script = Unesc(m.Groups[2].Value);
                if (!seen.Add(script) || HasPreReaderPhase(script)) continue;
                scripts.Add(script);
            }
            return scripts;
        }

        private static string Val(object v) => v == null ? "∅" : v is string[] a ? "[" + string.Join("|", a) + "]"
            : v is Float3 f ? $"({f.x},{f.y},{f.z})" : v is float x ? x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : v.ToString();

        private static List<string> Dump(PlaytestStep s) => StepFields.Select(f => f.Name + "=" + Val(f.GetValue(s))).ToList();

        private static List<List<string>> DumpAll(List<PlaytestStep> a, List<PlaytestStep> b, List<PlaytestStep> c,
            Dictionary<string, string> vars, bool abort, float defaultTimeout)
        {
            var all = new List<List<string>>();
            foreach (var l in new[] { a, b, c }) { all.Add(new List<string> { l == null ? "<null>" : "n=" + l.Count }); if (l != null) all.AddRange(l.Select(Dump)); }
            all.Add(new List<string> { "vars=" + string.Join(";", vars.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + ":" + k.Value)), "abort=" + abort, "dt=" + Val(defaultTimeout) });
            return all;
        }

        private static List<SourcedLine> Sourced(string[] lines) =>
            lines.Select((t, i) => new SourcedLine { Text = t, File = null, Line = i, MacroStack = null }).ToList();

        private static (List<List<string>> d, string err) Hand(string[] lines)
        {
            try
            {
                PlaytestParser.BuildSteps(lines, Sourced(lines), out var steps, out var setup, out var teardown, out var vars, out var abort, out var dt);
                return (DumpAll(steps, setup, teardown, vars, abort, dt), null);
            }
            catch (Exception e) { return (null, e.GetType().Name); }
        }

        private static (List<List<string>> d, string err) Mine(string[] lines)
        {
            try
            {
                StepBuilder.Build(lines, Sourced(lines).ToArray(), out var steps, out var setup, out var teardown, out var vars, out var abort, out var dt);
                return (DumpAll(steps, setup, teardown, vars, abort, dt), null);
            }
            catch (Exception e) { return (null, e is ArgumentException || e is InvalidOperationException ? e.GetType().Name + ":" + e.Message : e.GetType().Name); }
        }

        private static string FirstDifference(List<List<string>> a, List<List<string>> b)
        {
            for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                for (var j = 0; j < Math.Min(a[i].Count, b[i].Count); j++)
                    if (a[i][j] != b[i][j]) { var f = a[i][j].Split('=')[0]; return f + ": hand " + a[i][j].Substring(f.Length + 1) + " / builder " + b[i][j].Substring(f.Length + 1); }
                if (a[i].Count != b[i].Count) return "shape";
            }
            return a.Count != b.Count ? "step count " + a.Count + " vs " + b.Count : null;
        }

        // Named classes for a divergence the E-2 measurement already found and the plan (§6 Q1/Q2)
        // leaves for the owner to approve or close — not bugs in this harness. Each class is
        // counted, never silently dropped: Build_EveryRow below only lets a *classified* reason
        // through; anything else still fails the test.

        // STRICTER: the reader (grammar) rejects a script the hand parser accepted.
        private static string ClassifyStricter(string builderErr)
        {
            if (builderErr.StartsWith(NoBuilderPrefix, StringComparison.Ordinal))
            {
                var construct = builderErr.Substring(NoBuilderPrefix.Length);
                if (PendingConstructs.Contains(construct)) return "PT-7 pending: " + construct + " not registered yet";
                return null;
            }
            if (builderErr.Contains("PT_UNKNOWN_KEYWORD") || builderErr.Contains("block is missing END"))
                return "I4 case-sensitive keyword (hand upper-cases the verb / END before dispatch; plan §6 Q2, open)";
            if (builderErr.Contains("(?!.*\\["))
                return "I-BRACKET a single-token path/query/target slot's Match pattern does not grant the bracket-protects-spaces leniency hand's SplitTokens gives every token";
            if (builderErr.Contains("is not one of") || builderErr.Contains("is not like") || builderErr.Contains("is not a uint") || builderErr.Contains("is not a number"))
                return "I-VALIDATION grammar enforces a slot's declared vocabulary/pattern (enum, numeric format) the hand parser's untyped string capture never checked";
            if (builderErr.Contains("unexpected \"#\""))
                return "I-COMMENT hand strips an inline '#' comment the grammar does not model";
            if (builderErr.Contains("unexpected \"") || builderErr.Contains("the line ends early"))
                return "I1 hand's generic tail/arity check tolerates tokens the grammar's fixed slot order rejects";
            return null;
        }

        // DIFF: both sides build a step, but one field differs.
        private static readonly Regex ValueDiff = new Regex("^Value: hand (.*) / builder (.*)$");
        private static string ClassifyFieldDiff(string fd)
        {
            var m = ValueDiff.Match(fd);
            if (m.Success && m.Groups[2].Value.StartsWith(m.Groups[1].Value + " TIMEOUT ", StringComparison.Ordinal))
                return "I1 grammar's value slot swallows a trailing TIMEOUT tail hand strips generically (construct defines no timeout slot)";
            return null;
        }

        [Test]
        public void Build_EveryRow_AgreesWithTheHandParser_OrIsANamedDivergence()
        {
            var scripts = LoadScripts();
            Assert.That(scripts, Is.Not.Empty, "reader.rows produced no src/ex scripts to compare");

            var same = 0; var classified = new Dictionary<string, int>(); var unexpected = new List<string>();
            void Count(string reason) => classified[reason] = classified.TryGetValue(reason, out var n) ? n + 1 : 1;

            foreach (var script in scripts)
            {
                var lines = script.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                var h = Hand(lines);
                var m = Mine(lines);

                if (h.d != null && m.d != null)
                {
                    var fd = FirstDifference(h.d, m.d);
                    if (fd == null) { same++; continue; }
                    var reason = ClassifyFieldDiff(fd);
                    if (reason != null) Count(reason);
                    else unexpected.Add("DIFF " + script.Replace("\n", "\\n") + " | " + fd);
                }
                else if (h.d == null && m.d == null)
                {
                    same++; // both correctly reject the script
                }
                else if (h.d != null)
                {
                    var reason = ClassifyStricter(m.err);
                    if (reason != null) Count(reason);
                    else unexpected.Add("STRICTER " + script.Replace("\n", "\\n") + " -> " + m.err);
                }
                else unexpected.Add("SOFTER " + script.Replace("\n", "\\n") + " -> hand " + h.err);
            }

            TestContext.WriteLine($"scripts {scripts.Count}, same {same}, classified {classified.Values.Sum()}, unexpected {unexpected.Count}");
            foreach (var kv in classified.OrderByDescending(kv => kv.Value)) TestContext.WriteLine($"  {kv.Value,4} {kv.Key}");
            foreach (var u in unexpected.Take(40)) TestContext.WriteLine("  " + u);
            Assert.That(unexpected, Is.Empty);
        }

        // Control: the harness is not vacuously green — FirstDifference must catch a real,
        // single-field divergence, not swallow it (double-red: this fails if FirstDifference stops
        // comparing fields, independently of Build_EveryRow above).
        [Test]
        public void FirstDifference_SeesAChangedField()
        {
            var a = DumpAll(new List<PlaytestStep> { new PlaytestStep { Type = StepType.Wait, Delay = 2.5f } }, null, null, new Dictionary<string, string>(), false, 0f);
            var b = DumpAll(new List<PlaytestStep> { new PlaytestStep { Type = StepType.Wait, Delay = 3f } }, null, null, new Dictionary<string, string>(), false, 0f);
            Assert.That(FirstDifference(a, b), Does.StartWith("Delay:"));
        }
    }
}
