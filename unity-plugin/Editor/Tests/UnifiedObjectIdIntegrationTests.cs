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
        public void GetPropertyValueString_ObjectRef_EmitsAmpRef()
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

            // Assert: &ref format (RefManager), NOT $HEX or #decimal
            Assert.IsTrue(Regex.IsMatch(result, @"&[0-9a-zA-Z]+"),
                $"ObjectRef output must contain &ref (RefManager base62), got: {result}");
            Assert.IsFalse(Regex.IsMatch(result, @"\$[0-9A-F]+"),
                $"ObjectRef output must NOT use $HEX format, got: {result}");
            Assert.IsFalse(result.Contains(" #"),
                $"ObjectRef output must NOT use #decimal format, got: {result}");
        }

        [Test]
        public void SetObjectReference_AmpRef_ResolvesToObject()
        {
            var target = TrackOwnedObject(new GameObject("AmpRefTarget"));
            var holder = TrackOwnedObject(ScriptableObject.CreateInstance<GoRefHolder>());
            var ampRef = RefManager.AssignAny(target);

            var so = new SerializedObject(holder);
            var prop = so.FindProperty("Value");
            ValueParser.SetPropertyValue(prop, ampRef);
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();

            Assert.AreSame(target, so.FindProperty("Value").objectReferenceValue,
                $"SetPropertyValue({ampRef}) must resolve to target via ResolveAny");
        }

        [Test]
        public void HierarchySerializer_Output_ContainsDecimalRef()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("DecimalRefInHierarchy"));

            // Act: use public API which calls SerializeObject internally
            var output = HierarchySerializer.SerializeSubtree(go);

            // Assert: base62 RefManager ref with & prefix (like &1, &a, &Mo)
            Assert.IsTrue(Regex.IsMatch(output, @"&[0-9a-zA-Z]+"),
                $"Hierarchy must contain & base62 RefManager ref (like &1), got: {output}");
            Assert.IsFalse(Regex.IsMatch(output, @"\$[0-9]*[A-F]"),
                $"Hierarchy must NOT contain hex letter refs, got: {output}");
        }

        [Test]
        public void HierarchySerializer_SameGO_SameRef_Idempotent()
        {
            var go = TrackOwnedObject(new GameObject("IdempotentRefTest"));

            var out1 = HierarchySerializer.SerializeSubtree(go);
            var out2 = HierarchySerializer.SerializeSubtree(go);

            var ref1 = Regex.Match(out1, @"&[0-9a-zA-Z]+").Value;
            Assert.IsNotEmpty(ref1, $"No & base62 ref in first output: {out1}");
            var ref2 = Regex.Match(out2, @"&[0-9a-zA-Z]+").Value;
            Assert.AreEqual(ref1, ref2, "Same GO must produce same base62 ref on repeated serialization");
        }

        [Test]
        public void HierarchySerializer_RefFromOutput_ResolvesViaFindObject()
        {
            var go = TrackOwnedObject(new GameObject("FindByDecimalRef"));

            var output = HierarchySerializer.SerializeSubtree(go);
            var match = Regex.Match(output, @"&[0-9a-zA-Z]+");
            Assert.IsTrue(match.Success, $"No & base62 ref in hierarchy output: {output}");

            var found = ComponentSerializer.FindObject(match.Value);
            Assert.AreSame(go, found, $"FindObject({match.Value}) must return original GO");
        }

        [Test]
        public void ValueParser_SetObjectReference_RefManagerRef_Resolves()
        {
            var target = TrackOwnedObject(new GameObject("RefManagerParserTarget"));
            var holder = TrackOwnedObject(ScriptableObject.CreateInstance<GoRefHolder>());
            var refStr = RefManager.Assign(target);

            var so = new SerializedObject(holder);
            var prop = so.FindProperty("Value");
            ValueParser.SetPropertyValue(prop, refStr);
            so.ApplyModifiedPropertiesWithoutUndo();
            so.Update();

            Assert.AreSame(target, so.FindProperty("Value").objectReferenceValue,
                $"SetPropertyValue({refStr}) must resolve to target GO");
        }

        [Test]
        public void FindObject_DollarHexId_Resolves()
        {
            // Arrange
            var go = TrackOwnedObject(new GameObject("FindByHexRef"));
            var hexRef = "$" + ObjectIdCompat.GetRawId(go).ToString("X");

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
            var hexRef = "$" + ObjectIdCompat.GetRawId(target).ToString("X");

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
