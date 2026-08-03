using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.RegionTool;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    public class TransientObjectIdTests : UnityMcpTestBase
    {
        private sealed class ReferenceHolder : ScriptableObject
        {
            public Object Value;
        }

        [Test]
        public void FromObject_UsesUnsignedDecimalWireValue_AndResolvesObject()
        {
            var go = TrackOwnedObject(new GameObject("InstanceIdRoundTrip"));

            var objectId = TransientObjectId.FromObject(go);

            Assert.IsFalse(objectId.IsNone);
            Assert.IsFalse(objectId.WireValue.StartsWith("-"));
            Assert.IsTrue(ulong.TryParse(objectId.WireValue, out _));
            Assert.AreSame(go, objectId.Resolve());
            Assert.AreEqual(unchecked((ulong)(long)go.GetInstanceID()), objectId.RawValue);
        }

        [TestCase("18446744073709551615")]
        [TestCase("9007199254740993")]
        [TestCase("#42")]
        public void TryParse_PreservesUnsignedWirePrecision(string wireValue)
        {
            Assert.IsTrue(TransientObjectId.TryParse(wireValue, out var parsed));
            Assert.AreEqual(wireValue.TrimStart('#'), parsed.WireValue);
        }

        [Test]
        public void TryParse_AcceptsLegacySignedToken()
        {
            Assert.IsTrue(TransientObjectId.TryParse("#-33506", out var parsed));
            Assert.AreEqual(unchecked((ulong)(long)-33506), parsed.RawValue);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("#")]
        [TestCase("12.5")]
        [TestCase("not-an-id")]
        public void TryParse_RejectsInvalidTokens(string token)
        {
            Assert.IsFalse(TransientObjectId.TryParse(token, out _));
        }

        [Test]
        public void HasSerializedReference_DistinguishesNullAndAssignedReference()
        {
            var holder = TrackOwnedObject(ScriptableObject.CreateInstance<ReferenceHolder>());
            var target = TrackOwnedObject(new GameObject("ReferenceTarget"));
            var serialized = new SerializedObject(holder);
            var property = serialized.FindProperty("Value");

            Assert.IsFalse(TransientObjectId.HasSerializedReference(property));

            property.objectReferenceValue = target;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();

            Assert.IsTrue(TransientObjectId.HasSerializedReference(property));
        }

        [Test]
        public void RegionSnapshot_SerializesObjectIdsAsJsonStrings()
        {
            var snapshot = new RegionSnapshot
            {
                ObjectIds = new[] { "9007199254740993" },
                ObjectPaths = new[] { "/Target" }
            };

            var json = JsonUtility.ToJson(snapshot);

            StringAssert.Contains("\"ObjectIds\":[\"9007199254740993\"]", json);
        }
    }
}
