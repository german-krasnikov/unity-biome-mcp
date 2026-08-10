// TDD — Task 4: Unified Object ID ($HEX format) integration tests.
// Verify end-to-end that $HEX format is emitted and consumed correctly.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UnifiedObjectIdIntegrationTests : SceneTestBase
    {
        // Lightweight holder to get a serialized ObjectReference field in tests.
        private sealed class GoRefHolder : ScriptableObject
        {
            public Object Value;
        }

        [Test]
        public void GetPropertyValueString_ObjectRef_DollarHexFormat()
        {
            // Arrange: GO as a serialized ObjectReference target
            var target = TrackOwnedObject(new GameObject("HexRefTarget"));
            var holder = TrackOwnedObject(ScriptableObject.CreateInstance<GoRefHolder>());

            var so = new SerializedObject(holder);
            var prop = so.FindProperty("Value");
            prop.objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();
            prop = so.FindProperty("Value");

            // Act
            var result = ComponentSerializer.GetPropertyValueString(prop);

            // Assert: $HEX format used, NOT #decimal
            Assert.IsTrue(Regex.IsMatch(result, @"\$[0-9A-F]+"),
                $"ObjectRef output must contain $HEX (uppercase), got: {result}");
            Assert.IsFalse(result.Contains(" #"),
                $"ObjectRef output must NOT use # prefix for ID, got: {result}");
        }

        [Test]
        public void HierarchySerializer_Output_ContainsDollarHex()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("HexInHierarchy"));

            // Act: use public API which calls SerializeObject internally
            var output = HierarchySerializer.SerializeSubtree(go);

            // Assert: $HEX present and uppercase only
            Assert.IsTrue(Regex.IsMatch(output, @"\$[0-9A-F]+"),
                $"Hierarchy must contain $HEX (uppercase), got: {output}");
            Assert.IsFalse(Regex.IsMatch(output, @"\$[a-z]"),
                $"Hierarchy must NOT contain lowercase after $, got: {output}");
        }

        [Test]
        public void FindObject_DollarHexId_Resolves()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("FindByHexRef"));
            var hexRef = TransientObjectId.GetHexRef(go);

            // Act
            var found = ComponentSerializer.FindObject(hexRef);

            // Assert
            Assert.AreSame(go, found,
                $"FindObject($HEX) must return the original GO (hexRef={hexRef})");
        }

        [Test]
        public void SetObjectReference_DollarHex_SetsProperty()
        {
            // Arrange
            var target = TrackOwnedObject(new GameObject("SetHexRefTarget"));
            var holder = TrackOwnedObject(ScriptableObject.CreateInstance<GoRefHolder>());
            var hexRef = TransientObjectId.GetHexRef(target);

            var so = new SerializedObject(holder);
            var prop = so.FindProperty("Value");

            // Act: ValueParser handles $HEX as ObjectReference
            ValueParser.SetPropertyValue(prop, hexRef);
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();

            // Assert
            var stored = so.FindProperty("Value").objectReferenceValue;
            Assert.AreSame(target, stored,
                $"SetPropertyValue($HEX) must set objectReferenceValue to target GO (hexRef={hexRef})");
        }
    }
}
