// TDD tests for ProcessDraggedObject with SceneAsset.
// B1-B5: DnD a .unity file → produces chip with name-only path and "scene" kind.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class SceneAssetDragTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        List<(Object obj, string path, string name)> _chips;
        string _scenePath;
        SceneAsset _sceneAsset;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            _chips = new List<(Object, string, string)>();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            if (guids.Length == 0) return;
            _scenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
            _sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(_scenePath);
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        void Capture(Object o, string p, string n) => _chips.Add((o, p, n));

        // B1
        [Test]
        public void SceneAsset_Drag_ProducesChip()
        {
            if (_sceneAsset == null) Assert.Ignore("No Scene asset in test project");
            MCPChatWindow.ProcessDraggedObject(_sceneAsset, null, Capture);
            Assert.AreEqual(1, _chips.Count);
        }

        // B2 — path should be the scene name (name-only after Create override)
        [Test]
        public void SceneAsset_ChipPath_IsSceneName()
        {
            if (_sceneAsset == null) Assert.Ignore("No Scene asset in test project");
            MCPChatWindow.ProcessDraggedObject(_sceneAsset, null, Capture);
            Assert.AreEqual(_sceneAsset.name, _chips[0].path);
        }

        // B3
        [Test]
        public void SceneAsset_ChipName_EqualsAssetName()
        {
            if (_sceneAsset == null) Assert.Ignore("No Scene asset in test project");
            MCPChatWindow.ProcessDraggedObject(_sceneAsset, null, Capture);
            Assert.AreEqual(_sceneAsset.name, _chips[0].name);
        }

        // B4
        [Test]
        public void SceneAsset_KindKey_IsScene()
        {
            if (_sceneAsset == null) Assert.Ignore("No Scene asset in test project");
            Assert.AreEqual(ChipKindKeys.Scene, ChipKindDetector.Detect(_sceneAsset, _scenePath));
        }

        // B5
        [Test]
        public void SceneAsset_FormatPayload_ReturnsBracketWithName()
        {
            if (_sceneAsset == null) Assert.Ignore("No Scene asset in test project");
            var provider = ChipKindRegistry.Resolve(_sceneAsset, _scenePath) as SceneChipProvider;
            if (provider == null) Assert.Ignore("SceneChipProvider not resolved");
            var chip = provider.Create(_sceneAsset, _scenePath);
            var result = provider.FormatPayload(chip, new ChipPayloadContext("path", ""));
            StringAssert.StartsWith("[scene:", result);
            Assert.IsFalse(result.Contains("/"), $"Expected name-only bracket, got: {result}");
        }
    }
}
