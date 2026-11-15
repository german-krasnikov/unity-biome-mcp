using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;
using static UnityMCP.Playtest.Core.GrammarTests.MatchShow;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // The blocks (ASSERT_BATCH ... END) the reader reads. The expectations are what the reader in JavaScript says of these scripts (readSurface of the SURFACE playtest, lines numbered from 1),
    // printed for them and not read off this code. A line that is no item is said and left out; a head that is not read skips the block; an END deeper than the head is a line of the block (I11).
    public class PlaytestReaderBlockTests
    {
        [Test]
        public void Read_ABlock_GivesTheHeadTheLinesBetweenAndWhereItEnds_AndReadsOnAfterIt()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT Player/Health == 100
# a comment

ASSERT Enemy/Health > 0 TIMEOUT 5
END
LOG after");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-6:  ITEMS op===@21; query=Player/Health@7; value=100@24 || op=>@20; query=Enemy/Health@7; value=0 TIMEOUT 5@22 | PT_LOG 7-7: message=after@4"));
            Assert.That(r.Diagnostics, Is.Empty);
            Assert.That(r.Uses[0].Form.IsBlock, Is.True);
        }

        [Test]
        public void Read_ABlockAndBlankLinesInIt_AreBlankOrAComment_WhateverTheirBlanks()
        {
            var r = ReaderHarness.Read(new[] { "ASSERT_BATCH", "  ", "\t", "ASSERT a == 1", "   # note", "END" });
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-6:  ITEMS op===@9; query=a@7; value=1@12"));
            Assert.That(r.Diagnostics, Is.Empty);
        }

        [Test]
        public void Read_AnEmptyBlock_IsABlock()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
END
LOG after");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-2:  | PT_LOG 3-3: message=after@4"));
            Assert.That(r.Uses[0].Items, Is.Empty);
        }

        [Test]
        public void Read_TwoBlocks_AreTwoUses()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT a == 1
END
ASSERT_BATCH
ASSERT b == 2
ASSERT c == 3
END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-3:  ITEMS op===@9; query=a@7; value=1@12 | PT_ASSERT_BATCH 4-7:  ITEMS op===@9; query=b@7; value=2@12 || op===@9; query=c@7; value=3@12"));
        }

        // A line of the block that is no complete ASSERT is said and left out; the lines that are read stay (the hand parser skipped them in silence: INTENDED I6).
        [Test]
        public void Read_ALineOfABlockThatIsNoAssert_IsSaid_AndLeftOut_AndTheRestIsRead()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT a == 1 AS x
ASSERT b
ASSERT
ASSERT c contains d
END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-6:  ITEMS op===@9; query=a@7; value=1 AS x@12 || op=contains@9; query=c@7; value=d@18"));
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@3:8 ASSERT_BATCH: the line ends early | PT_SYNTAX@4:1 ASSERT_BATCH: the line ends early"));
        }

        [Test]
        public void Read_TheLinesOfABlockThatNoItemStartsWith_AreUnknownKeywords_EachOnItsOwnLine()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
LOG hi
ASSERT $x
ASSERT /x|C|f ==
ASSERT /x|C|f ~ 1
END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-6: "));
            Assert.That(Said(r), Is.EqualTo("PT_UNKNOWN_KEYWORD@2:1 unknown keyword LOG | PT_SYNTAX@3:8 ASSERT_BATCH: the line ends early | PT_SYNTAX@4:15 ASSERT_BATCH: the line ends early | PT_SYNTAX@5:17 ASSERT_BATCH: the line ends early"));
            Assert.That(r.Diagnostics.Select(d => d.Index), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        // A head that is not read skips its block to the END: the lines of the block are not commands, and the diagnostic is the head's alone.
        [Test]
        public void Read_ABlockWhoseHeadIsNotRead_IsSkippedToItsEnd_AndTheReaderReadsOnAfterIt()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH extra
ASSERT /X|C|f == 1
END
LOG after");
            Assert.That(Uses(r), Is.EqualTo("PT_LOG 4-4: message=after@4"));
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@1:14 ASSERT_BATCH: unexpected \"extra\""));
            var open = ReaderHarness.Read(@"ASSERT_BATCH extra
LOG never read");   // with no END the skip takes every line that follows
            Assert.That(Uses(open), Is.Empty);
            Assert.That(Said(open), Is.EqualTo("PT_SYNTAX@1:14 ASSERT_BATCH: unexpected \"extra\""));
        }

        // Where the cursor is after the first command of the lines.
        private static int After(params string[] lines)
        {
            var i = 0;
            PlaytestReader.Read(lines, Enumerable.Range(1, lines.Length).ToArray(), ref i, new List<PlaytestDiagnostic>());
            return i;
        }

        [Test]
        public void Read_ABlock_MovesTheCursorPastItsEnd_OrToTheEndOfTheLinesWhenThereIsNone()
        {
            Assert.That(After("ASSERT_BATCH", "ASSERT a == 1", "END", "LOG after"), Is.EqualTo(3));
            Assert.That(After("ASSERT_BATCH", "ASSERT a == 1"), Is.EqualTo(2));
            Assert.That(After("ASSERT_BATCH extra", "x", "END", "LOG after"), Is.EqualTo(3), "a head that is not read: past the END too");
            Assert.That(After("ASSERT_BATCH extra", "x"), Is.EqualTo(2));
        }

        [Test]
        public void Read_ALineThatLooksLikeTheNextBlock_IsNotTheEndOfTheOneBefore()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT_BATCH
END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-3: "));
            Assert.That(Said(r), Is.EqualTo("PT_UNKNOWN_KEYWORD@2:1 unknown keyword ASSERT_BATCH"));
        }
    }
}
