// TDD: ClientConnectionHandler → ICommandDispatcher seam (Phase 2.3, boundary-hardening).
// Proves the two call sites inside ClientConnectionHandler (get_enabled_tools fast-path,
// slow-path main-thread dispatch) route through the injected ClientConnectionHandler.Dispatcher
// seam rather than calling the static CommandRouter directly. A fake dispatcher returns a
// sentinel that the real CommandRouter would never produce — so these tests fail both if the
// seam is missing (compile error) and if a call site quietly reverts to calling CommandRouter
// directly (sentinel never observed).
//
// Uses a real fixture-owned TcpListener+ClientSlot+RunAcceptLoop — skeleton mirrors
// ConnectionStabilityTests.MultiClientPingLiveness / HttpGarbageProbeTests.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ClientConnectionHandlerDispatcherSeamTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeDispatcher : ICommandDispatcher
        {
            internal string ToolsSentinel = "__fake_tools_sentinel__";
            internal string ProcessSentinel = "__fake_process_sentinel__";

            public string ExecGetEnabledToolsCached() => ToolsSentinel;

            public void ProcessAsync(string json, TaskCompletionSource<string> tcs, string chatMode = "")
                => tcs.TrySetResult(ProcessSentinel);
        }

        // Local copy of the HttpGarbageProbeTests/ConnectionStabilityTests fixture-teardown
        // pattern (those helpers are private to their own classes).
        private static async Task AwaitAcceptLoopStoppedAsync(Task acceptLoop, TimeSpan timeout)
        {
            if (acceptLoop == null) return;
            var stopped = await Task.WhenAny(acceptLoop, Task.Delay(timeout)).ConfigureAwait(false);
            if (stopped != acceptLoop)
                throw new TimeoutException("Fixture-owned MCP accept loop did not stop.");
            await acceptLoop.ConfigureAwait(false);
        }

        [Test, Timeout(10000)]
        public async Task GetEnabledTools_FastPath_RoutesThroughInjectedDispatcher()
        {
            var fake = new FakeDispatcher();
            var original = ClientConnectionHandler.Dispatcher;
            ClientConnectionHandler.Dispatcher = fake;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "dispatcher-seam-test", lifetime, lifetime.Token);

                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port);
                var stream = client.GetStream();
                stream.ReadTimeout = 3000;
                stream.WriteTimeout = 3000;
                ConnectionStabilityTests.TcpSendFrame(stream,
                    "{\"id\":\"s1\",\"cmd\":\"get_enabled_tools\",\"args\":{}}");
                var reply = ConnectionStabilityTests.TcpReadFrame(stream);

                Assert.AreEqual(fake.ToolsSentinel, JsonHelper.ExtractString(reply, "data"),
                    $"Expected fake dispatcher sentinel, got: {reply}");
            }
            finally
            {
                ClientConnectionHandler.Dispatcher = original;
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (acceptLoop != null)
                    await AwaitAcceptLoopStoppedAsync(acceptLoop, TimeSpan.FromSeconds(2));
            }
        }

        [Test, Timeout(10000)]
        public async Task ProcessAsync_SlowPath_RoutesThroughInjectedDispatcher()
        {
            var fake = new FakeDispatcher();
            var original = ClientConnectionHandler.Dispatcher;
            ClientConnectionHandler.Dispatcher = fake;

            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "dispatcher-seam-test", lifetime, lifetime.Token);

                // Slow-path dispatch round-trips through MainThreadDispatcher.Enqueue, which
                // only drains on EditorApplication.update ticks. The blocking TcpReadFrame call
                // must therefore run on a background thread (mirrors
                // ConnectionStabilityTests.MultiClientPingLiveness) — calling it directly on
                // this test method's thread would block the very main-thread tick the
                // production dispatch depends on.
                string reply = null;
                var clientTask = Task.Run(() =>
                {
                    using var client = new TcpClient();
                    client.Connect("127.0.0.1", port);
                    var stream = client.GetStream();
                    stream.ReadTimeout = 5000;
                    stream.WriteTimeout = 5000;
                    // Any non-fast-path command takes the main-thread dispatch branch.
                    ConnectionStabilityTests.TcpSendFrame(stream,
                        "{\"id\":\"s2\",\"cmd\":\"__dispatcher_seam_probe__\",\"args\":{}}");
                    reply = ConnectionStabilityTests.TcpReadFrame(stream);
                });

                var completed = await Task.WhenAny(clientTask, Task.Delay(8000)).ConfigureAwait(false);
                Assert.AreSame(clientTask, completed, "Client round-trip did not complete within 8s");
                await clientTask.ConfigureAwait(false);

                Assert.AreEqual(fake.ProcessSentinel, reply,
                    $"Expected fake dispatcher sentinel, got: {reply}");
            }
            finally
            {
                ClientConnectionHandler.Dispatcher = original;
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (acceptLoop != null)
                    await AwaitAcceptLoopStoppedAsync(acceptLoop, TimeSpan.FromSeconds(2));
            }
        }
    }
}
