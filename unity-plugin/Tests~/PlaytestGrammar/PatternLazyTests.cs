using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;
using static UnityMCP.Playtest.Core.GrammarTests.SyntheticForms;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The end of a LAZY text that no word follows in its own list: before the first token that starts what follows it (pattern-lazy.ts). What follows it is the first word of each optional
    // group right after it, then what must come, a word or a slot; and when the text is in a group, what follows the group too. A form per rule, so that the rule that goes wrong is the test that
    // fails. The expected fates were printed by matchPattern of the reader in JavaScript (the options that READ uses) on the pattern text in the comment above each form.
    public class PatternLazyTests
    {
        // X <q:text LAZY> <op:match(==|!=|~)> <v:text LAZY>
        private static readonly Element[] Compare = { W("X"), Lazy("q"), S("op", SlotType.Match, "==|!=|~"), Lazy("v") };

        // X <q:text LAZY> [AT <n:number>] [BY <m:number>]
        private static readonly Element[] Tails = { W("X"), Lazy("q"), G(W("AT"), S("n", SlotType.Number)), G(W("BY"), S("m", SlotType.Number)) };

        // X <q:text LAZY> [A] B
        private static readonly Element[] GroupThenWord = { W("X"), Lazy("q"), G(W("A")), W("B") };

        // X <q:text LAZY> [<s:match(\d+)> Y] END
        private static readonly Element[] SlotGroup = { W("X"), Lazy("q"), G(S("s", SlotType.Match, "\\d+"), W("Y")), W("END") };

        // X [A <q:text LAZY> [B <n:number>]] C
        private static readonly Element[] InGroup = { W("X"), G(W("A"), Lazy("q"), G(W("B"), S("n", SlotType.Number))), W("C") };

        // X [A [B <q:text LAZY>]] [C] D
        private static readonly Element[] Deep = { W("X"), G(W("A"), G(W("B"), Lazy("q"))), G(W("C")), W("D") };

        // X [A <q:text LAZY>] [B <n:number>]
        private static readonly Element[] Outer = { W("X"), G(W("A"), Lazy("q")), G(W("B"), S("n", SlotType.Number)) };

        [TestCase("X a == b", "op===@4; q=a@2; v=b@7")]
        [TestCase("X a b == c d", "op===@6; q=a b@2; v=c d@9")]
        [TestCase("X a == b == c", "op===@4; q=a@2; v=b == c@7")]
        [TestCase("X a", "no match, at 2 slot op")]
        [TestCase("X a b c", "no match, at 4 slot op")]
        [TestCase("X a ==", "no match, at 3 slot v")]
        public void Match_ALazyTextBeforeASlot_EndsBeforeTheFirstTokenThatTheSlotTakes(string line, string fate) => Assert.That(Fate(Compare, line), Is.EqualTo(fate));

        // The slot is at least one token: the first token is the text, even where it is what would end it.
        [TestCase("X == == 1", "op===@5; q===@2; v=1@8")]
        [TestCase("X == == ==", "op===@5; q===@2; v===@8")]
        public void Match_TheFirstTokenOfALazyText_IsTheText_EvenWhenItIsWhatEndsIt(string line, string fate) => Assert.That(Fate(Compare, line), Is.EqualTo(fate));

        [TestCase("X AT AT 5", "n=5@8; q=AT@2")]
        public void Match_TheFirstTokenOfALazyText_IsTheText_EvenWhenItIsAWordOfAGroup(string line, string fate) => Assert.That(Fate(Tails, line), Is.EqualTo(fate));

        [TestCase("X a b AT 5 BY 6", "m=6@14; n=5@9; q=a b@2")]
        [TestCase("X a b BY 6", "m=6@9; q=a b@2")]
        [TestCase("X a b", "q=a b@2")]
        [TestCase("X AT 5", "q=AT 5@2")]
        [TestCase("X a AT", "no match, at 3 slot n")]
        [TestCase("X a AT x", "no match, at 3 slot n")]
        [TestCase("X a BY 6 AT 5", "no match, at 4")]
        [TestCase("X a b AT 5 AT 6", "no match, at 5 word BY")]
        public void Match_ALazyTextBeforeGroups_EndsBeforeTheFirstWordOfAnyOfThem(string line, string fate) => Assert.That(Fate(Tails, line), Is.EqualTo(fate));

        [TestCase("X a b A B", "q=a b@2; +A")]
        [TestCase("X a b B", "q=a b@2")]
        [TestCase("X a b", "no match, at 3 word B")]
        [TestCase("X a A", "no match, at 3 word B")]
        [TestCase("X B", "no match, at 2 word B")]
        [TestCase("X a B b B", "no match, at 3")]
        public void Match_ALazyTextBeforeAGroupAndAWord_EndsBeforeEitherOfThem_AndAWordMustCome(string line, string fate) => Assert.That(Fate(GroupThenWord, line), Is.EqualTo(fate));

        [TestCase("X a b 5 Y END", "q=a b 5 Y@2")]
        [TestCase("X a b END", "q=a b@2")]
        [TestCase("X a b", "no match, at 3 word END")]
        [TestCase("X a 5 Y", "no match, at 4 word END")]
        public void Match_AGroupThatStartsWithASlot_IsNoWordThatEndsALazyText(string line, string fate) => Assert.That(Fate(SlotGroup, line), Is.EqualTo(fate));

        // In a group the text stops at what follows the group too: that is the `after` of the group, and why `[AS <label LAZY> [TIMEOUT <n>]]` of ASSERT stops at TIMEOUT.
        [TestCase("X A p q B 5 C", "n=5@10; q=p q@4")]
        [TestCase("X A p q C", "q=p q@4")]
        [TestCase("X A p q B 5", "no match, at 6 word C")]
        [TestCase("X A p q B C", "no match, at 5 slot n")]
        [TestCase("X A B 5 C", "q=B 5@4")]
        [TestCase("X C", "")]
        public void Match_ALazyTextInAGroup_EndsBeforeWhatFollowsTheTextAndThenTheGroup(string line, string fate) => Assert.That(Fate(InGroup, line), Is.EqualTo(fate));

        [TestCase("X A B p q C D", "q=p q@6; +C")]
        [TestCase("X A B p q D", "q=p q@6")]
        [TestCase("X A B p q", "no match, at 5 word D")]
        [TestCase("X A B C D", "q=C@6")]
        [TestCase("X D", "")]
        public void Match_ALazyTextInAGroupInAGroup_EndsBeforeWhatFollowsBothGroups(string line, string fate) => Assert.That(Fate(Deep, line), Is.EqualTo(fate));

        [TestCase("X A p q B 5", "n=5@10; q=p q@4")]
        [TestCase("X A p q", "q=p q@4")]
        [TestCase("X A p q B", "no match, at 5 slot n")]
        [TestCase("X A B 5", "q=B 5@4")]
        [TestCase("X B 5", "n=5@4")]
        public void Match_ALazyTextInAGroup_EndsBeforeTheFirstWordOfTheGroupThatFollowsTheGroup(string line, string fate) => Assert.That(Fate(Outer, line), Is.EqualTo(fate));

        [Test]
        public void LazyEnd_IsTheIndexOfTheLastTokenOfTheText()
        {
            var toks = PatternTokens.Split("X a b == c");
            Assert.That(PatternLazy.LazyEnd(new[] { S("op", SlotType.Match, "==") }, toks, 1, null), Is.EqualTo(2));
            Assert.That(PatternLazy.LazyEnd(new[] { S("op", SlotType.Match, "==") }, PatternTokens.Split("X == == 1"), 1, null), Is.EqualTo(1), "the text is at least its first token");
        }

        [Test]
        public void LazyEnd_WithNothingThatMustCome_RunsToTheEndOfTheLine_AndNotesNoFailure()
        {
            var toks = PatternTokens.Split("X a b c");
            var trace = new MatchTrace();
            Assert.That(PatternLazy.LazyEnd(new[] { G(W("AT"), S("n")) }, toks, 1, trace), Is.EqualTo(3));
            Assert.That(PatternLazy.LazyEnd(new Element[0], toks, 1, trace), Is.EqualTo(3));
            Assert.That(trace.At, Is.EqualTo(-1));
        }

        [Test]
        public void LazyEnd_WhenAWordMustComeAndTheLineEnds_IsMinusOne_AndTheTraceHasTheWord()
        {
            var trace = new MatchTrace();
            Assert.That(PatternLazy.LazyEnd(new[] { W("INTO"), One("var") }, PatternTokens.Split("MCP a b c"), 1, trace), Is.EqualTo(-1));
            Assert.That(MatchShow.Failed(trace), Is.EqualTo("at 4 word INTO"));
        }

        [Test]
        public void LazyEnd_WhenASlotMustComeAndTheLineEnds_IsMinusOne_AndTheTraceHasTheSlot()
        {
            var trace = new MatchTrace();
            Assert.That(PatternLazy.LazyEnd(new[] { G(W("AT"), S("n")), S("op", SlotType.Match, "==") }, PatternTokens.Split("X a b"), 1, trace), Is.EqualTo(-1));
            Assert.That(MatchShow.Failed(trace), Is.EqualTo("at 3 slot op"));
        }

        [Test]
        public void LazyEnd_WithoutATrace_SaysTheSame()
        {
            var toks = PatternTokens.Split("X a b == c");
            Assert.That(PatternLazy.LazyEnd(new[] { S("op", SlotType.Match, "==") }, toks, 1, null), Is.EqualTo(2));
            Assert.That(PatternLazy.LazyEnd(new[] { W("INTO") }, toks, 1, null), Is.EqualTo(-1));
        }

        [Test]
        public void LazyEnd_AnElementKindItDoesNotKnow_IsRefusedLoudly() =>
            Assert.Throws<System.NotSupportedException>(() => PatternLazy.LazyEnd(new[] { new Element((PlaytestGrammar.ElementKind)9, "x") }, PatternTokens.Split("X a b"), 1, null));

        [Test]
        public void Ahead_IsTheRestOfTheListAndThenWhatFollowsTheGroup_WithoutACopyWhenThereIsNoRest()
        {
            var list = new[] { W("A"), W("B"), W("C") };
            var after = new[] { W("D") };
            Assert.That(PatternLazy.Ahead(list, 1, after), Is.EqualTo(new[] { list[1], list[2], after[0] }));
            Assert.That(PatternLazy.Ahead(list, 3, after), Is.SameAs(after));
            Assert.That(PatternLazy.Ahead(list, 1, new Element[0]), Is.EqualTo(new[] { list[1], list[2] }));
        }

        [Test]
        public void HoldsLazy_IsTrueForAGroupWithALazyTextInItOrInAGroupInIt_AndFalseOtherwise()
        {
            Assert.That(PatternLazy.HoldsLazy(G(W("A"), Lazy("q"))), Is.True);
            Assert.That(PatternLazy.HoldsLazy(G(W("A"), G(W("B"), G(Lazy("q"))))), Is.True);
            Assert.That(PatternLazy.HoldsLazy(G(W("A"), S("q"), G(W("B"), One("n")))), Is.False);
            Assert.That(PatternLazy.HoldsLazy(G()), Is.False);
        }
    }
}
