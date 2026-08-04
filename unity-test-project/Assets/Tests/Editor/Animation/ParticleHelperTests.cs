using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Animation
{
    // CS3.arch.2 — ParticleHelper must throw when non-'enabled' prop is passed to toggle-only modules
    [TestFixture]
    public class ParticleHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("PH_AuditPS"));
            _go.AddComponent<ParticleSystem>();
        }

        [TestCase("colorOverLifetime")]
        [TestCase("sizeOverLifetime")]
        [TestCase("velocityOverLifetime")]
        [TestCase("rotationOverLifetime")]
        [TestCase("trails")]
        [TestCase("collision")]
        public void SetProperty_ToggleOnlyModule_NonEnabledProp_ThrowsArgumentException(string module)
        {
            var path = ComponentSerializer.GetPath(_go);
            Assert.Throws<System.ArgumentException>(() =>
                ParticleHelper.SetProperty(path, module, "strength", "0.5"),
                $"Module '{module}' should throw ArgumentException for prop='strength'");
        }

        [TestCase("colorOverLifetime")]
        [TestCase("trails")]
        public void SetProperty_ToggleOnlyModule_EnabledProp_Succeeds(string module)
        {
            var path = ComponentSerializer.GetPath(_go);
            Assert.DoesNotThrow(() =>
                ParticleHelper.SetProperty(path, module, "enabled", "true"));
        }
    }
}
