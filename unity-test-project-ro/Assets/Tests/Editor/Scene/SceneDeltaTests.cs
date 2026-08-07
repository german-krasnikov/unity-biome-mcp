using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;
using System.Reflection;

namespace UnityMCP.TestProject.Scene
{
    public class SceneDeltaTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            // Clear changes before each test via reflection
            var field = typeof(ChangeWatcher).GetField("_changes",
                BindingFlags.Static | BindingFlags.NonPublic);
            var list = field.GetValue(null) as System.Collections.Generic.List<string>;
            list.Clear();
        }

        // --- set_property_delta ---

        [Test]
        public void SetPropertyDelta_Float_AddsValue()
        {
            var go = new GameObject("DeltaFloat");
            go.AddComponent<Rigidbody>();
            try
            {
                // Set baseline mass = 1
                CommandRouter.Process("{\"id\":\"d1\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/DeltaFloat\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"1\"}}");

                var result = CommandRouter.Process("{\"id\":\"d2\",\"cmd\":\"set_property_delta\",\"args\":{\"path\":\"/DeltaFloat\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"delta\":\"+5\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(6f, go.GetComponent<Rigidbody>().mass, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyDelta_NegativeDelta_SubtractsValue()
        {
            var go = new GameObject("DeltaNeg");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"d3\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/DeltaNeg\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"10\"}}");

                var result = CommandRouter.Process("{\"id\":\"d4\",\"cmd\":\"set_property_delta\",\"args\":{\"path\":\"/DeltaNeg\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"delta\":\"-3\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(7f, go.GetComponent<Rigidbody>().mass, 0.01f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyDelta_Vector3_AddsDelta()
        {
            var go = new GameObject("DeltaVec");
            go.transform.localPosition = Vector3.zero;
            try
            {
                var result = CommandRouter.Process("{\"id\":\"d5\",\"cmd\":\"set_property_delta\",\"args\":{\"path\":\"/DeltaVec\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition\",\"delta\":\"(+1,2,0)\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(new Vector3(1f, 2f, 0f), go.transform.localPosition);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetPropertyDelta_ReturnsOldArrowNew()
        {
            var go = new GameObject("DeltaArrow");
            go.AddComponent<Rigidbody>();
            try
            {
                CommandRouter.Process("{\"id\":\"d6\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/DeltaArrow\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"value\":\"2\"}}");

                var result = CommandRouter.Process("{\"id\":\"d7\",\"cmd\":\"set_property_delta\",\"args\":{\"path\":\"/DeltaArrow\",\"component\":\"Rigidbody\",\"prop\":\"m_Mass\",\"delta\":\"+3\"}}");
                // data should contain "→" and the new value
                StringAssert.Contains("→", result);
                StringAssert.Contains("5", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- scene_diff ---

        [Test]
        public void SceneDiff_FirstCall_SavesSnapshot()
        {
            // Reset state first by calling scene_diff twice
            CommandRouter.Process("{\"id\":\"sd0\",\"cmd\":\"scene_diff\",\"args\":{}}");
            var result = CommandRouter.Process("{\"id\":\"sd1\",\"cmd\":\"scene_diff\",\"args\":{}}");
            StringAssert.Contains("\"ok\":true", result);
            // Second call without changes returns NO CHANGES
            StringAssert.Contains("NO CHANGES", result);
        }

        [Test]
        public void SceneDiff_AddedObject_ShowsPlus()
        {
            // Take initial snapshot
            CommandRouter.Process("{\"id\":\"sd2\",\"cmd\":\"scene_diff\",\"args\":{}}");

            var go = new GameObject("SceneDiffTarget");
            try
            {
                var result = CommandRouter.Process("{\"id\":\"sd3\",\"cmd\":\"scene_diff\",\"args\":{}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("+", result);
                StringAssert.Contains("SceneDiffTarget", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- ChangeWatcher ---

        [Test]
        public void GetChanges_NoChanges_ReturnsNoChanges()
        {
            var result = ChangeWatcher.GetChanges();
            Assert.AreEqual("NO_CHANGES", result);
        }

        [Test]
        public void GetChanges_ClearsAfterRead()
        {
            // Manually inject a change
            var field = typeof(ChangeWatcher).GetField("_changes",
                BindingFlags.Static | BindingFlags.NonPublic);
            var list = field.GetValue(null) as System.Collections.Generic.List<string>;
            list.Add("12:00:00 TEST_CHANGE");

            var result = ChangeWatcher.GetChanges(clear: true);
            Assert.That(result, Does.Contain("TEST_CHANGE"));
            Assert.AreEqual("NO_CHANGES", ChangeWatcher.GetChanges());
        }

        [Test]
        public void GetChanges_ClearFalse_KeepsChanges()
        {
            var field = typeof(ChangeWatcher).GetField("_changes",
                BindingFlags.Static | BindingFlags.NonPublic);
            var list = field.GetValue(null) as System.Collections.Generic.List<string>;
            list.Add("12:00:00 HIERARCHY_CHANGED");

            ChangeWatcher.GetChanges(clear: false);
            var result2 = ChangeWatcher.GetChanges(clear: false);
            Assert.That(result2, Does.Contain("HIERARCHY_CHANGED"));
        }
    }
}
