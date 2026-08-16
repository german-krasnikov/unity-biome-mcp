// TDD: WP5 — Undo safety for non-atomic batch.
// T1 is the exact bug: direct create → batch create → undo_last → only batch reverted.
// T2-T5 are boundary cases for the push-or-not rule.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchUndoSafetyTests : UnityMcpTestBase
    {
        private readonly List<GameObject> _toDestroy = new List<GameObject>();
        private Func<bool> _origIsReadOnly;

        [SetUp]
        public void SetUp()
        {
            UndoGroupStack.Clear();
            _origIsReadOnly = CommandRouter.IsReadOnly;
            CommandRouter.IsReadOnly = () => false;
            CommandRouter.IsCompiling = () => false;
            BatchHelper.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsReadOnly = _origIsReadOnly;
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            UndoGroupStack.Clear();
            foreach (var go in _toDestroy)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        // T1: The exact bug — direct create then batch create then undo_last only reverts batch.
        // RED before fix: UndoGroupStack.Count == 1 (batch not pushed), so RevertLast(1)
        //   reverts down to Direct's group and both objects are gone.
        // GREEN after fix: Count == 2; RevertLast(1) only reverts the batch group.
        [Test]
        public void DirectThenBatch_UndoLast_OnlyBatchReverted()
        {
            CommandRouter.Process("{\"id\":\"t1d\",\"cmd\":\"create_object\",\"args\":{\"name\":\"BUS_TestDirect\"}}");
            var direct = GameObject.Find("BUS_TestDirect");
            if (direct != null) _toDestroy.Add(direct);
            Assert.IsNotNull(direct, "BUS_TestDirect should be created by direct command");

            BatchHelper.Execute("create_object name=BUS_TestViaBatch", "continue", 25000, false);
            var viaBatch = GameObject.Find("BUS_TestViaBatch");
            if (viaBatch != null) _toDestroy.Add(viaBatch);
            Assert.IsNotNull(viaBatch, "BUS_TestViaBatch should be created by batch");

            Assert.AreEqual(2, UndoGroupStack.Count,
                "Both direct command and batch must push to UndoGroupStack");

            UndoGroupStack.RevertLast(1);

            Assert.IsNotNull(GameObject.Find("BUS_TestDirect"),
                "Direct object must survive undo of batch group");
            Assert.IsNull(GameObject.Find("BUS_TestViaBatch"),
                "Batch object must be reverted by undo_last");
        }

        // T2: Read-only batch — no push to UndoGroupStack (hadMutations stays false).
        [Test]
        public void ReadOnlyBatch_NoPush()
        {
            BatchHelper.Execute("get_hierarchy", "continue", 25000, false);
            Assert.AreEqual(0, UndoGroupStack.Count,
                "Read-only batch must not push to UndoGroupStack");
        }

        // T3: Mixed read+write batch — group pushed exactly once.
        [Test]
        public void MixedBatch_GroupPushedOnce()
        {
            BatchHelper.Execute(
                "get_hierarchy\ncreate_object name=BUS_MixedObj",
                "continue", 25000, false);
            var obj = GameObject.Find("BUS_MixedObj");
            if (obj != null) _toDestroy.Add(obj);
            Assert.AreEqual(1, UndoGroupStack.Count,
                "Mixed batch with a mutation must push exactly one group");
            Assert.IsNotNull(obj, "BUS_MixedObj should be created");
        }

        // T4: Nested batch — only the outer group pushed (inner at depth==2 is not root).
        [Test]
        public void NestedBatch_OnlyOneGroupPushed()
        {
            BatchHelper.Execute(
                "batch commands=\"create_object name=BUS_NestedObj\"",
                "continue", 25000, false);
            var obj = GameObject.Find("BUS_NestedObj");
            if (obj != null) _toDestroy.Add(obj);
            Assert.AreEqual(1, UndoGroupStack.Count,
                "Nested batch must push only the outer (depth==1) group");
        }

        // T5: Atomic batch — NOT pushed to UndoGroupStack.
        // Atomic batches self-revert via AtomicFail; undo_last must not target them.
        [Test]
        public void AtomicBatch_NotPushedToUndoGroupStack()
        {
            BatchHelper.Execute("create_object name=BUS_AtomicObj", "continue", 25000, atomic: true);
            var obj = GameObject.Find("BUS_AtomicObj");
            if (obj != null) _toDestroy.Add(obj);
            Assert.AreEqual(0, UndoGroupStack.Count,
                "Atomic batch must NOT push to UndoGroupStack");
        }
    }
}
