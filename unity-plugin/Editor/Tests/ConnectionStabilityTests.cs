// TDD: Connection stability — ConfigureAwait(false) regression witness (internal-seam).
//
// THE WITNESS DESIGN (why this test is valid):
// SendAsync awaits WriteAsync then FlushAsync on the provided stream.
// AsyncYieldStream.WriteAsync/FlushAsync use `await Task.Yield()` — Task.Yield ALWAYS posts
// the continuation to the ambient SynchronizationContext (or ThreadPool if context is null).
//
// WITH ConfigureAwait(false): the continuation is posted to ThreadPool (context ignored).
//   → t.Wait(2000) on the BLOCKING test thread sees the task complete → true → GREEN.
//
// WITHOUT ConfigureAwait(false): the continuation is posted to NeverPumpingSyncContext.
//   The test thread is BLOCKING in t.Wait() → it never pumps the stalled context
//   → continuation never runs → WriteAsync never completes → SendAsync deadlocks
//   → t.Wait(2000) times out → false → RED.
//
// This is the canonical ConfigureAwait(false) correctness test. The TCP-level test below
// validates multi-client liveness but is NOT a regression witness for the focus-loss bug.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConnectionStabilityTests
    {
        // T-C#1: THE internal-seam regression witness.
        // Directly calls ClientConnectionHandler.SendAsync (internal) under a NeverPumpingSyncContext.
        // Phase 2 M1: SendAsync/ReadExactAsync moved from MCPServer to ClientConnectionHandler.
        // With ConfigureAwait(false): stream continuations run on ThreadPool → task completes.
        // Without it: continuations posted to stalled context → deadlock → Wait times out.
        //
        // RED/GREEN proof (run in gated NUnit phase):
        //   Strip .ConfigureAwait(false) from SendAsync's two awaits → test must go RED.
        //   Restore → test must go GREEN.
        [Test, Timeout(5000)]
        public void SendAsync_CompletesUnderStalledSyncContext()
        {
            var prev = SynchronizationContext.Current;
            var stalled = new NeverPumpingSyncContext();
            SynchronizationContext.SetSynchronizationContext(stalled);
            try
            {
                var stream = new AsyncYieldStream();
                // BLOCKING wait — the test thread holds the stalled context but never pumps it.
                Task t = ClientConnectionHandler.SendAsync(stream, "{\"ok\":true,\"data\":\"pong\"}", CancellationToken.None);
                bool done = t.Wait(2000);
                Assert.IsTrue(done,
                    "SendAsync deadlocked under stalled SynchronizationContext. " +
                    "ConfigureAwait(false) is missing from WriteAsync or FlushAsync in SendAsync.");
                Assert.IsNull(t.Exception, $"SendAsync threw: {t.Exception}");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        // T-C#1b: Same witness for ReadExactAsync.
        [Test, Timeout(5000)]
        public void ReadExactAsync_CompletesUnderStalledSyncContext()
        {
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new NeverPumpingSyncContext());
            try
            {
                var stream = new AsyncYieldStream();
                var buffer = new byte[4];
                Task<bool> t = ClientConnectionHandler.ReadExactAsync(stream, buffer, CancellationToken.None);
                bool done = t.Wait(2000);
                Assert.IsTrue(done,
                    "ReadExactAsync deadlocked under stalled SynchronizationContext. " +
                    "ConfigureAwait(false) is missing from ReadAsync in ReadExactAsync.");
                Assert.IsTrue(t.Result, "ReadExactAsync returned false (stream returned 0 bytes)");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        }

        // T-C#2: Multi-client TCP liveness.
        // DOWNGRADED CLAIM: validates that 4 TCP clients can ping/pong concurrently while
        // MCPServer is running. Does NOT prove ConfigureAwait(false) prevents the focus-loss
        // bug — that proof is T-C#1/T-C#1b (internal-seam) + test_focus_loss_zero_reconnects
        // (Python live test). Included for multi-client liveness regression coverage.
        [Test, Timeout(7000)]
        public void MultiClientPingLiveness()
        {
            if (!MCPServer.IsRunning)
                Assert.Ignore("MCPServer not running in this test environment");

            int port = MCPServer.ServerPort;
            const int clientCount = 4;
            var results = new string[clientCount];
            var errors = new string[clientCount];
            var barrier = new CountdownEvent(clientCount);

            var threads = new Thread[clientCount];
            for (int i = 0; i < clientCount; i++)
            {
                int idx = i;
                threads[idx] = new Thread(() =>
                {
                    try
                    {
                        using var c = new TcpClient("127.0.0.1", port);
                        var s = c.GetStream();
                        s.ReadTimeout = 3000;
                        s.WriteTimeout = 3000;
                        barrier.Signal();
                        barrier.Wait();

                        var ping = $"{{\"id\":\"m{idx}\",\"cmd\":\"ping\",\"args\":{{}}}}";
                        TcpSendFrame(s, ping);
                        results[idx] = TcpReadFrame(s);
                    }
                    catch (Exception e) { errors[idx] = e.Message; }
                });
                threads[idx].IsBackground = true;
                threads[idx].Start();
            }
            foreach (var t in threads) t.Join(4000);

            for (int i = 0; i < clientCount; i++)
            {
                Assert.IsNull(errors[i], $"Client {i} threw: {errors[i]}");
                Assert.IsNotNull(results[i], $"Client {i} received no response");
                StringAssert.Contains("pong", results[i], $"Client {i}: {results[i]}");
            }
        }

        // ── TCP protocol helpers (used by T-C#2) ─────────────────────────────

        private static void TcpSendFrame(NetworkStream stream, string json)
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

        private static string TcpReadFrame(NetworkStream stream)
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
    //   t.Wait(2000) on the blocking test thread returns true → GREEN.
    //
    // WITHOUT ConfigureAwait(false):
    //   After WriteAsync completes on ThreadPool, the continuation of `await WriteAsync`
    //   is posted to NeverPumpingSyncContext (the ambient context) → never executes.
    //   t.Wait(2000) on the blocking test thread (which never pumps) times out → RED.
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
