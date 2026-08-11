// TDD — F21 gap-fill: serialization edge cases, cap behaviour, tool chip reload (P0-B), image path (P1).
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TranscriptReloadEdgeCaseTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetForTests(); ChipPillFactory.ColorResolver = null; }

        private ChatTranscript Make(out VisualElement c)
        {
            c = new VisualElement();
            return new ChatTranscript(c, ChatBlockRendererFactory.CreateDefault(null, null));
        }

        // Helper: depth-first search for element with CSS class
        private static VisualElement FindByClass(VisualElement root, string cls)
        {
            if (root.ClassListContains(cls)) return root;
            foreach (var child in root.Children())
            {
                var found = FindByClass(child, cls);
                if (found != null) return found;
            }
            return null;
        }

        // 1. cap: 210 messages → serialized max 200, tail preserved
        [Test]
        public void SerializeForReload_CapsToMaxMessages()
        {
            var t = Make(out _);
            for (int i = 0; i < 210; i++)
                t.AppendUserBubble($"msg{i}");

            var data = t.SerializeForReload();
            var entries = TranscriptSerializer.Deserialize(data);

            Assert.AreEqual(200, entries.Count, "must cap at 200");
            Assert.IsTrue(entries[199].Text.Contains("msg209"), "tail must be preserved");
            Assert.IsFalse(entries[0].Text.Contains("msg0"), "msg0 must be trimmed");
        }

        // 2. restore does not double entries
        [Test]
        public void RestoreFromReload_DoesNotDoubleEntries()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("a");
            t1.AppendUserBubble("b");
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data);
            var data2 = t2.SerializeForReload();

            var entries1 = TranscriptSerializer.Deserialize(data);
            var entries2 = TranscriptSerializer.Deserialize(data2);
            Assert.AreEqual(entries1.Count, entries2.Count,
                "double restore must not duplicate entries");
        }

        // 2b. RestoreFromReload on the SAME instance twice must not accumulate entries
        [Test]
        public void RestoreFromReload_SameInstance_CalledTwice_DoesNotDouble()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("x");
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data);
            t2.RestoreFromReload(data);

            var entries = TranscriptSerializer.Deserialize(t2.SerializeForReload());
            Assert.AreEqual(1, entries.Count,
                "calling RestoreFromReload twice on the same instance must not double entries");
        }

        // 3. serialize → restore → serialize is idempotent
        [Test]
        public void SerializeForReload_Idempotent_DoubleRoundTrip()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("hello");
            t1.AppendOrExtendAssistant("world");
            t1.FinalizeAssistant();
            var data1 = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data1);
            var data2 = t2.SerializeForReload();

            Assert.AreEqual(data1, data2, "double round-trip must be idempotent");
        }

        // 4. Serialize(null) returns empty string
        [Test]
        public void Serialize_NullList_ReturnsEmpty()
        {
            Assert.AreEqual("", TranscriptSerializer.Serialize(null));
        }

        // 5. Deserialize(null) returns empty list
        [Test]
        public void Deserialize_Null_ReturnsEmptyList()
        {
            Assert.AreEqual(0, TranscriptSerializer.Deserialize(null).Count);
        }

        // 6. unicode text survives round-trip
        [Test]
        public void SerializeForReload_UnicodeText_Survives()
        {
            const string unicode = "Привет 🌍 日本語";
            var t1 = Make(out _);
            t1.AppendOrExtendAssistant(unicode);
            t1.FinalizeAssistant();
            var data = t1.SerializeForReload();

            var entries = TranscriptSerializer.Deserialize(data);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(unicode, entries[0].Text, "unicode must survive round-trip");
        }

        // 7. SerializeChips(null) returns null
        [Test]
        public void SerializeChips_Null_ReturnsNull()
        {
            Assert.IsNull(TranscriptSerializer.SerializeChips(null));
        }

        // 8. DeserializeChips(null) returns null
        [Test]
        public void DeserializeChips_Null_ReturnsNull()
        {
            Assert.IsNull(TranscriptSerializer.DeserializeChips(null));
        }

        // 9. _entries list is capped at MaxMessages
        [Test]
        public void Entries_CappedAtMaxMessages_WhenContainerEvicts()
        {
            var t = Make(out _);
            for (int i = 0; i < 210; i++)
                t.AppendUserBubble($"msg{i}");

            var entriesField = typeof(ChatTranscript)
                .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            var entries = (List<TranscriptEntry>)entriesField.GetValue(t);
            Assert.LessOrEqual(entries.Count, 200, "_entries must be capped at MaxMessages (200)");

            var data   = t.SerializeForReload();
            var serial = TranscriptSerializer.Deserialize(data);
            Assert.AreEqual(200, serial.Count, "serialized must cap at 200");
        }

        // ── P0-B: Tool chip reload survival ──────────────────────────────────────

        // 10. Tool chip after serialize/restore appears in DOM
        [Test]
        public void ToolChip_SerializeDeserialize_RoundTrip()
        {
            var t1 = Make(out _);
            t1.AppendToolChip("read_file", ok: true, toolId: "tool-1");
            var data = t1.SerializeForReload();

            Assert.IsNotEmpty(data, "tool chip must produce serialized data");

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            Assert.IsNotNull(FindByClass(c2, "tool-chip"),
                "tool-chip element must be present after restore");
        }

        // 11. Order: user → tool → assistant preserved after restore
        [Test]
        public void ToolChip_RestoreOrder_MatchesOriginal()
        {
            var t1 = Make(out var c1);
            t1.AppendUserBubble("question");
            t1.AppendToolChip("read_file", ok: true, toolId: "t1");
            t1.AppendOrExtendAssistant("answer");
            t1.FinalizeAssistant();
            var data = t1.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            Assert.AreEqual(c1.childCount, c2.childCount,
                "child count must match original after restore");
        }

        // 12. _restoring guard: AppendToolChip during restore does not double-add entries
        [Test]
        public void ToolChip_NoDoubleAdd_DuringRestore()
        {
            var t1 = Make(out _);
            t1.AppendToolChip("write_file", ok: true, toolId: "t2");
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data);
            var data2 = t2.SerializeForReload();

            var e1 = TranscriptSerializer.Deserialize(data);
            var e2 = TranscriptSerializer.Deserialize(data2);
            Assert.AreEqual(e1.Count, e2.Count, "restore must not double entries");
        }

        // 13. Failed tool chip (ok=false) preserves error status
        [Test]
        public void ToolChip_OkFalse_PreservedOnRoundTrip()
        {
            var t1 = Make(out _);
            t1.AppendToolChip("bad_tool", ok: false, toolId: "t3");
            var data = t1.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);
            Assert.IsNotNull(FindByClass(c2, "tool-chip--error"),
                "error chip must be restored with error class");
        }

        // 14. Backward compat: data with unknown future kind (e.g. 9) is skipped, no crash
        [Test]
        public void ToolChip_BackwardCompat_UnknownKind_Skipped()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("hi");
            var data = t1.SerializeForReload();
            // Inject an unknown-kind line (kind=9) — must be silently skipped
            data += "9|aGk=||||\n";

            var t2 = Make(out var c2);
            Assert.DoesNotThrow(() => t2.RestoreFromReload(data));
            Assert.AreEqual(1, c2.childCount, "unknown kind must be skipped, not crash");
        }

        // ── P1: Image path persistence ────────────────────────────────────────────

        // 15. Image path on user bubble survives serialize/deserialize
        [Test]
        public void ImagePath_SerializeDeserialize_RoundTrip()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("see image", chips: null, imagePath: "/tmp/test.png");
            var data = t1.SerializeForReload();

            var entries = TranscriptSerializer.Deserialize(data);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("/tmp/test.png", entries[0].ImagePath,
                "image path must survive serialization round-trip");
        }
    }
}
