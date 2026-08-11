// Phase 5 Integration Tests — ReplaceMentionRangeWithChip on InlineChipField.
// EditMode only — no MCPChatWindow instantiation (EditorWindow requires GUI panel).
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetForTests();

        private static ChipData Cam()
            => new ChipData(ChipKindKeys.Hierarchy, "/Camera", "Camera", 0);

        private static ChipData Light()
            => new ChipData(ChipKindKeys.Hierarchy, "/Light", "Light", 0);

        private static ChipData Script()
            => new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo", 0);

        // ── 1. Chip inserted into model after replace ─────────────────────────

        [Test]
        public void ReplaceMentionRangeWithChip_InsertsChip()
        {
            var field = new InlineChipField();
            field.Text = "@Camera";
            ChipViewTestHelpers.SetCursor(field, field.Text.Length);

            field.ReplaceMentionRangeWithChip(0, 6, Cam()); // '@'=0, query="Camera"(6 chars)

            Assert.AreEqual(1, field.Model.Count, "model must have 1 chip after replace");
        }

        // ── 2. '@' + query text removed from TextField ────────────────────────

        [Test]
        public void ReplaceMentionRangeWithChip_RemovesAtQuery()
        {
            var field = new InlineChipField();
            field.Text = "@Camera rest";
            ChipViewTestHelpers.SetCursor(field, 7); // cursor right after "Camera"

            field.ReplaceMentionRangeWithChip(0, 6, Cam());

            // "@Camera" removed (7 chars), replaced by "@Camera " mention text
            // The remaining " rest" should still be present
            Assert.IsTrue(field.Text.Contains("rest"),
                "text after '@Camera' must be preserved; got: " + field.Text);
            // raw "@Camera" typed text gone — it was replaced by the chip mention reinjection
            // The chip mention itself is "@Camera " so the original "@Camera" isn't literally "gone"
            // but it IS the chip mention now. Verify no double @Camera besides the chip mention.
            // Count "@Camera" occurrences — should be exactly 1 (the chip mention).
            int count = 0;
            int idx = 0;
            while ((idx = field.Text.IndexOf("@Camera", idx)) >= 0) { count++; idx++; }
            Assert.AreEqual(1, count, "exactly one @Camera in text (the chip mention); got: " + field.Text);
        }

        // ── 3. Pre-existing chips survive ────────────────────────────────────

        [Test]
        public void ReplaceMentionRangeWithChip_PreservesExistingChips()
        {
            var field = new InlineChipField();
            // Add a chip first via AddChip (inserts "@Foo " at offset 0)
            field.AddChip(Script());
            // Now the text is "@Foo " (5 chars), model has 1 chip at offset 0.
            // Type "@Camera" after the existing chip mention
            int atPos = field.Text.Length;
            field.Text = field.Text + "@Camera";
            ChipViewTestHelpers.SetCursor(field, field.Text.Length);

            field.ReplaceMentionRangeWithChip(atPos, 6, Cam());

            Assert.AreEqual(2, field.Model.Count, "model must have 2 chips; got: " + field.Model.Count);
        }

        // ── 4. Invalid range: no crash ────────────────────────────────────────

        [Test]
        public void ReplaceMentionRangeWithChip_InvalidRange_NoOp()
        {
            var field = new InlineChipField();
            field.Text = "hello";

            Assert.DoesNotThrow(() => field.ReplaceMentionRangeWithChip(-1, 3, Cam()));
            Assert.DoesNotThrow(() => field.ReplaceMentionRangeWithChip(10, 3, Cam()));
            Assert.AreEqual(0, field.Model.Count, "no chips inserted on invalid range");
            Assert.AreEqual("hello", field.Text, "text unchanged on invalid range");
        }

        // ── 5. '@' at start, trailing text preserved ──────────────────────────

        [Test]
        public void ReplaceMentionRangeWithChip_AtStartOfText_TrailingPreserved()
        {
            var field = new InlineChipField();
            field.Text = "@Cam rest of text";
            ChipViewTestHelpers.SetCursor(field, 4);

            field.ReplaceMentionRangeWithChip(0, 3, Cam()); // '@'=0, query="Cam"(3 chars)

            Assert.AreEqual(1, field.Model.Count, "chip inserted");
            Assert.IsTrue(field.Text.Contains("rest of text"),
                "trailing text preserved; got: " + field.Text);
        }
    }
}
