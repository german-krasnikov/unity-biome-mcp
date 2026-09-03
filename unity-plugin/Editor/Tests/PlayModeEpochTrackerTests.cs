// TDD tests for GAP 3: PlayModeEpochTracker (MCP-LIFE-004/005).
// EditMode only — no real Play Mode entered; state transitions driven via internal seams.
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlayModeEpochTrackerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            var savedEpoch = PlayModeEpochTracker.Epoch;
            var savedWorldReady = PlayModeEpochTracker.WorldReady;
            var savedPendingStop = SessionState.GetBool(PlayModeEpochTracker.PendingPlayStopKey, false);
            var savedPendingStart = SessionState.GetBool(PlayModeEpochTracker.PendingPlayStartKey, false);
            RegisterCleanup(() =>
            {
                PlayModeEpochTracker.RestoreForTest(savedEpoch, savedWorldReady);
                PlayModeEpochTracker.ResetPlayModeSeamsForTest();
                PlayModeEpochTracker.ResetWaitForCompileGuardForTest();
                SessionState.SetBool(PlayModeEpochTracker.PendingPlayStopKey, savedPendingStop);
                SessionState.SetBool(PlayModeEpochTracker.PendingPlayStartKey, savedPendingStart);
            });
            PlayModeEpochTracker.ResetForTest();
            SessionState.EraseBool(PlayModeEpochTracker.PendingPlayStopKey);
            SessionState.EraseBool(PlayModeEpochTracker.PendingPlayStartKey);
        }

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

        // ── force_play_stop reload-survival (DEV-66 Part B) ─────────────────────

        [Test]
        public void OnPlayModeStateChanged_PendingPlayStopFlagSet_RequestsExitAndClearsFlag()
        {
            SessionState.SetBool(PlayModeEpochTracker.PendingPlayStopKey, true);
            var exitRequested = false;
            PlayModeEpochTracker.RequestPlayModeExit = () => exitRequested = true;

            PlayModeEpochTracker.SimulateEnteredPlayMode();

            Assert.IsTrue(exitRequested,
                "a pending force_play_stop flag must trigger the exit-request seam on EnteredPlayMode");
            Assert.IsFalse(SessionState.GetBool(PlayModeEpochTracker.PendingPlayStopKey, false),
                "the pending flag must be cleared once the exit has been requested");
        }

        [Test]
        public void OnPlayModeStateChanged_NoPendingPlayStopFlag_DoesNotRequestExit()
        {
            var exitRequested = false;
            PlayModeEpochTracker.RequestPlayModeExit = () => exitRequested = true;

            PlayModeEpochTracker.SimulateEnteredPlayMode();

            Assert.IsFalse(exitRequested,
                "EnteredPlayMode must not request an exit when no force_play_stop is pending");
        }

        [Test]
        public void WaitForCompileThenEnterPlayMode_FlagSetAndCompilingClears_RequestsEnterAndArmsStopFlag()
        {
            SessionState.SetBool(PlayModeEpochTracker.PendingPlayStartKey, true);
            PlayModeEpochTracker.IsCompiling = () => false;
            var enterRequested = false;
            PlayModeEpochTracker.RequestPlayModeEnter = () => enterRequested = true;

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            PlayModeEpochTracker.WaitForCompileThenEnterPlayMode();
            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            RegisterCleanup(() =>
            {
                foreach (var d in added) EditorApplication.update -= (EditorApplication.CallbackFunction)d;
                PlayModeEpochTracker.ResetWaitForCompileGuardForTest();
            });

            Assert.GreaterOrEqual(added.Length, 1, "must arm an EditorApplication.update poll");
            foreach (var d in added) d.DynamicInvoke(); // simulate the next Editor tick

            Assert.IsTrue(enterRequested, "compilation finished — must request Play Mode entry via the seam");
            Assert.IsFalse(SessionState.GetBool(PlayModeEpochTracker.PendingPlayStartKey, false),
                "PendingPlayStartKey must be cleared once entry has been requested");
            Assert.IsTrue(SessionState.GetBool(PlayModeEpochTracker.PendingPlayStopKey, false),
                "the stop flag must be armed at the exact moment Play Mode entry is requested");
        }

        [Test]
        public void WaitForCompileThenEnterPlayMode_WhileStillCompiling_DoesNotArmStopFlagYet()
        {
            SessionState.SetBool(PlayModeEpochTracker.PendingPlayStartKey, true);
            PlayModeEpochTracker.IsCompiling = () => true;
            var enterRequested = false;
            PlayModeEpochTracker.RequestPlayModeEnter = () => enterRequested = true;

            var before = EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>();
            PlayModeEpochTracker.WaitForCompileThenEnterPlayMode();
            var added = (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
                .Except(before).ToArray();
            RegisterCleanup(() =>
            {
                foreach (var d in added) EditorApplication.update -= (EditorApplication.CallbackFunction)d;
                PlayModeEpochTracker.ResetWaitForCompileGuardForTest();
            });

            foreach (var d in added) d.DynamicInvoke(); // one tick, still compiling

            Assert.IsFalse(enterRequested, "must not enter Play Mode while still compiling");
            Assert.IsFalse(SessionState.GetBool(PlayModeEpochTracker.PendingPlayStopKey, false),
                "the stop flag must not be armed before Play Mode entry has actually been requested — " +
                "otherwise an unrelated later Play Mode session would be unexpectedly interrupted");
            Assert.IsTrue(SessionState.GetBool(PlayModeEpochTracker.PendingPlayStartKey, false),
                "the start flag must remain set while still waiting for compilation to finish");
        }
    }
}
