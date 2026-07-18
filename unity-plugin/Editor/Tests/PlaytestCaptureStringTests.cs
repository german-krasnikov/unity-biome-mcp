// TDD: String CAPTURE + ASSERT_CHANGED — pure-logic, EditMode safe.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestCaptureStringTests
    {
        // ── 1. ASSERT_CHANGED parsed correctly ────────────────────────────────────

        [Test]
        public void Parse_AssertChanged_StepType()
        {
            var steps = PlaytestParser.Parse("CAPTURE $hp /Player|Health|hp\nASSERT_CHANGED $hp");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.AssertChanged, steps[1].Type);
            Assert.AreEqual("$hp", steps[1].Message);
        }

        // ── 2. Capture stores raw string value ───────────────────────────────────

        [Test]
        public void State_Capture_StoresRaw()
        {
            var state = new PlaytestState();
            state.Capture("hp", "/Player|Health|hp", "42", 42f);
            Assert.AreEqual("42", state.GetCapturedRaw("hp"));
            Assert.AreEqual(42f, state.GetCapturedValue("hp"), 0.001f);
        }

        // ── 3. IsChanged returns true when string differs ─────────────────────────

        [Test]
        public void State_IsChanged_DifferentString_True()
        {
            var state = new PlaytestState();
            state.Capture("status", "/NPC|Status|name", "alive", 0f);
            Assert.IsTrue(state.IsChanged("status", "dead"));
        }

        // ── 4. IsChanged returns false when string is same ───────────────────────

        [Test]
        public void State_IsChanged_SameString_False()
        {
            var state = new PlaytestState();
            state.Capture("status", "/NPC|Status|name", "alive", 0f);
            Assert.IsFalse(state.IsChanged("status", "alive"));
        }

        // ── 5. IsChanged comparison is case-insensitive ───────────────────────────

        [Test]
        public void State_IsChanged_CaseInsensitive_False()
        {
            var state = new PlaytestState();
            state.Capture("flag", "/Door|Flag|open", "True", 0f);
            Assert.IsFalse(state.IsChanged("flag", "true"));
        }

        // ── 6. IsChanged detects numeric change ───────────────────────────────────

        [Test]
        public void State_IsChanged_NumericChange_True()
        {
            var state = new PlaytestState();
            state.Capture("hp", "/Player|Health|hp", "100", 100f);
            Assert.IsTrue(state.IsChanged("hp", "90"));
        }

        // ── 7. AssertChanged in _evidenceTypes ───────────────────────────────────

        [Test]
        public void AssertChanged_InEvidenceTypes()
        {
            Assert.IsTrue(PlaytestLinter._evidenceTypes.Contains(StepType.AssertChanged));
        }

        // ── 8. ASSERT_CHANGED in _DSL_KEYWORDS ───────────────────────────────────

        [Test]
        public void AssertChanged_InDSLKeywords()
        {
            Assert.IsTrue(PlaytestParser._DSL_KEYWORDS.Contains("ASSERT_CHANGED"));
        }
    }
}
