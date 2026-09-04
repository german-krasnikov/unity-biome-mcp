// TDD -- TestRunStore.PruneOldRuns retention (count 50 / window 7d), R-17: a
// non-terminal run must never be reaped by retention regardless of age or rank.
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunStoreTests : UnityMcpTestBase
    {
        private TestRunStore CreateIsolatedStore()
        {
            var storeRoot = Path.Combine(Path.GetTempPath(),
                "unity-mcp-prune-store-" + Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
            });
            return new TestRunStore(storeRoot);
        }

        private static string RunsPath(TestRunStore store) =>
            Path.Combine(store.RootPath, "runs");

        private static void WriteTerminalRun(TestRunStore store, string runId, string createdUtc)
        {
            store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Terminal,
                outcome = TestRunProtocol.RunOutcome.Passed,
                created_utc = createdUtc,
                finished_utc = createdUtc,
                build_coherent = true,
            });
        }

        private static void WriteNonTerminalRun(
            TestRunStore store, string runId, string createdUtc, string lifecycle)
        {
            store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = lifecycle,
                created_utc = createdUtc,
                build_coherent = true,
            });
        }

        [Test]
        public void PruneOldRuns_MoreThanKeepCount_DeletesOldestFirst()
        {
            var store = CreateIsolatedStore();
            var now = DateTime.UtcNow;
            const int keepCount = 5;
            for (var i = 0; i < keepCount + 5; i++)
            {
                WriteTerminalRun(store, "run-order-" + i.ToString("D2"),
                    now.AddMinutes(-i).ToString("O"));
            }

            store.PruneOldRuns(keepCount: keepCount, keepWindow: TimeSpan.FromDays(365));

            for (var i = 0; i < keepCount; i++)
                Assert.That(store.TryReadRun("run-order-" + i.ToString("D2"), out _), Is.True,
                    "newest run " + i + " must survive retention");
            for (var i = keepCount; i < keepCount + 5; i++)
                Assert.That(store.TryReadRun("run-order-" + i.ToString("D2"), out _), Is.False,
                    "oldest-beyond-keepCount run " + i + " must be pruned");
            Assert.That(Directory.GetDirectories(RunsPath(store)).Length, Is.EqualTo(keepCount));
        }

        [Test]
        public void PruneOldRuns_RunsOlderThanWindow_AreDeletedEvenUnderKeepCount()
        {
            var store = CreateIsolatedStore();
            var oldCreatedUtc = DateTime.UtcNow.AddDays(-30).ToString("O");
            var runIds = new[] { "run-win-0", "run-win-1", "run-win-2" };
            foreach (var runId in runIds)
                WriteTerminalRun(store, runId, oldCreatedUtc);

            // keepCount stays at its generous default (50) -- only the window rule
            // should be able to prune these 3 runs.
            store.PruneOldRuns(keepWindow: TimeSpan.FromDays(7));

            foreach (var runId in runIds)
                Assert.That(store.TryReadRun(runId, out _), Is.False,
                    runId + " is older than the retention window and must be pruned " +
                    "even though it is far under the keep count");
            Assert.That(Directory.Exists(RunsPath(store)), Is.True);
            Assert.That(Directory.GetDirectories(RunsPath(store)).Length, Is.EqualTo(0));
        }

        [Test]
        public void PruneOldRuns_NeverDeletesNonTerminalRun()
        {
            var store = CreateIsolatedStore();
            var oldCreatedUtc = DateTime.UtcNow.AddDays(-30).ToString("O");
            WriteTerminalRun(store, "run-terminal-old", oldCreatedUtc);
            WriteNonTerminalRun(store, "run-live-old", oldCreatedUtc,
                TestRunProtocol.Lifecycle.Running);

            // Same call must both prune the terminal run (proves the guard isn't a
            // widened "never delete anything" no-op) and preserve the non-terminal
            // run (proves the guard isn't missing) -- R-17.
            store.PruneOldRuns(keepWindow: TimeSpan.FromDays(7));

            Assert.That(store.TryReadRun("run-terminal-old", out _), Is.False,
                "an old terminal run beyond the window must still be pruned");
            Assert.That(store.TryReadRun("run-live-old", out var survivor), Is.True,
                "a non-terminal run must never be reaped by retention, regardless of age");
            Assert.That(survivor.lifecycle, Is.EqualTo(TestRunProtocol.Lifecycle.Running));
        }
    }
}
