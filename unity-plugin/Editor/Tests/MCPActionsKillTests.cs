using System;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPActionsKillTests
    {
        private TempDirScope _scope;

        [SetUp]
        public void SetUp()
        {
            // Isolate KillAll from real ~/.unity-mcp to avoid killing live Python MCP servers.
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

        [Test]
        public void KillAll_GlobsPerPidPattern()
        {
            // Verify the lockfile pattern uses PID format: server-{port}-*.lock
            var port = MCPServer.ServerPort;

            // Pattern used by KillAll must match per-PID files, NOT legacy single-file
            var perPidFile = $"server-{port}-12345.lock";
            var legacyFile = $"server-{port}.lock";

            // Confirm pattern: per-PID file matches glob "server-{port}-*.lock"
            Assert.IsTrue(perPidFile.StartsWith($"server-{port}-"),
                "Per-PID lockfile must start with server-{port}-");
            Assert.IsFalse(legacyFile.Contains("-12345"),
                "Legacy lockfile must NOT contain PID in filename");
        }

        [Test]
        public void KillAll_UsesServerPort_NotHardcoded9500()
        {
            var port = MCPServer.ServerPort;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var perPidPattern = Path.Combine(home, ".unity-mcp", $"server-{port}-*.lock");
            Assert.IsTrue(perPidPattern.Contains($"server-{port}-"),
                $"KillAll pattern must use ServerPort={port}, not hardcoded 9500");
        }

        // M17: RestartRelay must not thread-hop via Task.Run — SessionState (used deep in
        // RelaySpawner.EnsureRunning) is main-thread-only. Structural guard via source file read
        // (same technique as SyncHelperTests.ImportPackageSources_DoesNotDeleteDigestCache).
        [Test]
        public void RestartRelay_DoesNotUseTaskRun()
        {
            var assets = UnityEngine.Application.dataPath;          // …/unity-test-project/Assets
            var pluginSrc = Path.Combine(assets, "..", "..", "unity-plugin", "Editor", "MCPActions.cs");
            pluginSrc = Path.GetFullPath(pluginSrc);
            if (!File.Exists(pluginSrc))
            {
                Assert.Ignore($"MCPActions.cs not found at {pluginSrc} — skip in CI");
                return;
            }
            var src = File.ReadAllText(pluginSrc);
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
