using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SendFlowIntegrationExtraTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private InlineChipField _chipField;
        private ChatTranscript  _transcript;
        private VisualElement   _container;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            _chipField  = new InlineChipField();
            _container  = new VisualElement();
            _transcript = new ChatTranscript(_container, ChatBlockRendererFactory.CreateDefault(null, null));
            _cfg        = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }

        private static ChipData HierarchyChip(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        private static ChipData ScriptChip(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);

        private (string turnJson, string rawText) SimulateSend()
        {
            var rawText  = (_chipField.Text ?? "").Trim();
            var snapshot = _chipField.Model.Count > 0 ? new List<ChipData>(_chipField.Model.Chips) : null;
            var llmText  = rawText;
            if (_chipField.Model.Count > 0)
            {
                var ctx = _chipField.Model.SerializePayload(_cfg);
                if (!string.IsNullOrEmpty(ctx)) llmText += "\n" + ctx;
            }
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            _transcript.AppendUserBubble(rawText, snapshot);
            _chipField.ClearChips();
            _chipField.Text = "";
            return (turnJson, rawText);
        }

        [Test]
        public void Send_ScriptChip_NoInstanceID()
        {
            _chipField.Text = "review";
            _chipField.AddChip(ScriptChip("Assets/Health.cs", "Health"));
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[script:Assets/Health.cs]", tj);
            Assert.IsFalse(tj.Contains("[script:Assets/Health.cs #"), "script chip must not have #id suffix");
        }

        [Test]
        public void Send_HierarchyChip_InstanceIDInBracket()
        {
            _chipField.Text = "check";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 42));
            var (tj, _) = SimulateSend();
            StringAssert.Contains("[hierarchy:/Player$2A]", tj);
        }

        [Test]
        public void Send_RawText_NeverContainsBracketTags()
        {
            _chipField.Text = "analyze";
            _chipField.AddChip(HierarchyChip("/A", "A", 1));
            _chipField.AddChip(HierarchyChip("/B", "B", 2));
            _chipField.AddChip(ScriptChip("Assets/C.cs", "C"));
            var (_, raw) = SimulateSend();
            Assert.IsFalse(raw.Contains("[hierarchy:"), "rawText must not contain bracket tags");
        }

        [Test]
        public void Send_EmptyText_EmptyChips_ReturnsNull()
        {
            _chipField.Text = "";
            var (tj, _) = SimulateSend();
            Assert.IsNull(tj);
        }

        [Test]
        public void Send_WhitespaceOnly_NoChips_ReturnsNull()
        {
            _chipField.Text = "   ";
            var (tj, _) = SimulateSend();
            Assert.IsNull(tj);
        }

        // ── Sent bubble text stripping ────────────────────────────────────────

        [Test]
        public void SentBubble_WithChips_TextStripped()
        {
            _chipField.Text = "fix @Player health";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            var msgText = bubble.Q(className: "msg-text");
            Assert.IsNotNull(msgText, "msg-text must exist");
            var label = msgText.Q<UnityEngine.UIElements.Label>();
            Assert.IsNotNull(label);
            StringAssert.DoesNotContain("@Player", label.text);
            StringAssert.Contains("fix", label.text);
        }

        [Test]
        public void SentBubble_WithChips_UserDataPreservesRaw()
        {
            _chipField.Text = "fix @Player health";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            // AddChip injects "@Player " at cursor 0 → rawText includes injected mention
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            var userData = bubble.userData as string ?? "";
            StringAssert.Contains("fix @Player health", userData);
            StringAssert.Contains("@Player", userData);
        }

        [Test]
        public void SentBubble_ChipsOnly_NoMsgText()
        {
            _chipField.Text = "@Player";
            _chipField.AddChip(HierarchyChip("/Player", "Player", 1));
            SimulateSend();
            var bubble = ChatWindowAssertions.GetUserBubble(_container, 0);
            Assert.IsNull(bubble.Q(className: "msg-text"), "stripped to empty — no msg-text expected");
        }
    }
}
