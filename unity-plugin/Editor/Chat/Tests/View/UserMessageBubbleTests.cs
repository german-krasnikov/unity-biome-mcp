// TDD — Group D: ChatTranscript.AppendUserBubble(UserMessage) rendering tests.
// Verifies Bug 1 fix: interleaved segments, no strip-at-top double display.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserMessageBubbleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatTranscript _transcript;
        private VisualElement  _container;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container,
                ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static PositionedChip PC(ChipData chip, int offset)
            => new PositionedChip(chip, offset);

        private VisualElement Bubble(int i = 0) => ChatWindowAssertions.GetUserBubble(_container, i);

        // D1: text-only UserMessage → content container has label child
        [Test]
        public void TextOnly_ContentContainerHasLabel()
        {
            var msg = ChipTextInterleaver.Build("hello world", new List<PositionedChip>());
            _transcript.AppendUserBubble(msg);
            var b     = Bubble();
            var wrap  = b.Q(className: "msg-user-content");
            Assert.IsNotNull(wrap, "Should have msg-user-content container");
            var labels = wrap.Query<Label>().ToList();
            Assert.AreEqual(1, labels.Count);
            Assert.AreEqual("hello world", labels[0].text);
            Assert.IsTrue(labels[0].ClassListContains("msg-text"), "Text label must have msg-text class for correct USS styling");
        }

        // D2: chip-only UserMessage → no text labels, one pill
        [Test]
        public void ChipOnly_OnePillNoLabel()
        {
            var chip = H("/A", "A", 1);
            var msg  = ChipTextInterleaver.Build("", new List<PositionedChip> { PC(chip, 0) });
            _transcript.AppendUserBubble(msg);
            var wrap  = Bubble().Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills  = wrap.Query(className: "inline-chip-pill").ToList();
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(1, pills.Count);
            Assert.AreEqual(0, directLabels);
        }

        // D3: text+chip+text → 3 children in order: Label, pill, Label
        [Test]
        public void TextChipText_ThreeChildrenInOrder()
        {
            var chip = H("/A", "A", 1);
            var msg  = ChipTextInterleaver.Build("before after", new List<PositionedChip> { PC(chip, 6) });
            // "before" text, chip, " after" text
            _transcript.AppendUserBubble(msg);
            var wrap     = Bubble().Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            Assert.GreaterOrEqual(wrap.childCount, 3);
            // First child should be a label
            Assert.IsInstanceOf<Label>(wrap[0], "First child should be a label");
            // Second child should contain a pill
            Assert.IsNotNull(wrap[1].Q(className: "inline-chip-pill"), "Second child should be a pill");
            // Third child should be a label
            Assert.IsInstanceOf<Label>(wrap[2], "Third child should be a label");
        }

        // D4: chip+text+chip → no strip-at-top; interleaved
        [Test]
        public void ChipTextChip_Interleaved_NoStripAtTop()
        {
            var chipA = H("/A", "A", 1);
            var chipB = H("/B", "B", 2);
            var pos   = new List<PositionedChip> { PC(chipA, 0), PC(chipB, 5) };
            var msg   = ChipTextInterleaver.Build("hello world", pos);
            _transcript.AppendUserBubble(msg);
            var b = Bubble();
            // No user-chip-strip (that's the old layout)
            Assert.IsNull(b.Q(className: "user-chip-strip"),
                "F13: no strip-at-top; chips are interleaved");
            // But msg-user-content exists with pills
            var wrap  = b.Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count);
        }

        // D5: empty text segments between chips are omitted (no blank Labels)
        [Test]
        public void EmptyTextSegmentOmitted_NoBlankLabels()
        {
            // chip at offset 0 in empty text → only chip, empty text filtered out
            var chip = H("/A", "A", 1);
            var pos  = new List<PositionedChip> { PC(chip, 0) };
            var msg  = ChipTextInterleaver.Build("", pos);
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(0, directLabels, "Empty text segments should not produce Labels");
        }

        // D6: userData on bubble includes @mention (ToDisplayText with @Name) — via Display field.
        [Test]
        public void BubbleUserData_ContainsAtMention()
        {
            var chip = H("/Player", "Player", 1);
            var pos  = new List<PositionedChip> { PC(chip, 5) };
            var msg  = ChipTextInterleaver.Build("fix health now", pos);
            _transcript.AppendUserBubble(msg);
            var display = Bubble().userData is UserBubbleData u ? u.Display : Bubble().userData as string ?? "";
            StringAssert.Contains("@Player", display);
            StringAssert.Contains("fix", display);
        }

        // D7: bubble has msg-bubble and msg-bubble--user classes
        [Test]
        public void BubbleHasCorrectClasses()
        {
            var msg = ChipTextInterleaver.Build("hi", new List<PositionedChip>());
            _transcript.AppendUserBubble(msg);
            var b = Bubble();
            Assert.IsTrue(b.ClassListContains("msg-bubble"));
            Assert.IsTrue(b.ClassListContains("msg-bubble--user"));
        }

        // D8: inline pill label matches chip display name
        [Test]
        public void InlinePillLabel_MatchesDisplayName()
        {
            var chip = H("/Player", "Player", 1);
            var msg = ChipTextInterleaver.Build("fix", new List<PositionedChip> { PC(chip, 3) });
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            var pill = wrap.Query(className: "inline-chip-pill").First();
            Assert.IsNotNull(pill);
            ChatWindowAssertions.AssertPillContent(pill, ChipKindKeys.Hierarchy, "Player");
        }

        // D10: text label in F13 bubble has msg-text CSS class
        [Test]
        public void TextLabel_HasMsgTextClass()
        {
            var msg = ChipTextInterleaver.Build("hello", new List<PositionedChip>());
            _transcript.AppendUserBubble(msg);
            var wrap   = Bubble().Q(className: "msg-user-content");
            var labels = wrap.Query<Label>().ToList();
            Assert.AreEqual(1, labels.Count, "One label for text segment");
            Assert.IsTrue(labels[0].ClassListContains("msg-text"),
                "Text label must carry msg-text class for USS styling");
        }

        // D11: whitespace-only segment produces no Label child
        [Test]
        public void WhitespaceOnlySegment_NoLabelAdded()
        {
            // chip at offset 0 with spaces only before it → whitespace segment skipped
            var chip = H("/A", "A", 1);
            // Build a message that has "   " text + chip — whitespace text must not render
            var segments = new List<MessageSegment>
            {
                new MessageSegment("   "),
                new MessageSegment(chip),
            };
            var msg = new UserMessage(segments, new List<ChipData> { chip });
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(0, directLabels, "Whitespace-only text segment must not produce a Label");
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(1, pills.Count, "Chip must still render as pill");
        }

        // D12: chip segment renders via ChipPillFactory (pill), not as a Label
        [Test]
        public void ChipSegment_RendersPill_NotLabel()
        {
            var chip = H("/Enemy", "Enemy", 2);
            var msg  = ChipTextInterleaver.Build("", new List<PositionedChip> { PC(chip, 0) });
            _transcript.AppendUserBubble(msg);
            var wrap = Bubble().Q(className: "msg-user-content");
            // Must have a pill
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(1, pills.Count, "Chip segment must produce an inline-chip-pill");
            // Must NOT have a direct Label child (chip goes through ChipPillFactory, not ChatLabel)
            int directLabels = 0;
            foreach (var child in wrap.Children())
                if (child is Label) directLabels++;
            Assert.AreEqual(0, directLabels, "Chip segment must not produce a Label");
        }

    }
}
