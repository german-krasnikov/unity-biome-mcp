// TDD — T-4.3b: ChatTranscript wiring for TaskChecklistCard.
// Tests that TaskCreate/TaskUpdate routes to a task-card instead of regular chips.
// Static state cleanup: ClearForNextTurn() in [TearDown] — same as TaskChecklistCardTests.
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatTranscriptTaskTests : UnityMcpTestBase
    {
        private VisualElement _container;
        private ChatTranscript _transcript;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _transcript = new ChatTranscript(
                _container, ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [TearDown]
        public void TearDown() => TaskChecklistCard.ClearForNextTurn();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ToolCallRecord MakeTaskCreateRec(string toolId,
            string subject = "Task 1: Fix")
        {
            var args = "{\"subject\":\"" + subject +
                       "\",\"description\":\"\",\"activeForm\":\"\"}";
            var result = "Task #1 created successfully: " + subject;
            return new ToolCallRecord("TaskCreate", toolId, args, result, true);
        }

        // ── Test 1: First TaskCreate creates exactly one card ─────────────────────

        [Test]
        public void FirstTaskCreate_CreatesOneCard()
        {
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");

            Assert.AreEqual(1, _container.Query(className: "task-card").ToList().Count,
                "First TaskCreate must add exactly one task-card to the container");
        }

        // ── Test 2: Second TaskCreate reuses the same card ────────────────────────

        [Test]
        public void SecondTaskCreate_SameCard_NotDuplicated()
        {
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id2");

            Assert.AreEqual(1, _container.Query(className: "task-card").ToList().Count,
                "Two TaskCreate calls in the same turn must share one task-card");
        }

        // ── Test 3: TaskUpdate routes to existing card, no new card ───────────────

        [Test]
        public void TaskUpdate_RoutesToSameCard()
        {
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");
            _transcript.AppendToolChip("TaskUpdate", ok: true, toolId: "id2");

            Assert.AreEqual(1, _container.Query(className: "task-card").ToList().Count,
                "TaskUpdate must not create a new task-card");
        }

        // ── Test 4: UpdateToolDetail routes TaskCreate to the card ────────────────

        [Test]
        public void UpdateToolDetail_TaskCreate_RoutesToCard()
        {
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");
            var rec = MakeTaskCreateRec("id1");

            Assert.DoesNotThrow(() => _transcript.UpdateToolDetail("id1", rec),
                "UpdateToolDetail must not throw for TaskCreate routing");

            Assert.IsNotNull(_container.Q(className: "task-row"),
                "After UpdateToolDetail(TaskCreate), the card must contain a .task-row");
        }

        // ── Test 5: UpdateToolDetail routes TaskUpdate to the card ────────────────
        // Discriminating: task routing returns before chip.userData = rec and before
        // CopyableText.Attach. If routing was absent, both would be set by the
        // ToolDetailBuilder fallback path, failing both assertions below.

        [Test]
        public void UpdateToolDetail_TaskUpdate_RoutesToCard()
        {
            LogAssert.ignoreFailingMessages = true; // unknown taskId produces a warning

            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");
            _transcript.AppendToolChip("TaskUpdate", ok: true, toolId: "id2");

            var updateRec = new ToolCallRecord("TaskUpdate", "id2",
                "{\"taskId\":\"999\",\"status\":\"completed\"}");

            _transcript.UpdateToolDetail("id2", updateRec);

            // Task routing returns early — chip.userData must NOT be set to the rec.
            var card = _container.Q(className: "task-card");
            Assert.IsNull(card.userData,
                "Task routing must return early — card.userData must remain null");

            // Task routing returns early — CopyableText.Attach not called, no copy-attached class.
            Assert.IsFalse(card.ClassListContains("copy-attached"),
                "Task routing must return early — copy-attached class must not be added");
        }

        // ── Test 6: FinalizeAssistant resets state so next turn gets fresh card ───

        [Test]
        public void FinalizeAssistant_ClearsTaskMap()
        {
            // Turn 1
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");
            _transcript.FinalizeAssistant();

            // Turn 2 — must create a second, independent task-card
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id2");

            Assert.AreEqual(2, _container.Query(className: "task-card").ToList().Count,
                "After FinalizeAssistant, a new TaskCreate must produce a second task-card");
        }

        // ── Test 7: While restoring, TaskCreate falls through to regular chip ─────

        [Test]
        public void AppendTaskTool_WhileRestoring_CreatesRegularChip()
        {
            // Inject _restoring = true via reflection (same approach as plan §T-4.3b)
            var fi = typeof(ChatTranscript)
                .GetField("_restoring", BindingFlags.NonPublic | BindingFlags.Instance);
            fi.SetValue(_transcript, true);

            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "id1");

            fi.SetValue(_transcript, false);

            Assert.AreEqual(0, _container.Query(className: "task-card").ToList().Count,
                "While restoring, TaskCreate must NOT create a task-card");
            Assert.IsNotNull(_container.Q(className: "tool-chip"),
                "While restoring, TaskCreate must create a regular tool-chip instead");
        }

        // ── Test 8: Mixed Bash + TaskCreate — both appear in container ────────────

        [Test]
        public void MixedScenario_BashAndTaskCreate_CorrectCount()
        {
            _transcript.AppendToolChip("Bash", ok: true, toolId: "bash1");
            _transcript.AppendToolChip("TaskCreate", ok: true, toolId: "task1");

            Assert.AreEqual(1, _container.Query(className: "task-card").ToList().Count,
                "Container must have exactly 1 task-card");
            Assert.IsNotNull(_container.Q(className: "tool-chip"),
                "Container must also contain the Bash tool-chip");
        }
    }
}
