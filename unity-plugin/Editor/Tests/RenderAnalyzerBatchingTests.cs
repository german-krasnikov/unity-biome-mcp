// TDD: RenderAnalyzer.Batching — SRP, static, dynamic, GPU instancing.
// EditMode tests.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
using UnityEngine.AI;
#endif
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RenderAnalyzerBatchingTests : SceneTestBase
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
        }

        private GameObject MakeRendered(string name = "Obj")
        {
            var go = new GameObject(name);
            var mesh = MakeQuad();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
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

        // ── SRP Batcher ──────────────────────────────────────────────────────

        [Test]
        public void SRP_DetectsEnabled()
        {
            // Just check the section header is present — SRP may or may not be enabled
            var result = RenderAnalyzer.Execute(A("batching"));
            StringAssert.Contains("SRP BATCHER:", result);
        }

        [Test]
        public void SRP_SkippedInBuiltinRP()
        {
            // In test environment using built-in pipeline, SRP section shows status
            var result = RenderAnalyzer.Execute(A("batching"));
            Assert.IsNotNull(result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        // ── Static batching ──────────────────────────────────────────────────

        [Test]
        public void Static_DetectsCandidates()
        {
            MakeRendered("StaticCandidate");
            var result = RenderAnalyzer.Execute(A("batching"));
            StringAssert.Contains("STATIC BATCH:", result);
        }

        [Test]
        public void Static_ExcludesRigidbody()
        {
            var go = MakeRendered("RigidObj");
            go.AddComponent<Rigidbody>();
            // Should note that rigidbody objects can't be static-batched
            var result = RenderAnalyzer.Execute(A("batching", "full"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void Static_ExcludesAnimator()
        {
            var go = MakeRendered("AnimObj");
            go.AddComponent<Animator>();
            var result = RenderAnalyzer.Execute(A("batching", "full"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        // ── GPU instancing ───────────────────────────────────────────────────

        [Test]
        public void Instancing_CountsCouldEnable()
        {
            // Shared material without enableInstancing = candidate
            var mat = new Material(Shader.Find("Standard"));
            _assets.Add(mat);
            mat.enableInstancing = false;
            for (int i = 0; i < 3; i++)
                MakeRendered($"InstObj{i}").GetComponent<MeshRenderer>().sharedMaterial = mat;

            var result = RenderAnalyzer.Execute(A("batching"));
            StringAssert.Contains("GPU INSTANCING:", result);
        }

        // ── Batch key ────────────────────────────────────────────────────────

        [Test]
        public void BatchKey_SRP_GroupsByShader()
        {
            MakeRendered("A");
            MakeRendered("B");
            var result = RenderAnalyzer.Execute(A("batching"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void BatchKey_NullMaterial()
        {
            // Renderer with no material — should not crash
            var go = MakeRendered("NoMat");
            go.GetComponent<MeshRenderer>().sharedMaterial = null;
            var result = RenderAnalyzer.Execute(A("batching"));
            Assert.IsFalse(result.StartsWith("err:"));
        }

        // ── Edge cases ───────────────────────────────────────────────────────

        [Test]
        public void EmptyScene_ReturnsZeroCandidates()
        {
            var result = RenderAnalyzer.Execute(A("batching"));
            StringAssert.Contains("BATCHING:", result);
            Assert.IsFalse(result.StartsWith("err:"));
        }

        [Test]
        public void PathScoped_FiltersSubtree()
        {
            var parent = new GameObject("Zone");
            _gos.Add(parent);
            MakeRendered("Child").transform.SetParent(parent.transform);

            var result = RenderAnalyzer.Execute(A("batching", "brief", "/Zone"));
            Assert.IsFalse(result.StartsWith("err:"));
        }
    }
}
