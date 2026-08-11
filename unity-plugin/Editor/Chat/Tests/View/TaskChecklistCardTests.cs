// TDD — T-4.3a: TaskChecklistCard tests.
// Canonical pattern: construct directly, query by CSS class, no resolvedStyle.
// Static state cleanup: ClearForNextTurn() in [TearDown] (plan requirement).
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TaskChecklistCardTests : UnityMcpTestBase
    {
        [TearDown]
        public void TearDownTaskCard() => TaskChecklistCard.ClearForNextTurn();

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ToolCallRecord MakeCreateRec(
            string toolId, string subject, string resultText = null)
        {
            var args = "{\"subject\":\"" + subject +
                       "\",\"description\":\"\",\"activeForm\":\"\"}";
            return resultText != null
                ? new ToolCallRecord("TaskCreate", toolId, args, resultText, true)
                : new ToolCallRecord("TaskCreate", toolId, args);
        }

        private static ToolCallRecord MakeUpdateRec(string taskId, string status)
        {
            var args = "{\"taskId\":\"" + taskId + "\",\"status\":\"" + status + "\"}";
            return new ToolCallRecord("TaskUpdate", "upd-" + taskId, args);
        }

        // ── Test 1: Create returns element with task-card class ───────────────────

        [Test]
        public void Create_HasTaskCardClass()
        {
            var card = TaskChecklistCard.Create();
            Assert.IsTrue(card.ClassListContains(TaskChecklistCard.CardClass),
                "Create() must return a VisualElement with 'task-card' class");
        }

        // ── Test 2: Null ArgsJson produces no rows ────────────────────────────────

        [Test]
        public void OnTaskCreate_NullArgs_NoRows()
        {
            var card = TaskChecklistCard.Create();
            var rec = new ToolCallRecord("TaskCreate", "t1", null);

            TaskChecklistCard.OnTaskCreate(card, rec);

            Assert.IsNull(card.Q(className: "task-row"),
                "Null argsJson must produce no .task-row elements");
        }

        // ── Test 3: Valid args adds a row ─────────────────────────────────────────

        [Test]
        public void OnTaskCreate_ValidArgs_AddsRow()
        {
            var card = TaskChecklistCard.Create();

            TaskChecklistCard.OnTaskCreate(card,
                MakeCreateRec("t1", "Task 1: Fix"));

            Assert.IsNotNull(card.Q(className: "task-row"),
                "Valid argsJson must produce a .task-row element");
        }

        // ── Test 4: Subject text appears in the row label ─────────────────────────

        [Test]
        public void OnTaskCreate_SubjectInRow()
        {
            var card = TaskChecklistCard.Create();

            TaskChecklistCard.OnTaskCreate(card,
                MakeCreateRec("t1", "Task 1: Fix"));

            var textLabel = card.Q<Label>(className: "task-text");
            Assert.IsNotNull(textLabel, ".task-text Label must exist");
            StringAssert.Contains("Task 1: Fix", textLabel.text,
                ".task-text must contain the subject string");
        }

        // ── Test 5: Two separate calls produce two rows ───────────────────────────

        [Test]
        public void OnTaskCreate_Twice_TwoRows()
        {
            var card = TaskChecklistCard.Create();

            TaskChecklistCard.OnTaskCreate(card,
                MakeCreateRec("t1", "Task 1: First"));
            TaskChecklistCard.OnTaskCreate(card,
                MakeCreateRec("t2", "Task 2: Second"));

            var rows = card.Query(className: "task-row").ToList();
            Assert.AreEqual(2, rows.Count,
                "Two distinct TaskCreate calls must produce exactly 2 .task-row elements");
        }

        // ── Test 6: TaskUpdate with completed status updates the icon ─────────────

        [Test]
        public void OnTaskUpdate_CompletedStatus_UpdatesIcon()
        {
            var card = TaskChecklistCard.Create();

            // Subject "Task 1: Fix" → strategy B extracts taskId "1"
            // ResultText also provides taskId "1" via strategy A
            TaskChecklistCard.OnTaskCreate(card,
                MakeCreateRec("t1", "Task 1: Fix",
                    "Task #1 created successfully: Task 1: Fix"));

            TaskChecklistCard.OnTaskUpdate(card, MakeUpdateRec("1", "completed"));

            var statusLabel = card.Q<Label>(className: "task-status");
            Assert.IsNotNull(statusLabel, ".task-status Label must exist");
            Assert.AreEqual("✓", statusLabel.text,
                "Completed status must set the icon to '✓'");
        }

        // ── Test 7: TaskUpdate with unknown taskId logs warning, no new row ────────

        [Test]
        public void OnTaskUpdate_UnknownTaskId_NoRowAdded()
        {
            LogAssert.ignoreFailingMessages = true;
            var card = TaskChecklistCard.Create();

            TaskChecklistCard.OnTaskUpdate(card, MakeUpdateRec("999", "completed"));

            Assert.IsNull(card.Q(className: "task-row"),
                "Unknown taskId must not add a new .task-row element");
        }

        // ── Test 8: ClearForNextTurn resets static state for a fresh card ─────────

        [Test]
        public void ClearForNextTurn_StartsFresh()
        {
            var card1 = TaskChecklistCard.Create();
            TaskChecklistCard.OnTaskCreate(card1,
                MakeCreateRec("t1", "Task 1: Old"));

            // TearDown will also call ClearForNextTurn, but we test it inline here.
            TaskChecklistCard.ClearForNextTurn();

            var card2 = TaskChecklistCard.Create();
            TaskChecklistCard.OnTaskCreate(card2,
                MakeCreateRec("t1", "Task 1: New")); // reuse same toolId — must not collide

            var rows = card2.Query(className: "task-row").ToList();
            Assert.AreEqual(1, rows.Count,
                "After ClearForNextTurn, a new card must start with exactly 1 row");
        }
    }
}
