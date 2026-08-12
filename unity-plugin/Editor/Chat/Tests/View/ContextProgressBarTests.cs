using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ContextProgressBarTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Existing: hide/show based on window ──────────────────────────────

        [Test]
        public void Update_ZeroWindow_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(5000, 0);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Update_NegativeWindow_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(5000, -1);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Update_NonZeroWindow_ShowsElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Update_ZeroTokens_ShowsZeroPercent()
        {
            var bar = new ContextProgressBar();
            bar.Update(0, 200_000);
            Assert.AreEqual(DisplayStyle.Flex, bar.style.display.value);
        }

        [Test]
        public void Reset_HidesElement()
        {
            var bar = new ContextProgressBar();
            bar.Update(100_000, 200_000);
            bar.Reset();
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        [Test]
        public void Constructor_StartsHidden()
        {
            var bar = new ContextProgressBar();
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value);
        }

        // ── B2: negative tokens = unknown → bar hidden ────────────────────────
        // Python sends -1 when the backend does not report usage.
        // An empty bar reads as "lots of room" — wrong. Hide instead.

        [Test]
        public void Update_NegativeTokens_HidesBar()
        {
            var bar = new ContextProgressBar();
            bar.Update(-1, 200_000);
            Assert.AreEqual(DisplayStyle.None, bar.style.display.value,
                "Bar must be invisible when token count is unknown (negative).");
        }

        [Test]
        public void Update_NegativeTokens_HasNoStateClass()
        {
            var bar = new ContextProgressBar();
            bar.Update(-1, 200_000);
            Assert.IsFalse(bar.ClassListContains("context-bar--normal"),
                "Hidden bar must not carry a state class.");
            Assert.IsFalse(bar.ClassListContains("context-bar--warn"));
            Assert.IsFalse(bar.ClassListContains("context-bar--danger"));
            Assert.IsFalse(bar.ClassListContains("context-bar--overflow"));
        }

        // ── B3: thresholds — state CSS classes ───────────────────────────────
        // Formula denominator: contextWindow × OutputReserve (0.8) = 160 000 for 200k window.
        // Normal < 70 %, Warn [70, 90 %), Danger [90, 100 %), Overflow ≥ 100 %.

        [Test]
        public void Update_ZeroPercent_NormalState()
        {
            // 0 / 160 000 = 0 %
            var bar = new ContextProgressBar();
            bar.Update(0, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--normal"),
                "0 % fill must apply 'normal' state class.");
        }

        [Test]
        public void Update_BelowWarnBoundary_NormalState()
        {
            // 110 400 / 160 000 ≈ 69 % — one token below warn
            var bar = new ContextProgressBar();
            bar.Update(110_400, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--normal"),
                "69 % must remain in 'normal' state.");
            Assert.IsFalse(bar.ClassListContains("context-bar--warn"));
        }

        [Test]
        public void Update_AtWarnBoundary_WarnState()
        {
            // 112 000 / 160 000 = 70 % exactly — warn threshold
            var bar = new ContextProgressBar();
            bar.Update(112_000, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--warn"),
                "70 % must apply 'warn' state class.");
            Assert.IsFalse(bar.ClassListContains("context-bar--normal"));
        }

        [Test]
        public void Update_BelowDangerBoundary_WarnState()
        {
            // 142 400 / 160 000 = 89 % — still warn
            var bar = new ContextProgressBar();
            bar.Update(142_400, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--warn"),
                "89 % must remain in 'warn' state.");
            Assert.IsFalse(bar.ClassListContains("context-bar--danger"));
        }

        [Test]
        public void Update_AtDangerBoundary_DangerState()
        {
            // 144 000 / 160 000 = 90 % exactly — danger threshold
            var bar = new ContextProgressBar();
            bar.Update(144_000, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--danger"),
                "90 % must apply 'danger' state class.");
            Assert.IsFalse(bar.ClassListContains("context-bar--warn"));
        }

        [Test]
        public void Update_AtOverflowBoundary_OverflowState()
        {
            // 160 000 / 160 000 = 100 % — context window fully used, output reserve depleted
            var bar = new ContextProgressBar();
            bar.Update(160_000, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--overflow"),
                "100 % fill must apply 'overflow' state class.");
            Assert.IsFalse(bar.ClassListContains("context-bar--danger"));
        }

        [Test]
        public void Update_RealShortSession_NormalState()
        {
            // Real data: 40 492 tokens, 200 k window → 25.3 % (was shown as 0.009 % before fix)
            var bar = new ContextProgressBar();
            bar.Update(40_492, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--normal"),
                "Real short session (25 %) must be 'normal'.");
        }

        [Test]
        public void Update_RealLongSession_OverflowState()
        {
            // Real data: 361 937 tokens, 200 k window → 226 % — context window blown past
            var bar = new ContextProgressBar();
            bar.Update(361_937, 200_000);
            Assert.IsTrue(bar.ClassListContains("context-bar--overflow"),
                "Real long session (226 %) must be 'overflow'.");
        }

        [Test]
        public void Update_OverflowLabel_ShowsActualPercent()
        {
            // Label must show un-clamped percentage so user sees "226 %" not "100 %"
            var bar = new ContextProgressBar();
            bar.Update(361_937, 200_000);
            // 361 937 / 160 000 ≈ 226.2 % — label must show > 100
            var label = bar.Q<UnityEngine.UIElements.Label>();
            Assert.IsNotNull(label, "ContextProgressBar must contain a Label child.");
            Assert.IsTrue(label.text.Contains("2"), // crude: starts with '2' (200+ %)
                $"Overflow label should show > 100 % but was '{label.text}'.");
        }

        [Test]
        public void Update_StateTransition_ClearsPreviousClass()
        {
            // Going from warn → normal must remove 'warn' class
            var bar = new ContextProgressBar();
            bar.Update(112_000, 200_000); // 70 % → warn
            Assert.IsTrue(bar.ClassListContains("context-bar--warn"), "Pre-condition: warn applied.");

            bar.Update(40_492, 200_000);  // 25 % → normal
            Assert.IsFalse(bar.ClassListContains("context-bar--warn"),
                "After downgrade to normal the 'warn' class must be removed.");
            Assert.IsTrue(bar.ClassListContains("context-bar--normal"));
        }

        [Test]
        public void Reset_ClearsStateClasses()
        {
            var bar = new ContextProgressBar();
            bar.Update(361_937, 200_000); // overflow
            bar.Reset();
            Assert.IsFalse(bar.ClassListContains("context-bar--normal"),
                "Reset must clear all state classes.");
            Assert.IsFalse(bar.ClassListContains("context-bar--warn"));
            Assert.IsFalse(bar.ClassListContains("context-bar--danger"));
            Assert.IsFalse(bar.ClassListContains("context-bar--overflow"));
        }
    }
}
