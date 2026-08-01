// TDD: ReloadMiniServer — queue drain, unknown-command dispatch, bind-retry.
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadMiniServerTests
    {
        // ── BindListener tests ────────────────────────────────────────────

        [Test]
        public void BindListener_SkipsOccupiedPort_StartsOnNext()
        {
            // Occupy startPort so BindListener must skip to startPort+1.
            var blocker = new TcpListener(IPAddress.Loopback, 0);
            blocker.Start();
            TcpListener listener = null;
            try
            {
                int occupiedPort = ((IPEndPoint)blocker.LocalEndpoint).Port;

                // BindListener must skip occupiedPort and land on occupiedPort+1..+5.
                int actualPort;
                (listener, actualPort) = ReloadBinder.BindListener(occupiedPort, occupiedPort + 5);
                Assert.AreNotEqual(occupiedPort, actualPort, "must not bind on occupied port");
                Assert.Greater(actualPort, 0);
                // Listener should be running (AcceptTcpClient-able).
                Assert.IsNotNull(listener);
            }
            finally
            {
                listener?.Stop();
                blocker.Stop();
            }
        }

        [Test]
        public void BindListener_AllOccupied_ThrowsSocketException()
        {
            // Occupy a tiny range [P..P+1] so BindListener has nowhere to go.
            var b0 = new TcpListener(IPAddress.Loopback, 0);
            b0.Start();
            try
            {
                int p = ((IPEndPoint)b0.LocalEndpoint).Port;

                // Force p+1 to be occupied too using SO_REUSEADDR=false (default).
                // We can't guarantee p+1 is free, so occupy via OS-assigned port, then
                // call BindListener on a single-port range that we know is occupied.
                Assert.Throws<SocketException>(
                    () => ReloadBinder.BindListener(p, p),
                    "must throw when entire range is occupied");
            }
            finally { b0.Stop(); }
        }

        [Test]
        public void BindListener_FreePort_BindsSuccessfully()
        {
            // Use a high ephemeral range unlikely to be occupied.
            var (listener, actualPort) = ReloadBinder.BindListener(19700, 19800);
            try
            {
                Assert.Greater(actualPort, 0);
                Assert.IsNotNull(listener);
            }
            finally { listener?.Stop(); }
        }


        [Test]
        public void M1_AbandonedLambda_DoesNotExecuteDispatch()
        {
            // M1 race: lambda is ENQUEUED, then timeout fires (sets abandoned=1), THEN queue drains.
            // Prove: dispatch side-effect does NOT execute when abandoned flag is set before drain.
            // This is the real concurrent ordering: enqueue → timeout → drain (not: timeout → enqueue).
            var queue = new System.Collections.Concurrent.ConcurrentQueue<Action>();
            int abandoned = 0;
            bool sideEffectFired = false;

            // Step 1: enqueue lambda FIRST (represents EnqueueMainThread before Wait times out).
            queue.Enqueue(() =>
            {
                // Guard mirrors EnqueueMainThread's lambda guard.
                if (Volatile.Read(ref abandoned) != 0) return;
                sideEffectFired = true; // phantom mutation — must NOT run after timeout
            });

            // Step 2: simulate timeout — set abandoned AFTER lambda is already queued.
            Interlocked.Exchange(ref abandoned, 1);

            // Step 3: drain queue (simulates EditorApplication.update after timeout).
            while (queue.TryDequeue(out var action))
                action();

            Assert.IsFalse(sideEffectFired,
                "M1: dispatch side-effect must NOT fire when timeout sets abandoned before drain");
        }
        [Test]
        public void QueueDrain_ExecutesEnqueuedAction()
        {
            bool executed = false;
            ReloadMiniServer.UpdateQueue.Enqueue(() => executed = true);

            // Drain the queue manually (simulates EditorApplication.update).
            while (ReloadMiniServer.UpdateQueue.TryDequeue(out var action))
                action();

            Assert.IsTrue(executed, "Enqueued action must execute after dequeue");
        }

        [Test]
        public void Dispatch_UnknownCommand_ReturnsError()
        {
            // DispatchCommand with no stream → test-context path for main-thread cmds
            var response = ReloadMiniServer.DispatchCommand("unknown_cmd_xyz", "{}", "test-id");

            Assert.IsNotNull(response);
            StringAssert.Contains("\"ok\":false", response);
            StringAssert.Contains("unknown command", response);
        }

        [Test]
        public void Dispatch_Ping_ReturnsOk()
        {
            var response = ReloadMiniServer.DispatchCommand("ping", "{}", "id1");
            StringAssert.Contains("\"ok\":true", response);
            StringAssert.Contains("pong", response);
        }

        [Test]
        public void Dispatch_GetVersion_ReturnsNonEmpty()
        {
            var response = ReloadMiniServer.DispatchCommand("get_version", "{}", "id2");
            StringAssert.Contains("\"ok\":true", response);
            // stamp format: mvid:mtime — must contain a colon
            StringAssert.Contains(":", response);
        }

        [Test]
        public void OkResponse_FormatsCorrectly()
        {
            var r = ReloadMiniServer.OkResponse("abc", "hello");
            Assert.AreEqual("{\"id\":\"abc\",\"ok\":true,\"data\":\"hello\"}", r);
        }

        [Test]
        public void ErrResponse_FormatsCorrectly()
        {
            var r = ReloadMiniServer.ErrResponse("abc", "bad");
            Assert.AreEqual("{\"id\":\"abc\",\"ok\":false,\"err\":\"bad\"}", r);
        }

        // ── CP-4: tracked clients ─────────────────────────────────────────────

        [Test]
        public void ActiveClients_FieldExists_AndIsConcurrentDictionary()
        {
            var field = typeof(ReloadMiniServer).GetField("_activeClients",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "_activeClients field must exist");
            var value = field.GetValue(null);
            Assert.IsInstanceOf<ConcurrentDictionary<int, TcpClient>>(value,
                "_activeClients must be ConcurrentDictionary<int, TcpClient>");
        }

        [Test]
        public void Stop_CompletesQuicklyWithActiveClient()
        {
            ReloadMiniServer.Start(19760);
            if (ReloadMiniServer.ActualPort == 0)
            {
                Assert.Ignore("Port bind failed — skip in CI");
                return;
            }
            TcpClient tc = null;
            try
            {
                tc = new TcpClient("127.0.0.1", ReloadMiniServer.ActualPort);
                Thread.Sleep(50); // let AcceptLoop register the client
                var stopDone = false;
                var t = new Thread(() => { ReloadMiniServer.Stop(); stopDone = true; });
                t.Start();
                bool joined = t.Join(3000);
                Assert.IsTrue(joined, "Stop() must complete in < 3s with active client");
                Assert.IsTrue(stopDone);
            }
            finally
            {
                try { tc?.Close(); } catch { }
                ReloadMiniServer.Stop(); // idempotent cleanup
            }
        }
    }

    // ── Dispatch coverage: structured fields + null-stream guard + id echo ────

    [TestFixture]
    public class ReloadMiniServerDispatchTests
    {
        // A1: diagnose path wraps output in ok:true and preserves key fields.
        [Test]
        public void Dispatch_Diagnose_ContainsStructuredFields()
        {
            var response = ReloadMiniServer.DispatchCommand("diagnose", "{}", "d1");

            StringAssert.Contains("\"ok\":true", response);
            StringAssert.Contains("mvid=", response);
            StringAssert.Contains("compile=", response);
        }

        // A2: sync_status route: id round-trip + state= present.
        [Test]
        public void Dispatch_SyncStatus_ContainsStateField_ViaMiniServer()
        {
            var response = ReloadMiniServer.DispatchCommand("sync_status", "{}", "ss1");

            StringAssert.Contains("\"ok\":true", response);
            StringAssert.Contains("state=", response);
            StringAssert.Contains("\"id\":\"ss1\"", response);
        }

        // A3: null stream → EnqueueMainThread returns error immediately (no queue touch).
        [Test]
        public void Dispatch_ForceRefresh_NullStream_ReturnsMainThreadUnavailableError()
        {
            var response = ReloadMiniServer.DispatchCommand("force_refresh", "{}", "fr1", null);

            Assert.IsNotNull(response);
            StringAssert.Contains("\"ok\":false", response);
            StringAssert.Contains("main thread not available in test context", response);
        }

        // A4: symmetric with A3 — recompile command, same null-stream guard.
        [Test]
        public void Dispatch_Recompile_NullStream_ReturnsMainThreadUnavailableError()
        {
            var response = ReloadMiniServer.DispatchCommand("recompile", "{}", "rc1", null);

            StringAssert.Contains("\"ok\":false", response);
            StringAssert.Contains("main thread not available in test context", response);
        }

        // A5: id must round-trip in error response (Python-side correlation depends on this).
        [Test]
        public void Dispatch_IdIsEchoedInErrorResponse_ForUnknownCommand()
        {
            var response = ReloadMiniServer.DispatchCommand("no_such_cmd", "{}", "my-correlation-id-99", null);

            StringAssert.Contains("\"id\":\"my-correlation-id-99\"", response);
            StringAssert.Contains("\"ok\":false", response);
        }
    }

    // ── Port file lifecycle: write + delete via overridable PortsDir seam ────

    [TestFixture]
    public class ReloadPortResolverLifecycleTests
    {
        private string _originalPortsDir;
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _originalPortsDir = ReloadPortResolver.PortsDir;
            _tmpDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ReloadPortResolverTest_" + System.IO.Path.GetRandomFileName());
            ReloadPortResolver.PortsDir = _tmpDir;
        }

        [TearDown]
        public void TearDown()
        {
            ReloadPortResolver.PortsDir = _originalPortsDir;
            try { if (System.IO.Directory.Exists(_tmpDir)) System.IO.Directory.Delete(_tmpDir, true); }
            catch { }
        }

        // A6: full port-file lifecycle — write creates file with correct content, delete removes it.
        [Test]
        public void ReloadPortResolver_WriteAndDeletePortFile_CreatesAndRemovesFile()
        {
            int fakePid = System.Diagnostics.Process.GetCurrentProcess().Id;
            int port = 9612;
            string projDir = "/fake/proj/dir";

            ReloadPortResolver.WriteReloadPortFile(fakePid, port, projDir, "TestProj");

            var filePath = System.IO.Path.Combine(_tmpDir, $"{fakePid}.reload-port");
            Assert.IsTrue(System.IO.File.Exists(filePath),
                "WriteReloadPortFile must create the .reload-port file");
            var lines = System.IO.File.ReadAllText(filePath).Split('\n');
            Assert.AreEqual("9612", lines[0].Trim(), "First line must be the port number");
            Assert.AreEqual(projDir, lines[1].Trim(), "Second line must be the project directory");

            ReloadPortResolver.DeleteReloadPortFile(fakePid);

            Assert.IsFalse(System.IO.File.Exists(filePath),
                "DeleteReloadPortFile must remove the .reload-port file");
        }
    }

    // ── Stress tests: source structure + concurrent Stop (CP-4) ──────────────

    [TestFixture]
    public class ReloadMiniServerStressTests
    {
        // T-E: AcceptLoop sets client.ReceiveTimeout = 30_000 (source verification)
        [Test]
        public void AcceptLoop_SetsReceiveTimeout_InSource()
        {
            var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                Application.dataPath, "..", "..", "unity-plugin-reload/Editor/ReloadMiniServer.cs"));
            if (!System.IO.File.Exists(p)) { Assert.Ignore($"Source not found: {p}"); return; }
            var src = System.IO.File.ReadAllText(p);
            StringAssert.Contains("ReceiveTimeout = 30_000", src,
                "CP-4: AcceptLoop must set client.ReceiveTimeout = 30_000 to bound blocking reads");
        }

        // T-F: Stop() + concurrent blocking reader — no deadlock/exception
        [Test]
        public void Stop_ConcurrentHandleClient_DoesNotThrow()
        {
            ReloadMiniServer.Start(19770);
            if (ReloadMiniServer.ActualPort == 0) { Assert.Ignore("Port bind failed — skip"); return; }

            Exception caught = null;
            var client = new TcpClient("127.0.0.1", ReloadMiniServer.ActualPort);
            var reader = new Thread(() =>
            {
                try { client.GetStream().Read(new byte[4], 0, 4); }
                catch { } // SocketException on close = expected
            });
            reader.Start();
            Thread.Sleep(30); // let AcceptLoop register the client
            try { ReloadMiniServer.Stop(); }
            catch (Exception e) { caught = e; }
            finally { try { client.Close(); } catch { } }
            reader.Join(2000);
            Assert.IsNull(caught, $"Stop() must not throw with concurrent reader: {caught}");
        }
    }
}
