// TDD: FOR $i IN 0..N expansion — Phase 0.5, pure-logic, EditMode safe.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestForLoopTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── 1. Basic expansion: 3 iterations → 3 steps ───────────────────────────

        [Test]
        public void For_BasicExpansion_ThreeSteps()
        {
            var steps = PlaytestParser.Parse("FOR $i IN 0..3\nWAIT $i\nEND_FOR");
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(0f, steps[0].Delay, 0.001f);
            Assert.AreEqual(1f, steps[1].Delay, 0.001f);
            Assert.AreEqual(2f, steps[2].Delay, 0.001f);
        }

        // ── 2. INVOKE path substitution ──────────────────────────────────────────

        [Test]
        public void For_InvokeExpansion_PathIndexed()
        {
            var steps = PlaytestParser.Parse("FOR $i IN 0..2\nINVOKE /Block_$i Comp Method\nEND_FOR");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual("/Block_0", steps[0].Path);
            Assert.AreEqual("/Block_1", steps[1].Path);
        }

        // ── 3. Empty range (start == end) → zero steps ───────────────────────────

        [Test]
        public void For_EmptyRange_ZeroSteps()
        {
            var steps = PlaytestParser.Parse("FOR $i IN 5..5\nWAIT 1\nEND_FOR");
            Assert.AreEqual(0, steps.Count);
        }

        // ── 4. Missing END_FOR throws ─────────────────────────────────────────────

        [Test]
        public void For_MissingEndFor_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("FOR $i IN 0..3\nWAIT 1"));
            StringAssert.Contains("END_FOR", ex.Message);
        }

        // ── 5. Nested FOR → 4 steps ──────────────────────────────────────────────

        [Test]
        public void For_NestedFor_FourSteps()
        {
            var script = "FOR $i IN 0..2\nFOR $j IN 0..2\nLOG msg\nEND_FOR\nEND_FOR";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(4, steps.Count);
        }

        // ── 6. FOR inside MACRO body → expands on CALL ───────────────────────────

        [Test]
        public void For_InsideMacro_ExpandsOnCall()
        {
            var script = "MACRO spawn $N\nFOR $i IN 0..$N\nINVOKE /Obj_$i C M\nEND_FOR\nEND_MACRO\nCALL spawn 3";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual("/Obj_0", steps[0].Path);
            Assert.AreEqual("/Obj_1", steps[1].Path);
            Assert.AreEqual("/Obj_2", steps[2].Path);
        }

        // ── 7. CALL inside FOR body → expanded correctly ──────────────────────────

        [Test]
        public void For_CallInsideFor_MacroExpands()
        {
            var script = "MACRO hit $P\nINVOKE $P Health ApplyDamage 10\nEND_MACRO\nFOR $i IN 0..2\nCALL hit /Enemy_$i\nEND_FOR";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual("/Enemy_0", steps[0].Path);
            Assert.AreEqual("/Enemy_1", steps[1].Path);
        }

        // ── 8. MacroStack provenance is set for each iteration ───────────────────

        [Test]
        public void For_Provenance_MacroStackSet()
        {
            var steps = PlaytestParser.Parse("FOR $i IN 0..2\nWAIT 1\nEND_FOR");
            Assert.IsNotNull(steps[0].MacroStack);
            Assert.AreEqual("FOR:0", steps[0].MacroStack[steps[0].MacroStack.Length - 1]);
            Assert.AreEqual("FOR:1", steps[1].MacroStack[steps[1].MacroStack.Length - 1]);
        }

        // ── 9. Range > 10000 throws ───────────────────────────────────────────────

        [Test]
        public void For_RangeTooLarge_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("FOR $i IN 0..100001\nWAIT 1\nEND_FOR"));
            StringAssert.Contains("max 10000", ex.Message);
        }

        // ── 10. Negative range (end < start) → zero steps ────────────────────────

        [Test]
        public void For_NegativeRange_ZeroSteps()
        {
            var steps = PlaytestParser.Parse("FOR $i IN 5..0\nWAIT 1\nEND_FOR");
            Assert.AreEqual(0, steps.Count);
        }

        // ── 11. FOR / END_FOR in _DSL_KEYWORDS ───────────────────────────────────

        [Test]
        public void For_KeywordsInDSLKeywords()
        {
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("FOR"));
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("END_FOR"));
        }
    }
}
