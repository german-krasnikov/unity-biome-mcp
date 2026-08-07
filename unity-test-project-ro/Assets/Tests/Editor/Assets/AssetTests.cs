using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;
using System.IO;

// Test SO type — defined outside the test classes so Unity can find it
public class TestSOConfig : ScriptableObject
{
    public string configName = "default";
    public int maxHealth = 100;
    public float speed = 5.5f;
}

namespace UnityMCP.TestProject.Assets
{
    /// <summary>
    /// Phase 26a: AssetDatabase operations tests.
    /// </summary>
    [TestFixture]
    public class AssetTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly string TempFolder = "Assets/TestsTemp/AssetTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => LogAssert.ignoreFailingMessages = false);
            LogAssert.ignoreFailingMessages = false;
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
        }

        private static Material CreateTempMaterial(string name)
        {
            var mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, TempFolder + "/" + name + ".mat");
            AssetDatabase.SaveAssets();
            return mat;
        }

        // --- find ---

        [Test]
        public void FindAssets_ByType_ReturnsResults()
        {
            CreateTempMaterial("FindTypeTest");
            string result = CommandRouter.Process(
                "{\"id\":\"a1\",\"cmd\":\"asset\",\"args\":{\"action\":\"find\",\"type\":\"Material\",\"folder\":\"" + TempFolder + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("FindTypeTest.mat", result);
        }

        [Test]
        public void FindAssets_ByName_Filters()
        {
            CreateTempMaterial("MatchMe");
            CreateTempMaterial("OtherMat");
            string result = CommandRouter.Process(
                "{\"id\":\"a2\",\"cmd\":\"asset\",\"args\":{\"action\":\"find\",\"name\":\"MatchMe\",\"folder\":\"" + TempFolder + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("MatchMe", result);
            StringAssert.DoesNotContain("OtherMat", result);
        }

        [Test]
        public void FindAssets_InFolder_Scoped()
        {
            CreateTempMaterial("ScopedMat");
            string found = CommandRouter.Process(
                "{\"id\":\"a3\",\"cmd\":\"asset\",\"args\":{\"action\":\"find\",\"type\":\"Material\",\"folder\":\"" + TempFolder + "\"}}");
            StringAssert.Contains("\"ok\":true", found);
            StringAssert.Contains("ScopedMat.mat", found);

            string notFound = CommandRouter.Process(
                "{\"id\":\"a3b\",\"cmd\":\"asset\",\"args\":{\"action\":\"find\",\"type\":\"Material\",\"folder\":\"Assets/NonExistent\"}}");
            StringAssert.DoesNotContain("ScopedMat.mat", notFound);
        }

        // --- get_info ---

        [Test]
        public void GetInfo_ReturnsTypeAndGuid()
        {
            CreateTempMaterial("InfoTest");
            string path = TempFolder + "/InfoTest.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a4\",\"cmd\":\"asset\",\"args\":{\"action\":\"get_info\",\"path\":\"" + path + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Material", result);
            StringAssert.Contains("guid:", result);
        }

        // --- create ---

        [Test]
        public void CreateFolder_CreatesDirectory()
        {
            string newFolder = TempFolder + "/SubFolder";
            string result = CommandRouter.Process(
                "{\"id\":\"a5\",\"cmd\":\"asset\",\"args\":{\"action\":\"create\",\"type\":\"Folder\",\"path\":\"" + newFolder + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsTrue(AssetDatabase.IsValidFolder(newFolder));
        }

        [Test]
        public void CreateMaterial_CreatesAsset()
        {
            string path = TempFolder + "/CreatedMat.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a6\",\"cmd\":\"asset\",\"args\":{\"action\":\"create\",\"type\":\"Material\",\"path\":\"" + path + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        // --- move ---

        [Test]
        public void MoveAsset_MovesFile()
        {
            CreateTempMaterial("MoveSource");
            string src = TempFolder + "/MoveSource.mat";
            string dst = TempFolder + "/MoveDest.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a7\",\"cmd\":\"asset\",\"args\":{\"action\":\"move\",\"source\":\"" + src + "\",\"dest\":\"" + dst + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(src));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(dst));
        }

        // --- duplicate ---

        [Test]
        public void DuplicateAsset_CopiesFile()
        {
            CreateTempMaterial("DupSource");
            string src = TempFolder + "/DupSource.mat";
            string dst = TempFolder + "/DupCopy.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a8\",\"cmd\":\"asset\",\"args\":{\"action\":\"duplicate\",\"source\":\"" + src + "\",\"dest\":\"" + dst + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(src));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(dst));
        }

        // --- delete ---

        [Test]
        public void DeleteAsset_RemovesFile()
        {
            CreateTempMaterial("DeleteMe");
            string path = TempFolder + "/DeleteMe.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a9\",\"cmd\":\"asset\",\"args\":{\"action\":\"delete\",\"path\":\"" + path + "\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        // --- get_dependencies ---

        [Test]
        public void GetDependencies_ReturnsList()
        {
            CreateTempMaterial("DepMat");
            string path = TempFolder + "/DepMat.mat";
            // recursive=true to include transitive deps (non-recursive may return empty for new assets)
            string result = CommandRouter.Process(
                "{\"id\":\"a10\",\"cmd\":\"asset\",\"args\":{\"action\":\"get_dependencies\",\"path\":\"" + path + "\",\"recursive\":\"true\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("DepMat.mat", result);
        }

        [Test]
        public void GetDependencies_Recursive()
        {
            CreateTempMaterial("RecDepMat");
            string path = TempFolder + "/RecDepMat.mat";
            string result = CommandRouter.Process(
                "{\"id\":\"a11\",\"cmd\":\"asset\",\"args\":{\"action\":\"get_dependencies\",\"path\":\"" + path + "\",\"recursive\":\"true\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotEmpty(result);
        }

        // --- error cases ---

        [Test]
        public void InvalidAction_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            string result = CommandRouter.Process(
                "{\"id\":\"a12\",\"cmd\":\"asset\",\"args\":{\"action\":\"nonsense\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void MissingPath_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            string result = CommandRouter.Process(
                "{\"id\":\"a13\",\"cmd\":\"asset\",\"args\":{\"action\":\"get_info\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }
    }

    [TestFixture]
    public class ScriptableObjectTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private readonly string TempFolder = "Assets/TestsTemp/ScriptableObjectTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => LogAssert.ignoreFailingMessages = false);
            LogAssert.ignoreFailingMessages = false;
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
        }

        [Test]
        public void Create_BasicSO_CreatesAsset()
        {
            var path = TempFolder + "/Config.asset";
            var result = CommandRouter.Process(
                $"{{\"id\":\"so1\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"create\",\"type\":\"TestSOConfig\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TestSOConfig>(path));
        }

        [Test]
        public void Get_ReturnsFields()
        {
            var path = TempFolder + "/ConfigGet.asset";
            var asset = ScriptableObject.CreateInstance<TestSOConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var result = CommandRouter.Process(
                $"{{\"id\":\"so2\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"get\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("configName", result);
            StringAssert.Contains("maxHealth", result);
        }

        [Test]
        public void Set_StringField_Changes()
        {
            var path = TempFolder + "/ConfigStr.asset";
            var asset = ScriptableObject.CreateInstance<TestSOConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            var setResult = CommandRouter.Process(
                $"{{\"id\":\"so3\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"configName\",\"value\":\"NewValue\"}}}}");
            StringAssert.Contains("\"ok\":true", setResult);

            var getResult = CommandRouter.Process(
                $"{{\"id\":\"so3b\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"get\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("NewValue", getResult);
        }

        [Test]
        public void Set_IntField_Changes()
        {
            var path = TempFolder + "/ConfigInt.asset";
            var asset = ScriptableObject.CreateInstance<TestSOConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            CommandRouter.Process(
                $"{{\"id\":\"so4\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"maxHealth\",\"value\":\"200\"}}}}");

            var getResult = CommandRouter.Process(
                $"{{\"id\":\"so4b\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"get\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("200", getResult);
        }

        [Test]
        public void Set_FloatField_Changes()
        {
            var path = TempFolder + "/ConfigFloat.asset";
            var asset = ScriptableObject.CreateInstance<TestSOConfig>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            CommandRouter.Process(
                $"{{\"id\":\"so5\",\"cmd\":\"scriptable_object\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"speed\",\"value\":\"10.5\"}}}}");

            var loaded = AssetDatabase.LoadAssetAtPath<TestSOConfig>(path);
            Assert.AreEqual(10.5f, loaded.speed, 0.001f);
        }

        [Test]
        public void ListTypes_ReturnsTypes()
        {
            // Without filter, TypeCache returns 100s of types — TestSOConfig may not be in first 100.
            // Use filter to reliably find it.
            var result = CommandRouter.Process(
                "{\"id\":\"so6\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"list_types\",\"filter\":\"TestSO\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("TestSOConfig", result);
        }

        [Test]
        public void ListTypes_WithFilter_FiltersResults()
        {
            var result = CommandRouter.Process(
                "{\"id\":\"so7\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"list_types\",\"filter\":\"TestSOConfig\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("TestSOConfig", result);
            // Should not contain unrelated types like AudioMixer
            StringAssert.DoesNotContain("AudioMixer", result);
        }

        [Test]
        public void Find_ReturnsInstances()
        {
            var path = TempFolder + "/FindTimeline.playable";
            TrackOwnedAsset(path);
            var timeline = ScriptableObject.CreateInstance<
                UnityEngine.Timeline.TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
            AssetDatabase.SaveAssets();

            var result = CommandRouter.Process(
                "{\"id\":\"so8\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"find\",\"type\":\"TimelineAsset\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains(path, result);
        }

        [Test]
        public void InvalidAction_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = CommandRouter.Process(
                "{\"id\":\"so9\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"explode\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("Unknown action", result);
        }

        [Test]
        public void NonExistentPath_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = CommandRouter.Process(
                "{\"id\":\"so10\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"get\",\"path\":\"Assets/Missing/NoFile.asset\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("not found", result);
        }

        [Test]
        public void MissingPath_Get_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = CommandRouter.Process(
                "{\"id\":\"so11\",\"cmd\":\"scriptable_object\",\"args\":{\"action\":\"get\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }
    }

    [TestFixture]
    public class PrefabTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private readonly string TempFolder = "Assets/TestsTemp/PrefabTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => LogAssert.ignoreFailingMessages = false);
            LogAssert.ignoreFailingMessages = false;
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
        }

        [Test]
        public void Save_CreatesAsset()  // pf1
        {
            var go = new GameObject("PrefabSaveTest");
            try
            {
                var assetPath = TempFolder + "/PrefabSaveTest.prefab";
                var json = $"{{\"id\":\"pf1\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"save\",\"path\":\"/PrefabSaveTest\",\"asset_path\":\"{assetPath}\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Save_Overwrite_Updates()  // pf2
        {
            var go = new GameObject("PrefabOverwriteTest");
            try
            {
                var assetPath = TempFolder + "/Overwrite.prefab";
                var json = $"{{\"id\":\"pf2a\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"save\",\"path\":\"/PrefabOverwriteTest\",\"asset_path\":\"{assetPath}\"}}}}";
                CommandRouter.Process(json);

                go.tag = "EditorOnly";
                json = $"{{\"id\":\"pf2b\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"save\",\"path\":\"/PrefabOverwriteTest\",\"asset_path\":\"{assetPath}\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.IsNotNull(loaded);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateVariant_CreatesVariant()  // pf3
        {
            var go = new GameObject("VariantBase");
            try
            {
                var basePath = TempFolder + "/VariantBase.prefab";
                CommandRouter.Process($"{{\"id\":\"pf3a\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"save\",\"path\":\"/VariantBase\",\"asset_path\":\"{basePath}\"}}}}");

                var variantPath = TempFolder + "/VariantRed.prefab";
                var json = $"{{\"id\":\"pf3b\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"create_variant\",\"path\":\"dummy\",\"base_path\":\"{basePath}\",\"variant_path\":\"{variantPath}\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);

                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                Assert.IsNotNull(variant);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Revert_RevertsChanges()  // pf5
        {
            var go = new GameObject("RevertTest");
            try
            {
                var assetPath = TempFolder + "/RevertTest.prefab";
                var prefabGo = PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);
                Assert.IsNotNull(prefabGo);

                // Modify the instance
                go.transform.position = new Vector3(1, 2, 3);

                var json = $"{{\"id\":\"pf5\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"revert\",\"path\":\"/RevertTest\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(Vector3.zero, go.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetOverrides_ListsChanges()  // pf6
        {
            var go = new GameObject("OverridesTest");
            try
            {
                var assetPath = TempFolder + "/OverridesTest.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);

                go.transform.position = new Vector3(5, 0, 0);

                var json = $"{{\"id\":\"pf6\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"get_overrides\",\"path\":\"/OverridesTest\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Unpack_BreaksLink()  // pf7
        {
            var go = new GameObject("UnpackTest");
            try
            {
                var assetPath = TempFolder + "/UnpackTest.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(go));

                var json = $"{{\"id\":\"pf7\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"unpack\",\"path\":\"/UnpackTest\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(go));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Unpack_Recursive_BreaksNested()  // pf8
        {
            var go = new GameObject("UnpackRecursiveTest");
            var child = new GameObject("Child");
            child.transform.SetParent(go.transform);
            try
            {
                var assetPath = TempFolder + "/UnpackRecursive.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);
                Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(go));

                var json = $"{{\"id\":\"pf8\",\"cmd\":\"prefab\",\"args\":{{\"action\":\"unpack\",\"path\":\"/UnpackRecursiveTest\",\"recursive\":\"true\"}}}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsFalse(PrefabUtility.IsPartOfPrefabInstance(go));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void InvalidAction_ReturnsError()  // pf9
        {
            LogAssert.ignoreFailingMessages = true;
            var json = "{\"id\":\"pf9\",\"cmd\":\"prefab\",\"args\":{\"action\":\"nonsense\",\"path\":\"/Anything\"}}";
            var result = CommandRouter.Process(json);
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void NonPrefabInstance_Apply_ReturnsError()  // pf10
        {
            var go = new GameObject("NonPrefabApply");
            try
            {
                LogAssert.ignoreFailingMessages = true;
                var json = "{\"id\":\"pf10\",\"cmd\":\"prefab\",\"args\":{\"action\":\"apply\",\"path\":\"/NonPrefabApply\"}}";
                var result = CommandRouter.Process(json);
                LogAssert.ignoreFailingMessages = false;
                StringAssert.Contains("\"ok\":false", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }

}
