// NUnit tests for RefManager — prefix migration $ → &, base62 encoding, no wrap-around.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class RefManagerTests : SceneTestBase
    {
        [SetUp]
        public void SetUp() => RefManager.Invalidate();

        [TearDown]
        public void TearDown() => RefManager.Invalidate();

        // ── Assign ────────────────────────────────────────────────────────────

        [Test]
        public void Assign_SameObject_ReturnsSameRef()
        {
            var go = new GameObject("Ref_A");
            try
            {
                var r1 = RefManager.Assign(go);
                var r2 = RefManager.Assign(go);
                Assert.AreEqual(r1, r2);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Assign_TwoObjects_ReturnsDifferentRefs()
        {
            var go1 = new GameObject("Ref_B1");
            var go2 = new GameObject("Ref_B2");
            try
            {
                var r1 = RefManager.Assign(go1);
                var r2 = RefManager.Assign(go2);
                Assert.AreNotEqual(r1, r2);
            }
            finally
            {
                Object.DestroyImmediate(go1);
                Object.DestroyImmediate(go2);
            }
        }

        // ── Resolve ───────────────────────────────────────────────────────────

        [Test]
        public void Resolve_AssignedRef_ReturnsGO()
        {
            var go = new GameObject("Ref_C");
            try
            {
                var r = RefManager.Assign(go);
                Assert.AreEqual(go, RefManager.Resolve(r));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Resolve_UnknownRef_ReturnsNull()
        {
            Assert.IsNull(RefManager.Resolve("$zzz_unknown"));
        }

        [Test]
        public void Resolve_StalRef_AfterDestroy_ReturnsNull()
        {
            var go = new GameObject("Ref_Stale");
            var r = RefManager.Assign(go);
            Object.DestroyImmediate(go);
            Assert.IsNull(RefManager.Resolve(r));
        }

        // ── GenerateRef — base62, no wrap-around ──────────────────────────────

        [Test]
        public void GenerateRef_Zero_ReturnsAmpersand1()
        {
            Assert.AreEqual("&1", RefManager.GenerateRef(0));
        }

        [Test]
        public void GenerateRef_9_ReturnsAmpersandLowercaseA()
        {
            // n=9 → val=10 → base62[10]='a'
            Assert.AreEqual("&a", RefManager.GenerateRef(9));
        }

        [Test]
        public void GenerateRef_60_ReturnsAmpersandZ()
        {
            // n=60 → val=61 → base62[61]='Z' — last single-char ref
            Assert.AreEqual("&Z", RefManager.GenerateRef(60));
        }

        [Test]
        public void GenerateRef_61_ReturnsTwoCharRef()
        {
            // n=61 → val=62 = 1*62+0 → "10" in base62 — first two-char ref
            Assert.AreEqual("&10", RefManager.GenerateRef(61));
        }

        [Test]
        public void GenerateRef_3843_ReturnsThreeCharRef()
        {
            // n=3843 → val=3844 = 62^2 → "100" in base62 — first three-char ref
            Assert.AreEqual("&100", RefManager.GenerateRef(3843));
        }

        [Test]
        public void GenerateRef_NoWrapAround_LargeN()
        {
            // Counter grows freely — no wrap-around, each n gives unique output
            var r9999 = RefManager.GenerateRef(9999);
            Assert.IsTrue(r9999.StartsWith("&"), "Ref must start with &");
            Assert.AreNotEqual("&1", r9999, "Large n must not wrap to first slot");
        }

        // ── Prune ─────────────────────────────────────────────────────────────

        [Test]
        public void Prune_RemovesDestroyedGO_ResolveReturnsNull()
        {
            var go = new GameObject("Ref_Prune");
            var r = RefManager.Assign(go);
            Object.DestroyImmediate(go);
            RefManager.Prune();
            Assert.IsNull(RefManager.Resolve(r));
        }

        [Test]
        public void Prune_KeepsLiveGO()
        {
            var go = new GameObject("Ref_Live");
            try
            {
                var r = RefManager.Assign(go);
                RefManager.Prune();
                Assert.AreEqual(go, RefManager.Resolve(r));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── IsRef — & accepts alphanumeric (base62); $ is never a ref ──────────

        [Test]
        public void IsRef_AmpersandDecimalRef_ReturnsTrue()
        {
            Assert.IsTrue(RefManager.IsRef("&1"));
            Assert.IsTrue(RefManager.IsRef("&10"));
        }

        [Test]
        public void IsRef_AmpersandBase62Lowercase_ReturnsTrue()
        {
            Assert.IsTrue(RefManager.IsRef("&a"));
            Assert.IsTrue(RefManager.IsRef("&abc"));
        }

        [Test]
        public void IsRef_AmpersandBase62Mixed_ReturnsTrue()
        {
            Assert.IsTrue(RefManager.IsRef("&Mo"));
            Assert.IsTrue(RefManager.IsRef("&Z"));
        }

        [Test]
        public void IsRef_DollarDigits_ReturnsFalse()
        {
            // $digits is a hex instance ID (e.g. $1=1, $400=1024) — never a ref
            Assert.IsFalse(RefManager.IsRef("$1"));
            Assert.IsFalse(RefManager.IsRef("$9999"));
            Assert.IsFalse(RefManager.IsRef("$400"));
            Assert.IsFalse(RefManager.IsRef("$2710"));
        }

        [Test]
        public void IsRef_DollarAlpha_ReturnsFalse()
        {
            // $abc is an alias / hex ID, not a RefManager ref
            Assert.IsFalse(RefManager.IsRef("$abc"));
            Assert.IsFalse(RefManager.IsRef("$a"));
        }

        [Test]
        public void IsRef_AmpersandPunctuation_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef("&!@"));
            Assert.IsFalse(RefManager.IsRef("&1_2"));
        }

        [Test]
        public void IsRef_NoPrefix_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef("abc"));
            Assert.IsFalse(RefManager.IsRef("1"));
        }

        [Test]
        public void IsRef_Null_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef(null));
        }

        [Test]
        public void IsRef_TooShort_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef("&"));
            Assert.IsFalse(RefManager.IsRef("$"));
        }

        [Test]
        public void IsRef_HexTransientId_False()
        {
            // $ + non-digit → not a decimal ref, so TransientObjectId hex path used
            Assert.IsFalse(RefManager.IsRef("$3E8"));
            Assert.IsFalse(RefManager.IsRef("$2B678"));
        }

        // ── AssignAny / ResolveAny — universal UnityEngine.Object support ──────

        [Test]
        public void AssignAny_Component_ReturnsRef()
        {
            var go = new GameObject("AnyComp_A");
            try
            {
                var comp = go.AddComponent<BoxCollider>();
                var r = RefManager.AssignAny(comp);
                Assert.IsTrue(r.StartsWith("&"), "ref must start with &");
                Assert.AreEqual(comp, RefManager.ResolveAny(r));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AssignAny_SameObject_ReturnsSameRef()
        {
            var go = new GameObject("AnyComp_B");
            try
            {
                var comp = go.AddComponent<BoxCollider>();
                var r1 = RefManager.AssignAny(comp);
                var r2 = RefManager.AssignAny(comp);
                Assert.AreEqual(r1, r2);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ResolveAny_Stale_ReturnsNull()
        {
            var go = new GameObject("AnyComp_C");
            var comp = go.AddComponent<BoxCollider>();
            var r = RefManager.AssignAny(comp);
            Object.DestroyImmediate(go);
            Assert.IsNull(RefManager.ResolveAny(r));
        }

        [Test]
        public void Assign_GameObject_StillWorks_BackwardCompat()
        {
            var go = new GameObject("AnyComp_D");
            try
            {
                var r = RefManager.Assign(go);
                Assert.AreEqual(go, RefManager.Resolve(r));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Invalidate_ClearsAnyRefs()
        {
            var go = new GameObject("AnyComp_E");
            try
            {
                var comp = go.AddComponent<BoxCollider>();
                var r = RefManager.AssignAny(comp);
                RefManager.Invalidate();
                Assert.IsNull(RefManager.ResolveAny(r));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // === Base62 Codec ===

        [Test]
        public void Base62Encode_Zero_Returns0()
        {
            Assert.AreEqual("0", RefManager.Base62Encode(0));
        }

        [Test]
        public void Base62Encode_FortyTwo_ReturnsG()
        {
            // 42 in base62: 42/62=0 r42 → index 42 = 'G'
            Assert.AreEqual("G", RefManager.Base62Encode(42));
        }

        [Test]
        public void Base62Encode_MaxUint_Max6Chars()
        {
            var encoded = RefManager.Base62Encode(uint.MaxValue);
            Assert.LessOrEqual(encoded.Length, 6);
        }

        [TestCase(0UL)]
        [TestCase(1UL)]
        [TestCase(42UL)]
        [TestCase(1000UL)]
        [TestCase(4294967295UL)] // uint.MaxValue
        [TestCase(18446744073709551615UL)] // ulong.MaxValue
        public void Base62_RoundTrip(ulong value)
        {
            var encoded = RefManager.Base62Encode(value);
            Assert.IsTrue(RefManager.TryBase62Decode(encoded, out var decoded));
            Assert.AreEqual(value, decoded);
        }

        [Test]
        public void TryBase62Decode_InvalidChars_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.TryBase62Decode("!invalid!", out _));
        }

        // === Ref / ResolveRef ===

        [Test]
        public void Ref_GameObject_StartsWithAmpersand()
        {
            var go = TrackOwnedObject(new GameObject("RefTest"));
            var r = RefManager.Ref(go);
            Assert.IsTrue(r.StartsWith("&"));
        }

        [Test]
        public void Ref_SameObject_Twice_SameResult()
        {
            var go = TrackOwnedObject(new GameObject("Stable"));
            Assert.AreEqual(RefManager.Ref(go), RefManager.Ref(go));
        }

        [Test]
        public void Ref_TwoObjects_DifferentResults()
        {
            var a = TrackOwnedObject(new GameObject("A"));
            var b = TrackOwnedObject(new GameObject("B"));
            Assert.AreNotEqual(RefManager.Ref(a), RefManager.Ref(b));
        }

        [Test]
        public void ResolveRef_RoundTrip_SameObject()
        {
            var go = TrackOwnedObject(new GameObject("RoundTrip"));
            var r = RefManager.Ref(go);
            Assert.AreSame(go, RefManager.ResolveRef(r));
        }

        [Test]
        public void ResolveRef_DestroyedObject_ReturnsNull()
        {
            var go = new GameObject("Doomed");
            var r = RefManager.Ref(go);
            Object.DestroyImmediate(go);
            Assert.IsNull(RefManager.ResolveRef(r));
        }

        [Test]
        public void Ref_Null_ReturnsNull()
        {
            Assert.IsNull(RefManager.Ref(null));
        }

        [Test]
        public void ResolveRef_Invalid_ReturnsNull()
        {
            Assert.IsNull(RefManager.ResolveRef(null));
            Assert.IsNull(RefManager.ResolveRef(""));
            Assert.IsNull(RefManager.ResolveRef("&!bad!"));
        }

        [Test]
        public void Ref_AfterInvalidate_ReturnsSameRef()
        {
            var go = TrackOwnedObject(new GameObject("StableAcrossInvalidate"));
            var r1 = RefManager.Ref(go);
            RefManager.Invalidate();
            var r2 = RefManager.Ref(go);
            Assert.AreEqual(r1, r2, "Ref must be stable across Invalidate — derived from instanceID, not a counter");
        }
    }
}
