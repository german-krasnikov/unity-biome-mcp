using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // How a .playtest line is cut for the table of the grammar: into what the JavaScript expression /"[^"]*"|\S+/g finds, which is how the reader of the SURFACE
    // cuts a line (TOKENS words). The expectations were taken from that expression, not from PatternTokens. A character outside ASCII is written as a code point,
    // so that no tool or editor can turn it into the blank itself.
    public class PatternTokensTests
    {
        // The 25 UTF-16 code units that /\s/ matches in JavaScript, found by trying all 65536 of them (node 24, V8).
        private static readonly int[] JavaScriptBlanks =
        {
            0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x0020, 0x00A0, 0x1680,
            0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200A,
            0x2028, 0x2029, 0x202F, 0x205F, 0x3000, 0xFEFF,
        };

        private static string Chr(int code) => ((char)code).ToString();

        // The tokens as text@column: the form the expectations are written in.
        private static string[] Cut(string line) => PatternTokens.Split(line).Select(t => t.Text + "@" + t.Col).ToArray();

        [Test]
        public void Split_WordsOnBlanks_AreTokensWithTheirColumns() =>
            Assert.That(Cut("WAIT   2.5"), Is.EqualTo(new[] { "WAIT@0", "2.5@7" }));

        [Test]
        public void Split_TheColumnIsInThePhysicalLine_SoTheIndentCounts() =>
            Assert.That(Cut("    LOG x"), Is.EqualTo(new[] { "LOG@4", "x@8" }));

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(" \t\r\n")]
        public void Split_NothingButBlanks_IsNoToken(string line) => Assert.That(Cut(line), Is.Empty);

        [Test]
        public void Split_NoLine_IsNoToken_AndDoesNotThrow() => Assert.That(PatternTokens.Split(null), Is.Empty);

        [Test]
        public void Split_ATextInQuotes_IsOneTokenWithItsQuotesAndItsBlanks()
        {
            Assert.That(Cut("say \"a b\" c"), Is.EqualTo(new[] { "say@0", "\"a b\"@4", "c@10" }));
            Assert.That(Cut("INVOKE \"\" C M"), Is.EqualTo(new[] { "INVOKE@0", "\"\"@7", "C@10", "M@12" }));
        }

        [Test]
        public void Split_AQuoteWithoutAClosingQuote_IsPartOfAWord()
        {
            Assert.That(Cut("LOG \"a b"), Is.EqualTo(new[] { "LOG@0", "\"a@4", "b@7" }));
            Assert.That(Cut("LOG \"a\" \"b"), Is.EqualTo(new[] { "LOG@0", "\"a\"@4", "\"b@8" }));
            Assert.That(Cut("x \""), Is.EqualTo(new[] { "x@0", "\"@2" }));
            Assert.That(Cut("\""), Is.EqualTo(new[] { "\"@0" }));
        }

        [Test]
        public void Split_AQuoteInsideAWord_OpensNoText() =>
            Assert.That(Cut("a\"b c\"d"), Is.EqualTo(new[] { "a\"b@0", "c\"d@4" }));

        [Test]
        public void Split_ATextEndsAtItsClosingQuote_EvenWithoutABlankAfterIt()
        {
            Assert.That(Cut("\"a b\"c"), Is.EqualTo(new[] { "\"a b\"@0", "c@5" }));
            Assert.That(Cut("\"a\"\"b\""), Is.EqualTo(new[] { "\"a\"@0", "\"b\"@3" }));
        }

        // Keeping a [..] of a path whole is a rule of the slot that reads it (its Match expression), not of the tokenizer: here [..] is cut at its blanks like any word.
        [Test]
        public void Split_SquareBrackets_AreNotProtected() =>
            Assert.That(Cut("ASSERT /[Box contains Items]/Slot|C|f == 1"),
                Is.EqualTo(new[] { "ASSERT@0", "/[Box@7", "contains@13", "Items]/Slot|C|f@22", "==@38", "1@41" }));

        [Test]
        public void Split_AHashInALine_IsAWordLikeAnyOther() =>
            Assert.That(Cut("LOG # UNPARSED: x"), Is.EqualTo(new[] { "LOG@0", "#@4", "UNPARSED:@6", "x@16" }));

        [Test]
        public void Split_ABlankIsExactlyWhatJavaScriptCallsWhiteSpace()
        {
            var wrong = new List<string>();
            for (var code = 0; code <= char.MaxValue; code++)
                if (PatternTokens.Split("a" + (char)code + "b").Count != (JavaScriptBlanks.Contains(code) ? 2 : 1))
                    wrong.Add("U+" + code.ToString("X4"));
            Assert.That(wrong, Is.Empty);
        }

        [Test]
        public void Split_ANoBreakSpace_AndTheByteOrderMark_AreBlanks()
        {
            Assert.That(Cut("a" + Chr(0x00A0) + "b"), Is.EqualTo(new[] { "a@0", "b@2" }));
            Assert.That(Cut("a" + Chr(0xFEFF) + "b"), Is.EqualTo(new[] { "a@0", "b@2" }));
            Assert.That(Cut(Chr(0xFEFF) + "WAIT 1"), Is.EqualTo(new[] { "WAIT@1", "1@6" }));
        }

        [Test]
        public void Split_ATabAndACarriageReturn_AreBlanks()
        {
            Assert.That(Cut("a\tb"), Is.EqualTo(new[] { "a@0", "b@2" }));
            Assert.That(Cut("WAIT 1\r"), Is.EqualTo(new[] { "WAIT@0", "1@5" }));
        }

        // char.IsWhiteSpace says yes for U+0085 and JavaScript says no: one line must give the same tokens in both readers.
        [Test]
        public void Split_TheNextLineControl_IsNotABlank() =>
            Assert.That(Cut("a" + Chr(0x0085) + "b"), Is.EqualTo(new[] { "a" + Chr(0x0085) + "b@0" }));

        [Test]
        public void Split_ABlankInsideQuotes_StaysInTheText() =>
            Assert.That(Cut("\"" + Chr(0x00A0) + "\" x"), Is.EqualTo(new[] { "\"" + Chr(0x00A0) + "\"@0", "x@4" }));

        // JavaScript counts UTF-16 code units, so a character outside the BMP takes two columns.
        [Test]
        public void Split_AColumnCountsUtf16CodeUnits() =>
            Assert.That(Cut(char.ConvertFromUtf32(0x1F600) + " a"), Is.EqualTo(new[] { char.ConvertFromUtf32(0x1F600) + "@0", "a@3" }));

        // A quote that finds no closing quote means that there is none left in the line, so the search is made once; the ceiling is far over a linear pass and far under a quadratic one.
        [Test]
        public void Split_LongLinesOfQuotes_AreCutInLinearTime()
        {
            var watch = Stopwatch.StartNew();
            var quotes = PatternTokens.Split(new string('"', 400001));
            var opens = PatternTokens.Split(string.Concat(Enumerable.Repeat("\"a ", 200000)));
            var word = PatternTokens.Split(new string('a', 2000000));
            watch.Stop();
            Assert.That((quotes.Count, quotes[quotes.Count - 1].Col), Is.EqualTo((200001, 400000)));
            Assert.That((opens.Count, opens[opens.Count - 1].Col), Is.EqualTo((200000, 599998)));
            Assert.That(word.Count, Is.EqualTo(1));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000));
        }
    }
}
