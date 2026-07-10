// NUnit tests for ObjectManager.SetPropertyDelta — CS2.test.5.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetPropertyDeltaTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("SPD_TestObj");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ── Float delta ───────────────────────────────────────────────────────

        [Test]
        public void SetPropertyDelta_Float_PositiveDelta_Increments()
        {
            _go.AddComponent<Light>().intensity = 1f;
            ObjectManager.SetPropertyDelta("/SPD_TestObj", "Light", "m_Intensity", "+0.5");
            Assert.AreEqual(1.5f, _go.GetComponent<Light>().intensity, 0.001f);
        }

        [Test]
        public void SetPropertyDelta_Float_NegativeDelta_Decrements()
        {
            _go.AddComponent<Light>().intensity = 2f;
            ObjectManager.SetPropertyDelta("/SPD_TestObj", "Light", "m_Intensity", "-0.5");
            Assert.AreEqual(1.5f, _go.GetComponent<Light>().intensity, 0.001f);
        }

        // ── Integer delta ─────────────────────────────────────────────────────

        [Test]
        public void SetPropertyDelta_Integer_PositiveDelta_Increments()
        {
            // Camera.cullingMask is an integer serialized field
            _go.AddComponent<Camera>();
            var cam = _go.GetComponent<Camera>();
            cam.cullingMask = 3;

            ObjectManager.SetPropertyDelta("/SPD_TestObj", "Camera", "m_CullingMask", "+2");
            Assert.AreEqual(5, cam.cullingMask);
        }

        // ── Vector3 delta ─────────────────────────────────────────────────────

        [Test]
        public void SetPropertyDelta_Vector3_AppliesOffset()
        {
            _go.transform.localPosition = Vector3.zero;
            ObjectManager.SetPropertyDelta("/SPD_TestObj", "Transform", "m_LocalPosition", "(1,2,3)");
            Assert.AreEqual(1f, _go.transform.localPosition.x, 0.001f);
            Assert.AreEqual(2f, _go.transform.localPosition.y, 0.001f);
            Assert.AreEqual(3f, _go.transform.localPosition.z, 0.001f);
        }

        // ── Unsupported type throws ───────────────────────────────────────────

        [Test]
        public void SetPropertyDelta_UnsupportedType_ThrowsArgumentException()
        {
            // m_Name is a String property — unsupported for delta
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetPropertyDelta("/SPD_TestObj", "Transform", "m_Name", "+1"));
        }

        // ── Returns delta description ─────────────────────────────────────────

        [Test]
        public void SetPropertyDelta_ReturnsBeforeArrowAfter()
        {
            _go.AddComponent<Light>().intensity = 1f;
            var result = ObjectManager.SetPropertyDelta("/SPD_TestObj", "Light", "m_Intensity", "+1");
            StringAssert.Contains("→", result);
        }
    }
}
