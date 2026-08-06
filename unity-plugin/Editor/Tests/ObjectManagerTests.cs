// TDD — EditMode tests for ObjectManager mutations (P0-2 audit gap).
// Run in Unity Test Runner → EditMode.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ObjectManagerTests : SceneTestBase
    {
        private GameObject _go;
        private List<GameObject> _toDestroy = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("OM_TestObj");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);

            foreach (var go in _toDestroy)
                if (go != null) Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        // ── 1. CreateObject ───────────────────────────────────────────────────

        [Test]
        public void CreateObject_WithName_CreatesInScene()
        {
            var path = ObjectManager.CreateObject("OM_Created", null, null);

            var found = GameObject.Find("OM_Created");
            _toDestroy.Add(found);
            Assert.IsNotNull(found, "GameObject should exist in scene after CreateObject");
            Assert.AreEqual("/OM_Created", path);
        }

        [Test]
        public void CreateObject_WithPrimitive_CreatesMeshFilter()
        {
            var path = ObjectManager.CreateObject("OM_Created", null, null, primitive: "Cube");

            var found = GameObject.Find("OM_Created");
            _toDestroy.Add(found);
            Assert.IsNotNull(found);
            Assert.IsNotNull(found.GetComponent<MeshFilter>(), "Primitive Cube must have MeshFilter");
        }

        [Test]
        public void CreateObject_UnknownComponent_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.CreateObject("OM_Created", null, "NonExistentComponentXYZ"));

            // cleanup if it partially created
            var stray = GameObject.Find("OM_Created");
            if (stray != null) _toDestroy.Add(stray);
        }

        // ── 2. DeleteObject ───────────────────────────────────────────────────

        [Test]
        public void DeleteObject_RemovesFromScene()
        {
            // _go is tracked; after delete we null it so TearDown skips it
            ObjectManager.DeleteObject("/OM_TestObj");
            _go = null;

            Assert.IsNull(GameObject.Find("OM_TestObj"), "Object should be gone after DeleteObject");
        }

        [Test]
        public void DeleteObject_WithChildren_WithoutForce_ThrowsArgumentException()
        {
            var child = new GameObject("OM_Child");
            child.transform.SetParent(_go.transform);

            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.DeleteObject("/OM_TestObj"));

            // _go and child still alive — TearDown cleans up
        }

        [Test]
        public void DeleteObject_WithChildren_WithForce_DeletesAll()
        {
            var child = new GameObject("OM_Child");
            child.transform.SetParent(_go.transform);

            ObjectManager.DeleteObject("/OM_TestObj", force: true);
            _go = null;

            Assert.IsNull(GameObject.Find("OM_TestObj"));
            Assert.IsNull(GameObject.Find("OM_Child"));
        }

        [Test]
        public void DeleteObject_NotFound_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.DeleteObject("/OM_DoesNotExist_XYZ", force: true));
        }

        // ── 3. SetActive ──────────────────────────────────────────────────────

        [Test]
        public void SetActive_False_DeactivatesObject()
        {
            ObjectManager.SetActive("/OM_TestObj", false);

            Assert.IsFalse(_go.activeSelf, "activeSelf should be false after SetActive(false)");
        }

        [Test]
        public void SetActive_True_ActivatesObject()
        {
            _go.SetActive(false);

            ObjectManager.SetActive("/OM_TestObj", true);

            Assert.IsTrue(_go.activeSelf, "activeSelf should be true after SetActive(true)");
        }

        [Test]
        public void SetActive_ReturnsPathAndState()
        {
            var result = ObjectManager.SetActive("/OM_TestObj", false);

            StringAssert.Contains("OM_TestObj", result);
            StringAssert.Contains("active=False", result);
        }

        // ── 4. ManageComponent ────────────────────────────────────────────────

        [Test]
        public void ManageComponent_Add_AddsComponent()
        {
            ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "add");

            Assert.IsNotNull(_go.GetComponent<Rigidbody>(), "Rigidbody should be present after add");
        }

        [Test]
        public void ManageComponent_Remove_RemovesComponent()
        {
            _go.AddComponent<Rigidbody>();

            ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "remove");

            Assert.IsNull(_go.GetComponent<Rigidbody>(), "Rigidbody should be gone after remove");
        }

        [Test]
        public void ManageComponent_AddDuplicate_ThrowsArgumentException()
        {
            _go.AddComponent<Rigidbody>();

            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "add"));
        }

        [Test]
        public void ManageComponent_RemoveMissing_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "remove"));
        }

        [Test]
        public void ManageComponent_InvalidAction_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "replace"));
        }

        // ── 5. SetParent ──────────────────────────────────────────────────────

        [Test]
        public void SetParent_ValidPath_ReparentsObject()
        {
            var parent = new GameObject("OM_Parent");

            try
            {
                ObjectManager.SetParent("/OM_TestObj", "/OM_Parent");

                Assert.AreEqual(parent.transform, _go.transform.parent,
                    "Parent should be OM_Parent after SetParent");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SetParent_NullParent_UnparentsObject()
        {
            var parent = new GameObject("OM_Parent");
            _go.transform.SetParent(parent.transform);

            try
            {
                ObjectManager.SetParent("/OM_Parent/OM_TestObj", null);

                Assert.IsNull(_go.transform.parent, "Parent should be null after unparenting");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SetParent_InvalidChildPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetParent("/OM_DoesNotExist_XYZ", null));
        }

        // ── 6. SetProperty ────────────────────────────────────────────────────

        [Test]
        public void SetProperty_MarksSceneDirty()
        {
            _go.AddComponent<BoxCollider>();
            var scene = _go.scene;
            Assert.IsTrue(EditorSceneManager.SaveScene(scene),
                "Dirty-state precondition could not be persisted");
            Assert.IsFalse(scene.isDirty, "Scene must be clean before SetProperty");

            ObjectManager.SetProperty("/OM_TestObj", "BoxCollider", "m_Size", "(2,2,2)");

            Assert.IsTrue(scene.isDirty, "SetProperty must mark scene dirty");
        }

        [Test]
        public void SetPropertyDelta_MarksSceneDirty()
        {
            _go.AddComponent<Light>().intensity = 1f;
            var scene = _go.scene;
            Assert.IsTrue(EditorSceneManager.SaveScene(scene),
                "Dirty-state precondition could not be persisted");
            Assert.IsFalse(scene.isDirty, "Scene must be clean before SetPropertyDelta");

            ObjectManager.SetPropertyDelta("/OM_TestObj", "Light", "m_Intensity", "+0.5");

            Assert.IsTrue(scene.isDirty, "SetPropertyDelta must mark scene dirty");
        }

        [Test]
        public void SetProperty_FloatField_UpdatesValue()
        {
            // m_LocalPosition.x on Transform
            ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_LocalPosition", "(5,0,0)");

            Assert.AreEqual(5f, _go.transform.localPosition.x, 0.001f,
                "localPosition.x should be 5 after SetProperty");
        }

        [Test]
        public void SetProperty_InvalidComponent_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty("/OM_TestObj", "NonExistentComponent", "m_LocalPosition", "(0,0,0)"));
        }

        [Test]
        public void SetProperty_InvalidProperty_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_NonExistentProp_XYZ", "0"));
        }

        [Test]
        public void SetProperty_DryRun_DoesNotMutate()
        {
            _go.transform.localPosition = Vector3.zero;

            var result = ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_LocalPosition", "(9,0,0)", dryRun: true);

            Assert.AreEqual(0f, _go.transform.localPosition.x, 0.001f,
                "DryRun must not change value");
            StringAssert.Contains("DRY-RUN", result);
        }

        // ── BUG C: FindType / SafeGetTypes / candidate hints ─────────────────

        [Test]
        public void FindType_FullyQualifiedName_ReturnsType()
        {
            var t = ObjectManager.FindType("UnityEngine.Rigidbody");
            Assert.IsNotNull(t);
            Assert.AreEqual("Rigidbody", t.Name);
        }

        [Test]
        public void FindType_ShortName_ReturnsType()
        {
            var t = ObjectManager.FindType("Rigidbody");
            Assert.IsNotNull(t);
        }

        [Test]
        public void FindType_UnknownType_ReturnsNull()
        {
            var t = ObjectManager.FindType("CompletelyMadeUpType_XYZ");
            Assert.IsNull(t);
        }

        [Test]
        public void SafeGetTypes_ValidAssembly_ReturnsTypes()
        {
            // MAJOR 1: call the real internal method (visible via InternalsVisibleTo)
            // Verifies happy path: a real assembly with no load errors yields its types.
            var result = ObjectManager.SafeGetTypes(typeof(Rigidbody).Assembly).ToList();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0, "UnityEngine assembly must contain types");
            Assert.IsTrue(result.Contains(typeof(Rigidbody)), "Rigidbody must be in UnityEngine assembly types");
        }

        [Test]
        public void FindType_AbstractShortName_ReturnsNull()
        {
            // MAJOR 2: CLR-abstract types must not be returned — AddComponent rejects them.
            // Note: Unity ships some C#-source-"abstract" types as non-abstract in IL
            // (e.g. Renderer: IsAbstract=false at runtime despite C# "abstract" keyword).
            // UIBehaviour (UnityEngine.EventSystems) IS CLR-abstract (IsAbstract=true).
            var t = ObjectManager.FindType("UIBehaviour");
            Assert.IsNull(t, "FindType must not return CLR-abstract types; AddComponent would throw.");
        }

        [Test]
        public void ManageComponent_TypoInTypeName_ErrorMessageContainsCandidates()
        {
            // "Rigidbod" is Levenshtein distance 1 from "Rigidbody"
            var ex = Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbod", "add"));
            StringAssert.Contains("Did you mean", ex.Message);
            StringAssert.Contains("Rigidbody", ex.Message);
        }

        [Test]
        public void ManageComponent_UnknownType_NoCloseMatch_NoHint()
        {
            // "ZZZNoSuchComponent" has no Levenshtein-3 match in TypeCache
            var ex = Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "ZZZNoSuchComponent", "add"));
            StringAssert.DoesNotContain("Did you mean", ex.Message);
        }

        // ── G6: ResolveComponent warns on multiple matches ─────────────────────

        [Test]
        public void ResolveComponent_MultipleMatchingComponents_LogsWarning()
        {
            var go = new GameObject("G6Multi");
            RegisterCleanup(() => UnityEngine.Object.DestroyImmediate(go));
            go.AddComponent<BoxCollider>();
            go.AddComponent<BoxCollider>(); // second of same type

            ConsoleCapture.Clear();
            ObjectManager.ResolveComponent("/G6Multi", "BoxCollider");
            var logs = ConsoleCapture.GetLogs(10, "warning");
            StringAssert.Contains("2", logs, "Warning should mention the count (2)");
            StringAssert.Contains("BoxCollider", logs, "Warning should mention the component type");
        }

        [Test]
        public void ResolveComponent_SingleComponent_NoWarning()
        {
            var go = new GameObject("G6Single");
            RegisterCleanup(() => UnityEngine.Object.DestroyImmediate(go));
            go.AddComponent<BoxCollider>(); // only one

            ConsoleCapture.Clear();
            ObjectManager.ResolveComponent("/G6Single", "BoxCollider");
            var logs = ConsoleCapture.GetLogs(10, "warning");
            Assert.IsEmpty(logs, "No warning when only one component of the type exists");
        }

        // ── P-107: Serialize(GameObject, string) overload ────────────────────

        [Test]
        public void SerializeGO_NullGO_ReturnsNull()
        {
            var result = ComponentSerializer.Serialize((GameObject)null, "Transform");
            Assert.IsNull(result);
        }

        [Test]
        public void SerializeGO_MissingType_ReturnsNull()
        {
            var result = ComponentSerializer.Serialize(_go, "NonExistentTypeXYZ_P107");
            Assert.IsNull(result);
        }

        [Test]
        public void SerializeGO_ExistingType_ReturnsContent()
        {
            var result = ComponentSerializer.Serialize(_go, "Transform");
            Assert.IsNotNull(result, "Serialize(go, type) must return component data");
            StringAssert.Contains("m_LocalPosition", result);
        }

        [Test]
        public void SerializeGO_MatchesSerializePath_ForSameObject()
        {
            // Both overloads must return identical content — no double-FindObject drift.
            var byPath = ComponentSerializer.Serialize("/OM_TestObj", "Transform");
            var byGo   = ComponentSerializer.Serialize(_go, "Transform");
            Assert.AreEqual(byPath, byGo, "Serialize(go, type) and Serialize(path, type) must return same content");
        }

        [Test]
        public void SerializeGO_NestedObject_ReturnsComponent()
        {
            // P-107 root scenario: get_component on a freshly-created nested object
            // must not produce a STATE error. Simulate by calling Serialize(go, type)
            // directly (ExecGetComponent path) without a second FindObject.
            var parent = new GameObject("P107_Parent");
            var child  = new GameObject("P107_Child");
            RegisterCleanup(() => UnityEngine.Object.DestroyImmediate(parent));
            child.transform.SetParent(parent.transform);

            var result = ComponentSerializer.Serialize(child, "Transform");
            Assert.IsNotNull(result, "Nested object: Serialize(go, type) must return data, not STATE error");
        }
    }
}
