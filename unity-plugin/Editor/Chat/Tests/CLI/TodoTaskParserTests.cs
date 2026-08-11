// TDD Phase 2.5 — TodoTaskParser. 15 NUnit cases (11 methods, 5 via TestCase).
// Empirical data: TaskCreate/TaskUpdate verified from 192 sessions (not TodoWrite).
// noEngineReferences: true in Parsers — no Unity types in impl.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class TodoTaskParserTests : UnityMcpTestBase
    {
        // Real argsJson shapes from empirical sessions.
        private const string Create1Json =
            "{\"subject\":\"Task 1: MCP → Biome ребрендинг UI\"," +
            "\"description\":\"Переименование...\"," +
            "\"activeForm\":\"...\"}";

        private const string Create2Json =
            "{\"subject\":\"Task 2: Исправить контекстное меню\"," +
            "\"description\":\"Fix prop context menu\",\"activeForm\":\"\"}";

        private const string Create3Json =
            "{\"subject\":\"Task 3: Расширение @-меню\"," +
            "\"description\":\"d\"}";

        private const string Update1InProgressJson =
            "{\"taskId\":\"1\",\"status\":\"in_progress\"}";

        private const string Update2CompletedJson =
            "{\"taskId\":\"2\",\"status\":\"completed\"}";

        // === TaskCreate ===

        [Test]
        public void Parse_TaskCreate_ExtractsSubject()
        {
            var r = TodoTaskParser.Parse("TaskCreate", Create1Json);
            Assert.AreEqual(TaskCallKind.Create, r.Kind);
            Assert.AreEqual("Task 1: MCP → Biome ребрендинг UI", r.Subject);
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void Parse_TaskCreate_ExtractsDescription()
        {
            var r = TodoTaskParser.Parse("TaskCreate", Create2Json);
            Assert.AreEqual("Fix prop context menu", r.Description);
        }

        [Test]
        public void Parse_TaskCreate_MissingActiveForm_IsValid()
        {
            var r = TodoTaskParser.Parse("TaskCreate", Create3Json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(TaskCallKind.Create, r.Kind);
        }

        // === TaskUpdate ===

        [Test]
        public void Parse_TaskUpdate_ExtractsTaskId()
        {
            var r = TodoTaskParser.Parse("TaskUpdate", Update1InProgressJson);
            Assert.AreEqual(TaskCallKind.Update, r.Kind);
            Assert.AreEqual("1", r.TaskId);
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void Parse_TaskUpdate_StatusInProgress()
        {
            var r = TodoTaskParser.Parse("TaskUpdate", Update1InProgressJson);
            Assert.AreEqual(TaskStatus.InProgress, r.Status);
        }

        [Test]
        public void Parse_TaskUpdate_StatusCompleted()
        {
            var r = TodoTaskParser.Parse("TaskUpdate", Update2CompletedJson);
            Assert.AreEqual(TaskStatus.Completed, r.Status);
        }

        [Test]
        public void Parse_TaskUpdate_WithOptionalDescription_IsValid()
        {
            var json = "{\"taskId\":\"1\",\"status\":\"in_progress\",\"description\":\"In progress\"}";
            var r = TodoTaskParser.Parse("TaskUpdate", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("1", r.TaskId);
        }

        // === TryExtractTaskId — 5 TestCase (Strategy A + B) ===

        [TestCase("Task #1 created successfully: Task 1: MCP → Biome ребрендинг UI", null, "1")]
        [TestCase("Task #2 created successfully: Task 2: Исправить контекстное меню", null, "2")]
        [TestCase("Task #3 created successfully: Task 3: Расширение @-меню в чате", null, "3")]
        [TestCase(null, "Task 1: MCP → Biome ребрендинг UI", "1")]
        [TestCase(null, "Task 2: Исправить контекстное меню", "2")]
        public void TryExtractTaskId_ReturnsId(string resultText, string subject, string expected)
        {
            Assert.AreEqual(expected, TodoTaskParser.TryExtractTaskId(resultText, subject));
        }

        [Test]
        public void TryExtractTaskId_NoMatch_ReturnsNull()
        {
            Assert.IsNull(TodoTaskParser.TryExtractTaskId("Something went wrong", "Do this thing"));
        }

        // === Error cases ===

        [Test]
        public void Parse_Null_IsValidFalse()
        {
            var r = TodoTaskParser.Parse("TaskCreate", null);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void Parse_TaskUpdate_UnknownStatus_DefaultsPending()
        {
            var json = "{\"taskId\":\"1\",\"status\":\"unknown_future\"}";
            var r = TodoTaskParser.Parse("TaskUpdate", json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(TaskStatus.Pending, r.Status);
        }
    }
}
