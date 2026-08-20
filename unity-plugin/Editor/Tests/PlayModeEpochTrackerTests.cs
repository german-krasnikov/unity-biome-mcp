// TDD tests for GAP 3: PlayModeEpochTracker (MCP-LIFE-004/005).
// EditMode only — no real Play Mode entered; state transitions driven via internal seams.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlayModeEpochTrackerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => PlayModeEpochTracker.ResetForTest();

        [Test]
        public void Epoch_InitiallyZero()
        {
            Assert.AreEqual(0, PlayModeEpochTracker.Epoch);
        }

        [Test]
        public void Epoch_IncrementsOnEnteredPlayMode()
        {
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            Assert.AreEqual(1, PlayModeEpochTracker.Epoch);

            PlayModeEpochTracker.ResetForTest();
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            Assert.AreEqual(2, PlayModeEpochTracker.Epoch);
        }

        [Test]
        public void WorldReady_FalseBeforeFirstFrame()
        {
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            Assert.IsFalse(PlayModeEpochTracker.WorldReady);
        }

        [Test]
        public void WorldReady_TrueAfterUpdateCallback()
        {
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            Assert.IsFalse(PlayModeEpochTracker.WorldReady);

            PlayModeEpochTracker.SimulateFirstFrame();
            Assert.IsTrue(PlayModeEpochTracker.WorldReady);
        }

        [Test]
        public void ResetForTest_ResetsEpochAndWorldReady()
        {
            PlayModeEpochTracker.SimulateEnteredPlayMode();
            PlayModeEpochTracker.SimulateFirstFrame();
            Assert.AreNotEqual(0, PlayModeEpochTracker.Epoch);
            Assert.IsTrue(PlayModeEpochTracker.WorldReady);

            PlayModeEpochTracker.ResetForTest();

            Assert.AreEqual(0, PlayModeEpochTracker.Epoch);
            Assert.IsFalse(PlayModeEpochTracker.WorldReady);
        }
    }
}
