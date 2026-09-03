using System;
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

        // ── DEV-32 (ARC-14 T1 / ARC-0b T2): ConfigDir gating + DI seam ──────────

        [Test]
        public void DetectInstalled_DescriptorWithEmptyConfigDir_NeverAutoEnabled()
        {
            var descriptors = new[]
            {
                new BackendDescriptor { Key = "claude-code", AutoProjectConfig = true, ConfigDir = "~/.claude" },
                new BackendDescriptor { Key = "synthetic-no-dir", AutoProjectConfig = true, ConfigDir = "" },
            };
            // dirExists always true — proves exclusion isn't a dirExists coincidence.
            var result = AgentConfigPrefs.DetectInstalled(descriptors, _ => true);
            CollectionAssert.DoesNotContain(result, "synthetic-no-dir");
        }

        [Test]
        public void DetectInstalled_RealDescriptors_AllConfigDirsAbsent_ReturnsOnlyClaudeCodeFallback()
        {
            var result = AgentConfigPrefs.DetectInstalled(dirExists: _ => false);
            CollectionAssert.AreEquivalent(new[] { "claude-code" }, result);
        }
    }
}
