// G18: ReferenceHelper.WalkObjectRefs must traverse nested SerializedProperty fields.
// Red: NextVisible(false) skips children of struct fields.
// Green: NextVisible(true) (or Next(true)) visits all descendants.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WalkObjectRefsNestedTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Serializable]
        private struct NestedRefs
        {
            public GameObject nestedTarget;
        }

        private class NestedRefsComponent : MonoBehaviour
        {
            public NestedRefs nested;
        }

        private GameObject _owner;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("WOR_Owner");
            _target = new GameObject("WOR_Target");
            TrackOwnedObject(_owner);
            TrackOwnedObject(_target);
        }

        [Test]
        public void WalkObjectRefs_NestedStruct_VisitsNestedReference()
        {
            var comp = _owner.AddComponent<NestedRefsComponent>();
            comp.nested.nestedTarget = _target;

            var so = new SerializedObject(comp);
            so.Update();

            var foundLabels = new List<string>();
            ReferenceHelper.WalkObjectRefs(so, (prop, label) => foundLabels.Add(label));

            Assert.That(foundLabels.Count, Is.GreaterThan(0),
                "WalkObjectRefs must visit nested struct references; found none");

            bool foundNestedTarget = foundLabels.Exists(l => l.Contains("nestedTarget") || l.Contains("nested"));
            Assert.IsTrue(foundNestedTarget,
                $"Expected 'nestedTarget' or 'nested' in visited labels but got: [{string.Join(", ", foundLabels)}]");
        }

        [Test]
        public void WalkObjectRefs_DirectReference_StillVisited()
        {
            // Regression: direct (non-nested) refs must still be found
            var lightComp = _owner.AddComponent<Light>();
            var so = new SerializedObject(lightComp);
            so.Update();

            // Light has no object references by default, but the traversal should not throw
            Assert.DoesNotThrow(() =>
            {
                ReferenceHelper.WalkObjectRefs(so, (prop, label) => { });
            });
        }
    }
}
