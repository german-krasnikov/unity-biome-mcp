// Integration tests for chip-text-chip SEQUENCE — insertion & model state.
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSequenceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private InlineChipField _chipField;
        private ChipConfig      _cfg;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            _chipField = new InlineChipField();
            _cfg       = new ChipConfig();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }

        private void InsertChip(ChipData c) => ChipViewTestHelpers.InsertChip(_chipField, c);
        private void SetCursor(int p) => ChipViewTestHelpers.SetCursor(_chipField, p);
        private void Type(string t) => ChipViewTestHelpers.Type(_chipField, t);

        // ── Single chip insertion ─────────────────────────────────────────────

        [Test]
        public void InsertChip_AtEmptyField_ChipInModel()
        {
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("/Player", _chipField.Model.Chips[0].Path);
            Assert.AreEqual("@Player ", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtEndOfText_ChipInModel()
        {
            _chipField.Text = "hello "; SetCursor(6);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("hello @Player ", _chipField.Text);
        }

        [Test]
        public void InsertChip_AtBeginning_ChipInModel()
        {
            _chipField.Text = "world"; SetCursor(0);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("@Player world", _chipField.Text);
        }

        [Test]
        public void InsertChip_InMiddleOfText_ChipInModel()
        {
            _chipField.Text = "hello world"; SetCursor(6);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("hello @Player world", _chipField.Text);
        }

        [Test]
        public void InsertChip_CursorAtZero_ChipAtOffsetZero()
        {
            _chipField.Text = "tail"; SetCursor(0);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            Assert.AreEqual("@Player tail", _chipField.Text);
        }

        [Test]
        public void InsertChip_ThenModifyText_ChipStillInModel()
        {
            InsertChip(H("/Player", "Player", 1));
            _chipField.Text = "completely different text";
            Assert.AreEqual(1, _chipField.Model.Count);
        }

        // ── chip → text → chip sequence ───────────────────────────────────────

        [Test]
        public void ChipTextChip_SequencePreserved()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            Assert.AreEqual(2, _chipField.Model.Count);
            StringAssert.Contains("@Player", _chipField.Text);
            StringAssert.Contains("@Enemy", _chipField.Text);
            StringAssert.Contains("text", _chipField.Text);
        }

        [Test]
        public void ChipTextChip_ModelOrderMatchesInsertionOrder()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            Assert.AreEqual("Player", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("Enemy",  _chipField.Model.Chips[1].DisplayName);
        }

        [Test]
        public void ChipTextChip_PillRowHasTwoPills()
        {
            InsertChip(H("/Player", "Player", 1));
            Type(" text ");
            InsertChip(H("/Enemy", "Enemy", 2));
            ChatWindowAssertions.AssertChipCount(_chipField, 2);
        }

        // ── text → chip → text → chip → text ─────────────────────────────────

        [Test]
        public void TextChipTextChipText_FullSequence()
        {
            Type("start ");
            InsertChip(H("/Player", "Player", 1));
            Type(" middle ");
            InsertChip(H("/Enemy", "Enemy", 2));
            Type(" end");
            Assert.AreEqual(2, _chipField.Model.Count);
            StringAssert.Contains("start", _chipField.Text);
            StringAssert.Contains("@Player", _chipField.Text);
            StringAssert.Contains("middle", _chipField.Text);
            StringAssert.Contains("@Enemy", _chipField.Text);
            StringAssert.Contains("end", _chipField.Text);
        }
    }
}
