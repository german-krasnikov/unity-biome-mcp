using System;
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

        [Test]
        public void Run_FreshProjectDir_CreatesAllSixTargetFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");

            foreach (var target in ProjectConfigTargets.All)
                Assert.IsTrue(File.Exists(Path.Combine(_tmpDir, target.RelativePath)),
                    $"{target.RelativePath} should have been created");
        }

        [Test]
        public void Run_FreshProjectDir_EachFileContainsCurrentVersion()
        {
            // JSON entries no longer contain port (discovery via .port files); TOML still does.
            ProjectConfigWriter.Run(_tmpDir, 9501, "1.2.3");

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
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");
            var path = Path.Combine(_tmpDir, ".mcp.json");
            // Sentinel proves no rewrite happened — a rewrite would regenerate fresh
            // content and silently drop this, whereas plain string equality against
            // deterministic output would pass either way (same inputs → same bytes).
            File.AppendAllText(path, "\n// sentinel-untouched\n");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");
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

            ProjectConfigWriter.Run(_tmpDir, 9600, "1.0.0");

            var content = File.ReadAllText(path);
            StringAssert.Contains("other-tool", content);
        }

        [Test]
        public void Run_VersionChanged_RewritesFile()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.0.0");
            ProjectConfigWriter.Run(_tmpDir, 9500, "2.0.0");

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains("\"_v\": \"2.0.0\"", content);
        }

        [Test]
        public void Run_FileWithForeignUnityMcpEntry_SkipsFile_LeavesContentUntouched()
        {
            var path = Path.Combine(_tmpDir, ".mcp.json");
            var handWritten = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"custom\"}}}";
            File.WriteAllText(path, handWritten);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");

            Assert.AreEqual(handWritten, File.ReadAllText(path));
        }

        [Test]
        [Platform(Exclude = "Win")]
        public void Run_UnwritableTargetDirectory_DoesNotThrow_LogsWarning()
        {
            var chmod = new ProcessStartInfo("chmod", "000 " + _tmpDir) { UseShellExecute = false };
            Process.Start(chmod)?.WaitForExit();

            try
            {
                Assert.DoesNotThrow(() => ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3"));
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
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");

            var gitignore = File.ReadAllText(Path.Combine(_tmpDir, ".gitignore"));
            foreach (var target in ProjectConfigTargets.All)
                StringAssert.Contains(target.RelativePath, gitignore);
        }

        [Test]
        public void Run_GitignoreAlreadyPatched_SecondRunNoOp()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");
            var path = Path.Combine(_tmpDir, ".gitignore");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3");
            var after = File.ReadAllText(path);

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Run_EmptyVersionString_FallsBackToUnpinnedGitInstallUrl()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "");

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains(WizardConfigWriter.GitInstallUrl, content);
        }
    }
}
