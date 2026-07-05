// Tests: relay lifecycle across domain reload simulation.
// Verifies that relay stays alive, sessions resume, and stale state is discarded.
// Uses RelaySpawner seams — no real Python relay required.
#if UNITY_MCP_CHAT
using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayReloadSurvivalTests
    {
        private Func<ProcessStartInfo, Process>   _origFactory;
        private Func<(string cmd, string[] argv)> _origResolver;
        private TimeSpan                          _origTimeout;
        private Func<int, bool>                   _origTcpAlive;

        [SetUp]
        public void SetUp()
        {
            _origFactory  = RelaySpawner.ProcessFactory;
            _origResolver = RelaySpawner.CommandResolver;
            _origTimeout  = RelaySpawner.ReadTimeout;
            _origTcpAlive = RelaySpawner.TcpAliveOverride;
            RelaySpawner.Stop();
            ClearRelaySession();
            ReloadGuard.ResetForTest();
            ReloadGuard.OverrideFilePath(TempStatePath());
            ReloadGuard.ClearPendingState();
        }

        [TearDown]
        public void TearDown()
        {
            RelaySpawner.Stop();
            ClearRelaySession();
            RelaySpawner.ProcessFactory  = _origFactory;
            RelaySpawner.CommandResolver = _origResolver;
            RelaySpawner.ReadTimeout     = _origTimeout;
            RelaySpawner.TcpAliveOverride = _origTcpAlive;
            ReloadGuard.ClearPendingState();
            ReloadGuard.ResetForTest();
            // Restore default path so other test fixtures are not affected.
            ReloadGuard.OverrideFilePath(System.IO.Path.Combine("Library", "MCP_ChatPendingTurn.txt"));
        }

        // ── OnBeforeReload ────────────────────────────────────────────────────

        [Test]
        public void OnBeforeReload_WhenRelayRunning_DoesNotKillProcess()
        {
            SetMockRelay(19710);
            RelaySpawner.EnsureRunning();
            Assert.IsTrue(RelaySpawner.IsRunning);

            RelaySpawner.OnBeforeReload();

            Assert.IsTrue(RelaySpawner.IsRunning, "Relay must survive OnBeforeReload");
        }

        [Test]
        public void OnBeforeReload_PreservesSessionStatePort()
        {
            SetMockRelay(19711);
            RelaySpawner.EnsureRunning();
            int portBefore = SessionState.GetInt(RelaySpawner.PortKey, 0);

            RelaySpawner.OnBeforeReload();

            Assert.AreEqual(portBefore, SessionState.GetInt(RelaySpawner.PortKey, 0),
                "Port must not be erased by OnBeforeReload");
        }

        [Test]
        public void OnBeforeReload_PreservesSessionStatePid()
        {
            SetMockRelay(19712);
            RelaySpawner.EnsureRunning();
            int pidBefore = SessionState.GetInt(RelaySpawner.PidKey, 0);

            RelaySpawner.OnBeforeReload();

            Assert.AreEqual(pidBefore, SessionState.GetInt(RelaySpawner.PidKey, 0),
                "PID must not be erased by OnBeforeReload");
        }

        // ── OnAfterReload ─────────────────────────────────────────────────────

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
        public void OnAfterReload_MultipleSubscribers_AllFire()
        {
            int count = 0;
            Action h1 = () => count++;
            Action h2 = () => count++;
            RelaySpawner.OnAfterReloadResume += h1;
            RelaySpawner.OnAfterReloadResume += h2;
            try
            {
                RelaySpawner.OnAfterReload();
                Assert.AreEqual(2, count);
            }
            finally
            {
                RelaySpawner.OnAfterReloadResume -= h1;
                RelaySpawner.OnAfterReloadResume -= h2;
            }
        }

        [Test]
        public void OnAfterReload_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => RelaySpawner.OnAfterReload());
        }

        // ── EnsureRunning after simulated reload ──────────────────────────────

        [Test]
        public void EnsureRunning_AfterReload_RelayAlive_ReturnsSamePort()
        {
            SetMockRelay(19720);
            var portFirst = RelaySpawner.EnsureRunning();
            // Simulate domain reload: static _process field becomes null post-reload.
            // We can't null it directly (private), but relay's PID is alive in SessionState.
            // EnsureRunning should detect alive PID and return same port without re-spawn.
            var portSecond = RelaySpawner.EnsureRunning();
            Assert.AreEqual(portFirst, portSecond);
        }

        [Test]
        public void EnsureRunning_AfterReload_RelayAlive_DoesNotRespawn()
        {
            int spawnCount = 0;
            SetMockRelay(19721, onSpawn: () => spawnCount++);
            RelaySpawner.EnsureRunning();
            Assert.AreEqual(1, spawnCount, "Should spawn exactly once");

            RelaySpawner.EnsureRunning(); // second call — relay still alive
            Assert.AreEqual(1, spawnCount, "Should NOT respawn when relay alive");
        }

        [Test]
        public void EnsureRunning_AfterReload_RelayDied_Respawns()
        {
            // Place a dead PID in SessionState
            SessionState.SetInt(RelaySpawner.PortKey, 19722);
            SessionState.SetInt(RelaySpawner.PidKey,  99999999); // dead PID

            int spawnCount = 0;
            SetMockRelay(19723, onSpawn: () => spawnCount++);

            RelaySpawner.EnsureRunning();
            Assert.AreEqual(1, spawnCount, "Should respawn when relay died");
        }

        [Test]
        public void EnsureRunning_AfterReload_RelayDied_ReturnsFreshPort()
        {
            SessionState.SetInt(RelaySpawner.PortKey, 19724);
            SessionState.SetInt(RelaySpawner.PidKey,  99999999);

            SetMockRelay(19725);
            var port = RelaySpawner.EnsureRunning();
            Assert.AreEqual(19725, port, "Fresh spawn must return new port");
        }

        // ── Multiple rapid reloads ────────────────────────────────────────────

        [Test]
        public void MultipleReloads_DoNotOrphan_RelayStaysAlive()
        {
            SetMockRelay(19730);
            RelaySpawner.EnsureRunning();
            Assert.IsTrue(RelaySpawner.IsRunning);

            // Simulate 3 rapid reloads
            for (int i = 0; i < 3; i++)
            {
                RelaySpawner.OnBeforeReload();
                RelaySpawner.OnAfterReload();
            }

            Assert.IsTrue(RelaySpawner.IsRunning, "Relay must survive rapid reloads");
        }

        // ── PendingTurnState reload survival ──────────────────────────────────

        [Test]
        public void PendingTurnState_RoundTrip_SessionIdPreserved()
        {
            var state = new PendingTurnState("sess-xyz", "hello", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running");
            ReloadGuard.SavePendingState(state);

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("sess-xyz", loaded.Value.SessionId);
        }

        [Test]
        public void PendingTurnState_RoundTrip_PendingTextPreserved()
        {
            var state = new PendingTurnState("s", "my turn text", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running");
            ReloadGuard.SavePendingState(state);

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.AreEqual("my turn text", loaded.Value.PendingText);
        }

        [Test]
        public void PendingTurnState_RoundTrip_AgentModePreserved()
        {
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: true, agentName: "my-agent", activityPhase: "Running");
            ReloadGuard.SavePendingState(state);

            var loaded = ReloadGuard.LoadPendingState();
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.Value.AgentMode);
            Assert.AreEqual("my-agent", loaded.Value.AgentName);
        }

        [Test]
        public void PendingTurnState_AfterClear_LoadReturnsNull()
        {
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running");
            ReloadGuard.SavePendingState(state);
            ReloadGuard.ClearPendingState();

            Assert.IsNull(ReloadGuard.LoadPendingState());
        }

        [Test]
        public void PendingTurnState_NoFile_LoadReturnsNull()
        {
            // TearDown already clears; this just confirms fresh state
            Assert.IsNull(ReloadGuard.LoadPendingState());
        }

        // ── Stale state discard ───────────────────────────────────────────────

        [Test]
        public void IsStale_InFlight_OlderThanThreshold_ReturnsTrue()
        {
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running",
                savedAtUtc: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120);

            Assert.IsTrue(PendingTurnState.IsStale(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), thresholdSec: 60));
        }

        [Test]
        public void IsStale_InFlight_Fresher_ReturnsFalse()
        {
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running",
                savedAtUtc: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10);

            Assert.IsFalse(PendingTurnState.IsStale(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), thresholdSec: 60));
        }

        [Test]
        public void IsStale_IdleSave_AlwaysFalse()
        {
            // Idle saves are exempt from staleness check
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: false, agentName: null, activityPhase: "Idle",
                savedAtUtc: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 9999);

            Assert.IsFalse(PendingTurnState.IsStale(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), thresholdSec: 60));
        }

        [Test]
        public void IsStale_LegacyZeroTimestamp_ReturnsFalse()
        {
            var state = new PendingTurnState("s", "t", new string[0],
                agentMode: false, agentName: null, activityPhase: "Running",
                savedAtUtc: 0);

            Assert.IsFalse(PendingTurnState.IsStale(state, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), thresholdSec: 60));
        }

        // ── Stop clears session ───────────────────────────────────────────────

        [Test]
        public void Stop_AfterReload_ClearsSessionState()
        {
            SetMockRelay(19740);
            RelaySpawner.EnsureRunning();
            RelaySpawner.Stop();

            Assert.AreEqual(0, SessionState.GetInt(RelaySpawner.PortKey, 0));
            Assert.AreEqual(0, SessionState.GetInt(RelaySpawner.PidKey,  0));
        }

        [Test]
        public void Stop_ThenEnsureRunning_Respawns()
        {
            int spawnCount = 0;
            SetMockRelay(19741, onSpawn: () => spawnCount++);
            RelaySpawner.EnsureRunning();
            RelaySpawner.Stop();
            RelaySpawner.EnsureRunning();
            Assert.AreEqual(2, spawnCount, "Stop + EnsureRunning must spawn twice");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string TempStatePath()
            => System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"relay_test_state_{System.IO.Path.GetRandomFileName()}.txt");

        private static void ClearRelaySession()
        {
            SessionState.EraseInt(RelaySpawner.PortKey);
            SessionState.EraseInt(RelaySpawner.PidKey);
        }

        private void SetMockRelay(int port, Action onSpawn = null)
        {
            RelaySpawner.CommandResolver  = () => ("bash", Array.Empty<string>());
            RelaySpawner.TcpAliveOverride = _ => true;
            RelaySpawner.ProcessFactory   = _ =>
            {
                onSpawn?.Invoke();
                return SpawnFakeRelay(port);
            };
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
    }
}
#endif
