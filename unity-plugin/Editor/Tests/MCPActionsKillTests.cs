using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPActionsKillTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private TempDirScope _scope;

        [SetUp]
        public void SetUp()
        {
            // Isolate KillAll from real ~/.unity-biome-mcp to avoid killing live Python MCP servers.
            _scope = new TempDirScope("mcp-kill-test");
            MCPActions.OverrideLockDir = _scope.Path;
        }

        [TearDown]
        public void TearDown()
        {
            MCPActions.OverrideLockDir = null;
            _scope.Dispose();
        }

        [Test]
        public void Kill_MissingLockfile_DoesNotThrow()
        {
            // KillAll globs server-{ServerPort}-*.lock; when none exist it logs and returns.
            Assert.DoesNotThrow(() => MCPActions.Kill());
        }

        [Test]
        public void KillAll_MissingLockfile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => MCPActions.KillAll());
        }

        [Test]
        public void KillCurrent_StaleLockCurrentPort_Cleaned()
        {
            var port = MCPServer.ServerPort;
            var lockFile = Path.Combine(_scope.Path, $"server-{port}-99999.lock");
            File.WriteAllText(lockFile, "99999\n");
            MCPActions.KillCurrent();
            Assert.IsFalse(File.Exists(lockFile));
        }

        [Test]
        public void KillCurrent_LockDifferentPort_NotTouched()
        {
            var differentPort = MCPServer.ServerPort + 1;
            var lockFile = Path.Combine(_scope.Path, $"server-{differentPort}-99999.lock");
            File.WriteAllText(lockFile, "99999\n");
            MCPActions.KillCurrent();
            Assert.IsTrue(File.Exists(lockFile), "Lock for different port must survive KillCurrent");
        }

        [Test]
        public void Kill_DelegatesToKillCurrent_NotKillAll()
        {
            // Kill() should only clean current port, not all ports
            var currentPort = MCPServer.ServerPort;
            var otherPort = currentPort + 1;
            var currentLock = Path.Combine(_scope.Path, $"server-{currentPort}-99999.lock");
            var otherLock = Path.Combine(_scope.Path, $"server-{otherPort}-99999.lock");
            File.WriteAllText(currentLock, "99999\n");
            File.WriteAllText(otherLock, "99999\n");
            MCPActions.Kill();
            Assert.IsFalse(File.Exists(currentLock), "Current port lock should be cleaned");
            Assert.IsTrue(File.Exists(otherLock), "Other port lock should survive Kill()");
        }

        [Test]
        public void KillAll_StaleLockfile_CleanedUp()
        {
            // Write a lockfile with a dead PID — KillAll should clean it up.
            var port = MCPServer.ServerPort;
            var lockFile = Path.Combine(_scope.Path, $"server-{port}-99999.lock");
            File.WriteAllText(lockFile, "99999\n");  // PID that doesn't exist
            MCPActions.KillAll();
            Assert.IsFalse(File.Exists(lockFile), "Stale lockfile should be cleaned up");
        }

        [Test]
        public void KillAll_LockfileWithDifferentPort_IsStillCleaned()
        {
            // Scenario: user changed port in UI; running server wrote lockfile under old port.
            // KillAll must find the lockfile even when its port != MCPServer.ServerPort.
            var differentPort = MCPServer.ServerPort + 1;
            var lockFile = Path.Combine(_scope.Path, $"server-{differentPort}-99999.lock");
            File.WriteAllText(lockFile, "99999\n");  // dead PID — stale cleanup path
            MCPActions.KillAll();
            Assert.IsFalse(File.Exists(lockFile),
                "Lock file with a different port must still be cleaned when PID is dead");
        }

        [Test]
        public void KillByPort_MissingDir_DoesNotThrow()
        {
            MCPActions.OverrideLockDir = System.IO.Path.Combine(_scope.Path, "nonexistent");
            Assert.DoesNotThrow(() => MCPActions.KillByPort(9500));
        }

        [Test]
        public void KillByPort_StaleLock_Cleaned()
        {
            var lockFile = Path.Combine(_scope.Path, "server-9500-99999.lock");
            File.WriteAllText(lockFile, "99999\n");
            MCPActions.KillByPort(9500);
            Assert.IsFalse(File.Exists(lockFile));
        }

        [Test]
        public void KillByPort_DifferentPort_NotTouched()
        {
            var lock9500 = Path.Combine(_scope.Path, "server-9500-99999.lock");
            var lock9600 = Path.Combine(_scope.Path, "server-9600-99999.lock");
            File.WriteAllText(lock9500, "99999\n");
            File.WriteAllText(lock9600, "99999\n");
            MCPActions.KillByPort(9500);
            Assert.IsFalse(File.Exists(lock9500), "Killed port lock must be cleaned");
            Assert.IsTrue(File.Exists(lock9600), "Other port lock must survive");
        }

        [Test]
        public void KillByPort_CleansPortFiles()
        {
            var portsDir = Path.Combine(_scope.Path, "ports");
            Directory.CreateDirectory(portsDir);
            File.WriteAllText(Path.Combine(portsDir, "12345.port"), "9500\n");
            File.WriteAllText(Path.Combine(portsDir, "12345.chat-port"), "data");
            File.WriteAllText(Path.Combine(portsDir, "12345.reload-port"), "data");
            MCPActions.KillByPort(9500);
            Assert.IsFalse(File.Exists(Path.Combine(portsDir, "12345.port")));
            Assert.IsFalse(File.Exists(Path.Combine(portsDir, "12345.chat-port")));
            Assert.IsFalse(File.Exists(Path.Combine(portsDir, "12345.reload-port")));
        }

        // M17: RestartRelay must not thread-hop via Task.Run — SessionState (used deep in
        // RelaySpawner.EnsureRunning) is main-thread-only. Structural guard via source file read
        // (same technique as SyncHelperTests.ImportPackageSources_DoesNotDeleteDigestCache).
        [Test]
        public void RestartRelay_DoesNotUseTaskRun()
        {
            var src = ReadRequiredPackageSource(typeof(MCPActions), "Editor/MCPActions.cs");
            var start = src.IndexOf("static void RestartRelay");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "RestartRelay method not found");
            var end = src.IndexOf("\n        }", start);
            var body = src.Substring(start, end - start);
            StringAssert.DoesNotContain("Task.Run", body,
                "M17: RestartRelay must use EditorApplication.delayCall, not Task.Run — " +
                "SessionState calls inside InvokeRelay(\"EnsureRunning\") are main-thread-only");
        }
    }
}
