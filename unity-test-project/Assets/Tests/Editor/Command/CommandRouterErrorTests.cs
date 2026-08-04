using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Command
{
    [TestFixture]
    public class CommandRouterErrorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ObjectNotFound_ShowsRootObjects()
        {
            var go = new GameObject("KnownObj");
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not found"));
                var json = "{\"id\":\"e1\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/NonExistent\",\"type\":\"Transform\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("not found", result);
                StringAssert.Contains("Root objects:", result);
                StringAssert.Contains("KnownObj", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ComponentNotFound_ShowsAvailable()
        {
            var go = new GameObject("CompTestObj");
            go.AddComponent<BoxCollider>();
            try
            {
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not on"));
                var json = "{\"id\":\"e2\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/CompTestObj\",\"type\":\"Rigidbody\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("not on", result);
                StringAssert.Contains("Available:", result);
                StringAssert.Contains("BoxCollider", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetProperty_ObjectNotFound_ShowsRoots()
        {
            var go = new GameObject("ExistingObj");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not found"));
                var json = "{\"id\":\"e3\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/Ghost\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition.x\",\"value\":\"5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("not found", result);
                StringAssert.Contains("Root objects:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ManageComponent_ComponentNotFound_ShowsAvailable()
        {
            var go = new GameObject("ManageCompObj");
            go.AddComponent<BoxCollider>();
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not on"));
                var json = "{\"id\":\"e4\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/ManageCompObj\",\"type\":\"Rigidbody\",\"action\":\"remove\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("not on", result);
                StringAssert.Contains("Available:", result);
                StringAssert.Contains("BoxCollider", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ErrorHelper_PropertyNotFound_IncludesHint()
        {
            var msg = ErrorHelper.PropertyNotFound("m_Missing", "Transform", "/MyObj");
            StringAssert.Contains("m_Missing", msg);
            StringAssert.Contains("Transform", msg);
            StringAssert.Contains("get_component", msg);
            StringAssert.Contains("/MyObj", msg);
        }

        [Test]
        public void ErrorHelper_InvalidAction_ListsValid()
        {
            var msg = ErrorHelper.InvalidAction("fly", new[] { "add", "remove" });
            StringAssert.Contains("fly", msg);
            StringAssert.Contains("add", msg);
            StringAssert.Contains("remove", msg);
        }

        [Test]
        public void CreateObject_ReturnsParentSubtree()
        {
            var parent = new GameObject("CreateParent");
            try
            {
                var json = "{\"id\":\"m1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"CreateChild\",\"parent\":\"/CreateParent\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Created", result);
                StringAssert.Contains("parent", result);
                StringAssert.Contains("CreateParent", result);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void DeleteObject_ReturnsParentSubtree()
        {
            var parent = new GameObject("DeleteParent");
            var child = new GameObject("DeleteChild");
            child.transform.SetParent(parent.transform);
            var id = TransientObjectId.GetWireValue(child);
            try
            {
                var json = "{\"id\":\"m2\",\"cmd\":\"delete_object\",\"args\":{\"id\":\"" + id + "\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Deleted", result);
                StringAssert.Contains("parent", result);
                StringAssert.Contains("DeleteParent", result);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ManageComponent_ReturnsComponentList()
        {
            var go = new GameObject("ManageListObj");
            try
            {
                var json = "{\"id\":\"m3\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/ManageListObj\",\"type\":\"BoxCollider\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Added: BoxCollider", result);
                StringAssert.Contains("BoxCollider", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetProperty_ReturnsEchoBack()
        {
            var go = new GameObject("SetPropEchoObj");
            try
            {
                var json = "{\"id\":\"m4\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/SetPropEchoObj\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition.x\",\"value\":\"5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("m_LocalPosition.x = 5", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FindObject_SceneMode_FindsNormally()
        {
            var go = new GameObject("PrefabTestObj");
            try
            {
                var json = "{\"id\":\"p1\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/PrefabTestObj\",\"type\":\"Transform\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FindObject_EmptyPath_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("required"));
            var json = "{\"id\":\"p2\",\"cmd\":\"get_component\",\"args\":{\"path\":\"\",\"type\":\"Transform\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void FindObject_SlashOnlyPath_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(""));
            var json = "{\"id\":\"p3\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/\",\"type\":\"Transform\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
        }

        // --- FindObjectOrThrow ---

        [Test]
        public void FindObjectOrThrow_ExistingPath_ReturnsObject()
        {
            var go = new GameObject("FindOrThrowExisting");
            try
            {
                var result = ComponentSerializer.FindObjectOrThrow("/FindOrThrowExisting");
                Assert.AreEqual(go, result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FindObjectOrThrow_MissingPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ComponentSerializer.FindObjectOrThrow("/NonExistentObject_XYZ123"));
        }

        [Test]
        public void FindObjectOrThrow_ThrowsWithPathInMessage()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                ComponentSerializer.FindObjectOrThrow("/SomeMissingThing"));
            StringAssert.Contains("SomeMissingThing", ex.Message);
        }

        // Issue 27 (Step 6): execute_code is registered non-mutating, but a runtime exception
        // execute_code console injection removed: return value used programmatically,
        // contamination breaks all consumers. Use get_console for post-execute error check.
    }
}
