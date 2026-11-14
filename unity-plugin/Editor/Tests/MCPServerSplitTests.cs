// TDD: Phase 2 M1 — MCPServer split regression guards.
// Verifies the extracted PortFileManager/MainThreadDispatcher/ClientSlot types are
// reachable and behave correctly after the mechanical move out of MCPServer.cs.
using System.Collections.Concurrent;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerSplitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void MainThreadDispatcher_EnqueueThenDrain_RunsQueuedAction()
        {
            var queue = new ConcurrentQueue<System.Action>();
            bool ran = false;
            MainThreadDispatcher.Enqueue(queue, () => ran = true);
            MainThreadDispatcher.Drain(queue);
            Assert.IsTrue(ran, "Drain must execute actions enqueued via Enqueue");
        }

        [Test]
        public void MainThreadDispatcher_Clear_DiscardsPendingActions()
        {
            var queue = new ConcurrentQueue<System.Action>();
            bool ran = false;
            MainThreadDispatcher.Enqueue(queue, () => ran = true);
            MainThreadDispatcher.Clear(queue);
            MainThreadDispatcher.Drain(queue);
            Assert.IsFalse(ran, "Clear must discard queued actions before Drain can run them");
        }

        [Test]
        public void ClientSlot_PromotedTopLevelType_StartsWithNoConnections()
        {
            var slot = new ClientSlot();
            Assert.IsFalse(slot.AnyConnected, "Freshly constructed ClientSlot must have no connections");
            Assert.AreEqual(0, slot.CountPhantoms(), "Freshly constructed ClientSlot must have no phantom entries");
        }

        // ── TICK-DISPATCHER Part A: MainThreadDispatcher owns its own update hook ──

        [Test]
        public void MCPServer_DoesNotSubscribeMainThreadDispatcherDrain()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            Assert.That(src, Does.Not.Contain("update += MainThreadDispatcher.Drain"),
                "MCPServer must not subscribe MainThreadDispatcher.Drain — the dispatcher hooks its " +
                "own EditorApplication.update via its own [InitializeOnLoad] static constructor");
            Assert.That(src, Does.Not.Contain("update -= MainThreadDispatcher.Drain"),
                "MCPServer must not unsubscribe MainThreadDispatcher.Drain — it never subscribed it");
        }

        // ── Task 1.3: MCPServer routes before-reload through ReloadLifecycleCoordinator ──

        [Test]
        public void RegisterLifecycleCallbacks_RoutesBeforeReloadThroughCoordinator()
        {
            var src = ReadRequiredPackageSource(typeof(MCPServer), "Editor/MCPServer.cs");
            var start = src.IndexOf("internal static void RegisterLifecycleCallbacks()");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "RegisterLifecycleCallbacks method not found");
            var end = src.IndexOf("private static void OnCompilationStarted", start);
            Assert.That(end, Is.GreaterThan(start), "OnCompilationStarted not found after RegisterLifecycleCallbacks");
            var body = src.Substring(start, end - start);

            Assert.That(body, Does.Not.Contain("AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload"),
                "MCPServer must not subscribe AssemblyReloadEvents directly — ReloadLifecycleCoordinator " +
                "owns the one subscription pair");
            Assert.That(body, Does.Contain("ReloadLifecycleCoordinator.Register(_reloadSink)"),
                "MCPServer must register its reload sink with ReloadLifecycleCoordinator");

            // OnBeforeReload() body itself must be byte-for-byte unchanged by this refactor.
            Assert.That(src, Does.Contain(
                "private static void OnBeforeReload()\n" +
                "        {\n" +
                "            _shuttingDown = true;\n" +
                "            WriteStateFile(\"reloading\");"),
                "OnBeforeReload's existing body must not change — only its subscription site moves");
        }
    }
}
