// TDD: RenderAnalyzer — stats, overdraw, materials, shaders, audit, compare.
// EditMode tests. Scene objects created/destroyed per test.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RenderAnalyzerTests
    {
        private readonly List<GameObject> _gos = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _gos)
                if (go != null) Object.DestroyImmediate(go);
            _gos.Clear();
            foreach (var a in _assets)
                if (a != null) Object.DestroyImmediate(a);
            _assets.Clear();
            RenderAnalyzer.ClearBaselineForTest();
        }

        private GameObject MakeRendered(string name = "Obj", bool transparent = false)
        {
            var go = new GameObject(name);
            var mesh = MakeQuad();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (transparent)
            {
                var mat = new Material(Shader.Find("Standard"));
                _assets.Add(mat);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                r.sharedMaterial = mat;
            }
            _gos.Add(go);
            return go;
        }

        private Mesh MakeQuad()
        {
            var m = new Mesh();
            m.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            _assets.Add(m);
            return m;
        }

        private static string A(string action, string detail = "brief", string path = null) =>
            path == null
                ? $"{{\"action\":\"{action}\",\"detail\":\"{detail}\"}}"
                : $"{{\"action\":\"{action}\",\"detail\":\"{detail}\",\"path\":\"{path}\"}}";

        // ── FormatNum ────────────────────────────────────────────────────────

        [Test]
        public void FormatNum_MillionFormat()
        {
            Assert.AreEqual("1.5M", RenderAnalyzer.FormatNum(1_500_000L));
        }

        [Test]
        public void FormatNum_ThousandFormat()
        {
            Assert.AreEqual("3.5K", RenderAnalyzer.FormatNum(3_500L));
        }

        [Test]
        public void FormatNum_SmallNumber()
        {
            Assert.AreEqual("42", RenderAnalyzer.FormatNum(42L));
        }

        // ── Stats ────────────────────────────────────────────────────────────

        [Test]
        public void Stats_EmptyScene_ReturnsZeroCounts()
        {
            var result = RenderAnalyzer.Execute(A("stats"));
            StringAssert.Contains("tris=0", result);
            StringAssert.Contains("verts=0", result);
        }

        [Test]
        public void Stats_BriefUnder120Tokens()
        {
            var result = RenderAnalyzer.Execute(A("stats"));
            // 1 token ≈ 4 chars; 120 tokens ≈ 480 chars
            Assert.Less(result.Length, 480, $"Brief stats too long: {result.Length} chars\n{result}");
        }

        [Test]
        public void Stats_FullIncludesBatchBreakdown()
        {
            var result = RenderAnalyzer.Execute(A("stats", "full"));
            StringAssert.Contains("static=", result);
            StringAssert.Contains("dynamic=", result);
            StringAssert.Contains("instanced=", result);
        }

        [Test]
        public void Stats_AutoSavesBaseline()
        {
            RenderAnalyzer.Execute(A("stats"));
            var compare = RenderAnalyzer.Execute(A("compare"));
            StringAssert.DoesNotContain("err:no baseline", compare.ToLower());
        }

        [Test]
        public void Stats_ContainsDrawCount()
        {
            var result = RenderAnalyzer.Execute(A("stats"));
            StringAssert.Contains("draw=", result);
        }

        // ── Overdraw ─────────────────────────────────────────────────────────

        [Test]
        public void Overdraw_CountsTransparentSeparately()
        {
            MakeRendered("Opaque");
            MakeRendered("Transparent", transparent: true);
            var result = RenderAnalyzer.Execute(A("overdraw"));
            StringAssert.Contains("transparent=1", result);
        }

        [Test]
        public void Overdraw_FlagsHighTransparentRatio()
        {
            MakeRendered("O1");
            for (int i = 0; i < 5; i++) MakeRendered($"T{i}", transparent: true);
            var result = RenderAnalyzer.Execute(A("overdraw"));
            StringAssert.Contains("WARN: transparent > 15%", result);
        }

        [Test]
        public void Overdraw_CountsOverlayCanvases()
        {
            var cgo = new GameObject("Canvas");
            _gos.Add(cgo);
            var canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var result = RenderAnalyzer.Execute(A("overdraw"));
            StringAssert.Contains("ui=1", result);
        }

        // ── Materials ────────────────────────────────────────────────────────

        [Test]
        public void Materials_GroupsBySharedMaterial()
        {
            var mat = new Material(Shader.Find("Standard")) { name = "SharedMat" };
            _assets.Add(mat);
            for (int i = 0; i < 3; i++)
                MakeRendered($"Obj{i}").GetComponent<MeshRenderer>().sharedMaterial = mat;
            var result = RenderAnalyzer.Execute(A("materials"));
            StringAssert.Contains("Standard", result);
        }

        // ── Shaders ──────────────────────────────────────────────────────────

        [Test]
        public void Shaders_ListsUniqueShaders()
        {
            var mat = new Material(Shader.Find("Standard"));
            _assets.Add(mat);
            MakeRendered("A").GetComponent<MeshRenderer>().sharedMaterial = mat;
            MakeRendered("B").GetComponent<MeshRenderer>().sharedMaterial = mat;
            var result = RenderAnalyzer.Execute(A("shaders"));
            StringAssert.Contains("SHADERS:", result);
            StringAssert.Contains("Standard", result);
        }

        [Test]
        public void Shaders_ReportsKeywordCount()
        {
            var mat = new Material(Shader.Find("Standard"));
            _assets.Add(mat);
            MakeRendered("S").GetComponent<MeshRenderer>().sharedMaterial = mat;
            var result = RenderAnalyzer.Execute(A("shaders"));
            StringAssert.Contains("kw:", result);
        }

        // ── Audit ────────────────────────────────────────────────────────────

        [Test]
        public void Audit_IncludesStatsSection()
        {
            var result = RenderAnalyzer.Execute(A("audit"));
            StringAssert.Contains("RENDER STATS", result);
        }

        [Test]
        public void Audit_IncludesOverdrawSection()
        {
            var result = RenderAnalyzer.Execute(A("audit"));
            StringAssert.Contains("OVERDRAW", result);
        }

        // ── Compare ──────────────────────────────────────────────────────────

        [Test]
        public void Compare_NoBaseline_ReturnsError()
        {
            // ClearBaselineForTest() already called in TearDown, but we need to ensure clean state
            RenderAnalyzer.ClearBaselineForTest();
            var result = RenderAnalyzer.Execute(A("compare"));
            StringAssert.StartsWith("err:", result);
        }

        [Test]
        public void Compare_ShowsDeltas()
        {
            RenderAnalyzer.Execute(A("stats"));  // saves baseline
            var result = RenderAnalyzer.Execute(A("compare"));
            StringAssert.Contains("COMPARE", result);
        }

        // ── Execute dispatch ─────────────────────────────────────────────────

        [Test]
        public void Execute_UnknownAction_ReturnsErr()
        {
            var result = RenderAnalyzer.Execute(A("bogus_xyz"));
            StringAssert.StartsWith("err:", result);
        }

        [Test]
        public void Execute_PathScoped_FiltersToSubtree()
        {
            var parent = new GameObject("Env");
            _gos.Add(parent);
            var child = new GameObject("Child");
            _gos.Add(child);
            child.transform.SetParent(parent.transform);
            child.AddComponent<MeshFilter>().sharedMesh = MakeQuad();
            child.AddComponent<MeshRenderer>();

            var all = RenderAnalyzer.Execute(A("overdraw"));
            var scoped = RenderAnalyzer.Execute(A("overdraw", "brief", "/Env"));
            Assert.IsNotNull(all);
            Assert.IsNotNull(scoped);
            Assert.IsFalse(scoped.StartsWith("err:"));
        }
    }
}
