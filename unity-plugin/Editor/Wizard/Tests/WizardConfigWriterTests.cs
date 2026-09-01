using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardConfigWriterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"WizardConfigWriterTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_tmpDir))
                    Directory.Delete(_tmpDir, true);
            });
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

        [Test]
        public void Merge_PreservesUnknownEntryKeys()
        {
            // ARC-13 T1: the non-versioned call site (AiConfigScreen "Write Config"
            // button -> Merge) shares MergeWithEntry with ProjectConfigFormats.Merge —
            // an unrelated "cwd" key a user hand-added must survive here too.
            var existing = "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"args\": [\"--from\", \"" + WizardConfigWriter.GitInstallUrl + "\", \"unity-biome-mcp\"],"
                + "\"cwd\": \"/custom/path\""
                + "}}}";

            var result = WizardConfigWriter.Merge(existing, 9500);

            StringAssert.Contains("/custom/path", result, "unknown cwd key must survive a Merge");
        }

        // ── Fresh — port and key presence ─────────────────────────────────────

        [Test]
        public void Fresh_NoPortBakedIntoEnv()
        {
            // RC-3: UNITY_MCP_PORT must NOT be baked into the permanent Wizard config.
            // Python uses ~/.unity-biome-mcp/ports/{pid}.port discovery instead.
            var result = WizardConfigWriter.Fresh(9501);
            StringAssert.DoesNotContain("UNITY_MCP_PORT", result, "Wizard config must not bake port — use discovery");
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
