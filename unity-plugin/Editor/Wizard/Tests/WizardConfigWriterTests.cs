using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardConfigWriterTests
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"WizardConfigWriterTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); } catch { }
        }

        // ── RestoreConfig ─────────────────────────────────────────────────────

        [Test]
        public void RestoreConfig_ReturnsFalse_WhenNoBakExists()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            File.WriteAllText(cfg, "{\"original\": true}");

            bool result = WizardConfigWriter.RestoreConfig(cfg);

            Assert.IsFalse(result, "Should return false when no .bak exists");
        }

        [Test]
        public void RestoreConfig_ReturnsTrue_WhenBakExists()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            var bak = cfg + ".bak";
            File.WriteAllText(cfg, "{\"new\": true}");
            File.WriteAllText(bak, "{\"original\": true}");

            bool result = WizardConfigWriter.RestoreConfig(cfg);

            Assert.IsTrue(result, "Should return true when .bak exists");
        }

        [Test]
        public void RestoreConfig_CopiesBackup_ToOriginal()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            var bak = cfg + ".bak";
            File.WriteAllText(cfg, "{\"new\": true}");
            File.WriteAllText(bak, "{\"original\": true}");

            WizardConfigWriter.RestoreConfig(cfg);

            var content = File.ReadAllText(cfg);
            StringAssert.Contains("original", content, "Config should be restored from backup");
        }

        [Test]
        public void RestoreConfig_BakFileStillExists_AfterRestore()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            var bak = cfg + ".bak";
            File.WriteAllText(cfg, "{\"new\": true}");
            File.WriteAllText(bak, "{\"original\": true}");

            WizardConfigWriter.RestoreConfig(cfg);

            Assert.IsTrue(File.Exists(bak), ".bak should still exist after restore");
        }

        [Test]
        public void RestoreConfig_ReturnsFalse_WhenConfigMissingAndNoBak()
        {
            var cfg = Path.Combine(_tmpDir, "nonexistent.json");

            bool result = WizardConfigWriter.RestoreConfig(cfg);

            Assert.IsFalse(result);
        }

        // ── HasBackup ─────────────────────────────────────────────────────────

        [Test]
        public void HasBackup_ReturnsFalse_WhenNoBakFile()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            Assert.IsFalse(WizardConfigWriter.HasBackup(cfg));
        }

        [Test]
        public void HasBackup_ReturnsTrue_WhenBakExists()
        {
            var cfg = Path.Combine(_tmpDir, "config.json");
            File.WriteAllText(cfg + ".bak", "{}");
            Assert.IsTrue(WizardConfigWriter.HasBackup(cfg));
        }

        // ── Merge / Fresh (existing behavior, regression guard) ───────────────

        [Test]
        public void Fresh_ContainsMcpServers()
        {
            var result = WizardConfigWriter.Fresh(9500);
            StringAssert.Contains("mcpServers", result);
            StringAssert.Contains("unity-biome-mcp", result);
        }

        [Test]
        public void Merge_PreservesExistingKeys()
        {
            var existing = "{\"theme\":\"dark\",\"mcpServers\":{}}";
            var result = WizardConfigWriter.Merge(existing, 9500);
            StringAssert.Contains("theme", result);
            StringAssert.Contains("unity-biome-mcp", result);
        }

        // ── Fresh — port and key presence ─────────────────────────────────────

        [Test]
        public void Fresh_ContainsPort()
        {
            var result = WizardConfigWriter.Fresh(9501);
            StringAssert.Contains("9501", result, "Fresh should embed the port number");
        }

        [Test]
        public void Fresh_ContainsUnityMcpKey()
        {
            var result = WizardConfigWriter.Fresh(9500);
            StringAssert.Contains("unity-biome-mcp", result, "Fresh should contain the unity-biome-mcp key");
        }

        [Test]
        public void Fresh_ContainsGitInstallArgs()
        {
            var result = WizardConfigWriter.Fresh(9500);
            StringAssert.Contains("--from", result, "Fresh should use --from git install");
            StringAssert.Contains("github.com", result, "Fresh should reference GitHub URL");
        }
    }
}
