using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneRefLinterTests : SceneTestBase
    {
        [Test]
        public void LintScript_EmptyScript_ReturnsEmpty()
        {
            var issues = SceneRefLinter.LintScript("");
            Assert.AreEqual(0, issues.Count);
        }

        [Test]
        public void LintScript_CleanScript_ExistingObject_ReturnsEmpty()
        {
            new GameObject("LintPlayer");
            var issues = SceneRefLinter.LintScript("ASSERT /LintPlayer|Transform|position == 0,0,0");
            Assert.AreEqual(0, issues.Count);
        }

        [Test]
        public void LintScript_UnresolvedAlias_ReturnsError()
        {
            var issues = SceneRefLinter.LintScript("ASSERT $unknownAlias_XYZ|Transform|position == 0,0,0");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("ERROR", issues[0].Severity);
            StringAssert.Contains("unresolved alias", issues[0].Message);
        }

        [Test]
        public void LintScript_EmbeddedAlias_ReturnsError()
        {
            var issues = SceneRefLinter.LintScript("ASSERT /prefix/$alias/suffix|Transform|position == 0");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("ERROR", issues[0].Severity);
            StringAssert.Contains("embedded alias", issues[0].Message);
        }

        [Test]
        public void LintScript_MissingObject_ReturnsError()
        {
            var issues = SceneRefLinter.LintScript("ASSERT /DoesNotExist_XYZ999|Transform|position == 0,0,0");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("ERROR", issues[0].Severity);
            StringAssert.Contains("not found", issues[0].Message);
        }

        [Test]
        public void LintScript_ValidPipePath_ExistingObject_NoIssues()
        {
            new GameObject("LintCube");
            var issues = SceneRefLinter.LintScript("SET /LintCube|Transform|position 1,0,0");
            Assert.AreEqual(0, issues.Count);
        }

        [Test]
        public void FormatReport_NoIssues_ReturnsOK()
        {
            var report = SceneRefLinter.FormatReport("<inline>", new List<SceneRefLinter.LintIssue>());
            StringAssert.StartsWith("OK", report);
        }

        [Test]
        public void FormatReport_WithIssues_FormatsCorrectly()
        {
            var issues = new List<SceneRefLinter.LintIssue>
            {
                new SceneRefLinter.LintIssue
                {
                    Severity = "ERROR", Line = 3,
                    Token = "$missing",
                    Message = "unresolved alias: $missing"
                }
            };
            var report = SceneRefLinter.FormatReport("test.playtest", issues);
            StringAssert.Contains("ERROR", report);
            StringAssert.Contains("test.playtest:3", report);
            StringAssert.Contains("$missing", report);
        }

        // ── G14: INCLUDE directive / comma-tokenizing ──────────────────────────────

        [Test]
        public void Linter_IncludeDirectiveLine_FilenameNotLinted()
        {
            // INCLUDE lines are in _skipLineKeys — filename must not produce a lint issue.
            var script = "INCLUDE PlaytestDefs/my_aliases.defs";
            var issues = SceneRefLinter.LintScript(script);
            foreach (var issue in issues)
                Assert.AreNotEqual("PlaytestDefs/my_aliases.defs", issue.Token,
                    "INCLUDE filename must not be linted as a scene path");
        }

        [Test]
        public void Linter_CommaSeparatedPathTokens_SplitBeforeLinting()
        {
            // A token "/A|T,/B|T" should be split into /A|T and /B|T before checking.
            // Neither exists in the test scene so we get two MISS issues, not one for the combined token.
            var script = "ASSERT_BATCH /NotHere1|Transform,/NotHere2|Transform == 0";
            var issues = SceneRefLinter.LintScript(script);
            // Verify: no issue has a token that contains a comma (i.e., split happened)
            foreach (var issue in issues)
                Assert.IsFalse(issue.Token.Contains(','),
                    $"Linter must split comma-separated tokens; found unsplit: '{issue.Token}'");
        }
    }
}
