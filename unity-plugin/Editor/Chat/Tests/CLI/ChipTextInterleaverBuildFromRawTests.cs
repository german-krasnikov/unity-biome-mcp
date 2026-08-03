// TDD — F24: Fix @Object chip duplicates in BuildFromRaw else-branch.
// When @mention is not found near expected offset, a global search is done
// before falling back — preventing raw @mention text + chip duplication.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.TestStringHelpers;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipTextInterleaverBuildFromRawTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // 1. Exact offset — no duplication
        [Test]
        public void BuildFromRaw_MentionAtExactOffset_NoDuplication()
        {
            var chip = H("/Player", "Player", 1);
            var msg  = ChipTextInterleaver.BuildFromRaw("@Player do stuff",
                new List<PositionedChip> { PC(chip, 0) });

            Assert.AreEqual(1, msg.Chips.Count, "One chip");
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Player", seg.Text,
                    $"Text segment must not contain @Player: '{seg.Text}'");
        }

        // 2. Offset drifted far past mention location — global search should still find it
        [Test]
        public void BuildFromRaw_MentionOffsetDrifted_StillStrips()
        {
            var chip = H("/Player", "Player", 1);
            // @Player is at index 0 but stored offset is way off (999)
            var msg  = ChipTextInterleaver.BuildFromRaw("@Player do stuff",
                new List<PositionedChip> { PC(chip, 999) });

            Assert.AreEqual(1, msg.Chips.Count, "Chip must still be present");
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Player", seg.Text,
                    $"Global search must strip @Player: '{seg.Text}'");
        }

        // 3. Mention not in text at all — chip only, no stale raw text duplication
        [Test]
        public void BuildFromRaw_MentionNotInText_ChipOnly()
        {
            var chip = H("/Player", "Player", 1);
            // No @Player in raw text — chip tracked but no mention to strip
            var msg  = ChipTextInterleaver.BuildFromRaw("do stuff",
                new List<PositionedChip> { PC(chip, 0) });

            Assert.AreEqual(1, msg.Chips.Count, "Chip must still be tracked");
            // Text should be present without duplication artifacts
            var display = ChipTextInterleaver.ToDisplayText(msg);
            // display text reconstructs @Player at chip position — that's correct behavior
            // the key guarantee: no DOUBLE @Player in text segments
            int atCount = 0;
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) atCount += CountOccurrences(seg.Text, "@Player");
            Assert.AreEqual(0, atCount, "No @Player in text segments when mention absent from raw");
        }

        // 4. Multiple chips — all stripped, no dupes
        [Test]
        public void BuildFromRaw_MultipleMentions_AllStrippedNoDupes()
        {
            var c1 = H("/A", "Alpha", 1);
            var c2 = H("/B", "Beta",  2);
            var c3 = H("/C", "Gamma", 3);
            var raw = "@Alpha text @Beta more @Gamma end";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 12), PC(c3, 23) });

            Assert.AreEqual(3, msg.Chips.Count, "All three chips present");
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                StringAssert.DoesNotContain("@Alpha", seg.Text);
                StringAssert.DoesNotContain("@Beta",  seg.Text);
                StringAssert.DoesNotContain("@Gamma", seg.Text);
            }
        }

        // 5. Mention at end, no trailing space
        [Test]
        public void BuildFromRaw_MentionAtEndNoSpace_Works()
        {
            var chip = H("/Player", "Player", 1);
            var msg  = ChipTextInterleaver.BuildFromRaw("text @Player",
                new List<PositionedChip> { PC(chip, 5) });

            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Player", seg.Text);
        }

        // 6. Drifted offset — mention exists later in string — global search finds it
        [Test]
        public void BuildFromRaw_OffsetBeforeMention_GlobalSearchFinds()
        {
            var chip = H("/Enemy", "Enemy", 1);
            // @Enemy is at index 10, but offset stored as 0 (before the mention)
            var raw  = "some text @Enemy rest";
            var msg  = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(chip, 0) });

            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Enemy", seg.Text,
                    $"Global search must strip @Enemy: '{seg.Text}'");
        }

    }
}
