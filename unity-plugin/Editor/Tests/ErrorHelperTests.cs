// NUnit tests for ErrorHelper — CS2.arch.3 (multi-scene ObjectNotFound suggestion).
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ErrorHelperTests : MultiSceneTestBase
    {
        // ── Single-scene baseline ─────────────────────────────────────────────

        [Test]
        public void ObjectNotFound_SingleScene_ContainsPath()
        {
            var msg = ErrorHelper.ObjectNotFound("/SomeNonExistent");
            StringAssert.Contains("SomeNonExistent", msg);
        }

        [Test]
        public void ObjectNotFound_HasDidYouMean_ForCloseMatch()
        {
            var go = new GameObject("EH_RealObject");
            TrackOwnedObject(go);

            // "EH_RealObjec" is 1 char off → should get a suggestion
            var msg = ErrorHelper.ObjectNotFound("/EH_RealObjec");
            StringAssert.Contains("Did you mean", msg);
        }

        // ── Multi-scene: CS2.arch.3 regression ───────────────────────────────

        [Test]
        public void ObjectNotFound_MultiScene_SuggestsFromAllScenes()
        {
            // "AdditiveExclusive" lives ONLY in the additive scene, nowhere in the active scene.
            // The not-found query is "/NoSuchTarget" — the name "AdditiveExclusive" does NOT
            // appear anywhere in that query string, so a Contains("AdditiveExclusive") assertion
            // cannot pass by accident from the error prefix.
            //
            // OLD code: scanned only the active scene → ClosestName never saw "AdditiveExclusive"
            //           → no "Did you mean" for it → test FAILS on old code.
            // NEW code: scans all loaded scenes → "AdditiveExclusive" is a candidate → PASSES.
            CreateIn(_additiveScene, "AdditiveExclusive");

            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti, "Must be multi-scene for this regression test");

            // Query is close enough (Levenshtein) to trigger a suggestion for "AdditiveExclusive".
            // Use a name within the edit-distance threshold: threshold = max(3, len*2/5).
            // "AdditiveExclusiv" is 1 char short → distance 1 ≤ threshold(3) → suggestion fires.
            var msg = ErrorHelper.ObjectNotFound("/AdditiveExclusiv");

            StringAssert.Contains("Did you mean", msg);
            StringAssert.Contains("AdditiveExclusive", msg,
                "Suggestion must name the additive-scene object; old code misses it");
        }

        [Test]
        public void ObjectNotFound_MultiScene_RootObjectsIncludeBothScenes()
        {
            var mainGO = new GameObject("EH_MainSceneRoot");
            TrackOwnedObject(mainGO);
            var additiveGO = CreateIn(_additiveScene, "EH_AdditiveRoot");

            var msg = ErrorHelper.ObjectNotFound("/NoSuchObject_XYZ");

            // Both scene roots should appear in the "Root objects:" list
            StringAssert.Contains("EH_MainSceneRoot", msg);
            StringAssert.Contains("EH_AdditiveRoot", msg);
        }
    }
}
