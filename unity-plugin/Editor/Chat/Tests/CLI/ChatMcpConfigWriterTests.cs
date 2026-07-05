using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    // ── GetOrCreateConfigPath install-source tests ────────────────────────────

    [TestFixture]
    public class ChatMcpConfigWriterInstallSourceTests
    {
        [TearDown]
        public void TearDown()
        {
            InstallSourceDetector.ClearTestOverride();
            ChatMcpConfigWriter.ClearPackageRootForTest();
        }

        [Test]
        public void GetOrCreateConfigPath_GitInstall_NoServer_ReturnsUvxConfig()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);

            // Point package to a path with no server/ next to it
            // GetOrCreateConfigPath uses Packages/<id> which won't have a sibling server/ in test
            var path = ChatMcpConfigWriter.GetOrCreateConfigPath();

            Assert.IsNotNull(path, "should return a path, not null");
            var json = File.ReadAllText(path);
            StringAssert.Contains("uvx", json, "command must be uvx for git install");
            StringAssert.Contains("unity-mcp", json);
        }

        [Test]
        public void GetOrCreateConfigPath_LocalInstall_NoServer_ReturnsNull()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            // Override packageRoot to a path that has no sibling server/ with pyproject.toml
            ChatMcpConfigWriter.SetPackageRootForTest("/nonexistent/path/unity-plugin");
            LogAssert.Expect(LogType.Error, new Regex("Server not found at expected path"));

            var path = ChatMcpConfigWriter.GetOrCreateConfigPath();

            Assert.IsNull(path, "Local install with missing server should return null");
        }
    }
    [TestFixture]
    public class ChatMcpConfigWriterTests
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), "ChatMcpConfigWriterTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmpDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tmpDir))
                Directory.Delete(_tmpDir, true);
        }

        // ── 1. DeriveServerPath ───────────────────────────────────────────────

        [Test]
        public void DeriveServerPath_BasicRoot_ReturnsNormalizedPath()
        {
            var root = "/Users/dev/unity-kiss-mcp/unity-plugin";
            var result = ChatMcpConfigWriter.DeriveServerPath(root);
            Assert.AreEqual("/Users/dev/unity-kiss-mcp/server", result);
        }

        [Test]
        public void DeriveServerPath_TrailingSlash_ReturnsNormalizedPath()
        {
            var root = "/Users/dev/unity-kiss-mcp/unity-plugin/";
            var result = ChatMcpConfigWriter.DeriveServerPath(root);
            Assert.AreEqual("/Users/dev/unity-kiss-mcp/server", result);
        }

        // ── 2. ResolveServerDir missing server/ ───────────────────────────────

        [Test]
        public void ResolveServerDir_MissingDir_ReturnsNull()
        {
            var fakeRoot = Path.Combine(_tmpDir, "pkg");
            Directory.CreateDirectory(fakeRoot);
            // no 'server' subdirectory created
            var result = ChatMcpConfigWriter.ResolveServerDir(fakeRoot);
            Assert.IsNull(result);
        }

        // ── 3. ResolveServerDir server/ exists but no pyproject.toml ─────────

        [Test]
        public void ResolveServerDir_NoPyprojectToml_ReturnsNull()
        {
            var fakeRoot = Path.Combine(_tmpDir, "pkg");
            var serverDir = Path.Combine(fakeRoot, "..", "server");
            Directory.CreateDirectory(Path.GetFullPath(serverDir));
            var result = ChatMcpConfigWriter.ResolveServerDir(fakeRoot);
            Assert.IsNull(result);
        }

        // ── 3b. ResolveServerDir server/ + pyproject.toml present ────────────

        [Test]
        public void ResolveServerDir_PyprojectPresent_ReturnsAbsoluteServerDir()
        {
            var fakeRoot  = Path.Combine(_tmpDir, "pkg");
            var serverDir = Path.GetFullPath(Path.Combine(fakeRoot, "..", "server"));
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "pyproject.toml"), "[project]");
            Assert.AreEqual(serverDir, ChatMcpConfigWriter.ResolveServerDir(fakeRoot));
        }

        // ── 3c. BuildClaudeConfigJson JSON-escaping round-trip ────────────────

        [Test]
        public void BuildClaudeConfigJson_BackslashAndQuoteInArgs_RoundTripSurvives()
        {
            const string winPath = "C:\\Users\\dev\\server\\python.exe";
            const string argWithQuote = "say \"hello\"";
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                winPath, new[] { argWithQuote });

            var mcpServers = JsonHelper.ExtractObject(json, "mcpServers");
            var unity      = JsonHelper.ExtractObject(mcpServers, "unity-kiss");
            var command    = JsonHelper.ExtractString(unity, "command");

            Assert.AreEqual(winPath, command, "backslashes must survive the JSON round-trip");
            StringAssert.Contains("say", JsonHelper.ExtractArray(unity, "args"),
                "arg value must be present in serialized array");
        }

        // ── 4. BuildClaudeConfigJson valid JSON ───────────────────────────────

        [Test]
        public void BuildClaudeConfigJson_ValidInput_ParseableJson()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/usr/local/bin/uv",
                new[] { "run", "--directory", "/some/server", "unity-mcp" });

            var mcpServers = JsonHelper.ExtractObject(json, "mcpServers");
            var unity      = JsonHelper.ExtractObject(mcpServers, "unity-kiss");
            var command    = JsonHelper.ExtractString(unity, "command");
            var argsRaw    = JsonHelper.ExtractArray(unity, "args");

            Assert.AreEqual("/usr/local/bin/uv", command);
            StringAssert.Contains("\"run\"", argsRaw);
            StringAssert.Contains("\"unity-mcp\"", argsRaw);
        }

        // ── 5. BuildClaudeConfigJson args with spaces → valid JSON ────────────

        [Test]
        public void BuildClaudeConfigJson_ArgsWithSpaces_ValidJson()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/path with spaces/bin/uv",
                new[] { "run", "--directory", "/path with spaces/server", "unity-mcp" });

            var mcpServers = JsonHelper.ExtractObject(json, "mcpServers");
            var unity      = JsonHelper.ExtractObject(mcpServers, "unity-kiss");
            var command    = JsonHelper.ExtractString(unity, "command");

            Assert.AreEqual("/path with spaces/bin/uv", command);
            // 4 args preserved — count commas between strings in array
            var argsRaw = JsonHelper.ExtractArray(unity, "args");
            var commas  = 0;
            foreach (var c in argsRaw) if (c == ',') commas++;
            Assert.AreEqual(3, commas, "4 args → 3 commas");
        }

        // ── 6. ResolvePythonCommand venv exists ───────────────────────────────

        [Test]
        public void ResolvePythonCommand_VenvExists_ReturnsVenvPython()
        {
            var serverDir = Path.Combine(_tmpDir, "server");
            var venvBin   = Path.Combine(serverDir, ".venv", "bin");
            Directory.CreateDirectory(venvBin);
            File.WriteAllText(Path.Combine(venvBin, "python"), "#!/bin/sh");

            var (cmd, args) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);

            Assert.AreEqual(Path.Combine(venvBin, "python"), cmd);
            Assert.AreEqual(new[] { "-m", "unity_mcp.server" }, args);
        }

        // ── 7. ResolvePythonCommand no venv, uv injected → uv branch ─────────

        [Test]
        public void ResolvePythonCommand_NoVenvUvInjected_ReturnsUvInvocation()
        {
            var serverDir = Path.Combine(_tmpDir, "server");
            Directory.CreateDirectory(serverDir);

            var (cmd, args) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, "/usr/local/bin/uv");

            Assert.AreEqual("/usr/local/bin/uv", cmd);
            Assert.AreEqual(new[] { "run", "--directory", serverDir, "unity-mcp" }, args);
        }

        // ── 8. ResolvePythonCommand no venv, no uv → python3 fallback ─────────

        [Test]
        public void ResolvePythonCommand_NoVenvNoUv_ReturnsPython3Fallback()
        {
            var serverDir = Path.Combine(_tmpDir, "server");
            Directory.CreateDirectory(serverDir);

            var (cmd, args) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);

            Assert.AreEqual("python3", cmd);
            Assert.AreEqual(new[] { "-m", "unity_mcp.server" }, args);
        }

        // ── 8b. ResolvePythonCommand Windows Scripts/python.exe wins (File.Exists-driven) ──

        [Test]
        public void ResolvePythonCommand_WindowsVenvExists_ReturnsScriptsPythonExe()
        {
            var serverDir   = Path.Combine(_tmpDir, "server");
            var scriptsDir  = Path.Combine(serverDir, ".venv", "Scripts");
            Directory.CreateDirectory(scriptsDir);
            // Create the Windows-style executable
            File.WriteAllText(Path.Combine(scriptsDir, "python.exe"), "");

            var (cmd, args) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);

            // Scripts/python.exe must be returned regardless of platform (File.Exists is pure)
            StringAssert.EndsWith("python.exe", cmd);
            StringAssert.Contains("Scripts", cmd);
            Assert.AreEqual(new[] { "-m", "unity_mcp.server" }, args);
        }

        [Test]
        public void ResolvePythonCommand_BothVenvsExist_WindowsScriptsPythonExeWins()
        {
            // Both bin/python (Unix-style) AND Scripts/python.exe exist — Windows path must win
            var serverDir  = Path.Combine(_tmpDir, "server");
            var binDir     = Path.Combine(serverDir, ".venv", "bin");
            var scriptsDir = Path.Combine(serverDir, ".venv", "Scripts");
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "python.exe"), "");
            File.WriteAllText(Path.Combine(binDir, "python"), "#!/bin/sh");

            var (cmd, _) = ChatMcpConfigWriter.ResolvePythonCommand(serverDir, null);

            StringAssert.Contains("Scripts", cmd, "Scripts/python.exe must take priority over bin/python");
        }

        // ── 9. Contract/shape test ────────────────────────────────────────────

        [Test]
        public void BuildClaudeConfigJson_Contract_TopKeyMcpServersUnityHasCommandAndArgs()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/bin/uv", new[] { "run", "--directory", "/srv", "unity-mcp" });

            var mcpServers = JsonHelper.ExtractObject(json, "mcpServers");
            Assert.AreNotEqual("{}", mcpServers, "mcpServers key must exist");

            var unity = JsonHelper.ExtractObject(mcpServers, "unity-kiss");
            Assert.AreNotEqual("{}", unity, "unity-kiss server key must exist");

            var command = JsonHelper.ExtractString(unity, "command");
            Assert.IsNotNull(command, "command field must exist");

            var argsArr = JsonHelper.ExtractArray(unity, "args");
            Assert.AreNotEqual("[]", argsArr, "args array must exist and be non-empty");
        }

        [Test]
        public void BuildClaudeConfigJson_WithPort_ContainsEnvUnityMcpPort()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/bin/uv", new[] { "run", "--directory", "/srv", "unity-mcp" }, port: 9501);

            var mcpServers = JsonHelper.ExtractObject(json, "mcpServers");
            var unity      = JsonHelper.ExtractObject(mcpServers, "unity-kiss");
            var env        = JsonHelper.ExtractObject(unity, "env");
            var portVal    = JsonHelper.ExtractString(env, "UNITY_MCP_PORT");
            Assert.AreEqual("9501", portVal);
        }

        [Test]
        public void BuildClaudeConfigJson_WithPort_ContainsUnityMcpChatEnv()
        {
            // UNITY_MCP_CHAT must be in --mcp-config env (not CLI process env) so that
            // only "unity-kiss" server gets it, not "unity-mcp" from ~/.mcp.json.
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/bin/uv", new[] { "run", "--directory", "/srv", "unity-mcp" }, port: 9501);

            var env      = JsonHelper.ExtractObject(JsonHelper.ExtractObject(json, "mcpServers"), "unity-kiss");
            var chatFlag = JsonHelper.ExtractString(JsonHelper.ExtractObject(env, "env"), "UNITY_MCP_CHAT");
            Assert.AreEqual("1", chatFlag);
        }

        [Test]
        public void BuildClaudeConfigJson_NoPort_NoEnvField()
        {
            var json = ChatMcpConfigWriter.BuildClaudeConfigJson(
                "/bin/uv", new[] { "run", "--directory", "/srv", "unity-mcp" });

            StringAssert.DoesNotContain("\"env\"", json);
        }

        // ── Port-scoped filename tests (T1-T8) ───────────────────────────────

        // T1 — discriminates the core bug: two ports → two distinct file names
        [Test]
        public void ConfigFileName_TwoPorts_ProduceTwoDistinctNames()
        {
            var n1 = ChatMcpConfigWriter.ConfigFileName(9501);
            var n2 = ChatMcpConfigWriter.ConfigFileName(9502);
            Assert.AreNotEqual(n1, n2);
            StringAssert.Contains("9501", n1);
            StringAssert.Contains("9502", n2);
        }

        // T2 — no-clobber: writing config for 9502 does NOT overwrite config for 9501
        [Test]
        public void GetOrCreateConfigPath_TwoPorts_BDoesNotClobberA()
        {
            var pathA = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            var pathB = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9502);
            Assert.AreNotEqual(pathA, pathB);
            var jsonA = File.ReadAllText(pathA);
            StringAssert.Contains("\"9501\"", jsonA, "A's file must still have port 9501 after B was written");
        }

        // T3 — filename/body agreement: port in name matches port in body
        [Test]
        public void GetOrCreateConfigPath_FilenamePortMatchesBodyPort()
        {
            var path = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            var json = File.ReadAllText(path);
            StringAssert.Contains("9501", Path.GetFileName(path));
            StringAssert.Contains("\"9501\"", json);
        }

        // T4 — atomic write: no .tmp residue left behind
        [Test]
        public void GetOrCreateConfigPath_LeavesNoTmpResidue()
        {
            ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            var tmps = Directory.GetFiles(_tmpDir, "*.tmp");
            Assert.IsEmpty(tmps);
        }

        // T5 — regression guard: port>0 must not produce the legacy bare filename
        [Test]
        public void GetOrCreateConfigPath_PortGtZero_DoesNotProduceLegacyBareFilename()
        {
            var path = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            Assert.AreNotEqual("unity-mcp-config.json", Path.GetFileName(path));
        }

        // T6 — reload-survival: same port → same path on repeated calls
        [Test]
        public void GetOrCreateConfigPath_SamePort_ReturnsSamePathBothCalls()
        {
            var p1 = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            var p2 = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            Assert.AreEqual(p1, p2);
        }

        // T7 — stale-scan cleanup: old file deleted, fresh file preserved
        [Test]
        public void CleanupStaleConfigs_OldFilesDeleted_CurrentFilePreserved()
        {
            var stalePath = Path.Combine(_tmpDir, "unity-mcp-config-9999.json");
            File.WriteAllText(stalePath, "{}", JsonHelper.Utf8NoBom);
            File.SetLastWriteTime(stalePath, DateTime.UtcNow.AddHours(-3));

            var currentPath = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);

            ChatMcpConfigWriter.CleanupStaleConfigs(_tmpDir, maxAgeHours: 2);

            Assert.IsFalse(File.Exists(stalePath), "stale file must be deleted");
            Assert.IsTrue(File.Exists(currentPath), "current file must survive");
        }

        // T8 — own-file delete: only target port deleted, sibling untouched
        [Test]
        public void DeleteConfig_DeletesOnlyOwnPort_LeavesSiblingIntact()
        {
            var pathA = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9501);
            var pathB = ChatMcpConfigWriter.GetOrCreateConfigPath(_tmpDir, 9502);

            ChatMcpConfigWriter.DeleteConfig(_tmpDir, 9501);

            Assert.IsFalse(File.Exists(pathA), "own-port file must be deleted");
            Assert.IsTrue(File.Exists(pathB), "sibling port file must survive");
        }
    }
}
