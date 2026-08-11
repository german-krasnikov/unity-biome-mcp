// ChipNavigationIntegrationTests — direct tests for HierarchyChipProvider.Navigate + Create.
// Gap: InputChipClickTests/UserBubblePillTests use SpyProvider; real Navigate never tested directly.
// All tests use scene GOs (not assets) — no AssetDatabase.Contains check needed.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipNavigationIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private IChipKindProvider    _provider;
        private readonly List<GameObject> _created = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            _provider = ChipKindRegistry.ForKey(ChipKindKeys.Hierarchy);
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeGameObject = null;
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            ChipKindRegistry.ResetForTests();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // B1 — empty path logs warning but does not throw
        [Test]
        public void Navigate_EmptyPath_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _provider.Navigate(""));
        }

        // B2 — non-existent path logs warning but does not throw
        [Test]
        public void Navigate_NonExistentPath_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _provider.Navigate("/DoesNotExistXYZ999"));
        }

        // B3 — real GO path → Selection.activeGameObject is set
        [Test]
        public void Navigate_RealGO_SetsActiveGameObject()
        {
            var go = Make("NavTarget");
            _provider.Navigate(ComponentSerializer.GetPath(go));
            Assert.AreEqual(go, Selection.activeGameObject);
        }

        // B4 — path with $HEX suffix (/Name$XXXX) → GO resolved and selected
        [Test]
        public void Navigate_ByInstanceId_SetsActiveGameObject()
        {
            var go = Make("IdTarget");
            var id = TransientObjectId.GetHexRef(go);  // "$XXXX"
            _provider.Navigate($"/IdTarget{id}");       // "/IdTarget$XXXX"
            Assert.AreEqual(go, Selection.activeGameObject);
        }

        // B5 — leaf-name fuzzy fallback: mismatched parent path → GameObject.Find(leaf) succeeds
        [Test]
        public void Navigate_LeafFuzzyMatch_FindsGO()
        {
            var go = Make("FuzzyLeaf9743");
            _provider.Navigate("/SomeMissingParent/FuzzyLeaf9743");
            Assert.AreEqual(go, Selection.activeGameObject);
        }

        // B6 — scene GOs do NOT populate handledPaths (asset dedup must not fire for scene refs)
        [Test]
        public void ProcessDraggedObject_SceneGO_HandledPaths_NotPopulated()
        {
            var go      = Make("HpGO");
            var handled = new HashSet<string>();
            var chips   = new List<(Object, string, string)>();
            MCPChatWindow.ProcessDraggedObject(go, null,
                (o, p, n) => chips.Add((o, p, n)),
                handledPaths: handled);
            Assert.AreEqual(1, chips.Count,   "one chip must be inserted for a scene GO");
            Assert.AreEqual(0, handled.Count, "scene GOs must not populate handledPaths");
        }

        // B7 - Create() stores the object's EntityId as $HEX ref.
        [Test]
        public void HierarchyChipProvider_Create_SetsInstanceId()
        {
            var go   = Make("IdChip");
            var chip = _provider.Create(go, "");
            Assert.AreEqual(TransientObjectId.GetHexRef(go), chip.ObjectId);
        }

        // B8 — full round-trip: Create → FormatPayload → HierarchyReference.Parse → Resolve → same GO
        [Test]
        public void HierarchyChipProvider_RoundTrip_FormatThenParseThenResolve()
        {
            var go   = Make("RoundTripTarget");
            var chip = _provider.Create(go, "");

            // ObjectId must be $HEX after Phase 2
            StringAssert.StartsWith("$", chip.ObjectId);

            // FormatPayload → e.g. "[hierarchy:/RoundTripTarget$XXXX]" (or with @GOID suffix)
            var fullRef = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.IsNotEmpty(fullRef);
            Assert.IsTrue(fullRef.StartsWith("[hierarchy:"));

            // Strip outer brackets and "hierarchy:" prefix → raw ref for HierarchyReference.Parse
            var inner  = fullRef.Substring(1, fullRef.Length - 2);          // "hierarchy:/RoundTripTarget$XXXX..."
            var rawRef = inner.Substring("hierarchy:".Length);               // "/RoundTripTarget$XXXX..."

            var href     = HierarchyReference.Parse(rawRef);
            var resolved = new HierarchyResolver().Resolve(href);
            Assert.AreEqual(go, resolved, "Round-trip must resolve back to the original GameObject");
        }
    }
}
