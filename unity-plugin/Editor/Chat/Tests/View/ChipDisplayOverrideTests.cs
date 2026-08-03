// P4: ChipDisplayOverride — 15-test TDD matrix.
// Tests 1-10: ChipConfig override layer (color + depth).
// Tests 11-13: ChipPillFactory.ColorResolver static seam.
// Tests 14-15: BuildChipDisplayForm registry-driven form.
using NUnit.Framework;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipDisplayOverrideTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"ChipDisplayOverrideTest_{System.Guid.NewGuid():N}.json");
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null; // clean static seam
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        // ── 1: ResolveColor — no override → provider color ───────────────────

        [Test]
        public void ChipConfig_ResolveColor_NoOverride_ReturnsProviderColor()
        {
            var cfg = new ChipConfig();
            // HierarchyChipProvider.HexColor = "#4a9eff"
            Assert.AreEqual("#4a9eff", cfg.ResolveColor(ChipKindKeys.Hierarchy));
        }

        // ── 2: ResolveColor — with override → override color ─────────────────

        [Test]
        public void ChipConfig_ResolveColor_WithOverride_ReturnsOverrideColor()
        {
            var cfg = new ChipConfig();
            cfg.SetColorOverride(ChipKindKeys.Hierarchy, "#ff0000");
            Assert.AreEqual("#ff0000", cfg.ResolveColor(ChipKindKeys.Hierarchy));
        }

        // ── 3: ResolveColor — clear override → back to provider ──────────────

        [Test]
        public void ChipConfig_ResolveColor_ClearOverride_ReturnsProviderColor()
        {
            var cfg = new ChipConfig();
            cfg.SetColorOverride(ChipKindKeys.Hierarchy, "#ff0000");
            cfg.SetColorOverride(ChipKindKeys.Hierarchy, null);
            Assert.AreEqual("#4a9eff", cfg.ResolveColor(ChipKindKeys.Hierarchy));
        }

        // ── 4: ResolveColor — unknown kind → gray fallback ───────────────────

        [Test]
        public void ChipConfig_ResolveColor_UnknownKind_ReturnsGrayFallback()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("#94a3b8", cfg.ResolveColor("completely_unknown"));
        }

        // ── 5: SetDepthOverride → DepthFor returns override ──────────────────

        [Test]
        public void ChipConfig_SetDepthOverride_OverridesDepthFor()
        {
            var cfg = new ChipConfig();
            cfg.SetDepthOverride(ChipKindKeys.Hierarchy, "full");
            Assert.AreEqual("full", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        // ── 6: SetDepthOverride null → clears, legacy field used ─────────────

        [Test]
        public void ChipConfig_SetDepthOverride_Null_ClearsOverride()
        {
            var cfg = new ChipConfig { HierarchyDepth = "summary" };
            cfg.SetDepthOverride(ChipKindKeys.Hierarchy, "full");
            cfg.SetDepthOverride(ChipKindKeys.Hierarchy, null);
            // Should fall back to legacy field
            Assert.AreEqual("summary", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        // ── 7: SetDepthOverride — 3rd-party custom kind ───────────────────────

        [Test]
        public void ChipConfig_SetDepthOverride_CustomKind_Works()
        {
            var fake = new FakeProvider("custom_x", "path");
            ChipKindRegistry.Register(fake);
            var cfg = new ChipConfig();
            cfg.SetDepthOverride("custom_x", "none");
            Assert.AreEqual("none", cfg.DepthFor("custom_x"));
        }

        // ── 8: FlushToArrays + Save/Load round-trip ───────────────────────────

        [Test]
        public void ChipConfig_OverrideRoundTrip_SaveLoad()
        {
            var original = new BackendConfigStore();
            original.Chips.SetDepthOverride(ChipKindKeys.Hierarchy, "full");
            original.Chips.SetColorOverride(ChipKindKeys.Hierarchy, "#aabbcc");
            original.Save(_tempPath);

            var loaded = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("full",    loaded.Chips.DepthFor(ChipKindKeys.Hierarchy));
            Assert.AreEqual("#aabbcc", loaded.Chips.ResolveColor(ChipKindKeys.Hierarchy));
        }

        // ── 9: Legacy fields survive round-trip ───────────────────────────────

        [Test]
        public void ChipConfig_OverrideRoundTrip_PreservesLegacyFields()
        {
            var original = new BackendConfigStore
            {
                Chips = new ChipConfig { HierarchyDepth = "full" }
            };
            original.Save(_tempPath);

            var loaded = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("full", loaded.Chips.HierarchyDepth);
            Assert.AreEqual("full", loaded.Chips.DepthFor(ChipKindKeys.Hierarchy));
        }

        // ── 10: Override wins over legacy ─────────────────────────────────────

        [Test]
        public void ChipConfig_MixedLegacyAndOverride_OverrideWins()
        {
            var cfg = new ChipConfig { HierarchyDepth = "summary" };
            cfg.SetDepthOverride(ChipKindKeys.Hierarchy, "full");
            Assert.AreEqual("full", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        // ── 11: ColorResolver overrides provider color ────────────────────────

        [Test]
        public void ChipPillFactory_ColorResolver_OverridesProviderColor()
        {
            ChipPillFactory.ColorResolver = k => "#ff0000";
            var pill = ChipPillFactory.Build(ChipKindKeys.Hierarchy, "Scene");

            var bg = pill.style.backgroundColor;
            Assert.AreEqual(StyleKeyword.Undefined, bg.keyword, "color must be explicitly set");
            // #ff0000 → r=1, g=0, b=0
            Assert.Greater(bg.value.r, 0.9f, "pill should be red from resolver");
            Assert.Less(bg.value.g, 0.1f,    "pill should be red from resolver (g≈0)");
        }

        // ── 12: ColorResolver = null → provider color used ───────────────────

        [Test]
        public void ChipPillFactory_ColorResolver_Null_FallsToProvider()
        {
            ChipPillFactory.ColorResolver = null;
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs");

            // Script provider = "#4ade80" (greenish)
            var bg = pill.style.backgroundColor;
            Assert.AreEqual(StyleKeyword.Undefined, bg.keyword, "color must be set");
            Assert.Greater(bg.value.g, bg.value.r, "should be greenish (provider color)");
        }

        // ── 13: ColorResolver returns null → provider color used ─────────────

        [Test]
        public void ChipPillFactory_ColorResolver_ReturnsNull_FallsToProvider()
        {
            ChipPillFactory.ColorResolver = k => null;
            var pill = ChipPillFactory.Build(ChipKindKeys.Script, "Foo.cs");

            var bg = pill.style.backgroundColor;
            Assert.AreEqual(StyleKeyword.Undefined, bg.keyword, "color must be set");
            Assert.Greater(bg.value.g, bg.value.r, "should fall through to greenish provider color");
        }

        // ── 14: BuildChipDisplayForm — one row per registered kind ───────────

        [Test]
        public void BuildChipDisplayForm_CreatesRowPerRegisteredKind()
        {
            // 12 built-ins + 2 fakes = 14 rows
            ChipKindRegistry.Register(new FakeProvider("fake_alpha", "path"));
            ChipKindRegistry.Register(new FakeProvider("fake_beta",  "path"));

            var parent = new VisualElement();
            var config = new ChipConfig();
            BackendSettingsForm.BuildChipDisplayForm(parent, config, () => { });

            int registeredCount = ChipKindRegistry.AllKeys.Count;
            // Rows contain a depth dropdown; header/toggles/separator do not.
            int rowCount = parent.Children().Count(c => c.Q<DropdownField>() != null);
            Assert.AreEqual(registeredCount, rowCount,
                $"Expected {registeredCount} rows, got {rowCount}");
        }

        // ── 15: 3rd-party kind appears in form automatically ──────────────────

        [Test]
        public void BuildChipDisplayForm_ThirdPartyKind_AppearsAutomatically()
        {
            ChipKindRegistry.Register(new FakeProvider("my_custom_kind", "path"));

            var parent = new VisualElement();
            var config = new ChipConfig();
            BackendSettingsForm.BuildChipDisplayForm(parent, config, () => { });

            bool found = false;
            foreach (var child in parent.Children())
            {
                var lbl = child.Q<Label>();
                if (lbl != null && lbl.text == "my_custom_kind") { found = true; break; }
            }
            Assert.IsTrue(found, "'my_custom_kind' label must appear in the form");
        }

        // ── 16: Legacy non-default field is migrated into override cache ─────

        [Test] public void ChipConfig_EnsureCache_MigratesNonDefaultLegacyField()
        {
            var cfg = new ChipConfig { HierarchyDepth = "full" };
            Assert.AreEqual("full", cfg.DepthFor(ChipKindKeys.Hierarchy)); // migrated/legacy resolves
            cfg.FlushToArrays();
            Assert.AreEqual(1, cfg.OverrideKeys.Length);
            Assert.AreEqual(ChipKindKeys.Hierarchy, cfg.OverrideKeys[0]);
            Assert.AreEqual("full", cfg.OverrideDepths[0]);
        }

        // ── 17: Jagged parallel arrays don't throw, color still resolves ─────

        [Test] public void ChipConfig_EnsureCache_JaggedArrays_NoThrow_LoadsColor()
        {
            var cfg = new ChipConfig {
                OverrideKeys   = new[] { "hierarchy" },
                OverrideDepths = new string[0],          // shorter than keys
                OverrideColors = new[] { "#ff0000" }
            };
            Assert.DoesNotThrow(() => cfg.ResolveColor(ChipKindKeys.Hierarchy));
            Assert.AreEqual("#ff0000", cfg.ResolveColor(ChipKindKeys.Hierarchy));
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.Hierarchy)); // no depth entry → provider default
        }

        // ── 18: Reset writes provider default, overrides legacy ───────────────

        [Test] public void ChipConfig_ResetWritesProviderDefault_OverridesLegacy()
        {
            var cfg = new ChipConfig { HierarchyDepth = "full" };       // legacy non-default
            cfg.SetDepthOverride(ChipKindKeys.Hierarchy, "path");        // what Reset writes (provider default)
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private sealed class FakeProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly string _defaultDepth;

            public FakeProvider(string key, string defaultDepth)
            {
                _key = key; _defaultDepth = defaultDepth;
            }

            public string Key          => _key;
            public int    Priority     => 100;
            public string IconName     => "";
            public string HexColor     => "#abcdef";
            public string DefaultDepth => _defaultDepth;
            public string[] BarePathExtensions => System.Array.Empty<string>();
            public bool   CanHandle(UnityEngine.Object o, string p) => false;
            public ChipData Create(UnityEngine.Object o, string p)  => default;
            public string FormatPayload(ChipData c, ChipPayloadContext x) => "";
            public void   Navigate(string r) { }
            public void   Ping(string r) { }
            public void   AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string r) { }
        }
    }
}
