// ARC-15 T3 (DEV-50): TCP-level regression for the classifier (DEV-48) + limiter (DEV-49)
// wired into ClientConnectionHandler.HandleClientAsync's length-prefix overflow branch.
// A known foreign-protocol probe (HTTP/TLS bytes reinterpreted as a length prefix) must
// close quietly — Debug.Log, never Warning. An unrecognized overflow still warns through
// DesyncWarnLimiter (honest desync stays visible, just rate-limited).
//
// Uses a real fixture-owned TcpListener+ClientSlot+RunAcceptLoop — skeleton mirrors
// ConnectionStabilityTests.MultiClientPingLiveness. Per repo "no false-green" rule, the
// effect under test is an exact Application.logMessageReceived spy count, never a
// LogAssert regex scan alone (a scan can't prove absence, only presence).
//
// RED/GREEN proof (ARC-0a Arm A): this file was authored against the pre-wiring code,
// where every overflow — probe or not — always Debug.LogWarning-ed. Both tests below
// went red before ClientConnectionHandler.cs's overflow branch called
// IsKnownForeignProtocolProbe/_desyncLimiter; they pass once wired.
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HttpGarbageProbeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Static DesyncWarnLimiter is process-wide (one instance for the whole
        // ClientConnectionHandler, by design — ARC-15 §6). Reset it per test so a
        // real-clock 30s suppression window from a previous test (or a previous full-suite
        // run in the same Editor domain) can never leak into this test's assertions.
        [SetUp]
        public void ResetSharedDesyncLimiter() => ClientConnectionHandler.ResetDesyncLimiterForTests();

        [Test, Timeout(10000)]
        public async Task KnownHttpProbe_ClosesQuietly_NoWarningLogged()
        {
            var spy = new List<(string message, LogType type)>();
            void OnLog(string message, string stackTrace, LogType type) => spy.Add((message, type));

            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            listener.Start();
            Application.logMessageReceived += OnLog;
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "http-probe-test", lifetime, lifetime.Token);

                using (var probeClient = new TcpClient())
                {
                    await probeClient.ConnectAsync("127.0.0.1", port);
                    var probeBytes = Encoding.ASCII.GetBytes("GET ");
                    await probeClient.GetStream()
                        .WriteAsync(probeBytes, 0, probeBytes.Length);
                }

                await ConnectionStabilityTests.AwaitConditionAsync(() => slot.CountActive() == 0 && spy.Count > 0,
                    TimeSpan.FromSeconds(5),
                    "Handler did not close the probe connection and log within the timeout.");

                Assert.AreEqual(0, CountBiomeLogs(spy, LogType.Warning),
                    "A known HTTP probe must never produce a Warning-level log.");
                Assert.AreEqual(1, CountBiomeLogs(spy, LogType.Log, m => m.Contains("GET")),
                    "Exactly one info-level log naming the probe bytes is expected.");

                // Liveness check: rejecting a probe must not wedge the slot the probe
                // occupied — a normal client on the same listener still gets served
                // (RC-B verdict: probes are self-limiting, no capacity-side fix needed).
                using var realClient = new TcpClient();
                await realClient.ConnectAsync("127.0.0.1", port);
                var stream = realClient.GetStream();
                stream.ReadTimeout = 3000;
                stream.WriteTimeout = 3000;
                ConnectionStabilityTests.TcpSendFrame(stream, "{\"id\":\"p1\",\"cmd\":\"ping\",\"args\":{}}");
                var reply = ConnectionStabilityTests.TcpReadFrame(stream);
                StringAssert.Contains("pong", reply, "Slot must be reusable after a rejected probe.");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (acceptLoop != null)
                    await AwaitTaskStoppedAsync(acceptLoop, TimeSpan.FromSeconds(2), "accept loop");
            }
        }

        [Test, Timeout(10000)]
        public async Task UnrecognizedGarbage_StillWarns()
        {
            var spy = new List<(string message, LogType type)>();
            void OnLog(string message, string stackTrace, LogType type) => spy.Add((message, type));

            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            listener.Start();
            Application.logMessageReceived += OnLog;
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "http-probe-test", lifetime, lifetime.Token);

                using (var client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", port);
                    // 0x01020304 = 16,909,060 — exceeds MaxMessageSize, matches none of the
                    // 7 known ASCII prefixes and isn't the TLS handshake byte: honest desync.
                    var garbage = new byte[] { 0x01, 0x02, 0x03, 0x04 };
                    await client.GetStream()
                        .WriteAsync(garbage, 0, garbage.Length);
                }

                await ConnectionStabilityTests.AwaitConditionAsync(() => slot.CountActive() == 0 && spy.Count > 0,
                    TimeSpan.FromSeconds(5),
                    "Handler did not close the garbage connection and log within the timeout.");

                Assert.AreEqual(1, CountBiomeLogs(spy, LogType.Warning),
                    "An unrecognized overflow must still warn exactly once (honest desync path).");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (acceptLoop != null)
                    await AwaitTaskStoppedAsync(acceptLoop, TimeSpan.FromSeconds(2), "accept loop");
            }
        }

        // Filters to this handler's own tagged logs so unrelated Editor background noise
        // (asset import, shader compiler) sharing the live process cannot inflate the count.
        private static int CountBiomeLogs(
            List<(string message, LogType type)> spy, LogType type, Func<string, bool> predicate = null)
        {
            var count = 0;
            foreach (var entry in spy)
            {
                if (entry.type != type) continue;
                if (!entry.message.Contains(BiomeLabel.Tag)) continue;
                if (predicate != null && !predicate(entry.message)) continue;
                count++;
            }
            return count;
        }

        // AwaitConditionAsync lives on ConnectionStabilityTests and is reused here —
        // DRY, matching how the TCP framing helpers are already shared.

        private static async Task AwaitTaskStoppedAsync(Task task, TimeSpan timeout, string label)
        {
            var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
            if (completed != task)
                throw new TimeoutException($"Fixture-owned {label} did not stop.");
            await task.ConfigureAwait(false);
        }
    }
}
