using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ComponentSerializerSpecialCharTests : SceneCleanTestBase
    {
        private readonly List<GameObject> _toDestroy = new();

        [TearDown]
        public void DestroyTestObjects()
        {
            foreach (var go in _toDestroy)
                if (go) Object.DestroyImmediate(go);
            _toDestroy.Clear();
        }

        [TestCase("Normal")]
        [TestCase("[MECHANICS/ZONE TEMPLATE]")]
        [TestCase("My Object")]
        [TestCase("Day/Night")]
        [TestCase("path\\to\\thing")]
        [TestCase("[A/B]")]
        [TestCase("[back\\slash]")]
        [TestCase("Объект")]
        public void RoundTrip_GetPath_FindObject(string name)
        {
            var go = new GameObject(name);
            _toDestroy.Add(go);
            var path = ComponentSerializer.GetPath(go);
            var found = ComponentSerializer.FindObject(path);
            Assert.That(found, Is.EqualTo(go),
                $"Round-trip failed: name='{name}' path='{path}'");
        }

        [TestCase("Normal")]
        [TestCase("[MECHANICS/ZONE TEMPLATE]")]
        [TestCase("My Object")]
        [TestCase("Day/Night")]
        [TestCase("path\\to\\thing")]
        [TestCase("[A/B]")]
        [TestCase("[back\\slash]")]
        [TestCase("Объект")]
        public void RoundTrip_NestedChild_SpecialParentName(string parentName)
        {
            var parent = new GameObject(parentName);
            _toDestroy.Add(parent);
            var child = new GameObject("NormalChild");
            child.transform.SetParent(parent.transform);

            var path = ComponentSerializer.GetPath(child);
            var found = ComponentSerializer.FindObject(path);
            Assert.That(found, Is.EqualTo(child),
                $"Nested round-trip failed: parent='{parentName}' path='{path}'");
        }

        [TestCase("[MECHANICS/ZONE TEMPLATE]")]
        [TestCase("My Object")]
        [TestCase("Day/Night")]
        [TestCase("[A/B]")]
        public void PlaytestParser_Assert_SpecialCharPath_NotCorrupted(string name)
        {
            // PlaytestParser must not mangle paths containing special chars
            var line = $"ASSERT /{name}/Child|Transform|localPosition.x == 0";
            var steps = PlaytestParser.Parse(line);
            Assert.AreEqual(1, steps.Count, "Expected 1 step");
            StringAssert.Contains($"/{name}/Child", steps[0].Query,
                $"Path with '{name}' was corrupted by parser");
        }
    }
}
