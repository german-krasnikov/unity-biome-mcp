// TDD — ChangeWatcher domain-reload persistence (Subtask 2 of ARCH-domain-reload-state-fixes).
// Tests simulate a domain reload: populate _changes, call Save(), wipe in-memory via
// SimulateDomainReloadForTest(), call Load(), assert state restored.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChangeWatcherReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup()
        {
            SessionState.EraseString(ChangeWatcher.SessionKey);
            // Clear in-memory state before each test
            ChangeWatcher.SimulateDomainReloadForTest();
        }

        [TearDown]
        public void Teardown()
        {
            SessionState.EraseString(ChangeWatcher.SessionKey);
            ChangeWatcher.SimulateDomainReloadForTest();
        }

        [Test]
        public void ChangeWatcher_SurvivesDomainReload_ChangesRestored()
        {
            ChangeWatcher.RecordMutation("MCP_SET_PROPERTY");
            ChangeWatcher.RecordMutation("MCP_CREATE_OBJECT");
            ChangeWatcher.RecordMutation("MCP_DELETE_OBJECT");

            ChangeWatcher.Save();
            ChangeWatcher.SimulateDomainReloadForTest();
            ChangeWatcher.Load();

            var result = ChangeWatcher.GetChanges(clear: false);
            StringAssert.Contains("MCP_SET_PROPERTY", result);
            StringAssert.Contains("MCP_CREATE_OBJECT", result);
            StringAssert.Contains("MCP_DELETE_OBJECT", result);
        }

        [Test]
        public void ChangeWatcher_Load_EmptySessionState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChangeWatcher.Load());
            Assert.AreEqual("NO_CHANGES", ChangeWatcher.GetChanges(clear: false));
        }

        [Test]
        public void ChangeWatcher_Reload_DoesNotExceedMaxChanges()
        {
            // Add more than MaxChanges entries (50) — ChangeWatcher itself caps at MaxChanges
            for (int i = 0; i < 60; i++)
                ChangeWatcher.RecordMutation($"mutation_{i}");

            ChangeWatcher.Save();
            ChangeWatcher.SimulateDomainReloadForTest();
            ChangeWatcher.Load();

            var result = ChangeWatcher.GetChanges(clear: false);
            var lines = result.Split('\n');
            Assert.LessOrEqual(lines.Length, ChangeWatcher.MaxChanges,
                "Loaded changes must not exceed MaxChanges");
        }

        [Test]
        public void ChangeWatcher_GetChanges_ClearFalse_ReturnsRepeatable()
        {
            ChangeWatcher.RecordMutation("CHECK_IDEMPOTENT");

            var first = ChangeWatcher.GetChanges(clear: false);
            var second = ChangeWatcher.GetChanges(clear: false);

            StringAssert.Contains("CHECK_IDEMPOTENT", first);
            Assert.AreEqual(first, second, "clear:false must not remove entries");
        }
    }
}
