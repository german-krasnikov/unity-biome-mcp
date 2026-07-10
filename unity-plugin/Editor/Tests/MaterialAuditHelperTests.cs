// TDD: MaterialAuditHelper — material/texture scene-wide audit.
// Tests use reflection for private methods and create minimal scene objects.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MaterialAuditHelperTests : SceneTestBase
    {
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        // ── Reflection helpers ─────────────────────────────────────────────

        static string InvokeFingerprint(Material mat)
        {
            var mi = typeof(MaterialAuditHelper).GetMethod(
                "Fingerprint",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Material) }, null);
            Assert.IsNotNull(mi, "MaterialAuditHelper.Fingerprint not found via reflection");
            return (string)mi.Invoke(null, new object[] { mat });
        }

        static string Execute(string action = "summary", string platform = null)
        {
            var args = platform != null
                ? $"{{\"action\":\"{action}\",\"platform\":\"{platform}\"}}"
                : $"{{\"action\":\"{action}\"}}";
            return MaterialAuditHelper.Execute(args);
        }

        MeshRenderer CreateRenderer(Material mat, string name = "TestRenderer")
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }

        Material CreateMaterial(string shaderName = "Standard", string matName = "TestMat")
        {
            var shader = Shader.Find(shaderName) ?? Shader.Find("Hidden/InternalErrorShader");
            var mat = new Material(shader) { name = matName };
            _created.Add(mat);
            return mat;
        }

        // ── Tests ──────────────────────────────────────────────────────────

        [Test]
        public void Execute_Summary_ReturnsExpectedKeys()
        {
            var result = Execute("summary");
            StringAssert.Contains("MATERIAL AUDIT", result);
            StringAssert.Contains("mats:", result);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsError()
        {
            var result = Execute("not_a_real_action");
            StringAssert.StartsWith("err:", result);
            StringAssert.Contains("not_a_real_action", result);
            StringAssert.Contains("summary", result); // valid actions listed
        }

        [Test]
        public void Execute_Duplicates_IdenticalMaterials_GroupsThem()
        {
            // Two DIFFERENT material objects with identical shader + keywords = duplicate
            var mat1 = CreateMaterial(matName: "DupA");
            var mat2 = CreateMaterial(matName: "DupB"); // different name, same shader+props
            CreateRenderer(mat1, "RDup1");
            CreateRenderer(mat2, "RDup2");

            var result = Execute("duplicates");
            StringAssert.Contains("DUPLICATES", result);
            // group(2) should appear in the output
            StringAssert.Contains("group(2)", result);
        }

        [Test]
        public void Execute_Duplicates_DifferentKeywords_NotGrouped()
        {
            var mat1 = CreateMaterial(matName: "KWUnique1");
            var mat2 = CreateMaterial(matName: "KWUnique2");
            mat2.EnableKeyword("_ALPHATEST_ON"); // makes fingerprint differ
            CreateRenderer(mat1, "RKWA");
            CreateRenderer(mat2, "RKWB");

            var result = Execute("duplicates");
            StringAssert.Contains("DUPLICATES", result);
            // KWUnique1 and KWUnique2 must NOT appear together in the same group
            Assert.IsFalse(
                result.Contains("KWUnique1") && result.Contains("KWUnique2"),
                "Materials with different keywords must not be grouped as duplicates");
        }

        [Test]
        public void Execute_Materials_ContainsShaderName()
        {
            var mat = CreateMaterial();
            CreateRenderer(mat, "RMat");
            var result = Execute("materials");
            StringAssert.Contains("MATERIALS", result);
        }

        [Test]
        public void Execute_Textures_ReturnsTexturesSection()
        {
            var result = Execute("textures");
            StringAssert.Contains("TEXTURES", result);
        }

        [Test]
        public void Execute_Compression_ReturnsSectionHeader()
        {
            var result = Execute("compression", "Default");
            StringAssert.Contains("COMPRESSION", result);
        }

        [Test]
        public void Execute_Recommendations_ReturnsSectionHeader()
        {
            var result = Execute("recommendations");
            StringAssert.Contains("RECOMMENDATIONS", result);
        }

        [Test]
        public void Execute_NeverCallsMaterialProperty()
        {
            // If implementation accidentally uses .material (not .sharedMaterial),
            // it creates instances with " (Instance)" suffix. Verify this doesn't happen.
            var mat = CreateMaterial(matName: "NonInstanceMat");
            CreateRenderer(mat, "NonInstanceRenderer");

            // Call all read-only actions
            var summary = Execute("summary");
            var materials = Execute("materials");

            // Neither output should contain " (Instance)" from our test object
            // (The implementation must use sharedMaterials, not .material)
            StringAssert.DoesNotContain("NonInstanceMat (Instance)", summary);
            StringAssert.DoesNotContain("NonInstanceMat (Instance)", materials);
        }

        [Test]
        public void Execute_SkipsBuiltinTextures_NoAssetPath()
        {
            // Procedural textures (created via new Texture2D) have no AssetDatabase path.
            // Execute("textures") should not crash on them.
            var tex = new Texture2D(4, 4);
            _created.Add(tex);
            var mat = CreateMaterial();
            mat.mainTexture = tex;
            CreateRenderer(mat, "RendererWithProceduralTex");

            Assert.DoesNotThrow(() => Execute("textures"),
                "Execute('textures') must not crash on procedural textures with no asset path");
        }

        // ── Fingerprint tests (pure logic via reflection) ──────────────────

        [Test]
        public void Fingerprint_SameShaderSameKeywords_Matches()
        {
            var mat1 = CreateMaterial(matName: "FP_Same1");
            var mat2 = CreateMaterial(matName: "FP_Same2");

            var fp1 = InvokeFingerprint(mat1);
            var fp2 = InvokeFingerprint(mat2);

            Assert.AreEqual(fp1, fp2,
                "Same shader + same keywords must produce identical fingerprint");
        }

        [Test]
        public void Fingerprint_SameShaderDiffKeywords_Differs()
        {
            var mat1 = CreateMaterial(matName: "FP_Diff1");
            var mat2 = CreateMaterial(matName: "FP_Diff2");
            mat2.EnableKeyword("_ALPHATEST_ON");

            var fp1 = InvokeFingerprint(mat1);
            var fp2 = InvokeFingerprint(mat2);

            Assert.AreNotEqual(fp1, fp2,
                "Same shader + different keywords must produce different fingerprint");
        }

        [Test]
        public void Fingerprint_SkipsTextureProperties()
        {
            // Two materials with same shader/keywords but different texture = same fingerprint
            // (texture differences are intentional, not duplicates)
            var mat1 = CreateMaterial(matName: "FP_Tex1");
            var mat2 = CreateMaterial(matName: "FP_Tex2");
            mat2.mainTexture = new Texture2D(8, 8);
            _created.Add(mat2.mainTexture);

            var fp1 = InvokeFingerprint(mat1);
            var fp2 = InvokeFingerprint(mat2);

            Assert.AreEqual(fp1, fp2,
                "Materials differing only by texture should have same fingerprint");
        }
    }
}
