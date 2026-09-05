// TDD: MACRO/CALL composability — pure-logic tests, EditMode safe.
using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestMacroTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── 1. Basic MACRO + CALL expands to correct steps ───────────────────────

        [Test]
        public void Parse_MacroDefineAndCall_ExpandsCorrectly()
        {
            var script = @"MACRO greet
  LOG Hello
END_MACRO
CALL greet";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
            Assert.AreEqual("Hello", steps[0].Message);
        }

        // ── 2. Positional arg $1 is substituted ──────────────────────────────────

        [Test]
        public void Parse_MacroWithPositionalArgs_SubstitutesAll()
        {
            var script = @"MACRO say $1
  LOG $1
END_MACRO
CALL say world";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
            Assert.AreEqual("world", steps[0].Message);
        }

        // ── 3. Multiple args $1 and $2 both substituted ──────────────────────────

        [Test]
        public void Parse_MacroMultipleArgs_AllSubstituted()
        {
            var script = @"MACRO move_to $1 $2 $3
  MOVE TO $1,$2,$3
END_MACRO
CALL move_to 4 5 6";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Move, steps[0].Type);
            Assert.AreEqual(4f, steps[0].Position.x);
            Assert.AreEqual(5f, steps[0].Position.y);
            Assert.AreEqual(6f, steps[0].Position.z);
        }

        // ── 4. Circular macro call throws depth limit ─────────────────────────────

        [Test]
        public void Parse_MacroCircularCall_ThrowsDepthLimit()
        {
            var script = @"MACRO loop
  CALL loop
END_MACRO
CALL loop";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // ── 5. CALL before MACRO definition (forward reference) ───────────────────

        [Test]
        public void Parse_MacroForwardReference_Works()
        {
            var script = @"CALL greet
MACRO greet
  LOG Hello
END_MACRO";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
        }

        // ── 6. VAL + MACRO compose: val substituted inside expanded body ──────────

        [Test]
        public void Parse_MacroCallWithAlias_BothResolve()
        {
            var script = @"VAL $HP /Player|Health|Value
MACRO check $1
  ASSERT $1 > 0
END_MACRO
CALL check $HP";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.Assert, steps[0].Type);
            Assert.AreEqual("/Player|Health|Value", steps[0].Query);
        }

        // ── 7. Unknown macro name throws ──────────────────────────────────────────

        [Test]
        public void Parse_MacroUnknownName_Throws()
        {
            var script = "CALL nonexistent";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // ── 8. Empty macro body produces no steps ────────────────────────────────

        [Test]
        public void Parse_MacroEmptyBody_ProducesNoSteps()
        {
            var script = @"MACRO noop
END_MACRO
CALL noop";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(0, steps.Count);
        }

        // ── 9. Missing END_MACRO throws ───────────────────────────────────────────

        [Test]
        public void Parse_MacroNoEndMacro_Throws()
        {
            var script = @"MACRO broken
  LOG something";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // ── 10. Multiple CALLs expand all ─────────────────────────────────────────

        [Test]
        public void Parse_MultipleCallsSameMacro_ExpandsEach()
        {
            var script = @"MACRO ping
  LOG ping
END_MACRO
CALL ping
CALL ping";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
            Assert.AreEqual(StepType.Log, steps[1].Type);
        }

        // ── 11. $1 doesn't collide with $10 ──────────────────────────────────────

        [Test]
        public void Parse_MacroTenParams_NoCollision()
        {
            var script = "MACRO f $1 $2 $3 $4 $5 $6 $7 $8 $9 $10\nLOG $10 $1\nEND_MACRO\nCALL f a b c d e f g h i j";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual("j a", steps[0].Message); // $10→j, $1→a, no collision
        }

        // ── 12. Fewer call args than declared throws ──────────────────────────────

        [Test]
        public void Parse_MacroFewerArgs_Throws()
        {
            var script = "MACRO greet $1 $2\nLOG $1 $2\nEND_MACRO\nCALL greet hello";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // ── 13. Nested MACRO definition throws ────────────────────────────────────

        [Test]
        public void Parse_NestedMacro_Throws()
        {
            var script = "MACRO outer $1\nMACRO inner $1\nLOG $1\nEND_MACRO\nEND_MACRO\nCALL outer x";
            Assert.Throws<ArgumentException>(() => PlaytestParser.Parse(script));
        }

        // ── 14. Macro name is case-insensitive ────────────────────────────────────

        [Test]
        public void Parse_MacroNameCaseInsensitive_Works()
        {
            var script = @"MACRO MyMacro
  LOG ok
END_MACRO
CALL mymacro";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(1, steps.Count);
        }

        // ── 12. MACRO body with multiple steps produces all steps ─────────────────

        [Test]
        public void Parse_MacroMultiLineBody_ProducesAllSteps()
        {
            var script = @"MACRO setup
  LOG start
  WAIT 1
  LOG end
END_MACRO
CALL setup";
            var steps = PlaytestParser.Parse(script);
            Assert.AreEqual(3, steps.Count);
            Assert.AreEqual(StepType.Log, steps[0].Type);
            Assert.AreEqual(StepType.Wait, steps[1].Type);
            Assert.AreEqual(StepType.Log, steps[2].Type);
        }
    }
}
