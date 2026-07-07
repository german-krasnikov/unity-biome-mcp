// BuildFromRaw defensive @mention stripping tests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipTextInterleaverBuildFromRawDefensiveTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        // ── Positive: exact offsets ───────────────────────────────────────────

        [Test]
        public void ExactOffset_SingleChip_StripsCleanly()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "@Collectible_3 test" — chip at offset 0
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 0) });
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Collectible_3 test", display);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void TwoExactOffsets_BothStripped_TextPreserved()
        {
            var c1 = H("/Coll_3", "Collectible_3", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            // raw: "@Collectible_3 test @Collectible_2" — c2 @ at index 20
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test @Collectible_2",
                new List<PositionedChip> { PC(c1, 0), PC(c2, 20) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }

        [Test]
        public void ChipsAtStartAndEnd_TextSegmentContainsMiddle()
        {
            var c1 = H("/A", "Alpha", 1);
            var c2 = H("/B", "Beta", 2);
            // "@Alpha middle @Beta" — c1 at 0, c2 at 14
            var raw = "@Alpha middle @Beta";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 14) });
            var chips = new List<MessageSegment>();
            var texts = new List<string>();
            foreach (var s in msg.Segments)
                if (s.IsChip) chips.Add(s); else if (!string.IsNullOrEmpty(s.Text)) texts.Add(s.Text);
            Assert.AreEqual(2, chips.Count);
            Assert.AreEqual(1, texts.Count);
            StringAssert.Contains("middle", texts[0]);
        }

        [Test]
        public void ThreeChipsInterleaved_AllMentionsStripped()
        {
            var c1 = H("/A", "A1", 1); var c2 = H("/B", "B2", 2); var c3 = H("/C", "C3", 3);
            // "@A1 x @B2 y @C3"
            var raw = "@A1 x @B2 y @C3";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 6), PC(c3, 12) });
            Assert.AreEqual(3, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text);
        }

        // ── Negative/edge: misaligned offsets ────────────────────────────────

        [Test]
        public void OffsetOffByOneHigh_StillStrips()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "@Collectible_3 test", chip stored at offset 1 (off-by-one: points to 'C')
            var msg = ChipTextInterleaver.BuildFromRaw("@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 1) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void OffsetOffByOneLow_StillStrips()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // raw: "x@Collectible_3 test", chip at offset 0 (before @)
            var msg = ChipTextInterleaver.BuildFromRaw("x@Collectible_3 test",
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        [Test]
        public void MentionManuallyEdited_ChipStillTracked()
        {
            var chip = H("/Coll_3", "Collectible_3", 1);
            // User edited "@Collectible_3" to "@Coll" — mention not found
            var msg = ChipTextInterleaver.BuildFromRaw("@Coll test",
                new List<PositionedChip> { PC(chip, 0) });
            // Chip still present in Chips list
            Assert.AreEqual(1, msg.Chips.Count);
        }

        [Test]
        public void DuplicateDisplayNames_BothMentionsStripped()
        {
            var c1 = new ChipData(ChipKindKeys.Hierarchy, "/PathA", "Camera", 1);
            var c2 = new ChipData(ChipKindKeys.Hierarchy, "/PathB", "Camera", 2);
            // "@Camera text @Camera"
            var raw = "@Camera text @Camera";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 13) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Camera", seg.Text);
        }

        [Test]
        public void DisplayNameWithSpaces_MentionStripped()
        {
            var chip = H("/Main Camera", "Main Camera", 1);
            var raw = "@Main Camera fix";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Main Camera", seg.Text);
        }

        [Test]
        public void OnlyMention_NoTextSegmentsWithAtSign()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.BuildFromRaw("@Player",
                new List<PositionedChip> { PC(chip, 0) });
            Assert.AreEqual(1, msg.Chips.Count);
            var display = ChipTextInterleaver.ToDisplayText(msg);
            Assert.AreEqual("@Player", display);
        }

        [Test]
        public void TwoChips_NoTextSegmentContainsAtMention()
        {
            var c1 = H("/Coll_3", "Collectible_3", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            var raw = "@Collectible_3 test @Collectible_2";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 20) });
            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                Assert.IsFalse(seg.Text.Contains("@Collectible_3"),
                    $"Segment contains @Collectible_3: '{seg.Text}'");
                Assert.IsFalse(seg.Text.Contains("@Collectible_2"),
                    $"Segment contains @Collectible_2: '{seg.Text}'");
            }
        }

        // ── VE-tree: AppendUserBubble(UserMessage) label assertions ──────────

        [Test]
        public void AppendUserBubble_ChipAndText_LabelsContainNoAtMention()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container,
                ChatBlockRendererFactory.CreateDefault(null, null));

            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);
            var msg  = ChipTextInterleaver.Build("fix this",
                new List<PositionedChip> { new PositionedChip(chip, 8) }); // chip after text
            transcript.AppendUserBubble(msg);

            foreach (var lbl in container.Query<Label>().ToList())
                Assert.IsFalse(lbl.text.Contains("@Player"),
                    $"Label must not contain @mention: '{lbl.text}'");
        }

        [Test]
        public void AppendUserBubble_PillRendered_TextLabelPresent()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container,
                ChatBlockRendererFactory.CreateDefault(null, null));

            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Enemy", "Enemy", 2);
            var msg  = ChipTextInterleaver.Build("check enemy",
                new List<PositionedChip> { new PositionedChip(chip, 6) }); // after "check "
            transcript.AppendUserBubble(msg);

            var pill = container.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "Chip must be rendered as pill");

            bool hasText = false;
            foreach (var lbl in container.Query<Label>().ToList())
                if (lbl.text.Contains("check")) hasText = true;
            Assert.IsTrue(hasText, "Text 'check' must appear in a label");
        }

        [Test]
        public void AppendUserBubble_TwoChips_NoBareatSymbolInLabels()
        {
            var container  = new VisualElement();
            var transcript = new ChatTranscript(container,
                ChatBlockRendererFactory.CreateDefault(null, null));

            var c1 = new ChipData(ChipKindKeys.Hierarchy, "/A", "Alpha", 1);
            var c2 = new ChipData(ChipKindKeys.Hierarchy, "/B", "Beta",  2);
            var msg = ChipTextInterleaver.Build("between",
                new List<PositionedChip>
                {
                    new PositionedChip(c1, 0),
                    new PositionedChip(c2, 7),
                });
            transcript.AppendUserBubble(msg);

            foreach (var lbl in container.Query<Label>().ToList())
                Assert.IsFalse(lbl.text.Contains("@"),
                    $"Label must not contain bare @: '{lbl.text}'");
            var pills = container.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count, "Both chips must render as pills");
        }

        // ── F21: stored offset undershot (wide forward search) ───────────────

        [Test]
        public void StoredOffsetUndershot_FallbackSearchFindsMention()
        {
            // chip2 stored at offset 16, but @Collectible_3 is actually at position 23
            var c1 = H("/Coll_2", "Collectible_2", 1);
            var c2 = H("/Coll_3", "Collectible_3", 2);
            var raw = "@Collectible_2 что это @Collectible_3 ?";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 16) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_3", seg.Text);
        }

        // F21b: two chips same name, second offset overshoots — known limitation
        [Test]
        public void DuplicateNameSecondOffsetOvershot_BothChipsPresent()
        {
            var c1 = H("/Cam", "Camera", 1);
            var c2 = H("/Cam2", "Camera", 2);
            var raw = "@Camera is near @Camera object";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 99) });
            Assert.AreEqual(2, msg.Chips.Count, "Both chips must be in output regardless of offset");
        }

        // ── F21c–g: exact screenshot reproductions ───────────────────────────

        // F21c: exact screenshot — two chips, Cyrillic text between, both mentions stripped
        [Test]
        public void CyrillicTextBetweenTwoChips_BothMentionsStripped()
        {
            var c1 = H("/Coll_1", "Collectible_1", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            var raw = "@Collectible_1 что это @Collectible_2 ?";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 23) });
            Assert.AreEqual(2, msg.Chips.Count);
            Assert.AreEqual(4, msg.Segments.Count, "chip1, text, chip2, remaining");
            Assert.IsTrue(msg.Segments[0].IsChip);
            Assert.AreEqual("что это ", msg.Segments[1].Text);
            Assert.IsTrue(msg.Segments[2].IsChip);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text,
                    $"Text segment contains @: '{seg.Text}'");
        }

        // F21d: full InlineChipField integration with Cyrillic text
        [Test]
        public void InlineChipField_CyrillicTextBetweenChips_NoStrayAtMention()
        {
            var field = new InlineChipField();
            field.AddChip(H("/Coll_1", "Collectible_1", 1));
            ChipViewTestHelpers.Type(field, "что это ");
            field.AddChip(H("/Coll_2", "Collectible_2", 2));
            ChipViewTestHelpers.Type(field, "?");

            var rawText = (field.Text ?? "").Trim();
            var positioned = new System.Collections.Generic.List<PositionedChip>(
                field.Model.PositionedChips);
            var msg = ChipTextInterleaver.BuildFromRaw(rawText, positioned);

            Assert.AreEqual(2, msg.Chips.Count, "Both chips present");
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text,
                    $"Text contains @: '{seg.Text}'");
        }

        // F21e: display name with space + Cyrillic text
        [Test]
        public void SpaceInDisplayName_CyrillicText_BothMentionsStripped()
        {
            var c1 = H("/Coll_2", "Collectible_2", 1);
            var c2 = H("/Main Camera", "Main Camera", 2);
            var raw = "@Collectible_2 что это @Main Camera ?";
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 23) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip)
                {
                    StringAssert.DoesNotContain("@Collectible_2", seg.Text);
                    StringAssert.DoesNotContain("@Main Camera", seg.Text);
                }
        }

        // F21f: Trim() shifts offsets — fallback search still finds mentions
        [Test]
        public void TrimCausesOffsetShift_FallbackSearchStillFinds()
        {
            var c1 = H("/Coll_1", "Collectible_1", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            // Simulate: TextField had leading space, Trim removed it, offsets shifted by 1
            var raw = "@Collectible_1 что это @Collectible_2 ?"; // already trimmed
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 1), PC(c2, 24) }); // offsets +1 from original
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@", seg.Text,
                    $"Text contains @: '{seg.Text}'");
        }

        // F21g: large offset drift — fallback search across full remaining text
        [Test]
        public void LargeOffsetDrift_FallbackSearchFindsAllMentions()
        {
            var c1 = H("/Coll_1", "Collectible_1", 1);
            var c2 = H("/Coll_2", "Collectible_2", 2);
            var raw = "@Collectible_1 что это @Collectible_2 ?";
            // chip2 offset = 10 (way off — actual @ at 23)
            var msg = ChipTextInterleaver.BuildFromRaw(raw,
                new List<PositionedChip> { PC(c1, 0), PC(c2, 10) });
            Assert.AreEqual(2, msg.Chips.Count);
            foreach (var seg in msg.Segments)
                if (!seg.IsChip) StringAssert.DoesNotContain("@Collectible_2", seg.Text,
                    $"Text contains @Collectible_2: '{seg.Text}'");
        }

        // ── Regression: exact user scenario ──────────────────────────────────

        [Test]
        public void TwoChipsWithInterleavedTyping_NoStrayAtMentions()
        {
            var field = new InlineChipField();
            field.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Collectible_3", "Collectible_3", 1));
            // Simulate typing "test " after the chip
            ChipViewTestHelpers.Type(field, " test ");
            field.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Collectible_2", "Collectible_2", 2));

            var rawText    = (field.Text ?? "").Trim();
            var positioned = new System.Collections.Generic.List<PositionedChip>(
                field.Model.PositionedChips);
            var msg = ChipTextInterleaver.BuildFromRaw(rawText, positioned);

            foreach (var seg in msg.Segments)
            {
                if (seg.IsChip) continue;
                Assert.IsFalse(seg.Text.Contains("@"),
                    $"Text segment contains @: '{seg.Text}'");
            }
            Assert.AreEqual(2, msg.Chips.Count, "Both chips must be present");
        }
    }
}
