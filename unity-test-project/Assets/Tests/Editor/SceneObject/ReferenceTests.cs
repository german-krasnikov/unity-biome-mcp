using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor;

// Inline helper script used by reference tests
public class TestRefScript : MonoBehaviour
{
    public GameObject target;
    public GameObject[] targets;
    public Transform waypoint;
}

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class ReferenceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => RefManager.Invalidate();

        // --- get_references ---

        [Test]
        public void GetReferences_BasicObjectRef_ShowsTarget()
        {
            var root = new GameObject("RefRoot");
            var child = new GameObject("RefChild");
            child.transform.SetParent(root.transform);
            var script = root.AddComponent<TestRefScript>();
            script.target = child;
            try
            {
                var json = "{\"id\":\"r1\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/RefRoot\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("TestRefScript", result);
                StringAssert.Contains("target", result);
                StringAssert.Contains("RefChild", result);
                StringAssert.Contains("child", result);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void GetReferences_NullRef_ShowsNull()
        {
            var go = new GameObject("NullRefObj");
            go.AddComponent<TestRefScript>(); // target is null by default
            try
            {
                var json = "{\"id\":\"r2\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/NullRefObj\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("null", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void GetReferences_ExternalRef_MarkedExternal()
        {
            var objA = new GameObject("ExtRefA");
            var objB = new GameObject("ExtRefB");
            var script = objA.AddComponent<TestRefScript>();
            script.target = objB;
            try
            {
                var json = "{\"id\":\"r3\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/ExtRefA\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("external", result);
            }
            finally
            {
                Object.DestroyImmediate(objA);
                Object.DestroyImmediate(objB);
            }
        }

        [Test]
        public void GetReferences_WithChildren_IncludesChildRefs()
        {
            var root = new GameObject("ChildRefRoot");
            var child = new GameObject("ChildRefChild");
            child.transform.SetParent(root.transform);
            var ext = new GameObject("ChildRefExt");
            var script = child.AddComponent<TestRefScript>();
            script.target = ext;
            try
            {
                var json = "{\"id\":\"r4\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/ChildRefRoot\",\"children\":\"true\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("ChildRefChild", result);
                StringAssert.Contains("ChildRefExt", result);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(ext);
            }
        }

        [Test]
        public void GetReferences_NoRefs_ReturnsNoReferences()
        {
            var go = new GameObject("NoRefObj");
            // bare GameObject — only Transform, which is skipped by ReferenceHelper
            try
            {
                var json = "{\"id\":\"r5\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/NoRefObj\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("no references", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void GetReferences_ArrayField_ShowsElements()
        {
            var root = new GameObject("ArrRefRoot");
            var t1 = new GameObject("ArrTarget1");
            var t2 = new GameObject("ArrTarget2");
            t1.transform.SetParent(root.transform);
            t2.transform.SetParent(root.transform);
            var script = root.AddComponent<TestRefScript>();
            script.targets = new[] { t1, t2 };
            try
            {
                var json = "{\"id\":\"r6\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/ArrRefRoot\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("targets[0]", result);
                StringAssert.Contains("targets[1]", result);
                StringAssert.Contains("ArrTarget1", result);
                StringAssert.Contains("ArrTarget2", result);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // --- find_references_to ---

        [Test]
        public void FindReferencesTo_FindsReferencer()
        {
            var target = new GameObject("FindTarget");
            var referencer = new GameObject("FindReferencer");
            var script = referencer.AddComponent<TestRefScript>();
            script.target = target;
            try
            {
                var json = "{\"id\":\"r7\",\"cmd\":\"references\",\"args\":{\"action\":\"find_to\",\"path\":\"/FindTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("FindReferencer", result);
                StringAssert.Contains("[TestRefScript].target", result);
                StringAssert.Contains("found: 1", result);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(referencer);
            }
        }

        [Test]
        public void FindReferencesTo_NoRefs_ShowsZero()
        {
            var target = new GameObject("LonelyObj");
            try
            {
                var json = "{\"id\":\"r8\",\"cmd\":\"references\",\"args\":{\"action\":\"find_to\",\"path\":\"/LonelyObj\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("found: 0", result);
            }
            finally { Object.DestroyImmediate(target); }
        }

        [Test]
        public void FindReferencesTo_MultipleReferencers()
        {
            var target = new GameObject("MultiTarget");
            var ref1 = new GameObject("MultiRef1");
            var ref2 = new GameObject("MultiRef2");
            ref1.AddComponent<TestRefScript>().target = target;
            ref2.AddComponent<TestRefScript>().target = target;
            try
            {
                var json = "{\"id\":\"r9\",\"cmd\":\"references\",\"args\":{\"action\":\"find_to\",\"path\":\"/MultiTarget\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("found: 2", result);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(ref1);
                Object.DestroyImmediate(ref2);
            }
        }

        // --- set_property ObjectReference ---

        [Test]
        public void SetProperty_ObjectReference_ByPath()
        {
            var go = new GameObject("SetRefObj");
            var target = new GameObject("SetRefTarget");
            go.AddComponent<TestRefScript>();
            try
            {
                var json = "{\"id\":\"r10\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/SetRefObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"/SetRefTarget\"}}";
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
        public void SetProperty_ObjectReference_Null()
        {
            var go = new GameObject("NullSetObj");
            var target = new GameObject("NullSetTarget");
            var script = go.AddComponent<TestRefScript>();
            script.target = target;
            try
            {
                var json = "{\"id\":\"r11\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/NullSetObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"null\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNull(go.GetComponent<TestRefScript>().target);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SetProperty_ObjectReference_ByInstanceId()
        {
            var go = new GameObject("IdSetObj");
            var target = new GameObject("IdSetTarget");
            go.AddComponent<TestRefScript>();
            var id = TransientObjectId.GetWireValue(target);
            try
            {
                var json = "{\"id\":\"r12\",\"cmd\":\"set_property\",\"args\":{\"path\":\"/IdSetObj\",\"component\":\"TestRefScript\",\"prop\":\"target\",\"value\":\"#" + id + "\"}}";
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

        // --- ComponentSerializer ObjectReference output ---

        [Test]
        public void ComponentSerializer_ObjectRef_IncludesInstanceId()
        {
            var go = new GameObject("SerRefObj");
            var target = new GameObject("SerRefTarget");
            var script = go.AddComponent<TestRefScript>();
            script.target = target;
            try
            {
                var result = ComponentSerializer.Serialize("/SerRefObj", "TestRefScript");
                Assert.IsNotNull(result);
                StringAssert.Contains("#", result);
                StringAssert.Contains(TransientObjectId.GetWireValue(target), result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }

        // --- remap_references ---

        [Test]
        public void RemapReferences_AutoRemapsChildren()
        {
            var srcRoot = new GameObject("RemapSrc");
            var srcChild = new GameObject("RemapChild");
            srcChild.transform.SetParent(srcRoot.transform);
            var dstRoot = new GameObject("RemapDst");
            var dstChild = new GameObject("RemapChild");
            dstChild.transform.SetParent(dstRoot.transform);

            // Set up target with refs pointing to source children
            var dstScript = dstRoot.AddComponent<TestRefScript>();
            dstScript.target = srcChild;
            try
            {
                var json = "{\"id\":\"r13\",\"cmd\":\"references\",\"args\":{\"action\":\"remap\",\"source\":\"/RemapSrc\",\"target\":\"/RemapDst\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("remapped: 1", result);
                // Verify the ref was remapped to dst child
                Assert.AreEqual(dstChild, dstRoot.GetComponent<TestRefScript>().target);
            }
            finally
            {
                Object.DestroyImmediate(srcRoot);
                Object.DestroyImmediate(dstRoot);
            }
        }

        [Test]
        public void RemapReferences_ExternalKeepsUnchanged()
        {
            var src = new GameObject("KeepSrc");
            var dst = new GameObject("KeepDst");
            var external = new GameObject("KeepExternal");
            var dstScript = dst.AddComponent<TestRefScript>();
            dstScript.target = external;
            try
            {
                var json = "{\"id\":\"r14\",\"cmd\":\"references\",\"args\":{\"action\":\"remap\",\"source\":\"/KeepSrc\",\"target\":\"/KeepDst\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("kept", result);
                // External ref should remain unchanged
                Assert.AreEqual(external, dst.GetComponent<TestRefScript>().target);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(dst);
                Object.DestroyImmediate(external);
            }
        }

        [Test]
        public void GetReferences_ObjectNotFound_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not found"));
            var json = "{\"id\":\"r15\",\"cmd\":\"references\",\"args\":{\"action\":\"get\",\"path\":\"/NonExistentRefObj\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("not found", result);
        }

        [Test]
        public void FindReferencesTo_ObjectNotFound_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not found"));
            var json = "{\"id\":\"r16\",\"cmd\":\"references\",\"args\":{\"action\":\"find_to\",\"path\":\"/NonExistentFindObj\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("not found", result);
        }

        // --- MCPSettings includes new tools ---

        [Test]
        public void MCPSettings_IncludesReferenceTools()
        {
            var names = MCPSettings.GetToolNames();
            Assert.Contains("references", names);
        }

        // Fix A: remap preserves Component type
        [Test]
        public void RemapReferences_PreservesComponentType_ForTransformField()
        {
            var srcRoot = new GameObject("RemapCompSrc");
            var srcChild = new GameObject("RemapCompChild");
            srcChild.transform.SetParent(srcRoot.transform);
            var dstRoot = new GameObject("RemapCompDst");
            var dstChild = new GameObject("RemapCompChild");
            dstChild.transform.SetParent(dstRoot.transform);

            // waypoint is typed Transform — after remap should be Transform, not GameObject
            var dstScript = dstRoot.AddComponent<TestRefScript>();
            dstScript.waypoint = srcChild.transform;
            try
            {
                var json = "{\"id\":\"ra1\",\"cmd\":\"references\",\"args\":{\"action\":\"remap\",\"source\":\"/RemapCompSrc\",\"target\":\"/RemapCompDst\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("remapped: 1", result);
                // Must be the Transform component, not null (which would happen if wrongly assigned as GameObject)
                Assert.IsNotNull(dstScript.waypoint, "waypoint should be remapped to dstChild.transform");
                Assert.AreEqual(dstChild.transform, dstScript.waypoint);
            }
            finally
            {
                Object.DestroyImmediate(srcRoot);
                Object.DestroyImmediate(dstRoot);
            }
        }

        // --- RefManager (from MCPRefManagerTests) ---

        [Test]
        public void Assign_ReturnsSameRefForSameObject()
        {
            var go = new GameObject("TestObj");
            try
            {
                var r1 = RefManager.Assign(go);
                var r2 = RefManager.Assign(go);
                Assert.That(r1, Is.EqualTo(r2));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Assign_ReturnsDifferentRefsForDifferentObjects()
        {
            var a = new GameObject("A");
            var b = new GameObject("B");
            try
            {
                Assert.That(RefManager.Assign(a), Is.Not.EqualTo(RefManager.Assign(b)));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void Resolve_ReturnsCorrectObject()
        {
            var go = new GameObject("C");
            try
            {
                var r = RefManager.Assign(go);
                Assert.That(RefManager.Resolve(r), Is.SameAs(go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_ReturnsNullForDestroyedObject()
        {
            var go = new GameObject("D");
            var r = RefManager.Assign(go);
            Object.DestroyImmediate(go);
            Assert.That(RefManager.Resolve(r), Is.Null);
        }

        [Test]
        public void IsRef_TrueForDollar()
        {
            Assert.That(RefManager.IsRef("$a"), Is.True);
            Assert.That(RefManager.IsRef("$ab"), Is.True);
            Assert.That(RefManager.IsRef("$zz"), Is.True);
        }

        [Test]
        public void IsRef_FalseForPath()
        {
            Assert.That(RefManager.IsRef("/Player"), Is.False);
            Assert.That(RefManager.IsRef("Player"), Is.False);
            Assert.That(RefManager.IsRef(null), Is.False);
            Assert.That(RefManager.IsRef("$toolong"), Is.False);
        }

        [Test]
        public void Invalidate_ClearsAll()
        {
            var go = new GameObject("E");
            try
            {
                var r = RefManager.Assign(go);
                RefManager.Invalidate();
                Assert.That(RefManager.Resolve(r), Is.Null);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Prune_RemovesStale()
        {
            var go = new GameObject("F");
            var r = RefManager.Assign(go);
            Object.DestroyImmediate(go);
            RefManager.Prune();
            // After prune, re-assigning a new GO should get a fresh ref
            var go2 = new GameObject("F2");
            try
            {
                var r2 = RefManager.Assign(go2);
                // just verify it works without throwing
                Assert.That(r2, Does.StartWith("$"));
            }
            finally { Object.DestroyImmediate(go2); }
        }

        [Test]
        public void GenerateRef_Sequence()
        {
            // Assign 28 GOs and check the refs
            var gos = new GameObject[28];
            for (int i = 0; i < 28; i++) gos[i] = new GameObject($"Gen{i}");
            try
            {
                var refs = new string[28];
                for (int i = 0; i < 28; i++) refs[i] = RefManager.Assign(gos[i]);

                Assert.That(refs[0], Is.EqualTo("$a"));
                Assert.That(refs[25], Is.EqualTo("$z"));
                Assert.That(refs[26], Is.EqualTo("$aa"));
                Assert.That(refs[27], Is.EqualTo("$ab"));
            }
            finally { foreach (var g in gos) Object.DestroyImmediate(g); }
        }

        [Test]
        public void FindObject_ResolvesRef()
        {
            var go = new GameObject("RefTarget");
            try
            {
                var r = RefManager.Assign(go);
                var resolved = ComponentSerializer.FindObject(r);
                Assert.That(resolved, Is.SameAs(go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FindObject_FallsBackToPath()
        {
            var go = new GameObject("PathFallback");
            try
            {
                var resolved = ComponentSerializer.FindObject("PathFallback");
                Assert.That(resolved, Is.SameAs(go));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void HierarchyOutput_ContainsRefs()
        {
            var go = new GameObject("RefInHierarchy");
            try
            {
                var output = HierarchySerializer.Serialize();
                Assert.That(output, Does.Contain("$"));
                // The specific object should have a ref
                Assert.That(output, Does.Match(@"\$[a-z]{1,2}"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
