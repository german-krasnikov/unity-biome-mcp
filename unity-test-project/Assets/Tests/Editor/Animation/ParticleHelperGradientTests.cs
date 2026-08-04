using NUnit.Framework;
using System;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Animation
{
    // L2: VFX gradient/curve support — ParseGradient, ParseCurve, SetColorOverLifetime, etc.
    [TestFixture]
    public class ParticleHelperGradientTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("PH_GradientPS"));
            _go.AddComponent<ParticleSystem>();
        }

        // --- ParseGradient ---

        [Test]
        public void ParseGradient_TwoKeys_ReturnsTwoColorKeys()
        {
            var g = ParticleHelper.ParseGradient("#FF0000@0;#00FF00@1");
            Assert.AreEqual(2, g.colorKeys.Length);
            Assert.AreEqual(0f, g.colorKeys[0].time, 0.001f);
            Assert.AreEqual(1f, g.colorKeys[1].time, 0.001f);
        }

        [Test]
        public void ParseGradient_ThreeKeys_ReturnsThreeColorKeys()
        {
            var g = ParticleHelper.ParseGradient("#FF0000@0;#FFFF00@0.5;#000000@1");
            Assert.AreEqual(3, g.colorKeys.Length);
            Assert.AreEqual(0.5f, g.colorKeys[1].time, 0.001f);
        }

        [Test]
        public void ParseGradient_InvalidHex_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                ParticleHelper.ParseGradient("ZZZZZZ@0;#00FF00@1"));
        }

        [Test]
        public void ParseGradient_TooManyKeys_ThrowsArgumentException()
        {
            // 9 keys exceeds Unity hard limit of 8
            var tooMany = "#FF0000@0;#EE0000@0.1;#DD0000@0.2;#CC0000@0.3;#BB0000@0.4;#AA0000@0.5;#990000@0.6;#880000@0.7;#770000@1";
            Assert.Throws<ArgumentException>(() => ParticleHelper.ParseGradient(tooMany));
        }

        [Test]
        public void ParseGradient_WithoutHash_StillParses()
        {
            // # is optional — should be prepended if missing
            var g = ParticleHelper.ParseGradient("FF0000@0;00FF00@1");
            Assert.AreEqual(2, g.colorKeys.Length);
        }

        // --- ParseCurve ---

        [Test]
        public void ParseCurve_TwoKeys_ReturnsTwoKeyframes()
        {
            var c = ParticleHelper.ParseCurve("0:0.5;1:1.0");
            Assert.AreEqual(2, c.length);
            Assert.AreEqual(0f, c.keys[0].time, 0.001f);
            Assert.AreEqual(0.5f, c.keys[0].value, 0.001f);
            Assert.AreEqual(1f, c.keys[1].time, 0.001f);
        }

        [Test]
        public void ParseCurve_SingleKey_Works()
        {
            var c = ParticleHelper.ParseCurve("0.5:1.0");
            Assert.AreEqual(1, c.length);
            Assert.AreEqual(0.5f, c.keys[0].time, 0.001f);
        }

        [Test]
        public void ParseCurve_ThreeKeys_SmoothsTangents()
        {
            // Just verify it doesn't throw and returns 3 keys
            var c = ParticleHelper.ParseCurve("0:0;0.5:1;1:0");
            Assert.AreEqual(3, c.length);
        }

        // --- SetPropertyDirect integration ---

        [Test]
        public void SetPropertyDirect_ColorOverLifetime_Gradient_DoesNotThrow()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetPropertyDirect(ps, "colorOverLifetime", "gradient", "#FF0000@0;#00FF00@1"));
        }

        [Test]
        public void SetPropertyDirect_SizeOverLifetime_Curve_DoesNotThrow()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetPropertyDirect(ps, "sizeOverLifetime", "curve", "0:0.5;1:1.0"));
        }

        [Test]
        public void SetPropertyDirect_VelocityOverLifetime_X_DoesNotThrow()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetPropertyDirect(ps, "velocityOverLifetime", "x", "0:0;1:3"));
        }

        [Test]
        public void SetPropertyDirect_VelocityOverLifetime_Space_DoesNotThrow()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetPropertyDirect(ps, "velocityOverLifetime", "space", "World"));
        }

        [Test]
        public void SetPropertyDirect_ColorOverLifetime_Enabled_StillWorks()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetPropertyDirect(ps, "colorOverLifetime", "enabled", "true"));
        }

        [Test]
        public void SetPropertyDirect_ColorOverLifetime_UnknownProp_Throws()
        {
            var ps = _go.GetComponent<ParticleSystem>();
            Assert.Throws<ArgumentException>(() =>
                ParticleHelper.SetPropertyDirect(ps, "colorOverLifetime", "strength", "0.5"));
        }
    }
}
