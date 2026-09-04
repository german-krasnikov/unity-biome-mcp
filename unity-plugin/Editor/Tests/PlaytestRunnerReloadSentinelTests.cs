// TDD: C05 — reload sentinel (fail-loud, no resume). A `.running` sentinel marks a playtest run
// as genuinely in flight; FinishRun() deletes it on normal completion. If a domain reload kills
// the run mid-flight (MCPServer.OnBeforeReload tears down the TCS with nothing to resume), the
// sentinel survives into the next domain and PlaytestRunner's [InitializeOnLoad] static ctor
// converts it into a durable ABORTED receipt via the internal ReapOrphanedSentinels() seam.
// [BiomeWorkerOnly]: writes/reads real files under Library/UnityMCP/playtest and mutates the
// shared PlaytestRunState.Current singleton directly.
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerReloadSentinelTests : UnityMcpTestBase
    {
        private static string SentinelFullPath(string runId) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.SentinelPath(runId)));

        private static string ReceiptFullPath(string runId) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));

        private static async Task<string> AwaitBoundedAsync(
            TaskCompletionSource<string> tcs, double timeoutSeconds = 5.0)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        [Test]
        [BiomeWorkerOnly("Writes real files under Library/UnityMCP/playtest and mutates PlaytestRunState.Current.")]
        public void StaticCtor_OrphanedSentinelFile_ProducesAbortedReceipt()
        {
            const string orphanRunId = "c05orph01";
            const string liveRunId = "c05live01";
            var orphanSentinel = SentinelFullPath(orphanRunId);
            var orphanReceipt = ReceiptFullPath(orphanRunId);
            var liveSentinel = SentinelFullPath(liveRunId);
            var liveReceipt = ReceiptFullPath(liveRunId);
            RegisterCleanup(() => { if (File.Exists(orphanSentinel)) File.Delete(orphanSentinel); });
            RegisterCleanup(() => { if (File.Exists(orphanReceipt)) File.Delete(orphanReceipt); });
            RegisterCleanup(() => { if (File.Exists(liveSentinel)) File.Delete(liveSentinel); });
            RegisterCleanup(() => { if (File.Exists(liveReceipt)) File.Delete(liveReceipt); });
            RegisterCleanup(PlaytestRunState.ResetForTests);

            Directory.CreateDirectory(Path.GetDirectoryName(orphanSentinel));
            File.WriteAllText(orphanSentinel, "");
            File.WriteAllText(liveSentinel, "");
            PlaytestRunState.Begin(liveRunId, DateTime.Now); // simulates a run genuinely still in flight

            PlaytestRunner.ReapOrphanedSentinels(); // the internal seam the static ctor delegates to

            Assert.IsFalse(File.Exists(orphanSentinel), "Orphan sentinel must be reaped");
            Assert.IsTrue(File.Exists(orphanReceipt), "Orphan must get a durable ABORTED receipt");
            StringAssert.Contains("ABORTED: domain reload", File.ReadAllText(orphanReceipt));

            Assert.IsTrue(File.Exists(liveSentinel), "A live run's sentinel must never be reaped");
            Assert.IsFalse(File.Exists(liveReceipt), "A live run must not get a bogus receipt written for it");
        }

        [Test]
        [BiomeWorkerOnly("Drives a real PlaytestRunner.Run() and reads its sentinel/receipt files.")]
        public async Task FinishRun_DeletesSentinel()
        {
            const string runId = "c05sent01";
            var sentinelPath = SentinelFullPath(runId);
            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(sentinelPath)) File.Delete(sentinelPath); });
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs, requiresPlayMode: false, runId: runId);

            Assert.IsTrue(File.Exists(sentinelPath),
                "Sentinel must exist synchronously once Run() starts a real run");

            await AwaitBoundedAsync(tcs);

            Assert.IsFalse(File.Exists(sentinelPath), "FinishRun() must delete the sentinel on normal completion");
        }
    }
}
