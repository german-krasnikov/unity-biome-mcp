using NUnit.Framework;
using UnityMCP.Playtest.Core.SharedTests;
using static UnityMCP.Playtest.Core.GrammarTests.MatchShow;

namespace UnityMCP.Playtest.Core.GrammarTests
{
    // Where a block ends. An END is a line of that word alone (not `end`, not `END x`), and it ends the block if it is not indented deeper than the head (INTENDED I11): one that is deeper is a line
    // of the block like any other. A block with no END takes every line to the last and says so at its head, before it says anything of its lines. The expectations are what the reader in JavaScript
    // says of these scripts (readSurface of the SURFACE playtest, lines numbered from 1).
    public class PlaytestReaderEndTests
    {
        [Test]
        public void Read_ABlockWhoseHeadIsNotRead_IsSkippedToAnEndThatIsNotDeeperThanTheHead()
        {
            var r = ReaderHarness.Read(@"  ASSERT_BATCH extra
  ASSERT a == 1
    END
  ASSERT b == 2
  END
LOG after");
            Assert.That(Uses(r), Is.EqualTo("PT_LOG 6-6: message=after@4"));
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@1:16 ASSERT_BATCH: unexpected \"extra\""));
        }

        // Without an END the block takes every line to the end of the script; the first thing said is the block, at its head.
        [Test]
        public void Read_ABlockWithNoEnd_IsSaid_AtItsHead_FirstOfAll_AndKeepsWhatItRead()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT /X|C|f == 1");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-2:  ITEMS op===@14; query=/X|C|f@7; value=1@17"));
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@1:1 ASSERT_BATCH block is missing END"));
            Assert.That(r.Diagnostics[0].Construct, Is.EqualTo("PT_ASSERT_BATCH"));
            Assert.That(r.Diagnostics[0].Index, Is.EqualTo(0));
        }

        [Test]
        public void Read_ABlockWithNoEnd_SaysItsHeadBeforeTheLinesOfIt_ThatItDoesNotRead()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
LOG hi
ASSERT a == 1");
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@1:1 ASSERT_BATCH block is missing END | PT_UNKNOWN_KEYWORD@2:1 unknown keyword LOG"));
            Assert.That(r.Uses[0].EndIndex, Is.EqualTo(2), "the block ends at the last line");
        }

        // PT-8/Q22: the playtest SURFACE now says CASEFOLD, so an END is a line of that word
        // alone in any ASCII case — `end` in lower case now closes the block too. `END x` (a
        // word after END) is still a line of the block, case being irrelevant to that rule.
        [Test]
        public void Read_AnEndInLowerCase_ClosesTheBlock_ButAWordAfterItIsStillNoEnd()
        {
            var lower = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT /X|C|f == 1
end");
            Assert.That(Said(lower), Is.EqualTo(""));
            var more = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT a == 1
END x
END");
            Assert.That(Uses(more), Is.EqualTo("PT_ASSERT_BATCH 1-4:  ITEMS op===@9; query=a@7; value=1@12"));
            Assert.That(Said(more), Is.EqualTo("PT_UNKNOWN_KEYWORD@3:1 unknown keyword END"));
        }

        // An END ends the block if it is not indented deeper than the head (INTENDED I11): one that is, is a line of the block like any other.
        [Test]
        public void Read_AnEndIndentedDeeperThanTheHead_IsNoEnd_ButALineOfTheBlock()
        {
            var r = ReaderHarness.Read(@"  ASSERT_BATCH
  ASSERT a == 1
    END
  ASSERT b == 2
  END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-5:  ITEMS op===@11; query=a@9; value=1@14 || op===@11; query=b@9; value=2@14"));
            Assert.That(Said(r), Is.EqualTo("PT_UNKNOWN_KEYWORD@3:5 unknown keyword END"));
        }

        [Test]
        public void Read_AnEndIndentedDeeperThanTheHead_AndNoOtherEnd_LeavesTheBlockOpen()
        {
            var r = ReaderHarness.Read(@"ASSERT_BATCH
ASSERT a == 1
 END");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-3:  ITEMS op===@9; query=a@7; value=1@12"));
            Assert.That(Said(r), Is.EqualTo("PT_SYNTAX@1:1 ASSERT_BATCH block is missing END | PT_UNKNOWN_KEYWORD@3:2 unknown keyword END"));
        }

        [Test]
        public void Read_AnEndIndentedLessThanTheHead_IsTheEnd()
        {
            var r = ReaderHarness.Read(@"  ASSERT_BATCH
  ASSERT a == 1
END
LOG after");
            Assert.That(Uses(r), Is.EqualTo("PT_ASSERT_BATCH 1-3:  ITEMS op===@11; query=a@9; value=1@14 | PT_LOG 4-4: message=after@4"));
            Assert.That(r.Diagnostics, Is.Empty);
        }
    }
}
