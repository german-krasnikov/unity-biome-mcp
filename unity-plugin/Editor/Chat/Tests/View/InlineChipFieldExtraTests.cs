// TDD — InlineChipField additional tests (Wave 1).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipFieldExtraTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Chip(string path)
            => new ChipData(ChipKindKeys.Script, path, path, 0);

        [Test]
        public void AddFiveChips_PillRowHasFive()
        {
            var field = new InlineChipField();
            for (int i = 0; i < 5; i++) field.AddChip(Chip("Assets/" + i + ".cs"));
            Assert.AreEqual(5, field[0].childCount);
        }

        [Test]
        public void RemoveFirst_PillCountDecrements()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A")); field.AddChip(Chip("B")); field.AddChip(Chip("C"));
            field.RemoveChipAt(0);
            Assert.AreEqual(2, field[0].childCount);
        }

        [Test]
        public void RemoveLast_PillCountDecrements()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A")); field.AddChip(Chip("B")); field.AddChip(Chip("C"));
            field.RemoveChipAt(2);
            Assert.AreEqual(2, field[0].childCount);
        }

        [Test]
        public void RemoveMiddle_PillCountDecrements()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A")); field.AddChip(Chip("B")); field.AddChip(Chip("C"));
            field.RemoveChipAt(1);
            Assert.AreEqual(2, field[0].childCount);
            Assert.AreEqual(2, field.Model.Count);
            Assert.AreEqual("A", field.Model.Chips[0].Path);
            Assert.AreEqual("C", field.Model.Chips[1].Path);
        }

        [Test]
        public void ClearThenAdd_PillCountCorrect()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A")); field.AddChip(Chip("B")); field.AddChip(Chip("C"));
            field.ClearChips();
            field.AddChip(Chip("X")); field.AddChip(Chip("Y"));
            Assert.AreEqual(2, field[0].childCount);
            Assert.AreEqual(DisplayStyle.Flex, field[0].style.display.value);
        }

        [Test]
        public void RemoveChipAt_OutOfRange_NoCrash()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            Assert.DoesNotThrow(() => field.RemoveChipAt(-1));
            Assert.DoesNotThrow(() => field.RemoveChipAt(99));
            Assert.AreEqual(1, field[0].childCount);
        }

        [Test]
        public void RebuildFromModel_AfterRestore_PillsMatch()
        {
            var field = new InlineChipField();
            field.Model.RestoreFromReload(
                new[] { "/A", "/B", "/C" },
                new[] { ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy, ChipKindKeys.Hierarchy });
            field.RebuildFromModel();
            Assert.AreEqual(3, field[0].childCount);
            Assert.AreEqual(DisplayStyle.Flex, field[0].style.display.value);
        }
    }
}
