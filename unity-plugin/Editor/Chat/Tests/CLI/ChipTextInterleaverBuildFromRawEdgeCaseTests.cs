// TDD — F24 extra: multi-chip same name, drifted second chip, edge cases.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipTextInterleaverBuildFromRawEdgeCaseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // 1. Two chips with identical display name but different paths — both stripped
        [Test]
        public void BuildFromRaw_TwoChipsSameDisplayName_BothStripped()
        {
            var c1  = H("/A", "Camera", 1);
            var c2  = H("/B", "Camera", 2);
            var raw = "@Camera text @Camera end";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 13) });

            Assert.AreEqual(2, msg.Chips.Count, "Both chips present");
            var paths = new HashSet<string>();
            foreach (var c in msg.Chips) paths.Add(c.Path);
            Assert.IsTrue(paths.Contains("/A"), "Chip /A present");
            Assert.IsTrue(paths.Contains("/B"), "Chip /B present");
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Camera", seg.Text,
                    $"No @Camera in text segment: '{seg.Text}'");
        }

        // 2. First chip exact, second chip offset=999 — global search finds both
        [Test]
        public void BuildFromRaw_SecondChipDrifted_GlobalSearchFinds()
        {
            var c1  = H("/A", "Alpha", 1);
            var c2  = H("/B", "Beta",  2);
            var raw = "@Alpha text @Beta end";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 999) });

            Assert.AreEqual(2, msg.Chips.Count, "Both chips present");
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                StringAssert.DoesNotContain("@Alpha", seg.Text);
                StringAssert.DoesNotContain("@Beta",  seg.Text);
            }
        }

        // 3. Three chips, middle and last drifted — all stripped
        [Test]
        public void BuildFromRaw_ThreeChips_MiddleDrifted_AllStripped()
        {
            var c1  = H("/A", "Alpha", 1);
            var c2  = H("/B", "Beta",  2);
            var c3  = H("/C", "Gamma", 3);
            var raw = "@Alpha text @Beta more @Gamma end";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 999), PC(c3, 999) });

            Assert.AreEqual(3, msg.Chips.Count, "All three chips present");
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                StringAssert.DoesNotContain("@Alpha", seg.Text);
                StringAssert.DoesNotContain("@Beta",  seg.Text);
                StringAssert.DoesNotContain("@Gamma", seg.Text);
            }
        }

        // 4. Global search (offset=999) — no double-space after stripping mention + trailing space
        [Test]
        public void BuildFromRaw_GlobalSearch_TrailingSpaceConsumed()
        {
            var chip = H("/Player", "Player", 1);
            var raw  = "do stuff @Player ";
            var msg  = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(chip, 999) });

            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                StringAssert.DoesNotContain("  ", seg.Text, "No double-space in text segment");
                StringAssert.DoesNotContain("@Player", seg.Text);
            }
        }

        // 5. Empty raw text with a chip at offset 0 — chip tracked, no crash
        [Test]
        public void BuildFromRaw_EmptyTextWithChip_ChipTracked()
        {
            var chip = H("/Player", "Player", 1);
            var msg  = ChipTextInterleaver.BuildFromRaw("",
                new List<PositionedChip> { PC(chip, 0) });

            Assert.AreEqual(1, msg.Chips.Count, "Chip tracked despite empty raw");
        }

        // 6. Null raw text — treated as empty, chip tracked, no crash
        [Test]
        public void BuildFromRaw_NullRawText_TreatedAsEmpty()
        {
            var chip = H("/Player", "Player", 1);
            var msg  = ChipTextInterleaver.BuildFromRaw(null,
                new List<PositionedChip> { PC(chip, 0) });

            Assert.AreEqual(1, msg.Chips.Count, "Chip tracked with null raw");
        }
    }
}
