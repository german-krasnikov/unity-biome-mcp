// TDD — RC-5 + RC-2: CleanStalePeerPortFiles and SaveRuntimePorts contracts.
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortFileManagerTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PortFileManagerTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
            PortFileManager.ResetForTests();
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
            var settingsPath = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "ProjectSettings", "MCPSettings.json"));
            var portJsonPath = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, "..", "Library", "MCP_Port.json"));
            var before = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            var portJsonBefore = File.Exists(portJsonPath) ? File.ReadAllText(portJsonPath) : null;
            try
            {
                PortFileManager.SaveRuntimePorts(9999, 10000);

                var after = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
                Assert.AreEqual(before, after, "SaveRuntimePorts must not touch MCPSettings.json");
            }
            finally
            {
                if (portJsonBefore != null) File.WriteAllText(portJsonPath, portJsonBefore);
                else if (File.Exists(portJsonPath)) File.Delete(portJsonPath);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsProcessAlive(int pid)
        {
            try { System.Diagnostics.Process.GetProcessById(pid); return true; }
            catch (System.ArgumentException) { return false; }
        }
    }
}
