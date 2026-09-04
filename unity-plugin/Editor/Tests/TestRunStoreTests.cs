// TDD -- TestRunStore.PruneOldRuns retention (count 50 / window 7d), R-17: a
// non-terminal run must never be reaped by retention regardless of age or rank.
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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

        [Test]
        public void PruneOldRuns_OneUndeletableRun_StillPrunesTheOthers()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Assert.Ignore("chmod-based undeletable-directory reproduction requires a POSIX filesystem.");
                return;
            }

            var store = CreateIsolatedStore();
            var now = DateTime.UtcNow;
            // Both are beyond the 7d window; the victim is ranked ahead of the
            // survivor (newer created_utc sorts first) so an unguarded exception
            // deleting the victim would abort the pass before the survivor is
            // ever reached -- proving this test double-reds without the fix.
            WriteTerminalRun(store, "run-victim", now.AddDays(-30).ToString("O"));
            WriteTerminalRun(store, "run-survivor", now.AddDays(-31).ToString("O"));
            var victimDir = Path.Combine(RunsPath(store), "run-victim");
            MakeDirectoryUndeletable(victimDir);
            RegisterCleanup(() => RestoreDirectoryDeletable(victimDir));
            LogAssert.Expect(LogType.Warning, new Regex("PruneOldRuns could not delete run 'run-victim'"));

            store.PruneOldRuns(keepWindow: TimeSpan.FromDays(7));

            Assert.That(store.TryReadRun("run-survivor", out _), Is.False,
                "the survivor is also beyond the window and must still be pruned " +
                "even though an earlier-ranked run failed to delete");
            Assert.That(Directory.Exists(victimDir), Is.True,
                "the undeletable run must remain on disk rather than corrupt/vanish");
        }

        private static void MakeDirectoryUndeletable(string path) => RunChmod(path, "555");

        private static void RestoreDirectoryDeletable(string path) => RunChmod(path, "755");

        private static void RunChmod(string path, string mode)
        {
            using (var proc = Process.Start(new ProcessStartInfo("/bin/chmod", $"{mode} \"{path}\"")
                   {
                       UseShellExecute = false
                   }))
            {
                proc?.WaitForExit();
                Assert.That(proc, Is.Not.Null, "/bin/chmod failed to start");
                Assert.That(proc.ExitCode, Is.Zero, "/bin/chmod exited non-zero");
            }
        }
    }
}
