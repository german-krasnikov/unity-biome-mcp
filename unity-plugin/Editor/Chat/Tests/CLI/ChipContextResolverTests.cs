// TDD — ChipContextResolver tests.
// H6: ChipKind enum removed — FormatChipRef/EmitTyped take string kindKey.
// New: custom kind through ResolveAllTyped pipeline.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipContextResolverTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private List<GameObject> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new List<GameObject>();
            ChipContextResolver.FindObjectOverride = null;
            ChipKindRegistry.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            ChipContextResolver.FindObjectOverride = null;
            ChipKindRegistry.ResetForTests();
        }

        private GameObject MakeGo(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform);
            _created.Add(go);
            return go;
        }

        [Test]
        public void ResolveOne_PathOnly_ReturnsPathAsIs()
        {
            var result = ChipContextResolver.ResolveOne("/Scene/Hero", ChipDepth.PathOnly);
            Assert.AreEqual("/Scene/Hero", result);
        }

        [Test]
        public void ResolveOne_Summary_SceneObject_ReturnsSummaryFormat()
        {
            var go = MakeGo("Hero");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/Hero", ChipDepth.Summary);

            StringAssert.StartsWith("[Context:", result);
            StringAssert.Contains("/Hero", result);
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void ResolveOne_Summary_NullObject_FallsBackToPathOnly()
        {
            ChipContextResolver.FindObjectOverride = _ => null;
            var result = ChipContextResolver.ResolveOne("/Missing/Object", ChipDepth.Summary);
            Assert.AreEqual("/Missing/Object", result);
        }

        [Test]
        public void ResolveOne_Full_ReturnsComponentDump()
        {
            var go = MakeGo("Tank");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveOne("/Tank", ChipDepth.Full);
            StringAssert.Contains("Tank", result);
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void ResolveOne_Full_ExceedsBudget_FallsToSummary()
        {
            var go = MakeGo("Fat");
            for (var i = 0; i < 10; i++) go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveOne("/Fat", ChipDepth.Full, budgetOverride: 1);
            Assert.IsTrue(result.StartsWith("["), "Fallback should be summary format");
        }

        [Test]
        public void ResolveOne_AssetPath_AlwaysPathOnly()
        {
            var result = ChipContextResolver.ResolveOne("Assets/Scripts/Foo.cs", ChipDepth.Full);
            Assert.AreEqual("Assets/Scripts/Foo.cs", result);
        }

        [Test]
        public void ResolveAll_SingleChip_UsesFull()
        {
            var go = MakeGo("Solo");
            go.AddComponent<Rigidbody>();
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveAll(new List<string> { "/Solo" });
            StringAssert.Contains("Solo", result);
        }

        [Test]
        public void ResolveAll_MultipleChips_UsesSummary()
        {
            var go1 = MakeGo("A");
            var go2 = MakeGo("B");
            go1.AddComponent<BoxCollider>();
            go2.AddComponent<Rigidbody>();
            ChipContextResolver.FindObjectOverride = p => p == "/A" ? go1 : go2;
            var result = ChipContextResolver.ResolveAll(new List<string> { "/A", "/B" });
            StringAssert.Contains("/A", result);
            StringAssert.Contains("/B", result);
        }

        [Test]
        public void ResolveAll_EmptyList_ReturnsEmpty()
        {
            Assert.AreEqual("", ChipContextResolver.ResolveAll(new List<string>()));
        }

        [Test]
        public void ResolveAll_MixedSceneAndAsset_CorrectDepths()
        {
            var go = MakeGo("Player");
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveAll(new List<string> { "/Player", "Assets/Scripts/Foo.cs" });
            StringAssert.Contains("/Player", result);
            StringAssert.Contains("Assets/Scripts/Foo.cs", result);
        }

        [Test]
        public void ResolveOne_DestroyedGameObject_FallsBackToPathOnly()
        {
            var go = new GameObject("Dead");
            Object.DestroyImmediate(go);
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveOne("/Dead", ChipDepth.Summary);
            Assert.AreEqual("/Dead", result);
        }

        [Test]
        public void Summary_Format_IsStructurallyValid()
        {
            var go = MakeGo("Check");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;
            var chip = ChipContextResolver.ResolveOne("/Check", ChipDepth.Summary);
            var sel  = SelectionSummary.Summarize(go);
            Assert.IsTrue(chip.StartsWith("["));
            Assert.IsTrue(sel.StartsWith("["));
            Assert.IsTrue(chip.EndsWith("]"));
            Assert.IsTrue(sel.EndsWith("]"));
            StringAssert.Contains("BoxCollider", chip);
            StringAssert.Contains("BoxCollider", sel);
        }

        [Test]
        public void ResolveOne_PathOnly_SceneObject_IncludesAmpRef()
        {
            var go = MakeGo("SceneObj");
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveOne("/SceneObj", ChipDepth.PathOnly);
            // ST5 format: &ref immediately after path, no space (e.g. /SceneObj&1a)
            var ampRef = RefManager.Assign(go);
            StringAssert.Contains("/SceneObj" + ampRef, result);
            Assert.IsTrue(RefManager.IsRef(ampRef));
        }

        [Test]
        public void ResolveOne_PathOnly_AssetPath_NoInstanceID()
        {
            var result = ChipContextResolver.ResolveOne("Assets/Foo.prefab", ChipDepth.PathOnly);
            Assert.AreEqual("Assets/Foo.prefab", result);
            Assert.IsFalse(result.Contains("#"));
        }

        [Test]
        public void ResolveOne_Summary_IncludesAmpRef()
        {
            var go = MakeGo("SummaryObj");
            ChipContextResolver.FindObjectOverride = _ => go;
            var result = ChipContextResolver.ResolveOne("/SummaryObj", ChipDepth.Summary);
            // ST5 format: &ref via SelectionSummary → RefManager.Assign
            StringAssert.Contains(RefManager.Assign(go), result);
        }

        [Test]
        public void ResolveAll_MultiChip_EachHasAmpRef()
        {
            var go1 = MakeGo("Multi1");
            var go2 = MakeGo("Multi2");
            ChipContextResolver.FindObjectOverride = p => p.Contains("Multi1") ? go1 : go2;
            var result = ChipContextResolver.ResolveAll(new List<string> { "/Multi1", "/Multi2" });
            // ST5 format: &ref via SelectionSummary → RefManager.Assign
            StringAssert.Contains(RefManager.Assign(go1), result);
            StringAssert.Contains(RefManager.Assign(go2), result);
        }

        // ── FormatChipRef — string kindKey (H6) ─────────────────────────────

        [Test]
        public void FormatChipRef_Hierarchy_BracketFormatWithInstanceID()
        {
            // objectId already in $HEX format — direct concatenation
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Hierarchy, "/World/Player", "$3039");
            Assert.AreEqual("[hierarchy:/World/Player$3039]", result);
        }

        [Test]
        public void FormatChipRef_Hierarchy_AmpRef_ProducesCorrectBracket()
        {
            // ST5: &ref format — direct concatenation, same path as $HEX
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Hierarchy, "/Player", "&a");
            Assert.AreEqual("[hierarchy:/Player&a]", result);
        }

        [Test]
        public void FormatChipRef_Hierarchy_AlreadyHexRef_PassThrough()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Hierarchy, "/World/Player", "$3039");
            Assert.AreEqual("[hierarchy:/World/Player$3039]", result);
        }

        [Test]
        public void FormatChipRef_Script_NameOnly_NoBracketID()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Script, "PlayerController", "");
            Assert.AreEqual("[script:PlayerController]", result);
        }

        [Test]
        public void FormatChipRef_Scene_FullAssetPath()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Scene, "Assets/Scenes/Main.unity", "");
            Assert.AreEqual("[scene:Assets/Scenes/Main.unity]", result);
        }

        [Test]
        public void FormatChipRef_Asset_AssetPath()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Asset, "Assets/Fonts/Arial.ttf", "");
            Assert.AreEqual("[asset:Assets/Fonts/Arial.ttf]", result);
        }

        [Test]
        public void FormatChipRef_Hierarchy_ZeroInstanceID_OmitsHashSuffix()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.Hierarchy, "/Player", "");
            Assert.AreEqual("[hierarchy:/Player]", result);
        }

        [Test]
        public void FormatChipRef_ScriptableObject_UsesSoPrefix()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKindKeys.ScriptableObject, "Assets/Data/Cfg.asset", "");
            Assert.AreEqual("[so:Assets/Data/Cfg.asset]", result);
        }

        // ── EmitTyped — string kindKey (H6) ─────────────────────────────────

        [Test]
        public void EmitTyped_DepthNone_ReturnsEmpty()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Script, "Assets/Foo.cs", "", "none", (p, d) => "RESOLVED");
            Assert.AreEqual("", result);
        }

        [Test]
        public void EmitTyped_DepthPath_ReturnsBracketOnly()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Script, "Assets/Foo.cs", "", "path", (p, d) => "RESOLVED");
            Assert.AreEqual("[script:Assets/Foo.cs]", result);
        }

        [Test]
        public void EmitTyped_DepthPath_Hierarchy_IncludesInstanceID()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Hierarchy, "/Player", "$7B", "path", (p, d) => "RESOLVED");
            Assert.AreEqual("[hierarchy:/Player$7B]", result);
        }

        [Test]
        public void EmitTyped_DepthSummary_StartsWithBracketThenResolved()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Hierarchy, "/Player", "$7B", "summary",
                (p, d) => "summary-text");
            StringAssert.StartsWith("[hierarchy:/Player$7B]", result);
            StringAssert.Contains("summary-text", result);
        }

        [Test]
        public void EmitTyped_DepthFull_StartsWithBracketThenResolved()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Hierarchy, "/Player", "", "full",
                (p, d) => "full-dump");
            StringAssert.StartsWith("[hierarchy:/Player]", result);
            StringAssert.Contains("full-dump", result);
        }

        [Test]
        public void EmitTyped_DepthSummary_Asset_BracketAndAssetPath()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Script, "Assets/Foo.cs", "", "summary",
                (p, d) => p);
            StringAssert.StartsWith("[script:Assets/Foo.cs]", result);
        }

        [Test]
        public void EmitTyped_UnknownDepth_TreatedAsPath()
        {
            var result = ChipContextResolver.EmitTyped(ChipKindKeys.Asset, "Assets/X.png", "", "bogus", (p, d) => "R");
            Assert.AreEqual("[asset:Assets/X.png]", result);
        }

        // ── Custom kind through ResolveAllTyped pipeline ─────────────────────

        [Test]
        public void ResolveAllTyped_CustomKind_RoutesToProvider()
        {
            var fake = new FakeCustomProvider();
            ChipKindRegistry.Register(fake);
            var chips = new List<ChipData>
            {
                new ChipData("fake_kind", "Assets/x.fbx", "X", 0)
            };
            var result = ChipContextResolver.ResolveAllTyped(chips, new ChipConfig());
            StringAssert.Contains("[fake_kind:Assets/x.fbx]", result);
        }

        private sealed class FakeCustomProvider : IChipKindProvider
        {
            public string Key          => "fake_kind";
            public int    Priority     => 10;
            public string IconName     => "";
            public string HexColor     => "#000000";
            public string DefaultDepth => "path";
            public string[] BarePathExtensions => System.Array.Empty<string>();
            public bool   CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath) => default;
            public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
                => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";
            public void Navigate(string reference) { }
            public void Ping(string reference) { }
            public void AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string reference) { }
        }
    }
}
