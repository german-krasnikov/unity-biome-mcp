using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Scene
{
    [TestFixture]
    public class PhysicsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void RigidbodyAdd_DefaultValues()
        {
            var go = new GameObject("PhRbDefault");
            try
            {
                var result = CommandRouter.Process("{\"id\":\"ph1\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/PhRbDefault\",\"type\":\"Rigidbody\",\"action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", result);

                var rb = go.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.IsTrue(rb.useGravity);
                Assert.AreEqual(1f, rb.mass, 0.01f);
                Assert.IsFalse(rb.isKinematic);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RigidbodySetMassAndDrag()
        {
            var go = new GameObject("PhRbDrag");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph2a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbDrag\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"50\"}}");
                CommandRouter.Process("{\"id\":\"ph2b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbDrag\",\"component\":\"Rigidbody\",\"prop\":\"m_LinearDamping\",\"value\":\"2\"}}");
                CommandRouter.Process("{\"id\":\"ph2c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbDrag\",\"component\":\"Rigidbody\",\"prop\":\"m_AngularDamping\",\"value\":\"5\"}}");

                var rb = go.GetComponent<Rigidbody>();
                Assert.AreEqual(50f, rb.mass, 0.01f);
                Assert.AreEqual(2f, rb.linearDamping, 0.01f);
                Assert.AreEqual(5f, rb.angularDamping, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RigidbodyKinematic()
        {
            var go = new GameObject("PhRbKinematic");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph3a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbKinematic\",\"component\":\"Rigidbody\",\"prop\":\"m_IsKinematic\",\"value\":\"true\"}}");
                CommandRouter.Process("{\"id\":\"ph3b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbKinematic\",\"component\":\"Rigidbody\",\"prop\":\"m_UseGravity\",\"value\":\"false\"}}");

                var rb = go.GetComponent<Rigidbody>();
                Assert.IsTrue(rb.isKinematic);
                Assert.IsFalse(rb.useGravity);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RigidbodyFreezeRotation()
        {
            var go = new GameObject("PhRbFreeze");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph4\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbFreeze\",\"component\":\"Rigidbody\",\"prop\":\"m_Constraints\",\"value\":\"112\"}}");

                var rb = go.GetComponent<Rigidbody>();
                Assert.AreEqual(RigidbodyConstraints.FreezeRotation, rb.constraints);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RigidbodyCollisionDetectionCCD()
        {
            var go = new GameObject("PhRbCCD");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph5\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhRbCCD\",\"component\":\"Rigidbody\",\"prop\":\"m_CollisionDetection\",\"value\":\"Continuous Dynamic\"}}");

                var rb = go.GetComponent<Rigidbody>();
                Assert.AreEqual(CollisionDetectionMode.ContinuousDynamic, rb.collisionDetectionMode);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BoxColliderSetSizeCenterTrigger()
        {
            var go = new GameObject("PhBox");
            go.AddComponent<BoxCollider>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph6a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhBox\",\"component\":\"BoxCollider\",\"prop\":\"m_Size\",\"value\":\"(2,3,4)\"}}");
                CommandRouter.Process("{\"id\":\"ph6b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhBox\",\"component\":\"BoxCollider\",\"prop\":\"m_Center\",\"value\":\"(1,0.5,0)\"}}");
                CommandRouter.Process("{\"id\":\"ph6c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhBox\",\"component\":\"BoxCollider\",\"prop\":\"m_IsTrigger\",\"value\":\"true\"}}");

                var bc = go.GetComponent<BoxCollider>();
                Assert.AreEqual(new Vector3(2f, 3f, 4f), bc.size);
                Assert.AreEqual(new Vector3(1f, 0.5f, 0f), bc.center);
                Assert.IsTrue(bc.isTrigger);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SphereColliderSetRadiusTrigger()
        {
            var go = new GameObject("PhSphere");
            go.AddComponent<SphereCollider>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph7a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSphere\",\"component\":\"SphereCollider\",\"prop\":\"m_Radius\",\"value\":\"3\"}}");
                CommandRouter.Process("{\"id\":\"ph7b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSphere\",\"component\":\"SphereCollider\",\"prop\":\"m_IsTrigger\",\"value\":\"true\"}}");

                var sc = go.GetComponent<SphereCollider>();
                Assert.AreEqual(3f, sc.radius, 0.01f);
                Assert.IsTrue(sc.isTrigger);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CapsuleColliderSetProperties()
        {
            var go = new GameObject("PhCapsule");
            go.AddComponent<CapsuleCollider>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph8a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCapsule\",\"component\":\"CapsuleCollider\",\"prop\":\"m_Height\",\"value\":\"2\"}}");
                CommandRouter.Process("{\"id\":\"ph8b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCapsule\",\"component\":\"CapsuleCollider\",\"prop\":\"m_Radius\",\"value\":\"0.4\"}}");
                CommandRouter.Process("{\"id\":\"ph8c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCapsule\",\"component\":\"CapsuleCollider\",\"prop\":\"m_Center\",\"value\":\"(0,1,0)\"}}");

                var cc = go.GetComponent<CapsuleCollider>();
                Assert.AreEqual(2f, cc.height, 0.01f);
                Assert.AreEqual(0.4f, cc.radius, 0.01f);
                Assert.AreEqual(new Vector3(0f, 1f, 0f), cc.center);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MeshColliderConvex()
        {
            var go = new GameObject("PhMesh");
            go.AddComponent<MeshCollider>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph9\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhMesh\",\"component\":\"MeshCollider\",\"prop\":\"m_Convex\",\"value\":\"true\"}}");

                var mc = go.GetComponent<MeshCollider>();
                Assert.IsTrue(mc.convex);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CharacterControllerFullSetup()
        {
            var go = new GameObject("PhCC");
            go.AddComponent<CharacterController>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph10a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCC\",\"component\":\"CharacterController\",\"prop\":\"m_Height\",\"value\":\"2\"}}");
                CommandRouter.Process("{\"id\":\"ph10b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCC\",\"component\":\"CharacterController\",\"prop\":\"m_Radius\",\"value\":\"0.4\"}}");
                CommandRouter.Process("{\"id\":\"ph10c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCC\",\"component\":\"CharacterController\",\"prop\":\"m_Center\",\"value\":\"(0,1,0)\"}}");
                CommandRouter.Process("{\"id\":\"ph10d\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCC\",\"component\":\"CharacterController\",\"prop\":\"m_StepOffset\",\"value\":\"0.3\"}}");
                CommandRouter.Process("{\"id\":\"ph10e\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCC\",\"component\":\"CharacterController\",\"prop\":\"m_SlopeLimit\",\"value\":\"45\"}}");

                var cc = go.GetComponent<CharacterController>();
                Assert.AreEqual(2f, cc.height, 0.01f);
                Assert.AreEqual(0.4f, cc.radius, 0.01f);
                Assert.AreEqual(new Vector3(0f, 1f, 0f), cc.center);
                Assert.AreEqual(0.3f, cc.stepOffset, 0.01f);
                Assert.AreEqual(45f, cc.slopeLimit, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void CompoundColliders()
        {
            var root = new GameObject("PhCompRoot");
            var childA = new GameObject("PhCompChildA");
            var childB = new GameObject("PhCompChildB");
            childA.transform.SetParent(root.transform);
            childB.transform.SetParent(root.transform);
            try
            {
                CommandRouter.Process("{\"id\":\"ph11a\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/PhCompRoot\",\"type\":\"Rigidbody\",\"action\":\"add\"}}");
                CommandRouter.Process("{\"id\":\"ph11b\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/PhCompRoot/PhCompChildA\",\"type\":\"BoxCollider\",\"action\":\"add\"}}");
                CommandRouter.Process("{\"id\":\"ph11c\",\"cmd\":\"manage_component\",\"args\":{\"path\":\"/PhCompRoot/PhCompChildB\",\"type\":\"BoxCollider\",\"action\":\"add\"}}");
                CommandRouter.Process("{\"id\":\"ph11d\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCompRoot/PhCompChildA\",\"component\":\"BoxCollider\",\"prop\":\"m_Size\",\"value\":\"(1,1,1)\"}}");
                CommandRouter.Process("{\"id\":\"ph11e\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhCompRoot/PhCompChildB\",\"component\":\"BoxCollider\",\"prop\":\"m_Size\",\"value\":\"(2,2,2)\"}}");

                Assert.IsNotNull(root.GetComponent<Rigidbody>());
                Assert.IsNotNull(childA.GetComponent<BoxCollider>());
                Assert.IsNotNull(childB.GetComponent<BoxCollider>());
                Assert.IsNull(childA.GetComponent<Rigidbody>());
                Assert.IsNull(childB.GetComponent<Rigidbody>());

                var find = CommandRouter.Process("{\"id\":\"ph11f\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"Rigidbody\"}}");
                StringAssert.Contains("PhCompRoot", find);
                StringAssert.DoesNotContain("PhCompChildA", find);
                StringAssert.DoesNotContain("PhCompChildB", find);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HingeJointSetup()
        {
            var go = new GameObject("PhHinge");
            go.AddComponent<Rigidbody>();
            go.AddComponent<HingeJoint>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph12a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhHinge\",\"component\":\"HingeJoint\",\"prop\":\"m_Axis\",\"value\":\"(0,1,0)\"}}");
                CommandRouter.Process("{\"id\":\"ph12b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhHinge\",\"component\":\"HingeJoint\",\"prop\":\"m_UseLimits\",\"value\":\"true\"}}");

                var hj = go.GetComponent<HingeJoint>();
                Assert.AreEqual(new Vector3(0f, 1f, 0f), hj.axis);
                Assert.IsTrue(hj.useLimits);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SpringJointSetup()
        {
            var go = new GameObject("PhSpring");
            go.AddComponent<Rigidbody>();
            go.AddComponent<SpringJoint>();
            try
            {
                CommandRouter.Process("{\"id\":\"ph13a\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSpring\",\"component\":\"SpringJoint\",\"prop\":\"m_Spring\",\"value\":\"50\"}}");
                CommandRouter.Process("{\"id\":\"ph13b\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSpring\",\"component\":\"SpringJoint\",\"prop\":\"m_Damper\",\"value\":\"5\"}}");
                CommandRouter.Process("{\"id\":\"ph13c\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSpring\",\"component\":\"SpringJoint\",\"prop\":\"m_MinDistance\",\"value\":\"0\"}}");
                CommandRouter.Process("{\"id\":\"ph13d\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/PhSpring\",\"component\":\"SpringJoint\",\"prop\":\"m_MaxDistance\",\"value\":\"3\"}}");

                var sj = go.GetComponent<SpringJoint>();
                Assert.AreEqual(50f, sj.spring, 0.01f);
                Assert.AreEqual(5f, sj.damper, 0.01f);
                Assert.AreEqual(0f, sj.minDistance, 0.01f);
                Assert.AreEqual(3f, sj.maxDistance, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FixedJointAdd()
        {
            var go = new GameObject("PhFixed");
            go.AddComponent<Rigidbody>();
            go.AddComponent<FixedJoint>();
            try
            {
                var id = TransientObjectId.GetWireValue(go);
                var list = CommandRouter.Process("{\"id\":\"ph14\",\"cmd\":\"get_components_list\",\"args\":{\"id\":\"" + id + "\"}}");
                StringAssert.Contains("\"ok\":true", list);
                StringAssert.Contains("Rigidbody", list);
                StringAssert.Contains("FixedJoint", list);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void StaticColliderPattern()
        {
            var go = new GameObject("PhStatic");
            go.AddComponent<BoxCollider>();
            try
            {
                Assert.IsNull(go.GetComponent<Rigidbody>());

                var find = CommandRouter.Process("{\"id\":\"ph15\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"Rigidbody\"}}");
                StringAssert.DoesNotContain("PhStatic", find);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FindObjectsByPhysicsComponent()
        {
            var goBox = new GameObject("PhFindBox");
            var goSphere = new GameObject("PhFindSphere");
            var goRbBox = new GameObject("PhFindRbBox");
            goBox.AddComponent<BoxCollider>();
            goSphere.AddComponent<SphereCollider>();
            goRbBox.AddComponent<Rigidbody>();
            goRbBox.AddComponent<BoxCollider>();
            try
            {
                var findRb = CommandRouter.Process("{\"id\":\"ph16a\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"Rigidbody\"}}");
                StringAssert.Contains("PhFindRbBox", findRb);
                StringAssert.DoesNotContain("PhFindBox", findRb);
                StringAssert.DoesNotContain("PhFindSphere", findRb);

                var findBox = CommandRouter.Process("{\"id\":\"ph16b\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"BoxCollider\"}}");
                StringAssert.Contains("PhFindBox", findBox);
                StringAssert.Contains("PhFindRbBox", findBox);
                StringAssert.DoesNotContain("PhFindSphere", findBox);
            }
            finally
            {
                Object.DestroyImmediate(goBox);
                Object.DestroyImmediate(goSphere);
                Object.DestroyImmediate(goRbBox);
            }
        }

        [Test]
        public void BatchPhysicsSetup()
        {
            var json = "{\"id\":\"ph17\",\"cmd\":\"batch\",\"args\":{\"commands\":\"create_object name=BatchPhysObj primitive=Cube\\nmanage_component path=/BatchPhysObj type=Rigidbody action=add\\nset_property path=/BatchPhysObj component=Rigidbody prop=m_Mass value=10\\nset_property path=/BatchPhysObj component=Rigidbody prop=m_UseGravity value=false\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);

            var go = GameObject.Find("BatchPhysObj");
            try
            {
                Assert.IsNotNull(go);
                var rb = go.GetComponent<Rigidbody>();
                Assert.IsNotNull(rb);
                Assert.AreEqual(10f, rb.mass, 0.01f);
                Assert.IsFalse(rb.useGravity);
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
            }
        }
    }

    [TestFixture]
    public class ColliderCheckerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = TrackOwnedObject(new GameObject("ColliderTestRoot"));
        }

        [Test]
        public void ColliderChecker_TriggerNoRb_ReportsIssue()
        {
            var col = _root.AddComponent<BoxCollider>();
            col.isTrigger = true;

            var result = ColliderChecker.Check();

            StringAssert.Contains("TRIGGER_NO_RB", result);
            StringAssert.Contains("ColliderTestRoot", result);
        }

        [Test]
        public void ColliderChecker_TriggerWithRb_NoIssue()
        {
            var col = _root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            _root.AddComponent<Rigidbody>();

            var result = ColliderChecker.CheckPath("ColliderTestRoot");

            StringAssert.StartsWith("OK", result);
        }

        [Test]
        public void ColliderChecker_NegativeScale_ReportsIssue()
        {
            _root.AddComponent<BoxCollider>();
            _root.transform.localScale = new Vector3(-1, 1, 1);

            var result = ColliderChecker.Check();

            StringAssert.Contains("NEG_SCALE", result);
            StringAssert.Contains("ColliderTestRoot", result);
        }

        [Test]
        public void ColliderChecker_Clean_ReturnsOK()
        {
            var result = ColliderChecker.CheckPath("ColliderTestRoot");
            StringAssert.StartsWith("OK", result);
        }

        [Test]
        public void ColliderChecker_MicroBoxCollider_ReportsIssue()
        {
            var bc = _root.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.001f, 0.001f, 0.001f);

            var result = ColliderChecker.CheckPath("ColliderTestRoot");

            StringAssert.Contains("MICRO_COL", result);
        }

        [Test]
        public void ColliderChecker_MicroSphereCollider_ReportsIssue()
        {
            var sc = _root.AddComponent<SphereCollider>();
            sc.radius = 0.001f;

            var result = ColliderChecker.CheckPath("ColliderTestRoot");

            StringAssert.Contains("MICRO_COL", result);
        }
    }
}
