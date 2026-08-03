using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ReloadSendCompositionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
            _tmpPath    = Path.Combine(Path.GetTempPath(), $"ReloadSendComp_{System.Guid.NewGuid()}.txt");
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
        private void InsertChip(ChipData c) => ChipViewTestHelpers.InsertChip(_chipField, c);
        private void SetCursor(int p) => ChipViewTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipViewTestHelpers.Type(_chipField, t);
        private (string, string) SimulateSend() => ChipViewTestHelpers.SimulateSend(_chipField, _transcript, _cfg);

        // ── Domain reload + send ──────────────────────────────────────────────

        [Test]
        public void ReloadDuringComposition_RestoreThenSend()
        {
            InsertChip(H("/Player", "Player", 1)); Type("fix this");
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();
            var savedText = _chipField.Text;

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            _chipField.Text = savedText;
            Type(" more");

            var (tj, _) = SimulateSend();
            Assert.IsNotNull(tj);
            StringAssert.Contains("[hierarchy:/Player", tj);
        }

        [Test]
        public void ReloadDuringComposition_ChipsAndTextPreserved()
        {
            InsertChip(H("/Player", "Player", 1)); Type("check");
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();
            var savedText = _chipField.Text;

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            _chipField.Text = savedText;

            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("/Player", _chipField.Model.Chips[0].Path);
            StringAssert.Contains("check", _chipField.Text);
        }

        [Test]
        public void ReloadDuringComposition_ThenTwoSends_Independent()
        {
            InsertChip(H("/Player", "Player", 1));
            var (paths, kindKeys) = _chipField.Model.SerializeForReload();

            _chipField.ClearChips(); _chipField.Text = "";
            _chipField.Model.RestoreFromReload(paths, kindKeys);
            Type("first"); SimulateSend();

            InsertChip(H("/Enemy", "Enemy", 2)); Type("second"); SimulateSend();

            // F13: pills are in msg-user-content
            var pills0 = ChatWindowAssertions.GetUserBubble(_container, 0).Q(className: "msg-user-content")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills0[0], ChipKindKeys.Hierarchy, "Player");

            var pills1 = ChatWindowAssertions.GetUserBubble(_container, 1).Q(className: "msg-user-content")
                .Query(className: "inline-chip-pill").ToList();
            ChatWindowAssertions.AssertPillContent(pills1[0], ChipKindKeys.Hierarchy, "Enemy");
        }
    }
}
