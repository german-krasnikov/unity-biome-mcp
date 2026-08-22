using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerFastPlayModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
        }

        [Test]
        public void OnPlayModeStateChanged_WhenFastPlayModeEnabled_CallsCleanup()
        {
            PlaytestRunner.CompleteRunCleanupForTests();
            MCPSettings.SetFastPlayMode(true);
            PlaytestRunner.SimulatePlayModeStateChange(PlayModeStateChange.ExitingPlayMode);
            Assert.IsFalse(PlaytestRunner.IsRunningForTest,
                "ExitingPlayMode with fast_play_mode=true must call CompleteRunCleanup");
        }

        [Test]
        public void OnPlayModeStateChanged_WhenFastPlayModeDisabled_LeavesIsRunningUnchanged()
        {
            PlaytestRunner.SetRunningForTest(true);
            MCPSettings.SetFastPlayMode(false);
            PlaytestRunner.SimulatePlayModeStateChange(PlayModeStateChange.ExitingPlayMode);
            Assert.IsTrue(PlaytestRunner.IsRunningForTest,
                "ExitingPlayMode with fast_play_mode=false must NOT call CompleteRunCleanup");
        }

        [Test]
        public void OnPlayModeStateChanged_EnteringPlayMode_IsAlwaysNoOp()
        {
            MCPSettings.SetFastPlayMode(true);
            Assert.DoesNotThrow(() =>
                PlaytestRunner.SimulatePlayModeStateChange(PlayModeStateChange.EnteredPlayMode),
                "Non-Exiting state must never cause exceptions");
        }
    }
}
