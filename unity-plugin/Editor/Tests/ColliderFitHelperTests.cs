using System;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ColliderFitHelperTests : SceneTestBase
    {
        private GameObject _go;
        private Mesh _mesh;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CFit_TestObj");
            var mf = _go.AddComponent<MeshFilter>();
            _mesh = CreateUnitCube();
            mf.sharedMesh = _mesh;
            _go.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_mesh != null) UnityEngine.Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void AutoFit_BoxCollider_MatchesRendererBounds()
        {
            var bounds = ColliderFitHelper.GetLocalBounds(_go).Value;
            var json = $"{{\"path\":\"/{_go.name}\",\"type\":\"box\"}}";
            var result = ColliderFitHelper.Execute(json);

            StringAssert.Contains("BoxCollider fitted", result);
            var col = _go.GetComponent<BoxCollider>();
            Assert.IsNotNull(col);
            Assert.AreEqual(bounds.center.x, col.center.x, 0.001f);
            Assert.AreEqual(bounds.center.y, col.center.y, 0.001f);
            Assert.AreEqual(bounds.center.z, col.center.z, 0.001f);
            Assert.AreEqual(bounds.size.x, col.size.x, 0.001f);
            Assert.AreEqual(bounds.size.y, col.size.y, 0.001f);
            Assert.AreEqual(bounds.size.z, col.size.z, 0.001f);
        }

        [Test]
        public void AutoFit_SphereCollider_MatchesRendererBounds()
        {
            var bounds = ColliderFitHelper.GetLocalBounds(_go).Value;
            var json = $"{{\"path\":\"/{_go.name}\",\"type\":\"sphere\"}}";
            var result = ColliderFitHelper.Execute(json);

            StringAssert.Contains("SphereCollider fitted", result);
            var col = _go.GetComponent<SphereCollider>();
            Assert.IsNotNull(col);
            var expectedRadius = bounds.extents.magnitude;
            Assert.AreEqual(expectedRadius, col.radius, 0.001f);
            Assert.AreEqual(bounds.center.x, col.center.x, 0.001f);
            Assert.AreEqual(bounds.center.y, col.center.y, 0.001f);
            Assert.AreEqual(bounds.center.z, col.center.z, 0.001f);
        }

        [Test]
        public void AutoFit_CapsuleCollider_MatchesRendererBounds()
        {
            // Unit cube: extents (0.5, 0.5, 0.5) — Y wins ties, dir=1
            var bounds = ColliderFitHelper.GetLocalBounds(_go).Value;
            var json = $"{{\"path\":\"/{_go.name}\",\"type\":\"capsule\"}}";
            var result = ColliderFitHelper.Execute(json);

            StringAssert.Contains("CapsuleCollider fitted", result);
            var col = _go.GetComponent<CapsuleCollider>();
            Assert.IsNotNull(col);
            // Y >= X && Y >= Z → direction=1
            Assert.AreEqual(1, col.direction);
            Assert.AreEqual(bounds.extents.y * 2f, col.height, 0.001f);
            var expectedRadius = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
            Assert.AreEqual(expectedRadius, col.radius, 0.001f);
        }

        [Test]
        public void AutoFit_NoRenderer_Throws()
        {
            var bare = new GameObject("CFit_Bare");
            try
            {
                var json = $"{{\"path\":\"/{bare.name}\",\"type\":\"box\"}}";
                var ex = Assert.Throws<ArgumentException>(() => ColliderFitHelper.Execute(json));
                StringAssert.Contains("no Renderer or MeshFilter", ex.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void AutoFit_UnknownType_Throws()
        {
            var json = $"{{\"path\":\"/{_go.name}\",\"type\":\"cylinder\"}}";
            var ex = Assert.Throws<ArgumentException>(() => ColliderFitHelper.Execute(json));
            StringAssert.Contains("unknown collider type", ex.Message);
        }

        [Test]
        public void AutoFit_AddsColliderIfMissing()
        {
            Assert.IsNull(_go.GetComponent<BoxCollider>());
            var json = $"{{\"path\":\"/{_go.name}\",\"type\":\"box\"}}";
            ColliderFitHelper.Execute(json);
            Assert.IsNotNull(_go.GetComponent<BoxCollider>());
        }

        [Test]
        public void AutoFit_IsRegistered()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("autofit_collider"));
        }

        /// <summary>Creates a 1x1x1 cube mesh centered at origin.</summary>
        private static Mesh CreateUnitCube()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f),
            };
            mesh.triangles = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,1,5, 0,5,4, 2,3,7, 2,7,6, 0,4,7, 0,7,3, 1,2,6, 1,6,5 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
