// TDD: GetMemberNames + GetZeroArgMethodNames — reflection-only, no AssetDatabase.
using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestDropHelperMemberTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Test types — nested plain C# classes (no scene needed) ───────────

        private class TestWithFields
        {
            public float Speed;
            public int Count;
            private float _private;
        }

        private class TestWithProperties
        {
            public int Count { get; set; }
            public string Name { get; }
            public string this[int i] => "";
        }

        private class TestWithMethods
        {
            public void Fire() { }
            public void Move(Vector3 v) { }
            public int GetScore() => 0;
            private void PrivateMethod() { }
        }

        // ── GetMemberNames: fields ────────────────────────────────────────────

        [Test]
        public void GetMemberNames_ReturnsPublicFields()
        {
            var result = PlaytestDropHelper.GetMemberNames(typeof(TestWithFields));
            CollectionAssert.Contains(result, "Speed");
            CollectionAssert.Contains(result, "Count");
        }

        [Test]
        public void GetMemberNames_ExcludesPrivateFields()
        {
            var result = PlaytestDropHelper.GetMemberNames(typeof(TestWithFields));
            CollectionAssert.DoesNotContain(result, "_private");
        }

        // ── GetMemberNames: properties ────────────────────────────────────────

        [Test]
        public void GetMemberNames_ReturnsPublicProperties()
        {
            var result = PlaytestDropHelper.GetMemberNames(typeof(TestWithProperties));
            CollectionAssert.Contains(result, "Count");
            CollectionAssert.Contains(result, "Name");
        }

        [Test]
        public void GetMemberNames_ExcludesIndexedProperties()
        {
            // Indexer reflected as "Item" — must be absent
            var result = PlaytestDropHelper.GetMemberNames(typeof(TestWithProperties));
            CollectionAssert.DoesNotContain(result, "Item");
        }

        [Test]
        public void GetMemberNames_ExcludesBaseTypeMembers()
        {
            // MonoBehaviour is in _baseTypes — 'enabled' and 'tag' are declared in Behaviour/Component
            var result = PlaytestDropHelper.GetMemberNames(typeof(MonoBehaviour));
            CollectionAssert.DoesNotContain(result, "enabled");
            CollectionAssert.DoesNotContain(result, "tag");
        }

        [Test]
        public void GetMemberNames_NullType_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => PlaytestDropHelper.GetMemberNames(null));
        }

        // ── GetZeroArgMethodNames ─────────────────────────────────────────────

        [Test]
        public void GetZeroArgMethodNames_ReturnsZeroArgPublic()
        {
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(TestWithMethods));
            CollectionAssert.Contains(result, "Fire");
            CollectionAssert.Contains(result, "GetScore");
        }

        [Test]
        public void GetZeroArgMethodNames_ExcludesParameterized()
        {
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(TestWithMethods));
            CollectionAssert.DoesNotContain(result, "Move");
        }

        [Test]
        public void GetZeroArgMethodNames_ExcludesPrivate()
        {
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(TestWithMethods));
            CollectionAssert.DoesNotContain(result, "PrivateMethod");
        }

        [Test]
        public void GetZeroArgMethodNames_ExcludesBaseTypeMethods()
        {
            // GetHashCode and ToString are declared in object (in _baseTypes)
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(TestWithMethods));
            CollectionAssert.DoesNotContain(result, "GetHashCode");
            CollectionAssert.DoesNotContain(result, "ToString");
        }

        [Test]
        public void GetZeroArgMethodNames_ExcludesSpecialNames()
        {
            // Property getters (get_Count, get_Name) are special names
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(TestWithProperties));
            foreach (var name in result)
                StringAssert.DoesNotStartWith("get_", name);
        }

        [Test]
        public void GetZeroArgMethodNames_EmptyForBaseMonoBehaviour()
        {
            // MonoBehaviour itself is in _baseTypes — all its methods excluded
            var result = PlaytestDropHelper.GetZeroArgMethodNames(typeof(MonoBehaviour));
            Assert.IsEmpty(result);
        }
    }
}
