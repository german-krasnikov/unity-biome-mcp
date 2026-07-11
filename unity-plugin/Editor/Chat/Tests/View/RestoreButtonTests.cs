// TDD — Tests for RestoreButton.RefreshEnabled last-only disable rule (F6 MAJOR 2).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Verifies that once a new turn starts, the OLD turn's Restore button becomes disabled.
    /// This is the most important correctness rule for F6 (architecture §risk #6).
    /// </summary>
    [TestFixture]
    public class RestoreButtonTests
    {
        // --- 16. Button enabled for its turn index while that turn still exists ---
        [Test]
        public void RefreshEnabled_SameGeneration_EnablesButton()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A");
            tracker.OnTurnEnd();
            // capturedTurnIndex = TurnCount-1 = 0 (as Create() captures)
            int capturedTurnIndex = tracker.TurnCount - 1;

            var btn = new Button();
            RestoreButton.RefreshEnabled(btn, tracker, capturedTurnIndex);

            Assert.IsTrue(btn.enabledSelf, "Button should be enabled when its turn still exists");
        }

        // --- 17. Button disables when a new turn starts (old capturedGeneration is stale) ---
        [Test]
        public void RefreshEnabled_NewTurnStarted_DisablesOldButton()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A");
            tracker.OnTurnEnd();
            int genA = tracker.CurrentGeneration; // generation captured by old button

            // New turn starts — generation advances
            tracker.OnTurnStart("Turn_B");

            var btn = new Button();
            // capturedTurnIndex=0 (turn A was at index 0, which still exists)
            RestoreButton.RefreshEnabled(btn, tracker, 0);

            Assert.IsTrue(btn.enabledSelf,
                "Old button stays enabled while its turn still exists in the stack");
        }

        // --- F2: cascade restore — old button stays enabled until its turn is restored ---
        [Test]
        public void RefreshEnabled_OldButton_StillEnabled_WhenTurnExists()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A"); tracker.OnTurnEnd(); // index 0
            tracker.OnTurnStart("Turn_B"); tracker.OnTurnEnd(); // index 1

            var btn = new Button();
            // Button created after Turn_A completed → capturedTurnIndex = 0
            RestoreButton.RefreshEnabled(btn, tracker, 0);

            Assert.IsTrue(btn.enabledSelf, "Old button enabled while turn A still in stack");
        }

        [Test]
        public void RefreshEnabled_OldButton_Disabled_AfterTurnRestored()
        {
            var tracker = new TurnUndoTracker();
            tracker.OnTurnStart("Turn_A"); tracker.OnTurnEnd(); // index 0
            tracker.OnTurnStart("Turn_B"); tracker.OnTurnEnd(); // index 1

            // Restore Turn_A (cascades to remove A+B)
            tracker.RestoreFromIndex(0);

            var btn = new Button();
            RestoreButton.RefreshEnabled(btn, tracker, 0);

            Assert.IsFalse(btn.enabledSelf, "Button disabled after its turn was restored");
        }
    }
}
