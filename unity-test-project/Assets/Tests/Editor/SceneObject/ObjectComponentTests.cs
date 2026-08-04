using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.Events;
using UnityMCP.Editor;

// TestEventScript and TestArrayScript are in Assets/Scripts/ (TestHelpers assembly)
// EmptyMono fixture lives in Assets/EmptyMono.cs (runtime asmdef) — required so
// AddComponent<EmptyMono>() is recognized by Unity's component system in NUnit tests.

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class ObjectComponentTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // --- Object CRUD Edge Cases ---

        [Test]
        public void CreateObjectWithMultipleComponents()
        {
            var json = "{\"id\":\"oc1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"MultiCompObj\",\"components\":\"BoxCollider,Rigidbody,AudioSource,Light,MeshFilter\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var obj = GameObject.Find("MultiCompObj");
            try
            {
                Assert.IsNotNull(obj);
                Assert.IsNotNull(obj.GetComponent<Transform>());
                Assert.IsNotNull(obj.GetComponent<BoxCollider>());
                Assert.IsNotNull(obj.GetComponent<Rigidbody>());
                Assert.IsNotNull(obj.GetComponent<AudioSource>());
                Assert.IsNotNull(obj.GetComponent<Light>());
                Assert.IsNotNull(obj.GetComponent<MeshFilter>());

                // Verify via get_object_detail
                var id = TransientObjectId.GetWireValue(obj);
                var detail = CommandRouter.Process("{\"id\":\"oc1b\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":\"" + id + "\"}}");
                StringAssert.Contains("[BoxCollider]", detail);
                StringAssert.Contains("[Rigidbody]", detail);
                StringAssert.Contains("[AudioSource]", detail);
                StringAssert.Contains("[Light]", detail);
                StringAssert.Contains("[MeshFilter]", detail);
            }
            finally
            {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void CreateNestedHierarchy()
        {
            var createRoot = "{\"id\":\"oc2a\",\"cmd\":\"create_object\",\"args\":{\"name\":\"Root\"}}";
            CommandRouter.Process(createRoot);

            var createChild = "{\"id\":\"oc2b\",\"cmd\":\"create_object\",\"args\":{\"name\":\"Child\",\"parent\":\"/Root\"}}";
            CommandRouter.Process(createChild);

            var createGrand = "{\"id\":\"oc2c\",\"cmd\":\"create_object\",\"args\":{\"name\":\"GrandChild\",\"parent\":\"/Root/Child\"}}";
            CommandRouter.Process(createGrand);

            var root = GameObject.Find("Root");
            var child = GameObject.Find("Child");
            var grand = GameObject.Find("GrandChild");

            try
            {
                Assert.IsNotNull(root);
                Assert.IsNotNull(child);
                Assert.IsNotNull(grand);
                Assert.AreEqual(root.transform, child.transform.parent);
                Assert.AreEqual(child.transform, grand.transform.parent);

                // Verify hierarchy shows nesting
                var hier = CommandRouter.Process("{\"id\":\"oc2d\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99,\"root\":\"Root\"}}");
                StringAssert.Contains("Root", hier);
                StringAssert.Contains("Child", hier);
                StringAssert.Contains("GrandChild", hier);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeleteParent_RemovesChildren()
        {
            var root = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);

            var id = TransientObjectId.GetWireValue(root);
            var json = "{\"id\":\"oc3\",\"cmd\":\"delete_object\",\"args\":{\"id\":\"" + id + "\",\"force\":\"true\"}}";
            CommandRouter.Process(json);

            Assert.IsNull(GameObject.Find("Parent"));
            Assert.IsNull(GameObject.Find("Child"));
        }

        [Test]
        public void CreateObjectWithDuplicateName()
        {
            var json = "{\"id\":\"oc4a\",\"cmd\":\"create_object\",\"args\":{\"name\":\"DupObj\"}}";
            CommandRouter.Process(json);
            CommandRouter.Process(json);

            var objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var count = 0;
            foreach (var obj in objects)
            {
                if (obj.name != "DupObj") continue;
                TrackOwnedObject(obj);
                count++;
            }

            Assert.GreaterOrEqual(count, 2, "Should allow duplicate names");

            // Verify find_objects returns both
            var find = CommandRouter.Process("{\"id\":\"oc4b\",\"cmd\":\"find_objects\",\"args\":{\"name\":\"DupObj\"}}");
            StringAssert.Contains("DupObj", find);
        }

        // --- SetProperty Edge Cases ---

        [Test]
        public void SetPropertyVector3()
        {
            var go = new GameObject("VecTest");
            try
            {
                var json = "{\"id\":\"oc5\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/VecTest\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition\",\"value\":\"(1.5, 2.5, 3.5)\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                Assert.AreEqual(new Vector3(1.5f, 2.5f, 3.5f), go.transform.localPosition);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetPropertyBool()
        {
            var go = new GameObject("BoolTest");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                rb.useGravity = true;

                var json = "{\"id\":\"oc6\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/BoolTest\",\"component\":\"Rigidbody\",\"prop\":\"m_UseGravity\",\"value\":\"false\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                Assert.IsFalse(rb.useGravity);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetPropertyFloat()
        {
            var go = new GameObject("FloatTest");
            var rb = go.AddComponent<Rigidbody>();
            try
            {
                var json = "{\"id\":\"oc7\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/FloatTest\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"5.5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                Assert.AreEqual(5.5f, rb.mass, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetPropertyIntensity()
        {
            var go = new GameObject("LightTest");
            var light = go.AddComponent<Light>();
            try
            {
                var json = "{\"id\":\"oc8\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/LightTest\",\"component\":\"Light\",\"prop\":\"m_Intensity\",\"value\":\"2.5\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                Assert.AreEqual(2.5f, light.intensity, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetPropertyString()
        {
            var go = new GameObject("StringTest");
            var audioSource = go.AddComponent<AudioSource>();
            try
            {
                audioSource.playOnAwake = true;

                var json = "{\"id\":\"oc9\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/StringTest\",\"component\":\"AudioSource\",\"prop\":\"m_PlayOnAwake\",\"value\":\"false\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                Assert.IsFalse(audioSource.playOnAwake);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- SetProperty returns actual value (Phase 32a) ---

        [Test]
        public void SetProperty_ReturnsActualFloat()
        {
            var go = new GameObject("ReadbackFloat");
            go.AddComponent<Rigidbody>();
            try
            {
                var json = "{\"id\":\"rb1\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/ReadbackFloat\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"3.75\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Actual readback, not echo: "m_Mass = 3.75"
                StringAssert.Contains("m_Mass = 3.75", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetProperty_ReturnsActualBool()
        {
            var go = new GameObject("ReadbackBool");
            go.AddComponent<Rigidbody>();
            try
            {
                var json = "{\"id\":\"rb2\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/ReadbackBool\",\"component\":\"Rigidbody\",\"prop\":\"m_UseGravity\",\"value\":\"false\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Bool readback: "m_UseGravity = false"
                StringAssert.Contains("m_UseGravity = false", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetProperty_ReturnsActualEnumDisplayName()
        {
            var go = new GameObject("ReadbackEnum");
            go.AddComponent<Light>();
            try
            {
                // Pass enum by name (ValueParser requires name, not index)
                var json = "{\"id\":\"rb3\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/ReadbackEnum\",\"component\":\"Light\",\"prop\":\"m_Type\",\"value\":\"Point\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Readback returns enum display name: "m_Type = Point"
                StringAssert.Contains("m_Type = Point", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetMaterial_ReturnsShaderAndColor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ReadbackMat";
            try
            {
                var json = "{\"id\":\"rb4\",\"cmd\":\"set_material\",\"args\":{\"path\":\"/ReadbackMat\",\"color\":\"#FF0000FF\",\"shader\":\"Standard\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Returns shader name and color, not just "ok"
                StringAssert.Contains("shader=", result);
                StringAssert.Contains("color=", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void GetObjectDetail_ManyComponents()
        {
            var go = new GameObject("ManyCompObj");
            go.AddComponent<BoxCollider>();
            go.AddComponent<Rigidbody>();
            go.AddComponent<AudioSource>();
            go.AddComponent<Light>();
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();

            try
            {
                var id = TransientObjectId.GetWireValue(go);
                var json = "{\"id\":\"oc10\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":\"" + id + "\"}}";
                var result = CommandRouter.Process(json);

                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("[Transform]", result);
                StringAssert.Contains("[BoxCollider]", result);
                StringAssert.Contains("[Rigidbody]", result);
                StringAssert.Contains("[AudioSource]", result);
                StringAssert.Contains("[Light]", result);
                StringAssert.Contains("[MeshFilter]", result);
                StringAssert.Contains("[MeshRenderer]", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Component Copy Scenarios ---

        [Test]
        public void CopyRigidbodyData()
        {
            var src = new GameObject("RbSrc");
            var dst = new GameObject("RbDst");
            var srcRb = src.AddComponent<Rigidbody>();
            var dstRb = dst.AddComponent<Rigidbody>();

            srcRb.mass = 10f;
            srcRb.linearDamping = 2f;
            srcRb.angularDamping = 1f;
            srcRb.useGravity = false;

            try
            {
                // Read source
                var getComp = CommandRouter.Process("{\"id\":\"oc11a\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/RbSrc\",\"type\":\"Rigidbody\"}}");
                StringAssert.Contains("\"ok\":true", getComp);
                StringAssert.Contains("10", getComp);
                StringAssert.Contains("false", getComp);

                // Copy properties (Unity 6: m_LinearDamping, m_AngularDamping)
                CommandRouter.Process("{\"id\":\"oc11b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/RbDst\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"10\"}}");
                CommandRouter.Process("{\"id\":\"oc11c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/RbDst\",\"component\":\"Rigidbody\",\"prop\":\"m_LinearDamping\",\"value\":\"2\"}}");
                CommandRouter.Process("{\"id\":\"oc11d\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/RbDst\",\"component\":\"Rigidbody\",\"prop\":\"m_AngularDamping\",\"value\":\"1\"}}");
                CommandRouter.Process("{\"id\":\"oc11e\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/RbDst\",\"component\":\"Rigidbody\",\"prop\":\"m_UseGravity\",\"value\":\"false\"}}");

                // Verify
                Assert.AreEqual(10f, dstRb.mass, 0.01f);
                Assert.AreEqual(2f, dstRb.linearDamping, 0.01f);
                Assert.AreEqual(1f, dstRb.angularDamping, 0.01f);
                Assert.IsFalse(dstRb.useGravity);

                // Verify via get_component
                var verify = CommandRouter.Process("{\"id\":\"oc11f\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/RbDst\",\"type\":\"Rigidbody\"}}");
                StringAssert.Contains("10", verify);
                StringAssert.Contains("false", verify);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        [Test]
        public void CopyTransformData()
        {
            var src = new GameObject("TransSrc");
            var dst = new GameObject("TransDst");

            src.transform.localPosition = new Vector3(5f, 10f, 15f);
            src.transform.localScale = new Vector3(2f, 2f, 2f);

            try
            {
                // Read source
                var getComp = CommandRouter.Process("{\"id\":\"oc12a\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/TransSrc\",\"type\":\"Transform\"}}");
                StringAssert.Contains("\"ok\":true", getComp);
                StringAssert.Contains("(5, 10, 15)", getComp);
                StringAssert.Contains("(2, 2, 2)", getComp);

                // Copy properties
                CommandRouter.Process("{\"id\":\"oc12b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/TransDst\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition\",\"value\":\"(5, 10, 15)\"}}");
                CommandRouter.Process("{\"id\":\"oc12c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/TransDst\",\"component\":\"Transform\",\"prop\":\"m_LocalScale\",\"value\":\"(2, 2, 2)\"}}");

                // Verify
                Assert.AreEqual(new Vector3(5f, 10f, 15f), dst.transform.localPosition);
                Assert.AreEqual(new Vector3(2f, 2f, 2f), dst.transform.localScale);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        [Test]
        public void CopyLightData()
        {
            var src = new GameObject("LightSrc");
            var dst = new GameObject("LightDst");
            var srcLight = src.AddComponent<Light>();
            var dstLight = dst.AddComponent<Light>();

            srcLight.intensity = 5f;
            srcLight.range = 20f;
            srcLight.color = Color.red;

            try
            {
                // Read source
                var getComp = CommandRouter.Process("{\"id\":\"oc13a\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/LightSrc\",\"type\":\"Light\"}}");
                StringAssert.Contains("\"ok\":true", getComp);

                // Copy properties
                CommandRouter.Process("{\"id\":\"oc13b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/LightDst\",\"component\":\"Light\",\"prop\":\"m_Intensity\",\"value\":\"5\"}}");
                CommandRouter.Process("{\"id\":\"oc13c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/LightDst\",\"component\":\"Light\",\"prop\":\"m_Range\",\"value\":\"20\"}}");
                CommandRouter.Process("{\"id\":\"oc13d\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/LightDst\",\"component\":\"Light\",\"prop\":\"m_Color\",\"value\":\"#FF0000FF\"}}");

                // Verify
                Assert.AreEqual(5f, dstLight.intensity, 0.01f);
                Assert.AreEqual(20f, dstLight.range, 0.01f);
                Assert.AreEqual(Color.red, dstLight.color);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        // --- Manage Component ---

        [Test]
        public void AddAndRemoveComponent()
        {
            var go = new GameObject("CompManageObj");
            try
            {
                // Add BoxCollider
                var add = CommandRouter.Process("{\"id\":\"oc14a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/CompManageObj\",\"type\":\"BoxCollider\",\"action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", add);
                Assert.IsNotNull(go.GetComponent<BoxCollider>());

                // Remove BoxCollider
                var remove = CommandRouter.Process("{\"id\":\"oc14b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/CompManageObj\",\"type\":\"BoxCollider\",\"action\":\"remove\"}}");
                StringAssert.Contains("\"ok\":true", remove);
                Assert.IsNull(go.GetComponent<BoxCollider>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void AddComponent_OnObjectWithExisting()
        {
            var go = new GameObject("MultiManageObj");
            go.AddComponent<Rigidbody>();

            try
            {
                // Add BoxCollider to object that already has Rigidbody
                var add = CommandRouter.Process("{\"id\":\"oc15\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/MultiManageObj\",\"type\":\"BoxCollider\",\"action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", add);

                Assert.IsNotNull(go.GetComponent<Rigidbody>(), "Should still have Rigidbody");
                Assert.IsNotNull(go.GetComponent<BoxCollider>(), "Should have BoxCollider");

                // Verify via get_components_list
                var id = TransientObjectId.GetWireValue(go);
                var list = CommandRouter.Process("{\"id\":\"oc15b\",\"cmd\":\"get_components_list\",\"args\":{\"id\":\"" + id + "\"}}");
                StringAssert.Contains("Rigidbody", list);
                StringAssert.Contains("BoxCollider", list);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_InvalidAction_ReturnsError()
        {
            var go = new GameObject("ErrorTestObj");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Invalid action"));
                var json = "{\"id\":\"oc16\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/ErrorTestObj\",\"type\":\"BoxCollider\",\"action\":\"invalid\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("Invalid action", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_InvalidType_ReturnsError()
        {
            var go = new GameObject("ErrorTestObj2");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Component type not found"));
                var json = "{\"id\":\"oc17\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/ErrorTestObj2\",\"type\":\"NonExistentComponent\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("Component type not found", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- DeleteObject by path (Cycle 6c) ---

        [Test]
        public void DeleteObject_ByPath_Works()
        {
            var go = new GameObject("PathDeleteTarget");
            try
            {
                var json = "{\"id\":\"del1\",\"cmd\":\"delete_object\",\"args\":{\"path\":\"/PathDeleteTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNull(GameObject.Find("PathDeleteTarget"), "Object should be gone after path-delete");
            }
            finally
            {
                if (GameObject.Find("PathDeleteTarget") != null)
                    Object.DestroyImmediate(GameObject.Find("PathDeleteTarget"));
            }
        }

        [Test]
        public void DeleteObject_ByIdStillWorks()
        {
            var go = new GameObject("IdDeleteTarget");
            var id = TransientObjectId.GetWireValue(go);
            var json = "{\"id\":\"del2\",\"cmd\":\"delete_object\",\"args\":{\"id\":\"" + id + "\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNull(GameObject.Find("IdDeleteTarget"), "Back-compat: id-delete must still work");
        }

        [Test]
        public void DeleteObject_BothArgs_PrefersId()
        {
            var idTarget = new GameObject("IdPrefTarget");
            var pathTarget = new GameObject("PathPrefTarget");
            var id = TransientObjectId.GetWireValue(idTarget);
            try
            {
                var json = "{\"id\":\"del3\",\"cmd\":\"delete_object\",\"args\":{\"id\":\"" + id + "\",\"path\":\"/PathPrefTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNull(GameObject.Find("IdPrefTarget"), "id-target must be deleted");
                Assert.IsNotNull(GameObject.Find("PathPrefTarget"), "path-target must survive when id wins");
            }
            finally
            {
                var pt = GameObject.Find("PathPrefTarget");
                if (pt != null) Object.DestroyImmediate(pt);
            }
        }

        [Test]
        public void DeleteObject_NeitherArg_Throws()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("id or path"));
            var json = "{\"id\":\"del4\",\"cmd\":\"delete_object\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("id or path", result);
        }

        // --- Transform SyncTransforms (Cycle 6c) ---

        [Test]
        public void Transform_PositionWrite_SyncsPhysics()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PhysPosCube6c";
            go.transform.position = Vector3.zero;
            try
            {
                var json = "{\"id\":\"tp1\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhysPosCube6c\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition\",\"value\":\"(5,0,0)\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                // Physics.SyncTransforms should have been called — collider is at (5,0,0)
                bool hit = Physics.Raycast(new Vector3(5f, 5f, 0f), Vector3.down, 10f);
                Assert.IsTrue(hit, "Raycast should hit cube after position write + SyncTransforms");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Transform_RotationWrite_SyncsPhysics()
        {
            // A thin plane-like box rotated 90° — OverlapBox at rotated orientation should detect it
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PhysRotCube6c";
            go.transform.position = Vector3.zero;
            go.transform.localScale = new Vector3(4f, 0.1f, 4f); // thin flat box
            try
            {
                // Rotate 90° around Z → what was a flat top becomes a vertical wall
                var json = "{\"id\":\"tp2\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhysRotCube6c\",\"component\":\"Transform\",\"prop\":\"m_LocalRotation\",\"value\":\"(0,0,0.7071068,0.7071068)\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                // After sync, the collider should be oriented — overlap at center should find it
                var cols = Physics.OverlapBox(Vector3.zero, new Vector3(0.3f, 2.5f, 0.3f), Quaternion.identity);
                bool found = false;
                foreach (var c in cols) if (c.gameObject == go) { found = true; break; }
                Assert.IsTrue(found, "OverlapBox should find cube after rotation + SyncTransforms");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NonTransform_Write_DoesNotBreak()
        {
            var go = new GameObject("NonTransformWrite6c");
            go.AddComponent<Light>();
            try
            {
                var json = "{\"id\":\"tp3\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/NonTransformWrite6c\",\"component\":\"Light\",\"prop\":\"m_Range\",\"value\":\"15\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(15f, go.GetComponent<Light>().range, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- WireEvent + UnityEvent Reading ---

        [Test]
        public void UnityEvent_Empty_ShowsZeroCalls()
        {
            var go = new GameObject("EmptyEventObj");
            go.AddComponent<TestEventScript>();
            try
            {
                var result = CommandRouter.Process("{\"id\":\"we1\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/EmptyEventObj\",\"type\":\"TestEventScript\"}}");
                StringAssert.Contains("UnityEvent[0]", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WireEvent_VoidCall_AppearsInComponent()
        {
            var go = new GameObject("WireTestObj");
            go.AddComponent<TestEventScript>();
            var target = new GameObject("WireTarget");
            try
            {
                var wire = CommandRouter.Process("{\"id\":\"we2a\",\"cmd\":\"wire_event\",\"args\":{\"path\":\"/WireTestObj\",\"component\":\"TestEventScript\",\"event\":\"onActivate\",\"target\":\"/WireTarget\",\"method\":\"SetActive\",\"arg_type\":\"void\"}}");
                StringAssert.Contains("\"ok\":true", wire);

                var comp = CommandRouter.Process("{\"id\":\"we2b\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/WireTestObj\",\"type\":\"TestEventScript\"}}");
                StringAssert.Contains("UnityEvent[1]", comp);
                StringAssert.Contains("WireTarget", comp);
                StringAssert.Contains("SetActive", comp);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void WireEvent_BoolArg_AppearsInComponent()
        {
            var go = new GameObject("BoolTestObj");
            go.AddComponent<TestEventScript>();
            var target = new GameObject("BoolTarget");
            try
            {
                var wire = CommandRouter.Process("{\"id\":\"we3a\",\"cmd\":\"wire_event\",\"args\":{\"path\":\"/BoolTestObj\",\"component\":\"TestEventScript\",\"event\":\"onActivate\",\"target\":\"/BoolTarget\",\"method\":\"SetActive\",\"arg_type\":\"bool\",\"arg_value\":\"true\"}}");
                StringAssert.Contains("\"ok\":true", wire);

                var comp = CommandRouter.Process("{\"id\":\"we3b\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/BoolTestObj\",\"type\":\"TestEventScript\"}}");
                StringAssert.Contains("bool=True", comp);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void WireEvent_InvalidField_ReturnsError()
        {
            var go = new GameObject("InvalidFieldObj");
            go.AddComponent<TestEventScript>();
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not found"));
                var result = CommandRouter.Process("{\"id\":\"we4\",\"cmd\":\"wire_event\",\"args\":{\"path\":\"/InvalidFieldObj\",\"component\":\"TestEventScript\",\"event\":\"nonExistentField\",\"target\":\"/InvalidFieldObj\",\"method\":\"SetActive\",\"arg_type\":\"void\"}}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Array Expansion Tests ---

        [Test]
        public void Array_Empty_ShowsBrackets()
        {
            var go = new GameObject("ArrayEmptyObj");
            go.AddComponent<TestArrayScript>();
            try
            {
                var result = CommandRouter.Process("{\"id\":\"arr1\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/ArrayEmptyObj\",\"type\":\"TestArrayScript\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("intArray: []", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Array_SmallInts_ShowsValues()
        {
            var go = new GameObject("ArrayIntObj");
            var script = go.AddComponent<TestArrayScript>();
            script.intArray = new int[] { 1, 2, 3 };
            try
            {
                var result = CommandRouter.Process("{\"id\":\"arr2\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/ArrayIntObj\",\"type\":\"TestArrayScript\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("1, 2, 3", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Array_ObjectRefs_ShowsNames()
        {
            // MeshRenderer.m_Materials is an ObjectReference array
            var go = new GameObject("ArrayRefObj");
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.name = "TestMat";
            mr.sharedMaterials = new Material[] { mat };
            try
            {
                var result = CommandRouter.Process("{\"id\":\"arr3\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/ArrayRefObj\",\"type\":\"MeshRenderer\"}}");
                StringAssert.Contains("\"ok\":true", result);
                // m_Materials should now show the material name, not <Array[1]>
                StringAssert.DoesNotContain("<Array[", result);
                StringAssert.Contains("TestMat", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
            }
        }

        [Test]
        public void Array_LargeArray_CappedAt10()
        {
            var go = new GameObject("ArrayLargeObj");
            var script = go.AddComponent<TestArrayScript>();
            script.intArray = new int[] { 1,2,3,4,5,6,7,8,9,10,11,12,13,14,15 };
            try
            {
                var result = CommandRouter.Process("{\"id\":\"arr4\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/ArrayLargeObj\",\"type\":\"TestArrayScript\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("...+5", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WireEvent_MultipleListeners_AllVisible()
        {
            var go = new GameObject("MultiListenerObj");
            go.AddComponent<TestEventScript>();
            var target1 = new GameObject("MultiTarget1");
            var target2 = new GameObject("MultiTarget2");
            try
            {
                CommandRouter.Process("{\"id\":\"we5a\",\"cmd\":\"wire_event\",\"args\":{\"path\":\"/MultiListenerObj\",\"component\":\"TestEventScript\",\"event\":\"onActivate\",\"target\":\"/MultiTarget1\",\"method\":\"SetActive\",\"arg_type\":\"void\"}}");
                CommandRouter.Process("{\"id\":\"we5b\",\"cmd\":\"wire_event\",\"args\":{\"path\":\"/MultiListenerObj\",\"component\":\"TestEventScript\",\"event\":\"onActivate\",\"target\":\"/MultiTarget2\",\"method\":\"SetActive\",\"arg_type\":\"bool\",\"arg_value\":\"true\"}}");

                var comp = CommandRouter.Process("{\"id\":\"we5c\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/MultiListenerObj\",\"type\":\"TestEventScript\"}}");
                StringAssert.Contains("UnityEvent[2]", comp);
                StringAssert.Contains("MultiTarget1", comp);
                StringAssert.Contains("MultiTarget2", comp);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target1);
                Object.DestroyImmediate(target2);
            }
        }

        // --- Component Lifecycle (from MCPComponentLifecycleTests) ---

        [Test]
        public void FullLifecycle_AddSetRefRemove()
        {
            var objA = new GameObject("LifeA");
            var objB = new GameObject("LifeB");
            try
            {
                // Add TestRefScript
                var addJson = "{\"id\":\"lc1\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/LifeA\",\"type\":\"TestRefScript\",\"action\":\"add\"}}";
                var addResult = CommandRouter.Process(addJson);
                StringAssert.Contains("\"ok\":true", addResult);
                StringAssert.Contains("TestRefScript", addResult);
                Assert.IsNotNull(objA.GetComponent<TestRefScript>());

                // Set ref by path
                var setJson = "{\"id\":\"lc2\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/LifeA\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"/LifeB\"}}";
                var setResult = CommandRouter.Process(setJson);
                StringAssert.Contains("\"ok\":true", setResult);
                Assert.AreEqual(objB, objA.GetComponent<TestRefScript>().target);

                // Verify via get_component
                var getJson = "{\"id\":\"lc3\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/LifeA\",\"type\":\"TestRefScript\"}}";
                var getResult = CommandRouter.Process(getJson);
                StringAssert.Contains("\"ok\":true", getResult);
                StringAssert.Contains("LifeB", getResult);

                // Remove component
                var rmJson = "{\"id\":\"lc4\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/LifeA\",\"type\":\"TestRefScript\",\"action\":\"remove\"}}";
                var rmResult = CommandRouter.Process(rmJson);
                StringAssert.Contains("\"ok\":true", rmResult);
                Assert.IsNull(objA.GetComponent<TestRefScript>());
            }
            finally
            {
                Object.DestroyImmediate(objA);
                Object.DestroyImmediate(objB);
            }
        }

        [Test]
        public void SetRef_ByInstanceId()
        {
            var go = new GameObject("IdRefObj");
            var target = new GameObject("IdRefTarget");
            go.AddComponent<TestRefScript>();
            var id = TransientObjectId.GetWireValue(target);
            try
            {
                var json = "{\"id\":\"lc5\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/IdRefObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"#" + id + "\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(target, go.GetComponent<TestRefScript>().target);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SetRef_ThenClear()
        {
            var go = new GameObject("ClearRefObj");
            var target = new GameObject("ClearRefTarget");
            go.AddComponent<TestRefScript>();
            try
            {
                // Set ref
                var setJson = "{\"id\":\"lc6\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/ClearRefObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"/ClearRefTarget\"}}";
                CommandRouter.Process(setJson);
                Assert.AreEqual(target, go.GetComponent<TestRefScript>().target);

                // Clear ref
                var clearJson = "{\"id\":\"lc7\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/ClearRefObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"null\"}}";
                var clearResult = CommandRouter.Process(clearJson);
                StringAssert.Contains("\"ok\":true", clearResult);
                Assert.IsNull(go.GetComponent<TestRefScript>().target);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void RemoveNonexistent_ReturnsError()
        {
            var go = new GameObject("NoCompObj");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not on"));
            try
            {
                var json = "{\"id\":\"lc8\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/NoCompObj\",\"type\":\"Rigidbody\",\"action\":\"remove\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("not on", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Cycle 6d: Component Edge Cases ---

        [Test]
        public void GetComponent_NoSerializedFields_ReturnsExistsMessage()
        {
            var go = new GameObject("EmptyMonoObj");
            go.AddComponent<EmptyMono>();
            try
            {
                var json = "{\"id\":\"lc9\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/EmptyMonoObj\",\"type\":\"EmptyMono\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("(no serialized fields)", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetComponent_WithSerializedFields_UnchangedFormat()
        {
            var go = new GameObject("RbFieldsObj");
            go.AddComponent<Rigidbody>();
            try
            {
                var json = "{\"id\":\"lc10\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/RbFieldsObj\",\"type\":\"Rigidbody\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("m_Mass", result);
                StringAssert.DoesNotContain("(no serialized fields)", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Add_DuplicateRefused()
        {
            var go = new GameObject("DupCompObj");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already exists"));
            try
            {
                var add1 = "{\"id\":\"lc11a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/DupCompObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                CommandRouter.Process(add1);
                var add2 = "{\"id\":\"lc11b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/DupCompObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(add2);
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("already exists", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Add_DifferentTypeAfterFirst_Works()
        {
            var go = new GameObject("TwoCompObj");
            try
            {
                var addRb = "{\"id\":\"lc12a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/TwoCompObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                var r1 = CommandRouter.Process(addRb);
                StringAssert.Contains("\"ok\":true", r1);

                var addBox = "{\"id\":\"lc12b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/TwoCompObj\",\"type\":\"BoxCollider\",\"action\":\"add\"}}";
                var r2 = CommandRouter.Process(addBox);
                StringAssert.Contains("\"ok\":true", r2);

                Assert.IsNotNull(go.GetComponent<Rigidbody>());
                Assert.IsNotNull(go.GetComponent<BoxCollider>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Add_AfterRemove_Works()
        {
            var go = new GameObject("RoundTripObj");
            try
            {
                var add1 = "{\"id\":\"lc13a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RoundTripObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                CommandRouter.Process(add1);

                var remove = "{\"id\":\"lc13b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RoundTripObj\",\"type\":\"Rigidbody\",\"action\":\"remove\"}}";
                CommandRouter.Process(remove);

                var add2 = "{\"id\":\"lc13c\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RoundTripObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(add2);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNotNull(go.GetComponent<Rigidbody>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Remove_OutputFormat()
        {
            var go = new GameObject("RemoveFmtObj");
            try
            {
                var addRb = "{\"id\":\"lc14a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RemoveFmtObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                CommandRouter.Process(addRb);
                var addBox = "{\"id\":\"lc14b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RemoveFmtObj\",\"type\":\"BoxCollider\",\"action\":\"add\"}}";
                CommandRouter.Process(addBox);

                var remove = "{\"id\":\"lc14c\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/RemoveFmtObj\",\"type\":\"Rigidbody\",\"action\":\"remove\"}}";
                var result = CommandRouter.Process(remove);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Removed: Rigidbody", result);
                StringAssert.Contains("Remaining: ", result);
                StringAssert.Contains("BoxCollider", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Add_OutputFormat()
        {
            var go = new GameObject("AddFmtObj");
            try
            {
                var add = "{\"id\":\"lc15\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/AddFmtObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(add);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Added: Rigidbody", result);
                StringAssert.Contains("Components: ", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ManageComponent_Add_DuplicateError_HasFix()
        {
            var go = new GameObject("DupHintObj");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("already exists"));
            try
            {
                var add1 = "{\"id\":\"lc16a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/DupHintObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                CommandRouter.Process(add1);
                var add2 = "{\"id\":\"lc16b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/DupHintObj\",\"type\":\"Rigidbody\",\"action\":\"add\"}}";
                var result = CommandRouter.Process(add2);
                var containsFix = result.Contains("action=remove") || result.Contains("set_property");
                Assert.IsTrue(containsFix, $"Error message should hint at fix. Got: {result}");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- ResolveComponent (from MCPResolveComponentTests) ---

        [Test]
        public void ResolveComponent_ReturnsGoAndComponent()
        {
            var go = new GameObject("RCTest");
            go.AddComponent<BoxCollider>();
            try
            {
                var (resolvedGo, comp) = ObjectManager.ResolveComponent("/RCTest", "BoxCollider");
                Assert.AreEqual(go, resolvedGo);
                Assert.IsNotNull(comp);
                Assert.IsInstanceOf<BoxCollider>(comp);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResolveComponent_MissingObject_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ResolveComponent("/NoSuchObj_ABC", "BoxCollider"));
        }

        [Test]
        public void ResolveComponent_MissingComponent_ThrowsArgumentException()
        {
            var go = new GameObject("RCTestNoComp");
            try
            {
                Assert.Throws<System.ArgumentException>(() =>
                    ObjectManager.ResolveComponent("/RCTestNoComp", "Rigidbody"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
