using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EnvironmentHelperTests
    {
        private Color _savedAmbientLight;
        private bool _savedFog;
        private FogMode _savedFogMode;

        [SetUp]
        public void SetUp()
        {
            _savedAmbientLight = RenderSettings.ambientLight;
            _savedFog = RenderSettings.fog;
            _savedFogMode = RenderSettings.fogMode;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.ambientLight = _savedAmbientLight;
            RenderSettings.fog = _savedFog;
            RenderSettings.fogMode = _savedFogMode;
        }

        // ── Get ─────────────────────────────────────────────────────────────

        [Test]
        public void Get_ReturnsCurrentSettings()
        {
            var result = EnvironmentHelper.Execute("get", "{}");

            StringAssert.Contains("ambientMode:", result);
            StringAssert.Contains("fog:", result);
            StringAssert.Contains("fogColor:", result);
            StringAssert.Contains("skybox:", result);
            StringAssert.Contains("reflectionIntensity:", result);
            StringAssert.Contains("reflectionBounces:", result);
        }

        // ── Set ambient ─────────────────────────────────────────────────────

        [Test]
        public void Set_AmbientColor_ChangesValue()
        {
            EnvironmentHelper.Execute("set",
                "{\"prop\":\"ambientLight\",\"value\":\"#FF0000\"}");

            Assert.AreEqual(1f, RenderSettings.ambientLight.r, 0.01f);
            Assert.AreEqual(0f, RenderSettings.ambientLight.g, 0.01f);
            Assert.AreEqual(0f, RenderSettings.ambientLight.b, 0.01f);
        }

        // ── Set fog ─────────────────────────────────────────────────────────

        [Test]
        public void Set_FogEnabled_TogglesCorrectly()
        {
            RenderSettings.fog = false;
            EnvironmentHelper.Execute("set",
                "{\"prop\":\"fog\",\"value\":\"true\"}");

            Assert.IsTrue(RenderSettings.fog);
        }

        // ── Unknown property ────────────────────────────────────────────────

        [Test]
        public void Set_UnknownProperty_ReturnsError()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                EnvironmentHelper.Execute("set",
                    "{\"prop\":\"blahblah\",\"value\":\"42\"}"));

            StringAssert.Contains("Unknown property", ex.Message);
            StringAssert.Contains("blahblah", ex.Message);
        }

        // ── Registration ────────────────────────────────────────────────────

        [Test]
        public void Get_IsRegistered()
        {
            // RegisterAll must have been called by test setup
            Assert.IsTrue(CommandRegistry.IsRegistered("scene_environment"));
        }
    }
}
