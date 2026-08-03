// TDD tests for ChipKindDetector — pure unit tests using EditMode Unity objects.
// H6: ChipKind enum removed; assertions use ChipKindKeys string constants.
// ShortPrefix removed — Key IS the prefix.
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipKindDetectorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]  public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
        }

        // ── Scene GameObject ──────────────────────────────────────────────────

        [Test]
        public void Detect_SceneGameObject_ReturnsHierarchy()
        {
            var go = new GameObject("TestGO");
            try
            {
                Assert.AreEqual(ChipKindKeys.Hierarchy, ChipKindDetector.Detect(go, null));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── Script asset ──────────────────────────────────────────────────────

        [Test]
        public void Detect_MonoScript_ReturnsScript()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            if (guids.Length == 0) Assert.Ignore("No MonoScript assets found");
            var path   = AssetDatabase.GUIDToAssetPath(guids[0]);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.IsNotNull(script);
            Assert.AreEqual(ChipKindKeys.Script, ChipKindDetector.Detect(script, path));
        }

        // ── Scene asset ───────────────────────────────────────────────────────

        [Test]
        public void Detect_SceneAsset_ReturnsScene()
        {
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Assert.AreEqual(ChipKindKeys.Scene, ChipKindDetector.Detect(so, "Assets/Levels/Test.unity"));
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Prefab ────────────────────────────────────────────────────────────

        [Test]
        public void Detect_PrefabPath_ReturnsPrefab()
        {
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Assert.AreEqual(ChipKindKeys.Prefab, ChipKindDetector.Detect(so, "Assets/Prefabs/Enemy.prefab"));
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Material ──────────────────────────────────────────────────────────

        [Test]
        public void Detect_Material_ReturnsMaterial()
        {
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            try
            {
                Assert.AreEqual(ChipKindKeys.Material, ChipKindDetector.Detect(mat, "Assets/Materials/Lava.mat"));
            }
            finally { Object.DestroyImmediate(mat); }
        }

        // ── Texture ───────────────────────────────────────────────────────────

        [Test]
        public void Detect_Texture2D_ReturnsTexture()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                Assert.AreEqual(ChipKindKeys.Texture, ChipKindDetector.Detect(tex, "Assets/Textures/Icon.png"));
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ── ScriptableObject ─────────────────────────────────────────────────

        [Test]
        public void Detect_ScriptableObject_ReturnsSO()
        {
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                Assert.AreEqual(ChipKindKeys.ScriptableObject, ChipKindDetector.Detect(so, "Assets/Data/Config.asset"));
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Unknown / fallback ────────────────────────────────────────────────

        [Test]
        public void Detect_UnknownAsset_ReturnsAsset()
        {
            var mesh = new Mesh();
            try
            {
                Assert.AreEqual(ChipKindKeys.Asset, ChipKindDetector.Detect(mesh, "Assets/Models/Cube.fbx"));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Detect_NullObject_ReturnsAsset()
        {
            Assert.AreEqual(ChipKindKeys.Asset, ChipKindDetector.Detect(null, "anything"));
        }

        // ── Key is the prefix ─────────────────────────────────────────────────

        [Test]
        public void Detect_ReturnsNonEmptyKey()
        {
            var go = new GameObject("TestGO");
            try
            {
                var key = ChipKindDetector.Detect(go, null);
                Assert.IsFalse(string.IsNullOrEmpty(key), "Detect must return a non-empty key");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Detect_Hierarchy_KeyIsHierarchy()
        {
            var go = new GameObject("TestGO2");
            try
            {
                Assert.AreEqual("hierarchy", ChipKindDetector.Detect(go, null));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
