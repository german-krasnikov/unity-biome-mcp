// NUnit tests for InputNormalizer.NormalizeProperty — S3 Audio/Light aliases.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class InputNormalizerAudioLightAliasTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("IN_AudioLight_TestObj");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ── AudioSource aliases ──────────────────────────────────────────────
        // Verified against UnityCsReference AudioSourceInspector.cs (branch 6000.0,
        // matching this package's minimum Unity version). AudioSource fields do NOT
        // follow a uniform "m_PascalCase" convention — several are plain PascalCase
        // with no "m_" prefix.

        [Test]
        public void NormalizeProperty_AudioClip_ReturnsResource()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("m_Resource", InputNormalizer.NormalizeProperty("audioClip", so));
        }

        [Test]
        public void NormalizeProperty_Mute_ReturnsMute()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("Mute", InputNormalizer.NormalizeProperty("mute", so));
        }

        [Test]
        public void NormalizeProperty_Pitch_ReturnsSerialized()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("m_Pitch", InputNormalizer.NormalizeProperty("pitch", so));
        }

        [Test]
        public void NormalizeProperty_MinDistance_ReturnsMinDistance()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("MinDistance", InputNormalizer.NormalizeProperty("minDistance", so));
        }

        [Test]
        public void NormalizeProperty_MaxDistance_ReturnsMaxDistance()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("MaxDistance", InputNormalizer.NormalizeProperty("maxDistance", so));
        }

        [Test]
        public void NormalizeProperty_Priority_ReturnsPriority()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("Priority", InputNormalizer.NormalizeProperty("priority", so));
        }

        [Test]
        public void NormalizeProperty_DopplerLevel_ReturnsDopplerLevel()
        {
            var so = new SerializedObject(_go.AddComponent<AudioSource>());
            Assert.AreEqual("DopplerLevel", InputNormalizer.NormalizeProperty("dopplerLevel", so));
        }

        // NOTE: "spatialBlend" is intentionally NOT aliased. Unity has no direct
        // scalar field for it — the inspector drives it entirely through the
        // "panLevelCustomCurve" AnimationCurve (see AudioSourceInspector.cs).
        // Aliasing to a fake "m_SpatialBlend" would silently no-op and mislead.

        // ── Light aliases ────────────────────────────────────────────────────
        // Verified against UnityCsReference LightEditor.cs (branch 6000.0).
        // Shadow fields live under the nested "m_Shadows" struct.

        [Test]
        public void NormalizeProperty_ShadowStrength_ReturnsShadowsStrength()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_Shadows.m_Strength", InputNormalizer.NormalizeProperty("shadowStrength", so));
        }

        [Test]
        public void NormalizeProperty_ShadowBias_ReturnsShadowsBias()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_Shadows.m_Bias", InputNormalizer.NormalizeProperty("shadowBias", so));
        }

        [Test]
        public void NormalizeProperty_ShadowNormalBias_ReturnsShadowsNormalBias()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_Shadows.m_NormalBias", InputNormalizer.NormalizeProperty("shadowNormalBias", so));
        }

        [Test]
        public void NormalizeProperty_LightType_ReturnsType()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_Type", InputNormalizer.NormalizeProperty("lightType", so));
        }

        [Test]
        public void NormalizeProperty_BounceIntensity_ReturnsBounceIntensity()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_BounceIntensity", InputNormalizer.NormalizeProperty("bounceIntensity", so));
        }

        [Test]
        public void NormalizeProperty_CookieSize_ReturnsCookieSize()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_CookieSize", InputNormalizer.NormalizeProperty("cookieSize", so));
        }

        [Test]
        public void NormalizeProperty_InnerSpotAngle_ReturnsInnerSpotAngle()
        {
            var so = new SerializedObject(_go.AddComponent<Light>());
            Assert.AreEqual("m_InnerSpotAngle", InputNormalizer.NormalizeProperty("innerSpotAngle", so));
        }
    }
}
