using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class McpServerScannerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;     // ports/ (scan dir)
        private TempDirScope _lockScope; // lock dir (parent level in production)

        [SetUp]
        public void SetUp()
        {
            _scope = new TempDirScope("mcp-scanner-ports");
            _lockScope = new TempDirScope("mcp-scanner-lock");
            McpServerScanner.OverrideScanDir = _scope.Path;
            McpServerScanner.OverrideLockDir = _lockScope.Path;
        }

        [TearDown]
        public void TearDown()
        {
            McpServerScanner.OverrideScanDir = null;
            McpServerScanner.OverrideLockDir = null;
            _scope.Dispose();
            _lockScope.Dispose();
        }

        [Test]
        public void Scan_EmptyDir_ReturnsEmpty()
        {
            var result = McpServerScanner.Scan();
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void Scan_PortFileWithoutLock_ReportsNotAlive()
        {
            const int deadPid = 99999999;
            Assume.That(!IsProcessAlive(deadPid), "PID must not be alive for this test");
            File.WriteAllText(Path.Combine(_scope.Path, $"{deadPid}.port"), "9500\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Alive, Is.False);
        }

        [Test]
        public void Scan_StaleLockfile_ReportsNotAlive()
        {
            const int deadPid = 99999999;
            Assume.That(!IsProcessAlive(deadPid), "PID 99999999 must not be alive for this test");

            // Port file in scan dir, lock in lock dir (separate directories)
            File.WriteAllText(Path.Combine(_scope.Path, $"{deadPid}.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{deadPid}.lock"), $"{deadPid}\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Alive, Is.False);
        }

        [Test]
        public void Scan_CurrentPortMarked_IsCurrentProject()
        {
            var port = MCPServer.ServerPort;
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), $"{port}\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].IsCurrentProject, Is.True);
        }

        [Test]
        public void Scan_DifferentPort_NotCurrentProject()
        {
            const int differentPort = 29999;
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), $"{differentPort}\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].IsCurrentProject, Is.False);
        }

        // --- New tests for the lock-dir split fix ---

        [Test]
        public void Scan_LockInRootDir_ReportsAlive()
        {
            // Use PID 0 in filename so the PID-fallback can't produce alive=true on its own.
            // Only a lock file found in lockDir can produce alive=true here.
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_scope.Path, "0.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{selfPid}.lock"), "");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Alive, Is.True);
            Assert.That(result[0].Pid, Is.EqualTo(selfPid));
        }

        [Test]
        public void Scan_NoLock_FallbackToPidFromFileName()
        {
            // No lockfile — scanner must fall back to PID extracted from the filename.
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            File.WriteAllText(Path.Combine(_scope.Path, $"{selfPid}.port"), "9600\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Alive, Is.True);
            Assert.That(result[0].Pid, Is.EqualTo(selfPid));
            Assert.That(result[0].Port, Is.EqualTo(9600));
        }

        [Test]
        public void CleanPhantomFiles_RemovesDeadPidFiles()
        {
            const int deadPid = 99999999;
            Assume.That(!IsProcessAlive(deadPid), "PID 99999999 must not be alive for this test");

            var portFile = Path.Combine(_scope.Path, $"{deadPid}.port");
            var chatPort = Path.Combine(_scope.Path, $"{deadPid}.chat-port");
            var reloadPort = Path.Combine(_scope.Path, $"{deadPid}.reload-port");
            var lockFile = Path.Combine(_lockScope.Path, $"server-9600-{deadPid}.lock");
            File.WriteAllText(portFile, "9600\n");
            File.WriteAllText(chatPort, "9601\n");
            File.WriteAllText(reloadPort, "9602\n");
            File.WriteAllText(lockFile, $"{deadPid}\n");

            McpServerScanner.CleanPhantomFiles();

            Assert.That(File.Exists(portFile), Is.False);
            Assert.That(File.Exists(chatPort), Is.False, ".chat-port sibling must be cleaned");
            Assert.That(File.Exists(reloadPort), Is.False, ".reload-port sibling must be cleaned");
            Assert.That(File.Exists(lockFile), Is.False);
        }

        [Test]
        public void CleanPhantomFiles_KeepsAliveFiles()
        {
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            var portFile = Path.Combine(_scope.Path, $"{selfPid}.port");
            var lockFile = Path.Combine(_lockScope.Path, $"server-9500-{selfPid}.lock");
            File.WriteAllText(portFile, "9500\n");
            File.WriteAllText(lockFile, $"{selfPid}\n");

            McpServerScanner.CleanPhantomFiles();

            Assert.That(File.Exists(portFile), Is.True);
            Assert.That(File.Exists(lockFile), Is.True);
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch { return false; }
        }
    }
}
