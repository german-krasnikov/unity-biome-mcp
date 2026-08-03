// Tests for cursor tracking and chip removal with @mention injection.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CursorAndRemoveTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Chip(string name)
            => new ChipData(ChipKindKeys.Hierarchy, "/" + name, name, 0);

        [Test]
        public void LastCursorPos_DefaultsToZero()
        {
            var field = new InlineChipField();
            Assert.AreEqual(0, field.LastCursorPos);
        }

        [Test]
        public void ClearChips_ResetsLastCursorPos()
        {
            var field = new InlineChipField();
            field.TextField.value = "hello";
            field.AddChip(Chip("A"));
            field.ClearChips();
            Assert.AreEqual(0, field.LastCursorPos);
        }

        // RemoveChipAt removes @mention text from TextField when present.
        [Test]
        public void RemoveChip_RemovesAtMentionFromText()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            // TextField now contains "@Player "
            field.RemoveChipAt(0);
            Assert.AreEqual(0, field.Model.Count);
            Assert.AreEqual("", field.Text);
        }

        // Removing one of two chips removes only that @mention.
        [Test]
        public void RemoveChip_TwoChips_RemovesFirstMentionFromText()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("B"));
            // TextField: "@A @B "
            field.RemoveChipAt(0);
            Assert.AreEqual(1, field.Model.Count);
            Assert.AreEqual("/B", field.Model.Chips[0].Path);
            StringAssert.DoesNotContain("@A", field.Text);
            StringAssert.Contains("@B", field.Text);
        }

        // Guard: if @mention not at stored offset (text was replaced), skip mutation.
        [Test]
        public void RemoveChip_NoMentionAtOffset_TextUnchanged()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("Player"));
            field.TextField.value = "something else"; // overwrite directly
            field.RemoveChipAt(0);
            Assert.AreEqual("something else", field.Text);
        }

        // Duplicate-name chips removed by index — correct entry removed.
        [Test]
        public void RemoveChip_DuplicateNames_RemovesModelEntry0()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            field.AddChip(Chip("A"));
            field.RemoveChipAt(0);
            Assert.AreEqual(1, field.Model.Count);
        }

        [Test]
        public void TwoChipsWithoutRefocus_SecondUsesLastCursorPos()
        {
            var field = new InlineChipField();
            field.AddChip(Chip("A"));
            // After AddChip, lastCursorPos is advanced past the injected @mention.
            Assert.AreEqual("@A ".Length, field.LastCursorPos,
                "LastCursorPos advanced past injected mention");
        }
    }
}
