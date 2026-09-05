// TDD: B09 — INV-022: Play-bound verbs (Move/Simulate/CaptureFrames/TimeScale) require Play
// Mode to do anything, so they are a compile error under `# @needs editmode` rather than a
// silent no-op or a runtime failure. MOVE_PATH/SWEEP_PATH desugar to StepType.Move at parse
// time, so checking the expanded step is enough to cover both surface keywords.
// This file also hosts B11's golden parser corpus and B12's negative parser corpus.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserCorpusTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string EditModeHeader = "# @needs editmode\n";

        // B11 — golden parser corpus: the 11 MCPFeedbackFixture .playtest files all
        // INCLUDE only common.defs, so a resolver is enough (no live AssetDatabase).
        private const string FixtureDir = "Assets/MCPFeedbackFixture/PlayTests";
        private const string PlaytestDefsDir = "Assets/PlaytestDefs";

        private static string ResolvePlaytestDefs(string filename) =>
            File.ReadAllText(Path.Combine(PlaytestDefsDir, filename));

        // Step counts below are hand-counted from each fixture file: every non-blank,
        // non-comment, non-INCLUDE/VAL/VAR line becomes exactly one PlaytestStep
        // (verified by reading PlaytestParser.cs:258-289 — VAL/VAR/PATH_PREFIX/blank/
        // comment lines all `continue` before a step is constructed). Comparing only
        // the count means RawLine/SourceFile/SourceLine/MacroStack never enter the
        // assertion, so unrelated line-number shifts in a fixture can't spuriously
        // break this test.
        [TestCase("A_shared_setup.playtest", 4)]
        [TestCase("B_shared_continue.playtest", 3)]
        [TestCase("C_shared_finish.playtest", 4)]
        [TestCase("DSL_types.playtest", 7)]
        [TestCase("F_independent_fail.playtest", 2)]
        [TestCase("I1_independent_pass.playtest", 5)]
        [TestCase("I2_independent_pass.playtest", 5)]
        [TestCase("I3_independent_pass.playtest", 4)]
        [TestCase("INVOKE_arguments.playtest", 5)]
        [TestCase("L_long_pass.playtest", 3)]
        [TestCase("MOVEMENT_profiles.playtest", 4)]
        public void Parse_MCPFeedbackFixtureCorpus_MatchesHandCountedStepCount(string fileName, int expectedStepCount)
        {
            var raw = File.ReadAllText(Path.Combine(FixtureDir, fileName));
            var result = PlaytestParser.Parse(raw, resolver: ResolvePlaytestDefs);
            Assert.IsNull(result.Errors, $"{fileName}: unexpected parse errors");
            Assert.AreEqual(expectedStepCount, result.Steps.Count, $"{fileName}: hand-counted step count mismatch");
        }

        // B12 — negative parser corpus: freeze the exact error text of the three
        // existing parse-time rejection paths so a future refactor can't silently
        // change or drop them.

        [Test]
        public void Parse_UnknownCommand_ThrowsWithCommandName()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => PlaytestParser.Parse("FOOBAR_CMD arg1"));
            StringAssert.Contains("Unknown command: FOOBAR_CMD", ex.Message);
        }

        [Test]
        public void Parse_IncludeTraversal_Rejected()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => PlaytestParser.Parse("INCLUDE ../../etc/passwd"));
            StringAssert.Contains("path traversal not allowed", ex.Message);
        }

        [Test]
        public void Parse_IncludeOutsidePlaytestDefs_Rejected()
        {
            // A single "." does not contain ".." and is not rooted, so it slips past the
            // traversal guard; Path.GetFullPath(Combine("Assets/PlaytestDefs/", ".")) then
            // resolves to the PlaytestDefs directory itself *without* the trailing
            // separator the precomputed base path has, so the StartsWith check fails and
            // this hits the "outside PlaytestDefs/" branch specifically (resolver: null
            // forces the hardcoded Assets/PlaytestDefs/ branch rather than a caller resolver).
            var ex = Assert.Throws<System.ArgumentException>(() => PlaytestParser.Parse("INCLUDE .", resolver: null));
            StringAssert.Contains("path outside PlaytestDefs/", ex.Message);
        }

        [Test]
        public void Parse_MoveUnderNeedsEditmode_ProducesCompileError()
        {
            var result = PlaytestParser.Parse(EditModeHeader + "MOVE TO 1,0,0");
            Assert.IsNotNull(result.Errors, "MOVE under @needs editmode must be a compile error");
            StringAssert.Contains("Move", result.Errors[0]);
        }

        [Test]
        public void Parse_SimulateUnderNeedsEditmode_ProducesCompileError()
        {
            var result = PlaytestParser.Parse(EditModeHeader + "SIMULATE walk");
            Assert.IsNotNull(result.Errors, "SIMULATE under @needs editmode must be a compile error");
            StringAssert.Contains("Simulate", result.Errors[0]);
        }

        [Test]
        public void Parse_CaptureFramesUnderNeedsEditmode_ProducesCompileError()
        {
            var result = PlaytestParser.Parse(EditModeHeader + "CAPTURE_FRAMES 3 INTERVAL 0.1");
            Assert.IsNotNull(result.Errors, "CAPTURE_FRAMES under @needs editmode must be a compile error");
            StringAssert.Contains("CaptureFrames", result.Errors[0]);
        }

        [Test]
        public void Parse_TimeScaleUnderNeedsEditmode_ProducesCompileError()
        {
            var result = PlaytestParser.Parse(EditModeHeader + "TIMESCALE 0.5");
            Assert.IsNotNull(result.Errors, "TIMESCALE under @needs editmode must be a compile error");
            StringAssert.Contains("TimeScale", result.Errors[0]);
        }

        // ── Regression: the Play-default script (no header) is unaffected ───────────

        [Test]
        public void Parse_MoveWithoutNeedsEditmode_NoError()
        {
            var result = PlaytestParser.Parse("MOVE TO 1,0,0");
            Assert.IsNull(result.Errors, "Play-default script must be unaffected by the editmode-only rejection");
        }

        // B20 — census of which MCPFeedbackFixture .playtest files are actually Edit-capable:
        // FixtureAsyncState.StartOperation / FixtureState.CompleteAfterSeconds use
        // StartCoroutine (Play-only); FixtureMoveAdapter/FixtureMover are synchronous, so
        // scripts that only touch FixtureState/FixtureMoveAdapter run fine under EditMode.
        // C_shared_finish, DSL_types and I3_independent_pass touch the async/coroutine
        // path and stay Play-bound (no header).
        [TestCase("A_shared_setup.playtest", true)]
        [TestCase("B_shared_continue.playtest", true)]
        [TestCase("F_independent_fail.playtest", true)]
        [TestCase("I1_independent_pass.playtest", true)]
        [TestCase("I2_independent_pass.playtest", true)]
        [TestCase("INVOKE_arguments.playtest", true)]
        [TestCase("L_long_pass.playtest", true)]
        [TestCase("MOVEMENT_profiles.playtest", true)]
        [TestCase("C_shared_finish.playtest", false)]
        [TestCase("DSL_types.playtest", false)]
        [TestCase("I3_independent_pass.playtest", false)]
        public void Parse_EditCapableFiles_HeaderHonored(string fileName, bool expectedNeedsEditmode)
        {
            var raw = File.ReadAllText(Path.Combine(FixtureDir, fileName));
            var result = PlaytestParser.Parse(raw, resolver: ResolvePlaytestDefs);
            Assert.AreEqual(expectedNeedsEditmode, result.Header.NeedsEditmode,
                $"{fileName}: @needs editmode header mismatch");
        }

        // ── C01 — `MCP <cmd> key=value... [INTO $result]` parsing + JSON assembler ──

        [Test]
        public void Parse_McpSimpleCommand_ProducesMcpStep()
        {
            var result = PlaytestParser.Parse("MCP get_hierarchy depth=2");
            Assert.IsNull(result.Errors);
            Assert.AreEqual(1, result.Steps.Count);
            var step = result.Steps[0];
            Assert.AreEqual(StepType.Mcp, step.Type);
            Assert.AreEqual("get_hierarchy", step.Method);
            Assert.AreEqual("{\"depth\":2}", step.Args);
            Assert.IsNull(step.ResultVar);
        }

        [Test]
        public void Parse_McpKeyValueTypes_InfersBoolIntStringCorrectly()
        {
            var result = PlaytestParser.Parse("MCP fake_cmd flag=TRUE count=3 ratio=2.5 label=hello");
            Assert.IsNull(result.Errors);
            var step = result.Steps[0];
            Assert.AreEqual("{\"flag\":true,\"count\":3,\"ratio\":2.5,\"label\":\"hello\"}", step.Args);
        }

        [Test]
        public void Parse_McpNullArrayAndObject_PreserveJsonTypes()
        {
            var result = PlaytestParser.Parse("MCP fake_cmd n=null arr=[1,2,\"x\"] obj={\"x\":1}");
            Assert.IsNull(result.Errors);
            var step = result.Steps[0];
            Assert.AreEqual("{\"n\":null,\"arr\":[1,2,\"x\"],\"obj\":{\"x\":1}}", step.Args);
        }

        [Test]
        public void Parse_McpQuotedValueWithSpaces_PreservesAsString()
        {
            var result = PlaytestParser.Parse("MCP fake_cmd label=\"hello world\"");
            Assert.IsNull(result.Errors);
            var step = result.Steps[0];
            Assert.AreEqual("{\"label\":\"hello world\"}", step.Args);
        }

        [Test]
        public void Parse_McpValRunIdSubstitution_ExpandsBeforeTokenizing()
        {
            var result = PlaytestParser.Parse("VAL $RUN_ID abc12345\nMCP create_object name=pt_$RUN_ID");
            Assert.IsNull(result.Errors);
            var step = result.Steps[0];
            Assert.AreEqual("{\"name\":\"pt_abc12345\"}", step.Args);
        }

        [Test]
        public void Parse_McpInto_CapturesFinalSigilWithoutPuttingItInArgs()
        {
            var result = PlaytestParser.Parse("MCP get_hierarchy depth=2 INTO $tree");
            Assert.IsNull(result.Errors);
            var step = result.Steps[0];
            Assert.AreEqual("tree", step.ResultVar);
            Assert.AreEqual("{\"depth\":2}", step.Args);
        }

        [Test]
        public void Parse_McpMalformedObjectValue_ThrowsCompileError()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MCP fake_cmd obj={\"x\":}"));
            StringAssert.Contains("malformed JSON value", ex.Message);
        }

        [TestCase("MCP fake_cmd key=1 INTO")]                  // missing destination
        [TestCase("MCP fake_cmd key=1 INTO result")]            // non-sigil destination
        [TestCase("MCP fake_cmd INTO $result key=1")]           // not the final token
        public void Parse_McpMalformedInto_Throws(string script)
        {
            Assert.Throws<System.ArgumentException>(() => PlaytestParser.Parse(script));
        }

        [Test]
        public void Parse_UnknownTopLevelKeyword_StillThrows()
        {
            var ex = Assert.Throws<System.ArgumentException>(() => PlaytestParser.Parse("TOTALLY_UNKNOWN_CMD 1"));
            StringAssert.Contains("Unknown command: TOTALLY_UNKNOWN_CMD", ex.Message);
        }

        [Test]
        public void Parse_McpStep_RawLineRoundTripsForComposerPreservation()
        {
            const string line = "MCP get_hierarchy depth=2 INTO $tree";
            var result = PlaytestParser.Parse(line);
            Assert.AreEqual(line, result.Steps[0].RawLine);
        }
    }
}
