// TDD: E02 — start_playtest non-blocking dispatch. Structurally identical to run_playtest
// (same script/path/header/dirty gates via the shared TryBuildPlaytestRunRequest helper) except
// the final dispatch contract: preallocate a run_id via PlaytestRunner.CreateRunId(), pass it into
// PlaytestRunner.Run(runId:), and return immediately with a "run_id=xxxxxxxx" sentinel instead of
// waiting for the whole playtest to finish. Dispatched end-to-end through CommandRouter.ProcessAsync
// (same pattern as PlaytestPathTests.cs / PlaytestRunnerEditModeTests.cs).
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AsyncStartPlaytestTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown() => PlaytestRunState.ResetForTests();

        private static async Task<string> DispatchAsync(string argsJson, double timeoutSeconds = 5.0)
        {
            var json = $"{{\"id\":\"t\",\"cmd\":\"start_playtest\",\"args\":{{{argsJson}}}}}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CommandRouter.ProcessAsync(json, tcs);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        private static string ExtractRunId(string result)
        {
            var marker = "run_id=";
            var idx = result.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(idx, 0, $"expected a run_id= sentinel in: {result}");
            var start = idx + marker.Length;
            var end = start;
            while (end < result.Length && Uri.IsHexDigit(result[end])) end++;
            return result.Substring(start, end - start);
        }

        // Bounded poll on PlaytestRunState.Current — the dispatch response itself resolves
        // synchronously, so this is the only way a test can drain a still-running playtest
        // without leaking a stuck _isRunning flag into the next test.
        private static async Task WaitForTerminalPhaseAsync(string runId, double timeoutSeconds = 10.0)
        {
            var deadline = DateTime.Now.AddSeconds(timeoutSeconds);
            while (DateTime.Now < deadline)
            {
                var snap = PlaytestRunState.Current;
                if (snap.RunId == runId &&
                    (snap.Phase == PlaytestRunState.RunPhase.Passed || snap.Phase == PlaytestRunState.RunPhase.Failed))
                    return;
                await Task.Delay(50);
            }
            Assert.Fail($"run {runId} did not reach a terminal phase within {timeoutSeconds}s");
        }

        [Test]
        public async Task StartPlaytest_ReturnsBeforeWaitElapses_WithRunId()
        {
            var sw = Stopwatch.StartNew();
            var result = await DispatchAsync("\"script\":\"# @needs editmode\\nWAIT 2.0\"");
            sw.Stop();

            Assert.Less(sw.Elapsed.TotalSeconds, 1.5, "start_playtest must return before the WAIT elapses");
            var runId = ExtractRunId(result);
            Assert.AreEqual(8, runId.Length);
            Assert.AreEqual(runId, PlaytestRunState.Current.RunId,
                "the returned run_id must be the one PlaytestRunState tracks");

            await WaitForTerminalPhaseAsync(runId); // drain — avoid leaking a running playtest
        }

        [Test]
        public async Task StartPlaytest_RunIdPropagatesToReceipt()
        {
            var result = await DispatchAsync("\"script\":\"# @needs editmode\\nLOG hi\"");
            var runId = ExtractRunId(result);
            await WaitForTerminalPhaseAsync(runId);

            var receiptPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));
            RegisterCleanup(() => { if (System.IO.File.Exists(receiptPath)) System.IO.File.Delete(receiptPath); });
            Assert.IsTrue(System.IO.File.Exists(receiptPath), $"expected canonical receipt at {receiptPath}");
            var json = System.IO.File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
            StringAssert.Contains($"\"run_id\":\"{runId}\"", json);
        }

        [Test]
        public async Task StartPlaytest_ConcurrentDispatch_HitsIsRunningGuard_NoSecondId()
        {
            var first = await DispatchAsync("\"script\":\"# @needs editmode\\nWAIT 2.0\"");
            var firstId = ExtractRunId(first);

            var second = await DispatchAsync("\"script\":\"# @needs editmode\\nLOG two\"");
            StringAssert.Contains("already running", second);
            StringAssert.DoesNotContain("run_id=", second,
                "a guard-rejected dispatch must never surface a run_id — it never ran");
            Assert.AreEqual(firstId, PlaytestRunState.Current.RunId,
                "the guard must not fork a second identity");

            await WaitForTerminalPhaseAsync(firstId); // drain the first, real run
        }
    }
}
