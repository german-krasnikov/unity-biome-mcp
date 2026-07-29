using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ComponentSerializerBracketFinderTests : SceneCleanTestBase
    {
        private readonly List<GameObject> _toDestroy = new();

        [TearDown]
        public void DestroyTestObjects()
        {
            foreach (var go in _toDestroy)
                if (go) Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        [Test]
        public void FindGameObject_BracketNameWithSlash_FindsCorrectObject()
        {
            // Bug: Split('/') on "[Zone A/Zone B]/Child" → ["[Zone A", "Zone B]", "Child"] (wrong)
            var parent = new GameObject("[Zone A/Zone B]");
            _toDestroy.Add(parent);
            var child = new GameObject("Child");
            child.transform.SetParent(parent.transform);

            var found = SceneObjectFinder.FindGameObject("/[Zone A/Zone B]/Child");

            Assert.That(found, Is.EqualTo(child),
                "SceneObjectFinder must handle brackets containing '/' in segment names");
        }

        [Test]
        public void FindGameObject_NormalPath_StillWorks()
        {
            // Regression guard: normal paths must not break after the fix
            var go = new GameObject("NormalObject");
            _toDestroy.Add(go);
            var child = new GameObject("ChildObject");
            child.transform.SetParent(go.transform);

            var found = SceneObjectFinder.FindGameObject("/NormalObject/ChildObject");

            Assert.That(found, Is.EqualTo(child));
        }
    }
}
