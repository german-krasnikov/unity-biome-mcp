// Monkey tests: message send edge cases — special chars, size, rapid fire, transcript.
// Does NOT duplicate: SendFlowIntegrationTests, SendFlowIntegrationExtraTests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    [Category(TestCategories.Stress)]
    public class ChatWindowSendMonkeyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
        public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        private (string turnJson, string rawText) Send()
            => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        private static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        // ── Empty / whitespace ────────────────────────────────────────────────

        [Test] public void Send_NewlinesOnly_ReturnsNull()
            { _chipField.Text = "\n\n\n"; Assert.IsNull(Send().turnJson); }

        [Test] public void Send_TabOnly_ReturnsNull()
            { _chipField.Text = "\t\t"; Assert.IsNull(Send().turnJson); }

        [Test]
        public void Send_WhitespaceWithChip_ProducesTurnJson()
        {
            _chipField.Text = " ";
            _chipField.AddChip(H("/Player", "Player", 1));
            Assert.IsNotNull(Send().turnJson);
        }

        // ── Special characters ────────────────────────────────────────────────

        [Test]
        public void Send_SingleQuote_InTurnJson()
        {
            _chipField.Text = "it's fine";
            var (tj, _) = Send();
            StringAssert.Contains("it's fine", tj);
        }

        [Test] public void Send_DoubleQuote_NoThrow()
            { _chipField.Text = "say \"hi\""; Assert.DoesNotThrow(() => Send()); }

        [Test] public void Send_Backslash_NoThrow()
            { _chipField.Text = @"C:\Users\dev"; Assert.DoesNotThrow(() => Send()); }

        [Test]
        public void Send_PipeSeparator_Preserved()
        {
            _chipField.Text = "cmd | grep x";
            StringAssert.Contains("cmd | grep x", Send().turnJson);
        }

        [Test]
        public void Send_Unicode_Preserved()
        {
            _chipField.Text = "こんにちは🌍";
            StringAssert.Contains("こんにちは🌍", Send().turnJson);
        }

        [Test] public void Send_ControlChars_NoThrow()
            { _chipField.Text = "test\t\rmessage"; Assert.DoesNotThrow(() => Send()); }

        [Test] public void Send_NullByte_NoThrow()
            { _chipField.Text = "data\0end"; Assert.DoesNotThrow(() => Send()); }

        // ── Size extremes ─────────────────────────────────────────────────────

        [Test]
        public void Send_100CharText_FullyPreserved()
        {
            var text = new string('a', 100);
            _chipField.Text = text;
            StringAssert.Contains(text, Send().turnJson);
        }

        [Test] public void Send_10KBText_NoThrow()
            { _chipField.Text = new string('x', 10_000); Assert.DoesNotThrow(() => Send()); }

        [Test] public void Send_100KBText_NoThrow()
            { _chipField.Text = new string('x', 100_000); Assert.DoesNotThrow(() => Send()); }

        [Test, Category("perf")] public void Send_1MBText_NoThrow()
            { _chipField.Text = new string('x', 1_000_000); Assert.DoesNotThrow(() => Send()); }

        // ── Multiple chips ────────────────────────────────────────────────────

        [Test]
        public void Send_5Chips_AllPathsInTurnJson()
        {
            _chipField.Text = "check";
            for (int i = 1; i <= 5; i++) _chipField.AddChip(H($"/Obj{i}", $"Obj{i}", i));
            var tj = Send().turnJson;
            for (int i = 1; i <= 5; i++) StringAssert.Contains($"/Obj{i}", tj);
        }

        [Test] public void Send_50Chips_NoException()
        {
            _chipField.Text = "mass";
            for (int i = 1; i <= 50; i++) _chipField.AddChip(H($"/O{i}", $"O{i}", i));
            Assert.DoesNotThrow(() => Send());
        }

        [Test] public void Send_ChipWithSpaceInPath_NoThrow()
        {
            _chipField.Text = "check";
            _chipField.AddChip(H("/Scene/My Object", "My Object", 42));
            Assert.DoesNotThrow(() => Send());
        }

        [Test] public void Send_ChipWithNullPath_NoThrow()
        {
            _chipField.Text = "check";
            _chipField.AddChip(new ChipData(ChipKindKeys.Hierarchy, null, "NullPath", 0));
            Assert.DoesNotThrow(() => Send());
        }

        // ── Rapid fire ────────────────────────────────────────────────────────

        [Test]
        public void Send_10xRapid_10BubblesInTranscript()
        {
            for (int i = 0; i < 10; i++) { _chipField.Text = $"msg{i}"; Send(); }
            Assert.AreEqual(10, _container.childCount);
        }

        [Test] public void Send_100xRapid_NoException()
        {
            Assert.DoesNotThrow(() => { for (int i = 0; i < 100; i++) { _chipField.Text = $"m{i}"; Send(); } });
        }

        // ── Transcript state ──────────────────────────────────────────────────

        [Test]
        public void Send_FieldClearedAfterSend()
        {
            _chipField.Text = "hello";
            _chipField.AddChip(H("/A", "A", 1));
            Send();
            Assert.AreEqual("", _chipField.Text);
            Assert.AreEqual(0, _chipField.Model.Count);
        }

        [Test]
        public void Send_BubbleText_MatchesInput()
        {
            _chipField.Text = "precise text";
            Send();
            ChatWindowAssertions.AssertBubbleText(ChatWindowAssertions.GetUserBubble(_container, 0), "precise text");
        }

        [Test]
        public void Send_TwoBubbles_SecondAtIndex1()
        {
            _chipField.Text = "first"; Send();
            _chipField.Text = "second"; Send();
            ChatWindowAssertions.AssertBubbleText(ChatWindowAssertions.GetUserBubble(_container, 1), "second");
        }
    }
}
