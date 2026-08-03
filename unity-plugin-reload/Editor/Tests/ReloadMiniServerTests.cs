// TDD: ReloadMiniServer — queue drain, unknown-command dispatch, bind-retry.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadMiniServerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
        public void MainThreadDispatchGate_TimeoutBeforeDrainPreventsDispatch()
        {
            var gate = new ReloadMainThreadDispatchGate();
            var queue = new ConcurrentQueue<Action>();
            var dispatchCount = 0;
            queue.Enqueue(() =>
            {
                if (gate.TryStart()) dispatchCount++;
            });

            Assert.IsTrue(gate.TryAbandon());
            while (queue.TryDequeue(out var action))
                action();

            Assert.AreEqual(0, dispatchCount,
                "A command abandoned before dequeue must never mutate Editor state.");
            Assert.IsFalse(gate.HasStarted);
        }

        [Test]
        public void MainThreadDispatchGate_DispatchStartPreventsFalseAbandonment()
        {
            var gate = new ReloadMainThreadDispatchGate();

            Assert.IsTrue(gate.TryStart());
            Assert.IsFalse(gate.TryAbandon(),
                "A timeout must not claim abandonment after dispatch has started.");
            Assert.IsTrue(gate.HasStarted);
        }

        [Test]
        public void MainThreadDispatch_AcceptsBeforeInvokingMutation()
        {
            var gate = new ReloadMainThreadDispatchGate();
            var order = new List<string>();

            Assert.IsTrue(gate.TryStart());
            gate.ExecuteStarted(
                () => order.Add("accepted"),
                () => order.Add("dispatch"));

            CollectionAssert.AreEqual(new[] { "accepted", "dispatch" }, order);
            Assert.IsFalse(gate.TryAbandon());
        }

        [Test]
        public void LifecycleGeneration_InvalidatesWorkCapturedBeforeAdvance()
        {
            var generation = new ReloadMiniServerGeneration();
            var queue = new ConcurrentQueue<Action>();
            var dispatchCount = 0;
            var first = generation.Advance();
            queue.Enqueue(generation.Bind(first, () => dispatchCount++));

            Assert.IsTrue(generation.IsCurrent(first));
            Assert.AreEqual(first, generation.Current);

            var second = generation.Advance();
            while (queue.TryDequeue(out var staleAction)) staleAction();
            queue.Enqueue(generation.Bind(second, () => dispatchCount++));
            while (queue.TryDequeue(out var currentAction)) currentAction();

            Assert.IsFalse(generation.IsCurrent(first));
            Assert.IsTrue(generation.IsCurrent(second));
            Assert.AreEqual(1, dispatchCount,
                "Only work captured for the current lifecycle may execute.");
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
        [UnityMCP.Editor.Testing.BiomeWorkerOnly(
            "Stops the live reload listener and must run only in a disposable worker.")]
        public async Task Stop_CompletesQuicklyWithActiveClient()
        {
            var server = ReloadMiniServerWorkerHarness.Create();
            server.Restart(19760);
            RegisterCleanup(server.Stop);
            if (ReloadMiniServer.ActualPort == 0)
            {
                Assert.Ignore("Port bind failed — skip in CI");
                return;
            }
            TcpClient tc = null;
            try
            {
                tc = new TcpClient();
                await tc.ConnectAsync("127.0.0.1", ReloadMiniServer.ActualPort);
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    await ReloadMiniServerTestAwait.WaitUntilAsync(
                        () => ReloadMiniServerTestAwait.ActiveClientCount > 0,
                        timeout.Token,
                        "AcceptLoop did not register the active client within 3s");

                    var stopTask = Task.Run((Action)server.Stop);
                    await ReloadMiniServerTestAwait.WaitForTaskAsync(
                        stopTask, timeout.Token,
                        "Stop() must complete in < 3s with an active client");
                }
            }
            finally
            {
                try { tc?.Close(); } catch { }
            }
            Assert.AreEqual(0, ReloadMiniServer.ActualPort,
                "A stopped listener must not advertise a stale port.");
        }
    }

    // ── Dispatch coverage: structured fields + null-stream guard + id echo ────

    [TestFixture]
    public class ReloadMiniServerDispatchTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
    public class ReloadPortResolverLifecycleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
            RegisterCleanup(() =>
            {
                if (System.IO.Directory.Exists(_tmpDir))
                    System.IO.Directory.Delete(_tmpDir, true);
            });
            RegisterCleanup(() => ReloadPortResolver.PortsDir = _originalPortsDir);
            ReloadPortResolver.PortsDir = _tmpDir;
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
    public class ReloadMiniServerStressTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // T-E: AcceptLoop sets client.ReceiveTimeout = 30_000 (source verification)
        [Test]
        public void AcceptLoop_SetsReceiveTimeout_InSource()
        {
            var src = ReadRequiredPackageSource(
                typeof(ReloadMiniServer), "Editor/ReloadMiniServer.cs");
            StringAssert.Contains("ReceiveTimeout = 30_000", src,
                "CP-4: AcceptLoop must set client.ReceiveTimeout = 30_000 to bound blocking reads");
        }

        // T-F: Stop() + concurrent blocking reader — no deadlock/exception
        [Test]
        [UnityMCP.Editor.Testing.BiomeWorkerOnly(
            "Stops the live reload listener and must run only in a disposable worker.")]
        public async Task Stop_ConcurrentHandleClient_DoesNotThrow()
        {
            var server = ReloadMiniServerWorkerHarness.Create();
            server.Restart(19770);
            RegisterCleanup(server.Stop);
            if (ReloadMiniServer.ActualPort == 0) { Assert.Ignore("Port bind failed — skip"); return; }

            Exception caught = null;
            var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", ReloadMiniServer.ActualPort);
            var reader = Task.Run(() =>
            {
                try { client.GetStream().Read(new byte[4], 0, 4); }
                catch { } // SocketException on close = expected
            });
            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                {
                    await ReloadMiniServerTestAwait.WaitUntilAsync(
                        () => ReloadMiniServerTestAwait.ActiveClientCount > 0,
                        timeout.Token,
                        "AcceptLoop did not register the concurrent reader within 3s");

                    var stopTask = Task.Run(() =>
                    {
                        try { server.Stop(); }
                        catch (Exception e) { caught = e; }
                    });
                    await ReloadMiniServerTestAwait.WaitForTaskAsync(
                        stopTask, timeout.Token,
                        "Stop() did not complete within 3s with a concurrent reader");
                }
            }
            finally
            {
                try { client.Close(); } catch { }
            }

            using (var readerTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                await ReloadMiniServerTestAwait.WaitForTaskAsync(
                    reader, readerTimeout.Token,
                    "Concurrent reader did not exit within 2s after Stop()");
            }
            Assert.IsNull(caught, $"Stop() must not throw with concurrent reader: {caught}");
        }
    }

    internal static class ReloadMiniServerTestAwait
    {
        internal static int ActiveClientCount
        {
            get
            {
                var field = typeof(ReloadMiniServer).GetField(
                    "_activeClients", BindingFlags.NonPublic | BindingFlags.Static);
                var clients = field?.GetValue(null) as ConcurrentDictionary<int, TcpClient>;
                return clients?.Count ?? 0;
            }
        }

        internal static async Task WaitUntilAsync(
            Func<bool> predicate,
            CancellationToken cancellationToken,
            string timeoutMessage)
        {
            try
            {
                while (!predicate())
                    await Task.Delay(10, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(timeoutMessage);
            }
        }

        internal static async Task WaitForTaskAsync(
            Task task,
            CancellationToken cancellationToken,
            string timeoutMessage)
        {
            var cancellation = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(task, cancellation) != task)
                throw new TimeoutException(timeoutMessage);
            await task;
        }
    }
}
