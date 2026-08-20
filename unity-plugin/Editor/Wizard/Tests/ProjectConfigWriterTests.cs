using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigWriterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"ProjectConfigWriterTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_tmpDir))
                    Directory.Delete(_tmpDir, true);
            });
        }

        // --- helpers ---

        private static HashSet<string> AllKeys()
        {
            var keys = new HashSet<string>();
            foreach (var t in ProjectConfigTargets.All) keys.Add(t.Key);
            return keys;
        }

        // --- filtering tests (new) ---

        [Test]
        public void Run_WithAllKeysEnabled_CreatesAllSixTargetFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());

            foreach (var target in ProjectConfigTargets.All)
                Assert.IsTrue(File.Exists(Path.Combine(_tmpDir, target.RelativePath)),
                    $"{target.RelativePath} should have been created");
        }

        [Test]
        public void Run_WithNoEnabledKeys_WritesNoNewFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string>());

            foreach (var target in ProjectConfigTargets.All)
                Assert.IsFalse(File.Exists(Path.Combine(_tmpDir, target.RelativePath)),
                    $"{target.RelativePath} should NOT have been created");
        }

        [Test]
        public void Run_WithEnabledKeySubset_WritesOnlyThoseFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            Assert.IsTrue(File.Exists(Path.Combine(_tmpDir, ".mcp.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_tmpDir, ".cursor/mcp.json")));
        }

        [Test]
        public void Run_ExistingFileNotInEnabledSet_StillUpdatesFile()
        {
            // Pre-create .cursor/mcp.json with old version — not in enabled set
            var cursorDir = Path.Combine(_tmpDir, ".cursor");
            Directory.CreateDirectory(cursorDir);
            var cursorPath = Path.Combine(cursorDir, "mcp.json");
            File.WriteAllText(cursorPath,
                "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"uvx\",\"_v\":\"0.0.1\"}}}");

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            // file-exists bypass: cursor file must still be updated
            var content = File.ReadAllText(cursorPath);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
        }

        [Test]
        public void GetActiveTargets_FileExistsNotEnabled_IncludesTarget()
        {
            var cursorDir = Path.Combine(_tmpDir, ".cursor");
            Directory.CreateDirectory(cursorDir);
            File.WriteAllText(Path.Combine(cursorDir, "mcp.json"), "{}");

            var enabled = new HashSet<string>(); // cursor not enabled
            var active = new List<ProjectConfigTarget>(
                ProjectConfigWriter.GetActiveTargets(_tmpDir, enabled));

            Assert.IsTrue(active.Exists(t => t.Key == "cursor"),
                "cursor should be included because file exists");
        }

        [Test]
        public void GetActiveTargets_FileAbsentNotEnabled_ExcludesTarget()
        {
            var enabled = new HashSet<string>(); // nothing enabled, no files
            var active = new List<ProjectConfigTarget>(
                ProjectConfigWriter.GetActiveTargets(_tmpDir, enabled));

            Assert.IsFalse(active.Exists(t => t.Key == "cursor"),
                "cursor should be excluded — file absent, not enabled");
        }

        [Test]
        public void Run_UpdatesGitignore_WithOnlyActiveTargetPaths()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            var gitignore = File.ReadAllText(Path.Combine(_tmpDir, ".gitignore"));
            StringAssert.Contains(".mcp.json", gitignore);
            StringAssert.DoesNotContain(".cursor/mcp.json", gitignore);
        }

        // --- original tests (updated to inject enabledKeys for isolation) ---

        [Test]
        public void Run_FreshProjectDir_EachFileContainsCurrentVersion()
        {
            // JSON entries no longer contain port (discovery via .port files); TOML still does.
            ProjectConfigWriter.Run(_tmpDir, 9501, "1.2.3", AllKeys());

            foreach (var target in ProjectConfigTargets.All)
            {
                var content = File.ReadAllText(Path.Combine(_tmpDir, target.RelativePath));
                if (target.IsToml)
                {
                    StringAssert.Contains("v1.2.3", content);
                    StringAssert.Contains("9501", content);
                }
                else
                {
                    StringAssert.Contains("\"_v\": \"1.2.3\"", content);
                }
            }
        }

        [Test]
        public void Run_ExistingOwnedCurrentFile_DoesNotRewrite()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });
            var path = Path.Combine(_tmpDir, ".mcp.json");
            // Sentinel proves no rewrite happened — a rewrite would regenerate fresh
            // content and silently drop this, whereas plain string equality against
            // deterministic output would pass either way (same inputs → same bytes).
            File.AppendAllText(path, "\n// sentinel-untouched\n");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });
            var after = File.ReadAllText(path);

            Assert.AreEqual(before, after);
            StringAssert.Contains("sentinel-untouched", after);
        }

        [Test]
        public void Run_PortChangedOnly_SkipsRewrite_PreservesUnrelatedMcpServers()
        {
            // Port change alone no longer triggers a rewrite (port not in JSON entries).
            // Same-version entry is OwnedCurrent → skip; sibling servers stay untouched.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            File.WriteAllText(path,
                "{\"mcpServers\":{\"other-tool\":{\"command\":\"x\"},"
                + "\"unity-mcp\":{\"command\":\"uvx\",\"_v\":\"1.0.0\"}}}");

            ProjectConfigWriter.Run(_tmpDir, 9600, "1.0.0", new HashSet<string>());

            var content = File.ReadAllText(path);
            StringAssert.Contains("other-tool", content);
        }

        [Test]
        public void Run_VersionChanged_RewritesFile()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.0.0", new HashSet<string> { "claude-code" });
            ProjectConfigWriter.Run(_tmpDir, 9500, "2.0.0", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains("\"_v\": \"2.0.0\"", content);
        }

        [Test]
        public void Run_FileWithForeignUnityMcpEntry_AdoptsEntry_AddsVersionMarker()
        {
            // Adoption: foreign entry gets "_v" marker inserted; custom content preserved.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            var handWritten = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"custom\"}}}";
            File.WriteAllText(path, handWritten);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string>());

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
            StringAssert.Contains("custom", content);
        }

        [Test]
        public void WriteOne_Foreign_AdoptsEntryInsteadOfSkipping()
        {
            var path = Path.Combine(_tmpDir, ".mcp.json");
            var handWritten = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"custom\"}}}";
            File.WriteAllText(path, handWritten);

            ProjectConfigWriter.WriteOne(_tmpDir, ProjectConfigTargets.All[0], 9500, "1.2.3",
                WizardConfigWriter.GitInstallUrlFor("1.2.3"));

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
            StringAssert.Contains("custom", content);
        }

        [Test]
        [Platform(Exclude = "Win")]
        public void Run_UnwritableTargetDirectory_DoesNotThrow_LogsWarning()
        {
            var chmod = new ProcessStartInfo("chmod", "000 " + _tmpDir) { UseShellExecute = false };
            Process.Start(chmod)?.WaitForExit();

            try
            {
                Assert.DoesNotThrow(() =>
                    ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys()));
            }
            finally
            {
                var restore = new ProcessStartInfo("chmod", "755 " + _tmpDir) { UseShellExecute = false };
                Process.Start(restore)?.WaitForExit();
            }
        }

        [Test]
        public void Run_UpdatesGitignore_WithAllSixPaths()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());

            var gitignore = File.ReadAllText(Path.Combine(_tmpDir, ".gitignore"));
            foreach (var target in ProjectConfigTargets.All)
                StringAssert.Contains(target.RelativePath, gitignore);
        }

        [Test]
        public void Run_GitignoreAlreadyPatched_SecondRunNoOp()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());
            var path = Path.Combine(_tmpDir, ".gitignore");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());
            var after = File.ReadAllText(path);

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Run_EmptyVersionString_FallsBackToUnpinnedGitInstallUrl()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains(WizardConfigWriter.GitInstallUrl, content);
        }
    }
}
