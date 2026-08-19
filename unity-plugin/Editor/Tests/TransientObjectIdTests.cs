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
            Assert.AreEqual(ObjectIdCompat.GetRawId(go), objectId.RawValue);
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

        // ── HexRef format (Task 4: unified $HEX ID) ──────────────────────────

        [Test]
        public void TryParse_DollarHex_ReturnsCorrectRawValue()
        {
            // 0x3E8 = 1000 — new canonical $HEX input format
            Assert.IsTrue(TransientObjectId.TryParse("$3E8", out var id));
            Assert.AreEqual(1000UL, id.RawValue);
        }

        [Test]
        public void TryParse_HashDecimal_StillWorks()
        {
            // Backward compat: #decimal must still parse
            Assert.IsTrue(TransientObjectId.TryParse("#1000", out var id));
            Assert.AreEqual(1000UL, id.RawValue);
        }

        [Test]
        public void TryParse_LegacyRef_FallsThrough()
        {
            // $g is a RefManager slot ref, NOT valid hex — must return false
            Assert.IsFalse(TransientObjectId.TryParse("$g", out _));
        }

        [Test]
        public void HexRef_PositiveId_UppercaseNoLeadingZeros()
        {
            // Build via decimal parse (no Unity object needed)
            Assert.IsTrue(TransientObjectId.TryParse("#1000", out var id));
            Assert.AreEqual("$3E8", id.HexRef);
        }

        [Test]
        public void HexRef_NegativeId_PreservesFullBits()
        {
            // -12345 as unsigned 64-bit = 0xFFFFFFFFFFFFCFC7
            var wire = unchecked((ulong)(long)(-12345)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.IsTrue(TransientObjectId.TryParse(wire, out var id));
            Assert.AreEqual("$FFFFFFFFFFFFCFC7", id.HexRef);
        }

        [Test]
        public void HexRef_FromGameObject_StartsWithDollarUppercaseHex()
        {
            var go = TrackOwnedObject(new GameObject("HexRefFormat"));
            var id = TransientObjectId.FromObject(go);
            StringAssert.StartsWith("$", id.HexRef);
            // All chars after $ must be uppercase hex digits
            var hexPart = id.HexRef.Substring(1);
            Assert.IsTrue(hexPart.Length > 0 && hexPart.Length <= 16,
                $"HexRef '{id.HexRef}' must be 1-16 hex chars after $");
            foreach (var c in hexPart)
                Assert.IsTrue((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'),
                    $"Non-hex char '{c}' in '{id.HexRef}'");
        }

        [Test]
        public void HexRef_StaticOverload_MatchesInstanceProperty()
        {
            var go = TrackOwnedObject(new GameObject("HexRefStatic"));
            var id = TransientObjectId.FromObject(go);
            Assert.AreEqual(id.HexRef, TransientObjectId.GetHexRef(go));
        }
    }
}
