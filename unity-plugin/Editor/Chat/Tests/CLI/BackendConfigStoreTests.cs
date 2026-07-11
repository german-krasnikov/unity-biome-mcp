// TDD tests for BackendConfigStore. Pure unit — no Unity API, no I/O beyond temp files.
// H15: HierarchyDepth default changed from "summary" to "path".
// H5/H6: DepthFor takes string kindKey; custom key falls to provider.DefaultDepth.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendConfigStoreTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"BackendConfigTest_{System.Guid.NewGuid():N}.json");
            ChipKindRegistry.ResetToBuiltIns();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
            ChipKindRegistry.ResetToBuiltIns();
        }

        [Test]
        public void BackendConfigStore_Load_DefaultsWhenFileAbsent()
        {
            var store = BackendConfigStore.Load(_tempPath);

            Assert.IsNotNull(store);
            Assert.IsNotNull(store.Claude);
            Assert.IsNotNull(store.Codex);
            Assert.AreEqual("plan",              store.Claude.PermissionMode);
            Assert.AreEqual("",                  store.Claude.Model);
            Assert.AreEqual("",                  store.Claude.ExtraArgs);
            Assert.AreEqual("danger-full-access", store.Codex.PermissionMode);
            Assert.AreEqual(30,                  store.Codex.StartupTimeoutSec);
            Assert.AreEqual("",                  store.Codex.Model);
            Assert.AreEqual("",                  store.Codex.ExtraArgs);
        }

        [Test]
        public void BackendConfigStore_SaveLoad_RoundTrip()
        {
            var original = new BackendConfigStore
            {
                Claude = new ClaudeBackendConfig { Model = "claude-opus-4", PermissionMode = "acceptEdits", ExtraArgs = "--debug" },
                Codex  = new CodexBackendConfig  { Model = "o3", StartupTimeoutSec = 60, ExtraArgs = "--verbose" }
            };

            original.Save(_tempPath);
            var loaded = BackendConfigStore.Load(_tempPath);

            Assert.AreEqual("claude-opus-4",  loaded.Claude.Model);
            Assert.AreEqual("acceptEdits",     loaded.Claude.PermissionMode);
            Assert.AreEqual("--debug",         loaded.Claude.ExtraArgs);
            Assert.AreEqual("o3",              loaded.Codex.Model);
            Assert.AreEqual(60,                loaded.Codex.StartupTimeoutSec);
            Assert.AreEqual("--verbose",       loaded.Codex.ExtraArgs);
        }

        // ── ChipConfig defaults (H15: HierarchyDepth is now "path") ──────────

        [Test]
        public void ChipConfig_DefaultsOnLoad()
        {
            var store = BackendConfigStore.Load(_tempPath);
            Assert.IsNotNull(store.Chips);
            // H15: HierarchyDepth default changed from "summary" to "path"
            Assert.AreEqual("path", store.Chips.HierarchyDepth);
            Assert.AreEqual("path", store.Chips.ScriptDepth);
            Assert.AreEqual("path", store.Chips.SceneDepth);
            Assert.AreEqual("path", store.Chips.PrefabDepth);
            Assert.AreEqual("path", store.Chips.AssetDepth);
        }

        [Test]
        public void ChipConfig_RoundTrip_CustomDepths()
        {
            var original = new BackendConfigStore
            {
                Chips = new ChipConfig
                {
                    HierarchyDepth = "full",
                    ScriptDepth    = "none",
                    SceneDepth     = "summary",
                    PrefabDepth    = "path",
                    AssetDepth     = "none",
                }
            };

            original.Save(_tempPath);
            var loaded = BackendConfigStore.Load(_tempPath);

            Assert.AreEqual("full",    loaded.Chips.HierarchyDepth);
            Assert.AreEqual("none",    loaded.Chips.ScriptDepth);
            Assert.AreEqual("summary", loaded.Chips.SceneDepth);
            Assert.AreEqual("path",    loaded.Chips.PrefabDepth);
            Assert.AreEqual("none",    loaded.Chips.AssetDepth);
        }

        [Test]
        public void ChipConfig_MissingField_DefaultsFallback()
        {
            var oldJson = "{\"Claude\":{\"PermissionMode\":\"plan\",\"Model\":\"\",\"ExtraArgs\":\"\"},\"Codex\":{\"Model\":\"\",\"PermissionMode\":\"danger-full-access\",\"StartupTimeoutSec\":30,\"ExtraArgs\":\"\"}}";
            System.IO.File.WriteAllText(_tempPath, oldJson);

            var loaded = BackendConfigStore.Load(_tempPath);

            Assert.IsNotNull(loaded.Chips);
            // H15: new default is "path"
            Assert.AreEqual("path", loaded.Chips.HierarchyDepth);
            Assert.AreEqual("path", loaded.Chips.ScriptDepth);
        }

        // ── ChipConfig.DepthFor — string kindKey (H6) ────────────────────────

        [Test]
        public void DepthFor_Hierarchy_ReturnsHierarchyDepth()
        {
            var cfg = new ChipConfig { HierarchyDepth = "full" };
            Assert.AreEqual("full", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        [Test]
        public void DepthFor_Script_ReturnsScriptDepth()
        {
            var cfg = new ChipConfig { ScriptDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKindKeys.Script));
        }

        [Test]
        public void DepthFor_Scene_ReturnsSceneDepth()
        {
            var cfg = new ChipConfig { SceneDepth = "summary" };
            Assert.AreEqual("summary", cfg.DepthFor(ChipKindKeys.Scene));
        }

        [Test]
        public void DepthFor_Prefab_ReturnsPrefabDepth()
        {
            var cfg = new ChipConfig { PrefabDepth = "path" };
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.Prefab));
        }

        [Test]
        public void DepthFor_Material_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKindKeys.Material));
        }

        [Test]
        public void DepthFor_Texture_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "summary" };
            Assert.AreEqual("summary", cfg.DepthFor(ChipKindKeys.Texture));
        }

        [Test]
        public void DepthFor_ScriptableObject_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "path" };
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.ScriptableObject));
        }

        [Test]
        public void DepthFor_Asset_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKindKeys.Asset));
        }

        // H15: renamed from DepthFor_Default_HierarchyIsSummary
        [Test]
        public void DepthFor_Default_HierarchyIsPath()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.Hierarchy));
        }

        [Test]
        public void DepthFor_Default_ScriptIsPath()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("path", cfg.DepthFor(ChipKindKeys.Script));
        }

        // H5: custom key falls to provider.DefaultDepth
        [Test]
        public void DepthFor_CustomKey_FallsToProviderDefaultDepth()
        {
            var fake = new FakeDepthProvider();
            ChipKindRegistry.Register(fake);
            var cfg = new ChipConfig();
            Assert.AreEqual("summary", cfg.DepthFor("custom_depth_test"));
        }

        [Test]
        public void DepthFor_UnregisteredCustomKey_FallsToPath()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("path", cfg.DepthFor("completely_unknown_key"));
        }

        // ── ModelPresets / GetPresetsForKind ──────────────────────────────────

        [Test]
        public void GetPresetsForKind_EmptyConfig_ReturnsDefaults()
        {
            var store   = new BackendConfigStore(); // ModelPresetsConfig with empty arrays
            var presets = store.GetPresetsForKind(BackendKind.Claude);
            // Must equal the static defaults
            var defaults = ModelPresetDefaults.For(BackendKind.Claude);
            Assert.AreEqual(defaults.Length, presets.Length);
            Assert.AreEqual(defaults[0].label, presets[0].label);
        }

        [Test]
        public void GetPresetsForKind_CustomEntries_WrapsWithDefaultAndCustom()
        {
            var store = new BackendConfigStore();
            store.ModelPresets.Claude = new[]
            {
                new ModelPresetEntry { label = "My Model", modelId = "my-model-id" },
            };
            var presets = store.GetPresetsForKind(BackendKind.Claude);
            // Default + 1 custom + Custom... = 3
            Assert.AreEqual(3, presets.Length);
            Assert.AreEqual("Default",   presets[0].label);
            Assert.AreEqual("",          presets[0].modelId);
            Assert.AreEqual("My Model",  presets[1].label);
            Assert.AreEqual("my-model-id", presets[1].modelId);
            Assert.AreEqual("Custom...", presets[2].label);
            Assert.AreEqual(ModelPresetDefaults.CustomSentinel, presets[2].modelId);
        }

        [Test]
        public void ModelPresetDefaults_AllKinds_HaveDefaultAndCustom()
        {
            foreach (var kind in new[] { BackendKind.Claude, BackendKind.Codex, BackendKind.Antigravity })
            {
                var p = ModelPresetDefaults.For(kind);
                Assert.AreEqual("Default",   p[0].label,              $"{kind}: first must be Default");
                Assert.AreEqual("Custom...", p[p.Length - 1].label,   $"{kind}: last must be Custom...");
                Assert.AreEqual(ModelPresetDefaults.CustomSentinel, p[p.Length - 1].modelId);
            }
        }

        // ── Kimi model ID migration (v0.34.6) ─────────────────────────────────

        [Test]
        public void MigrateKimi_OldK27Code_BecomesKimiForCoding()
        {
            var json = "{\"Kimi\":{\"Model\":\"kimi-k2.7-code\",\"ApprovalMode\":\"\",\"ExtraArgs\":\"\"}}";
            File.WriteAllText(_tempPath, json);
            var store = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("kimi-for-coding", store.Kimi.Model);
        }

        [Test]
        public void MigrateKimi_OldK27CodeHighspeed_BecomesKimiForCoding()
        {
            var json = "{\"Kimi\":{\"Model\":\"kimi-k2.7-code-highspeed\",\"ApprovalMode\":\"\",\"ExtraArgs\":\"\"}}";
            File.WriteAllText(_tempPath, json);
            var store = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("kimi-for-coding", store.Kimi.Model);
        }

        [Test]
        public void MigrateKimi_OldK26_BecomesK2p6()
        {
            var json = "{\"Kimi\":{\"Model\":\"kimi-k2.6\",\"ApprovalMode\":\"\",\"ExtraArgs\":\"\"}}";
            File.WriteAllText(_tempPath, json);
            var store = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("k2p6", store.Kimi.Model);
        }

        [Test]
        public void MigrateKimi_OldK25_BecomesK2p5()
        {
            var json = "{\"Kimi\":{\"Model\":\"kimi-k2.5\",\"ApprovalMode\":\"\",\"ExtraArgs\":\"\"}}";
            File.WriteAllText(_tempPath, json);
            var store = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("k2p5", store.Kimi.Model);
        }

        [Test]
        public void MigrateKimi_NewId_Unchanged()
        {
            var json = "{\"Kimi\":{\"Model\":\"kimi-for-coding\",\"ApprovalMode\":\"\",\"ExtraArgs\":\"\"}}";
            File.WriteAllText(_tempPath, json);
            var store = BackendConfigStore.Load(_tempPath);
            Assert.AreEqual("kimi-for-coding", store.Kimi.Model);
        }

        // ── BackendConfigStore.WithModel (C10) ───────────────────────────────────

        [Test]
        public void WithModel_EmptyModel_ReturnsSelf()
        {
            var store = new BackendConfigStore();
            Assert.AreSame(store, store.WithModel(BackendKind.Claude, ""));
        }

        [Test]
        public void WithModel_CustomSentinel_ReturnsSelf()
        {
            var store = new BackendConfigStore();
            Assert.AreSame(store, store.WithModel(BackendKind.Claude, "__custom__"));
        }

        [Test]
        public void WithModel_Claude_SameModel_ReturnsSelf()
        {
            var store = new BackendConfigStore { Claude = new ClaudeBackendConfig { Model = "opus" } };
            Assert.AreSame(store, store.WithModel(BackendKind.Claude, "opus"));
        }

        [Test]
        public void WithModel_Claude_NewModel_ChangesOnlyClaude()
        {
            var store = new BackendConfigStore
            {
                Claude = new ClaudeBackendConfig { Model = "a", PermissionMode = "plan", ExtraArgs = "-x" },
                Codex  = new CodexBackendConfig  { Model = "codex" },
                InactivityTimeoutSec = 300,
            };
            var result = store.WithModel(BackendKind.Claude, "opus");
            Assert.AreEqual("opus",  result.Claude.Model);
            Assert.AreEqual("plan",  result.Claude.PermissionMode);
            Assert.AreEqual("-x",    result.Claude.ExtraArgs);
            Assert.AreSame(store.Codex, result.Codex);           // other backends untouched
            Assert.AreEqual(300,     result.InactivityTimeoutSec);
        }

        [Test]
        public void WithModel_Codex_NewModel_ChangesOnlyCodex()
        {
            var store = new BackendConfigStore
            {
                Codex = new CodexBackendConfig { Model = "old", PermissionMode = "danger-full-access", StartupTimeoutSec = 45, ExtraArgs = "" },
                InactivityTimeoutSec = 99,
            };
            var result = store.WithModel(BackendKind.Codex, "o3");
            Assert.AreEqual("o3",                result.Codex.Model);
            Assert.AreEqual("danger-full-access", result.Codex.PermissionMode);
            Assert.AreEqual(45,                  result.Codex.StartupTimeoutSec);
            Assert.AreSame(store.Claude,         result.Claude);
            Assert.AreEqual(99,                  result.InactivityTimeoutSec);
        }

        [Test]
        public void WithModel_Antigravity_NewModel_ChangesOnlyAntigravity()
        {
            var store = new BackendConfigStore
            {
                Antigravity = new AntigravityBackendConfig { Model = "old", ApprovalMode = "yolo", Sandbox = true, ExtraArgs = "" },
            };
            var result = store.WithModel(BackendKind.Antigravity, "new-agy");
            Assert.AreEqual("new-agy", result.Antigravity.Model);
            Assert.AreEqual("yolo",    result.Antigravity.ApprovalMode);
            Assert.AreEqual(true,      result.Antigravity.Sandbox);
        }

        [Test]
        public void WithModel_Kimi_NewModel_ChangesOnlyKimi()
        {
            var store = new BackendConfigStore
            {
                Kimi = new KimiBackendConfig { Model = "old", ApprovalMode = "plan", ExtraArgs = "" },
            };
            var result = store.WithModel(BackendKind.Kimi, "kimi-k3");
            Assert.AreEqual("kimi-k3", result.Kimi.Model);
            Assert.AreEqual("plan",    result.Kimi.ApprovalMode);
        }

        [Test]
        public void WithModel_OpenCode_NewModel_ChangesOnlyOpenCode()
        {
            var store = new BackendConfigStore
            {
                OpenCode = new OpenCodeBackendConfig { Model = "old", SkipPermissions = true, ExtraArgs = "" },
            };
            var result = store.WithModel(BackendKind.OpenCode, "anthropic/claude-opus-4");
            Assert.AreEqual("anthropic/claude-opus-4", result.OpenCode.Model);
            Assert.AreEqual(true,                      result.OpenCode.SkipPermissions);
        }

        [Test]
        public void WithModel_UnknownKind_ReturnsSelf()
        {
            var store = new BackendConfigStore();
            Assert.AreSame(store, store.WithModel((BackendKind)999, "model"));
        }

        private sealed class FakeDepthProvider : IChipKindProvider
        {
            public string Key          => "custom_depth_test";
            public int    Priority     => 10;
            public string IconName     => "";
            public string HexColor     => "#000";
            public string DefaultDepth => "summary"; // non-path to verify the routing
            public string[] BarePathExtensions => System.Array.Empty<string>();
            public bool   CanHandle(UnityEngine.Object o, string p) => false;
            public ChipData Create(UnityEngine.Object o, string p) => default;
            public string FormatPayload(ChipData c, ChipPayloadContext x) => "";
            public void   Navigate(string r) { }
            public void   Ping(string r) { }
            public void   AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string r) { }
        }
    }
}
