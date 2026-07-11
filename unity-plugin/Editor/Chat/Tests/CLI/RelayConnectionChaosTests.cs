// Monkey tests: RelaySpawner/RelayBackend connection lifecycle chaos.
// Tests IsProcessAlive, ParseRelayPort, Stop idempotency, port cleanup,
// TcpAliveOverride seam, and multi-backend isolation.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayConnectionChaosTests
    {
        [SetUp]  public void SetUp()    => RelaySpawner.EnsureRunningOverride = () => 19900;
        [TearDown] public void TearDown()
        {
            RelayBackend.ProcessFactory        = null;
            RelaySpawner.EnsureRunningOverride  = null;
            RelaySpawner.TcpAliveOverride       = null;
            RelaySpawner.Stop();
            SessionState.EraseInt(RelaySpawner.PortKey);
            SessionState.EraseInt(RelaySpawner.PidKey);
        }

        static RelayChatProcess OkProc() =>
            new RelayChatProcess(j => "{\"ok\":true,\"data\":\"\"}");

        // ── IsProcessAlive edge cases ─────────────────────────────────────────

        [Test] public void IsProcessAlive_NegativePid_ReturnsFalse()
            => Assert.IsFalse(RelaySpawner.IsProcessAlive(-1));

        [Test] public void IsProcessAlive_ZeroPid_ReturnsFalse()
            => Assert.IsFalse(RelaySpawner.IsProcessAlive(0));

        [Test] public void IsProcessAlive_NegativeTwoPid_ReturnsFalse()
            => Assert.IsFalse(RelaySpawner.IsProcessAlive(-2));

        [Test] public void IsProcessAlive_SelfPid_ReturnsFalse()
        {
            // Code explicitly guards: if (pid == Process.GetCurrentProcess().Id) return false
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            Assert.IsFalse(RelaySpawner.IsProcessAlive(selfPid));
        }

        [Test] public void IsProcessAlive_NonExistentLargePid_ReturnsFalse()
            => Assert.IsFalse(RelaySpawner.IsProcessAlive(999_999_999));

        // ── ParseRelayPort ────────────────────────────────────────────────────

        [Test] public void ParseRelayPort_ValidLine_ReturnsPort()
            => Assert.AreEqual(9500, RelaySpawner.ParseRelayPort("relay_port:9500"));

        [Test] public void ParseRelayPort_WithLeadingSpaceAfterColon_ReturnsPort()
            => Assert.AreEqual(9999, RelaySpawner.ParseRelayPort("relay_port: 9999"));

        [Test] public void ParseRelayPort_NullLine_ThrowsFormatException()
            => Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort(null));

        [Test] public void ParseRelayPort_WrongPrefix_ThrowsFormatException()
            => Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort("port:9500"));

        [Test] public void ParseRelayPort_NonIntegerSuffix_ThrowsFormatException()
            => Assert.Throws<FormatException>(() => RelaySpawner.ParseRelayPort("relay_port:abc"));

        // ── TcpAliveOverride seam ─────────────────────────────────────────────

        [Test] public void TcpAliveOverride_Null_EnsureRunningOverrideStillWorks()
        {
            RelaySpawner.TcpAliveOverride = null; // no override
            // EnsureRunningOverride still short-circuits → returns 19900
            var port = RelaySpawner.EnsureRunning();
            Assert.AreEqual(19900, port);
        }

        [Test] public void TcpAliveOverride_Set_DoesNotThrow()
        {
            RelaySpawner.TcpAliveOverride = _ => true;
            // EnsureRunningOverride takes priority; TcpAliveOverride has no effect here
            Assert.DoesNotThrow(() => RelaySpawner.EnsureRunning());
        }

        // ── Backend lifecycle chaos ───────────────────────────────────────────

        [Test] public void Backend_IsRunning_FalseBeforeStart()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = new RelayBackend("claude","agent","m",0);
            Assert.IsFalse(b.IsRunning);
        }

        [Test] public void Backend_IsRunning_TrueAfterStart_FalseAfterStop()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = new RelayBackend("claude","agent","m",0);
            b.Start(); Assert.IsTrue(b.IsRunning);
            b.Stop();  Assert.IsFalse(b.IsRunning);
        }

        [Test] public void Backend_Stop10x_Idempotent()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = new RelayBackend("claude","agent","m",0); b.Start();
            Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) b.Stop(); });
        }

        [Test] public void Backend_Start10x_AlwaysSucceeds()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = new RelayBackend("claude","agent","m",0);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) b.Start(); });
            b.Stop();
        }

        [Test] public void Backend_Dispose_Idempotent()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var b = new RelayBackend("claude","agent","m",0); b.Start();
            Assert.DoesNotThrow(() => { b.Dispose(); b.Dispose(); b.Dispose(); });
        }

        // ── Spawner lifecycle ─────────────────────────────────────────────────

        [Test] public void Spawner_Stop_Idempotent_10x()
            => Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) RelaySpawner.Stop(); });

        [Test] public void Spawner_Stop_ErasesPortKey()
        {
            SessionState.SetInt(RelaySpawner.PortKey, 9500);
            RelaySpawner.Stop();
            Assert.AreEqual(0, SessionState.GetInt(RelaySpawner.PortKey, 0));
        }

        [Test] public void Spawner_EnsureRunningOverride_ReturnsCorrectPort()
        {
            RelaySpawner.EnsureRunningOverride = () => 42000;
            Assert.AreEqual(42000, RelaySpawner.EnsureRunning());
        }

        // ── Multi-backend isolation ───────────────────────────────────────────

        [Test] public void Backend_10Instances_AllSucceed()
        {
            RelayBackend.ProcessFactory = () => OkProc();
            var backends = new List<RelayBackend>();
            for (int i = 0; i < 10; i++)
            {
                var b = new RelayBackend("claude","agent","m",0);
                Assert.DoesNotThrow(() => b.Start());
                backends.Add(b);
            }
            foreach (var b in backends) b.Stop();
        }

        [Test] public void Backend_SendTurnAutoStarts_WhenNotRunning()
        {
            var sent = new List<string>();
            RelayBackend.ProcessFactory = () => new RelayChatProcess(j =>
            { lock (sent) sent.Add(j); return "{\"ok\":true,\"data\":\"\"}"; });
            var b = new RelayBackend("claude","agent","m",0);
            b.SendTurn("{\"type\":\"user\"}");
            lock (sent) Assert.IsTrue(sent.Exists(j => j.Contains("\"cmd\":\"start\"")));
            b.Stop();
        }
    }
}
