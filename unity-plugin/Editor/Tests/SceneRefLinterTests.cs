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
            TrackOwnedObject(new GameObject("LintPlayer"));
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
            TrackOwnedObject(new GameObject("LintCube"));
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

        // ── DEF-5: VAL-defined alias should not produce false-positive ────────────

        [Test]
        public void LintScript_ValDefinedAlias_NoFalsePositive()
        {
            // A script with inline VAL $player /SomeObj should not report
            // $player as "unresolved alias" even if /SomeObj doesn't exist in scene.
            // The linter should recognize $player is defined and skip it.
            var script = "VAL $player /SomeObj\nASSERT $player|Transform|position == 0,0,0";
            var issues = SceneRefLinter.LintScript(script);

            // $player must NOT be flagged as unresolved alias
            foreach (var issue in issues)
                Assert.AreNotEqual("$player", issue.Token,
                    "VAL-defined alias $player must not be reported as unresolved");
        }

        [Test]
        public void LintScript_UndefinedAlias_StillReportsError()
        {
            // Ensure the fix doesn't suppress ALL alias errors — only defined ones.
            var script = "VAL $player /SomeObj\nASSERT $unknownThing|Transform|position == 0";
            var issues = SceneRefLinter.LintScript(script);
            bool foundUnknown = false;
            foreach (var issue in issues)
                if (issue.Token.StartsWith("$unknownThing")) foundUnknown = true;
            Assert.IsTrue(foundUnknown, "Undefined alias $unknownThing must still be reported");
        }

        // ── P-287: bracket-aware tokenizer ────────────────────────────────────────

        [Test]
        public void Linter_BracketProtectedPath_NotSplitOnSpace()
        {
            // Naive Split(' ','\t') breaks "[Zone A/Zone B]|Health" into
            // tokens "[Zone", "A/Zone", "B]|Health" — false MISS on "B]|Health".
            // After fix (SplitTokens): bracket content is one token.
            var script = "ASSERT [Zone A/Zone B]|Health == 100";
            var issues = SceneRefLinter.LintScript(script);
            foreach (var issue in issues)
                Assert.AreNotEqual("B]|Health", issue.Token,
                    "Bracket-protected path must not be split on spaces; found false token 'B]|Health'");
        }

        [Test]
        public void Linter_CommaArgAfterPath_ZeroNotTreatedAsPath()
        {
            // FillPrimaryItemByPath /path,0 — "0" after comma must not be linted as path.
            // Neither naive split nor SplitTokens change the space-split here;
            // the G14 comma handler must produce "/path" and "0" where "0" fails IsPathToken.
            var script = "INVOKE /LintPlayer FillPrimaryItemByPath /item_path,0";
            TrackOwnedObject(new GameObject("LintPlayer"));
            // Only "/item_path" (missing) may be flagged; "0" must never appear as an issue token.
            var issues = SceneRefLinter.LintScript(script);
            foreach (var issue in issues)
                Assert.AreNotEqual("0", issue.Token,
                    "Numeric comma-arg '0' must not be linted as a path token");
        }
    }
}
