// Integration tests for chip-text-chip SEQUENCE — send flow, bubble rendering, cleanup.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSendSequenceTests
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private void InsertChip(ChipData c) => ChipViewTestHelpers.InsertChip(_chipField, c);
        private void SetCursor(int p) => ChipViewTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipViewTestHelpers.Type(_chipField, t);
        private (string, string) SimulateSend() => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        // ── Send: sequence preserved in payload ───────────────────────────────

        [Test]
        public void Send_ChipTextChip_LlmPayloadHasSequence()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            var (tj, _) = SimulateSend();
            // F13: chip order is in llm payload; raw text has no @mentions
            Assert.Less(tj.IndexOf("[hierarchy:/Player"), tj.IndexOf("[hierarchy:/Enemy"));
        }

        [Test]
        public void Send_ChipTextChip_LlmTextHasChipPayload()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player#1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy#2]",  tj);
        }

        [Test]
        public void Send_ChipTextChip_BubblePillsPreserveOrder()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            SimulateSend();
            // F13: chips are inline pills in msg-user-content, not @mentions in userData
            var wrap  = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count);
        }

        [Test]
        public void Send_ChipTextChip_ChipStripHasTwoPills()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            SimulateSend();
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 2);
        }

        [Test]
        public void Send_ChipTextChip_ChipStripOrderMatchesModel()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            SimulateSend();
            // F13: pills are in msg-user-content
            var wrap  = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills[0], ChipKindKeys.Hierarchy, "Player");
            ChatWindowAssertions.AssertPillContent(pills[1], ChipKindKeys.Hierarchy, "Enemy");
        }

        // ── Send: chips-only ──────────────────────────────────────────────────

        [Test]
        public void Send_TwoChipsNoText_BothInPayload()
        {
            InsertChip(H("/Player", "Player", 1));
            InsertChip(H("/Enemy",  "Enemy",  2));
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player#1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy#2]",  tj);
        }

        [Test]
        public void Send_EmptyTextOnlyChips_Sends()
        {
            InsertChip(H("/Player", "Player", 1));
            InsertChip(H("/Enemy",  "Enemy",  2));
            _chipField.Text = "";
            var (tj, _) = SimulateSend();
            Assert.IsNotNull(tj);
        }

        // ── Send: three chips interleaved ─────────────────────────────────────

        [Test]
        public void Send_ThreeChipsInterleaved_FullSequence()
        {
            Type("a "); InsertChip(H("/Player", "Player", 1));
            Type(" b "); InsertChip(H("/Enemy",  "Enemy",  2));
            Type(" c "); InsertChip(H("/Boss",   "Boss",   3));
            var (tj, _) = SimulateSend();
            // F13: order reflected in llm payload position, not raw text @mentions
            Assert.Less(tj.IndexOf("[hierarchy:/Player"), tj.IndexOf("[hierarchy:/Enemy"));
            Assert.Less(tj.IndexOf("[hierarchy:/Enemy"),  tj.IndexOf("[hierarchy:/Boss"));
            StringAssert.Contains("[hierarchy:/Player#1]", tj);
            StringAssert.Contains("[hierarchy:/Enemy#2]",  tj);
            StringAssert.Contains("[hierarchy:/Boss#3]",   tj);
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 3);
        }

        // ── Post-send: cleanup ────────────────────────────────────────────────

        [Test]
        public void Send_AfterChipTextChip_AllCleared()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            SimulateSend();
            ChatWindowAssertions.AssertInputText(_chipField, "");
            Assert.AreEqual(0, _chipField.Model.Count);
            ChatWindowAssertions.AssertPillRowHidden(_chipField);
        }

        [Test]
        public void Send_SingleChipWithText_PillLabelMatchesName()
        {
            InsertChip(H("/Player", "Player", 1));
            Type("fix ");
            SimulateSend();
            var wrap  = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            var pill  = wrap.Query(className: "inline-chip-pill").First();
            Assert.IsNotNull(pill);
            ChatWindowAssertions.AssertPillContent(pill, ChipKindKeys.Hierarchy, "Player");
        }

        [Test]
        public void Send_InlineOrder_TextBetweenPills()
        {
            Type("a ");
            InsertChip(H("/X", "X", 1));
            Type(" b ");
            InsertChip(H("/Y", "Y", 2));
            SimulateSend();
            var wrap = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            Assert.GreaterOrEqual(wrap.childCount, 4, "Expected at least: Label, pill, Label, pill");
            Assert.IsInstanceOf<UnityEngine.UIElements.Label>(wrap[0]);
            Assert.IsNotNull(wrap[1].Q(className: "inline-chip-pill"));
            Assert.IsInstanceOf<UnityEngine.UIElements.Label>(wrap[2]);
            Assert.IsNotNull(wrap[3].Q(className: "inline-chip-pill"));
        }

        // ── E2E: @mention in turnJson ─────────────────────────────────────────

        // E2E_1: chip + text → turnJson text block includes @mention (full path)
        [Test]
        public void Send_ChipWithText_TurnJsonContainsAtMention()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" fix health");
            var (tj, _) = SimulateSend();
            StringAssert.Contains("@/Player", tj);
            StringAssert.Contains("[hierarchy:/Player#1]", tj);
        }

        // E2E_2: chip-only message → turnJson contains @Path (full path)
        [Test]
        public void Send_ChipOnly_TurnJsonContainsAtMention()
        {
            InsertChip(H("/Cube", "Cube", 5));
            var (tj, _) = SimulateSend();
            StringAssert.Contains("@/Cube", tj);
        }

        // E2E_3: chip with spaces in path → @mention uses full path (preserves spaces)
        [Test]
        public void Send_ChipWithSpaces_TurnJsonHasAtMentionWithSpaces()
        {
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, "/Main Camera", "Main Camera", -7));
            Type("look");
            var (tj, _) = SimulateSend();
            StringAssert.Contains("@/Main Camera", tj);
        }
    }
}
