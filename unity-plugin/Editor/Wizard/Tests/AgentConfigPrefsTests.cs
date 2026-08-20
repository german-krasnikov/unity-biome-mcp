using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AgentConfigPrefsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void IsFirstRun_WhenPrefAbsent_ReturnsTrue()
        {
            DeleteEditorPrefString(PrefKeys.EnabledAgentConfigs);
            Assert.IsTrue(AgentConfigPrefs.IsFirstRun);
        }

        [Test]
        public void IsFirstRun_AfterInitialize_ReturnsFalse()
        {
            DeleteEditorPrefString(PrefKeys.EnabledAgentConfigs);
            AgentConfigPrefs.InitializeFromDetected(new[] { "claude-code" });
            Assert.IsFalse(AgentConfigPrefs.IsFirstRun);
        }

        [Test]
        public void SetAndGet_RoundTrip_ReturnsSetKeys()
        {
            ProtectEditorPrefString(PrefKeys.EnabledAgentConfigs);
            AgentConfigPrefs.SetEnabledKeys(new[] { "claude-code", "cursor" });
            var result = AgentConfigPrefs.GetEnabledKeys();
            Assert.IsTrue(result.Contains("claude-code"));
            Assert.IsTrue(result.Contains("cursor"));
        }

        [Test]
        public void GetEnabledKeys_AfterEmptySet_ReturnsEmptyHashSet()
        {
            ProtectEditorPrefString(PrefKeys.EnabledAgentConfigs);
            AgentConfigPrefs.SetEnabledKeys(new string[0]);
            var result = AgentConfigPrefs.GetEnabledKeys();
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void InitializeFromDetected_PersistsKeys_SetsNotFirstRun()
        {
            DeleteEditorPrefString(PrefKeys.EnabledAgentConfigs);
            AgentConfigPrefs.InitializeFromDetected(new[] { "claude-code", "cursor" });
            Assert.IsFalse(AgentConfigPrefs.IsFirstRun);
            var keys = AgentConfigPrefs.GetEnabledKeys();
            Assert.IsTrue(keys.Contains("claude-code"));
            Assert.IsTrue(keys.Contains("cursor"));
        }
    }
}
