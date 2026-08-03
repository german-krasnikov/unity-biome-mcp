// Integration tests for OnSend data-flow pipeline.
// Tests data transformation directly — no window instantiation required.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SendFlowIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
            var registry = ChatBlockRendererFactory.CreateDefault(null, null);
            _transcript = new ChatTranscript(_container, registry);
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData HierarchyChip(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static ChipData ScriptChip(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);

        private (string turnJson, string rawText) SimulateSend()
            => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        [Test]
        public void TextOnly_TurnJsonContainsText()
        {
            _chipField.Text = "hello world";
            var (turnJson, _) = SimulateSend();
            StringAssert.Contains("hello world", turnJson);
        }

        [Test]
        public void TextOnly_BubbleShowsText()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleText(bubble, "hello world");
        }

        [Test]
        public void TextOnly_NoChipStrip()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasNoChipStrip(bubble);
        }

        [Test]
        public void TextPlusOneChip_LlmTextHasChipContext()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            var (turnJson, _) = SimulateSend();
            // hierarchy chip with id=42 → [hierarchy:/Player#42]
            StringAssert.Contains("[hierarchy:/Player#42]", turnJson);
        }

        [Test]
        public void TextPlusOneChip_RawTextPreserved()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            var (_, rawText) = SimulateSend();
            // rawText must NOT contain bracket expansion
            Assert.IsFalse(rawText.Contains("[hierarchy:"), "rawText must not contain bracket tags");
            StringAssert.Contains("fix this", rawText);
        }

        [Test]
        public void TextPlusOneChip_BubbleHasChipStrip()
        {
            _chipField.Text = "fix this";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 1);
        }

        [Test]
        public void TextPlusThreeChips_AllChipRefsInPayload()
        {
            _chipField.Text = "analyze";
            _chipField.AddChip(HierarchyChip("/Player",  "Player",  1));
            _chipField.AddChip(HierarchyChip("/Enemy",   "Enemy",   2));
            _chipField.AddChip(ScriptChip("Assets/Health.cs", "Health"));
            var (turnJson, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player#1]",     turnJson);
            StringAssert.Contains("[hierarchy:/Enemy#2]",      turnJson);
            StringAssert.Contains("[script:Assets/Health.cs]",  turnJson);
        }

        [Test]
        public void TextPlusThreeChips_StripHasThreePills()
        {
            _chipField.Text = "analyze";
            _chipField.AddChip(HierarchyChip("/Player",  "Player",  1));
            _chipField.AddChip(HierarchyChip("/Enemy",   "Enemy",   2));
            _chipField.AddChip(ScriptChip("Assets/Health.cs", "Health"));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 3);
        }

        [Test]
        public void ChipsOnly_NoText_StillSends()
        {
            _chipField.Text = "";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            var (turnJson, _) = SimulateSend();
            Assert.IsNotNull(turnJson, "chips-only send must produce a turnJson");
        }

        [Test]
        public void ChipsOnly_BubbleStripPresent()
        {
            _chipField.Text = "";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            ChatWindowAssertions.AssertBubbleHasChipStrip(bubble, 1);
        }

        [Test]
        public void AfterSend_InputCleared()
        {
            _chipField.Text = "some text";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            ChatWindowAssertions.AssertInputText(_chipField, "");
            Assert.AreEqual(0, _chipField.Model.Count, "Model must be empty after send");
        }

        [Test]
        public void AfterSend_PillRowHidden()
        {
            _chipField.Text = "some text";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            ChatWindowAssertions.AssertPillRowHidden(_chipField);
        }

        [Test]
        public void SentBubble_UserDataContainsAtMention()
        {
            _chipField.Text = "check the player";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 99));
            SimulateSend();
            var bubble   = ChatWindowAssertions.GetUserBubble(_container, 0);
            var display  = ChatWindowAssertions.GetBubbleDisplayText(bubble) ?? "";
            StringAssert.Contains("check the player", display);
            StringAssert.Contains("@Player", display);
            Assert.IsFalse(display.Contains("[hierarchy:"), "Display must not contain bracket expansion");
        }

        [Test]
        public void TextOnly_BubbleContentContainer_HasOneLabel()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var wrap = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            Assert.IsNotNull(wrap, "F13 bubble must have .msg-user-content");
            int labelCount = 0;
            foreach (var child in wrap.Children())
                if (child is UnityEngine.UIElements.Label) labelCount++;
            Assert.AreEqual(1, labelCount);
        }

        [Test]
        public void TextOnly_NoPillsInContent()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var wrap = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content");
            Assert.IsNotNull(wrap);
            var pills = wrap.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(0, pills.Count, "Text-only message must have no pills");
        }

        [Test]
        public void TextOnly_BubbleUserData_EqualsRawText()
        {
            _chipField.Text = "hello world";
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            var display = ChatWindowAssertions.GetBubbleDisplayText(bubble);
            Assert.AreEqual("hello world", display);
        }
    }
}
