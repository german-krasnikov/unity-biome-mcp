using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class NlCommandWindowLogicTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            NlComposerBridge.RunProcessOverride    = null;
            NlComposerBridge.ResolveBinaryOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            NlComposerBridge.RunProcessOverride    = null;
            NlComposerBridge.ResolveBinaryOverride = null;
        }

        // ── DslToSteps ─────────────────────────────────────────────────────

        [Test]
        public void DslToSteps_ValidLines_ReturnsSteps()
        {
            var steps = NlCommandWindow.DslToSteps("WAIT 2\nASSERT_CONSOLE_CLEAN");
            Assert.AreEqual(2, steps.Count);
        }

        [Test]
        public void DslToSteps_AllInvalidLines_ReturnsEmpty()
        {
            var steps = NlCommandWindow.DslToSteps("GARBAGE ONE\nGARBAGE TWO");
            Assert.AreEqual(0, steps.Count);
        }

        [Test]
        public void DslToSteps_MixedLines_OnlyValidReturned()
        {
            var steps = NlCommandWindow.DslToSteps("WAIT 2\nGARBAGE\nASSERT_CONSOLE_CLEAN");
            Assert.AreEqual(2, steps.Count);
        }

        [Test]
        public void DslToSteps_EmptyString_ReturnsEmpty()
        {
            var steps = NlCommandWindow.DslToSteps("");
            Assert.AreEqual(0, steps.Count);
        }

        [Test]
        public void DslToSteps_NullString_ReturnsEmpty()
        {
            Assert.DoesNotThrow(() => NlCommandWindow.DslToSteps(null));
            var steps = NlCommandWindow.DslToSteps(null);
            Assert.AreEqual(0, steps.Count);
        }

        [Test]
        public void DslToSteps_UnparsedLines_CountAsSteps()
        {
            var steps = NlCommandWindow.DslToSteps("LOG # UNPARSED: flobber");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].type);
        }

        [Test]
        public void DslToSteps_WhitespaceOnlyLines_Skipped()
        {
            var steps = NlCommandWindow.DslToSteps("WAIT 2\n\n\nASSERT_CONSOLE_CLEAN");
            Assert.AreEqual(2, steps.Count);
        }

        [Test]
        public void DslToSteps_CommentLines_Skipped()
        {
            var steps = NlCommandWindow.DslToSteps("# comment\nWAIT 2");
            Assert.AreEqual(1, steps.Count);
        }

        // ── GetLineStatus ──────────────────────────────────────────────────

        [Test]
        public void GetLineStatus_WaitLine_Valid() =>
            Assert.AreEqual(LineStatus.Valid, NlCommandWindow.GetLineStatus("WAIT 2"));

        [Test]
        public void GetLineStatus_GarbageLine_Invalid() =>
            Assert.AreEqual(LineStatus.Invalid, NlCommandWindow.GetLineStatus("GARBAGE FOO"));

        [Test]
        public void GetLineStatus_UnparsedLog_UnparsedIntent() =>
            Assert.AreEqual(LineStatus.UnparsedIntent, NlCommandWindow.GetLineStatus("LOG # UNPARSED: flobber"));

        [Test]
        public void GetLineStatus_EmptyLine_Valid() =>
            Assert.AreEqual(LineStatus.Valid, NlCommandWindow.GetLineStatus(""));

        [Test]
        public void GetLineStatus_AssertConsoleClean_Valid() =>
            Assert.AreEqual(LineStatus.Valid, NlCommandWindow.GetLineStatus("ASSERT_CONSOLE_CLEAN"));

        [Test]
        public void GetLineStatus_MoveWithoutTo_Invalid() =>
            Assert.AreEqual(LineStatus.Invalid, NlCommandWindow.GetLineStatus("MOVE /Player 0,0,0"));

        // ── Safety guards ──────────────────────────────────────────────────

        [Test]
        public void DslToSteps_OnlyInvalidLines_DoesNotThrow() =>
            Assert.DoesNotThrow(() => NlCommandWindow.DslToSteps("GARBAGE A\nGARBAGE B\nGARBAGE C"));

        [Test]
        public void DslToSteps_PartiallyInvalid_DoesNotThrow_AndCountsOnlyValid()
        {
            List<VisualStep> steps = null;
            Assert.DoesNotThrow(() => steps = NlCommandWindow.DslToSteps("WAIT 2\nGARBAGE\nASSERT_CONSOLE_CLEAN"));
            Assert.AreEqual(2, steps.Count);
        }

        // ── Integration scenarios ──────────────────────────────────────────

        [Test]
        public void FullRoundTrip_HeuristicPath_TwoStepsAdded()
        {
            var dsl   = NlStepParser.ConvertToDsl("wait 2 seconds then assert console clean");
            var steps = NlCommandWindow.DslToSteps(dsl);
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.Wait, steps[0].type);
            Assert.AreEqual(StepType.AssertConsoleClean, steps[1].type);
        }

        [Test]
        public async Task LlmPath_ValidDslReturned_StepsAdded()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) =>
                Task.FromResult("MOVE /Player TO 0,0,0\nWAIT 2");
            var cfg    = new SamplingConfig { Model = "haiku", Backend = "claude" };
            var result = await NlComposerBridge.ParseAsync("подвинь игрока к центру и жди 2", cfg);
            var steps  = NlCommandWindow.DslToSteps(result);
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].type);
        }

        [Test]
        public async Task LlmReturnsNull_HeuristicFallbackUsed()
        {
            NlComposerBridge.ResolveBinaryOverride = _ => "/usr/bin/claude";
            NlComposerBridge.RunProcessOverride    = (_, __, ___) => Task.FromResult<string>(null);
            var cfg      = new SamplingConfig { Model = "haiku" };
            var heuristic = NlStepParser.ConvertToDsl("wait 3");
            var llmResult = await NlComposerBridge.ParseAsync("wait 3", cfg);
            var result    = llmResult ?? heuristic;
            StringAssert.Contains("WAIT 3", result);
        }

        [Test]
        public void BackendSwitch_CodexPrefKey_UsedForResolution()
        {
            SetEditorPrefString("UnityMCP_Chat_Path_codex", "/usr/local/bin/codex");
            var cfg    = new SamplingConfig { Model = "gpt-4o", Backend = "codex" };
            var binary = NlComposerBridge.ResolveBinary(cfg);
            Assert.AreEqual("/usr/local/bin/codex", binary);
            DeleteEditorPrefString("UnityMCP_Chat_Path_codex");
        }

        [Test]
        public async Task LlmDisabled_ParseAsyncReturnsNull_HeuristicUsed()
        {
            var cfg    = new SamplingConfig { Model = "" };
            var result = await NlComposerBridge.ParseAsync("wait 2", cfg);
            Assert.IsNull(result);
            var dsl = NlStepParser.ConvertToDsl("wait 2");
            Assert.AreEqual("WAIT 2", dsl);
        }
    }
}
