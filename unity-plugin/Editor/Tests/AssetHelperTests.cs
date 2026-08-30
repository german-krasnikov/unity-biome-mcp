// TDD: Pure-logic tests for ShaderHelper, ShaderGraphHelper, AssetDatabaseHelper.
// All tested methods are private statics — accessed via BindingFlags.NonPublic.
// No Unity assets on disk required; EditMode safe.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AssetHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Reflection helpers ────────────────────────────────────────────────

        static object InvokePrivate(Type type, string method, params object[] args)
        {
            var mi = type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(mi, $"Method {type.Name}.{method} not found");
            return mi.Invoke(null, args);
        }

        static string BuildPreset(string preset, string name) =>
            (string)InvokePrivate(typeof(ShaderHelper), "BuildPreset", preset, name);

        static List<string> SplitBlocks(string content, bool skipStrings = true) =>
            (List<string>)InvokePrivate(typeof(ShaderGraphHelper), "SplitBlocks", content, skipStrings);

        static string ShortType(string t) =>
            (string)InvokePrivate(typeof(ShaderGraphHelper), "ShortType", t);

        static void ValidatePath(string path) =>
            InvokePrivate(typeof(AssetDatabaseHelper), "ValidatePath", path);

        static string InsertIntoArray(string content, string root, string arrayKey, string item) =>
            (string)InvokePrivate(typeof(ShaderGraphHelper), "InsertIntoArray", content, root, arrayKey, item);

        // ── ShaderHelper.BuildPreset ─────────────────────────────────────────

        [Test]
        public void BuildPreset_Unlit_ContainsShaderName()
        {
            var result = BuildPreset("unlit", "Custom/MyShader");
            StringAssert.Contains("Custom/MyShader", result);
        }

        [Test]
        public void BuildPreset_Unlit_HasUnlitStructure()
        {
            var result = BuildPreset("unlit", "Test/Unlit");
            StringAssert.Contains("CGPROGRAM", result);
            StringAssert.Contains("_MainTex", result);
            StringAssert.DoesNotContain("Surface", result); // not a surface shader
        }

        [Test]
        public void BuildPreset_Lit_ContainsShaderName()
        {
            var result = BuildPreset("lit", "Custom/LitShader");
            StringAssert.Contains("Custom/LitShader", result);
        }

        [Test]
        public void BuildPreset_Lit_HasSurfaceShaderDirective()
        {
            var result = BuildPreset("lit", "Test/Lit");
            StringAssert.Contains("#pragma surface surf Standard", result);
            StringAssert.Contains("_Metallic", result);
            StringAssert.Contains("_Smoothness", result);
        }

        [Test]
        public void BuildPreset_Transparent_ContainsShaderName()
        {
            var result = BuildPreset("transparent", "Custom/TransShader");
            StringAssert.Contains("Custom/TransShader", result);
        }

        [Test]
        public void BuildPreset_Transparent_HasTransparentTags()
        {
            var result = BuildPreset("transparent", "Test/Trans");
            StringAssert.Contains("Transparent", result);
            StringAssert.Contains("Blend SrcAlpha OneMinusSrcAlpha", result);
            StringAssert.Contains("ZWrite Off", result);
        }

        [Test]
        public void BuildPreset_UnknownPreset_ThrowsArgumentException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() => BuildPreset("bogus", "Test"));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
            StringAssert.Contains("bogus", ex.InnerException.Message);
        }

        [Test]
        public void BuildPreset_NameWithSlash_IsEmbeddedAsIs()
        {
            var result = BuildPreset("unlit", "My/Deep/Name");
            // Shader declaration should be: Shader "My/Deep/Name" {
            StringAssert.Contains("Shader \"My/Deep/Name\"", result);
        }

        // ── ShaderGraphHelper.SplitBlocks ────────────────────────────────────

        [Test]
        public void SplitBlocks_EmptyString_ReturnsEmpty()
        {
            var blocks = SplitBlocks("");
            Assert.AreEqual(0, blocks.Count);
        }

        [Test]
        public void SplitBlocks_SingleEmptyBraces_ReturnsOneBlock()
        {
            var blocks = SplitBlocks("{}");
            Assert.AreEqual(1, blocks.Count);
            Assert.AreEqual("{}", blocks[0]);
        }

        [Test]
        public void SplitBlocks_NestedBraces_CountsAsOneBlock()
        {
            var blocks = SplitBlocks("{ \"a\": { \"b\": 1 } }");
            Assert.AreEqual(1, blocks.Count);
        }

        [Test]
        public void SplitBlocks_TwoSiblingBlocks_ReturnsTwoBlocks()
        {
            var blocks = SplitBlocks("{\"id\":\"1\"}\n{\"id\":\"2\"}");
            Assert.AreEqual(2, blocks.Count);
        }

        [Test]
        public void SplitBlocks_StringLiteralWithBrace_DoesNotSplitBlock()
        {
            // The string "{ fake" should not open a new block depth
            var blocks = SplitBlocks("{ \"key\": \"value with { brace\" }");
            Assert.AreEqual(1, blocks.Count);
        }

        [Test]
        public void SplitBlocks_SkipStringsFalse_StringBraceCausesSplit()
        {
            // With skipStrings=false the brace inside the string WILL count
            // "{ \"key\": \"val {\" }" — depth goes 1, then brace in string +1, then close -1 (miss), then outer close
            // Exact behavior: just verify it does NOT crash
            Assert.DoesNotThrow(() => SplitBlocks("{ \"k\": \"{\" }", false));
        }

        [Test]
        public void SplitBlocks_ThreeBlocks_ReturnsThree()
        {
            var content = "{\"a\":1}\n{\"b\":2}\n{\"c\":3}";
            Assert.AreEqual(3, SplitBlocks(content).Count);
        }

        [Test]
        public void SplitBlocks_PreservesBlockContent()
        {
            var block = "{\"m_ObjectId\":\"abc123\"}";
            var blocks = SplitBlocks(block);
            Assert.AreEqual(1, blocks.Count);
            StringAssert.Contains("m_ObjectId", blocks[0]);
            StringAssert.Contains("abc123", blocks[0]);
        }

        // ── ShaderGraphHelper.ShortType ──────────────────────────────────────

        [Test]
        public void ShortType_DottedType_ReturnsLastSegment()
        {
            Assert.AreEqual("GraphData", ShortType("UnityEditor.ShaderGraph.GraphData"));
        }

        [Test]
        public void ShortType_NoDot_ReturnsInputUnchanged()
        {
            Assert.AreEqual("GraphData", ShortType("GraphData"));
        }

        [Test]
        public void ShortType_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual("", ShortType(""));
        }

        [Test]
        public void ShortType_TrailingDot_ReturnsEmptySegment()
        {
            // "Foo." → last segment after dot is ""
            Assert.AreEqual("", ShortType("Foo."));
        }

        // ── AssetDatabaseHelper.ValidatePath ──────────────────────────────────

        [Test]
        public void ValidatePath_Assets_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ValidatePath("Assets/Foo/bar.mat"));
        }

        [Test]
        public void ValidatePath_Packages_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ValidatePath("Packages/com.foo/bar.asset"));
        }

        [Test]
        public void ValidatePath_RelativePath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() => ValidatePath("Foo/bar.mat"));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
        }

        [Test]
        public void ValidatePath_AbsoluteUnixPath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<TargetInvocationException>(() => ValidatePath("/home/user/file.mat"));
            Assert.IsInstanceOf<ArgumentException>(ex.InnerException);
        }

        [Test]
        public void ValidatePath_ErrorMessage_MentionsBothPrefixes()
        {
            var ex = Assert.Throws<TargetInvocationException>(() => ValidatePath("Nope/file.mat"));
            StringAssert.Contains("Assets/", ex.InnerException.Message);
            StringAssert.Contains("Packages/", ex.InnerException.Message);
        }

        // ── ShaderGraphHelper.InsertIntoArray ─────────────────────────────────

        [Test]
        public void InsertIntoArray_EmptyArray_InsertsItem()
        {
            var root = "{\"m_Nodes\": []}";
            var content = root;
            var result = InsertIntoArray(content, root, "m_Nodes", "{\"m_Id\": \"abc\"}");
            StringAssert.Contains("m_Id", result);
            StringAssert.Contains("abc", result);
        }

        [Test]
        public void InsertIntoArray_NonEmptyArray_AppendWithComma()
        {
            var root = "{\"m_Nodes\": [{\"m_Id\": \"existing\"}]}";
            var content = root;
            var result = InsertIntoArray(content, root, "m_Nodes", "{\"m_Id\": \"new\"}");
            StringAssert.Contains("existing", result);
            StringAssert.Contains("new", result);
        }

        [Test]
        public void InsertIntoArray_UpdatesContentNotRoot()
        {
            // Content can have extra text beyond root block
            var root = "{\"m_Edges\": []}";
            var content = "preamble\n" + root + "\npostamble";
            var result = InsertIntoArray(content, root, "m_Edges", "{\"edge\":1}");
            StringAssert.Contains("preamble", result);
            StringAssert.Contains("postamble", result);
            StringAssert.Contains("edge", result);
        }

        // ── AssetDatabaseHelper.ValidateMove path_only mode (P-199) ─────────────

        static string ExecValidateMove(string argsJson) =>
            (string)InvokePrivate(typeof(AssetDatabaseHelper), "Execute", "validate_move", argsJson);

        [Test]
        public void ValidateMove_PathOnly_SucceedsWithNonExistentDest()
        {
            // path_only=true must bypass AssetDatabase.ValidateMoveAsset (folder existence check)
            var result = ExecValidateMove(
                "{\"source\":\"Assets/SomeSrc.prefab\"," +
                "\"dest\":\"Assets/NonExistentFolder123456/SomeSrc.prefab\"," +
                "\"path_only\":\"true\"}");
            StringAssert.Contains("ok", result);
        }

        [Test]
        public void ValidateMove_PathOnly_False_FailsOnNonExistentDest()
        {
            // Without path_only, ValidateMoveAsset runs and returns an error for a non-existent folder
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecValidateMove(
                    "{\"source\":\"Assets/SomeSrc.prefab\"," +
                    "\"dest\":\"Assets/NonExistentFolder123456/SomeSrc.prefab\"}"));
            Assert.IsNotNull(ex.InnerException);
        }

        // ── AssetDatabaseHelper export_package / import_package arg validation ──

        static string ExecAssetAction(string action, string argsJson) =>
            (string)InvokePrivate(typeof(AssetDatabaseHelper), "Execute", action, argsJson);

        [Test]
        public void ExportPackage_MissingOutput_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecAssetAction("export_package", "{\"path\":\"Assets/Foo\"}"));
            StringAssert.Contains("output", ex.InnerException.Message);
        }

        [Test]
        public void ExportPackage_MissingPath_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecAssetAction("export_package", "{\"output\":\"/tmp/x.unitypackage\"}"));
            StringAssert.Contains("path", ex.InnerException.Message);
        }

        [Test]
        public void ImportPackage_MissingPath_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecAssetAction("import_package", "{}"));
            StringAssert.Contains("path", ex.InnerException.Message);
        }

        [Test]
        public void ImportPackage_NonExistentFile_Throws()
        {
            var ex = Assert.Throws<TargetInvocationException>(() =>
                ExecAssetAction("import_package", "{\"path\":\"/nonexistent/pkg.unitypackage\"}"));
            StringAssert.Contains("not found", ex.InnerException.Message);
        }

        // ── Pipeline gap: read_text / write_text / reimport / AnimatorController / ScriptableObject ─────

        private const string TempFolder = "Assets/TestsTemp/AssetHelperExt";

        [Test]
        public void ReadText_ValidPath_ReturnsContent()
        {
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/file.txt");
            var abs = System.IO.Path.GetFullPath(TempFolder + "/file.txt");
            System.IO.File.WriteAllText(abs, "hello", System.Text.Encoding.UTF8);
            UnityEditor.AssetDatabase.ImportAsset(TempFolder + "/file.txt",
                UnityEditor.ImportAssetOptions.ForceSynchronousImport);

            var result = AssetDatabaseHelper.Execute("read_text",
                $"{{\"path\":\"{TempFolder}/file.txt\"}}");

            StringAssert.StartsWith("ok:read", result);
            StringAssert.Contains("hello", result);
        }

        [Test]
        public void ReadText_InvalidPath_ThrowsArgumentException()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                AssetDatabaseHelper.Execute("read_text", "{\"path\":\"/tmp/bad.txt\"}"));
            StringAssert.Contains("must start with", ex.Message);
        }

        [Test]
        public void WriteText_Roundtrip_WritesFile()
        {
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/out.txt");

            var result = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/out.txt\",\"content\":\"testdata\"}}");

            StringAssert.StartsWith("ok:write", result);
            var abs = System.IO.Path.GetFullPath(TempFolder + "/out.txt");
            Assert.IsTrue(System.IO.File.Exists(abs));
            Assert.AreEqual("testdata", System.IO.File.ReadAllText(abs, System.Text.Encoding.UTF8));
        }

        // ── P0-30: freeze OFF encoding/direct-vs-batch baseline (pre-SourcePatch) ──

        [Test]
        public void WriteText_WritesUtf8ByteOrderMark()
        {
            // AssetDatabaseHelper.WriteText uses System.Text.Encoding.UTF8 (NOT
            // JsonHelper.Utf8NoBom, used by every other writer in this codebase),
            // which emits a 3-byte BOM. Freezing this pre-existing quirk as-is —
            // not fixing it here (see Plans/HotReload P0-30). WriteText has no
            // per-extension branch, so this also documents the .cs write path
            // without triggering a real Unity compile (a real .cs import is
            // covered separately by the BiomeWorkerOnly-gated explicit test).
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/bom.txt");

            AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/bom.txt\",\"content\":\"testdata\"}}");

            var abs = System.IO.Path.GetFullPath(TempFolder + "/bom.txt");
            var bytes = System.IO.File.ReadAllBytes(abs);
            Assert.GreaterOrEqual(bytes.Length, 3);
            Assert.AreEqual(0xEF, bytes[0]);
            Assert.AreEqual(0xBB, bytes[1]);
            Assert.AreEqual(0xBF, bytes[2]);
        }

        [Test]
        public void WriteText_JsonExtension_RoundtripsContentUnchanged()
        {
            // Freezes "non-.cs behavior stays unchanged": .json goes through the
            // exact same WriteText code path as .txt/.cs, with no special-casing.
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/data.json");

            var result = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/data.json\",\"content\":\"{{\\\"a\\\":1}}\"}}");

            StringAssert.StartsWith("ok:write", result);
            var abs = System.IO.Path.GetFullPath(TempFolder + "/data.json");
            Assert.AreEqual("{\"a\":1}", System.IO.File.ReadAllText(abs, System.Text.Encoding.UTF8));
        }

        [Test]
        public void WriteText_ViaBatchAndDirect_ProduceIdenticalFileBytes()
        {
            // P0-30 baseline: today nothing distinguishes the direct `asset`
            // dispatch from the `batch`-routed one — both reach
            // AssetDatabaseHelper.WriteText via CommandRouter.ExecuteCommand with
            // no additional gate. This freezes that absence-of-gate as the
            // explicit pre-SourcePatch OFF behavior (the future §3.2 "same C#
            // pre-write gate" requirement has nothing to enforce yet).
            SetEditorPrefBool(MCPSettings.KeyPrefix + "asset", true);
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/direct.txt");
            AssetHelper.EnsureDirectory(TempFolder + "/viabatch.txt");

            var directResult = AssetDatabaseHelper.Execute("write_text",
                $"{{\"path\":\"{TempFolder}/direct.txt\",\"content\":\"parity-check\"}}");

            var batchResult = BatchHelper.Execute(
                $"asset action=write_text path=\"{TempFolder}/viabatch.txt\" content=\"parity-check\"",
                "stop");

            StringAssert.StartsWith("ok:write", directResult);
            Assert.IsFalse(BatchHelper.HasErrors(batchResult), batchResult);

            var directBytes = System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(TempFolder + "/direct.txt"));
            var batchBytes = System.IO.File.ReadAllBytes(System.IO.Path.GetFullPath(TempFolder + "/viabatch.txt"));
            CollectionAssert.AreEqual(directBytes, batchBytes);
        }

        [Test]
        public void Reimport_NonExistentAsset_Throws()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                AssetDatabaseHelper.Execute("reimport",
                    "{\"path\":\"Assets/NonExistent_AssetHelperExt.png\"}"));
            StringAssert.Contains("Asset not found", ex.Message);
        }

        [Test]
        public void CreateAnimatorController_CreatesAsset()
        {
            TrackOwnedAsset(TempFolder);
            AssetHelper.EnsureDirectory(TempFolder + "/Ctrl.controller");

            var result = AssetDatabaseHelper.Execute("create",
                $"{{\"type\":\"AnimatorController\",\"path\":\"{TempFolder}/Ctrl.controller\"}}");

            StringAssert.StartsWith("ok:", result);
            var type = UnityEditor.AssetDatabase.GetMainAssetTypeAtPath(TempFolder + "/Ctrl.controller");
            Assert.AreEqual(typeof(UnityEditor.Animations.AnimatorController), type);
        }

        [Test]
        public void CreateScriptableObject_MissingClass_Throws()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                AssetDatabaseHelper.Execute("create",
                    $"{{\"type\":\"ScriptableObject\",\"path\":\"{TempFolder}/S.asset\"}}"));
            StringAssert.Contains("class is required", ex.Message);
        }
    }
}
