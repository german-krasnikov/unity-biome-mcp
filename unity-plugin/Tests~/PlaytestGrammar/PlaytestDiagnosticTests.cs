using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using Form = UnityMCP.Playtest.Core.PlaytestGrammar.Form;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;
using static UnityMCP.Playtest.Core.GrammarTests.SyntheticForms;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // What the reader says about a line that no form reads (uses.ts, `take`): the code, where, and a text. The code is PT_SYNTAX unless the failure is a value of the wrong type in a slot that has
    // a code of its own; the place is the token the furthest failure was at, the last token when the line ends early; the text names the form by its leading words. Written here on forms and traces
    // made by hand, so that the rule that goes wrong is the test that fails; PlaytestReaderTests has the same through the reader and the real table, with the texts the reader in JavaScript prints.
    public class PlaytestDiagnosticTests
    {
        private static Form Line(string construct, params Element[] elements) => new Form(construct, elements, new Element[0][], false);

        // PT_T: T <n:number CODE PT_NUMBER>
        private static readonly Form Number = Line("PT_T", W("T"), new Element(PlaytestGrammar.ElementKind.Slot, "n", SlotType.Number, null, "PT_NUMBER"));

        // PT_U: U <w:text>   (a slot with no code of its own)
        private static readonly Form Plain = Line("PT_U", W("U"), S("w"));

        // The diagnostic for a line of which the furthest failure is `at`, with the slot or the word that was due there.
        private static PlaytestDiagnostic Refused(Form form, string line, int at, Element slot = null, string word = null, int lineNo = 1, int index = 0)
        {
            var trace = new MatchTrace();
            trace.Note(at, slot, word);
            return PlaytestDiagnostic.Refused(form, trace, PatternTokens.Split(line), lineNo, index);
        }

        private static string Say(PlaytestDiagnostic d) => d.Code + "@" + d.Line + ":" + d.Col + " " + d.Text;

        [Test]
        public void Refused_ALineThatEndsEarly_IsSyntax_AtItsLastToken_WhateverSlotWasDue()
        {
            Assert.That(Say(Refused(Number, "T", 1, Number.Elements[1])), Is.EqualTo("PT_SYNTAX@1:1 T: the line ends early"));
            Assert.That(Say(Refused(Number, "  T", 1, Number.Elements[1])), Is.EqualTo("PT_SYNTAX@1:3 T: the line ends early"));
            Assert.That(Say(Refused(Plain, "U a", 2, null, "W")), Is.EqualTo("PT_SYNTAX@1:3 U: the line ends early"), "a word that was due, at the end of the line");
        }

        [Test]
        public void Refused_AValueOfTheWrongType_HasTheCodeOfItsSlot_AtThatToken_AndSaysWhatItIsNot()
        {
            Assert.That(Say(Refused(Number, "T abc", 1, Number.Elements[1])), Is.EqualTo("PT_NUMBER@1:3 T: \"abc\" is not a number (n)"));
            Assert.That(Say(Refused(Number, "  T abc", 1, Number.Elements[1])), Is.EqualTo("PT_NUMBER@1:5 T: \"abc\" is not a number (n)"));
        }

        [Test]
        public void Refused_AValueOfTheWrongType_InASlotWithNoCode_IsSyntax()
        {
            var one = Line("PT_V", W("V"), S("m", SlotType.Enum, "a|b"));
            Assert.That(Say(Refused(one, "V c", 1, one.Elements[1])), Is.EqualTo("PT_SYNTAX@1:3 V: \"c\" is not one of a|b (m)"));
        }

        // A text in quotes is no word for the grammar: `WAIT "abc"` is not a bad number but a syntax error, as the hand parser said it.
        [TestCase("T \"abc\"", "PT_SYNTAX@1:3 T: \"\"abc\"\" is not a number (n)")]
        [TestCase("T \"\"", "PT_SYNTAX@1:3 T: \"\"\"\" is not a number (n)")]
        [TestCase("T \"a b\"", "PT_SYNTAX@1:3 T: \"\"a b\"\" is not a number (n)")]
        [TestCase("T \"abc", "PT_NUMBER@1:3 T: \"\"abc\" is not a number (n)")]
        [TestCase("T abc\"", "PT_NUMBER@1:3 T: \"abc\"\" is not a number (n)")]
        [TestCase("T \"", "PT_NUMBER@1:3 T: \"\"\" is not a number (n)")]
        public void Refused_AQuotedText_IsNoBadNumber_ButOnlyWhenItIsQuotedWhole(string line, string said) => Assert.That(Say(Refused(Number, line, 1, Number.Elements[1])), Is.EqualTo(said));

        [Test]
        public void Refused_ATokenThatNothingWasDueFor_IsUnexpected_AndSyntax()
        {
            Assert.That(Say(Refused(Number, "T 1 2", 2)), Is.EqualTo("PT_SYNTAX@1:5 T: unexpected \"2\""));
            Assert.That(Say(Refused(Number, "T 1 2 3", 2)), Is.EqualTo("PT_SYNTAX@1:5 T: unexpected \"2\""), "the token where the failure is, not the last");
            Assert.That(Say(Refused(Plain, "U a", 1, null, "W")), Is.EqualTo("PT_SYNTAX@1:3 U: unexpected \"a\""), "a word was due and another token is there");
        }

        [Test]
        public void Refused_NamesTheForm_ByItsLeadingWords()
        {
            var two = Line("PT_TF", W("TRACE_FLOW"), W("FROM"), S("from"), W("TO"), S("to"));
            Assert.That(Say(Refused(two, "TRACE_FLOW FROM /a TO /b", 5, null, "FIELD")), Is.EqualTo("PT_SYNTAX@1:23 TRACE_FLOW FROM: the line ends early"));
            Assert.That(PlaytestDiagnostic.Kw(two), Is.EqualTo("TRACE_FLOW FROM"));
            Assert.That(PlaytestDiagnostic.Kw(Number), Is.EqualTo("T"));
            Assert.That(PlaytestDiagnostic.Kw(Line("PT_NOWORD", S("a"), W("B"))), Is.EqualTo("PT_NOWORD"), "a form that starts with no word is named by its construct");
        }

        [Test]
        public void Refused_HasTheConstructLineAndIndexItWasGiven()
        {
            var d = Refused(Number, "T abc", 1, Number.Elements[1], null, 17, 4);
            Assert.That(d.Construct, Is.EqualTo("PT_T"));
            Assert.That(d.Line, Is.EqualTo(17));
            Assert.That(d.Index, Is.EqualTo(4));
        }

        [Test]
        public void UnknownKeyword_IsAtTheFirstToken_AndHasNoConstruct()
        {
            var d = PlaytestDiagnostic.UnknownKeyword(PatternTokens.Split("  wait 1")[0], 2, 1);
            Assert.That(Say(d), Is.EqualTo("PT_UNKNOWN_KEYWORD@2:3 unknown keyword wait"));
            Assert.That(d.Construct, Is.Null);
            Assert.That(d.Index, Is.EqualTo(1));
        }

        [Test]
        public void MissingEnd_IsSyntax_AtTheHead_AndNamesTheBlock()
        {
            var block = new Form("PT_B", new[] { W("BLOCK"), W("NOW") }, new[] { new[] { W("ITEM") } }, true);
            var d = PlaytestDiagnostic.MissingEnd(block, PatternTokens.Split("  BLOCK NOW")[0], 7, 6);
            Assert.That(Say(d), Is.EqualTo("PT_SYNTAX@7:3 BLOCK NOW block is missing END"));
            Assert.That(d.Construct, Is.EqualTo("PT_B"));
            Assert.That(d.Index, Is.EqualTo(6));
        }

        [Test]
        public void TheDiagnostic_KeepsWhatItWasGiven()
        {
            var d = new PlaytestDiagnostic("PT_X", 3, 4, 2, "PT_Y", "text");
            Assert.That(d.Code, Is.EqualTo("PT_X"));
            Assert.That(d.Line, Is.EqualTo(3));
            Assert.That(d.Col, Is.EqualTo(4));
            Assert.That(d.Index, Is.EqualTo(2));
            Assert.That(d.Construct, Is.EqualTo("PT_Y"));
            Assert.That(d.Text, Is.EqualTo("text"));
        }
    }
}
