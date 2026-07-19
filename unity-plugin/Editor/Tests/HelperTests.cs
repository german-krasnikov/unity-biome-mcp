// TDD — MaterialHelper, PrefabHelper, SceneHelper, SpatialHelper, MCPServer helpers.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UnityMCP.Editor.Tests
{
    // ── MaterialHelper.FormatProperty — all 6 type branches ─────────────────

    [TestFixture]
    public class MaterialHelperFormatPropertyTests
    {
        private Material _mat;

        // FormatProperty signature: (Material mat, string name, ShaderPropertyType type)
        private static string Invoke(Material mat, string name, ShaderPropertyType type)
        {
            var method = typeof(MaterialHelper).GetMethod(
                "FormatProperty",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (string)method.Invoke(null, new object[] { mat, name, type });
        }

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Standard")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Hidden/InternalErrorShader");
            Assume.That(shader, Is.Not.Null, "No usable shader found");
            _mat = new Material(shader);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_mat);

        // Color branch
        [Test]
        public void FormatProperty_Color_ContainsColorTag()
        {
            _mat.SetColor("_Color", Color.red);
            var result = Invoke(_mat, "_Color", ShaderPropertyType.Color);
            StringAssert.Contains("[Color]", result);
            StringAssert.Contains("_Color", result);
        }

        // Float branch
        [Test]
        public void FormatProperty_Float_ContainsFloatTag()
        {
            _mat.SetFloat("_Glossiness", 0.5f);
            var result = Invoke(_mat, "_Glossiness", ShaderPropertyType.Float);
            StringAssert.Contains("[Float]", result);
            StringAssert.Contains("_Glossiness", result);
        }

        // Range branch (maps to Float tag)
        [Test]
        public void FormatProperty_Range_ContainsFloatTag()
        {
            _mat.SetFloat("_Glossiness", 0.3f);
            var result = Invoke(_mat, "_Glossiness", ShaderPropertyType.Range);
            StringAssert.Contains("[Float]", result);
        }

        // Float uses invariant decimal separator
        [Test]
        public void FormatProperty_Float_InvariantDecimalSeparator()
        {
            var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");
                _mat.SetFloat("_Glossiness", 0.75f);
                var result = Invoke(_mat, "_Glossiness", ShaderPropertyType.Float);
                // Should not use comma as decimal separator
                StringAssert.DoesNotContain("0,75", result);
                StringAssert.Contains("0.75", result);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        // Texture branch — no texture assigned, path should be empty string
        [Test]
        public void FormatProperty_Texture_ContainsTextureTag()
        {
            var result = Invoke(_mat, "_MainTex", ShaderPropertyType.Texture);
            StringAssert.Contains("[Texture]", result);
            StringAssert.Contains("_MainTex", result);
        }

        // Vector branch
        [Test]
        public void FormatProperty_Vector_ContainsVectorTag()
        {
            _mat.SetVector("_EmissionColor", new Vector4(1f, 0f, 0f, 1f));
            var result = Invoke(_mat, "_EmissionColor", ShaderPropertyType.Vector);
            StringAssert.Contains("[Vector]", result);
            StringAssert.Contains("_EmissionColor", result);
        }

        // Unknown/fallback branch
        [Test]
        public void FormatProperty_Unknown_ContainsQuestionMark()
        {
            // Use a cast to an out-of-range enum value to hit the default branch
            var unknownType = (ShaderPropertyType)99;
            var result = Invoke(_mat, "_SomeProp", unknownType);
            StringAssert.Contains("?", result);
            StringAssert.Contains("_SomeProp", result);
        }
    }

    // ── PrefabHelper — pure-logic branch ────────────────────────────────────

    [TestFixture]
    public class PrefabHelperExecuteTests
    {
        [Test]
        public void Execute_InvalidAction_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PrefabHelper.Execute("invalid_action", "{}"));
            StringAssert.Contains("invalid_action", ex.Message);
        }

        [Test]
        public void Execute_EditAction_MissingAssetPath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PrefabHelper.Execute("edit", "{}"));
            StringAssert.Contains("asset_path is required", ex.Message);
        }

        [Test]
        public void Execute_EditAction_InvalidPrefabPath_ThrowsArgumentException()
        {
            // PrefabUtility.LoadPrefabContents throws ArgumentException if path doesn't exist
            var ex = Assert.Throws<ArgumentException>(() =>
                PrefabHelper.Execute("edit",
                    "{\"asset_path\":\"Assets/DoesNotExist.prefab\",\"component\":\"BoxCollider\"}"));
            StringAssert.Contains("DoesNotExist.prefab", ex.Message);
        }

        [Test]
        public void Execute_EditAction_RoundTrip_PersistsPropertyChange()
        {
            var tmpPath = "Assets/MCPTests/EditRoundTrip.prefab";
            AssetHelper.EnsureDirectory(tmpPath);

            var go = new GameObject("EditTarget");
            go.AddComponent<BoxCollider>();
            PrefabUtility.SaveAsPrefabAsset(go, tmpPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.Refresh();

            try
            {
                var argsJson = "{\"asset_path\":\"" + tmpPath + "\"," +
                               "\"component\":\"BoxCollider\"," +
                               "\"prop\":\"m_IsTrigger\",\"value\":\"true\"}";
                var result = PrefabHelper.Execute("edit", argsJson);
                StringAssert.Contains("ok", result);

                AssetDatabase.ImportAsset(tmpPath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(tmpPath);
                var col = loaded.GetComponent<BoxCollider>();
                Assert.IsTrue(col.isTrigger, "isTrigger should be true after prefab edit");
            }
            finally
            {
                AssetDatabase.DeleteAsset(tmpPath);
                if (AssetDatabase.IsValidFolder("Assets/MCPTests"))
                    AssetDatabase.DeleteAsset("Assets/MCPTests");
            }
        }

        [Test]
        public void Execute_EditAction_PathTargetsPrefabChild_NotRoot()
        {
            var tmpPath = "Assets/TestsTemp/PrefabChildTarget.prefab";
            TestPaths.EnsureFolder("Assets/TestsTemp");

            var root = new GameObject("PrefabRoot");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, tmpPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.Refresh();

            try
            {
                var argsJson = "{\"asset_path\":\"" + tmpPath + "\"," +
                               "\"path\":\"Child\"," +
                               "\"component\":\"Transform\"," +
                               "\"prop\":\"localScale\",\"value\":\"2,2,2\"}";
                var result = PrefabHelper.Execute("edit", argsJson);
                StringAssert.Contains("ok", result);

                AssetDatabase.ImportAsset(tmpPath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(tmpPath);
                Assert.AreEqual(Vector3.one, loaded.transform.localScale);
                Assert.AreEqual(new Vector3(2f, 2f, 2f), loaded.transform.Find("Child").localScale);
            }
            finally
            {
                AssetDatabase.DeleteAsset(tmpPath);
            }
        }
    }

    [TestFixture]
    public class UIHelperCreateTests : SceneTestBase
    {
        [Test]
        public void CreateText_WithExplicitCanvasParent_Succeeds()
        {
            UIHelper.CreateUI("Canvas", "MCP_UI_Canvas", null, null, null, null, null, null, null, null);

            var result = UIHelper.CreateUI("Text", "MCP_UI_Title", "/MCP_UI_Canvas",
                null, null, null, null, null, "Hello", "32");

            StringAssert.Contains("Created /MCP_UI_Canvas/MCP_UI_Title", result);
            var textGo = ComponentSerializer.FindObject("/MCP_UI_Canvas/MCP_UI_Title");
            Assert.IsNotNull(textGo);
            Assert.IsTrue(
                textGo.GetComponent<Text>() != null || textGo.GetComponent("TextMeshProUGUI") != null,
                "Text object should have TMP or legacy Text component");
        }
    }

    [TestFixture]
    public class TimelineHelperCreateTests : SceneTestBase
    {
        const string TimelinePath = "Assets/TestsTemp/TimelinePathCreatesDirector.playable";

        [TearDown]
        public void DeleteTimelineAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TimelinePath) != null)
                AssetDatabase.DeleteAsset(TimelinePath);
        }

        [Test]
        public void Process_CreateTimeline_UsesPathAsDirectorWhenDirectorPathOmitted()
        {
            TestPaths.EnsureFolder("Assets/TestsTemp");
            var json = "{\"cmd\":\"timeline\",\"id\":\"tl1\",\"args\":{\"action\":\"create\",\"path\":\"/MCP_TimelineDirector\",\"asset_path\":\"" + TimelinePath + "\"}}";

            var result = CommandRouter.Process(json);

            var directorGo = ComponentSerializer.FindObject("/MCP_TimelineDirector");
            Assert.IsNotNull(directorGo, result);
            var director = directorGo.GetComponent<PlayableDirector>();
            Assert.IsNotNull(director);
            Assert.IsNotNull(director.playableAsset);
            StringAssert.Contains("/MCP_TimelineDirector", result);
        }
    }

    // ── ErrorHelper — prefab asset hint ─────────────────────────────────────

    [TestFixture]
    public class ErrorHelperPrefabHintTests
    {
        [SetUp]
        public void SetUp() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void ObjectNotFound_AssetsPrefabPath_ContainsPrefabHint()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var msg = ErrorHelper.ObjectNotFound("Assets/Prefabs/Enemy.prefab");
            StringAssert.Contains("prefab(action=\"edit\"", msg);
        }

        [Test]
        public void ObjectNotFound_AssetPathNonPrefab_ContainsGenericAssetHint()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var msg = ErrorHelper.ObjectNotFound("Assets/Data/Config.asset");
            StringAssert.Contains("Asset paths are not scene objects", msg);
        }

        [Test]
        public void ObjectNotFound_ScenePath_NoAssetHint()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var msg = ErrorHelper.ObjectNotFound("Player/Health");
            StringAssert.DoesNotContain("Assets/", msg);
        }
    }

    // ── SceneHelper — pure guard branches ───────────────────────────────────

    [TestFixture]
    public class SceneHelperGuardTests
    {
        [SetUp]
        public void SetUp() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [TearDown]
        public void TearDown() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // SaveScene with no path on untitled scene throws
        [Test]
        public void SaveScene_NullPath_UntitledScene_ThrowsArgumentException()
        {
            // Create a fresh untitled scene so scene.path is empty
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ex = Assert.Throws<ArgumentException>(() => SceneHelper.SaveScene(null));
            StringAssert.Contains("untitled", ex.Message);
        }

        // OpenScene with null/empty path throws
        [Test]
        public void OpenScene_NullPath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => SceneHelper.OpenScene(null));
            StringAssert.Contains("path required", ex.Message);
        }

        [Test]
        public void OpenScene_EmptyPath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => SceneHelper.OpenScene(""));
            StringAssert.Contains("path required", ex.Message);
        }

        // OpenScene with non-existent path throws with file name in message
        [Test]
        public void OpenScene_NonexistentPath_ThrowsWithPathInMessage()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                SceneHelper.OpenScene("Assets/DoesNotExist.unity"));
            StringAssert.Contains("Scene not found", ex.Message);
        }

        // SaveScene with an explicit path on untitled scene returns the path
        [Test]
        public void SaveScene_WithPath_ReturnsThatPath()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var tmpPath = TestPaths.TempFolder + "/_HelperTest_Temp.unity";
            TestPaths.EnsureFolder();
            try
            {
                var result = SceneHelper.SaveScene(tmpPath);
                Assert.AreEqual(tmpPath, result);
            }
            finally
            {
                // Cleanup: load a new scene then delete the file
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
                if (File.Exists(tmpPath + ".meta")) File.Delete(tmpPath + ".meta");
                AssetDatabase.Refresh();
            }
        }
    }

    // ── SpatialHelper — path-not-found and truncation ───────────────────────

    [TestFixture]
    public class SpatialHelperEdgeCaseTests : SceneTestBase
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private GameObject Make(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            _created.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _created.Clear();
        }

        // ObjectsInRadius: invalid path throws
        [Test]
        public void ObjectsInRadius_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                SpatialHelper.ObjectsInRadius("/NoSuchObject_XYZ", 5f));
        }

        // Nearest: invalid path throws
        [Test]
        public void Nearest_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                SpatialHelper.Nearest("/NoSuchObject_XYZ", ""));
        }

        // InFrontOf: invalid path throws
        [Test]
        public void InFrontOf_InvalidPath_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                SpatialHelper.InFrontOf("/NoSuchObject_XYZ", 1f));
        }

        // ObjectsInRadius defaults to 20 shown entries and adds "...+N more"
        [Test]
        public void ObjectsInRadius_Over20Objects_TruncatesWithEllipsis()
        {
            var anchor = Make("SpatialEdge_Anchor", Vector3.zero);
            // Create 22 objects at distance 1 so all are within radius 5
            for (int i = 0; i < 22; i++)
                Make($"SpatialEdge_Near_{i}", new Vector3(i * 0.01f, 0f, 0f));

            var result = SpatialHelper.ObjectsInRadius("/" + anchor.name, 5f);
            StringAssert.Contains("...+2 more", result);
        }

        [Test]
        public void ObjectsInRadius_ExplicitCap_LimitsShownEntries()
        {
            var anchor = Make("SpatialCap_Anchor", new Vector3(2000f, 0f, 0f));
            for (int i = 0; i < 8; i++)
                Make($"SpatialCap_Near_{i}", new Vector3(2000f + i * 0.01f, 0f, 0f));

            var result = SpatialHelper.ObjectsInRadius("/" + anchor.name, 5f, cap: 5);
            StringAssert.Contains("8 objects within 5m (showing 5)", result);
            StringAssert.Contains("...+3 more", result);
            StringAssert.DoesNotContain("SpatialCap_Near_7", result);
        }

        // ObjectsInRadius 19 objects — no truncation (cap is >= 20)
        [Test]
        public void ObjectsInRadius_Under20Objects_NoEllipsis()
        {
            var anchor = Make("SpatialEdge19_Anchor", new Vector3(1000f, 0f, 0f));
            for (int i = 0; i < 19; i++)
                Make($"SpatialEdge19_Near_{i}", new Vector3(1000f + i * 0.01f, 0f, 0f));

            var result = SpatialHelper.ObjectsInRadius("/" + anchor.name, 5f);
            StringAssert.DoesNotContain("...+more", result);
        }
    }

    // ── MCPServer.FormatStatusResponse — pure string formatting ──────────────

    [TestFixture]
    public class MCPServerStatusTests
    {

        [Test]
        public void FormatStatusResponse_NotCompiling_IdleState()
        {
            var result = MCPServer.FormatStatusResponse("abc", false, 0.0);
            StringAssert.Contains("\"id\":\"abc\"", result);
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("idle|0", result);
            StringAssert.Contains("\"compile\":false", result);
        }

        [Test]
        public void FormatStatusResponse_Compiling_IncludesElapsed()
        {
            var result = MCPServer.FormatStatusResponse("xyz", true, 3.5);
            StringAssert.Contains("compiling|3.5", result);
            StringAssert.Contains("\"compile\":true", result);
        }

        [Test]
        public void FormatStatusResponse_Compiling_ElapsedUsesInvariantDecimalSeparator()
        {
            var saved = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture =
                    new System.Globalization.CultureInfo("de-DE");
                var result = MCPServer.FormatStatusResponse("id1", true, 2.7);
                // Must not use comma as decimal separator
                StringAssert.DoesNotContain("2,7", result);
                StringAssert.Contains("2.7", result);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void FormatStatusResponse_NullId_EmptyIdInJson()
        {
            var result = MCPServer.FormatStatusResponse(null, false, 0.0);
            StringAssert.Contains("\"id\":\"\"", result);
        }

        // WriteStateFile + DeleteStateFile round-trip
        [Test]
        public void WriteStateFile_CreatesFile_DeleteStateFile_RemovesIt()
        {
            // Use internal access (internal methods)
            var port = MCPServer.ServerPort;
            var statePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-mcp", "state", $"port-{port}.state");

            try
            {
                MCPServer.WriteStateFile("test_state");
                Assert.IsTrue(File.Exists(statePath), "State file should exist after WriteStateFile");

                var contents = File.ReadAllText(statePath);
                StringAssert.Contains("test_state", contents);

                MCPServer.DeleteStateFile();
                Assert.IsFalse(File.Exists(statePath), "State file should be gone after DeleteStateFile");
            }
            finally
            {
                if (File.Exists(statePath)) File.Delete(statePath);
            }
        }

        [Test]
        public void WriteStateFile_ContainsPidAndTimestamp()
        {
            MCPServer.WriteStateFile("ready");
            var port = MCPServer.ServerPort;
            var statePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-mcp", "state", $"port-{port}.state");

            try
            {
                var lines = File.ReadAllLines(statePath);
                // State file: line 0=state, 1=timestamp, 2=pid, 3=epoch (added v0.21)
                Assert.GreaterOrEqual(lines.Length, 3, "State file should have at least 3 lines: state, ts, pid");
                Assert.AreEqual("ready", lines[0]);
                Assert.IsTrue(double.TryParse(lines[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _),
                    "Second line should be a parseable timestamp");
                Assert.IsTrue(int.TryParse(lines[2], out var pid) && pid > 0,
                    "Third line should be a positive PID");
            }
            finally
            {
                MCPServer.DeleteStateFile();
            }
        }

        // CP-7: MainThreadDispatcher's queue must be a ConcurrentQueue<Action> (drain-safe by design).
        // Does NOT call Stop() — that kills the live TCP server and breaks subsequent tests.
        // Phase 2 M1: queue moved from MCPServer._mainThreadQueue to MainThreadDispatcher._queue.
        [Test]
        public void MainThreadQueue_Exists_AndIsConcurrentQueue()
        {
            var qField = typeof(MainThreadDispatcher).GetField("_queue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(qField, "MainThreadDispatcher._queue field must exist");

            var queue = qField.GetValue(null)
                as System.Collections.Concurrent.ConcurrentQueue<System.Action>;
            Assert.IsNotNull(queue, "MainThreadDispatcher._queue must be ConcurrentQueue<Action>");
        }
    }
}
