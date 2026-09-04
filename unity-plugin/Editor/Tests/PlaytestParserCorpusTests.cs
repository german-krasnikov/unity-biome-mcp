// TDD: B09 — INV-022: Play-bound verbs (Move/Simulate/CaptureFrames/TimeScale) require Play
// Mode to do anything, so they are a compile error under `# @needs editmode` rather than a
// silent no-op or a runtime failure. MOVE_PATH/SWEEP_PATH desugar to StepType.Move at parse
// time, so checking the expanded step is enough to cover both surface keywords.
// This file also hosts B11's golden parser corpus (see PlaytestParser.Directives.cs).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserCorpusTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string EditModeHeader = "# @needs editmode\n";

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
    }
}
