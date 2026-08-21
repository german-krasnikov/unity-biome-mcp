using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Testing;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Tests
{
    public class ObjectIdCompatTests : UnityMcpTestBase
    {
        [Test]
        public void GetRawId_IsNonZeroForLiveObject()
        {
            var go = TrackOwnedObject(new GameObject("CompatTest"));
            Assert.That(ObjectIdCompat.GetRawId(go), Is.Not.EqualTo(0UL));
        }

        [Test]
        public void RoundTrip_ResolveObject_ReturnsSameObject()
        {
            var so = TrackOwnedObject(ScriptableObject.CreateInstance<ScriptableObject>());
            var raw = ObjectIdCompat.GetRawId(so);
            Assert.That(ObjectIdCompat.ResolveObject(raw), Is.SameAs(so));
        }

        [Test]
        public void HasSerializedReference_DoesNotThrow_OnStringProperty()
        {
            var so = TrackOwnedObject(ScriptableObject.CreateInstance<ScriptableObject>());
            var sp = new SerializedObject(so);
            var prop = sp.FindProperty("m_Name");
            // HasSerializedReference guards propertyType first on all Unity versions,
            // so no Error is emitted even on pre-6000.4 builds.
            var result = ObjectIdCompat.HasSerializedReference(prop);
            Assert.That(result, Is.False, "string property should not be an object reference");
        }

        [Test]
        public void GetRawId_ReturnsZero_ForNull()
        {
            Assert.That(ObjectIdCompat.GetRawId(null), Is.EqualTo(0UL));
        }

        [Test]
        public void ResolveObject_ReturnsNull_ForZero()
        {
            Assert.That(ObjectIdCompat.ResolveObject(0UL), Is.Null);
        }

        [Test]
        public void GetRawId_FitsIn32BitRange()
        {
            // Pre-6000.4: GetRawId must use (uint) cast, NOT (ulong)(long).
            // Sign-extension would produce values > uint.MaxValue for negative instance IDs.
            var go = TrackOwnedObject(new GameObject("32BitBoundCheck"));
            var raw = ObjectIdCompat.GetRawId(go);
            Assert.That(raw, Is.LessThanOrEqualTo((ulong)uint.MaxValue),
                $"GetRawId must not sign-extend negative instance IDs — got 0x{raw:X}");
        }

        [Test]
        public void GetRawId_HexRef_Max8Chars()
        {
            // HexRef = "$" + RawValue.ToString("X") — with (uint) cast, max 8 hex chars.
            var go = TrackOwnedObject(new GameObject("HexRefMaxLen"));
            var hexRef = TransientObjectId.GetHexRef(go);
            var hexPart = hexRef.Substring(1); // strip $
            Assert.That(hexPart.Length, Is.LessThanOrEqualTo(8),
                $"HexRef hex part must be ≤ 8 chars on pre-6000.4 — got '{hexRef}'");
        }
    }
}
