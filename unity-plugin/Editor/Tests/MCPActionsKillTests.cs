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
        public void Kill_ForwardsToKillAll()
        {
            // Kill() must delegate to KillAll() — both must not throw when no lockfiles present.
            Assert.DoesNotThrow(() => MCPActions.Kill());
            Assert.DoesNotThrow(() => MCPActions.KillAll());
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
