// TDD: PATH_PREFIX directive — Phase 0.7 extension, pure-logic, EditMode safe.
using System;
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestPathPrefixTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── 1. Prefix applied to path VAL values ──────────────────────────────────

        [Test]
        public void PathPrefix_BasicPrefix_Applied()
        {
            var script = "PATH_PREFIX /[LEVEL1]\nVAL $door /Door\nASSERT $door|Collider|enabled == True";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("/[LEVEL1]/Door|Collider|enabled", steps[0].Query);
        }

        // ── 2. Non-path VAL (no leading /) is not modified ────────────────────────

        [Test]
        public void PathPrefix_NonPathVal_NotAffected()
        {
            var vals = PlaytestParser.CollectVals(new[] { "PATH_PREFIX /Level", "VAL $speed 10" });
            Assert.AreEqual("10", vals["speed"]);
        }

        // ── 3. First PATH_PREFIX wins; second is ignored ──────────────────────────

        [Test]
        public void PathPrefix_MultiplePrefix_FirstWins()
        {
            var vals = PlaytestParser.CollectVals(new[] {
                "PATH_PREFIX /A",
                "PATH_PREFIX /B",
                "VAL $door /Door"
            });
            Assert.AreEqual("/A/Door", vals["door"]);
        }

        // ── 4. Trailing slash on prefix is stripped ───────────────────────────────

        [Test]
        public void PathPrefix_TrailingSlash_Normalized()
        {
            var vals = PlaytestParser.CollectVals(new[] { "PATH_PREFIX /Level/", "VAL $x /Obj" });
            Assert.AreEqual("/Level/Obj", vals["x"]);
        }

        // ── 5. No prefix → VAL value unchanged ───────────────────────────────────

        [Test]
        public void PathPrefix_NoPrefix_NoChange()
        {
            var script = "VAL $x /Player\nASSERT $x|C|f == 0";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual("/Player|C|f", steps[0].Query);
        }

        // ── 6. Empty prefix (whitespace only) is a no-op ─────────────────────────

        [Test]
        public void PathPrefix_EmptyPrefix_NoOp()
        {
            var vals = PlaytestParser.CollectVals(new[] { "PATH_PREFIX ", "VAL $x /Player" });
            Assert.AreEqual("/Player", vals["x"]);
        }

        // ── 7. PATH_PREFIX is not emitted as a step ───────────────────────────────

        [Test]
        public void PathPrefix_SkippedAsCommand_NoStep()
        {
            var steps = PlaytestParser.Parse("PATH_PREFIX /L\nWAIT 1");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Wait, steps[0].Type);
        }

        // ── 8. PATH_PREFIX in _DSL_KEYWORDS ──────────────────────────────────────

        [Test]
        public void PathPrefix_InDSLKeywords()
        {
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("PATH_PREFIX"));
        }
    }
}
