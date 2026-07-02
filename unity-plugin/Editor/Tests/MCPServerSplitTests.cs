// TDD: Phase 2 M1 — MCPServer split regression guards.
// Verifies the extracted PortFileManager/MainThreadDispatcher/ClientSlot types are
// reachable and behave correctly after the mechanical move out of MCPServer.cs.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPServerSplitTests
    {
        [TearDown]
        public void TearDown()
        {
            MainThreadDispatcher.Clear();
        }

        [Test]
        public void MainThreadDispatcher_EnqueueThenDrain_RunsQueuedAction()
        {
            MCPServer._shuttingDown = false;  // Drain() no-ops while shutting down
            bool ran = false;
            MainThreadDispatcher.Enqueue(() => ran = true);
            MainThreadDispatcher.Drain();
            Assert.IsTrue(ran, "Drain must execute actions enqueued via Enqueue");
        }

        [Test]
        public void MainThreadDispatcher_Clear_DiscardsPendingActions()
        {
            MCPServer._shuttingDown = false;
            bool ran = false;
            MainThreadDispatcher.Enqueue(() => ran = true);
            MainThreadDispatcher.Clear();
            MainThreadDispatcher.Drain();
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
