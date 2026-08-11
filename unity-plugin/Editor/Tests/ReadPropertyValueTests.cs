// TDD Red: ReadPropertyValue — read a serialized property without writing.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ReadPropertyValueTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("RPV_Test"));
            _go.AddComponent<BoxCollider>();
        }

        [Test]
        public void ReadPropertyValue_ExistingProp_ReturnsCurrentValue()
        {
            var result = ObjectManager.ReadPropertyValue("/RPV_Test", "BoxCollider", "m_Size");
            Assert.IsNotNull(result);
            StringAssert.Contains("1", result);
        }

        [Test]
        public void ReadPropertyValue_NonExistentProp_ReturnsNull()
        {
            var result = ObjectManager.ReadPropertyValue("/RPV_Test", "BoxCollider", "nonExistentProp_xyz");
            Assert.IsNull(result);
        }

        [Test]
        public void ReadPropertyValue_ReflectsValueAfterWrite()
        {
            ObjectManager.SetProperty("/RPV_Test", "BoxCollider", "m_Size", "(2,2,2)");
            var result = ObjectManager.ReadPropertyValue("/RPV_Test", "BoxCollider", "m_Size");
            Assert.IsNotNull(result);
            StringAssert.Contains("2", result);
        }
    }
}
