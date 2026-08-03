// TDD — RC-5 + RC-2: CleanStalePeerPortFiles and SaveRuntimePorts contracts.
using System;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortFileManagerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PortFileManagerTests_" + System.Guid.NewGuid().ToString("N"));
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, recursive: true);
            });
            Directory.CreateDirectory(_tempDir);
        }

        // ── CleanStalePeerPortFiles ───────────────────────────────────────────

        [Test]
        public void CleanStalePeerPortFiles_DeadPid_DeletesPortFile()
        {
            // PID 99999 is highly unlikely to be alive; if it is, test is inconclusive.
            const int deadPid = 99999;
            Assume.That(!IsProcessAlive(deadPid), "PID 99999 must not be a live process for this test");

            var portFile = Path.Combine(_tempDir, $"{deadPid}.port");
            File.WriteAllText(portFile, "9514");

            PortFileManager.CleanStalePeerPortFiles(_tempDir);

            Assert.IsFalse(File.Exists(portFile), "Dead-PID port file should be deleted");
        }

        [Test]
        public void CleanStalePeerPortFiles_DeadPid_DeletesChatPortFile()
        {
            const int deadPid = 99999;
            Assume.That(!IsProcessAlive(deadPid));

            var portFile     = Path.Combine(_tempDir, $"{deadPid}.port");
            var chatPortFile = Path.Combine(_tempDir, $"{deadPid}.chat-port");
            File.WriteAllText(portFile, "9514");
            File.WriteAllText(chatPortFile, "9515");

            PortFileManager.CleanStalePeerPortFiles(_tempDir);

            Assert.IsFalse(File.Exists(chatPortFile), "Dead-PID chat-port file should also be deleted");
        }

        [Test]
        public void CleanStalePeerPortFiles_LivePid_KeepsPortFile()
        {
            // Current process is guaranteed alive
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var portFile = Path.Combine(_tempDir, $"{currentPid}.port");
            File.WriteAllText(portFile, "9514");

            PortFileManager.CleanStalePeerPortFiles(_tempDir);

            Assert.IsTrue(File.Exists(portFile), "Live-PID port file must not be deleted");
        }

        [Test]
        public void CleanStalePeerPortFiles_NonNumericFilename_Ignored()
        {
            // Files that don't match {pid}.port pattern must be left alone
            var bogusFile = Path.Combine(_tempDir, "not-a-pid.port");
            File.WriteAllText(bogusFile, "9514");

            Assert.DoesNotThrow(() => PortFileManager.CleanStalePeerPortFiles(_tempDir));
            Assert.IsTrue(File.Exists(bogusFile), "Non-numeric port file must not be touched");
        }

        [Test]
        public void CleanStalePeerPortFiles_EmptyDir_DoesNotThrow()
            => Assert.DoesNotThrow(() => PortFileManager.CleanStalePeerPortFiles(_tempDir));

        [Test]
        public void CleanStalePeerPortFiles_MissingDir_DoesNotThrow()
            => Assert.DoesNotThrow(() =>
                PortFileManager.CleanStalePeerPortFiles(
                    Path.Combine(_tempDir, "nonexistent")));

        // ── SaveRuntimePorts ─────────────────────────────────────────────────

        [Test]
        public void SaveRuntimePorts_DoesNotModifySettings()
        {
            const int processId = 4242;
            var settingsPath = Path.Combine(_tempDir, "ProjectSettings", "MCPSettings.json");
            var portJsonPath = Path.Combine(_tempDir, "Library", "MCP_Port.json");
            var temporaryCachePath = Path.Combine(_tempDir, "Temp", "mcp_port.txt");
            var discoveryDirectory = Path.Combine(_tempDir, "discovery");
            var discoveryPath = Path.Combine(discoveryDirectory, $"{processId}.port");
            var chatDiscoveryPath = Path.Combine(
                discoveryDirectory, $"{processId}.chat-port");
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, "{\"port\":9500,\"chatPort\":9501}");
            var before = File.ReadAllText(settingsPath);

            var persisted = PortFileManager.SaveRuntimePortsCore(
                9999,
                10000,
                portJsonPath,
                temporaryCachePath,
                discoveryDirectory,
                processId,
                "/tmp/isolated-project",
                "isolated-project");

            Assert.IsTrue(persisted);
            Assert.AreEqual(before, File.ReadAllText(settingsPath),
                "Runtime persistence must not touch MCPSettings.json.");
            Assert.AreEqual(9999,
                PortResolver.ResolvePort(null, null, File.ReadAllText(portJsonPath), 0));
            Assert.AreEqual(10000,
                PortResolver.ResolveChatPort(
                    null, null, File.ReadAllText(portJsonPath), 9999, 0));
            Assert.AreEqual("9999", File.ReadAllText(temporaryCachePath));
            Assert.AreEqual(
                "9999\n/tmp/isolated-project\nisolated-project",
                File.ReadAllText(discoveryPath));
            Assert.AreEqual(
                "10000\n/tmp/isolated-project\nisolated-project",
                File.ReadAllText(chatDiscoveryPath));
        }

        [Test]
        public void SaveRuntimePorts_RuntimeJsonWriteFails_DoesNotCommitResolvedPorts()
        {
            var committed = false;

            var persisted = PortFileManager.SaveRuntimePortsCore(
                9999,
                10000,
                Path.Combine(_tempDir, "Library", "MCP_Port.json"),
                Path.Combine(_tempDir, "Temp", "mcp_port.txt"),
                Path.Combine(_tempDir, "discovery"),
                4242,
                "/tmp/isolated-project",
                "isolated-project",
                (_, __) => throw new IOException("deterministic writer failure"),
                (_, __) => committed = true);

            Assert.IsFalse(persisted);
            Assert.IsFalse(committed,
                "Resolved ports must not be committed when MCP_Port.json persistence fails.");
        }

        [Test]
        public void SaveRuntimePorts_DiscoveryWriteFails_DoesNotCommitResolvedPorts()
        {
            const int processId = 4242;
            var discoveryDirectory = Path.Combine(_tempDir, "discovery");
            var discoveryPath = Path.Combine(discoveryDirectory, $"{processId}.port");
            var committed = false;
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                "[MCP] Could not write discovery file: deterministic discovery failure");

            var persisted = PortFileManager.SaveRuntimePortsCore(
                9999,
                10000,
                Path.Combine(_tempDir, "Library", "MCP_Port.json"),
                Path.Combine(_tempDir, "Temp", "mcp_port.txt"),
                discoveryDirectory,
                processId,
                "/tmp/isolated-project",
                "isolated-project",
                (path, contents) =>
                {
                    if (path == discoveryPath)
                        throw new IOException("deterministic discovery failure");
                    File.WriteAllText(path, contents);
                },
                (_, __) => committed = true);

            Assert.IsFalse(persisted);
            Assert.IsFalse(committed,
                "Resolved ports must not be committed when discovery persistence fails.");
            Assert.IsFalse(File.Exists(discoveryPath));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsProcessAlive(int pid)
        {
            try { System.Diagnostics.Process.GetProcessById(pid); return true; }
            catch (System.ArgumentException) { return false; }
        }
    }
}
