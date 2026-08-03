using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BackendDescriptorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void AllBackends_HaveNonEmptyNames()
        {
            foreach (var b in BackendDescriptor.All)
                Assert.IsFalse(string.IsNullOrEmpty(b.DisplayName),
                    $"Backend key '{b.Key}' has empty DisplayName");
        }

        [Test]
        public void AllBackends_HaveNonEmptyKeys()
        {
            foreach (var b in BackendDescriptor.All)
                Assert.IsFalse(string.IsNullOrEmpty(b.Key),
                    "Backend has empty Key");
        }

        [Test]
        public void AllBackends_HaveNonEmptyDescriptions()
        {
            foreach (var b in BackendDescriptor.All)
                Assert.IsFalse(string.IsNullOrEmpty(b.Description),
                    $"Backend '{b.Key}' has empty Description");
        }

        [Test]
        public void ClaudeCode_IsPythonConfig()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-code");
            Assert.IsNotNull(found, "claude-code backend not found");
            Assert.AreEqual(InstallMechanism.PythonConfig, found.Mechanism);
        }

        [Test]
        public void ClaudeCode_HasBinaryName()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-code");
            Assert.IsFalse(string.IsNullOrEmpty(found.BinaryName), "claude-code should have BinaryName");
        }

        [Test]
        public void ClaudeDesktop_HasConfigDir()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-desktop");
            Assert.IsFalse(string.IsNullOrEmpty(found.ConfigDir), "claude-desktop should have ConfigDir (no CLI binary)");
        }

        [Test]
        public void AllPythonConfig_HaveBinaryOrConfigDir()
        {
            foreach (var b in BackendDescriptor.All)
            {
                if (b.Mechanism != InstallMechanism.PythonConfig) continue;
                bool hasHint = !string.IsNullOrEmpty(b.BinaryName) || !string.IsNullOrEmpty(b.ConfigDir);
                Assert.IsTrue(hasHint, $"PythonConfig backend '{b.Key}' has neither BinaryName nor ConfigDir");
            }
        }

        [Test]
        public void ClaudeDesktop_IsPythonConfig()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-desktop");
            Assert.IsNotNull(found, "claude-desktop backend not found");
            Assert.AreEqual(InstallMechanism.PythonConfig, found.Mechanism);
        }

        [Test]
        public void Antigravity_IsChatAuto()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "antigravity");
            Assert.IsNotNull(found, "antigravity backend not found");
            Assert.AreEqual(InstallMechanism.ChatAuto, found.Mechanism);
        }

        [Test]
        public void AllBackends_CountIsAtLeastNine()
        {
            Assert.GreaterOrEqual(BackendDescriptor.All.Length, 9);
        }

        // ── Phase 1A: AutoProjectConfig / ManualInstructions ───────────────────

        [Test]
        public void ClaudeCode_HasAutoProjectConfig()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-code");
            Assert.IsTrue(found.AutoProjectConfig);
        }

        [Test]
        public void ClaudeDesktop_DoesNotHaveAutoProjectConfig()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-desktop");
            Assert.IsFalse(found.AutoProjectConfig);
        }

        [Test]
        public void RiderAi_IsManualInstructions()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "rider-ai");
            Assert.IsNotNull(found, "rider-ai backend not found");
            Assert.AreEqual(InstallMechanism.ManualInstructions, found.Mechanism);
        }

        [Test]
        public void RiderAi_HasNonEmptyInstructions()
        {
            var found = System.Array.Find(BackendDescriptor.All, b => b.Key == "rider-ai");
            Assert.IsFalse(string.IsNullOrEmpty(found.Instructions));
        }
    }
}
