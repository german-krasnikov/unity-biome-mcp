// TDD — RED first. EditMode tests proving RuntimeHelper.FindComponent delegates to
// ComponentSerializer.FindComponent (fuzzy/alias-aware). Previously exact-match-only.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperFindComponentTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("RHFindComp_Test");
            _go.AddComponent<Rigidbody>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        // Proves alias lookup: "RigidBody" → Rigidbody (old exact-match returned null)
        [Test]
        public void FindComponent_AliasInput_ReturnsComponent()
        {
            var result = RuntimeHelper.FindComponentInternal(_go, "RigidBody");
            Assert.IsNotNull(result, "Expected Rigidbody via alias 'RigidBody'");
            Assert.IsInstanceOf<Rigidbody>(result);
        }

        // Proves case-insensitive: "rigidbody" → Rigidbody (old exact-match returned null)
        [Test]
        public void FindComponent_CaseInsensitiveInput_ReturnsComponent()
        {
            var result = RuntimeHelper.FindComponentInternal(_go, "rigidbody");
            Assert.IsNotNull(result, "Expected Rigidbody via case-insensitive 'rigidbody'");
            Assert.IsInstanceOf<Rigidbody>(result);
        }

        // Sanity: exact match still works
        [Test]
        public void FindComponent_ExactInput_ReturnsComponent()
        {
            var result = RuntimeHelper.FindComponentInternal(_go, "Rigidbody");
            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Rigidbody>(result);
        }

        // Missing component returns null (no regression)
        [Test]
        public void FindComponent_MissingComponent_ReturnsNull()
        {
            var result = RuntimeHelper.FindComponentInternal(_go, "Camera");
            Assert.IsNull(result);
        }
    }
}
