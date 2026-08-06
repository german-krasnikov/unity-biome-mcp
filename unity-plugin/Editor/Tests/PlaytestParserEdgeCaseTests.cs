using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestParserEdgeCaseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── CS4.arch.5: MOVE TO missing position ────────────────────────────

        [Test]
        public void Parse_MoveWithoutToKeyword_ThrowsWithMoveSyntaxMessage()
        {
            // "MOVE /Player" has no TO keyword: toIdx == -1
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE /Player"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_ToAtEnd_ThrowsArgumentException()
        {
            // "MOVE TO" — TO is last token, no position follows
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE TO"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_WithPathAndNoPosition_ThrowsArgumentException()
        {
            // "MOVE /Player TO" — position token missing
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("MOVE /Player TO"));
            StringAssert.Contains("MOVE syntax", ex.Message);
        }

        [Test]
        public void Parse_Move_ValidLine_Succeeds()
        {
            var steps = PlaytestParser.Parse("MOVE TO 1,2,3");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].Type);
            Assert.That(steps[0].Position.x, Is.EqualTo(1f).Within(0.001f));
        }

        // ── CS4.test.5: Compare error paths ─────────────────────────────────

        [Test]
        public void Compare_RelationalOp_NonNumericOperands_ThrowsRequiresNumericValues()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("idle", "??", "running"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        [Test]
        public void Compare_GreaterThan_StringValues_Throws()
        {
            // ">" requires numeric values — strings don't parse as float
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("idle", ">", "running"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        [Test]
        public void Compare_LessThan_StringValues_Throws()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Compare("abc", "<", "def"));
            StringAssert.Contains("requires numeric values", ex.Message);
        }

        // ── CS4.test.7: ASSERT_BATCH missing END ────────────────────────────

        [Test]
        public void Parse_AssertBatchBlockWithoutEnd_ThrowsWithEndMessage()
        {
            var ex = Assert.Throws<System.ArgumentException>(
                () => PlaytestParser.Parse("ASSERT_BATCH\nASSERT /X|C|f == 1"));
            StringAssert.Contains("END", ex.Message);
        }

        [Test]
        public void Parse_AssertBatch_WithEnd_Succeeds()
        {
            var steps = PlaytestParser.Parse("ASSERT_BATCH\nASSERT /X|C|f == 1\nEND");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.AssertBatch, steps[0].Type);
            Assert.AreEqual(1, steps[0].Queries.Length);
        }

        // ── ABORT token in WAIT_UNTIL ─────────────────────────────────────────

        [Test]
        public void Parse_WaitUntil_AbortStandaloneToken_SetsAbortOnFail()
        {
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|C|f == ok ABORT");
            Assert.IsTrue(steps[0].AbortOnFail, "Standalone ABORT should set AbortOnFail");
        }

        [Test]
        public void Parse_WaitUntil_AbortAsAndConditionValue_DoesNotSetAbortOnFail()
        {
            // "ABORT" is the VALUE of an AND condition, not a standalone token
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|C|state == ok AND /Q|C|mode == ABORT");
            Assert.IsFalse(steps[0].AbortOnFail, "ABORT as AND value must not set AbortOnFail");
        }

        [Test]
        public void Parse_WaitUntil_AbortAfterAndBlock_SetsAbortOnFail()
        {
            // ABORT appears AFTER AND block as standalone
            var steps = PlaytestParser.Parse("WAIT_UNTIL /P|C|state == ok AND /Q|C|mode == run ABORT");
            Assert.IsTrue(steps[0].AbortOnFail, "Standalone ABORT after AND block should set AbortOnFail");
        }

        // ── Phase 3: SplitTokens + ParseQOV ─────────────────────────────────

        [Test]
        public void Parse_Assert_BracketPathWithSpaces_ParsesPathCorrectly()
        {
            var result = PlaytestParser.Parse(
                "ASSERT /[MECHANICS/ZONE TEMPLATE]/Child|Comp|field == value");
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("[MECHANICS/ZONE TEMPLATE]", result[0].Query);
            Assert.AreEqual("value", result[0].Value);
        }

        [Test]
        public void Parse_Assert_SpaceInName_OperatorBased()
        {
            var result = PlaytestParser.Parse(
                "ASSERT /My Object/Child|Health|hp == 100");
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("My Object", result[0].Query);
            Assert.AreEqual("100", result[0].Value);
        }

        [Test]
        public void Parse_WaitUntil_SpaceInName_ParsesQueryAndValue()
        {
            var result = PlaytestParser.Parse(
                "WAIT_UNTIL /My Object|AI|active == True TIMEOUT 5");
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("My Object", result[0].Query);
            Assert.AreEqual("True", result[0].Value);
            Assert.AreEqual(5f, result[0].Timeout, 0.001f);
        }

        [Test]
        public void Parse_Assert_BoolShorthand_NoOperator_ImpliesEqualTrue()
        {
            var result = PlaytestParser.Parse("ASSERT /Player|Health|isAlive");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/Player|Health|isAlive", result[0].Query);
            Assert.AreEqual("==", result[0].Op);
            Assert.AreEqual("True", result[0].Value);
        }

        [Test]
        public void Parse_Assert_SpaceInPath_ExplicitTrue_ParsesQueryCorrectly()
        {
            var result = PlaytestParser.Parse("ASSERT /My Player|Health|isAlive == True");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("/My Player|Health|isAlive", result[0].Query);
            Assert.AreEqual("==", result[0].Op);
            Assert.AreEqual("True", result[0].Value);
        }

        [Test]
        public void Parse_AssertBatch_SpaceInPath_ParsesCorrectly()
        {
            var result = PlaytestParser.Parse(
                "ASSERT_BATCH\nASSERT /My Object|C|f == 1\nEND");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(StepType.AssertBatch, result[0].Type);
            StringAssert.Contains("My Object", result[0].Queries[0]);
        }

        [Test]
        public void Parse_WaitUntil_CompoundAND_SpaceInPath()
        {
            var result = PlaytestParser.Parse(
                "WAIT_UNTIL /My Object|C|f == 1 AND /Other Space|C|g == 2 TIMEOUT 5");
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("My Object", result[0].Query);
            Assert.AreEqual(1, result[0].Queries.Length);
            StringAssert.Contains("Other Space", result[0].Queries[0]);
            Assert.AreEqual(5f, result[0].Timeout, 0.001f);
        }

        [TestCase("TIMEOUT")]
        [TestCase("ABORT")]
        [TestCase("AND")]
        [TestCase("OR")]
        public void Parse_Assert_ValueIsStopKeyword_PreservesValueToken(string keyword)
        {
            var result = PlaytestParser.Parse($"ASSERT /x|C|state == {keyword}");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(keyword, result[0].Value);
        }

        [Test]
        public void Parse_Assert_MultiWordValue_ConcatenatesTokens()
        {
            var result = PlaytestParser.Parse("ASSERT /x|C|state == hello world");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("hello world", result[0].Value);
        }

        // ── G16: SETUP_END / TEARDOWN_END recognized by parser ────────────────────

        [Test]
        public void Parser_SetupEnd_NoException()
        {
            Assert.DoesNotThrow(() => PlaytestParser.Parse("SETUP\nLOG s\nSETUP_END\nLOG m"));
        }

        [Test]
        public void Parser_TeardownEnd_NoException()
        {
            Assert.DoesNotThrow(() => PlaytestParser.Parse("TEARDOWN\nLOG t\nTEARDOWN_END\nLOG m"));
        }

        [Test]
        public void Parser_SetupEnd_ResetsToMainSection()
        {
            var result = PlaytestParser.Parse("SETUP\nLOG s\nSETUP_END\nLOG main");
            Assert.AreEqual(1, result.SetupSteps?.Count ?? 0, "One step in SETUP section");
            Assert.AreEqual(1, result.Steps.Count, "One step in main section after SETUP_END");
        }

        [Test]
        public void Parser_TeardownEnd_ResetsToMainSection()
        {
            var result = PlaytestParser.Parse("TEARDOWN\nLOG t\nTEARDOWN_END\nLOG main");
            Assert.AreEqual(1, result.TeardownSteps?.Count ?? 0, "One step in TEARDOWN section");
            Assert.AreEqual(1, result.Steps.Count, "One step in main section after TEARDOWN_END");
        }
    }
}
