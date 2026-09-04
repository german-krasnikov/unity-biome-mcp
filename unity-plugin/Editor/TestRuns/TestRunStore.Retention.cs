using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Opportunistic durable-store retention. Not a scheduled job -- callers invoke
    /// this piggybacked on natural traffic (e.g. before dispatching a new run) so a
    /// long-lived Editor session doesn't accumulate unbounded run history on disk.
    /// </summary>
    public sealed partial class TestRunStore
    {
        private const int DefaultRunRetentionCount = 50;
        private static readonly TimeSpan DefaultRunRetentionWindow = TimeSpan.FromDays(7);

        /// <summary>
        /// Deletes terminal runs beyond the newest <paramref name="keepCount"/> (ranked
        /// by created_utc descending, ties broken by run_id -- the same order as
        /// <see cref="ListRunIds"/>) and any terminal run older than
        /// <paramref name="keepWindow"/>. Both rules skip a run whose lifecycle is not
        /// terminal: a still-active run must never be reaped by retention (R-17).
        /// A run with an unreadable or corrupt run.json is also left untouched rather
        /// than aborting the whole pass.
        /// </summary>
        public void PruneOldRuns(
            int keepCount = DefaultRunRetentionCount,
            TimeSpan? keepWindow = null)
        {
            var window = keepWindow ?? DefaultRunRetentionWindow;
            var runsPath = Path.Combine(RootPath, "runs");
            if (!Directory.Exists(runsPath)) return;

            lock (FileGate)
            {
                var nowUtc = DateTime.UtcNow.ToString("O");
                var terminalRuns = new List<KeyValuePair<string, string>>();
                foreach (var directory in Directory.GetDirectories(runsPath))
                {
                    var runId = Path.GetFileName(directory);
                    try
                    {
                        ValidateIdentity(runId, nameof(runId));
                        if (!TryReadRun(runId, out var run) || !string.Equals(
                                run.lifecycle, TestRunProtocol.Lifecycle.Terminal,
                                StringComparison.Ordinal))
                            continue; // non-terminal or unreadable -- never touched
                        terminalRuns.Add(new KeyValuePair<string, string>(
                            runId, run.created_utc ?? ""));
                    }
                    catch (ArgumentException)
                    {
                        // Unsafe directory name -- not addressable, leave it alone.
                    }
                    catch (TestRunStoreException)
                    {
                        // Corrupt run.json -- opportunistic pruning skips it rather
                        // than aborting the whole pass.
                    }
                }

                var newestFirst = terminalRuns
                    .OrderByDescending(run => run.Value, StringComparer.Ordinal)
                    .ThenBy(run => run.Key, StringComparer.Ordinal)
                    .ToArray();

                for (var rank = 0; rank < newestFirst.Length; rank++)
                {
                    var runId = newestFirst[rank].Key;
                    var createdUtc = newestFirst[rank].Value;
                    var beyondKeepCount = rank >= keepCount;
                    var beyondWindow = TestRunProtocol.ElapsedSeconds(createdUtc, nowUtc) >
                        window.TotalSeconds;
                    if (beyondKeepCount || beyondWindow)
                        Directory.Delete(GetRunDirectory(runId), true);
                }
            }
        }
    }
}
