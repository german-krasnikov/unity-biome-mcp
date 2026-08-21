// TDD: RuntimeHelper.SetRuntimeProperty, ReadField (IList + null chain), TryResolveVirtualField.
// EditMode only — no TCP, no Play Mode required.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RuntimeHelperPropertyAndFieldTests : SceneTestBase
    {
        // ─── Test components ──────────────────────────────────────────────────

        private class SetPropBehaviour : MonoBehaviour
        {
            public string PropValue { get; set; } = "initial";
            public int IntField = 0;
        }

        private class ReadFieldBehaviour : MonoBehaviour
        {
            public List<int> Numbers = new List<int>();
            public InnerData Nested;

            public class InnerData
            {
                public int Value = 42;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Task 1: SetRuntimeProperty (6 tests)
        // ────────────────────────────────────────────────────────────────────

        [Test]
        public void SetRuntimeProperty_PublicProperty_SetsValueAndReturnsConfirmation()
        {
            var go = TrackOwnedObject(new GameObject("RHSetProp_Prop"));
            var comp = go.AddComponent<SetPropBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            var result = RuntimeHelper.SetRuntimeProperty(path, "SetPropBehaviour", "PropValue", "world");

            Assert.AreEqual("PropValue=world", result);
            Assert.AreEqual("world", comp.PropValue);
        }

        [Test]
        public void SetRuntimeProperty_PublicField_SetsValueAndReturnsConfirmation()
        {
            var go = TrackOwnedObject(new GameObject("RHSetProp_Field"));
            var comp = go.AddComponent<SetPropBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            var result = RuntimeHelper.SetRuntimeProperty(path, "SetPropBehaviour", "IntField", "99");

            Assert.AreEqual("IntField=99", result);
            Assert.AreEqual(99, comp.IntField);
        }

        [Test]
        public void SetRuntimeProperty_NonExistentMember_ThrowsArgumentException()
        {
            var go = TrackOwnedObject(new GameObject("RHSetProp_Missing"));
            go.AddComponent<SetPropBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.SetRuntimeProperty(path, "SetPropBehaviour", "NoSuchField", "value"));
            Assert.That(ex.Message, Does.Contain("not found"));
        }

        [Test]
        public void SetRuntimeProperty_InvalidPath_ThrowsObjectNotFound()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.SetRuntimeProperty("/NoSuchObject_999", "SetPropBehaviour", "PropValue", "v"));
            Assert.That(ex.Message.ToLowerInvariant(), Does.Contain("not found"));
        }

        [Test]
        public void SetRuntimeProperty_InvalidComponentType_ThrowsComponentNotFound()
        {
            var go = TrackOwnedObject(new GameObject("RHSetProp_BadComp"));
            go.AddComponent<SetPropBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.SetRuntimeProperty(path, "NonExistentComponent9999", "PropValue", "v"));
            Assert.That(ex.Message.ToLowerInvariant(), Does.Contain("not found"));
        }

        [Test]
        public void SetRuntimeProperty_TypeMismatch_ThrowsConversionError()
        {
            var go = TrackOwnedObject(new GameObject("RHSetProp_Type"));
            go.AddComponent<SetPropBehaviour>();
            var path = ComponentSerializer.GetPath(go);

            // "not_an_int" cannot be converted to int — ConvertValue throws ArgumentException
            Assert.Throws<ArgumentException>(
                () => RuntimeHelper.SetRuntimeProperty(path, "SetPropBehaviour", "IntField", "not_an_int"));
        }

        // ────────────────────────────────────────────────────────────────────
        // Task 2: ReadField — IList and null chain (4 tests)
        // ────────────────────────────────────────────────────────────────────

        [Test]
        public void ReadField_ListThreeItems_GetItemOneReturnsCorrectValue()
        {
            var go = TrackOwnedObject(new GameObject("RHRead_List3"));
            var comp = go.AddComponent<ReadFieldBehaviour>();
            comp.Numbers = new List<int> { 10, 20, 30 };

            // get_Item(1) accesses index 1 via method call syntax in ReadField
            var result = RuntimeHelper.ReadFieldInternal(comp, "Numbers.get_Item(1)");

            Assert.AreEqual("20", result);
        }

        [Test]
        public void ReadField_ListTwelveItems_ShowsTruncationAndDoesNotThrow()
        {
            var go = TrackOwnedObject(new GameObject("RHRead_List12"));
            var comp = go.AddComponent<ReadFieldBehaviour>();
            comp.Numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            var result = RuntimeHelper.ReadFieldInternal(comp, "Numbers");

            // First 10 items shown; remaining 2 as ...+2
            Assert.That(result, Does.Contain("...+2"), "Truncation marker missing for 12-item list");
            Assert.That(result, Does.StartWith("[1,").Or.StartWith("[1, "));
        }

        [Test]
        public void ReadField_NullNestedObject_ThrowsWithNullAtMessage()
        {
            var go = TrackOwnedObject(new GameObject("RHRead_Null"));
            var comp = go.AddComponent<ReadFieldBehaviour>();
            comp.Nested = null; // explicit — reference type default is already null

            var ex = Assert.Throws<ArgumentException>(
                () => RuntimeHelper.ReadFieldInternal(comp, "Nested.Value"));
            Assert.That(ex.Message, Does.Contain("Null at"));
        }

        [Test]
        public void ReadField_ListOutOfRangeIndex_ThrowsTargetInvocationException()
        {
            var go = TrackOwnedObject(new GameObject("RHRead_OOB"));
            var comp = go.AddComponent<ReadFieldBehaviour>();
            comp.Numbers = new List<int> { 1, 2, 3 }; // 3 items, valid indices 0-2

            // get_Item(10) on 3-item list → ArgumentOutOfRangeException wrapped in TargetInvocationException
            Assert.Throws<System.Reflection.TargetInvocationException>(
                () => RuntimeHelper.ReadFieldInternal(comp, "Numbers.get_Item(10)"));
        }

        // ────────────────────────────────────────────────────────────────────
        // Task 3: TryResolveVirtualField — Rigidbody speed (3 tests)
        // ────────────────────────────────────────────────────────────────────

        [Test]
        public void TryResolveVirtualField_RigidbodySpeed_ReturnsFloatString()
        {
            var go = TrackOwnedObject(new GameObject("RHVirtual_RB"));
            var rb = go.AddComponent<Rigidbody>();

            var result = RuntimeHelper.TryResolveVirtualField(rb, "speed");

            Assert.IsNotNull(result);
            Assert.IsTrue(
                float.TryParse(result, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _),
                $"Expected parseable float, got: '{result}'");
        }

        [Test]
        public void TryResolveVirtualField_Rigidbody2DSpeed_ReturnsFloatString()
        {
            var go = TrackOwnedObject(new GameObject("RHVirtual_RB2D"));
            var rb2d = go.AddComponent<Rigidbody2D>();

            var result = RuntimeHelper.TryResolveVirtualField(rb2d, "speed");

            Assert.IsNotNull(result);
            Assert.IsTrue(
                float.TryParse(result, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _),
                $"Expected parseable float, got: '{result}'");
        }

        [Test]
        public void TryResolveVirtualField_NonVirtualFieldOnRigidbody_ReturnsNull()
        {
            var go = TrackOwnedObject(new GameObject("RHVirtual_Mass"));
            var rb = go.AddComponent<Rigidbody>();

            // "mass" is a real Rigidbody property, not a virtual field — returns null
            var result = RuntimeHelper.TryResolveVirtualField(rb, "mass");

            Assert.IsNull(result);
        }
    }
}
