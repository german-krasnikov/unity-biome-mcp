// TDD tests for F26: drag/drop MonoScript with @Object | @Script dual-chip binding.
// Tests verify detector + policy behavior without needing live drag events.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ScriptDragDropTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        private List<(Object obj, string path, string name)> _chips;
        private void Capture(Object obj, string path, string name)
            => _chips.Add((obj, path, name));

        [Test]
        public void ChipKindDetector_MonoScript_ReturnsScriptKind()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript ChatActivityState");
            if (guids.Length == 0) { Assert.Ignore("No MonoScript found in project"); return; }
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var ms   = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.IsNotNull(ms);
            Assert.AreEqual(ChipKindKeys.Script, ChipKindDetector.Detect(ms, path));
        }

        [Test]
        public void ChatChipPolicy_AllowsMonoScript()
        {
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(MonoScript)));
        }

        // --- ProcessDraggedObject tests ---

        private static MonoScript FindTestMonoScript()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript ChatActivityState");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<MonoScript>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void ProcessDraggedObject_Null_Skipped()
        {
            _chips = new List<(Object, string, string)>();
            MCPChatWindow.ProcessDraggedObject(null, null, Capture);
            Assert.AreEqual(0, _chips.Count);
        }

        [Test]
        public void ProcessDraggedObject_MonoScript_InsertsScriptChip()
        {
            var ms = FindTestMonoScript();
            if (ms == null) { Assert.Ignore("No MonoScript found in project"); return; }

            _chips = new List<(Object, string, string)>();
            MCPChatWindow.ProcessDraggedObject(ms, null, Capture);

            Assert.AreEqual(1, _chips.Count, "Exactly one chip inserted");
            Assert.AreEqual(ms, _chips[0].obj);
        }

        [Test]
        public void ProcessDraggedObject_MonoScript_NoSelection_OnlyScriptChip()
        {
            var ms = FindTestMonoScript();
            if (ms == null) { Assert.Ignore("No MonoScript found in project"); return; }

            _chips = new List<(Object, string, string)>();
            MCPChatWindow.ProcessDraggedObject(ms, null, Capture);

            Assert.AreEqual(1, _chips.Count, "Only script chip — no GO inserted");
            Assert.AreEqual(ms, _chips[0].obj, "Chip is the MonoScript");
        }

        [Test]
        public void ProcessDraggedObject_MonoScript_GOWithoutComponent_OnlyScriptChip()
        {
            var ms = FindTestMonoScript();
            if (ms == null) { Assert.Ignore("No MonoScript found in project"); return; }

            // Scene GO that does NOT have this script attached
            var go = new GameObject("TestGO_NoCmp");
            try
            {
                _chips = new List<(Object, string, string)>();
                MCPChatWindow.ProcessDraggedObject(ms, go, Capture);

                // GO lacks the component → only 1 chip (the script)
                Assert.AreEqual(1, _chips.Count, "Only script chip when GO lacks the component");
                Assert.AreEqual(ms, _chips[0].obj);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // P1-4 F26: dual-chip happy-path — GO "has" component → [GO chip, Script chip]
        // Uses injectable hasComponent predicate because MonoScript.FromMonoBehaviour returns null
        // for MonoBehaviours in Editor-only test assemblies that aren't indexed by AssetDatabase.
        [Test]
        public void ProcessDraggedObject_MonoScript_GOWithComponent_TwoChips()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript TestDummyMB");
            if (guids.Length == 0) { Assert.Ignore("TestDummyMB MonoScript not found in project"); return; }
            var msPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(msPath);
            if (ms == null || ms.GetClass() == null)
            { Assert.Ignore("TestDummyMB MonoScript could not be loaded"); return; }

            var go = new GameObject("TestGO_WithDummy");
            try
            {
                _chips = new List<(Object, string, string)>();
                // Inject predicate: simulate GO has TestDummyMB attached
                MCPChatWindow.ProcessDraggedObject(ms, go, Capture,
                    hasComponent: (g, t) => t == typeof(TestDummyMB));

                Assert.AreEqual(2, _chips.Count, "Expected 2 chips: [GO, Script]");
                Assert.AreEqual(go, _chips[0].obj, "First chip must be the GO");
                Assert.AreEqual(ms, _chips[1].obj, "Second chip must be the MonoScript");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // --- Component drag tests (Block 5) ---

        [Test]
        public void ProcessDraggedObject_MonoBehaviourComponent_InsertsGOChip()
        {
            var go = new GameObject("TestGO_Comp");
            try
            {
                var comp = go.AddComponent<BoxCollider>();
                Assert.IsNotNull(comp, "AddComponent<BoxCollider> must not return null");
                _chips = new List<(Object, string, string)>();
                MCPChatWindow.ProcessDraggedObject(comp, null, Capture);

                Assert.AreEqual(1, _chips.Count, "Built-in Component: exactly GO chip");
                Assert.AreEqual(go, _chips[0].obj, "Chip must be the GO");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ProcessDraggedObject_BuiltInComponent_InsertsOnlyGOChip()
        {
            var go = new GameObject("TestGO_Rigid");
            try
            {
                var rb = go.AddComponent<Rigidbody>();
                _chips = new List<(Object, string, string)>();
                MCPChatWindow.ProcessDraggedObject(rb, null, Capture);
                Assert.AreEqual(1, _chips.Count, "Built-in: only GO chip");
                Assert.AreEqual(go, _chips[0].obj);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- DefaultAsset (non-folder) tests ---

        [Test]
        public void ProcessDraggedObject_DefaultAsset_HandledPaths_TracksInsertedPath()
        {
            // Use a real DefaultAsset from the project if available
            var guids = AssetDatabase.FindAssets("t:DefaultAsset", new[] { "Assets" });
            if (guids.Length == 0) { Assert.Ignore("No DefaultAsset found in Assets"); return; }
            var assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            var obj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(assetPath);
            if (obj == null) { Assert.Ignore("Could not load DefaultAsset"); return; }

            _chips = new List<(Object, string, string)>();
            var handled = new System.Collections.Generic.HashSet<string>();
            MCPChatWindow.ProcessDraggedObject(obj, null, Capture, handledPaths: handled);

            // Whether it's folder or file, the path should be tracked if a chip was inserted
            Assert.AreEqual(1, _chips.Count, "Expected chip for DefaultAsset");
            Assert.IsTrue(handled.Contains(assetPath), "handledPaths must contain inserted path");
        }
    }
}
