// v0.25.10
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Integration
{
    [TestFixture]
    public class PluginIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Ping_ReturnsPong()
        {
            var json = "{\"id\":\"t001\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("pong", result);
        }

        [Test]
        public void GetVersion_ReturnsNumber()
        {
            // C7: get_version is served by MCPServer fast-path, not CommandRouter.
            // Test the fast-path logic directly: stamp is empty or non-empty → version always starts with "1.0".
            var stamp = SyncHelper.CurrentDomainStamp;
            var ver = string.IsNullOrEmpty(stamp) ? "1.0" : $"1.0|stamp:{stamp}";
            StringAssert.StartsWith("1.0", ver);
        }

        [Test]
        public void GetHierarchy_ReturnsSceneTree()
        {
            var json = "{\"id\":\"t003\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":2}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
        }

        [Test]
        public void UnknownCommand_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("not registered"));
            var json = "{\"id\":\"t004\",\"cmd\":\"nonexistent\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("not registered", result);
        }

        [Test]
        public void CreateAndDeleteObject()
        {
            var create = "{\"id\":\"t005\",\"cmd\":\"create_object\",\"args\":{\"name\":\"TestMCPObj\"}}";
            var result = CommandRouter.Process(create);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("TestMCPObj", result);

            var obj = GameObject.Find("TestMCPObj");
            Assert.IsNotNull(obj);
            var id = TransientObjectId.GetWireValue(obj);

            var delete = "{\"id\":\"t006\",\"cmd\":\"delete_object\",\"args\":{\"id\":\"" + id + "\"}}";
            result = CommandRouter.Process(delete);
            StringAssert.Contains("\"ok\":true", result);
        }

        [Test]
        public void GetObjectDetail_ReturnsAllComponents()
        {
            var go = new GameObject("DetailTestObj");
            go.AddComponent<BoxCollider>();
            var id = TransientObjectId.GetWireValue(go);
            try
            {
                var json = "{\"id\":\"t007\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":\"" + id + "\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("DetailTestObj", result);
                StringAssert.Contains("[Transform]", result);
                StringAssert.Contains("[BoxCollider]", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetObjectDetail_NotFound_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Object not found"));
            var json = "{\"id\":\"t008\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":999999}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("Object not found", result);
        }

        [Test]
        public void NestedHierarchy_WithComponents_FullRoundtrip()
        {
            var root = new GameObject("NestRoot");
            var childA = new GameObject("ChildA");
            var childB = new GameObject("ChildB");
            var grand = new GameObject("GrandChild");

            childA.transform.SetParent(root.transform);
            childB.transform.SetParent(root.transform);
            grand.transform.SetParent(childA.transform);

            root.AddComponent<Rigidbody>();
            childA.AddComponent<BoxCollider>();
            childA.AddComponent<AudioSource>();
            childB.AddComponent<SphereCollider>();
            grand.AddComponent<Light>();
            grand.AddComponent<MeshFilter>();

            try
            {
                var r = CommandRouter.Process("{\"id\":\"n1\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":99}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("NestRoot", r);
                StringAssert.Contains("ChildA", r);
                StringAssert.Contains("ChildB", r);
                StringAssert.Contains("GrandChild", r);

                var rootId = TransientObjectId.GetWireValue(root);
                r = CommandRouter.Process("{\"id\":\"n2\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":\"" + rootId + "\"}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("[Rigidbody]", r);
                StringAssert.Contains("name: NestRoot", r);

                var grandId = TransientObjectId.GetWireValue(grand);
                r = CommandRouter.Process("{\"id\":\"n3\",\"cmd\":\"get_object_detail\",\"args\":{\"id\":\"" + grandId + "\"}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("[Light]", r);
                StringAssert.Contains("[MeshFilter]", r);
                StringAssert.Contains("name: GrandChild", r);

                var childAId = TransientObjectId.GetWireValue(childA);
                r = CommandRouter.Process("{\"id\":\"n4\",\"cmd\":\"get_components_list\",\"args\":{\"id\":\"" + childAId + "\"}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("BoxCollider", r);
                StringAssert.Contains("AudioSource", r);

                r = CommandRouter.Process("{\"id\":\"n5\",\"cmd\":\"find_objects\",\"args\":{\"component\":\"Light\"}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("GrandChild", r);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CopyComponentData_BoxCollider()
        {
            var src = new GameObject("CopySrc");
            var dst = new GameObject("CopyDst");
            src.AddComponent<BoxCollider>();
            dst.AddComponent<BoxCollider>();

            var srcCollider = src.GetComponent<BoxCollider>();
            srcCollider.center = new Vector3(1.5f, 2.5f, 3.5f);
            srcCollider.size = new Vector3(4f, 5f, 6f);
            srcCollider.isTrigger = true;

            try
            {
                var r = CommandRouter.Process(
                    "{\"id\":\"c1\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/CopySrc\",\"type\":\"BoxCollider\"}}");
                StringAssert.Contains("\"ok\":true", r);
                StringAssert.Contains("(1.5, 2.5, 3.5)", r);
                StringAssert.Contains("(4, 5, 6)", r);

                CommandRouter.Process(
                    "{\"id\":\"c2\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/CopyDst\",\"component\":\"BoxCollider\",\"prop\":\"m_Center\",\"value\":\"(1.5, 2.5, 3.5)\"}}");
                CommandRouter.Process(
                    "{\"id\":\"c3\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/CopyDst\",\"component\":\"BoxCollider\",\"prop\":\"m_Size\",\"value\":\"(4, 5, 6)\"}}");
                CommandRouter.Process(
                    "{\"id\":\"c4\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/CopyDst\",\"component\":\"BoxCollider\",\"prop\":\"m_IsTrigger\",\"value\":\"true\"}}");

                var dstCollider = dst.GetComponent<BoxCollider>();
                Assert.AreEqual(new Vector3(1.5f, 2.5f, 3.5f), dstCollider.center, "center mismatch");
                Assert.AreEqual(new Vector3(4f, 5f, 6f), dstCollider.size, "size mismatch");
                Assert.IsTrue(dstCollider.isTrigger, "isTrigger should be true");

                r = CommandRouter.Process(
                    "{\"id\":\"c5\",\"cmd\":\"get_component\",\"args\":{\"path\":\"/CopyDst\",\"type\":\"BoxCollider\"}}");
                StringAssert.Contains("(1.5, 2.5, 3.5)", r);
                StringAssert.Contains("(4, 5, 6)", r);
                StringAssert.Contains("true", r);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
            }
        }

        // --- Scene Management ---

        [Test]
        public void NewScene_ReturnsSceneName()
        {
            var json = "{\"id\":\"s1\",\"cmd\":\"scene\",\"args\":{\"action\":\"new\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
        }

        [Test]
        public void DiscardChanges_ReturnsResult()
        {
            var json = "{\"id\":\"s2\",\"cmd\":\"scene\",\"args\":{\"action\":\"discard\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("reloaded", result);
        }

        [Test]
        public void OpenScene_MissingPath_ReturnsError()
        {
            // ROI reliability sprint: string errors became exceptions, and CommandRouter now
            // logs via ErrorClassifier.FormatError, which prefixes the category (ArgumentException
            // → "VALIDATION:") ahead of the original message.
            LogAssert.Expect(LogType.Warning, "[MCP] VALIDATION: path required");
            var json = "{\"id\":\"s3\",\"cmd\":\"scene\",\"args\":{\"action\":\"open\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("path required", result);
        }

        // --- Hierarchy Components Parameter ---

        [Test]
        public void Hierarchy_Default_OmitsComponents()
        {
            var go = new GameObject("CompParamObj");
            go.AddComponent<BoxCollider>();
            try
            {
                var result = HierarchySerializer.Serialize(depth: 99, root: "/CompParamObj");
                StringAssert.Contains("CompParamObj", result);
                Assert.IsFalse(result.Contains("[BoxCollider]"), "Default should omit component list");
                Assert.IsFalse(result.Contains("["), "Default should have no brackets");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hierarchy_ComponentsTrue_IncludesComponents()
        {
            var go = new GameObject("CompParamObj2");
            go.AddComponent<BoxCollider>();
            try
            {
                var result = HierarchySerializer.Serialize(depth: 99, root: "/CompParamObj2", components: true);
                StringAssert.Contains("CompParamObj2", result);
                StringAssert.Contains("[BoxCollider]", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Hierarchy_ComponentsViaCommand_Works()
        {
            var go = new GameObject("CompCmdObj");
            go.AddComponent<Light>();
            try
            {
                var r1 = CommandRouter.Process("{\"id\":\"hc1\",\"cmd\":\"get_hierarchy\",\"args\":{\"root\":\"/CompCmdObj\"}}");
                StringAssert.Contains("\"ok\":true", r1);
                Assert.IsFalse(r1.Contains("[Light]"), "Without components param should omit component list");

                var r2 = CommandRouter.Process("{\"id\":\"hc2\",\"cmd\":\"get_hierarchy\",\"args\":{\"root\":\"/CompCmdObj\",\"components\":\"true\"}}");
                StringAssert.Contains("\"ok\":true", r2);
                StringAssert.Contains("[Light]", r2);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- Hierarchy Safety Cap ---

        [Test]
        public void Hierarchy_SmallScene_NotTruncated()
        {
            var result = HierarchySerializer.Serialize(depth: 99);
            Assert.IsFalse(result.Contains("truncated"), "Small scene should not be truncated");
        }

        [Test]
        public void Hierarchy_MaxNodes_Constant()
        {
            Assert.AreEqual(3000, HierarchySerializer.MAX_NODES);
        }

        [Test]
        public void Hierarchy_LargeScene_Truncates()
        {
            var root = new GameObject("TruncateTestRoot");
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    var child = new GameObject($"Child{i}");
                    child.transform.SetParent(root.transform);
                    for (int j = 0; j < 50; j++)
                    {
                        var grandchild = new GameObject($"Grandchild{i}_{j}");
                        grandchild.transform.SetParent(child.transform);
                    }
                }

                var result = HierarchySerializer.Serialize(depth: 99, root: "/TruncateTestRoot");
                StringAssert.Contains("truncated at 3000 nodes", result);
                StringAssert.Contains("filter/root/depth", result);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- MCPServer Public API ---

        [Test]
        public void MCPServer_IsRunning_ReturnsTrue()
        {
            if (UnityEngine.Application.isBatchMode)
                Assert.Ignore("MCPServer does not start in headless batchmode CI");
            Assert.IsTrue(MCPServer.IsRunning);
        }

        [Test]
        public void MCPServer_IsClientConnected_Accessible()
        {
            var connected = MCPServer.IsClientConnected;
            Assert.IsInstanceOf<bool>(connected);
        }

        [Test]
        public void MCPServer_ServerPort_IsValidPortNumber()
        {
            var port = MCPServer.ServerPort;
            Assert.IsTrue(port >= 1024 && port <= 65535,
                $"ServerPort must be in valid unprivileged range [1024,65535], got {port}");
        }

    }
}
