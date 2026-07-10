// TDD — MultiViewCapture pure-logic tests (no GPU, no file I/O).
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class MultiViewCaptureTests : SceneTestBase
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        // ── CameraLookAt via reflection ─────────────────────────────────────

        private static Vector3 InvokeCameraLookAt(Bounds bounds, Quaternion rot, float dist, out float orthoSize)
        {
            var method = typeof(MultiViewCapture).GetMethod(
                "CameraLookAt",
                BindingFlags.NonPublic | BindingFlags.Static);
            var args = new object[] { bounds, rot, dist, 0f };
            var result = (Vector3)method.Invoke(null, args);
            orthoSize = (float)args[3];
            return result;
        }

        [Test]
        public void CameraLookAt_PointsAtTarget_FrontView()
        {
            // Camera at +Z distance looking back: position = center - forward * dist
            var bounds = new Bounds(Vector3.zero, Vector3.one);
            var rot = Quaternion.LookRotation(Vector3.back, Vector3.up);  // front view
            float dist = 10f;

            var pos = InvokeCameraLookAt(bounds, rot, dist, out _);

            // forward = back direction, so center - back * dist = +Z * dist
            Assert.AreEqual(Vector3.zero.z + dist, pos.z, 0.001f);
            Assert.AreEqual(0f, pos.x, 0.001f);
            Assert.AreEqual(0f, pos.y, 0.001f);
        }

        [Test]
        public void CameraLookAt_DistanceFromBounds_ScalesOrthoSize()
        {
            // Large bounds → larger orthoSize
            var small = new Bounds(Vector3.zero, Vector3.one);
            var large = new Bounds(Vector3.zero, Vector3.one * 10f);
            var rot = Quaternion.LookRotation(Vector3.back, Vector3.up);

            InvokeCameraLookAt(small, rot, 5f, out float smallOrtho);
            InvokeCameraLookAt(large, rot, 50f, out float largeOrtho);

            Assert.Greater(largeOrtho, smallOrtho);
        }

        [Test]
        public void CameraLookAt_TopView_PositionAboveCenter()
        {
            var bounds = new Bounds(new Vector3(1f, 2f, 3f), Vector3.one * 2f);
            var rot = Quaternion.Euler(90f, 0f, 0f);  // top view
            float dist = 15f;

            var pos = InvokeCameraLookAt(bounds, rot, dist, out _);

            // Top camera: position is above center (y > bounds.center.y)
            Assert.Greater(pos.y, bounds.center.y);
        }

        // ── ComputeBounds ───────────────────────────────────────────────────

        [Test]
        public void ComputeBounds_EmptyRenderers_ReturnsFallbackAroundPosition()
        {
            // No renderers, no colliders → fallback: center at go.position, size 3x3x3
            _root.transform.position = new Vector3(5f, 0f, 0f);

            var b = MultiViewCapture.ComputeBounds(_root);

            Assert.AreEqual(_root.transform.position, b.center);
            Assert.AreEqual(3f, b.size.x, 0.001f);
        }

        [Test]
        public void ComputeBounds_MultipleRenderers_Encapsulates()
        {
            // Two children with MeshFilter+MeshRenderer far apart → bounds contains both
            var childA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            childA.transform.SetParent(_root.transform);
            childA.transform.position = new Vector3(-10f, 0f, 0f);

            var childB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            childB.transform.SetParent(_root.transform);
            childB.transform.position = new Vector3(10f, 0f, 0f);

            var b = MultiViewCapture.ComputeBounds(_root);

            Assert.Less(b.min.x, -9f,  "min.x should be left of childA");
            Assert.Greater(b.max.x, 9f, "max.x should be right of childB");

            Object.DestroyImmediate(childA);
            Object.DestroyImmediate(childB);
        }

        // ── GetStandardView ─────────────────────────────────────────────────

        [Test]
        public void GetStandardView_Front_CameraFacesBack()
        {
            var center = Vector3.zero;
            float dist = 10f;

            var view = MultiViewCapture.GetStandardView("front", center, dist);

            // Front: pos = center + forward*dist = (0,0,dist)
            Assert.AreEqual(dist, view.pos.z, 0.001f);
        }

        [Test]
        public void GetStandardView_Top_CameraAboveCenter()
        {
            var center = Vector3.zero;
            float dist = 10f;

            var view = MultiViewCapture.GetStandardView("top", center, dist);

            Assert.AreEqual(dist, view.pos.y, 0.001f);
        }

        [Test]
        public void GetStandardView_Unknown_FallsBackToFront()
        {
            var center = Vector3.zero;
            float dist = 5f;

            var view = MultiViewCapture.GetStandardView("bogus", center, dist);

            // Fallback = front view: pos.z = dist
            Assert.AreEqual(dist, view.pos.z, 0.001f);
        }

        [Test]
        public void GetStandardView_CustomEuler_ParsesAngles()
        {
            // "0,90,0" = rotated 90° around Y → forward = left (-X)
            var view = MultiViewCapture.GetStandardView("0,90,0", Vector3.zero, 10f);

            // rot = Euler(0,90,0); forward = (1,0,0); pos = center - dir*dist = (-10,0,0)
            Assert.AreEqual(-10f, view.pos.x, 0.01f);
        }
    }
}
