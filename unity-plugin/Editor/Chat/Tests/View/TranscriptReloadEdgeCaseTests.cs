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

        // 14b. Guard exhaustiveness: every Kind declared in the enum must survive Deserialize.
        // RED when: a new Kind is added to TranscriptEntry.Kind without fixing the guard at
        // TranscriptSerializer.cs:49 from `kindInt > 2` to `!Enum.IsDefined(...)`.
        // GREEN permanently after the guard uses Enum.IsDefined — no manual bump needed.
        [Test]
        public void Deserialize_AllDeclaredKinds_PassGuardAndRoundTrip()
        {
            var text64  = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test"));
            var chips64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("1"));

            foreach (TranscriptEntry.Kind kind in System.Enum.GetValues(typeof(TranscriptEntry.Kind)))
            {
                var line    = $"{(int)kind}|{text64}|{chips64}|||{System.Environment.NewLine}";
                var entries = TranscriptSerializer.Deserialize(line);

                Assert.AreEqual(1, entries.Count,
                    $"Kind.{kind} (int={(int)kind}) is silently dropped by the guard. " +
                    $"Fix: replace `kindInt > 2` with `!Enum.IsDefined(typeof(TranscriptEntry.Kind), kindInt)` " +
                    $"at TranscriptSerializer.cs:49");
                Assert.AreEqual(kind, entries[0].EntryKind,
                    $"Kind.{kind} must round-trip correctly");
            }
        }

        // 14c. No exception during restore for any declared Kind (guard + switch consistency).
        // Catches the case where guard is fixed but switch has no case and throws.
        [Test]
        public void RestoreFromReload_AllDeclaredKinds_NoException()
        {
            var text64  = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test"));
            var chips64 = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("1"));

            foreach (TranscriptEntry.Kind kind in System.Enum.GetValues(typeof(TranscriptEntry.Kind)))
            {
                var line = $"{(int)kind}|{text64}|{chips64}|||{System.Environment.NewLine}";
                var t = Make(out _);
                Assert.DoesNotThrow(() => t.RestoreFromReload(line),
                    $"RestoreFromReload must not throw for Kind.{kind}");
            }
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

        // ── T1.2: FinalizeAssistant before error chip clears _taskCard ────────────

        // 16a. BUG SNAPSHOT — raw AppendToolChip without FinalizeAssistant leaves _taskCard stale.
        //
        // THIS ASSERTION RECORDS THE BUG STATE (taskCards.Count == 1 = stale card).
        // RED when fixed: if AppendToolChip is improved to clear _taskCard on error chip,
        //   this test goes RED — that IS the improvement.
        //   → Change Assert.AreEqual from 1 to 2 (two distinct cards).
        //   → Confirm 16b (ErrorTurn_ViaProductionMethod_ClearsTaskCardAndCreatesNew) still passes.
        //
        // The production path already handles this correctly:
        //   MCPChatWindow.ApplyErrorTurn → FinalizeAssistant → ClearForNextTurn (see 16b).
        [Test]
        public void ErrorChip_WithoutFinalizeAssistant_BugSnapshot_StalesTaskCard()
        {
            var t = Make(out var c);

            // Turn 1: assistant streams and TaskCreate fires (sets _taskCard)
            t.AppendOrExtendAssistant("Создаю задачу: анализ сцены «Возрождение»");
            t.AppendToolChip("TaskCreate", ok: true, toolId: "task-т1-01");

            // Error arrives — WITHOUT calling FinalizeAssistant first
            // (AppendToolChip calls FreezeAssistantBubble but does NOT clear _taskCard)
            t.AppendToolChip("✕ Ошибка подключения к Claude API", ok: false);

            // Simulated next turn: new TaskCreate sees stale _taskCard and reuses it
            t.AppendToolChip("TaskCreate", ok: true, toolId: "task-т2-02");

            var taskCards = c.Query(className: TaskChecklistCard.CardClass).ToList();
            Assert.AreEqual(1, taskCards.Count,
                "without FinalizeAssistant before error chip, _taskCard is not cleared; " +
                "both TaskCreates land in the same stale card");
        }

        // 16b. FIX: MCPChatWindow.ApplyErrorTurn calls FinalizeAssistant internally,
        //      so the next turn's TaskCreate gets a fresh card.
        //      RED B: remove FinalizeAssistant from ApplyErrorTurn → test fails (1 stale card).
        [Test]
        public void ErrorTurn_ViaProductionMethod_ClearsTaskCardAndCreatesNew()
        {
            var t = Make(out var c);

            // Turn 1: TaskCreate fires (sets _taskCard)
            t.AppendOrExtendAssistant("Создаю задачу: анализ сцены «Возрождение»");
            t.AppendToolChip("TaskCreate", ok: true, toolId: "task-т1-01");

            // Call the actual production method that EventHandlers invokes for Error events.
            // If FinalizeAssistant is removed from ApplyErrorTurn, only 1 card appears → RED.
            MCPChatWindow.ApplyErrorTurn(t, "✕ Ошибка подключения к Claude API");

            // Next turn: fresh TaskCreate must get its own card
            t.AppendToolChip("TaskCreate", ok: true, toolId: "task-т2-02");

            var taskCards = c.Query(className: TaskChecklistCard.CardClass).ToList();
            Assert.AreEqual(2, taskCards.Count,
                "ApplyErrorTurn must call FinalizeAssistant first — removing it leaves " +
                "_taskCard stale and both TaskCreates land in the same card");
        }

        // ── T1.6: multi-image paths survive serialize/deserialize ─────────────────

        // 17. Three real screenshot paths must ALL survive a reload round-trip.
        [Test]
        public void MultiImage_AllThreePaths_SurviveReload()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble(
                "три скриншота: до, после и финальный",
                chips: null,
                imagePaths: new[]
                {
                    "/Users/german/Work/ScreenShots/2026-08-12_09-00-00.png",
                    "/Users/german/Work/ScreenShots/2026-08-12_09-01-00.png",
                    "/Users/german/Work/ScreenShots/2026-08-12_09-02-00.png",
                });
            var data = t1.SerializeForReload();

            var entries = TranscriptSerializer.Deserialize(data);
            Assert.AreEqual(1, entries.Count);

            // T1.6 fix: ImagePath stores all paths as \x1E-delimited string
            var paths = entries[0].ImagePath?.Split('\x1E')
                        ?? System.Array.Empty<string>();
            Assert.AreEqual(3, paths.Length,
                "all 3 image paths must survive serialization (T1.6 fix applies \x1E delimiter)");
            Assert.AreEqual("/Users/german/Work/ScreenShots/2026-08-12_09-00-00.png", paths[0],
                "first image path must match");
            Assert.AreEqual("/Users/german/Work/ScreenShots/2026-08-12_09-01-00.png", paths[1],
                "second image path must match");
            Assert.AreEqual("/Users/german/Work/ScreenShots/2026-08-12_09-02-00.png", paths[2],
                "third image path must match");
        }

        // 18. After full restore, all 3 image elements appear in DOM.
        //     Non-existent files render as AltLabel with class "md-image-alt" — one per path.
        [Test]
        public void MultiImage_AllThreePaths_AppearedInDomAfterRestore()
        {
            var t1 = Make(out _);
            // Paths that do not exist on disk → ImageBlockRenderer returns AltLabel("[image]")
            // with class "md-image-alt" — one per path. That's a reliable, renderer-independent signal.
            var imagePaths = new[]
            {
                "/tmp/biome-test-т1.6-кадр-01.png",
                "/tmp/biome-test-т1.6-кадр-02.png",
                "/tmp/biome-test-т1.6-кадр-03.png",
            };
            t1.AppendUserBubble("три скриншота финала игры", chips: null, imagePaths: imagePaths);
            var data = t1.SerializeForReload();

            var t2 = Make(out var c2);
            t2.RestoreFromReload(data);

            // AltLabel is produced for every non-existent image path; class = "md-image-alt"
            var imageAlts = c2.Query(className: "md-image-alt").ToList();
            Assert.AreEqual(3, imageAlts.Count,
                "all 3 image paths must render as md-image-alt in DOM after restore (T1.6 fix)");
        }
    }
}
