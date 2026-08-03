// TDD — ChipSequence additional tests (Wave 1).
using NUnit.Framework;
using UnityMCP.Editor.Chat;
using static UnityMCP.Editor.Chat.Tests.ChipTestHelpers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSequenceExtraTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private InlineChipField _chipField;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _chipField = new InlineChipField();
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

        // ── Cursor placement ──────────────────────────────────────────────────

        [Test]
        public void InsertChip_CursorBetweenWords_ChipInModel()
        {
            _chipField.Text = "hello world"; SetCursor(6);
            InsertChip(H("/Player", "Player"));
            Assert.AreEqual(1, _chipField.Model.Count);
            StringAssert.Contains("@Player", _chipField.Text);
            StringAssert.Contains("hello", _chipField.Text);
        }

        [Test]
        public void InsertChip_AfterAnotherChip_BothPresent()
        {
            InsertChip(H("/A", "A", 1));
            InsertChip(H("/B", "B", 2));
            Assert.AreEqual(2, _chipField.Model.Count);
            Assert.AreEqual("A", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("B", _chipField.Model.Chips[1].DisplayName);
        }

        [Test]
        public void InsertChip_CursorClampedToTextLength()
        {
            _chipField.Text = "hi"; SetCursor(999);
            Assert.DoesNotThrow(() => InsertChip(H("/X", "X")));
            Assert.AreEqual(1, _chipField.Model.Count);
        }

        // ── Multiple chips ────────────────────────────────────────────────────

        [Test]
        public void FiveChipsInterleaved_ModelOrder()
        {
            for (int i = 1; i <= 5; i++)
            {
                Type("t" + i + " ");
                InsertChip(H("/N" + i, "N" + i, i));
            }
            Assert.AreEqual(5, _chipField.Model.Count);
            for (int i = 0; i < 5; i++) Assert.AreEqual("N" + (i + 1), _chipField.Model.Chips[i].DisplayName);
        }

        // ── No chips ──────────────────────────────────────────────────────────

        [Test]
        public void TextOnly_NoChips_ModelEmpty()
        {
            Type("just text");
            Assert.AreEqual(0, _chipField.Model.Count);
        }

        // ── Clear then re-insert ──────────────────────────────────────────────

        [Test]
        public void ChipThenClear_ThenChipTextChip_FreshSequence()
        {
            InsertChip(H("/A", "A", 1));
            _chipField.ClearChips();
            _chipField.Text = "";
            SetCursor(0);

            InsertChip(H("/B", "B", 2));
            Type("mid ");
            InsertChip(H("/C", "C", 3));

            Assert.AreEqual(2, _chipField.Model.Count);
            Assert.AreEqual("B", _chipField.Model.Chips[0].DisplayName);
            Assert.AreEqual("C", _chipField.Model.Chips[1].DisplayName);
            StringAssert.DoesNotContain("@A", _chipField.Text);
        }

        // ── @mention injection: AddChip injects @Name into TextField ────────

        [Test]
        public void AddChip_TextFieldContainsAtMention()
        {
            InsertChip(H("/Player", "Player", 1));
            StringAssert.Contains("@Player", _chipField.Text);
        }

        [Test]
        public void AddChip_OnExistingText_InjectsAtMentionAtCursor()
        {
            _chipField.Text = "fix health";
            SetCursor(3);
            InsertChip(H("/Player", "Player", 1));
            StringAssert.Contains("@Player", _chipField.Text);
            StringAssert.Contains("fix", _chipField.Text);
        }

        [Test]
        public void MultipleAddChips_TextAccumulatesAtMentions()
        {
            Type("hello ");
            InsertChip(H("/A", "A", 1));
            StringAssert.Contains("@A", _chipField.Text);
            Type("world ");
            InsertChip(H("/B", "B", 2));
            StringAssert.Contains("@A", _chipField.Text);
            StringAssert.Contains("@B", _chipField.Text);
            InsertChip(H("/C", "C", 3));
            StringAssert.Contains("@C", _chipField.Text);
        }

        [Test]
        public void AddChip_WithSpaceInName_AtMentionPresent()
        {
            InsertChip(H("/Player Controller", "Player Controller", 1));
            StringAssert.Contains("@Player Controller", _chipField.Text);
        }
    }
}
