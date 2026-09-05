using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

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

            // Review note (B14, closed as comment only): if an assertion above throws before this
            // drain runs, PlaytestRunner's `_isRunning` flag is left stuck true — no ForceStop()/
            // abort API exists yet to recover the runner for the next test in the same session.
            // Not fixed here; flagged so a future flake traces back to this known gap.
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

        // ── E01: Wave E's async-dispatch contract — total step count + terminal text/receipt ──

        [Test]
        public async Task Current_DuringRun_TotalStepsMatchesScript()
        {
            var tcs = new TaskCompletionSource<string>();
            // 3 steps: LOG, WAIT, LOG — WAIT keeps the run mid-flight long enough to observe.
            PlaytestRunner.Run("LOG a\nWAIT 2.0\nLOG b", 10f, tcs, requiresPlayMode: false, runId: "e01steps");

            await WaitForEditorUpdatesAsync(2);
            Assert.AreEqual(3, PlaytestRunState.Current.TotalSteps);

            await AwaitBoundedAsync(tcs, timeoutSeconds: 10.0); // drain
        }

        [Test]
        public async Task Current_AfterFinish_TerminalTextMatchesCallerResultAndReceipt()
        {
            const string runId = "e01term1";
            var receiptPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs, requiresPlayMode: false, runId: runId);
            var result = await AwaitBoundedAsync(tcs);

            var snap = PlaytestRunState.Current;
            Assert.AreEqual(result, snap.TerminalText,
                "Current.TerminalText must be exactly what the caller's tcs received");

            Assert.IsTrue(File.Exists(receiptPath));
            var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
            StringAssert.Contains($"\"text_report\":\"{result}\"", json,
                "the persisted receipt's text_report must match the same terminal text");
        }

        [Test]
        public void UnknownRunId_IsNotActiveAndHasNoReceipt()
        {
            const string unknownId = "e01ghost";
            var receiptPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(unknownId)));

            Assert.AreNotEqual(unknownId, PlaytestRunState.Current.RunId,
                "a never-dispatched id must not appear as the active run");
            Assert.IsFalse(File.Exists(receiptPath),
                "a never-dispatched id must have no persisted receipt either");
        }
    }
}
