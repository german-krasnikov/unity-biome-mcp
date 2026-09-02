// TDD: Connection stability — ConfigureAwait(false) regression witness (internal-seam).
//
// THE WITNESS DESIGN (why this test is valid):
// SendAsync awaits WriteAsync then FlushAsync on the provided stream.
// AsyncYieldStream.WriteAsync/FlushAsync use `await Task.Yield()` — Task.Yield ALWAYS posts
// the continuation to the ambient SynchronizationContext (or ThreadPool if context is null).
//
// WITH ConfigureAwait(false): the continuation is posted to ThreadPool (context ignored).
//   → the dedicated witness thread sees the task complete → GREEN.
//
// WITHOUT ConfigureAwait(false): the continuation is posted to NeverPumpingSyncContext.
//   The context never dispatches its queue, so the continuation never runs and
//   the asynchronously awaited witness reaches its bounded timeout → RED.
//
// This is the canonical ConfigureAwait(false) correctness test. The TCP-level test below
// validates multi-client liveness but is NOT a regression witness for the focus-loss bug.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConnectionStabilityTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // T-C#1: THE internal-seam regression witness.
        // Directly calls ClientConnectionHandler.SendAsync (internal) under a NeverPumpingSyncContext.
        // Phase 2 M1: SendAsync/ReadExactAsync moved from MCPServer to ClientConnectionHandler.
        // With ConfigureAwait(false): stream continuations run on ThreadPool → task completes.
        // Without it: continuations posted to stalled context → async witness times out.
        //
        // RED/GREEN proof (run in gated NUnit phase):
        //   Strip .ConfigureAwait(false) from SendAsync's two awaits → test must go RED.
        //   Restore → test must go GREEN.
        [Test, Timeout(5000)]
        public async Task SendAsync_CompletesUnderStalledSyncContext()
        {
            var stream = new AsyncYieldStream();
            var witness = RunUnderStalledContext(() =>
                ClientConnectionHandler.SendAsync(
                    stream, "{\"ok\":true,\"data\":\"pong\"}", CancellationToken.None));

            var operation = await AwaitWitnessAsync(witness,
                "SendAsync deadlocked under stalled SynchronizationContext. " +
                "ConfigureAwait(false) is missing from WriteAsync or FlushAsync in SendAsync.");
            await operation;
        }

        // T-C#1b: Same witness for ReadExactAsync.
        [Test, Timeout(5000)]
        public async Task ReadExactAsync_CompletesUnderStalledSyncContext()
        {
            var stream = new AsyncYieldStream();
            var buffer = new byte[4];
            var witness = RunUnderStalledContext(() =>
                ClientConnectionHandler.ReadExactAsync(stream, buffer, CancellationToken.None));

            var operation = (Task<bool>)await AwaitWitnessAsync(witness,
                "ReadExactAsync deadlocked under stalled SynchronizationContext. " +
                "ConfigureAwait(false) is missing from ReadAsync in ReadExactAsync.");
            Assert.IsTrue(await operation, "ReadExactAsync returned false (stream returned 0 bytes)");
        }

        // T-C#2: Multi-client TCP liveness.
        // DOWNGRADED CLAIM: validates that the production accept loop can serve 4 TCP
        // clients concurrently. Does NOT prove ConfigureAwait(false) prevents the focus-loss
        // bug — that proof is T-C#1/T-C#1b (internal-seam) + test_focus_loss_zero_reconnects
        // (Python live test). The listener is fixture-owned so the test cannot depend on the
        // live MCP singleton or on any preceding fixture.
        [Test, Timeout(15000)]
        public async Task MultiClientPingLiveness()
        {
            const int clientCount = 4;
            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            Task allClients = null;
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "liveness-test", lifetime, lifetime.Token);

                var results = new string[clientCount];
                var errors = new string[clientCount];
                var allConnected = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var connectedCount = 0;
                var clients = new Task[clientCount];
                for (int i = 0; i < clientCount; i++)
                {
                    int idx = i;
                    clients[idx] = Task.Run(async () =>
                    {
                        try
                        {
                            using var client = new TcpClient();
                            await client.ConnectAsync("127.0.0.1", port)
                                .ConfigureAwait(false);
                            var stream = client.GetStream();
                            stream.ReadTimeout = 3000;
                            stream.WriteTimeout = 3000;
                            if (Interlocked.Increment(ref connectedCount) == clientCount)
                                allConnected.TrySetResult(true);
                            var released = await Task.WhenAny(
                                    allConnected.Task, Task.Delay(3000))
                                .ConfigureAwait(false);
                            if (released != allConnected.Task)
                            {
                                errors[idx] = "Start-gate timeout";
                                return;
                            }

                            var ping = $"{{\"id\":\"m{idx}\",\"cmd\":\"ping\",\"args\":{{}}}}";
                            TcpSendFrame(stream, ping);
                            results[idx] = TcpReadFrame(stream);
                        }
                        catch (Exception exception)
                        {
                            errors[idx] = exception.Message;
                        }
                    });
                }

                allClients = Task.WhenAll(clients);
                var completed = await Task.WhenAny(
                        allClients, Task.Delay(5000))
                    .ConfigureAwait(false);
                Assert.AreSame(allClients, completed, "Clients did not complete within 5s");
                await allClients.ConfigureAwait(false);

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsNull(errors[i], $"Client {i} threw: {errors[i]}");
                    Assert.IsNotNull(results[i], $"Client {i} received no response");
                    StringAssert.Contains("pong", results[i], $"Client {i}: {results[i]}");
                }
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (allClients != null && !allClients.IsCompleted)
                {
                    var clientsStopped = await Task.WhenAny(
                            allClients, Task.Delay(3000))
                        .ConfigureAwait(false);
                    if (clientsStopped != allClients)
                        throw new TimeoutException("Fixture-owned MCP clients did not stop.");
                    await allClients.ConfigureAwait(false);
                }
                if (acceptLoop != null)
                {
                    var stopped = await Task.WhenAny(
                            acceptLoop, Task.Delay(2000))
                        .ConfigureAwait(false);
                    if (stopped != acceptLoop)
                        throw new TimeoutException("Fixture-owned MCP accept loop did not stop.");
                    await acceptLoop.ConfigureAwait(false);
                }
            }
        }

        [Test]
        public void ClientSlot_Add_DoesNotEvictBeforeHandlerClear()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var first = new TcpClient();
            using var second = new TcpClient();
            CancellationTokenSource firstClient = null;
            CancellationTokenSource secondClient = null;
            try
            {
                firstClient = slot.Add(first, lifetime.Token).clientCts;
                secondClient = slot.Add(second, lifetime.Token).clientCts;
                var occupied = new List<TcpClient>();
                slot.ForEach(occupied.Add);

                CollectionAssert.AreEquivalent(new[] { first, second }, occupied,
                    "Only the connection handler may clear an occupied slot; " +
                    "Add must not race the handler with a socket health probe.");
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                firstClient?.Dispose();
                secondClient?.Dispose();
            }
        }

        [Test]
        public void ClientSlot_LivenessIsOwnedByHandlerRegistration()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            using var client = new TcpClient();
            var handle = slot.Add(client, lifetime.Token);
            try
            {
                Assert.IsTrue(slot.AnyConnected,
                    "An occupied handler slot is authoritative even when a socket snapshot is stale.");
                Assert.AreEqual(0, slot.CountPhantoms());
                Assert.AreEqual(0, slot.KillPhantoms(),
                    "Liveness inspection must not close a client owned by an active handler.");

                var occupied = new List<TcpClient>();
                slot.ForEach(occupied.Add);
                CollectionAssert.AreEqual(new[] { client }, occupied);

                slot.Clear(handle.index, handle.generation);
                Assert.IsFalse(slot.AnyConnected);
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                handle.clientCts.Dispose();
            }
        }

        [Test, Timeout(5000)]
        public async Task ClientSlot_LivenessInspectionDoesNotCloseLiveReadableSocket()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var lifetime = new CancellationTokenSource();
            using var peer = new TcpClient();
            TcpClient accepted = null;
            CancellationTokenSource clientCts = null;
            try
            {
                var accept = listener.AcceptTcpClientAsync();
                await peer.ConnectAsync(
                    IPAddress.Loopback,
                    ((IPEndPoint)listener.LocalEndpoint).Port);
                accepted = await accept;
                var inspectedSlot = new ClientSlot();
                clientCts = inspectedSlot.Add(accepted, lifetime.Token).clientCts;

                var payload = new byte[] { 0x5a };
                await peer.GetStream().WriteAsync(payload, 0, payload.Length);
                Assert.IsTrue(inspectedSlot.AnyConnected);
                Assert.AreEqual(0, inspectedSlot.CountPhantoms());
                Assert.AreEqual(0, inspectedSlot.KillPhantoms());

                var received = new byte[1];
                var read = accepted.GetStream().ReadAsync(received, 0, received.Length);
                var completed = await Task.WhenAny(read, Task.Delay(1000));
                Assert.AreSame(read, completed, "Readable socket was reset during inspection.");
                Assert.AreEqual(1, await read);
                Assert.AreEqual(0x5a, received[0],
                    "Liveness inspection must neither consume nor reset a readable socket.");

                inspectedSlot.DisconnectAll();
            }
            finally
            {
                lifetime.Cancel();
                accepted?.Dispose();
                clientCts?.Dispose();
                listener.Stop();
            }
        }

        [Test, Timeout(10000)]
        public async Task ClientSlot_RemoteEofIsReleasedByHandlerFinally()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            Task acceptLoop = null;
            var peer = new TcpClient();
            listener.Start();
            try
            {
                acceptLoop = ClientConnectionHandler.RunAcceptLoop(
                    listener, slot, "eof-test", lifetime, lifetime.Token);
                await peer.ConnectAsync(
                    IPAddress.Loopback,
                    ((IPEndPoint)listener.LocalEndpoint).Port);
                var stream = peer.GetStream();
                TcpSendFrame(stream, "{\"id\":\"eof\",\"cmd\":\"ping\",\"args\":{}}");
                StringAssert.Contains("pong", TcpReadFrame(stream));
                Assert.IsTrue(slot.AnyConnected);

                peer.Dispose();
                await AwaitConditionAsync(
                    () => !slot.AnyConnected,
                    TimeSpan.FromSeconds(3),
                    "Remote EOF was not released by HandleClientAsync.finally.");
                Assert.AreEqual(0, slot.CountPhantoms(),
                    "Normal EOF must clear the slot instead of leaving manual cleanup work.");
                Assert.AreEqual(0, slot.KillPhantoms());
            }
            finally
            {
                peer.Dispose();
                lifetime.Cancel();
                slot.DisconnectAll();
                listener.Stop();
                if (acceptLoop != null)
                {
                    var stopped = await Task.WhenAny(acceptLoop, Task.Delay(2000));
                    Assert.AreSame(acceptLoop, stopped, "Fixture-owned accept loop did not stop.");
                    await acceptLoop;
                }
            }
        }

        [Test]
        public void ClientSlot_WhenFullRejectsNewClientAndPreservesExistingClients()
        {
            var slot = new ClientSlot();
            using var lifetime = new CancellationTokenSource();
            var clients = Enumerable.Range(0, ClientSlot.MaxClients + 1)
                .Select(_ => new TcpClient())
                .ToArray();
            var handles = new (int index, long generation, CancellationTokenSource clientCts)[clients.Length];
            try
            {
                for (var i = 0; i < ClientSlot.MaxClients; i++)
                    handles[i] = slot.Add(clients[i], lifetime.Token);

                Assert.IsFalse(slot.TryAdd(clients[ClientSlot.MaxClients], lifetime.Token,
                    out var rejectedIndex, out var rejectedGeneration, out var rejectedCts),
                    "A full slot must reject admission instead of evicting an established client.");
                Assert.AreEqual(-1, rejectedIndex);
                Assert.AreEqual(0, rejectedGeneration);
                Assert.IsNull(rejectedCts);

                var occupied = new List<TcpClient>();
                slot.ForEach(occupied.Add);
                Assert.AreEqual(ClientSlot.MaxClients, occupied.Count);
                CollectionAssert.DoesNotContain(occupied, clients[ClientSlot.MaxClients]);
                for (var i = 0; i < ClientSlot.MaxClients; i++)
                    CollectionAssert.Contains(occupied, clients[i]);

                slot.Clear(handles[0].index, handles[0].generation);
                occupied.Clear();
                slot.ForEach(occupied.Add);
                Assert.AreEqual(ClientSlot.MaxClients - 1, occupied.Count);
                CollectionAssert.DoesNotContain(occupied, clients[0]);
                CollectionAssert.DoesNotContain(occupied, clients[ClientSlot.MaxClients]);

                for (var i = 1; i < ClientSlot.MaxClients; i++)
                    slot.Clear(handles[i].index, handles[i].generation);
                Assert.IsFalse(slot.AnyConnected);
            }
            finally
            {
                lifetime.Cancel();
                slot.DisconnectAll();
                foreach (var client in clients) client.Dispose();
                foreach (var handle in handles) handle.clientCts?.Dispose();
            }
        }

        [Test]
        public void BoundPortResolution_UsesActualEndpoint()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var actualPort = ((IPEndPoint)listener.LocalEndpoint).Port;

                Assert.IsTrue(MCPServer.TryGetBoundPort(listener, out var publishedPort));
                Assert.AreEqual(actualPort, publishedPort);
                Assert.Greater(publishedPort, 0);
            }
            finally
            {
                listener.Stop();
            }

            Assert.IsFalse(MCPServer.TryGetBoundPort(listener, out _));
            Assert.IsFalse(MCPServer.TryGetBoundPort(null, out _));
        }

        [Test, Timeout(10000)]
        public async Task BoundPortResolution_IsRaceSafeDuringStopStart()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var readers = new Task[4];
                for (var readerIndex = 0; readerIndex < readers.Length; readerIndex++)
                {
                    readers[readerIndex] = Task.Run(() =>
                    {
                        for (var iteration = 0; iteration < 5000; iteration++)
                        {
                            if (MCPServer.TryGetBoundPort(listener, out var port))
                                Assert.Greater(port, 0);
                        }
                    });
                }

                var churn = Task.Run(() =>
                {
                    for (var iteration = 0; iteration < 250; iteration++)
                    {
                        listener.Stop();
                        listener.Start();
                    }
                });

                await Task.WhenAll(readers.Concat(new[] { churn }));
            }
            finally
            {
                listener.Stop();
            }
        }

        private static Task<Task> RunUnderStalledContext(Func<Task> operation)
        {
            var completion = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
            new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new NeverPumpingSyncContext());
                try
                {
                    var task = operation();
                    task.ContinueWith(
                        completed => completion.TrySetResult(completed),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                }
            }) { IsBackground = true, Name = "UnityMCP.StalledContextWitness" }.Start();
            return completion.Task;
        }

        private static async Task<Task> AwaitWitnessAsync(Task<Task> witness, string timeoutMessage)
        {
            var completed = await Task.WhenAny(witness, Task.Delay(3000));
            Assert.AreSame(witness, completed, timeoutMessage);
            try
            {
                return await witness;
            }
            catch (TimeoutException)
            {
                Assert.Fail(timeoutMessage);
                return null;
            }
        }

        private static async Task AwaitConditionAsync(
            Func<bool> condition, TimeSpan timeout, string timeoutMessage)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            Assert.IsTrue(condition(), timeoutMessage);
        }

        // ── TCP protocol helpers (used by T-C#2) ─────────────────────────────

        // internal (not private) so HttpGarbageProbeTests can reuse the same framing
        // helpers for its post-probe liveness check — DRY, matching how RunAcceptLoop
        // is already reused across test files.
        internal static void TcpSendFrame(NetworkStream stream, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var header = new byte[4];
            header[0] = (byte)(payload.Length >> 24);
            header[1] = (byte)(payload.Length >> 16);
            header[2] = (byte)(payload.Length >> 8);
            header[3] = (byte)(payload.Length);
            stream.Write(header, 0, 4);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        internal static string TcpReadFrame(NetworkStream stream)
        {
            var header = new byte[4];
            int read = 0;
            while (read < 4) { int n = stream.Read(header, read, 4 - read); if (n == 0) return null; read += n; }
            int length = (header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3];
            if (length <= 0 || length > 10_000_000) return null;
            var buf = new byte[length];
            read = 0;
            while (read < length) { int n = stream.Read(buf, read, length - read); if (n == 0) return null; read += n; }
            return Encoding.UTF8.GetString(buf);
        }
    }

    // ── AsyncYieldStream ─────────────────────────────────────────────────────
    // Mock Stream: all async methods yield to ThreadPool before completing.
    //
    // HOW THE DISCRIMINATOR WORKS:
    // WriteAsync/FlushAsync/ReadAsync complete on a ThreadPool thread (via Task.Run).
    // SendAsync has TWO awaits: WriteAsync then FlushAsync.
    //
    // WITH ConfigureAwait(false) on those awaits:
    //   After WriteAsync completes on ThreadPool, the continuation of `await WriteAsync`
    //   is scheduled on ThreadPool (ambient SyncContext ignored) → FlushAsync is called
    //   from ThreadPool → it completes → continuation runs on ThreadPool → task completes.
    //   the dedicated witness thread sees the task complete → GREEN.
    //
    // WITHOUT ConfigureAwait(false):
    //   After WriteAsync completes on ThreadPool, the continuation of `await WriteAsync`
    //   is posted to NeverPumpingSyncContext (the ambient context) → never executes.
    //   the dedicated witness thread (which never pumps) times out → RED.
    //
    // Task.Yield() is NOT used here because it posts to the ambient SyncContext at
    // the call site — making WriteAsync itself deadlock before returning, regardless
    // of ConfigureAwait on the outer await. Task.Run() escapes ambient context.

    public sealed class AsyncYieldStream : Stream
    {
        private static readonly byte[] _dummyData = new byte[8];

        public override bool CanRead  => true;
        public override bool CanWrite => true;
        public override bool CanSeek  => false;
        public override long Length   => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        // Completes on ThreadPool. The continuation of `await WriteAsync` is the
        // discriminator: with ConfigureAwait(false) → ThreadPool; without → ambient SyncContext.
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => Task.Run(() => { /* no-op — just forces async hop to ThreadPool */ }, ct);

        public override Task FlushAsync(CancellationToken ct)
            => Task.Run(() => { }, ct);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
            => Task.Run(() =>
            {
                int n = Math.Min(count, _dummyData.Length);
                Array.Copy(_dummyData, 0, buffer, offset, n);
                return n;
            }, ct);

        public override void Write(byte[] buffer, int offset, int count) { }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

    // ── NeverPumpingSyncContext ───────────────────────────────────────────────
    // Captures all Post() calls but NEVER executes them.
    // Simulates a backgrounded Unity Editor where EditorApplication.update is throttled.

    public sealed class NeverPumpingSyncContext : SynchronizationContext
    {
        private readonly List<(SendOrPostCallback callback, object state)> _captured
            = new List<(SendOrPostCallback, object)>();

        public int CapturedCount => _captured.Count;

        public override void Post(SendOrPostCallback d, object state)
        {
            lock (_captured) { _captured.Add((d, state)); }
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            d(state);  // Send is synchronous — must execute or callers deadlock.
        }
    }
}
