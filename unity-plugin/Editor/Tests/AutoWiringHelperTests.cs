// TDD: AutoWiringHelper — scan/apply/format unit tests.
// EditMode NUnit tests — run via Unity Test Runner. No TCP required.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AutoWiringHelperTests : SceneTestBase
    {
        // Minimal MonoBehaviour with one ObjectReference field per component type tested.
        private class LightHolder : MonoBehaviour
        {
            public Light lightRef = null;
        }

        private class MultiLightHolder : MonoBehaviour
        {
            // field name unlikely to match anything by name — forces type-only path
            public Light xyzNoMatch123 = null;
        }

        private class WiredHolder : MonoBehaviour
        {
            public Light lightRef = null;
        }

        private List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject MakeGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // ── ExactNameMatch ────────────────────────────────────────────────────

        [Test]
        public void ExactNameMatch_WiresField()
        {
            var root  = MakeGO("Root");
            var child = MakeGO("lightRef");  // exact match (case-insensitive)
            child.transform.SetParent(root.transform);
            child.AddComponent<Light>();
            root.AddComponent<LightHolder>();

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            Assert.AreEqual(1, wired.Count, "expected 1 wired candidate");
            Assert.AreEqual(0, skipped.Count);
            Assert.AreEqual("exact", wired[0].Reason);
            Assert.IsTrue(wired[0].Target is Light, "target should be the Light component");
        }

        // ── ContainsMatch ─────────────────────────────────────────────────────

        [Test]
        public void ContainsMatch_WiresField()
        {
            var root  = MakeGO("Root");
            var child = MakeGO("PlayerLightRef");  // contains "lightRef"
            child.transform.SetParent(root.transform);
            child.AddComponent<Light>();
            root.AddComponent<LightHolder>();

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            Assert.AreEqual(1, wired.Count);
            Assert.AreEqual("contains", wired[0].Reason);
        }

        // ── TypeOnly — single candidate ───────────────────────────────────────

        [Test]
        public void TypeOnlyMatch_SingleCandidate_Wires()
        {
            var root  = MakeGO("Root");
            var child = MakeGO("CompletelyUnrelatedName");  // no name overlap
            child.transform.SetParent(root.transform);
            child.AddComponent<Light>();
            root.AddComponent<MultiLightHolder>();  // field name = "xyzNoMatch123"

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            Assert.AreEqual(1, wired.Count, "expected type-only wire");
            Assert.AreEqual("type-only", wired[0].Reason);
        }

        // ── TypeOnly — multiple candidates → ambiguous ────────────────────────

        [Test]
        public void TypeOnlyMatch_Multiple_Skips()
        {
            var root   = MakeGO("Root");
            var child1 = MakeGO("UnrelatedA");
            var child2 = MakeGO("UnrelatedB");
            child1.transform.SetParent(root.transform);
            child2.transform.SetParent(root.transform);
            child1.AddComponent<Light>();
            child2.AddComponent<Light>();
            root.AddComponent<MultiLightHolder>();

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            Assert.AreEqual(0, wired.Count);
            Assert.AreEqual(1, skipped.Count, "ambiguous entry expected");
            StringAssert.Contains("AMBIGUOUS", skipped[0]);
        }

        // ── AlreadyWired — skip ───────────────────────────────────────────────

        [Test]
        public void AlreadyWired_NotTouched()
        {
            var root   = MakeGO("Root");
            var child  = MakeGO("lightRef");
            child.transform.SetParent(root.transform);
            var light  = child.AddComponent<Light>();
            var holder = root.AddComponent<WiredHolder>();
            holder.lightRef = light;  // already assigned

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            Assert.AreEqual(0, wired.Count, "already-wired field must be skipped");
        }

        // ── DryRun — Apply not called, value stays null ───────────────────────

        [Test]
        public void DryRun_NoChange()
        {
            var root   = MakeGO("Root");
            var child  = MakeGO("lightRef");
            child.transform.SetParent(root.transform);
            child.AddComponent<Light>();
            var holder = root.AddComponent<LightHolder>();

            var (wired, _) = AutoWiringHelper.Scan(root);
            Assert.AreEqual(1, wired.Count, "prerequisite: scan found a candidate");

            // dryRun=true: do NOT apply
            var result = AutoWiringHelper.Format(wired, new List<string>(), dryRun: true);
            Assert.IsNull(holder.lightRef, "lightRef must still be null — Apply not called");
            StringAssert.Contains("[DRY]", result);
        }

        // ── NoParent — siblings scope skipped gracefully ──────────────────────

        [Test]
        public void NoParent_SiblingsScopeSkipped()
        {
            var root = MakeGO("RootNoParent");  // no parent — root GO
            root.AddComponent<LightHolder>();

            // Should NOT throw NullReferenceException
            Assert.DoesNotThrow(() => AutoWiringHelper.Scan(root));
        }

        // ── Format — dryRun prepends [DRY] tag ───────────────────────────────

        [Test]
        public void Format_DryRun_PrependsTag()
        {
            var root  = MakeGO("Root");
            var child = MakeGO("lightRef");
            child.transform.SetParent(root.transform);
            child.AddComponent<Light>();
            root.AddComponent<LightHolder>();

            var (wired, skipped) = AutoWiringHelper.Scan(root);

            var result = AutoWiringHelper.Format(wired, skipped, dryRun: true);

            foreach (var line in result.Split('\n'))
            {
                if (line.Trim().Length == 0 || line.StartsWith("Wired:")) continue;
                StringAssert.StartsWith("[DRY]", line.Trim(), "each wired line must start with [DRY]");
            }
            StringAssert.DoesNotContain("[DRY]", result.Split('\n')[result.Split('\n').Length - 1],
                "summary line should not have [DRY]");
        }
    }
}
