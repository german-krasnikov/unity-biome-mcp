// TDD: PlaytestLinter — lint .playtest scripts for common issues.
// Tests run against LintScript (inline script, no file I/O) to stay fast and self-contained.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestLintTests
    {
        const string CleanScript =
            "ASSERT /Player|Transform|localPosition.x > 0\n" +
            "ASSERT_CONSOLE_CLEAN";

        [Test]
        public void CleanScript_ReturnsOk()
        {
            var result = PlaytestLinter.LintScript(CleanScript, "test");
            Assert.That(result, Does.StartWith("OK"));
        }

        [Test]
        public void DeprecatedAlias_Detected()
        {
            var script = "ALIAS $foo bar\n" + CleanScript;
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("ERROR"));
            Assert.That(result, Does.Contain("deprecated"));
        }

        [Test]
        public void TraceFlow_Detected()
        {
            var script =
                "TRACE_FLOW FROM /Player TO /Enemy\n" +
                "ASSERT /Player|Transform|localPosition.x > 0\n" +
                "ASSERT_CONSOLE_CLEAN";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("WARN"));
            Assert.That(result, Does.Contain("TRACE_FLOW"));
        }

        [Test]
        public void NoEvidenceCommands_Errors()
        {
            var script = "WAIT 1\nWAIT 2";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("ERROR"));
            Assert.That(result, Does.Contain("evidence"));
        }

        [Test]
        public void NoAssertConsoleClean_IsError()
        {
            var script = "ASSERT /Player|Transform|localPosition.x > 0\nWAIT 1";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("ERROR"));
            Assert.That(result, Does.Contain("ASSERT_CONSOLE_CLEAN"));
        }

        [Test]
        public void FinishCleanCall_SatisfiesCleanup()
        {
            // MACRO defined inline so test is self-contained; finish_clean expands to
            // TIMESCALE 1 + ASSERT_CONSOLE_CLEAN → last meaningful step = AssertConsoleClean.
            var script =
                "MACRO finish_clean\n" +
                "TIMESCALE 1\n" +
                "ASSERT_CONSOLE_CLEAN\n" +
                "END_MACRO\n" +
                "ASSERT /Player|Transform|localPosition.x > 0\n" +
                "CALL finish_clean";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Not.Contain("no ASSERT_CONSOLE_CLEAN"));
        }

        [Test]
        public void UnknownMacro_ReturnsError()
        {
            var script = "CALL nonexistent_macro_xyz";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("ERROR"));
        }

        [Test]
        public void MixedAndOr_Warns()
        {
            var script =
                "WAIT_UNTIL /A|Transform|localPosition.x > 0 AND /B|Transform|localPosition.x > 0 OR /C|Transform|localPosition.x > 0\n" +
                "ASSERT_CONSOLE_CLEAN";
            var result = PlaytestLinter.LintScript(script, "test");
            Assert.That(result, Does.Contain("WARN"));
            Assert.That(result, Does.Contain("AND"));
        }
    }
}
