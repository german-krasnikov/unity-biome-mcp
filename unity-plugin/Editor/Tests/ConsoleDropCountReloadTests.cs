// TDD — ConsoleCapture._droppedProblemCount domain-reload persistence (Subtask 4).
// Bug: _droppedProblemCount resets to 0 on reload; [+N dropped] suffix disappears.
// Fix: persist via SessionState, restore in static ctor via RestoreDroppedCountFromSessionState.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConsoleDropCountReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup()
        {
            ConsoleCapture.Clear();
            SessionState.EraseString("MCP_DroppedProblemCount");
        }

        [TearDown]
        public void Teardown()
        {
            ConsoleCapture.Clear();
            SessionState.EraseString("MCP_DroppedProblemCount");
        }

        [Test]
        public void DroppedProblemCount_SurvivesDomainReload_RestoredCorrectly()
        {
            // Overflow ConsoleProblemPersistence.MAX_PERSISTED_PROBLEMS (20) to get 1 dropped
            for (int i = 0; i < 21; i++)
                ConsoleCapture.InjectForTest($"error {i}", LogType.Error);

            // Simulate domain reload: wipes in-memory state + resets _droppedProblemCount
            ConsoleCapture.SimulateDomainReloadForTest();

            // Restore what the static ctor would do post-reload
            ConsoleCapture.RestoreDroppedCountFromSessionState();

            var result = ConsoleCapture.GetLogs();
            StringAssert.Contains("dropped]", result,
                "Dropped count must survive domain reload and appear in GetLogs output");
        }

        [Test]
        public void DroppedProblemCount_ConsoleClear_ResetsSessionState()
        {
            for (int i = 0; i < 21; i++)
                ConsoleCapture.InjectForTest($"error {i}", LogType.Error);

            ConsoleCapture.SimulateUnityConsoleClearForTest();

            // SessionState must be cleared — a post-reload restore would read 0
            var raw = SessionState.GetString("MCP_DroppedProblemCount", "not_set");
            Assert.IsTrue(raw == "0" || raw == "" || raw == "not_set",
                "SessionState dropped count must be cleared after console clear");
            var result = ConsoleCapture.GetLogs();
            StringAssert.DoesNotContain("dropped]", result,
                "No dropped suffix after console clear");
        }
    }
}
