using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // What the trace of a match counts. Work is a count of the elements visited and the tokens scanned for a word, not a time, so that "linear in the line" is a number a test can hold on any
    // machine. The plan's bound is 12 * T + 100 for every form of the verb of a line of T tokens, however the words fall; the lines below are the shapes that could go quadratic, and they
    // measure about 1.0 * T, so the bound held here is 2 * T + 100: a scan that is done again for each group goes over it too.
    public class MatchTraceTests
    {
        private const int Tokens = 20000;

        private static string Repeat(string unit, int times) => string.Concat(Enumerable.Repeat(unit, times));

        // The work of every form of the verb of the line, one trace each, as the second pass of the reader runs them (the first pass has no trace and counts nothing).
        private static long WorkOf(string line)
        {
            var toks = PatternTokens.Split(line);
            return PlaytestGrammar.Forms.Where(f => f.Verb == toks[0].Text).Sum(f =>
            {
                var trace = new MatchTrace();
                PatternMatcher.Match(f.Elements, toks, line, trace);
                return trace.Work;
            });
        }

        // `scans`: the line has a text that is looked through for the token that ends it, so the work is at least the line; that lower bound keeps the count honest (a Work not counted is no linear Work).
        private static void AssertLinear(string line, bool scans = true)
        {
            var tokens = PatternTokens.Split(line).Count;
            var work = WorkOf(line);
            TestContext.WriteLine(line.Substring(0, System.Math.Min(line.Length, 30)) + "...: " + tokens + " tokens, work " + work);
            Assert.That(tokens, Is.GreaterThanOrEqualTo(Tokens));
            Assert.That(work, Is.InRange(scans ? tokens : 1, 2L * tokens + 100));
        }

        [Test]
        public void Work_OfATextThatTakesTheRestOfTheLine_IsLinear() => AssertLinear("LOG" + Repeat(" x", Tokens), scans: false);

        [Test]
        public void Work_OfALazyTextThatNothingEnds_AndAnOperatorNeverComes_IsLinear() => AssertLinear("ASSERT" + Repeat(" x", Tokens));

        [Test]
        public void Work_OfALazyTextThatTheEndOfTheLineEnds_IsLinear() => AssertLinear("ASSERT /x|C|f == v" + Repeat(" x", Tokens));

        [Test]
        public void Work_OfAChainOfFiveThousandAnds_IsLinear() => AssertLinear("WAIT_UNTIL a == 1" + Repeat(" AND b == 2", Tokens / 4));

        [Test]
        public void Work_OfAChainOfOrs_IsLinear() => AssertLinear("WAIT_UNTIL a == 1" + Repeat(" OR b == 2", Tokens / 3));

        [Test]
        public void Work_OfALazyTextThatAWordAfterAGroupNeverEnds_IsLinear() => AssertLinear("MCP cmd" + Repeat(" a", Tokens));

        [Test]
        public void Work_OfALazyTextInAGroupThatTheWordAfterItNeverEnds_IsLinear() => AssertLinear("MCP cmd" + Repeat(" a", Tokens) + " INTO");

        [Test]
        public void Work_OfALazyTextBeforeAGroupAndAWordThatNeverComes_IsLinear() => AssertLinear("ASSERT_CONSERVED SUM" + Repeat(" a + b", Tokens / 3));

        [Test]
        public void Work_OfALazyTextBeforeAGroup_IsLinear() => AssertLinear("MOVE_PATH 1,0,0" + Repeat(" > 5,0,0", Tokens / 2) + " TIMEOUT 8");

        [Test]
        public void Work_WithoutATrace_IsNotCounted_AndTheMatchIsTheSame()
        {
            var line = "ASSERT /x|C|f == 1 AS my label";
            var toks = PatternTokens.Split(line);
            var form = PlaytestGrammar.Forms.First(f => f.Construct == "PT_ASSERT");
            var trace = new MatchTrace();
            Assert.That(MatchShow.Bound(PatternMatcher.Match(form.Elements, toks, line, null)), Is.EqualTo(MatchShow.Bound(PatternMatcher.Match(form.Elements, toks, line, trace))));
            Assert.That(trace.Work, Is.GreaterThan(0));
        }

        [Test]
        public void Note_KeepsTheFurthestFailure_AndOfTwoAtOneTokenTheFirst()
        {
            var first = new PlaytestGrammar.Element(PlaytestGrammar.ElementKind.Slot, "first");
            var second = new PlaytestGrammar.Element(PlaytestGrammar.ElementKind.Slot, "second");
            var trace = new MatchTrace();
            trace.Note(2, first, null);
            trace.Note(2, second, "W");
            Assert.That(MatchShow.Failed(trace), Is.EqualTo("at 2 slot first"), "a tie keeps the first");
            trace.Note(1, second, null);
            Assert.That(trace.At, Is.EqualTo(2), "an earlier failure is not kept");
            trace.Note(3, null, "W");
            Assert.That(MatchShow.Failed(trace), Is.EqualTo("at 3 word W"), "a further one replaces the slot as well");
        }
    }
}
