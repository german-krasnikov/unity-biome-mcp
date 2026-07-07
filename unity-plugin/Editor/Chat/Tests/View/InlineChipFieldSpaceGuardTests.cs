// TDD — F15c: Leading-space guard when adding a chip adjacent to non-space text.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipFieldSpaceGuardTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        private static ChipData Player()
            => new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);

        // ── AddChip leading-space guard ───────────────────────────────────────

        [Test]
        public void AddChip_AfterTextNoSpace_LeadingSpaceInserted()
        {
            var field = new InlineChipField();
            ChipViewTestHelpers.Type(field, "fix");
            field.AddChip(Player());
            StringAssert.Contains(" @Player", field.Text);
        }

        [Test]
        public void AddChip_AfterSpace_NoDoubleSpace()
        {
            var field = new InlineChipField();
            ChipViewTestHelpers.Type(field, "fix ");
            field.AddChip(Player());
            Assert.IsFalse(field.Text.Contains("  @"), "Should not have double space before @");
        }

        [Test]
        public void AddChip_EmptyField_NoLeadingSpace()
        {
            var field = new InlineChipField();
            field.AddChip(Player());
            Assert.IsTrue(field.Text.StartsWith("@Player"), $"Expected '@Player' at start, got: '{field.Text}'");
        }

        [Test]
        public void AddChip_AfterTextNoSpace_TextNotGlued()
        {
            var field = new InlineChipField();
            ChipViewTestHelpers.Type(field, "fix");
            field.AddChip(Player());
            // "fix@Player" must NOT appear — they must be separated by a space
            Assert.IsFalse(field.Text.Contains("fix@"), $"Text was glued: '{field.Text}'");
        }

        [Test]
        public void InsertChipAt_MidTextNoSpace_LeadingSpaceInserted()
        {
            var field = new InlineChipField();
            ChipViewTestHelpers.Type(field, "fixthis");
            // Insert at position 3 — after "fix", no space there
            field.InsertChipAt(3, Player());
            StringAssert.Contains(" @Player", field.Text);
        }

        [Test]
        public void AddChip_AfterText_RemoveChip_TextRestored()
        {
            var field = new InlineChipField();
            ChipViewTestHelpers.Type(field, "fix");
            field.AddChip(Player());
            Assert.AreEqual(1, field.Model.Count);
            field.RemoveChipAt(0);
            Assert.AreEqual(0, field.Model.Count);
            Assert.AreEqual("fix ", field.Text, $"After remove: '{field.Text}'");
        }
    }
}
