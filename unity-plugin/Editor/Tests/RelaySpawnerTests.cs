// TDD tests for RelaySpawner — all process interactions injected via seams.
// Uses ProcessFactory + CommandResolver to avoid requiring a real Python install.
using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RelaySpawnerTests
    {
        private Func<ProcessStartInfo, Process>   _origFactory;
        private Func<(string cmd, string[] argv)> _origResolver;
        private TimeSpan                          _origTimeout;

        [SetUp]
        public void SetUp()
        {
            _origFactory  = RelaySpawner.ProcessFactory;
            _origResolver = RelaySpawner.CommandResolver;
            _origTimeout  = RelaySpawner.ReadTimeout;
            RelaySpawner.Stop();
            ClearSessionState();
        }

        [TearDown]
        public void TearDown()
        {
            RelaySpawner.Stop();
            ClearSessionState();
            RelaySpawner.ProcessFactory   = _origFactory;
            RelaySpawner.CommandResolver  = _origResolver;
            RelaySpawner.ReadTimeout      = _origTimeout;
            RelaySpawner.TcpAliveOverride = null;
            InstallSourceDetector.ClearTestOverride();
        }

        // ── ParseRelayPort (pure unit, no process) ────────────────────────────

        [Test]
        public void ParseRelayPort_ValidLine_ReturnsPort()
        {
            Assert.AreEqual(12345, RelaySpawner.ParseRelayPort("relay_port:12345"));
        }

        [Test]
        public void ParseRelayPort_ValidLineWithWhitespace_ReturnsPort()
        {
            Assert.AreEqual(9700, RelaySpawner.ParseRelayPort("relay_port:9700 "));
        }

        [Test]
        public void ParseRelayPort_NullLine_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort(null));
        }

        [Test]
        public void ParseRelayPort_EmptyLine_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort(""));
        }

        [Test]
        public void ParseRelayPort_WrongPrefix_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort("ready:12345"));
        }

        [Test]
        public void ParseRelayPort_NonIntegerPort_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort("relay_port:abc"));
        }

        // ── IsProcessAlive (pure unit) ────────────────────────────────────────

        [Test]
        public void IsProcessAlive_ZeroPid_ReturnsFalse()
        {
            Assert.IsFalse(RelaySpawner.IsProcessAlive(0));
        }

        [Test]
        public void IsProcessAlive_NegativePid_ReturnsFalse()
        {
            Assert.IsFalse(RelaySpawner.IsProcessAlive(-1));
        }

        [Test]
        public void IsProcessAlive_VeryLargePid_ReturnsFalse()
        {
            // PID 99999999 almost certainly does not exist
            Assert.IsFalse(RelaySpawner.IsProcessAlive(99999999));
        }

        [Test]
        public void IsProcessAlive_CurrentProcessPid_ReturnsFalse()
        {
            // Self-PID guard: Unity process must never be confused for the relay
            var selfPid = Process.GetCurrentProcess().Id;
            Assert.IsFalse(RelaySpawner.IsProcessAlive(selfPid));
        }

        // ── EnsureRunning — basic spawn ───────────────────────────────────────

        [Test]
        public void EnsureRunning_SpawnsProcess_ReturnsExpectedPort()
        {
            SetMockRelay(port: 19701);
            var port = RelaySpawner.EnsureRunning();
            Assert.AreEqual(19701, port);
        }

        [Test]
        public void EnsureRunning_SpawnsProcess_SavesPortToSessionState()
        {
            SetMockRelay(port: 19702);
            RelaySpawner.EnsureRunning();
            Assert.AreEqual(19702, SessionState.GetInt("MCPChat_Relay_Port", 0));
        }

        [Test]
        public void EnsureRunning_SpawnsProcess_SavesPidToSessionState()
        {
            SetMockRelay(port: 19703);
            RelaySpawner.EnsureRunning();
            Assert.Greater(SessionState.GetInt("MCPChat_Relay_PID", 0), 0);
        }

        [Test]
        public void EnsureRunning_CallsProcessFactory_WithCommandAsFileName()
        {
            ProcessStartInfo capturedPsi = null;
            RelaySpawner.CommandResolver = () => ("testpython3", Array.Empty<string>());
            RelaySpawner.ProcessFactory = psi =>
            {
                capturedPsi = psi;
                return SpawnFakeRelay(19704);
            };
            RelaySpawner.EnsureRunning();
            Assert.IsNotNull(capturedPsi);
            Assert.AreEqual("testpython3", capturedPsi.FileName);
        }

        [Test]
        public void EnsureRunning_CallsProcessFactory_WithRelayModuleArgumentList()
        {
            ProcessStartInfo capturedPsi = null;
            RelaySpawner.CommandResolver = () => ("python3", new[] { "-m", "unity_mcp.chat_relay" });
            RelaySpawner.ProcessFactory = psi => { capturedPsi = psi; return SpawnFakeRelay(19705); };
            RelaySpawner.EnsureRunning();
            CollectionAssert.AreEqual(new[] { "-m", "unity_mcp.chat_relay" }, capturedPsi.ArgumentList);
        }

        // ── Tier 1: non-local uvx invocation (ARCH-relay-upm-bootstrap.md Q2) ─

        [Test]
        public void EnsureRunning_NonLocalUvxCommand_BuildsExpectedArgumentList()
        {
            ProcessStartInfo capturedPsi = null;
            RelaySpawner.CommandResolver = () => ("/usr/bin/uvx",
                new[] { "--from", "git+https://example.com/repo.git#subdirectory=server", "unity-biome-mcp-relay" });
            RelaySpawner.ProcessFactory = psi => { capturedPsi = psi; return SpawnFakeRelay(19706); };

            RelaySpawner.EnsureRunning();

            Assert.AreEqual("/usr/bin/uvx", capturedPsi.FileName);
            CollectionAssert.AreEqual(
                new[] { "--from", "git+https://example.com/repo.git#subdirectory=server", "unity-biome-mcp-relay" },
                capturedPsi.ArgumentList);
        }

        [Test]
        public void EnsureRunning_CommandResolverReturnsNull_NonLocal_ThrowsWithUvHint_NotInstallPy()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            RelaySpawner.CommandResolver = () => (null, null);

            var ex = Assert.Throws<InvalidOperationException>(() => RelaySpawner.EnsureRunning());
            Assert.IsTrue(ex.Message.Contains("uv not found"), ex.Message);
            Assert.IsFalse(ex.Message.Contains("install.py"), ex.Message);
        }

        [Test]
        public void EnsureRunning_CommandResolverReturnsNull_Local_ThrowsWithInstallPyHint()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            RelaySpawner.CommandResolver = () => (null, null);

            var ex = Assert.Throws<InvalidOperationException>(() => RelaySpawner.EnsureRunning());
            Assert.IsTrue(ex.Message.Contains("install.py"), ex.Message);
        }

        // ── Tier 1: timeout selection (pure helper — no real 45s wait) ────────

        [Test]
        public void TimeoutFor_Local_ReturnsReadTimeout()
        {
            RelaySpawner.ReadTimeout = TimeSpan.FromSeconds(5);
            Assert.AreEqual(TimeSpan.FromSeconds(5), RelaySpawner.TimeoutFor(isLocal: true));
        }

        [Test]
        public void TimeoutFor_NonLocal_Returns45Seconds()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(45), RelaySpawner.TimeoutFor(isLocal: false));
        }

        // ── EnsureRunning — already running ───────────────────────────────────

        [Test]
        public void EnsureRunning_WhenAlreadyRunning_SkipsSpawn()
        {
            // Self-PID guard excludes current process; spawn a real process for a live PID
            var liveRelay = SpawnFakeRelay(19800);
            try
            {
                SessionState.SetInt("MCPChat_Relay_Port", 19800);
                SessionState.SetInt("MCPChat_Relay_PID",  liveRelay.Id);

                // TcpAliveOverride: bypass real TCP probe (port 19800 has no listener in tests)
                RelaySpawner.TcpAliveOverride = port => true;

                int spawnCount = 0;
                RelaySpawner.CommandResolver = () => ("python3", Array.Empty<string>());
                RelaySpawner.ProcessFactory = _ => { spawnCount++; return SpawnFakeRelay(19999); };

                RelaySpawner.EnsureRunning();

                Assert.AreEqual(0, spawnCount, "Factory must not be called when relay is alive");
            }
            finally
            {
                RelaySpawner.TcpAliveOverride = null;
                try { liveRelay.Kill(); } catch { }
                liveRelay.Dispose();
            }
        }

        [Test]
        public void EnsureRunning_WhenAlreadyRunning_ReturnsCachedPort()
        {
            // Self-PID guard excludes current process; spawn a real process for a live PID
            var liveRelay = SpawnFakeRelay(19801);
            try
            {
                SessionState.SetInt("MCPChat_Relay_Port", 19801);
                SessionState.SetInt("MCPChat_Relay_PID",  liveRelay.Id);

                // TcpAliveOverride: bypass real TCP probe (port 19801 has no listener in tests)
                RelaySpawner.TcpAliveOverride = port => true;

                RelaySpawner.CommandResolver = () => ("python3", Array.Empty<string>());
                RelaySpawner.ProcessFactory = _ => SpawnFakeRelay(99999);

                var port = RelaySpawner.EnsureRunning();

                Assert.AreEqual(19801, port);
            }
            finally
            {
                RelaySpawner.TcpAliveOverride = null;
                try { liveRelay.Kill(); } catch { }
                liveRelay.Dispose();
            }
        }

        [Test]
        public void EnsureRunning_WhenPidDead_Respawns()
        {
            SessionState.SetInt("MCPChat_Relay_Port", 19802);
            SessionState.SetInt("MCPChat_Relay_PID",  99999999); // dead PID

            int spawnCount = 0;
            SetMockRelay(port: 19803);
            var origFactory = RelaySpawner.ProcessFactory;
            RelaySpawner.ProcessFactory = psi => { spawnCount++; return origFactory(psi); };

            RelaySpawner.EnsureRunning();

            Assert.AreEqual(1, spawnCount, "Factory must be called once to respawn");
        }

        // ── C5: stdout noise before relay_port ───────────────────────────────

        [Test]
        public void EnsureRunning_NoiseLinesBeforePort_ParsesPortCorrectly()
        {
            RelaySpawner.CommandResolver = () => ("bash", Array.Empty<string>());
            RelaySpawner.ProcessFactory = _ =>
            {
                var psi = new ProcessStartInfo("bash")
                {
                    Arguments              = "-c \"echo 'DeprecationWarning: foo'; echo 'WARNING: bar'; echo relay_port:19850; exec sleep 60\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                return Process.Start(psi);
            };
            var port = RelaySpawner.EnsureRunning();
            Assert.AreEqual(19850, port);
        }

        [Test]
        public void EnsureRunning_ManyNoiseLinesBeforePort_StillFinds()
        {
            RelaySpawner.CommandResolver = () => ("bash", Array.Empty<string>());
            RelaySpawner.ProcessFactory = _ =>
            {
                var psi = new ProcessStartInfo("bash")
                {
                    Arguments              = "-c \"for i in 1 2 3 4 5; do echo noise; done; echo relay_port:19851; exec sleep 60\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                return Process.Start(psi);
            };
            var port = RelaySpawner.EnsureRunning();
            Assert.AreEqual(19851, port);
        }

        [Test]
        public void EnsureRunning_OnlyNoiseNoPort_ThrowsTimeout()
        {
            RelaySpawner.ReadTimeout    = TimeSpan.FromMilliseconds(200);
            RelaySpawner.CommandResolver = () => ("bash", Array.Empty<string>());
            RelaySpawner.ProcessFactory = _ =>
            {
                var psi = new ProcessStartInfo("bash")
                {
                    Arguments              = "-c \"echo noise; exec sleep 10\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                return Process.Start(psi);
            };
            Assert.Throws<TimeoutException>(() => RelaySpawner.EnsureRunning());
        }

        // ── m1: PID reuse false-positive ──────────────────────────────────────

        [Test]
        public void EnsureRunning_PidAliveButTcpDead_Respawns()
        {
            var liveProc = SpawnFakeRelay(19860);
            try
            {
                SessionState.SetInt("MCPChat_Relay_Port", 19860);
                SessionState.SetInt("MCPChat_Relay_PID",  liveProc.Id);

                // TCP probe returns false: relay crashed, PID reused by another process
                RelaySpawner.TcpAliveOverride = port => false;

                int spawnCount = 0;
                SetMockRelay(port: 19861);
                var origFactory = RelaySpawner.ProcessFactory;
                RelaySpawner.ProcessFactory = psi => { spawnCount++; return origFactory(psi); };

                RelaySpawner.EnsureRunning();

                Assert.AreEqual(1, spawnCount, "Must respawn when TCP probe fails");
            }
            finally
            {
                RelaySpawner.TcpAliveOverride = null;
                try { liveProc.Kill(); } catch { }
                liveProc.Dispose();
            }
        }

        [Test]
        public void EnsureRunning_PidAliveAndTcpAlive_SkipsSpawn()
        {
            var liveProc = SpawnFakeRelay(19870);
            try
            {
                SessionState.SetInt("MCPChat_Relay_Port", 19870);
                SessionState.SetInt("MCPChat_Relay_PID",  liveProc.Id);

                // TCP probe returns true: relay genuinely alive
                RelaySpawner.TcpAliveOverride = port => true;

                int spawnCount = 0;
                RelaySpawner.CommandResolver = () => ("python3", Array.Empty<string>());
                RelaySpawner.ProcessFactory = _ => { spawnCount++; return SpawnFakeRelay(99999); };

                RelaySpawner.EnsureRunning();

                Assert.AreEqual(0, spawnCount, "Must not spawn when PID+TCP both alive");
            }
            finally
            {
                RelaySpawner.TcpAliveOverride = null;
                try { liveProc.Kill(); } catch { }
                liveProc.Dispose();
            }
        }

        // ── EnsureRunning — command not found ─────────────────────────────────

        [Test]
        public void EnsureRunning_CommandNull_ThrowsInvalidOperation()
        {
            RelaySpawner.CommandResolver = () => (null, null);
            Assert.Throws<InvalidOperationException>(() => RelaySpawner.EnsureRunning());
        }

        [Test]
        public void EnsureRunning_CommandEmpty_ThrowsInvalidOperation()
        {
            RelaySpawner.CommandResolver = () => ("", null);
            Assert.Throws<InvalidOperationException>(() => RelaySpawner.EnsureRunning());
        }

        // ── EnsureRunning — timeout ───────────────────────────────────────────

        [Test]
        public void EnsureRunning_RelayNoOutput_ThrowsTimeout()
        {
            RelaySpawner.ReadTimeout    = TimeSpan.FromMilliseconds(100); // fast timeout for tests
            RelaySpawner.CommandResolver = () => ("bash", Array.Empty<string>());
            RelaySpawner.ProcessFactory = _ =>
            {
                var psi = new ProcessStartInfo("bash")
                {
                    Arguments = "-c \"exec sleep 10\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                return Process.Start(psi);
            };
            Assert.Throws<TimeoutException>(() => RelaySpawner.EnsureRunning());
        }

        // ── Stop ──────────────────────────────────────────────────────────────

        [Test]
        public void Stop_ClearsPortFromSessionState()
        {
            SetMockRelay(port: 19900);
            RelaySpawner.EnsureRunning();
            RelaySpawner.Stop();
            Assert.AreEqual(0, SessionState.GetInt("MCPChat_Relay_Port", 0));
        }

        [Test]
        public void Stop_ClearsPidFromSessionState()
        {
            SetMockRelay(port: 19901);
            RelaySpawner.EnsureRunning();
            RelaySpawner.Stop();
            Assert.AreEqual(0, SessionState.GetInt("MCPChat_Relay_PID", 0));
        }

        [Test]
        public void Stop_SetsIsRunning_False()
        {
            SetMockRelay(port: 19902);
            RelaySpawner.EnsureRunning();
            RelaySpawner.Stop();
            Assert.IsFalse(RelaySpawner.IsRunning);
        }

        // ── OnBeforeReload / OnAfterReload ────────────────────────────────────

        [Test]
        public void OnBeforeReload_DoesNotKillRelayProcess()
        {
            SetMockRelay(port: 19950);
            RelaySpawner.EnsureRunning();
            RelaySpawner.OnBeforeReload();
            Assert.IsTrue(RelaySpawner.IsRunning, "Relay must survive OnBeforeReload");
        }

        [Test]
        public void OnAfterReload_FiresOnAfterReloadResume()
        {
            bool fired = false;
            Action handler = () => fired = true;
            RelaySpawner.OnAfterReloadResume += handler;
            try
            {
                RelaySpawner.OnAfterReload();
                Assert.IsTrue(fired);
            }
            finally
            {
                RelaySpawner.OnAfterReloadResume -= handler;
            }
        }

        [Test]
        public void OnAfterReload_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RelaySpawner.OnAfterReload());
        }

        // ── Defaults / properties ─────────────────────────────────────────────

        [Test]
        public void ProcessFactory_DefaultValue_IsNotNull()
        {
            Assert.IsNotNull(RelaySpawner.ProcessFactory);
        }

        [Test]
        public void CommandResolver_DefaultValue_IsNotNull()
        {
            Assert.IsNotNull(RelaySpawner.CommandResolver);
        }

        [Test]
        public void RelayPort_WhenNoSession_ReturnsZero()
        {
            Assert.AreEqual(0, RelaySpawner.RelayPort);
        }

        [Test]
        public void IsRunning_WhenNoProcess_ReturnsFalse()
        {
            Assert.IsFalse(RelaySpawner.IsRunning);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetMockRelay(int port)
        {
            RelaySpawner.CommandResolver = () => ("bash", Array.Empty<string>());
            RelaySpawner.ProcessFactory = _ => SpawnFakeRelay(port);
        }

        private static Process SpawnFakeRelay(int port)
        {
            var psi = new ProcessStartInfo("bash")
            {
                Arguments              = $"-c \"echo relay_port:{port}; exec sleep 60\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            return Process.Start(psi);
        }

        private static void ClearSessionState()
        {
            SessionState.EraseInt("MCPChat_Relay_Port");
            SessionState.EraseInt("MCPChat_Relay_PID");
        }
    }
}
