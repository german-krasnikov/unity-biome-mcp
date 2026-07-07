using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class NlStepParserTests
    {
        // ── Fragment splitting ──────────────────────────────────────────────

        [Test] public void SplitFragments_Then_Splits() =>
            Assert.AreEqual(2, NlStepParser.SplitFragments("move Player to 0,0,0 then wait 2").Length);

        [Test] public void SplitFragments_CommaVerb_Splits() =>
            Assert.AreEqual(2, NlStepParser.SplitFragments("wait 2, assert console clean").Length);

        [Test] public void SplitFragments_AndVerb_Splits() =>
            Assert.AreEqual(2, NlStepParser.SplitFragments("invoke Player.Shoot and check console clean").Length);

        [Test] public void SplitFragments_AndNonVerb_NoSplit() =>
            Assert.AreEqual(1, NlStepParser.SplitFragments("Player and Enemy").Length);

        // ── Core conversions ────────────────────────────────────────────────

        [Test] public void ConvertToDsl_Wait_Seconds() =>
            Assert.AreEqual("WAIT 2", NlStepParser.ConvertToDsl("wait 2 seconds"));

        [Test] public void ConvertToDsl_Wait_Short() =>
            Assert.AreEqual("WAIT 1.5", NlStepParser.ConvertToDsl("wait 1.5s"));

        [Test] public void ConvertToDsl_TimeScale() =>
            Assert.AreEqual("TIMESCALE 0.5", NlStepParser.ConvertToDsl("set timescale 0.5"));

        [Test] public void ConvertToDsl_Move_WithPosition() =>
            Assert.AreEqual("MOVE /Player TO 5,0,3", NlStepParser.ConvertToDsl("move Player to 5,0,3"));

        [Test] public void ConvertToDsl_Move_ToOrigin() =>
            Assert.AreEqual("MOVE TO 0,0,0", NlStepParser.ConvertToDsl("move to origin"));

        [Test] public void ConvertToDsl_Teleport() =>
            Assert.AreEqual("TELEPORT /Camera 1,2,3", NlStepParser.ConvertToDsl("teleport Camera to 1,2,3"));

        [Test] public void ConvertToDsl_Assert_Operator() =>
            Assert.AreEqual("ASSERT /Enemy|Health|hp > 50", NlStepParser.ConvertToDsl("assert /Enemy|Health|hp > 50"));

        [Test] public void ConvertToDsl_AssertConsoleClean() =>
            Assert.AreEqual("ASSERT_CONSOLE_CLEAN", NlStepParser.ConvertToDsl("assert console clean"));

        [Test] public void ConvertToDsl_WaitUntil() =>
            Assert.AreEqual("WAIT_UNTIL /E|H|hp < 10 TIMEOUT 5", NlStepParser.ConvertToDsl("wait until /E|H|hp < 10"));

        [Test] public void ConvertToDsl_Invoke_DotNotation() =>
            Assert.AreEqual("INVOKE /Player Player Attack", NlStepParser.ConvertToDsl("invoke Player.Attack"));

        [Test] public void ConvertToDsl_Invoke_ThreeParts() =>
            Assert.AreEqual("INVOKE /Player Combat Attack", NlStepParser.ConvertToDsl("invoke Player.Combat.Attack"));

        [Test] public void ConvertToDsl_Log() =>
            Assert.AreEqual("LOG test started", NlStepParser.ConvertToDsl("log test started"));

        [Test] public void ConvertToDsl_Section() =>
            Assert.AreEqual("SECTION \"Combat Test\"", NlStepParser.ConvertToDsl("section Combat Test"));

        [Test] public void ConvertToDsl_Unknown_Fallback() =>
            StringAssert.Contains("UNPARSED", NlStepParser.ConvertToDsl("flobber wibble"));

        // ── Helpers ─────────────────────────────────────────────────────────

        [Test] public void NormalizePath_WithSlash() =>
            Assert.AreEqual("/Player", NlStepParser.NormalizePath("/Player"));

        [Test] public void NormalizePath_WithoutSlash() =>
            Assert.AreEqual("/Player", NlStepParser.NormalizePath("Player"));

        [Test] public void TryParsePosition_Origin()
        {
            Assert.IsTrue(NlStepParser.TryParsePosition("origin", out var v));
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test] public void TryParsePosition_Comma()
        {
            Assert.IsTrue(NlStepParser.TryParsePosition("5,0,3", out var v));
            Assert.AreEqual(new Vector3(5, 0, 3), v);
        }

        [Test] public void TryParsePosition_Parens()
        {
            Assert.IsTrue(NlStepParser.TryParsePosition("(5, 0, 3)", out var v));
            Assert.AreEqual(new Vector3(5, 0, 3), v);
        }

        [Test] public void TryParsePosition_Bad() =>
            Assert.IsFalse(NlStepParser.TryParsePosition("bad", out _));

        // ── Round-trip ──────────────────────────────────────────────────────

        [Test]
        [TestCase("move Player to 5,0,3")]
        [TestCase("wait 2 seconds")]
        [TestCase("assert console clean")]
        [TestCase("assert /E|H|hp > 50")]
        [TestCase("teleport Camera to 0,1,0")]
        [TestCase("section Combat Test")]
        [TestCase("log hello")]
        [TestCase("set timescale 0.5")]
        [TestCase("flobber wibble")]
        public void RoundTrip_ParseDoesNotThrow(string input)
        {
            var dsl = NlStepParser.ConvertToDsl(input);
            Assert.DoesNotThrow(() => PlaytestParser.Parse(dsl), $"DSL: {dsl}");
        }

        // ── Edge cases ──────────────────────────────────────────────────────

        [Test] public void ConvertToDsl_Empty_ReturnsEmpty() =>
            Assert.AreEqual("", NlStepParser.ConvertToDsl(""));

        [Test]
        public void ConvertToDsl_MultiStep()
        {
            var lines = NlStepParser.ConvertToDsl("move Player to 5,0,3 then wait 2").Split('\n');
            Assert.AreEqual(2, lines.Length);
        }

        // ── Edge cases (NlCommandWindow integration) ─────────────────────

        [Test]
        public void ConvertToDsl_WhitespaceOnly_ReturnsEmpty() =>
            Assert.AreEqual("", NlStepParser.ConvertToDsl("   \t\n  "));

        [Test]
        public void ConvertToDsl_PathWithSpaces_DoesNotThrow() =>
            Assert.DoesNotThrow(() => NlStepParser.ConvertToDsl("move [/My Player] to 0,0,0"));

        [Test]
        public void ConvertToDsl_PathWithUnicode_DoesNotThrow() =>
            Assert.DoesNotThrow(() => NlStepParser.ConvertToDsl("assert [/Персонаж]|Health|hp > 10"));

        [Test]
        public void ConvertToDsl_PathWithPipe_RoundTrips()
        {
            var dsl = NlStepParser.ConvertToDsl("assert /UI|Button|isActive == true");
            Assert.DoesNotThrow(() => PlaytestParser.Parse(dsl), $"DSL: {dsl}");
            StringAssert.Contains("ASSERT", dsl);
        }

        [Test]
        public void ConvertToDsl_VeryLongInput_NoThrow()
        {
            var words = new System.Text.StringBuilder();
            for (int i = 0; i < 40; i++) words.Append("move player to origin then ");
            Assert.DoesNotThrow(() => NlStepParser.ConvertToDsl(words.ToString()));
        }

        [Test]
        public void ConvertToDsl_MultilineInput_AllFragmentsProcessed()
        {
            var result = NlStepParser.ConvertToDsl("wait 2\nwait 3");
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void ConvertToDsl_BracketPathRef_EmitsUnparsed() =>
            StringAssert.Contains("UNPARSED", NlStepParser.ConvertToDsl("[/Player]"));

        [Test]
        public void ConvertToDsl_Screenshot_DoesNotThrow()
        {
            // "screenshot" may emit SCREENSHOT or UNPARSED — heuristic coverage check
            string result = null;
            Assert.DoesNotThrow(() => result = NlStepParser.ConvertToDsl("screenshot"));
            Assert.IsNotNull(result);
        }
    }
}
