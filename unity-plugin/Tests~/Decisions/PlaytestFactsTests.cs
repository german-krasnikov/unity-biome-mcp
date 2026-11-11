using NUnit.Framework;
using UnityMCP.Editor.Decisions;

namespace UnityMCP.Decisions.Tests
{
    // What the editor plane reads out of a report (PlaytestFacts.Read), fact by fact. The decision over these facts is Verdict.Policy.Passes,
    // which RowsTests and PlaytestOracleTests cover; here the extractor alone, with the cases that tell this plane from the Python one (Q-V1).
    public sealed class PlaytestFactsTests
    {
        private static Editor.Decisions.Verdict.Policy.PassesInput Text(string report) => PlaytestFacts.Read(report, "text");

        [TestCase("PLAYTEST: 2/3 (1.0s)", 2, 3)]
        [TestCase("PLAYTEST:  7/7 OK", 7, 7)]
        [TestCase("PLAYTEST: 1/2 (1.0s)\n[2] FAIL x", 1, 2)]
        public void A_header_at_the_very_start_gives_its_counts(string report, int passed, int total)
        {
            var facts = Text(report);
            Assert.That((facts.TextHeader, facts.TextPassed, facts.TextTotal), Is.EqualTo(("first", (double)passed, (double)total)));
            Assert.That(facts.ReportPlane, Is.EqualTo("editor"));
        }

        [TestCase("⚡ warning\nPLAYTEST: 1/1 (1s)", Description = "a header on a later line is none here (the Python plane accepts it)")]
        [TestCase("playtest: 1/1 (1s)", Description = "the word is case sensitive")]
        [TestCase("PLAYTEST: 1/1", Description = "no space ends the count token")]
        [TestCase("PLAYTEST: one/two (1s)", Description = "counts that do not parse")]
        [TestCase("PLAYTEST: 1/2/3 (1s)", Description = "three parts")]
        [TestCase("PLAYTEST: 99999999999/99999999999 (1s)", Description = "counts beyond int")]
        public void Anything_else_is_no_header(string report) => Assert.That(Text(report).TextHeader, Is.EqualTo("none"));

        [Test]
        public void A_failure_marker_is_no_fact_of_this_plane_but_ABORTED_is()
        {
            var facts = Text("PLAYTEST: 1/1 (1s)\n[2] FAIL x\n[3] ERROR y\n[4] CONSOLE_ERR z\n[5] BLOCKED w\n[6] TIMEOUT v");
            Assert.That((facts.TextHasFail, facts.TextHasError, facts.TextHasConsoleErr, facts.TextHasBlocked, facts.TextHasTimeout, facts.TextHasAborted),
                Is.EqualTo((false, false, false, false, false, false)));
            Assert.That(Text("PLAYTEST: 1/1 (1s)\n[2] ABORTED x").TextHasAborted, Is.True);
        }

        [TestCase("PLAYTEST: 1000001/1000001 (1s)", 1, 1)]
        [TestCase("PLAYTEST: 5/1000001 (1s)", 0, 1)]
        [TestCase("PLAYTEST: 1000001/5 (1s)", 0, 1)]
        [TestCase("PLAYTEST: -3/-3 (1s)", 0, 0)]
        [TestCase("PLAYTEST: -3/4 (1s)", 0, 1)]
        public void Counts_beyond_the_fact_domain_fold_to_the_same_answers(string report, int passed, int total)
        {
            var facts = Text(report);
            Assert.That((facts.TextPassed, facts.TextTotal), Is.EqualTo(((double)passed, (double)total)));
        }

        [Test]
        public void A_ledger_gives_teardown_the_aggregate_and_the_steps()
        {
            var facts = PlaytestFacts.Read(
                "{\"failed\":0,\"outer\":{\"teardown_ok\":true},\"steps\":[{\"ok\":true},{\"ok\":false},{\"index\":2}]}", "json");
            Assert.That((facts.LedgerTeardownOk, facts.LedgerHasFailed, facts.LedgerFailed, facts.LedgerSteps, facts.LedgerStepsNotOk),
                Is.EqualTo((true, true, 0d, 3d, 2d)));
        }

        [Test]
        public void A_missing_aggregate_is_minus_one_and_not_there()
        {
            var facts = PlaytestFacts.Read("{\"outer\":{\"teardown_ok\":true},\"steps\":[{\"ok\":true}]}", "json");
            Assert.That((facts.LedgerHasFailed, facts.LedgerFailed), Is.EqualTo((false, -1d)));
        }

        [TestCase("{\"outer\":{\"teardown_ok\":true},\"failed\":0,\"steps\":[{\"ok\":true}]}", "json", "json")]
        [TestCase("{\"outer\":{\"teardown_ok\":true},\"failed\":0,\"steps\":[{\"ok\":true}]}", null, "json")]
        [TestCase("  {}", "other", "json")]
        [TestCase("PLAYTEST: 1/1 (1s)", null, "text")]
        [TestCase("{\"source_file\":\"x\"}", "text", "text")]
        public void The_format_is_what_the_caller_asked_for_else_what_the_report_opens_with(string report, string asked, string read)
            => Assert.That(PlaytestFacts.Read(report, asked).ReportFormat, Is.EqualTo(read));

        [TestCase(null)]
        [TestCase("")]
        public void No_report_is_blank(string report) => Assert.That(PlaytestFacts.Read(report, "text").ReportBlank, Is.True);
    }
}
