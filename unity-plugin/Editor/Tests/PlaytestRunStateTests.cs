using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown() => PlaytestRunState.ResetForTests();

        // Bounded wait shared by this fixture — races the TCS against a fixed timeout rather
        // than an unbounded await (same pattern as PlaytestRunnerEditModeTests.cs).
        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds = 5.0)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        [Test]
        public void Current_BeforeAnyRun_IsIdleSentinel()
        {
            PlaytestRunState.ResetForTests();

            var snap = PlaytestRunState.Current;

            Assert.IsNull(snap.RunId);
            Assert.AreEqual(PlaytestRunState.RunPhase.Idle, snap.Phase);
            Assert.AreEqual(default(DateTime), snap.StartUtc);
            Assert.AreEqual(0, snap.passed);
            Assert.AreEqual(0, snap.failed);
        }

        [Test]
        public async Task Current_DuringRun_ExposesRunIdAndStepIndex()
        {
            // WAIT window kept generous (2s) with only a couple of ticks polled afterward —
            // this observes transient mid-run state, not tick-exact timing, so the margin
            // matters more than the tick count.
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG start\nWAIT 2.0\nLOG done", 10f, tcs,
                requiresPlayMode: false, runId: "cafebabe");

            await WaitForEditorUpdatesAsync(2);
            var snap = PlaytestRunState.Current;

            Assert.AreEqual("cafebabe", snap.RunId);
            Assert.AreEqual(1, snap.StepIndex); // 0-based: WAIT is the second step
            Assert.AreEqual(PlaytestRunState.RunPhase.Running, snap.Phase);
            Assert.Less((DateTime.Now - snap.StartUtc).TotalSeconds, 10.0);

            await AwaitBoundedAsync(tcs, timeoutSeconds: 10.0); // drain — avoid leaking a running playtest
        }

        [Test]
        public async Task Current_AfterRunCompletes_ReflectsTerminalState()
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs, requiresPlayMode: false, runId: "deadbeef");

            await AwaitBoundedAsync(tcs);
            var snap = PlaytestRunState.Current;

            Assert.AreEqual("deadbeef", snap.RunId);
            Assert.AreEqual(PlaytestRunState.RunPhase.Passed, snap.Phase);
            Assert.AreEqual(0, snap.failed);
        }
    }
}
