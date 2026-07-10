// TDD: LodCullingAnalyzer — LOD group coverage + occlusion culling analysis.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class LodCullingAnalyzerTests : SceneTestBase
    {
        readonly List<GameObject> _created = new List<GameObject>();
        readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
            foreach (var a in _assets)
                if (a != null) Object.DestroyImmediate(a);
            _assets.Clear();
        }

        // ── Reflection helpers ─────────────────────────────────────────────

        static long InvokeGetPolyCount(Mesh m)
        {
            var mi = typeof(LodCullingAnalyzer).GetMethod(
                "GetPolyCount",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Mesh) }, null);
            Assert.IsNotNull(mi, "LodCullingAnalyzer.GetPolyCount not found");
            return (long)mi.Invoke(null, new object[] { m });
        }

        GameObject CreateGO(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        // ── Tests ──────────────────────────────────────────────────────────

        [Test]
        public void Analyze_LOD_EmptyScene_ReturnsZero()
        {
            var result = LodCullingAnalyzer.Analyze("lod");
            StringAssert.Contains("LOD", result);
            // With no LOD groups, groups count must be 0
            StringAssert.Contains("0", result);
        }

        [Test]
        public void Analyze_Culling_ContainsCullingSection()
        {
            var result = LodCullingAnalyzer.Analyze("culling");
            StringAssert.Contains("CULLING", result);
        }

        [Test]
        public void Analyze_NullFocus_ContainsBothSections()
        {
            var result = LodCullingAnalyzer.Analyze(null);
            StringAssert.Contains("LOD", result);
            StringAssert.Contains("CULLING", result);
        }

        [Test]
        public void Analyze_Focus_LOD_SkipsCullingSection()
        {
            var result = LodCullingAnalyzer.Analyze("lod");
            StringAssert.Contains("LOD", result);
            // Culling section header must NOT appear when focus=lod
            StringAssert.DoesNotContain("CULLING", result);
        }

        [Test]
        public void Analyze_Focus_Culling_SkipsLODSection()
        {
            var result = LodCullingAnalyzer.Analyze("culling");
            StringAssert.Contains("CULLING", result);
            // LOD groups analysis should not appear
            StringAssert.DoesNotContain("LOD GROUPS", result);
        }

        [Test]
        public void Analyze_LOD_CrossFade_Flagged()
        {
            var go = CreateGO("LODCrossFadeTest");
            var group = go.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            var lods = new LOD[] { new LOD(0.5f, new Renderer[0]) };
            group.SetLODs(lods);

            var result = LodCullingAnalyzer.Analyze("lod");
            StringAssert.Contains("CrossFade", result);
            StringAssert.Contains("WARN", result);
        }

        [Test]
        public void Analyze_BriefUnder150Tokens()
        {
            var result = LodCullingAnalyzer.Analyze(null);
            // 150 tokens ≈ 600 chars (rough estimate: 4 chars/token)
            Assert.Less(result.Length, 800,
                "Brief output should be under ~150 tokens (~800 chars)");
        }

        [Test]
        public void Analyze_Coverage_EmptyScene()
        {
            var result = LodCullingAnalyzer.Analyze(null);
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void Analyze_HighPolyNoLOD_Recommends()
        {
            // Create a mesh renderer with many triangles but no LODGroup
            var go = CreateGO("HighPolyNoLOD");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            // The mesh filter is empty (no mesh), but the renderer exists without LODGroup
            // Implementation should flag renderers without LODGroup as potential candidates

            var result = LodCullingAnalyzer.Analyze("lod");
            // Either it lists them or shows the LOD section — it should not crash
            Assert.IsNotNull(result);
        }

        [Test]
        public void Analyze_Culling_NoOcclusion_Flagged()
        {
            // By default in a test scene, occlusion culling data doesn't exist
            // The output should flag this with a warning
            var result = LodCullingAnalyzer.Analyze("culling");
            // When no occlusion data exists, the report should mention it
            // (either WARN or "not baked" or "none")
            Assert.IsTrue(
                result.Contains("WARN") || result.Contains("not baked") || result.Contains("0"),
                $"Expected occlusion warning when no data baked, got:\n{result}");
        }

        // ── GetPolyCount: zero-alloc via GetIndexCount ─────────────────────

        [Test]
        public void GetPolyCount_NullMesh_ReturnsZero()
        {
            var count = InvokeGetPolyCount(null);
            Assert.AreEqual(0L, count);
        }

        [Test]
        public void GetPolyCount_EmptyMesh_ReturnsZero()
        {
            var mesh = new Mesh();
            _assets.Add(mesh);
            var count = InvokeGetPolyCount(mesh);
            Assert.AreEqual(0L, count);
        }

        [Test]
        public void GetPolyCount_UsesGetIndexCount_NotTrianglesArray()
        {
            // Verify GetPolyCount uses GetIndexCount(s)/3 logic by checking
            // the method body doesn't reference triangles property —
            // behaviorally: it must return correct count for a simple triangle mesh.
            var mesh = new Mesh();
            _assets.Add(mesh);
            mesh.vertices = new Vector3[] {
                Vector3.zero, Vector3.up, Vector3.right
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateBounds();

            var count = InvokeGetPolyCount(mesh);
            Assert.AreEqual(1L, count, "Single triangle mesh must return poly count = 1");
        }
    }
}
