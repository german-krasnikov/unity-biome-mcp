using System;
using UnityMCP.Editor;

namespace UnityMCP.Decisions.Tests.Oracle
{
    // CommandRouter.IsPlaytestSuccess and its two helpers as they stood at c30c356b (unity-plugin/Editor/CommandRouter.cs), before step 7.2 V2
    // moved their rules into dsl/decisions/verdict.logic: the text between the markers, byte for byte (the only edit is `internal` for
    // `private` on IsPlaytestSuccess, which a test calls). An oracle, not a copy to maintain: it is never edited to follow the generated
    // policy. JsonHelper is the real one, compiled from Editor/JsonHelper.cs.
    internal static class HandPlaytestSuccess
    {
        // B17: the caller (AsyncRunPlaytest, above) already knows which representation it
        // requested — format is passed explicitly (R-07 explicit-over-implicit) rather than
        // re-derived here. The `{`-sniff below survives only as a fallback for a legacy/unknown
        // format argument; it is never reached from the one production call site, which always
        // resolves format to "text" or "json" before calling in (line ~493).
        internal static bool IsPlaytestSuccess(string report, string format)
        {
            if (string.IsNullOrEmpty(report)) return false;
            if (format == "json") return IsPlaytestSuccessFromLedger(report);
            if (format == "text") return IsPlaytestSuccessFromText(report);
            return report.TrimStart().StartsWith("{", StringComparison.Ordinal)
                ? IsPlaytestSuccessFromLedger(report)
                : IsPlaytestSuccessFromText(report);
        }

        // B16's canonical JSON receipt: "outer":{"teardown_ok":...} plus one
        // {"ok":true/false,...} entry per step (PlaytestStepReceipt.ToJson). A step's `ok` is the
        // ledger fact — never re-derived from scanning report text (that was the B17 bug: a
        // step's source_file containing " OK" made the legacy substring check say "pass").
        private static bool IsPlaytestSuccessFromLedger(string report)
        {
            var outer = JsonHelper.ExtractObject(report, "outer");
            if (JsonHelper.ExtractString(outer, "teardown_ok") != "true") return false;
            // F7: aggregate cross-check — a receipt whose own `failed` count disagrees with
            // its per-step ok:true entries is definitionally suspect. Missing key defaults to
            // -1 (fail-closed): production always emits `failed` (BuildJsonReport), so this
            // only rejects a malformed/hand-crafted receipt.
            if (JsonHelper.ExtractInt(report, "failed", -1) != 0) return false;

            var stepsArray = JsonHelper.ExtractArray(report, "steps");
            var pos = 0;
            string stepJson;
            var stepCount = 0;
            while ((stepJson = JsonHelper.ExtractNextArrayObject(stepsArray, ref pos)) != null)
            {
                stepCount++;
                if (JsonHelper.ExtractString(stepJson, "ok") != "true") return false;
            }
            return stepCount > 0; // F7: an empty ledger is not a pass
        }

        // INV-005 / v1 §41: the legacy text scan. Untouched by B17 — deleting it is gated on
        // Player/Wave-D parity, not this item.
        private static bool IsPlaytestSuccessFromText(string report)
        {
            if (string.IsNullOrEmpty(report)) return false;
            // F7: fail-closed on an abort marker before either shortcut below — BuildReport
            // never emits "ABORTED" in the same report as the " OK" one-liner, so this can only
            // remove a false positive, never flip an already-correct pass to a false negative.
            if (report.Contains("ABORTED")) return false;
            if (!report.StartsWith("PLAYTEST:", StringComparison.Ordinal)) return false;

            var firstLineEnd = report.IndexOf('\n');
            var firstLine = firstLineEnd >= 0 ? report.Substring(0, firstLineEnd) : report;
            var countStart = "PLAYTEST:".Length;
            while (countStart < firstLine.Length && char.IsWhiteSpace(firstLine[countStart]))
                countStart++;
            var countEnd = firstLine.IndexOf(' ', countStart);
            if (countEnd < 0) return false;

            var counts = firstLine.Substring(countStart, countEnd - countStart).Split('/');
            if (counts.Length != 2) return false;
            return int.TryParse(counts[0], out var passed)
                && int.TryParse(counts[1], out var total)
                && total > 0
                && passed == total;
        }
    }
}
