// TDD — InlineChipField tests (Wave 0).
// Element-tree construction asserts. No resolvedStyle/layout/live-panel dependence.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipFieldTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Chip(string path)
            => new ChipData(ChipKindKeys.Script, path, path, 0);

        // ── Constructor ───────────────────────────────────────────────────────

        [Test]
        public void Constructor_HasTextField()
        {
            var field = new InlineChipField();
            var tf = field.Q<TextField>();
            Assert.IsNotNull(tf, "InlineChipField must contain a TextField");
        }

        // ── AddChip ───────────────────────────────────────────────────────────

        [Test]
        public void AddChip_AddsPillToInternalPillRow()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));

            // outer: [pillRow, textField] — always exactly 2 direct children
            Assert.AreEqual(2, field.childCount);
            Assert.IsNotInstanceOf<TextField>(field[0], "first child must be pillRow, not TextField");
            Assert.IsInstanceOf<TextField>(field[1], "second child must be TextField");
            // pill lives inside pillRow
            Assert.AreEqual(1, field[0].childCount, "pillRow must contain 1 pill");
        }

        [Test]
        public void AddTwoChips_PillsInsidePillRow()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            // outer structure unchanged — still 2 direct children
            Assert.AreEqual(2, field.childCount);
            Assert.IsInstanceOf<TextField>(field[1]);
            // pills accumulate in pillRow
            Assert.AreEqual(2, field[0].childCount, "pillRow must contain 2 pills");
        }

        // ── RemoveChipAt ──────────────────────────────────────────────────────

        [Test]
        public void RemoveChip_RemovesPillFromPillRow()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            field.RemoveChipAt(0);

            Assert.AreEqual(1, field[0].childCount, "pillRow must contain 1 remaining pill");
            Assert.IsInstanceOf<TextField>(field[1]);
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [Test]
        public void Clear_RemovesAllPills()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));
            field.AddChip(Chip("Assets/C.cs"));

            field.ClearChips();

            // outer structure: pillRow (empty) + TextField — 2 direct children
            Assert.AreEqual(2, field.childCount);
            Assert.AreEqual(0, field[0].childCount, "pillRow must be empty after clear");
            Assert.IsInstanceOf<TextField>(field[1]);
        }

        // ── Text ──────────────────────────────────────────────────────────────

        [Test]
        public void Text_ReturnsTextFieldValue()
        {
            var field = new InlineChipField();
            // Access internal TextField via Q
            var tf = field.Q<TextField>();
            tf.value = "hello world";

            Assert.AreEqual("hello world", field.Text);
        }

        // ── PillRow visibility ────────────────────────────────────────────────

        [Test]
        public void Constructor_PillRowStartsHidden()
        {
            var field = new InlineChipField();
            Assert.AreEqual(DisplayStyle.None, field[0].style.display.value);
        }

        [Test]
        public void AddChip_PillRowBecomesVisible()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            Assert.AreEqual(DisplayStyle.Flex, field[0].style.display.value);
        }

        [Test]
        public void ClearChips_PillRowBecomesHidden()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.ClearChips();
            Assert.AreEqual(DisplayStyle.None, field[0].style.display.value);
        }

        // ── F20: no select-all on focus / mouse-up ────────────────────────────

        [Test]
        public void SelectAllOnFocus_IsFalse()
        {
            var field = new InlineChipField();
            Assert.IsFalse(field.TextField.selectAllOnFocus,
                "TextField must not select-all on focus (F20)");
        }

        [Test]
        public void SelectAllOnMouseUp_IsFalse()
        {
            var field = new InlineChipField();
            Assert.IsFalse(field.TextField.selectAllOnMouseUp,
                "TextField must not select-all on mouse-up (F20)");
        }

        // ── Model sync ────────────────────────────────────────────────────────

        [Test]
        public void Model_SyncsWithField()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Assets/A.cs"));
            field.AddChip(Chip("Assets/B.cs"));

            Assert.AreEqual(2, field.Model.Count);

            field.RemoveChipAt(0);
            Assert.AreEqual(1, field.Model.Count);

            field.ClearChips();
            Assert.AreEqual(0, field.Model.Count);
        }
    }
}
