// TDD: PlaytestLinter TIMESCALE_WARN rule — no Unity scene needed, EditMode safe.
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestLinterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string Cleanup = "\nASSERT_CONSOLE_CLEAN";

        // ── TIMESCALE_WARN ───────────────────────────────────────────────────

        [Test]
        public void Linter_WaitsAfterHighTimescale_ProducesWarning()
        {
            var script = "TIMESCALE 10\nWAIT_UNTIL /Obj|Transform|position.x > 0" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.Contains("TIMESCALE_WARN", result);
        }

        [Test]
        public void Linter_TimescaleOkAnnotation_SuppressesWarning()
        {
            var script = "TIMESCALE 10\n# @timescale-ok\nWAIT_UNTIL /Obj|Transform|position.x > 0" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.DoesNotContain("TIMESCALE_WARN", result);
        }

        [Test]
        public void Linter_TimescaleOne_NoWarning()
        {
            var script = "TIMESCALE 1\nWAIT_UNTIL /Obj|Transform|position.x > 0" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.DoesNotContain("TIMESCALE_WARN", result);
        }

        // ── C11: MCP without a following evidence step ─────────────────────────

        [Test]
        public void LintScript_McpStepWithNoFollowingEvidence_ReportsWarn()
        {
            var script = "MCP get_hierarchy depth=2\nLOG hello" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.Contains("WARN", result);
            StringAssert.Contains("MCP", result);
            StringAssert.Contains("evidence", result);
        }

        [Test]
        public void LintScript_McpStepFollowedByAssert_NoWarn()
        {
            var script = "MCP get_hierarchy depth=2 INTO $tree\nASSERT $tree contains foo" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.DoesNotContain("has no following evidence step", result);
        }

        [Test]
        public void LintScript_McpInTeardown_NoWarn()
        {
            var script = "ASSERT_CONSOLE_CLEAN\nTEARDOWN\nMCP delete_object path=/Obj\nTEARDOWN_END";
            var result = PlaytestLinter.LintScript(script);
            StringAssert.DoesNotContain("has no following evidence step", result);
        }

        // ── C12: unknown-verb regression guard ──────────────────────────────────

        [Test]
        public void LintScript_UnknownDslKeywordAfterMcpAdded_StillErrorsAsParseError()
        {
            // Proves adding the "MCP" case to PlaytestParser's switch(cmd) did not
            // loosen the `default: throw new ArgumentException(...)` catch-all into
            // something that silently accepts any other unrecognized top-level verb.
            var script = "BOGUS_COMMAND_XYZ foo" + Cleanup;
            var result = PlaytestLinter.LintScript(script);
            StringAssert.Contains("ERROR", result);
            StringAssert.Contains("parse error", result);
        }
    }
}
