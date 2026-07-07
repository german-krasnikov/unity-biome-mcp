using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ReloadSendIntegrationTests
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;
        private string          _tmpPath;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
            _tmpPath    = Path.Combine(Path.GetTempPath(), $"ReloadSendTest_{System.Guid.NewGuid()}.txt");
            ReloadGuard.OverrideFilePath(_tmpPath);
            ReloadGuard.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            ReloadGuard.ResetForTest();
            if (File.Exists(_tmpPath)) File.Delete(_tmpPath);
        }

        static ChipData H(string path, string name, int id = 0) => ChipTestHelpers.H(path, name, id);
        static ChipData S(string path, string name) => ChipTestHelpers.S(path, name);
        private void InsertChip(ChipData c) => ChipViewTestHelpers.InsertChip(_chipField, c);
        private void SetCursor(int p) => ChipViewTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipViewTestHelpers.Type(_chipField, t);
        private (string, string) SimulateSend() => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        // ── Multi-send (no reload) ─────────────────────────────────────────────

        [Test]
        public void MultiSend_TextThenText_TwoBubbles()
        {
            _chipField.Text = "first"; SimulateSend();
            _chipField.Text = "second"; SimulateSend();
            ChatWindowAssertions.AssertUserBubbleCount(_container, 2);
            var d0 = ChatWindowAssertions.GetBubbleDisplayText(ChatWindowAssertions.GetUserBubble(_container, 0));
            var d1 = ChatWindowAssertions.GetBubbleDisplayText(ChatWindowAssertions.GetUserBubble(_container, 1));
            Assert.AreEqual("first",  d0);
            Assert.AreEqual("second", d1);
        }

        [Test]
        public void MultiSend_ChipsThenNoChips_FirstHasStripSecondNot()
        {
            InsertChip(H("/Player", "Player", 1)); Type("text");
            SimulateSend();
            _chipField.Text = "plain"; SimulateSend();
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 1);
            ChatWindowAssertions.AssertBubbleHasNoChipStrip(ChatWindowAssertions.GetUserBubble(_container, 1));
        }

        [Test]
        public void MultiSend_DifferentChips_NoLeakage()
        {
            InsertChip(H("/Player", "Player", 1)); SimulateSend();
            InsertChip(S("Assets/Foo.cs", "Foo")); SimulateSend();

            // F13: pills are in msg-user-content
            var pills0 = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills0[0], ChipKindKeys.Hierarchy, "Player");

            var pills1 = ChatWindowAssertions.GetUserBubble(_container, 1).Q(className: "msg-user-content")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills1[0], ChipKindKeys.Script, "Foo");
        }

        [Test]
        public void MultiSend_ThreeRapid_StateCleanBetween()
        {
            for (int i = 0; i < 3; i++)
            {
                _chipField.Text = "msg" + i;
                SimulateSend();
                Assert.AreEqual(0, _chipField.Model.Count);
                Assert.AreEqual("", _chipField.Text);
            }
            ChatWindowAssertions.AssertUserBubbleCount(_container, 3);
        }

        [Test]
        public void MultiSend_ChipsOnly_ThenTextOnly_DifferentStructure()
        {
            InsertChip(H("/Player", "Player", 1)); SimulateSend();
            _chipField.Text = "hello"; SimulateSend();
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 1);
            ChatWindowAssertions.AssertBubbleHasNoChipStrip(ChatWindowAssertions.GetUserBubble(_container, 1));
        }

        [Test]
        public void MultiSend_TwoBubbles_ChipStripCountsIndependent()
        {
            InsertChip(H("/A", "A", 1));
            InsertChip(H("/B", "B", 2));
            InsertChip(H("/C", "C", 3));
            SimulateSend();
            InsertChip(H("/X", "X", 4)); SimulateSend();
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 0), 3);
            ChatWindowAssertions.AssertBubbleHasChipStrip(ChatWindowAssertions.GetUserBubble(_container, 1), 1);
        }

        [Test]
        public void MultiSend_ModelEmptyBetweenSends()
        {
            InsertChip(H("/Player", "Player", 1)); SimulateSend();
            Assert.AreEqual(0, _chipField.Model.Count);
            InsertChip(H("/Enemy", "Enemy", 2)); SimulateSend();
            Assert.AreEqual(0, _chipField.Model.Count);
        }
    }
}
