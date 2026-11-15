using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    // C05 — reload sentinel: fail-loud, no resume. A `.running` sentinel is written the moment a
    // run genuinely starts (past every early-return in Run()) and deleted by FinishRun() on
    // normal completion. A domain reload mid-run kills the run's TCS (MCPServer.OnBeforeReload)
    // with nothing left to resume, so the sentinel survives into the next domain; PlaytestRunner's
    // [InitializeOnLoad] static ctor (runs on every domain reload) then converts each orphan into
    // a durable ABORTED receipt instead of leaving the run's outcome silently missing. Kept out of
    // PlaytestRunner.cs itself (already at the file's practical size ceiling) per R-04 /
    // csharp-unity.md file-size convention.
    internal static partial class PlaytestRunner
    {
        /// <summary>Writes the `.running` sentinel for a run that is genuinely starting.
        /// Deleted by <see cref="DeleteSentinel"/> once FinishRun() completes normally.</summary>
        internal static void WriteSentinel(string runId)
        {
            var path = ProjectRelativePath(PlaytestReceiptStore.SentinelPath(runId));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "", new System.Text.UTF8Encoding(false));
        }

        /// <summary>Deletes the sentinel for a run that reached FinishRun() normally.</summary>
        internal static void DeleteSentinel(string runId)
        {
            var path = ProjectRelativePath(PlaytestReceiptStore.SentinelPath(runId));
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// Scans PlaytestReceiptStore.Root for `.running` sentinels left behind by a run this
        /// domain never got to close. Writes an ABORTED receipt for each orphan and removes its
        /// sentinel. Never touches the sentinel of a run PlaytestRunState still reports as
        /// Running — that guard is what makes this seam safe to call directly from a unit test
        /// (not only from the static ctor). Internal so a test can invoke it without driving a
        /// real domain reload.
        /// </summary>
        internal static void ReapOrphanedSentinels()
        {
            var root = ProjectRelativePath(PlaytestReceiptStore.Root);
            if (!Directory.Exists(root)) return;

            var liveRunId = PlaytestRunState.Current.Phase == PlaytestRunState.RunPhase.Running
                ? PlaytestRunState.Current.RunId
                : null;

            foreach (var sentinelPath in Directory.GetFiles(root, "*" + PlaytestReceiptStore.SentinelExtension))
            {
                var runId = Path.GetFileNameWithoutExtension(sentinelPath);
                if (runId == liveRunId) continue; // C05: never reap a run still genuinely in flight
                WriteAbortedReceipt(runId);
                File.Delete(sentinelPath);
            }
        }

        /// <summary>Durable ABORTED receipt for a run whose in-memory state died with the domain
        /// — no step ledger survives a killed TCS, so this is the honest minimum: zero steps, a
        /// failed teardown, and a text report a receipt reader can grep for.</summary>
        internal static void WriteAbortedReceipt(string runId)
        {
            var json = BuildJsonReport(runId, new List<PlaytestStepReceipt>(), passed: 0, failed: 0,
                elapsedSeconds: 0f, teardownOk: false, sceneClean: true, textReport: "ABORTED: domain reload");
            var path = ProjectRelativePath(PlaytestReceiptStore.ReceiptPath(runId));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
        }

        private static string ProjectRelativePath(string relativePath) =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
    }
}
