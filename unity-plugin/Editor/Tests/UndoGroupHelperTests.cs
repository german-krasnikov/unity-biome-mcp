// TDD — RED first. Tests for UndoGroupHelper.OpenNamedGroup / CloseNamedGroup /
// RevertToBeforeGroup / CanRevert (scenarios 1-6, Feature F6).
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UndoGroupHelperTests
    {
        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            // Replace scene to destroy any GameObjects leaked by failed undo operations
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // --- 1. OpenNamedGroup returns a non-negative id -----------------------
        [Test]
        public void OpenNamedGroup_ReturnsNonNegativeId()
        {
            int id = UndoGroupHelper.OpenNamedGroup("Test_1");
            Assert.GreaterOrEqual(id, 0);
        }

        // --- 2. CloseNamedGroup collapses: single PerformUndo removes both objects ---
        [Test]
        public void CloseNamedGroup_CollapsesTwoObjects_SingleUndoRemovesBoth()
        {
            int id = UndoGroupHelper.OpenNamedGroup("Test_2");

            var a = new GameObject("UGH_Test_A");
            Undo.RegisterCreatedObjectUndo(a, "create A");
            var b = new GameObject("UGH_Test_B");
            Undo.RegisterCreatedObjectUndo(b, "create B");

            UndoGroupHelper.CloseNamedGroup(id);

            Undo.PerformUndo();

            Assert.IsTrue(a == null, "Object A should be destroyed after single undo");
            Assert.IsTrue(b == null, "Object B should be destroyed after single undo");
        }

        // --- 3. RevertToBeforeGroup undoes all operations in the group ----------
        [Test]
        public void RevertToBeforeGroup_UndoesAllOpsInGroup()
        {
            int id = UndoGroupHelper.OpenNamedGroup("Test_3");

            var go = new GameObject("UGH_Test_C");
            Undo.RegisterCreatedObjectUndo(go, "create C");

            UndoGroupHelper.CloseNamedGroup(id);
            UndoGroupHelper.RevertToBeforeGroup(id);

            Assert.IsTrue(go == null, "Object should be destroyed after RevertToBeforeGroup");
        }

        // --- 4. Multiple groups: revert B leaves A alive -----------------------
        [Test]
        public void RevertToBeforeGroup_MultipleGroups_RevertsOnlyTargetGroup()
        {
            int idA = UndoGroupHelper.OpenNamedGroup("Test_4A");
            var obj1 = new GameObject("UGH_Test_D");
            Undo.RegisterCreatedObjectUndo(obj1, "create D");
            UndoGroupHelper.CloseNamedGroup(idA);

            int idB = UndoGroupHelper.OpenNamedGroup("Test_4B");
            var obj2 = new GameObject("UGH_Test_E");
            Undo.RegisterCreatedObjectUndo(obj2, "create E");
            UndoGroupHelper.CloseNamedGroup(idB);

            UndoGroupHelper.RevertToBeforeGroup(idB);

            Assert.IsTrue(obj2 == null, "Object2 (group B) should be gone after revert B");
            Assert.IsTrue(obj1 != null, "Object1 (group A) should still be alive");

            // Cleanup
            Object.DestroyImmediate(obj1);
        }

        // --- 5. CanRevert(-1) == false -----------------------------------------
        [Test]
        public void CanRevert_NegativeId_ReturnsFalse()
        {
            Assert.IsFalse(UndoGroupHelper.CanRevert(-1));
        }

        // --- 6. CanRevert(validId) == true -------------------------------------
        [Test]
        public void CanRevert_ValidId_ReturnsTrue()
        {
            int id = UndoGroupHelper.OpenNamedGroup("Test_6");
            UndoGroupHelper.CloseNamedGroup(id);
            Assert.IsTrue(UndoGroupHelper.CanRevert(id));
        }
    }
}
