using System;

namespace UnityMCP.Editor.Decisions
{
    /// <summary>
    /// The facts of the decision passes (dsl/decisions/verdict.contract) that this plane reads out of a playtest report, the way
    /// CommandRouter.IsPlaytestSuccess always read it. The rules are Verdict.Policy.Passes (dsl/decisions/verdict.logic); nothing here decides.
    /// A text report is read at its very start only: the PLAYTEST: header must begin the report, its count token is ended by a space and
    /// both counts must parse; the word ABORTED anywhere is a fact of its own (F7: fail closed). No failure marker is looked for in a text
    /// report (report.plane editor). A JSON report is the canonical receipt of B16: outer.teardown_ok, an aggregate `failed` that a missing
    /// or unreadable key reads as -1 (F7), and the `ok` of every step, which is the ledger fact and never re-derived from scanning the
    /// report text (the B17 bug: a step's source_file containing " OK" said pass). The Python plane reads the same report differently
    /// (the header on any line, marker words): the two extractors differ, the rules do not (owner question Q-V1).
    /// A count beyond the million of the fact domain is folded to 1/1 or 0/1, which answers the two questions the rules ask the same way.
    /// </summary>
    internal static class PlaytestFacts
    {
        private const int Domain = 1000000;

        /// <param name="format">"json" or "text": what the caller asked for. Anything else is sniffed: a report that opens with `{` is a ledger.</param>
        internal static Verdict.Policy.PassesInput Read(string report, string format)
        {
            var blank = string.IsNullOrEmpty(report);
            var effective = format == "json" || format == "text" ? format
                : !blank && report.TrimStart().StartsWith("{", StringComparison.Ordinal) ? "json" : "text";
            var ledger = effective == "json" && !blank ? ReadLedger(report) : new LedgerFacts();
            var text = effective == "text" && !blank ? ReadText(report) : TextFacts.None;
            return new Verdict.Policy.PassesInput(
                LedgerFailed: ledger.Failed, LedgerHasFailed: ledger.HasFailed, LedgerSteps: ledger.Steps, LedgerStepsNotOk: ledger.StepsNotOk,
                LedgerTeardownOk: ledger.TeardownOk, ReportBlank: blank, ReportFormat: effective, ReportPlane: "editor",
                TextHasAborted: text.HasAborted, TextHasBlocked: false, TextHasConsoleErr: false, TextHasError: false, TextHasFail: false,
                TextHasTimeout: false, TextHeader: text.Header, TextPassed: text.Passed, TextTotal: text.Total);
        }

        private struct LedgerFacts { public double Failed; public bool HasFailed; public double Steps; public double StepsNotOk; public bool TeardownOk; }

        private struct TextFacts
        {
            public bool HasAborted; public string Header; public double Passed; public double Total;
            public static TextFacts None => new TextFacts { Header = "none" };
        }

        // B16's canonical JSON receipt: "outer":{"teardown_ok":...} plus one {"ok":true/false,...} entry per step (PlaytestStepReceipt.ToJson).
        // F7: the aggregate `failed` is a cross-check (a receipt whose own count disagrees with its per-step entries is suspect); production
        // always emits it (BuildJsonReport), so a missing key only rejects a malformed or hand-crafted receipt.
        private static LedgerFacts ReadLedger(string report)
        {
            var steps = 0;
            var notOk = 0;
            var stepsArray = JsonHelper.ExtractArray(report, "steps");
            var pos = 0;
            string stepJson;
            while ((stepJson = JsonHelper.ExtractNextArrayObject(stepsArray, ref pos)) != null)
            {
                steps++;
                if (JsonHelper.ExtractString(stepJson, "ok") != "true") notOk++;
            }
            return new LedgerFacts
            {
                Failed = JsonHelper.ExtractInt(report, "failed", -1),
                HasFailed = JsonHelper.ExtractString(report, "failed") != null,
                Steps = Math.Min(steps, Domain),
                StepsNotOk = Math.Min(notOk, Domain),
                TeardownOk = JsonHelper.ExtractString(JsonHelper.ExtractObject(report, "outer"), "teardown_ok") == "true",
            };
        }

        // INV-005 / v1 section 41: the legacy text scan. Untouched by B17 -- deleting it is gated on Player/Wave-D parity.
        // F7: ABORTED is read before anything else can pass the report: BuildReport never emits it in the same report as the " OK" one-liner,
        // so it can only remove a false positive, never flip an already-correct pass.
        private static TextFacts ReadText(string report)
        {
            var text = new TextFacts { HasAborted = report.Contains("ABORTED"), Header = "none" };
            if (!report.StartsWith("PLAYTEST:", StringComparison.Ordinal)) return text;
            var firstLineEnd = report.IndexOf('\n');
            var firstLine = firstLineEnd >= 0 ? report.Substring(0, firstLineEnd) : report;
            var countStart = "PLAYTEST:".Length;
            while (countStart < firstLine.Length && char.IsWhiteSpace(firstLine[countStart]))
                countStart++;
            var countEnd = firstLine.IndexOf(' ', countStart);
            if (countEnd < 0) return text;
            var counts = firstLine.Substring(countStart, countEnd - countStart).Split('/');
            if (counts.Length != 2 || !int.TryParse(counts[0], out var passed) || !int.TryParse(counts[1], out var total)) return text;
            text.Header = "first";
            if (total <= 0) { text.Passed = 0; text.Total = 0; }
            else if (passed < 0 || passed > Domain || total > Domain) { text.Passed = passed == total ? 1 : 0; text.Total = 1; }
            else { text.Passed = passed; text.Total = total; }
            return text;
        }
    }
}
