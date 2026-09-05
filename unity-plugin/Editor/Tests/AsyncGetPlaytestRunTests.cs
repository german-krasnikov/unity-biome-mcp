// TDD: E03 — get_playtest_run compact poll, the read half of Wave E's async start/poll pair.
// First matches PlaytestRunState.Current (in-progress compact sentinel, or the exact terminal
// text E01 threaded through Finish()); falls back to the canonical receipt's text_report once
// in-memory state is gone (simulated domain reload). Unknown ids fail closed with a clear error.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AsyncGetPlaytestRunTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown() => PlaytestRunState.ResetForTests();

        private static async Task<string> DispatchAsync(string cmd, string argsJson, double timeoutSeconds = 5.0)
        {
            var json = $"{{\"id\":\"t\",\"cmd\":\"{cmd}\",\"args\":{{{argsJson}}}}}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CommandRouter.ProcessAsync(json, tcs);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        private static string ExtractRunId(string startResult)
        {
            var marker = "run_id=";
            var idx = startResult.IndexOf(marker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(idx, 0, $"expected a run_id= sentinel in: {startResult}");
            var start = idx + marker.Length;
            var end = start;
            while (end < startResult.Length && Uri.IsHexDigit(startResult[end])) end++;
            return startResult.Substring(start, end - start);
        }

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

        private static string ReceiptFullPath(string runId) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));

        [Test]
        public async Task GetPlaytestRun_ImmediatelyAfterStart_ReturnsInProgressCompact()
        {
            // 4 steps: LOG, WAIT, LOG, LOG — WAIT keeps it mid-flight long enough to poll.
            var startResult = await DispatchAsync("start_playtest",
                "\"script\":\"# @needs editmode\\nLOG a\\nWAIT 2.0\\nLOG b\\nLOG c\"");
            var runId = ExtractRunId(startResult);
            RegisterCleanup(() => { var p = ReceiptFullPath(runId); if (File.Exists(p)) File.Delete(p); });

            var poll = await DispatchAsync("get_playtest_run", $"\"run_id\":\"{runId}\"");
            StringAssert.IsMatch(@"phase=running\|step=\d+/4\|elapsed_ms=\d+", poll);

            await WaitForTerminalPhaseAsync(runId); // drain
        }

        [Test]
        public async Task GetPlaytestRun_AfterCompletion_ReturnsTerminalTextMatchingCurrent()
        {
            var startResult = await DispatchAsync("start_playtest", "\"script\":\"# @needs editmode\\nLOG hello\"");
            var runId = ExtractRunId(startResult);
            RegisterCleanup(() => { var p = ReceiptFullPath(runId); if (File.Exists(p)) File.Delete(p); });
            await WaitForTerminalPhaseAsync(runId);

            var expectedTerminal = PlaytestRunState.Current.TerminalText;
            var poll = await DispatchAsync("get_playtest_run", $"\"run_id\":\"{runId}\"");

            // poll is the wire-wrapped JSON envelope ({"id":...,"ok":true,"data":"<escaped>"}) —
            // the escaped terminal text must appear verbatim inside it.
            StringAssert.Contains(JsonHelper.EscapeJson(expectedTerminal), poll);
        }

        [Test]
        public async Task GetPlaytestRun_AfterSimulatedReload_ReturnsReceiptTextReport_ByteIdenticalToBeforeReload()
        {
            var startResult = await DispatchAsync("start_playtest", "\"script\":\"# @needs editmode\\nLOG hello\"");
            var runId = ExtractRunId(startResult);
            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });
            await WaitForTerminalPhaseAsync(runId);

            var beforeReload = await DispatchAsync("get_playtest_run", $"\"run_id\":\"{runId}\"");

            PlaytestRunState.ResetForTests(); // simulates a domain reload clearing in-memory state

            var afterReload = await DispatchAsync("get_playtest_run", $"\"run_id\":\"{runId}\"");

            Assert.AreEqual(beforeReload, afterReload,
                "the receipt fallback must return the byte-identical terminal text as the in-memory path");
        }

        [Test]
        public async Task GetPlaytestRun_CorruptReceipt_MismatchedRunId_FailsClosed()
        {
            const string runId = "e03badid1";
            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });
            Directory.CreateDirectory(Path.GetDirectoryName(receiptPath));
            // A receipt physically named for `runId` but whose own run_id field disagrees —
            // simulates disk corruption / a copy-paste mistake, not a real PlaytestRunner output.
            File.WriteAllText(receiptPath,
                "{\"schema_version\":1,\"run_id\":\"someone-else\",\"text_report\":\"PLAYTEST: 1/1 (0.0s)\"}",
                new UTF8Encoding(false));

            var poll = await DispatchAsync("get_playtest_run", $"\"run_id\":\"{runId}\"");
            StringAssert.Contains("corrupt", poll.ToLowerInvariant());
            StringAssert.DoesNotContain("PLAYTEST: 1/1", poll,
                "a mismatched receipt's content must never be surfaced as if it were valid");
        }

        [Test]
        public async Task GetPlaytestRun_UnknownId_ReturnsError_NeverAnotherRunsResult()
        {
            var startResult = await DispatchAsync("start_playtest", "\"script\":\"# @needs editmode\\nLOG hello\"");
            var realRunId = ExtractRunId(startResult);
            RegisterCleanup(() => { var p = ReceiptFullPath(realRunId); if (File.Exists(p)) File.Delete(p); });
            await WaitForTerminalPhaseAsync(realRunId);
            var realTerminal = PlaytestRunState.Current.TerminalText;

            var poll = await DispatchAsync("get_playtest_run", "\"run_id\":\"e03ghost\"");
            StringAssert.Contains("unknown", poll.ToLowerInvariant());
            StringAssert.DoesNotContain(JsonHelper.EscapeJson(realTerminal), poll,
                "an unknown id must never return another run's result");
        }
    }
}
