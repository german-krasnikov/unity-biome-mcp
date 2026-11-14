using System;
using NUnit.Framework;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using ElementKind = UnityMCP.Playtest.Core.PlaytestGrammar.ElementKind;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The front of the reader implements one reading of the SURFACE line of dsl/constructs/playtest.construct and the slot types of the table that it knows today.
    // When the line or the table moves, these go red first: a front that reads a table it does not understand would accept or refuse lines in silence.
    public class ReaderOptionsTests
    {
        [Test]
        public void TheSurfaceOfTheTable_IsTheOneTheFrontImplements()
        {
            Assert.That(PlaytestGrammar.Ordered, Is.True, "the first form that reads a line wins");
            Assert.That(PlaytestGrammar.TokensWords, Is.True, "a token is a run of non-blanks, or a text in quotes");
            Assert.That(PlaytestGrammar.CommentLine, Is.True, "a line that starts with # is a comment, and a # inside a line is text");
        }

        [Test]
        public void EverySlotTypeOfTheTable_IsHandledByTheSlotCheck()
        {
            foreach (SlotType type in Enum.GetValues(typeof(SlotType)))
            {
                var slot = new Element(ElementKind.Slot, "x", type, type == SlotType.Match ? "\\S+" : "a|b");
                Assert.DoesNotThrow(() => SlotCheck.Accepts(slot, "a"), type.ToString());
                Assert.DoesNotThrow(() => SlotCheck.Describe(slot), type.ToString());
            }
        }

        [Test]
        public void ASlotTypeTheFrontDoesNotKnow_IsRefusedLoudly_NotAcceptedInSilence()
        {
            var unknown = new Element(ElementKind.Slot, "x", (SlotType)99);
            Assert.Throws<NotSupportedException>(() => SlotCheck.Accepts(unknown, "a"));
            Assert.Throws<NotSupportedException>(() => SlotCheck.Describe(unknown));
        }
    }
}
