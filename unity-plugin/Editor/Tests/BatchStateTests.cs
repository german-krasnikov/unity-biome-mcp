// TDD: BatchState seam tests (Phase 5 A1 — Core.Scene asmdef cycle-breaking).
// BatchState holds the batch-depth counter independently of BatchHelper so
// Core.Scene (which cannot reference BatchHelper/CommandRouter) can still
// read InBatch via the Primitives leaf.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void ResetDepth()
        {
            // Defensive: drain any depth a failed assertion mid-test left behind.
            while (BatchState.InBatch) BatchState.Exit();
        }

        [Test]
        public void InBatch_NoEnter_IsFalse()
        {
            Assert.IsFalse(BatchState.InBatch);
        }

        [Test]
        public void InBatch_AfterEnter_IsTrue()
        {
            BatchState.Enter();
            Assert.IsTrue(BatchState.InBatch);
        }

        [Test]
        public void InBatch_AfterMatchingExit_IsFalseAgain()
        {
            BatchState.Enter();
            BatchState.Exit();
            Assert.IsFalse(BatchState.InBatch);
        }

        [Test]
        public void InBatch_NestedEnter_StaysTrueUntilOutermostExit()
        {
            BatchState.Enter(); // depth 1
            BatchState.Enter(); // depth 2 (nested batch)
            BatchState.Exit();  // depth 1 — still in batch
            Assert.IsTrue(BatchState.InBatch, "nested exit must not clear InBatch while outer depth remains");
            BatchState.Exit();  // depth 0
            Assert.IsFalse(BatchState.InBatch);
        }

        [Test]
        public void Enter_ReturnsNewDepth()
        {
            int depth = BatchState.Enter();
            Assert.AreEqual(1, depth);
        }

        [Test]
        public void Exit_ReturnsNewDepth()
        {
            BatchState.Enter();
            int depth = BatchState.Exit();
            Assert.AreEqual(0, depth);
        }

        [Test]
        public void BatchHelper_InBatch_ForwardsToBatchState_IndependentOfBatchHelperInternals()
        {
            // BatchHelper.InBatch must reflect BatchState alone, not a separate
            // private counter -- proves the forward, not a parallel duplicate.
            Assert.IsFalse(BatchHelper.InBatch);
            BatchState.Enter();
            Assert.IsTrue(BatchHelper.InBatch, "BatchHelper.InBatch must forward to BatchState.InBatch");
            BatchState.Exit();
            Assert.IsFalse(BatchHelper.InBatch);
        }
    }
}
