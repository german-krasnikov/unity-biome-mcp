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
    }
}
