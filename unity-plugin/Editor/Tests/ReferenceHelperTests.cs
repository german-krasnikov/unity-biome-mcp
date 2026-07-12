// NUnit tests for ReferenceHelper — CS2.test.3 + CS2.arch.2 (ClassifyRef multi-scene regression).
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ReferenceHelperTests : MultiSceneTestBase
    {
        // ── Single-scene ClassifyRef via GetReferences ────────────────────────

        [Test]
        public void GetReferences_DoesNotThrow_ReturnsNonNull()
        {
            var parent = new GameObject("RH_Parent");
            var child = new GameObject("RH_Child");
            child.transform.SetParent(parent.transform);
            _toDestroy.Add(parent);
            _toDestroy.Add(child);

            // Give parent a component that references the child
            var light = parent.AddComponent<Light>();

            // Use GetReferences on parent — should list the child light's own component refs
            // We simply verify the API doesn't throw and returns something
            var result = ReferenceHelper.GetReferences("/RH_Parent", false, 1);
            Assert.IsNotNull(result);
        }

        [Test]
        public void FindReferencesTo_NoIncomingRefs_ContainsFoundLabel()
        {
            var go = new GameObject("RH_Target");
            _toDestroy.Add(go);

            // No refs point to it — result contains "found: 0"
            var result = ReferenceHelper.FindReferencesTo("/RH_Target");
            StringAssert.Contains("found: 0", result);
        }

        // ── Multi-scene ClassifyRef (CS2.arch.2 regression) ──────────────────
        //
        // Old code: ownerSearchFrom = path.IndexOf('/', 1)
        //   On "SceneA:/RootA" that lands on the ':/' slash (index 7), so the
        //   extracted root part is "SceneA:" for every object in SceneA.
        //   Two distinct scene roots therefore compare equal → both labelled
        //   "sibling" instead of "external".
        //
        // New code: SkipScenePrefix uses IndexOf(":/") → advances past the full
        //   "SceneName:/" prefix, so root parts correctly include the object name.

        [Test]
        public void ClassifyRef_MultiScene_DifferentRoots_ReturnsExternal()
        {
            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti, "Must be multi-scene for this test");

            // Two distinct root GOs in the additive scene.
            var goA = CreateIn(_additiveScene, "RH_RootA");
            var goB = CreateIn(_additiveScene, "RH_RootB");

            // ownerPath = "AdditiveName:/RH_RootA", referenced = goB
            // OLD: root-part extraction lands on ':/' → both yield "SceneName:" → "sibling" (WRONG)
            // NEW: skips past ":/" → "AdditiveName:/RH_RootA" vs "AdditiveName:/RH_RootB" → "external"
            var relation = ReferenceHelper.ClassifyRef(ComponentSerializer.GetPath(goA), goB);
            Assert.AreEqual("external", relation,
                "Two distinct scene roots must be 'external', not 'sibling'");
        }

        [Test]
        public void ClassifyRef_MultiScene_SameRoot_ReturnsSibling()
        {
            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti, "Must be multi-scene for this test");

            // Parent + two children, all in the additive scene.
            var parent = CreateIn(_additiveScene, "RH_MS_Parent");
            var childA = CreateChild(parent, "RH_MS_ChildA");
            var childB = CreateChild(parent, "RH_MS_ChildB");

            // Both children share root prefix "AdditiveName:/RH_MS_Parent" → "sibling"
            // OLD and NEW both give "sibling" here, but the test guards against regression
            // where the sibling path would accidentally become "external".
            var relation = ReferenceHelper.ClassifyRef(
                ComponentSerializer.GetPath(childA), childB);
            Assert.AreEqual("sibling", relation,
                "Two children of the same parent must be 'sibling'");
        }
    }
}
