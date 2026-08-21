using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.SceneObject
{
    [TestFixture]
    public class ReferenceHelperClassifyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _root;
        private GameObject _child;
        private GameObject _external;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ClassifyRoot");
            _child = new GameObject("ClassifyChild");
            _child.transform.SetParent(_root.transform);
            _external = new GameObject("ClassifyExternal");
            RegisterCleanup(() =>
            {
                Object.DestroyImmediate(_root);
                Object.DestroyImmediate(_external);
            });
        }

        [Test]
        public void ClassifyRef_SameObject_ReturnsSelf()
        {
            var ownerPath = ComponentSerializer.GetPath(_root);
            Assert.That(ReferenceHelper.ClassifyRef(ownerPath, _root), Is.EqualTo("self"));
        }

        [Test]
        public void ClassifyRef_ReferencedIsChild_ReturnsChild()
        {
            var ownerPath = ComponentSerializer.GetPath(_root);
            Assert.That(ReferenceHelper.ClassifyRef(ownerPath, _child), Is.EqualTo("child"));
        }

        [Test]
        public void ClassifyRef_ReferencedIsParent_ReturnsParent()
        {
            var ownerPath = ComponentSerializer.GetPath(_child);
            Assert.That(ReferenceHelper.ClassifyRef(ownerPath, _root), Is.EqualTo("parent"));
        }

        [Test]
        public void ClassifyRef_UnrelatedObject_ReturnsExternal()
        {
            var ownerPath = ComponentSerializer.GetPath(_root);
            Assert.That(ReferenceHelper.ClassifyRef(ownerPath, _external), Is.EqualTo("external"));
        }
    }
}
