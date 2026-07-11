// TDD tests for RelaySpawnState — the async wrapper RelayBackend.Start() uses in production
// so a uvx cold start (up to 45s, see RelaySpawner.TimeoutFor) never freezes the main thread.
//
// Threading contract under test: Unity Editor APIs (SessionState, EditorPrefs, PackageInfo,
// Debug.Log) are not safe to call off the main thread. RelaySpawnState splits the cold-start
// path into PrepareSpawn() (main thread, Editor-API resolution) → ExecuteSpawn() (ThreadPool,
// pure I/O) → CommitSpawn() (main thread, SessionState writes). The tests below inject
// PreparePlanOverride/ExecutePlanOverride to prove: (a) PrepareSpawn always runs synchronously
// on the calling thread before any ThreadPool hop, (b) ExecuteSpawn genuinely runs off that
// thread, and (c) a PrepareSpawn failure never even reaches the ThreadPool.
//
// The "already running" fast path is synchronous and fully covered here too. The cold-start
// path's ThreadPool→MainThreadDispatcher handoff is covered by manually draining the queue
// (same technique MCPServerSplitTests.cs uses for MainThreadDispatcher itself), which exercises
// the real production code path without needing a live EditorApplication tick.
//
// What is NOT covered by this file: the actual RelayBackend.Start()/SendTurn() wiring in the
// `#if !UNITY_INCLUDE_TESTS` branch of RelayBackend.cs (that branch never compiles into test
// assemblies) — that wiring requires manual/PlayMode verification against a real relay process.
using System;
using System.Threading;
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelaySpawnStateTests
    {
        [SetUp]
        public void SetUp()
        {
            RelaySpawnState.ResetForTests();
            MainThreadDispatcher.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RelaySpawnState.ResetForTests();
            MainThreadDispatcher.Clear();
        }

        // ── Fast path: relay already running ──────────────────────────────────

        [Test]
        public void RequestSpawn_AlreadyRunning_CallsOnReadySynchronously()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => true;
            RelaySpawnState.EnsureRunningOverride        = () => 19700;

            int? readyPort = null;
            RelaySpawnState.RequestSpawn(port => readyPort = port, err => Assert.Fail(err));

            Assert.AreEqual(19700, readyPort, "Fast path must invoke onReady before returning");
            Assert.IsTrue(RelaySpawnState.IsReady);
            Assert.IsFalse(RelaySpawnState.IsPending, "Fast path must never set IsPending");
            Assert.AreEqual(19700, RelaySpawnState.Port);
        }

        [Test]
        public void RequestSpawn_AlreadyRunning_EnsureRunningThrows_CallsOnErrorSynchronously()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => true;
            RelaySpawnState.EnsureRunningOverride        = () => throw new InvalidOperationException("boom");

            string error = null;
            RelaySpawnState.RequestSpawn(port => Assert.Fail("onReady must not fire"), msg => error = msg);

            Assert.AreEqual("boom", error);
            Assert.IsFalse(RelaySpawnState.IsPending);
            Assert.IsFalse(RelaySpawnState.IsReady);
            Assert.AreEqual("boom", RelaySpawnState.Error);
        }

        // ── Cold-start path: PrepareSpawn (main thread) → ExecuteSpawn (ThreadPool) ─

        [Test]
        public void RequestSpawn_NotRunning_PreparePlanRunsSynchronously_OnCallingThread()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => false;
            var callingThreadId = Thread.CurrentThread.ManagedThreadId;
            var prepareThreadId = -1;
            RelaySpawnState.PreparePlanOverride = () =>
            {
                prepareThreadId = Thread.CurrentThread.ManagedThreadId;
                return new RelaySpawner.SpawnPlan("bash", Array.Empty<string>(), true, TimeSpan.FromSeconds(1));
            };
            RelaySpawnState.ExecutePlanOverride = plan => (19701, 4241);

            RelaySpawnState.RequestSpawn(port => { }, err => { });

            // Must have run already (synchronously) before RequestSpawn even returns.
            Assert.AreEqual(callingThreadId, prepareThreadId,
                "PrepareSpawn must run on the calling thread — it touches Editor APIs and must " +
                "happen before any ThreadPool hop, not inside one");

            WaitForPendingToClear();
        }

        [Test]
        public void RequestSpawn_NotRunning_ExecutePlanRunsOffCallingThread()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => false;
            var callingThreadId = Thread.CurrentThread.ManagedThreadId;
            RelaySpawnState.PreparePlanOverride = () =>
                new RelaySpawner.SpawnPlan("bash", Array.Empty<string>(), true, TimeSpan.FromSeconds(1));
            var executeThreadId = -1;
            RelaySpawnState.ExecutePlanOverride = plan =>
            {
                executeThreadId = Thread.CurrentThread.ManagedThreadId;
                return (19702, 4242);
            };

            int? readyPort = null;
            RelaySpawnState.RequestSpawn(port => readyPort = port, err => Assert.Fail(err));

            WaitForPendingToClear();

            Assert.AreNotEqual(-1, executeThreadId, "ExecutePlan must have run");
            Assert.AreNotEqual(callingThreadId, executeThreadId,
                "ExecutePlan (the I/O step — Process.Start + read stdout) must run off the calling " +
                "thread so a uvx cold start never blocks the Unity main thread");
            Assert.AreEqual(19702, readyPort);
            Assert.IsTrue(RelaySpawnState.IsReady);
            Assert.AreEqual(19702, RelaySpawnState.Port);
        }

        [Test]
        public void RequestSpawn_PreparePlanThrows_CallsOnErrorSynchronously_NeverHopsToThreadPool()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => false;
            RelaySpawnState.PreparePlanOverride = () => throw new InvalidOperationException("uv not found");
            var executePlanCalled = false;
            RelaySpawnState.ExecutePlanOverride = plan => { executePlanCalled = true; return (0, 0); };

            string error = null;
            RelaySpawnState.RequestSpawn(port => Assert.Fail("onReady must not fire"), msg => error = msg);

            Assert.AreEqual("uv not found", error,
                "onError must fire synchronously — a resolution failure is known before any I/O");
            Assert.IsFalse(RelaySpawnState.IsPending,
                "IsPending must already be cleared — no background work was ever started");
            Assert.IsFalse(executePlanCalled,
                "ExecutePlan must never run when PrepareSpawn fails — nothing to hop to the ThreadPool for");
        }

        [Test]
        public void RequestSpawn_NotRunning_ExecutePlanThrows_OnErrorCarriesMessage()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => false;
            RelaySpawnState.PreparePlanOverride = () =>
                new RelaySpawner.SpawnPlan("bash", Array.Empty<string>(), true, TimeSpan.FromSeconds(1));
            RelaySpawnState.ExecutePlanOverride = plan =>
                throw new InvalidOperationException("relay crashed mid-spawn");

            string error = null;
            RelaySpawnState.RequestSpawn(port => Assert.Fail("onReady must not fire"), msg => error = msg);

            WaitForPendingToClear();

            Assert.AreEqual("relay crashed mid-spawn", error);
            Assert.AreEqual("relay crashed mid-spawn", RelaySpawnState.Error);
            Assert.IsFalse(RelaySpawnState.IsReady);
        }

        [Test]
        public void RequestSpawn_SecondCallWhilePending_IsNoOp_DoesNotDoubleExecute()
        {
            RelaySpawnState.LooksAlreadyRunningOverride = () => false;
            RelaySpawnState.PreparePlanOverride = () =>
                new RelaySpawner.SpawnPlan("bash", Array.Empty<string>(), true, TimeSpan.FromSeconds(1));
            var executeCount = 0;
            var gate = new ManualResetEventSlim(false);
            RelaySpawnState.ExecutePlanOverride = plan =>
            {
                Interlocked.Increment(ref executeCount);
                gate.Wait(2000);
                return (19703, 4243);
            };

            RelaySpawnState.RequestSpawn(port => { }, err => { });
            Assert.IsTrue(RelaySpawnState.IsPending);

            // Second call while the first spawn is still in flight must not start another one.
            RelaySpawnState.RequestSpawn(port => { }, err => { });

            gate.Set();
            WaitForPendingToClear();

            Assert.AreEqual(1, executeCount, "A spawn already in flight must not be started twice");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Manually pumps MainThreadDispatcher.Drain() — the same call EditorApplication.update
        // wires up in MCPServer.StartAsync — until the background spawn's continuation has run.
        private static void WaitForPendingToClear(int timeoutMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                MainThreadDispatcher.Drain();
                if (!RelaySpawnState.IsPending) return;
                Thread.Sleep(10);
            }
            Assert.Fail("Timed out waiting for background spawn to resolve");
        }
    }
}
