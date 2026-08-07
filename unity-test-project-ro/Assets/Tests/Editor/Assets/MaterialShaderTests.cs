using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;
using System.Text.RegularExpressions;

namespace UnityMCP.TestProject.Assets
{
    /// <summary>
    /// Phase 26c + 20a/b/c/d/e/f: Material and Shader tests.
    /// </summary>
    [TestFixture]
    public class MaterialShaderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/MaterialShaderTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => LogAssert.ignoreFailingMessages = false);
            TrackOwnedAsset(TempFolder);
            LogAssert.ignoreFailingMessages = true;
            TestPaths.EnsureFolder(TempFolder);
        }

        // =====================================================================
        // Material Tests (from MCPMaterialTests)
        // =====================================================================

        [Test]
        public void Create_DefaultShader_CreatesAsset()
        {
            var path = TempFolder + "/M1.mat";
            var result = CommandRouter.Process(
                $"{{\"id\":\"m1\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(path));
        }

        [Test]
        public void Create_CustomShader_UsesShader()
        {
            var path = TempFolder + "/M2.mat";
            var result = CommandRouter.Process(
                $"{{\"id\":\"m2\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Unlit/Color\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(mat);
            StringAssert.Contains("Unlit", mat.shader.name);
        }

        [Test]
        public void Get_AssetPath_ListsProperties()
        {
            var path = TempFolder + "/M3.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m3a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m3b\",\"cmd\":\"material\",\"args\":{{\"action\":\"get\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            // Standard/URP shaders expose color or other props
            Assert.IsTrue(result.Contains("_Color") || result.Contains("_BaseColor") || result.Contains("properties"));
        }

        [Test]
        public void Get_SceneObject_ListsProperties()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "MatTestCube";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"m4\",\"cmd\":\"material\",\"args\":{\"action\":\"get\",\"object_path\":\"/MatTestCube\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsTrue(result.Contains("Shader:") || result.Contains("_Color") || result.Contains("_BaseColor"),
                    "Expected shader/property info. Got: " + result.Substring(0, System.Math.Min(200, result.Length)));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Set_Float_ChangesValue()
        {
            var path = TempFolder + "/M5.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m5a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Standard\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m5b\",\"cmd\":\"material\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"_Glossiness\",\"value\":\"0.8\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(mat.GetFloat("_Glossiness"), Is.EqualTo(0.8f).Within(0.01f));
        }

        [Test]
        public void Set_Color_Hex_ChangesValue()
        {
            var path = TempFolder + "/M6.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m6a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Standard\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m6b\",\"cmd\":\"material\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"_Color\",\"value\":\"#FF0000FF\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var c = mat.GetColor("_Color");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Set_Color_Tuple_ChangesValue()
        {
            var path = TempFolder + "/M7.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m7a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Standard\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m7b\",\"cmd\":\"material\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"_Color\",\"value\":\"(1,0,0,1)\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var c = mat.GetColor("_Color");
            Assert.That(c.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(c.g, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Set_Keyword_EnablesDisables()
        {
            var path = TempFolder + "/M8.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m8a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Standard\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m8b\",\"cmd\":\"material\",\"args\":{{\"action\":\"set\",\"path\":\"{path}\",\"prop\":\"_EMISSION\",\"value\":\"true\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsTrue(mat.IsKeywordEnabled("_EMISSION"));
        }

        [Test]
        public void Copy_TransfersMaterial()
        {
            var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.name = "MatCopySource";
            var target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            target.name = "MatCopyTarget";

            // Assign a custom material to source
            var matPath = TempFolder + "/M9.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m9a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{matPath}\",\"shader\":\"Unlit/Color\"}}}}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            source.GetComponent<Renderer>().sharedMaterial = mat;

            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"m9b\",\"cmd\":\"material\",\"args\":{\"action\":\"copy\",\"source\":\"/MatCopySource\",\"targets\":\"/MatCopyTarget\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.AreEqual(mat, target.GetComponent<Renderer>().sharedMaterial);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ListProperties_ShowsAllTypes()
        {
            var path = TempFolder + "/M10.mat";
            CommandRouter.Process(
                $"{{\"id\":\"m10a\",\"cmd\":\"material\",\"args\":{{\"action\":\"create\",\"path\":\"{path}\",\"shader\":\"Standard\"}}}}");
            var result = CommandRouter.Process(
                $"{{\"id\":\"m10b\",\"cmd\":\"material\",\"args\":{{\"action\":\"list_properties\",\"path\":\"{path}\"}}}}");
            StringAssert.Contains("\"ok\":true", result);
            // Standard shader always has _Color and _MainTex
            Assert.IsTrue(result.Contains("_Color") || result.Contains("_BaseColor") || result.Contains("Float") || result.Contains("Texture"));
        }

        [Test]
        public void Material_InvalidAction_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = CommandRouter.Process(
                "{\"id\":\"m11\",\"cmd\":\"material\",\"args\":{\"action\":\"nonsense\",\"path\":\"Assets/Foo.mat\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void MissingPathAndObjectPath_ReturnsError()
        {
            LogAssert.ignoreFailingMessages = true;
            var result = CommandRouter.Process(
                "{\"id\":\"m12\",\"cmd\":\"material\",\"args\":{\"action\":\"get\"}}");
            LogAssert.ignoreFailingMessages = false;
            StringAssert.Contains("\"ok\":false", result);
        }

        // =====================================================================
        // Shader Tests (from MCPShaderTests)
        // =====================================================================

        // --- Shader serialization via scene object ---

        [Test]
        public void ShaderGet_SceneObject_ContainsShaderName()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestObj";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh1\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestObj\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Shader:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_SceneObject_ContainsPropertiesSection()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestProps";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh2\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestProps\"}}");
                StringAssert.Contains("properties:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_SceneObject_ContainsPassCount()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestPass";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh3\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestPass\"}}");
                StringAssert.Contains("passes:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_SceneObject_ContainsErrorsLine()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ShTestErrors";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh3b\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestErrors\"}}");
                StringAssert.Contains("errors:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- Material serialization ---

        [Test]
        public void ShaderGet_MaterialTarget_ContainsMaterialHeader()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestMat";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh4\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestMat\",\"target\":\"material\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Material on", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_MaterialTarget_ContainsShaderLine()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestMatShader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh5\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestMatShader\",\"target\":\"material\"}}");
                StringAssert.Contains("shader:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_MaterialTarget_ContainsKeywordsLine()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShTestMatKw";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sh6\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShTestMatKw\",\"target\":\"material\"}}");
                StringAssert.Contains("keywords:", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // --- Error cases ---

        [Test]
        public void ShaderGet_MissingObject_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("Command failed"));
            var result = CommandRouter.Process(
                "{\"id\":\"sh7\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/DoesNotExistShader\"}}");
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void ShaderGet_MaterialOnObjectWithNoRenderer_ReturnsError()
        {
            var go = new GameObject("ShNoRenderer");
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("Command failed"));
                var result = CommandRouter.Process(
                    "{\"id\":\"sh8\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShNoRenderer\",\"target\":\"material\"}}");
                StringAssert.Contains("\"ok\":false", result);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderGet_UnknownAction_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new Regex("VALIDATION"));
            var result = CommandRouter.Process(
                "{\"id\":\"sh9\",\"cmd\":\"shader\",\"args\":{\"action\":\"badaction\",\"path\":\"/Cube\"}}");
            StringAssert.Contains("\"ok\":false", result);
            StringAssert.Contains("badaction", result);
        }

        // --- MCPSettings includes shader ---

        [Test]
        public void MCPSettings_ContainsShaderTool()
        {
            var tools = MCPSettings.GetToolNames();
            CollectionAssert.Contains(tools, "shader");
        }

        // --- Phase 20b: Create tests ---

        [Test]
        public void ShaderCreate_UnlitPreset_CreatesFile()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestUnlit.shader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shc1\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"unlit\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Shader:", result);
                Assert.IsTrue(System.IO.File.Exists(path));
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_LitPreset_HasMetallicProperty()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestLit.shader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shc2\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"lit\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("_Metallic", result);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_TransparentPreset_CreatesFile()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestTransparent.shader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shc3\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"transparent\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Shader:", result);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_CustomCode_WritesFile()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestCustom.shader";
            var code = "Shader \"Custom/TestCustom\" { SubShader { Pass { CGPROGRAM #pragma vertex vert #pragma fragment frag #include \"UnityCG.cginc\" struct v2f { float4 vertex : SV_POSITION; }; v2f vert(float4 v : POSITION) { v2f o; o.vertex = UnityObjectToClipPos(v); return o; } fixed4 frag(v2f i) : SV_Target { return fixed4(1,0,0,1); } ENDCG } } }";
            try
            {
                var result = ShaderHelper.Create(path, null, code, null);
                Assert.IsTrue(System.IO.File.Exists(path));
                StringAssert.Contains("Shader:", result);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_CustomName_UsesProvidedName()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestNamed.shader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shc5\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"unlit\",\"shader_name\":\"Custom/MyShader\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("Custom/MyShader", result);
            }
            finally { ShaderCleanup(path); }
        }

        // --- Phase 20b: Set property tests ---

        [Test]
        public void ShaderSet_Float_AppliesValue()
        {
            var shaderPath = "Assets/TestsTemp/MaterialShaderTests/TestSetFloat.shader";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShSetFloat";
            try
            {
                ShaderHelper.Create(shaderPath, "lit", null, null);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                go.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                var result = CommandRouter.Process(
                    "{\"id\":\"shs1\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShSetFloat\",\"prop\":\"_Metallic\",\"value\":\"0.75\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("_Metallic=0.75", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                ShaderCleanup(shaderPath);
            }
        }

        [Test]
        public void ShaderSet_Color_AppliesValue()
        {
            var shaderPath = "Assets/TestsTemp/MaterialShaderTests/TestSetColor.shader";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShSetColor";
            try
            {
                ShaderHelper.Create(shaderPath, "unlit", null, null);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                go.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                var result = CommandRouter.Process(
                    "{\"id\":\"shs2\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShSetColor\",\"prop\":\"_Color\",\"value\":\"#FF0000\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("_Color=#FF0000", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
                ShaderCleanup(shaderPath);
            }
        }

        [Test]
        public void ShaderSet_Keyword_Toggles()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShSetKw";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shs3\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShSetKw\",\"keyword\":\"_EMISSION\",\"enabled\":\"true\"}}");
                StringAssert.Contains("\"ok\":true", result);

                var result2 = CommandRouter.Process(
                    "{\"id\":\"shs4\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShSetKw\",\"keyword\":\"_EMISSION\",\"enabled\":\"false\"}}");
                StringAssert.Contains("\"ok\":true", result2);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ShaderSet_MissingObject_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Command failed"));
            var result = CommandRouter.Process(
                "{\"id\":\"shs5\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/NoSuchObj\",\"prop\":\"_Color\",\"value\":\"#FF0000\"}}");
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void ShaderCreate_NoPresetNoCode_ReturnsError()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VALIDATION"));
            var result = CommandRouter.Process(
                "{\"id\":\"shc6\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"Assets/TestsTemp/MaterialShaderTests/Bad.shader\"}}");
            StringAssert.Contains("\"ok\":false", result);
        }

        // --- Phase 20c: ShaderGraph tests ---

        [Test]
        public void ShaderGraphGet_ValidFile_ReturnsNodeList()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestGraph.shadergraph";
            // ShaderGraph importer may log parse errors on freshly-created files
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var createResult = CommandRouter.Process(
                    "{\"id\":\"sgc1\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                StringAssert.Contains("\"ok\":true", createResult);

                var result = CommandRouter.Process(
                    "{\"id\":\"sgg1\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_get\",\"path\":\"" + path + "\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("ShaderGraph:", result);
                StringAssert.Contains("nodes:", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphGet_ShowsEdges()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestGraphEdges.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"sgc2\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var result = CommandRouter.Process(
                    "{\"id\":\"sgg2\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_get\",\"path\":\"" + path + "\"}}");
                StringAssert.Contains("edges:", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphGet_ShowsProperties()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestGraphProps.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"sgc3\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var result = CommandRouter.Process(
                    "{\"id\":\"sgg3\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_get\",\"path\":\"" + path + "\"}}");
                StringAssert.Contains("properties:", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphCreate_UnlitPreset_CreatesFile()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestGraphCreate.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"sgc4\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                StringAssert.Contains("\"ok\":true", result);
                Assert.IsTrue(System.IO.File.Exists(path));
                StringAssert.Contains("ShaderGraph:", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphGet_MissingFile_ReturnsError()
        {
            LogAssert.Expect(LogType.Error, new Regex("Command failed"));
            var result = CommandRouter.Process(
                "{\"id\":\"sgg4\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_get\",\"path\":\"Assets/NoSuchFile.shadergraph\"}}");
            StringAssert.Contains("\"ok\":false", result);
        }

        // --- Phase 20d: graph_node + graph_edge tests ---

        [Test]
        public void ShaderGraphNode_Add_CreatesNode()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestNodeAdd.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"snd1a\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var result = CommandRouter.Process(
                    "{\"id\":\"snd1\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path + "\",\"node_type\":\"MultiplyNode\",\"node_action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("MultiplyNode", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphNode_Remove_DeletesNode()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestNodeRemove.shadergraph";
            // ShaderGraph importer may log parse errors on modified files
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"snd2a\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                CommandRouter.Process(
                    "{\"id\":\"snd2b\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path + "\",\"node_type\":\"AddNode\",\"node_action\":\"add\"}}");

                var content = System.IO.File.ReadAllText(path);
                var addNodeId = FindNodeIdByType(content, "AddNode");
                Assert.IsNotNull(addNodeId, "Should find AddNode in graph");

                var removeResult = CommandRouter.Process(
                    "{\"id\":\"snd2d\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path + "\",\"node_id\":\"" + addNodeId + "\",\"node_action\":\"remove\"}}");
                StringAssert.Contains("\"ok\":true", removeResult);
                Assert.IsFalse(removeResult.Contains("AddNode"), "AddNode should be removed");
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphEdge_Add_ConnectsNodes()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestEdgeAdd.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"sed1a\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var content = System.IO.File.ReadAllText(path);
                var nodeIds = GetBlockNodeIds(content, 2);
                Assert.IsTrue(nodeIds.Count >= 2, "Need at least 2 nodes");

                var result = CommandRouter.Process(
                    "{\"id\":\"sed1\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_edge\",\"path\":\"" + path +
                    "\",\"output_node\":\"" + nodeIds[0] + "\",\"output_slot\":\"0\",\"input_node\":\"" + nodeIds[1] + "\",\"input_slot\":\"0\",\"edge_action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("edges:", result);
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphEdge_Remove_DisconnectsNodes()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestEdgeRemove.shadergraph";
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"sed2a\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var content = System.IO.File.ReadAllText(path);
                var edge = ExtractFirstEdge(content);
                if (edge != null)
                {
                    var result = CommandRouter.Process(
                        "{\"id\":\"sed2\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_edge\",\"path\":\"" + path +
                        "\",\"output_node\":\"" + edge.Item1 + "\",\"output_slot\":\"" + edge.Item2 +
                        "\",\"input_node\":\"" + edge.Item3 + "\",\"input_slot\":\"" + edge.Item4 +
                        "\",\"edge_action\":\"remove\"}}");
                    StringAssert.Contains("\"ok\":true", result);
                }
                else
                {
                    Assert.Pass("No edges in unlit template — edge remove not applicable");
                }
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderGraphNode_Remove_CascadesEdges()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestNodeCascade.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                CommandRouter.Process(
                    "{\"id\":\"snc1a\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                var content = System.IO.File.ReadAllText(path);
                var edge = ExtractFirstEdge(content);
                if (edge != null)
                {
                    var result = CommandRouter.Process(
                        "{\"id\":\"snc1c\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path +
                        "\",\"node_id\":\"" + edge.Item1 + "\",\"node_action\":\"remove\"}}");
                    StringAssert.Contains("\"ok\":true", result);
                    // Removed node's edges should be cascade-deleted: the edge output node is gone
                    var newContent = System.IO.File.ReadAllText(path);
                    Assert.IsFalse(newContent.Contains(edge.Item1), "Removed node ID should not appear in file");
                }
                else
                {
                    Assert.Pass("No edges in template — cascade not applicable");
                }
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        // --- Phase 20e: Integration tests ---

        [Test]
        public void ShaderCreate_Overwrite_ReplacesShader()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestOverwrite.shader";
            try
            {
                // Create unlit first
                CommandRouter.Process(
                    "{\"id\":\"ov1\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"unlit\"}}");
                // Overwrite with lit
                var result = CommandRouter.Process(
                    "{\"id\":\"ov2\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"lit\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("_Metallic", result);
                StringAssert.Contains("_Smoothness", result);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderSet_Vector_AppliesValue()
        {
            var shaderPath = "Assets/TestsTemp/MaterialShaderTests/TestVec.shader";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShSetVec";
            try
            {
                // Custom shader with a Vector property
                var code = "Shader \"Custom/TestVec\" { Properties { _TestVec (\"TestVec\", Vector) = (0,0,0,0) } SubShader { Pass { } } }";
                ShaderHelper.Create(shaderPath, null, code, null);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                go.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                var setResult = ShaderHelper.SetProperty("/ShSetVec", "_TestVec", "(1,2,3,4)");
                StringAssert.Contains("_TestVec", setResult);

                // Read back via material target
                var getResult = CommandRouter.Process(
                    "{\"id\":\"sv1\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShSetVec\",\"target\":\"material\"}}");
                StringAssert.Contains("\"ok\":true", getResult);
                StringAssert.Contains("_TestVec", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                ShaderCleanup(shaderPath);
            }
        }

        [Test]
        public void ShaderCreate_GlowName_UsesProvidedName()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestGlow.shader";
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"shg1\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"preset\":\"unlit\",\"shader_name\":\"MyProject/Effects/Glow\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("MyProject/Effects/Glow", result);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_InvalidCode_ReturnsWarning()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestInvalid.shader";
            try
            {
                // Passing invalid HLSL — ShaderHelper.Create returns warning prefix when shader has errors
                var result = ShaderHelper.Create(path, null, "Shader \"Custom/Bad\" { SubShader { Pass { CGPROGRAM INVALID ENDCG } } }", null);
                // Either warning in create result or errors:yes in serialized output
                var hasWarning = result.Contains("warning") || result.Contains("error") || result.Contains("errors: yes");
                Assert.IsTrue(hasWarning, "Expected warning or error indicator for invalid shader, got: " + result);

                // Now get it — errors line should say yes
                var getResult = CommandRouter.Process(
                    "{\"id\":\"inv1\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"" + path + "\"}}");
                StringAssert.Contains("errors:", getResult);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ShaderCreate_NoPresetNoCode_ThrowsArgumentException()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/ShouldNotExist.shader";
            Assert.Throws<System.ArgumentException>(() => ShaderHelper.Create(path, "", "", null));
        }

        [Test]
        public void ShaderKeyword_Roundtrip_EnableDisable()
        {
            var shaderPath = "Assets/TestsTemp/MaterialShaderTests/TestKwRoundtrip.shader";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShKwRoundtrip";
            try
            {
                ShaderHelper.Create(shaderPath, "lit", null, null);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                go.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                // Enable _EMISSION
                CommandRouter.Process(
                    "{\"id\":\"kw1\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShKwRoundtrip\",\"keyword\":\"_EMISSION\",\"enabled\":\"true\"}}");
                var after = CommandRouter.Process(
                    "{\"id\":\"kw2\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShKwRoundtrip\",\"target\":\"material\"}}");
                StringAssert.Contains("_EMISSION", after);

                // Disable _EMISSION
                CommandRouter.Process(
                    "{\"id\":\"kw3\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShKwRoundtrip\",\"keyword\":\"_EMISSION\",\"enabled\":\"false\"}}");
                var afterDisable = CommandRouter.Process(
                    "{\"id\":\"kw4\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShKwRoundtrip\",\"target\":\"material\"}}");
                // keywords line should be "none" or not contain _EMISSION
                var kwLine = ExtractKeywordsLine(afterDisable);
                Assert.IsFalse(kwLine.Contains("_EMISSION"), "Expected _EMISSION to be disabled, keywords line: " + kwLine);
            }
            finally
            {
                Object.DestroyImmediate(go);
                ShaderCleanup(shaderPath);
            }
        }

        [Test]
        public void ShaderGraph_NodeEdge_FullWorkflow()
        {
            var path = "Assets/TestsTemp/MaterialShaderTests/TestFullWorkflow.shadergraph";
            LogAssert.ignoreFailingMessages = true;
            try
            {
                // Create graph
                var createResult = CommandRouter.Process(
                    "{\"id\":\"fw1\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_create\",\"path\":\"" + path + "\",\"preset\":\"unlit_graph\"}}");
                StringAssert.Contains("\"ok\":true", createResult);

                // Add two nodes
                var add1 = CommandRouter.Process(
                    "{\"id\":\"fw2\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path + "\",\"node_type\":\"MultiplyNode\",\"node_action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", add1);

                var add2 = CommandRouter.Process(
                    "{\"id\":\"fw3\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path + "\",\"node_type\":\"AddNode\",\"node_action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", add2);

                // Verify node count increased — graph_get should list both nodes
                var getAfterAdd = CommandRouter.Process(
                    "{\"id\":\"fw4\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_get\",\"path\":\"" + path + "\"}}");
                StringAssert.Contains("MultiplyNode", getAfterAdd);
                StringAssert.Contains("AddNode", getAfterAdd);

                // Connect the two added nodes
                var content = System.IO.File.ReadAllText(path);
                var multiplyId = FindNodeIdByType(content, "MultiplyNode");
                var addId = FindNodeIdByType(content, "AddNode");
                Assert.IsNotNull(multiplyId, "MultiplyNode not found");
                Assert.IsNotNull(addId, "AddNode not found");

                var edgeResult = CommandRouter.Process(
                    "{\"id\":\"fw5\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_edge\",\"path\":\"" + path +
                    "\",\"output_node\":\"" + multiplyId + "\",\"output_slot\":\"0\",\"input_node\":\"" + addId + "\",\"input_slot\":\"0\",\"edge_action\":\"add\"}}");
                StringAssert.Contains("\"ok\":true", edgeResult);
                StringAssert.Contains("edges:", edgeResult);

                // Remove first node — should cascade (edge gone), AddNode still present
                var removeResult = CommandRouter.Process(
                    "{\"id\":\"fw6\",\"cmd\":\"shader\",\"args\":{\"action\":\"graph_node\",\"path\":\"" + path +
                    "\",\"node_id\":\"" + multiplyId + "\",\"node_action\":\"remove\"}}");
                StringAssert.Contains("\"ok\":true", removeResult);

                var afterRemove = System.IO.File.ReadAllText(path);
                Assert.IsFalse(afterRemove.Contains(multiplyId), "MultiplyNode should be removed from file");
                // AddNode should still be present
                Assert.IsTrue(afterRemove.Contains("AddNode"), "AddNode should still exist after cascade remove");
            }
            finally { LogAssert.ignoreFailingMessages = false; ShaderCleanup(path); }
        }

        [Test]
        public void ShaderSet_Float_Roundtrip()
        {
            var shaderPath = "Assets/TestsTemp/MaterialShaderTests/TestFloatRoundtrip.shader";
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ShFloatRoundtrip";
            try
            {
                ShaderHelper.Create(shaderPath, "lit", null, null);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                go.GetComponent<Renderer>().sharedMaterial = new Material(shader);

                // Set _Metallic = 0.75
                var setResult = CommandRouter.Process(
                    "{\"id\":\"fr1\",\"cmd\":\"shader\",\"args\":{\"action\":\"set\",\"path\":\"/ShFloatRoundtrip\",\"prop\":\"_Metallic\",\"value\":\"0.75\"}}");
                StringAssert.Contains("\"ok\":true", setResult);

                // Read back via material target
                var getResult = CommandRouter.Process(
                    "{\"id\":\"fr2\",\"cmd\":\"shader\",\"args\":{\"action\":\"get\",\"path\":\"/ShFloatRoundtrip\",\"target\":\"material\"}}");
                StringAssert.Contains("\"ok\":true", getResult);
                // Value should be ~0.75 — serialized via G format
                StringAssert.Contains("0.75", getResult);
            }
            finally
            {
                Object.DestroyImmediate(go);
                ShaderCleanup(shaderPath);
            }
        }

        // --- Phase 20f: Regression tests for JSON unescape ---

        [Test]
        public void ShaderCreate_CustomCodeViaJson_UnescapesQuotes()
        {
            // Regression: ExtractString must unescape \" in JSON so shader code gets proper quotes
            var path = "Assets/TestsTemp/MaterialShaderTests/TestUnescape.shader";
            try
            {
                // JSON has escaped quotes inside shader code — the Properties block uses "Texture" etc.
                var code = "Shader \\\"Custom/Unescape\\\" {\\n    Properties {\\n        _Val (\\\"Val\\\", Float) = 1\\n    }\\n    SubShader { Pass { CGPROGRAM\\n#pragma vertex vert\\n#pragma fragment frag\\n#include \\\"UnityCG.cginc\\\"\\nstruct v2f { float4 vertex : SV_POSITION; };\\nfloat _Val;\\nv2f vert (float4 v : POSITION) { v2f o; o.vertex = UnityObjectToClipPos(v); return o; }\\nfixed4 frag (v2f i) : SV_Target { return _Val; }\\nENDCG } }\\n}";
                var json = "{\"id\":\"ue1\",\"cmd\":\"shader\",\"args\":{\"action\":\"create\",\"path\":\"" + path + "\",\"code\":\"" + code + "\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.Contains("\"ok\":true", result);
                // Verify the shader file has actual quotes, not escaped ones
                var fileContent = System.IO.File.ReadAllText(path);
                StringAssert.Contains("\"Custom/Unescape\"", fileContent);
                StringAssert.Contains("\"Val\"", fileContent);
            }
            finally { ShaderCleanup(path); }
        }

        [Test]
        public void ExtractString_UnescapesJsonSequences()
        {
            // Direct unit test for unescape behavior
            var json = "{\"key\": \"hello \\\"world\\\" \\n tab\\t end\"}";
            var result = JsonHelper.ExtractString(json, "key");
            Assert.AreEqual("hello \"world\" \n tab\t end", result);
        }

        // --- helpers ---

        static void ShaderCleanup(string path)
        {
            AssetDatabase.DeleteAsset(path);
        }

        static string FindNodeIdByType(string content, string nodeType)
        {
            var needle = $"\"UnityEditor.ShaderGraph.{nodeType}\"";
            var idx = content.IndexOf(needle);
            if (idx < 0) return null;
            var blockStart = content.LastIndexOf('{', idx);
            if (blockStart < 0) return null;
            var chunk = content.Substring(blockStart, System.Math.Min(300, content.Length - blockStart));
            return JsonHelper.ExtractString(chunk, "m_ObjectId");
        }

        static System.Collections.Generic.List<string> GetBlockNodeIds(string content, int count)
        {
            var ids = new System.Collections.Generic.List<string>();
            int idx = 0;
            while (ids.Count < count && idx < content.Length)
            {
                idx = content.IndexOf("\"m_Type\": \"UnityEditor.ShaderGraph.BlockNode\"", idx);
                if (idx < 0) break;
                var blockStart = content.LastIndexOf('{', idx);
                if (blockStart >= 0)
                {
                    var chunk = content.Substring(blockStart, System.Math.Min(300, content.Length - blockStart));
                    var id = JsonHelper.ExtractString(chunk, "m_ObjectId");
                    if (id != null) ids.Add(id);
                }
                idx++;
            }
            return ids;
        }

        static System.Tuple<string, string, string, string> ExtractFirstEdge(string content)
        {
            var idx = content.IndexOf("\"m_OutputSlot\"");
            if (idx < 0) return null;
            var outNode = ExtractNestedId(content, idx);
            var outSlot = ExtractNestedSlot(content, idx);
            var inIdx = content.IndexOf("\"m_InputSlot\"", idx);
            if (inIdx < 0) return null;
            var inNode = ExtractNestedId(content, inIdx);
            var inSlot = ExtractNestedSlot(content, inIdx);
            if (outNode == null || inNode == null) return null;
            return System.Tuple.Create(outNode, outSlot ?? "0", inNode, inSlot ?? "0");
        }

        static string ExtractNestedId(string content, int from)
        {
            var s = content.IndexOf("\"m_Id\"", from);
            if (s < 0 || s > from + 300) return null;
            return JsonHelper.ExtractString(content.Substring(s, System.Math.Min(100, content.Length - s)), "m_Id");
        }

        static string ExtractNestedSlot(string content, int from)
        {
            var s = content.IndexOf("\"m_SlotId\"", from);
            if (s < 0 || s > from + 300) return null;
            return JsonHelper.ExtractString(content.Substring(s, System.Math.Min(100, content.Length - s)), "m_SlotId");
        }

        static string ExtractKeywordsLine(string content)
        {
            var idx = content.IndexOf("keywords:");
            if (idx < 0) return "";
            var end = content.IndexOf('\n', idx);
            return end < 0 ? content.Substring(idx) : content.Substring(idx, end - idx);
        }

    }
}
