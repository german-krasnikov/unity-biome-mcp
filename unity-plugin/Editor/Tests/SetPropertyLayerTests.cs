// G22: set_property for GameObject.layer — typed mutator.
// Red: SetProperty with prop="layer" currently fails (no intercept).
// Green: intercept added in ObjectManager.Properties.cs.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetPropertyLayerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SPL_Test");
            TrackOwnedObject(_go);
            _go.layer = 0; // Default layer
        }

        [Test]
        public void SetProperty_LayerByIndex_UpdatesGameObjectLayer()
        {
            var path = ComponentSerializer.GetPath(_go);
            var result = ObjectManager.SetProperty(path, "GameObject", "layer", "5");
            Assert.AreEqual(5, _go.layer, $"layer should be 5; result: {result}");
        }

        [Test]
        public void SetProperty_LayerNormVariant_UpdatesLayer()
        {
            // Also test "m_layer" normalised name
            var path = ComponentSerializer.GetPath(_go);
            ObjectManager.SetProperty(path, "GameObject", "m_layer", "3");
            Assert.AreEqual(3, _go.layer);
        }

        [Test]
        public void SetProperty_LayerByName_UpdatesGameObjectLayer()
        {
            // "Default" layer is always index 0
            var path = ComponentSerializer.GetPath(_go);
            _go.layer = 5;
            ObjectManager.SetProperty(path, "GameObject", "layer", "Default");
            Assert.AreEqual(0, _go.layer);
        }

        [Test]
        public void SetProperty_InvalidLayerName_Throws()
        {
            var path = ComponentSerializer.GetPath(_go);
            Assert.Throws<System.ArgumentException>(
                () => ObjectManager.SetProperty(path, "GameObject", "layer", "NonExistentLayerXyz123"));
        }
    }
}
