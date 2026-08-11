// TDD tests for SceneChipProvider name-only path format.
// D1-D8: verifies chip.Path == scene name, FormatPayload, Navigate, ResolveExists.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneChipDisplayTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        SceneChipProvider _provider;
        string _realScenePath;
        SceneAsset _realSceneAsset;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            _provider = new SceneChipProvider();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            if (guids.Length == 0) return;
            _realScenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            _realSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_realScenePath);
        }

        [TearDown]
        public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // D1
        [Test]
        public void Create_SceneAsset_PathIsSceneName()
        {
            if (_realSceneAsset == null) Assert.Ignore("No Scene asset found");
            var chip = _provider.Create(_realSceneAsset, _realScenePath);
            Assert.AreEqual(_realSceneAsset.name, chip.Path);
        }

        // D2
        [Test]
        public void Create_NullObj_PathFromFileNameWithoutExtension()
        {
            var chip = _provider.Create(null, "Assets/Scenes/FooBar.unity");
            Assert.AreEqual("FooBar", chip.Path);
        }

        // D3
        [Test]
        public void Create_DisplayName_EqualsPath()
        {
            var chip = _provider.Create(null, "Assets/Scenes/FooBar.unity");
            Assert.AreEqual(chip.Path, chip.DisplayName);
        }

        // D4
        [Test]
        public void FormatPayload_PathDepth_ReturnsBracketWithName()
        {
            var chip = new ChipData(ChipKindKeys.Scene, "MyScene", "MyScene", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[scene:MyScene]", result);
        }

        // D5
        [Test]
        public void FormatPayload_NoneDepth_ReturnsEmpty()
        {
            var chip = new ChipData(ChipKindKeys.Scene, "MyScene", "MyScene", 0);
            var result = _provider.FormatPayload(chip, new ChipPayloadContext("none", ""));
            Assert.AreEqual("", result);
        }

        // D6
        [Test]
        public void FormatAsRef_SceneAsset_ReturnsBracketWithNameOnly()
        {
            if (_realSceneAsset == null) Assert.Ignore("No Scene asset found");
            var result = ChipContextResolver.FormatAsRef(_realSceneAsset);
            StringAssert.StartsWith("[scene:", result);
            Assert.IsFalse(result.Contains("/"), $"Expected name-only ref, got: {result}");
        }

        // D7
        [Test]
        public void Navigate_NameOnly_DoesNotThrow()
            => Assert.DoesNotThrow(() => _provider.Navigate("SampleScene"));

        // D8
        [Test]
        public void ResolveExists_NameOnly_ReturnsTrueForRealScene()
        {
            if (_realSceneAsset == null) Assert.Ignore("No Scene asset found");
            var service = new ChipExistenceService();
            try
            {
                service.Exists(ChipKindKeys.Scene, _realSceneAsset.name);
                service.ForceProcessForTests();
                var result = service.Exists(ChipKindKeys.Scene, _realSceneAsset.name);
                Assert.AreEqual((bool?)true, result, $"Name-only scene '{_realSceneAsset.name}' should be found");
            }
            finally { service.Dispose(); }
        }

        // D9: exact match — FindScenePathByExactName returns the correct asset path
        [Test]
        public void FindScenePathByExactName_ExistingScene_ReturnsExactPath()
        {
            if (_realSceneAsset == null) Assert.Ignore("No Scene asset found");
            var name = _realSceneAsset.name;
            var path = SceneChipProvider.FindScenePathByExactName(name);
            Assert.IsNotNull(path, $"Should find existing scene '{name}'");
            Assert.AreEqual(name, System.IO.Path.GetFileNameWithoutExtension(path),
                "Returned path must be the exact scene, not a substring match");
        }

        // D10: exact match — returns null for a name that does not exist (no substring false positives)
        [Test]
        public void FindScenePathByExactName_Nonexistent_ReturnsNull()
        {
            var path = SceneChipProvider.FindScenePathByExactName("__BiomeTest_NoSuchScene_XYZ__");
            Assert.IsNull(path, "Should return null for non-existent scene");
        }

        // D11: Phase 1.2c — dialog override seam: for an already-loaded scene the dialog is skipped.
        [Test]
        public void Navigate_SceneAlreadyLoaded_DisplayDialogOverrideNotCalled()
        {
            // Find the scene that is currently loaded (bootstrap scene in the test worker).
            var loadedScene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(0);
            if (!loadedScene.isLoaded || string.IsNullOrEmpty(loadedScene.name))
                Assert.Ignore("No loaded scene found in the test worker");

            bool overrideCalled = false;
            SceneChipProvider.DisplayDialogOverride = _ => { overrideCalled = true; return false; };
            RegisterCleanup(() => SceneChipProvider.DisplayDialogOverride = null);

            // Navigate to the currently-open scene — dialog must not be shown.
            Assert.DoesNotThrow(() => _provider.Navigate(loadedScene.name));
            Assert.IsFalse(overrideCalled,
                "DisplayDialogOverride must not be called when the scene is already loaded");
        }

        // D12: Phase 1.2c — dialog override seam: for a scene not found, no dialog is shown.
        [Test]
        public void Navigate_SceneNotFound_DisplayDialogOverrideNotCalled()
        {
            bool overrideCalled = false;
            SceneChipProvider.DisplayDialogOverride = _ => { overrideCalled = true; return false; };
            RegisterCleanup(() => SceneChipProvider.DisplayDialogOverride = null);

            Assert.DoesNotThrow(() => _provider.Navigate("__BiomeTest_NoSuchScene_XYZ__"));
            Assert.IsFalse(overrideCalled,
                "DisplayDialogOverride must not be called when the scene asset is not found");
        }
    }
}
