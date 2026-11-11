using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The requirement examples of the repository (dsl/constructs/playtest*.examples), which the reader in JavaScript is held to by `biome verify` and by dsl/playtest/compare.mjs, and this reader is
    // held to by the same files: each EXAMPLE is a script (its SOURCE lines, numbered from 1), and what the reader says about it is the multiset of its EXPECT lines, as `PT_SYNTAX@1:23`
    // (DECLARE_ is PT_, " at " is @). A script with no EXPECT is read with nothing said. The host's part of a script (the lines that go on after SWEEP_PATH) is not played here: the
    // reader says the same with and without it.
    public class PlaytestReaderExamplesTests
    {
        private static readonly Regex Expectation = new Regex(@"^diagnostic DECLARE_([A-Z_]+) at (\d+):(\d+)$", RegexOptions.CultureInvariant);

        // "diagnostic DECLARE_SYNTAX at 1:23" -> "PT_SYNTAX@1:23"; a line that is anything else fails the test, so that no EXPECT is passed over.
        private static string Expected(ExampleBlock block, string expect)
        {
            var m = Expectation.Match(expect);
            if (!m.Success) throw new InvalidOperationException(block.FileName + ": \"" + block.Title.Substring(0, Math.Min(60, block.Title.Length)) + "\": not an EXPECT diagnostic line: " + expect);
            return "PT_" + m.Groups[1].Value + "@" + m.Groups[2].Value + ":" + m.Groups[3].Value;
        }

        [Test]
        public void Read_EveryExample_SaysWhatItsExpectLinesSay_AtTheirPlaces_AndReadsEveryConstruct()
        {
            var blocks = ExamplesFile.ReadAll();
            var wrong = new List<string>();
            var covered = new HashSet<string>();
            int expectations = 0, uses = 0;
            foreach (var block in blocks)
            {
                var result = ReaderHarness.Read(block.Source.ToArray());
                var got = result.Diagnostics.Select(ReaderHarness.Place).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var expected = block.Expect.Select(e => Expected(block, e)).OrderBy(x => x, StringComparer.Ordinal).ToList();
                expectations += expected.Count;
                uses += result.Uses.Count;
                foreach (var use in result.Uses) covered.Add(use.Form.Construct);
                if (!got.SequenceEqual(expected))
                    wrong.Add(block.FileName + ": " + block.Title.Substring(0, Math.Min(70, block.Title.Length)) + "\n    read:     " + string.Join(", ", got) + "\n    expected: " + string.Join(", ", expected));
            }
            TestContext.WriteLine("examples " + blocks.Count + ", expectations " + expectations + ", uses " + uses + ", constructs read " + covered.Count + "/" + PlaytestGrammar.Forms.Count);
            Assert.That(wrong, Is.Empty);
            Assert.That(blocks.Count, Is.GreaterThanOrEqualTo(66), "a check over fewer examples than the repository had is no check");
            Assert.That(expectations, Is.GreaterThanOrEqualTo(217));
            Assert.That(PlaytestGrammar.Forms.Select(f => f.Construct).Except(covered), Is.Empty, "a construct that no SOURCE line of the examples is read as has no positive example");
        }

        // The examples that expect nothing are the positive ones: no diagnostic, and every line that is no blank and no comment is in a use (a block covers its head, its lines and its END).
        [Test]
        public void Read_EveryExampleThatExpectsNothing_ReadsEveryLineOfItIntoAUse()
        {
            var unread = new List<string>();
            var commands = 0;
            foreach (var block in ExamplesFile.ReadAll().Where(b => b.Expect.Count == 0))
            {
                var result = ReaderHarness.Read(block.Source.ToArray());
                var inAUse = new bool[block.Source.Count];
                foreach (var use in result.Uses)
                    for (var k = use.Index; k <= use.EndIndex; k++) inAUse[k] = true;
                for (var k = 0; k < inAUse.Length; k++)
                {
                    var text = block.Source[k].Trim();
                    if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal)) continue;
                    commands++;
                    if (!inAUse[k]) unread.Add(block.FileName + ":" + (k + 1) + ": " + text);
                }
                if (result.Diagnostics.Count > 0) unread.Add(block.FileName + ": " + block.Title.Substring(0, Math.Min(60, block.Title.Length)) + " says " + string.Join(", ", result.Diagnostics.Select(ReaderHarness.Place)));
            }
            TestContext.WriteLine("lines in the examples that expect nothing: " + commands);
            Assert.That(unread, Is.Empty);
            Assert.That(commands, Is.GreaterThan(200));
        }
    }
}
