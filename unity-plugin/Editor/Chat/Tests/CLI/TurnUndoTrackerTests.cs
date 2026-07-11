// TDD — RED first. Tests for TurnUndoTracker (scenarios 7-14 + generation test, Feature F6).
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TurnUndoTrackerTests
    {
        private TurnUndoTracker _tracker;

        [SetUp]
        public void SetUp() => _tracker = new TurnUndoTracker();

        // --- 7. OnTurnStart → HasRestorableGroup == false (turn not ended yet) --
        [Test]
        public void OnTurnStart_HasRestorableGroup_IsFalse()
        {
            _tracker.OnTurnStart("Test_7");
            Assert.IsFalse(_tracker.HasRestorableGroup);
        }

        // --- 8. OnTurnEnd → HasRestorableGroup == true -------------------------
        [Test]
        public void OnTurnEnd_HasRestorableGroup_IsTrue()
        {
            _tracker.OnTurnStart("Test_8");
            _tracker.OnTurnEnd();
            Assert.IsTrue(_tracker.HasRestorableGroup);
        }

        // --- 9. RestoreLastTurn reverts a created object -----------------------
        [Test]
        public void RestoreLastTurn_RevertsCreatedObject()
        {
            _tracker.OnTurnStart("Test_9");
            var go = new GameObject("TUT_Test_9");
            Undo.RegisterCreatedObjectUndo(go, "create 9");
            _tracker.OnTurnEnd();

            _tracker.RestoreLastTurn();

            Assert.IsTrue(go == null, "Object should be destroyed after RestoreLastTurn");
        }

        // --- 10. RestoreLastTurn on empty stack → no exception -----------------
        [Test]
        public void RestoreLastTurn_EmptyStack_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.RestoreLastTurn());
        }

        // --- 11. Two turns: restore reverts only the last (obj2 gone, obj1 alive) ---
        [Test]
        public void RestoreLastTurn_TwoTurns_RevertsOnlyLast()
        {
            _tracker.OnTurnStart("Turn_A");
            var obj1 = new GameObject("TUT_Test_11a");
            Undo.RegisterCreatedObjectUndo(obj1, "create 11a");
            _tracker.OnTurnEnd();

            _tracker.OnTurnStart("Turn_B");
            var obj2 = new GameObject("TUT_Test_11b");
            Undo.RegisterCreatedObjectUndo(obj2, "create 11b");
            _tracker.OnTurnEnd();

            _tracker.RestoreLastTurn();

            Assert.IsTrue(obj2 == null, "obj2 (turn B) should be gone");
            Assert.IsTrue(obj1 != null, "obj1 (turn A) should still be alive");

            // Cleanup
            Object.DestroyImmediate(obj1);
        }

        // --- 12. OnTurnFailed → still restorable, restore removes object -------
        [Test]
        public void OnTurnFailed_StillRestorable_RestoreRemovesObject()
        {
            _tracker.OnTurnStart("Test_12");
            var go = new GameObject("TUT_Test_12");
            Undo.RegisterCreatedObjectUndo(go, "create 12");
            _tracker.OnTurnFailed();

            Assert.IsTrue(_tracker.HasRestorableGroup, "Failed turn should still be restorable");
            _tracker.RestoreLastTurn();
            Assert.IsTrue(go == null, "Object should be destroyed after restoring failed turn");
        }

        // --- 13. Invalidate clears all → HasRestorableGroup == false -----------
        [Test]
        public void Invalidate_ClearsAll_HasRestorableGroup_IsFalse()
        {
            _tracker.OnTurnStart("Test_13");
            _tracker.OnTurnEnd();
            Assert.IsTrue(_tracker.HasRestorableGroup);

            _tracker.Invalidate();
            Assert.IsFalse(_tracker.HasRestorableGroup);
        }

        // --- 14. RestoreLastTurn after Invalidate → no-op ----------------------
        [Test]
        public void RestoreLastTurn_AfterInvalidate_IsNoOp()
        {
            _tracker.OnTurnStart("Test_14");
            var go = new GameObject("TUT_Test_14");
            Undo.RegisterCreatedObjectUndo(go, "create 14");
            _tracker.OnTurnEnd();

            _tracker.Invalidate();
            Assert.DoesNotThrow(() => _tracker.RestoreLastTurn());
            // Object is NOT reverted — group id is stale/cleared
            Assert.IsTrue(go != null, "Object should still exist after no-op restore");

            Object.DestroyImmediate(go);
        }

        // --- 16. InflightGroupId exposed for SaveStateBeforeReload (#12) --------
        [Test]
        public void InflightGroupId_DuringTurn_IsNonNegative()
        {
            Assert.AreEqual(-1, _tracker.InflightGroupId, "idle tracker must return -1");
            _tracker.OnTurnStart("Test_16");
            Assert.GreaterOrEqual(_tracker.InflightGroupId, 0, "in-flight group id must be >= 0");
            _tracker.OnTurnEnd();
            Assert.AreEqual(-1, _tracker.InflightGroupId, "after OnTurnEnd must be -1 again");
        }

        // --- 15. Generation check: old turn's restore becomes disabled when new turn starts ---
        [Test]
        public void CurrentGeneration_AdvancesOnEachTurnStart()
        {
            int gen0 = _tracker.CurrentGeneration;
            _tracker.OnTurnStart("Gen_1");
            int gen1 = _tracker.CurrentGeneration;
            _tracker.OnTurnEnd();
            _tracker.OnTurnStart("Gen_2");
            int gen2 = _tracker.CurrentGeneration;

            Assert.Greater(gen1, gen0, "Generation should increase after first OnTurnStart");
            Assert.Greater(gen2, gen1, "Generation should increase after second OnTurnStart");
        }

        // --- #13: cross-consumer rollback tests ---

        // #13: turn group wraps batch group — RestoreLastTurn reverts both after failed atomic batch.
        [Test]
        public void RestoreLastTurn_AfterFailedAtomicBatch_RevertsBoth()
        {
            _tracker.OnTurnStart("Turn_FailedBatch");
            var preBatch = new GameObject("TUT_PreBatch_Fail");
            Undo.RegisterCreatedObjectUndo(preBatch, "pre-batch object");
            int batchGid = UndoGroupHelper.OpenNamedGroup("MCP Atomic Batch");
            var inBatch = new GameObject("TUT_InBatch_Fail");
            Undo.RegisterCreatedObjectUndo(inBatch, "in-batch object");
            UndoGroupHelper.RevertToBeforeGroup(batchGid); // atomic rollback
            _tracker.OnTurnFailed();
            _tracker.RestoreLastTurn();
            Assert.IsTrue(preBatch == null, "Pre-batch object must be reverted by RestoreLastTurn");
            Assert.IsTrue(inBatch  == null, "In-batch object already reverted by RevertToBeforeGroup");
        }

        // --- F2: RestoreFromIndex scenarios ---

        [Test]
        public void RestoreFromIndex_LastIndex_RevertsOnlyLast()
        {
            _tracker.OnTurnStart("A"); var o1 = new GameObject("TUT_F2_A"); Undo.RegisterCreatedObjectUndo(o1, "A"); _tracker.OnTurnEnd();
            _tracker.OnTurnStart("B"); var o2 = new GameObject("TUT_F2_B"); Undo.RegisterCreatedObjectUndo(o2, "B"); _tracker.OnTurnEnd();

            _tracker.RestoreFromIndex(1); // last turn

            Assert.IsTrue(o2 == null, "o2 reverted");
            Assert.IsTrue(o1 != null, "o1 untouched");
            Object.DestroyImmediate(o1);
        }

        [Test]
        public void RestoreFromIndex_FirstOfThree_CascadeRevertsAll()
        {
            _tracker.OnTurnStart("A"); var o1 = new GameObject("TUT_F2_C1"); Undo.RegisterCreatedObjectUndo(o1, "A"); _tracker.OnTurnEnd();
            _tracker.OnTurnStart("B"); var o2 = new GameObject("TUT_F2_C2"); Undo.RegisterCreatedObjectUndo(o2, "B"); _tracker.OnTurnEnd();
            _tracker.OnTurnStart("C"); var o3 = new GameObject("TUT_F2_C3"); Undo.RegisterCreatedObjectUndo(o3, "C"); _tracker.OnTurnEnd();

            _tracker.RestoreFromIndex(0);

            Assert.IsTrue(o1 == null, "o1 reverted");
            Assert.IsTrue(o2 == null, "o2 reverted");
            Assert.IsTrue(o3 == null, "o3 reverted");
        }

        [Test]
        public void RestoreFromIndex_MiddleOfThree_RevertsLastTwo()
        {
            _tracker.OnTurnStart("A"); var o1 = new GameObject("TUT_F2_M1"); Undo.RegisterCreatedObjectUndo(o1, "A"); _tracker.OnTurnEnd();
            _tracker.OnTurnStart("B"); var o2 = new GameObject("TUT_F2_M2"); Undo.RegisterCreatedObjectUndo(o2, "B"); _tracker.OnTurnEnd();
            _tracker.OnTurnStart("C"); var o3 = new GameObject("TUT_F2_M3"); Undo.RegisterCreatedObjectUndo(o3, "C"); _tracker.OnTurnEnd();

            _tracker.RestoreFromIndex(1);

            Assert.IsTrue(o2 == null, "o2 reverted");
            Assert.IsTrue(o3 == null, "o3 reverted");
            Assert.IsTrue(o1 != null, "o1 untouched");
            Object.DestroyImmediate(o1);
        }

        [Test]
        public void RestoreFromIndex_InvalidIndex_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _tracker.RestoreFromIndex(-1));
            Assert.DoesNotThrow(() => _tracker.RestoreFromIndex(99));
        }

        // #13: turn group wraps batch group — RestoreLastTurn reverts both after successful atomic batch.
        [Test]
        public void RestoreLastTurn_AfterSuccessfulAtomicBatch_RevertsBoth()
        {
            _tracker.OnTurnStart("Turn_SuccessBatch");
            var preBatch = new GameObject("TUT_PreBatch_Ok");
            Undo.RegisterCreatedObjectUndo(preBatch, "pre-batch object ok");
            int batchGid = UndoGroupHelper.OpenNamedGroup("MCP Atomic Batch");
            var inBatch = new GameObject("TUT_InBatch_Ok");
            Undo.RegisterCreatedObjectUndo(inBatch, "in-batch object ok");
            UndoGroupHelper.CloseNamedGroup(batchGid); // batch succeeded
            _tracker.OnTurnEnd();
            _tracker.RestoreLastTurn();
            Assert.IsTrue(preBatch == null, "Pre-batch object must be reverted");
            Assert.IsTrue(inBatch  == null, "In-batch object must be reverted");
        }
    }
}
