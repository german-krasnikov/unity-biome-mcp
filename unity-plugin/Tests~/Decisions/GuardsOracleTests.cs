using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Decisions.Tests.Oracle;
using UnityMCP.Editor.Decisions;

namespace UnityMCP.Decisions.Tests
{
    // The generated guardVerdict (through GuardFacts, the way CheckGuards and BatchHelper call it) against the hand-written guards of the
    // parent commit (Oracle/HandGuards.cs) over every combination of the facts they read: 2^12 for the `top` site, 2^11 for the `batch` site.
    public sealed class GuardsOracleTests
    {
        private static IEnumerable<bool[]> Combinations(int facts) =>
            Enumerable.Range(0, 1 << facts).Select(bits => Enumerable.Range(0, facts).Select(bit => (bits >> bit & 1) == 1).ToArray());

        [Test]
        public void Top_answers_as_CheckGuards_did_for_every_combination_of_its_facts()
        {
            var problems = new List<string>();
            var count = 0;
            foreach (var f in Combinations(12))
            {
                count++;
                var want = HandGuards.Top(ready: f[0], pythonOnly: f[1], authDenied: f[2], compiling: f[3], allowedDuringCompile: f[4], playMode: f[5],
                    mutating: f[6], allowedInPlay: f[7], runtime: f[8], readOnly: f[9], alwaysAllowed: f[10], toolEnabled: f[11]);
                var got = Editor.Decisions.Guards.Policy.GuardVerdict(GuardFacts.Top(ready: f[0], pythonOnly: f[1], authDenied: f[2], compiling: f[3],
                    allowedDuringCompile: f[4], playMode: f[5], mutating: f[6], allowedInPlay: f[7], runtime: f[8], readOnly: f[9], alwaysAllowed: f[10],
                    toolEnabled: f[11]));
                if (got != want) problems.Add($"{string.Join("", f.Select(b => b ? 1 : 0))}: {got}, was {want}");
            }
            Assert.That(count, Is.EqualTo(4096));
            Assert.That(problems, Is.Empty, $"{problems.Count} of {count} combinations differ:\n" + string.Join("\n", problems.Take(20)));
        }

        [Test]
        public void Batch_answers_as_the_per_line_guards_of_BatchHelper_did_for_every_combination_of_its_facts()
        {
            var problems = new List<string>();
            var count = 0;
            foreach (var f in Combinations(11))
            {
                count++;
                var want = HandGuards.Batch(ready: f[0], batchable: f[1], compiling: f[2], allowedDuringCompile: f[3], playMode: f[4], mutating: f[5],
                    allowedInPlay: f[6], runtime: f[7], readOnly: f[8], alwaysAllowed: f[9], toolEnabled: f[10]);
                var got = Editor.Decisions.Guards.Policy.GuardVerdict(GuardFacts.Batch(ready: f[0], batchable: f[1], compiling: f[2],
                    allowedDuringCompile: f[3], playMode: f[4], mutating: f[5], allowedInPlay: f[6], runtime: f[7], readOnly: f[8],
                    alwaysAllowed: f[9], toolEnabled: f[10]));
                if (got != want) problems.Add($"{string.Join("", f.Select(b => b ? 1 : 0))}: {got}, was {want}");
            }
            Assert.That(count, Is.EqualTo(2048));
            Assert.That(problems, Is.Empty, $"{problems.Count} of {count} combinations differ:\n" + string.Join("\n", problems.Take(20)));
        }

        [Test]
        public void The_oracle_is_not_vacuous_it_stops_calls_for_every_reason_of_each_site()
        {
            var top = Combinations(12).Select(f => HandGuards.Top(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10], f[11])).Distinct().OrderBy(x => x);
            var batch = Combinations(11).Select(f => HandGuards.Batch(f[0], f[1], f[2], f[3], f[4], f[5], f[6], f[7], f[8], f[9], f[10])).Distinct().OrderBy(x => x);
            Assert.That(top, Is.EqualTo(new[] { "authDenied", "compiling", "disabled", "initializing", "notInPlay", "ok", "playBlocked", "pythonOnly", "readOnlyBlocked" }));
            Assert.That(batch, Is.EqualTo(new[] { "asyncOnly", "compiling", "disabled", "initializing", "notInPlay", "ok", "playBlocked", "readOnlyBlocked" }));
        }
    }
}
