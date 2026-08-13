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
            McpServerScanner.OverrideLiveTcpCountGetter = null;
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

        // ── ScanDetailed() tests ─────────────────────────────────────────────

        [Test]
        public void ScanDetailed_MultipleLocksForSamePort_ReturnsAllConnections()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-1001.lock"), "1001\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-1002.lock"), "1002\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-1003.lock"), "1003\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Connections.Count, Is.EqualTo(3));
        }

        [Test]
        public void ScanDetailed_MixedAliveDeadLocks_IndividualAliveFlags()
        {
            const int deadPid = 99999999;
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            Assume.That(!IsProcessAlive(deadPid), "PID 99999999 must not be alive for this test");

            File.WriteAllText(Path.Combine(_scope.Path, "0.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{deadPid}.lock"), $"{deadPid}\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{selfPid}.lock"), $"{selfPid}\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            var conns = result[0].Connections;
            Assert.That(conns.Count, Is.EqualTo(2));

            bool deadAlive = false, selfAlive = false;
            foreach (var c in conns)
            {
                if (c.BridgePid == deadPid) deadAlive = c.BridgeAlive;
                if (c.BridgePid == selfPid) selfAlive = c.BridgeAlive;
            }
            Assert.That(deadAlive, Is.False, "dead PID should have BridgeAlive=false");
            Assert.That(selfAlive, Is.True, "self PID should have BridgeAlive=true");
        }

        [Test]
        public void ScanDetailed_NoLocks_EmptyConnections()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Connections.Count, Is.EqualTo(0));
        }

        [Test]
        public void ScanDetailed_UnityPidFromFilename()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "83782.port"), "9500\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].UnityPid, Is.EqualTo(83782));
        }

        [Test]
        public void ScanDetailed_MultiplePortFiles_MultipleEntries()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");
            File.WriteAllText(Path.Combine(_scope.Path, "88888888.port"), "9501\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(2));
        }

        [Test]
        public void ScanDetailed_LiveTcpCountInjected()
        {
            var port = MCPServer.ServerPort;
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), $"{port}\n");
            McpServerScanner.OverrideLiveTcpCountGetter = _ => 3;

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].IsCurrentProject, Is.True);
            Assert.That(result[0].LiveTcpCount, Is.EqualTo(3));
        }

        [Test]
        public void ScanDetailed_NonCurrentProject_LiveTcpCountZero()
        {
            const int differentPort = 29999;
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), $"{differentPort}\n");
            McpServerScanner.OverrideLiveTcpCountGetter = _ => 99;

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].IsCurrentProject, Is.False);
            Assert.That(result[0].LiveTcpCount, Is.EqualTo(0));
        }

        [Test]
        public void Scan_BackwardCompat_FirstAlivePidStrategy()
        {
            const int deadPid = 99999999;
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            Assume.That(!IsProcessAlive(deadPid), "PID 99999999 must not be alive for this test");

            File.WriteAllText(Path.Combine(_scope.Path, "0.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{deadPid}.lock"), $"{deadPid}\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{selfPid}.lock"), $"{selfPid}\n");

            var result = McpServerScanner.Scan();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Alive, Is.True, "Should find alive bridge");
            Assert.That(result[0].Pid, Is.EqualTo(selfPid), "Should select the alive PID");
        }

        // Discriminating counterpart: dead PID sorts FIRST (ascending), alive must still win.
        // The test above uses deadPid=99999999 > selfPid, so "just take first" and "first-alive"
        // produce the same result — this one proves they don't when the order is reversed.
        [Test]
        public void Scan_BackwardCompat_AliveWins_WhenDeadSortsFirst()
        {
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            // Find a dead PID strictly less than selfPid so it sorts before selfPid.
            int deadPid = -1;
            for (int c = selfPid - 1; c >= 2 && deadPid < 0; c--)
                if (!IsProcessAlive(c)) deadPid = c;
            Assume.That(deadPid > 0, "Could not find a dead PID < selfPid on this machine");

            File.WriteAllText(Path.Combine(_scope.Path, "0.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{deadPid}.lock"), "");
            File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{selfPid}.lock"), "");

            var result = McpServerScanner.Scan();

            // Sort order: [deadPid (dead), selfPid (alive)]. "just take first" would return dead.
            // First-alive strategy must skip deadPid and return selfPid.
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Pid, Is.EqualTo(selfPid), "alive PID must win over earlier-sorted dead PID");
            Assert.That(result[0].Alive, Is.True);
        }

        [Test]
        public void ScanDetailed_EightLocks_AllReturned()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");
            for (int i = 1; i <= 8; i++)
                File.WriteAllText(Path.Combine(_lockScope.Path, $"server-9500-{1000 + i}.lock"), $"{1000 + i}\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Connections.Count, Is.EqualTo(8));
        }

        // ── H6 edge-case tests ───────────────────────────────────────────────

        [Test]
        public void ScanDetailed_MalformedLockFilename_Skipped()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");
            // Malformed: empty PID segment — int.TryParse("") fails
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-.lock"), "bad\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-1001.lock"), "1001\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result[0].Connections.Count, Is.EqualTo(1),
                "Malformed lock filename must be skipped");
        }

        [Test]
        public void ScanDetailed_NonNumericPid_Skipped()
        {
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-abc.lock"), "abc\n");
            File.WriteAllText(Path.Combine(_lockScope.Path, "server-9500-1001.lock"), "1001\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result[0].Connections.Count, Is.EqualTo(1),
                "Non-numeric PID in lock filename must be skipped");
        }

        [Test]
        public void FindConnections_EmptyDirectory_ReturnsEmpty()
        {
            // Lock dir that doesn't exist → FindConnections returns empty
            McpServerScanner.OverrideLockDir = Path.Combine(
                Path.GetTempPath(), $"no_such_{System.Guid.NewGuid():N}");
            File.WriteAllText(Path.Combine(_scope.Path, "99999999.port"), "9500\n");

            var result = McpServerScanner.ScanDetailed();

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Connections.Count, Is.EqualTo(0),
                "Non-existent lock dir must return empty connections");
        }
    }
}
