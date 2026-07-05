// TDD tests for RelayCommandResolver — the install-source-aware (cmd, argv) resolver
// used by RelaySpawner.CommandResolver (ARCH-relay-upm-bootstrap.md Q2).
#if UNITY_MCP_CHAT
using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RelayCommandResolverTests
    {
        private const string UvxPrefKey = "UnityMCP_Chat_Path_uvx";
        private const string UvPrefKey  = "UnityMCP_Chat_Path_uv";

        private Func<string> _origVersionResolver;
        private Func<string, string> _origWhichOverride;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _origVersionResolver = RelayCommandResolver.VersionResolver;
            _origWhichOverride   = ChatBinaryResolver.WhichOverride;
            // A real Setup Wizard run on this machine may have persisted a EditorPrefs override
            // for uvx/uv — delete them so ChatBinaryResolver.Resolve(...) actually reaches WhichOverride.
            EditorPrefs.DeleteKey(UvxPrefKey);
            EditorPrefs.DeleteKey(UvPrefKey);
            ChatBinaryResolver.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            RelayCommandResolver.VersionResolver = _origVersionResolver;
            ChatBinaryResolver.WhichOverride      = _origWhichOverride;
            EditorPrefs.DeleteKey(UvxPrefKey);
            EditorPrefs.DeleteKey(UvPrefKey);
            ChatBinaryResolver.ResetCacheForTests();
            InstallSourceDetector.ClearTestOverride();
            ChatMcpConfigWriter.ClearPackageRootForTest();
            if (_tmpDir != null && Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true);
            _tmpDir = null;
        }

        // Lays out <tmp>/unity-plugin (packageRoot) + <tmp>/server (sibling, with pyproject.toml)
        // — mirrors ChatMcpConfigWriterTests' fixture so both writers agree on directory shape.
        private string MakeServerDir(bool withVenv)
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "RelayCommandResolverTests_" + Guid.NewGuid().ToString("N"));
            var packageRoot = Path.Combine(_tmpDir, "unity-plugin");
            var serverDir   = Path.Combine(_tmpDir, "server");
            Directory.CreateDirectory(packageRoot);
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "pyproject.toml"), "[project]\nname=\"unity-mcp\"\n");
            if (withVenv)
            {
                var venvBin = Path.Combine(serverDir, ".venv", "bin");
                Directory.CreateDirectory(venvBin);
                File.WriteAllText(Path.Combine(venvBin, "python"), "#!/bin/sh\n");
            }
            ChatMcpConfigWriter.SetPackageRootForTest(packageRoot);
            return serverDir;
        }

        [Test]
        public void Resolve_NonLocal_ReturnsUvxWithPinnedGitUrlAndRelayScript()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            ChatBinaryResolver.WhichOverride = name => name == "uvx" ? "/opt/homebrew/bin/uvx" : null;
            RelayCommandResolver.VersionResolver = () => "1.2.3";

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.AreEqual("/opt/homebrew/bin/uvx", cmd);
            Assert.AreEqual(3, argv.Length);
            Assert.AreEqual("--from", argv[0]);
            Assert.AreEqual(
                "git+https://github.com/german-krasnikov/unity-kiss-mcp.git@v1.2.3#subdirectory=server",
                argv[1]);
            Assert.AreEqual("unity-mcp-relay", argv[2]);
        }

        [Test]
        public void Resolve_NonLocal_UvxMissing_ReturnsNullCommand()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Registry);
            ChatBinaryResolver.WhichOverride = _ => null;

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.IsNull(cmd);
            Assert.IsNull(argv);
        }

        [Test]
        public void Resolve_NonLocal_EmbeddedSource_AlsoUsesUvx()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Embedded);
            ChatBinaryResolver.WhichOverride = name => name == "uvx" ? "/usr/local/bin/uvx" : null;
            RelayCommandResolver.VersionResolver = () => null; // unpinned fallback

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.AreEqual("/usr/local/bin/uvx", cmd);
            Assert.AreEqual("unity-mcp-relay", argv[2]);
            // Unpinned URL (no version) still targets the right repo/subdirectory.
            StringAssert.StartsWith("git+https://github.com/german-krasnikov/unity-kiss-mcp.git", argv[1]);
            StringAssert.EndsWith("#subdirectory=server", argv[1]);
        }

        // ── Local branch (uses ChatMcpConfigWriter.PackageRoot()'s test seam) ──

        [Test]
        public void Resolve_Local_VenvPresent_ReturnsVenvPythonAndModuleArgs()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            var serverDir  = MakeServerDir(withVenv: true);
            var venvPython = Path.Combine(serverDir, ".venv", "bin", "python");

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.AreEqual(venvPython, cmd);
            CollectionAssert.AreEqual(new[] { "-m", "unity_mcp.chat_relay" }, argv);
        }

        [Test]
        public void Resolve_Local_NoVenv_UvAvailable_ReturnsUvRunRelayScript()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            var serverDir = MakeServerDir(withVenv: false);
            ChatBinaryResolver.WhichOverride = name => name == "uv" ? "/opt/homebrew/bin/uv" : null;

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.AreEqual("/opt/homebrew/bin/uv", cmd);
            CollectionAssert.AreEqual(new[] { "run", "--directory", serverDir, "unity-mcp-relay" }, argv);
        }

        [Test]
        public void Resolve_Local_NoServerDir_ReturnsNullCommand()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            ChatMcpConfigWriter.SetPackageRootForTest(
                Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")));

            var (cmd, argv) = RelayCommandResolver.Resolve();

            Assert.IsNull(cmd);
            Assert.IsNull(argv);
        }
    }
}
#endif
