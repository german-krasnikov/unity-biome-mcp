using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using UnityMCP.Decisions.Tests.Oracle;
using UnityMCP.Editor.Decisions;

namespace UnityMCP.Decisions.Tests
{
    // The generated verdict decision `passes` (through PlaytestFacts, plane editor: the way CommandRouter.IsPlaytestSuccess calls it) against
    // the hand-written IsPlaytestSuccess of the parent commit (Oracle/HandPlaytestSuccess.cs, a verbatim copy) on the reports of
    // server/tests/golden/decisions/verdict_reports.txt: the reports of the tests of the project, the header "PLAYTEST: p/t" in six spellings
    // against thirteen neighbours and five counts, JSON ledgers of every failed / steps / teardown combination, receipts that are no ledger,
    // counts beyond a million. The same reports are the inputs of the golden of the Python plane.
    public sealed class PlaytestOracleTests
    {
        // one line per report: the format asked for ("-" for none), a tab, the report as a JSON string (or null)
        private static List<(string Format, string Report)> Reports() =>
            File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "rows", "verdict_reports.txt"))
                .Where(line => line.Length > 0 && line[0] != '#')
                .Select(line => line.Split('\t'))
                .Select(f => (f[0] == "-" ? null : f[0], JsonSerializer.Deserialize<string>(f[1])))
                .ToList();

        [Test]
        public void Passes_answers_as_IsPlaytestSuccess_did_for_every_report()
        {
            var reports = Reports();
            Assert.That(reports.Count, Is.GreaterThan(500), "the corpus is nearly empty: this test would pass without checking");
            var problems = reports
                .Select(r => (r, want: HandPlaytestSuccess.IsPlaytestSuccess(r.Report, r.Format), got: Editor.Decisions.Verdict.Policy.Passes(PlaytestFacts.Read(r.Report, r.Format))))
                .Where(x => x.got != x.want)
                .Select(x => $"format {x.r.Format ?? "none"}: {JsonSerializer.Serialize(x.r.Report)} is {x.got}, was {x.want}")
                .ToList();
            Assert.That(problems, Is.Empty, $"{problems.Count} of {reports.Count} reports differ:\n" + string.Join("\n", problems.Take(20)));
        }

        [Test]
        public void The_corpus_has_passes_and_failures_of_both_representations()
        {
            var results = Reports().Select(r => (r.Format, pass: HandPlaytestSuccess.IsPlaytestSuccess(r.Report, r.Format))).ToList();
            Assert.That(results.Count(x => x.pass), Is.GreaterThan(30));
            Assert.That(results.Count(x => !x.pass), Is.GreaterThan(300));
            Assert.That(results.Where(x => x.Format == "json").Select(x => x.pass).Distinct().Count(), Is.EqualTo(2));
            Assert.That(results.Where(x => x.Format == "text").Select(x => x.pass).Distinct().Count(), Is.EqualTo(2));
        }
    }
}
