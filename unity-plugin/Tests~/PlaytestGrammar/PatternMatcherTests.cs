using System;
using System.Linq;
using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;
using static UnityMCP.Playtest.Core.GrammarTests.SyntheticForms;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // How the front matches the tokens of a line against the elements of a form: a word, a slot, an optional group. The rules are those of matchPattern of the reader in
    // JavaScript (pattern.ts) in the mode that READ uses, where an optional group whose first word is the token has to match whole. Each rule has a form of its own here, so that a
    // rule that goes wrong is the test that fails; every case below was also run through matchPattern on the same pattern text. The forms of the real table: PatternMatcherTableTests.
    public class PatternMatcherTests
    {
        private static readonly Element[] Wait = { W("WAIT"), S("seconds", SlotType.Number) };
        private static readonly Element[] Log = { W("LOG"), G(S("message")) };
        private static readonly Element[] Move = { W("MOVE"), G(One("path")), W("TO"), One("to"), G(S("offset")) };
        private static readonly Element[] Timed = { W("X"), G(W("WAIT"), S("n", SlotType.Number)), S("rest") };
        private static readonly Element[] Sum = { W("X"), G(W("SUM")), S("terms") };
        private static readonly Element[] Between = { W("X"), S("q"), W("AT"), S("n", SlotType.Number) };
        private static readonly Element[] Greedy = { W("X"), G(S("a")), W("STOP") };
        private static readonly Element[] Nested = { W("X"), G(W("A"), G(W("B"), S("n", SlotType.Number))), W("C") };
        private static readonly Element[] Either = { W("X"), G(W("A")), W("B") };

        [Test]
        public void Match_AWordAndASlot_BindTheSlotWithItsColumn()
        {
            Assert.That(Bound(Wait, "WAIT 2.5"), Is.EqualTo("seconds=2.5@5"));
            Assert.That(Bound(Wait, "  WAIT   2.5"), Is.EqualTo("seconds=2.5@9"));
        }

        // PT-8/Q22: the one generated table in this assembly (playtest) now says CASEFOLD, so
        // PlaytestGrammar.CaseFold is true file-wide — a synthetic W("WAIT") folds case too, and
        // "wait 1" now matches in full instead of failing at the word.
        [Test]
        public void Match_AWordIsComparedCaseFolded() => Assert.That(Why(Wait, "wait 1"), Is.EqualTo("matched"));

        [Test]
        public void Match_ATokenTooMany_FailsAtThatToken() => Assert.That(Why(Wait, "WAIT 1 2"), Is.EqualTo("at 2"));

        [Test]
        public void Match_ALineThatEndsEarly_FailsWhereTheTokenIsDue() => Assert.That(Why(Wait, "WAIT"), Is.EqualTo("at 1 slot seconds"));

        [Test]
        public void Match_AValueOfTheWrongType_FailsAtItsSlot() => Assert.That(Why(Wait, "WAIT abc"), Is.EqualTo("at 1 slot seconds"));

        [Test]
        public void Match_ATextSlot_TakesTheRestOfTheLineAsWritten()
        {
            Assert.That(Bound(Log, "LOG a  b \"c d\""), Is.EqualTo("message=a  b \"c d\"@4"));
            Assert.That(Bound(Log, "LOG # UNPARSED: x"), Is.EqualTo("message=# UNPARSED: x@4"));
        }

        [Test]
        public void Match_AGroupWithNoTokenLeftForIt_IsLeftOut() => Assert.That(Bound(Log, "LOG"), Is.EqualTo(""));

        [Test]
        public void Match_AGroupThatStartsWithASlot_IsTriedFirst_AndLeftOutWhenTheRestFails()
        {
            Assert.That(Bound(Move, "MOVE TO 1,2,3"), Is.EqualTo("to=1,2,3@8"));
            Assert.That(Bound(Move, "MOVE Player TO @/E + (5,0,0)"), Is.EqualTo("offset=+ (5,0,0)@19; path=Player@5; to=@/E@15"));
            Assert.That(Bound(Move, "MOVE TO TO 1,0,0"), Is.EqualTo("path=TO@5; to=1,0,0@11"));
            Assert.That(Why(Move, "MOVE /Player"), Is.EqualTo("at 2 word TO"));
            Assert.That(Why(Move, "MOVE /Player TO"), Is.EqualTo("at 3 slot to"));
        }

        [Test]
        public void Match_AGroupWhoseFirstWordIsTheToken_MustMatchWhole_NotBeLeftOut()
        {
            Assert.That(Bound(Timed, "X WAIT 5 more"), Is.EqualTo("n=5@7; rest=more@9"));
            Assert.That(Bound(Timed, "X other"), Is.EqualTo("rest=other@2"));
            Assert.That(Bound(Timed, "X WAIT abc"), Is.EqualTo("no match"), "left out, `WAIT abc` would be the rest");
            Assert.That(Why(Timed, "X WAIT abc"), Is.EqualTo("at 2 slot n"));
        }

        [Test]
        public void Match_AGroupOfOnlyWords_SetsAFlagNamedByItsFirstWord()
        {
            Assert.That(Bound(Sum, "X SUM a b"), Is.EqualTo("terms=a b@6; +SUM"));
            Assert.That(Bound(Sum, "X a b"), Is.EqualTo("terms=a b@2"));
            Assert.That(Bound(Sum, "X SUM"), Is.EqualTo("no match"));
            Assert.That(Bound(new[] { W("X"), G(W("A"), W("B")), One("y") }, "X A B c"), Is.EqualTo("y=c@6; +A"));
            Assert.That(Bound(new[] { W("X"), G(W("A"), W("B")), One("y") }, "X A c"), Is.EqualTo("no match"));
        }

        [Test]
        public void Match_ATextThatAWordFollowsInItsList_EndsBeforeTheFirstToken_ThatIsThatWord()
        {
            Assert.That(Bound(Between, "X a b AT 5"), Is.EqualTo("n=5@9; q=a b@2"));
            Assert.That(Why(Between, "X AT 5"), Is.EqualTo("at 1 slot q"));
            Assert.That(Why(Between, "X a b"), Is.EqualTo("at 3 word AT"));
            Assert.That(Why(Between, "X a AT b AT 5"), Is.EqualTo("at 3 slot n"));
        }

        [Test]
        public void Match_AWordAfterAGroup_IsNotWhereTheTextInTheGroupStops()
        {
            Assert.That(Why(Greedy, "X foo STOP"), Is.EqualTo("at 3 word STOP"));
            Assert.That(Bound(Greedy, "X STOP"), Is.EqualTo(""));
        }

        [Test]
        public void Match_GroupsInsideGroups_CommitAndSkipTheSameWay()
        {
            Assert.That(Bound(Nested, "X A B 1 C"), Is.EqualTo("n=1@6"));
            Assert.That(Bound(Nested, "X A C"), Is.EqualTo(""));
            Assert.That(Bound(Nested, "X C"), Is.EqualTo(""));
            Assert.That(Bound(Nested, "X A B C"), Is.EqualTo("no match"));
        }

        [Test]
        public void Match_TheTraceKeepsTheFurthestFailure_AndOfTwoAtOneTokenTheFirst()
        {
            Assert.That(Why(Either, "X C"), Is.EqualTo("at 1 word A"));
            Assert.That(Why(Either, "X A C"), Is.EqualTo("at 2 word B"));
            Assert.That(Why(Move, "MOVE TO"), Is.EqualTo("at 2 word TO"));
        }

        [Test]
        public void Match_WithoutATrace_ReadsAsWithOne()
        {
            foreach (var (form, line) in new[] { (Move, "MOVE Player TO @/E + (5,0,0)"), (Timed, "X WAIT abc"), (Sum, "X SUM a"), (Between, "X a b AT 5"), (Wait, "WAIT") })
                Assert.That(MatchShow.Bound(Read(form, line, null)), Is.EqualTo(MatchShow.Bound(Read(form, line, new MatchTrace()))), line);
        }

        [Test]
        public void Match_NoElementsAndNoTokens_IsAMatchThatBindsNothing()
        {
            var bindings = PatternMatcher.Match(new Element[0], PatternTokens.Split(""), "", null);
            Assert.That(bindings, Is.Not.Null);
            Assert.That(bindings.Slots, Is.Empty);
            Assert.That(bindings.Flags, Is.Empty);
            Assert.That(Why(new Element[0], "a"), Is.EqualTo("at 0"));
        }

        // The work is a count, not a time: a text is taken whole, a word that ends a text is looked for once.
        [Test]
        public void Match_TheWorkOnALongLine_IsLinear()
        {
            var tail = new MatchTrace();
            Read(Log, "LOG" + string.Concat(Enumerable.Repeat(" x", 20000)), tail);
            var between = new MatchTrace();
            Read(Between, "X" + string.Concat(Enumerable.Repeat(" a", 20000)) + " AT 5", between);
            Assert.That(tail.Work, Is.InRange(1, 20));
            Assert.That(between.Work, Is.InRange(20000, 20050));
        }

        [Test]
        public void Match_ALazyTextWithAWordAfterIt_EndsAtThatWord_AsAPlainOneDoes() =>
            Assert.That(Bound(new[] { W("X"), Lazy("q"), W("AT"), S("n", SlotType.Number) }, "X a b AT 5"), Is.EqualTo("n=5@9; q=a b@2"));

        [Test]
        public void Match_AnElementKindItDoesNotKnow_IsRefusedLoudly() =>
            Assert.Throws<NotSupportedException>(() => Read(new[] { new Element((ElementKind)9, "x") }, "a"));
    }
}
