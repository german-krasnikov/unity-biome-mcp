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
    }
}
