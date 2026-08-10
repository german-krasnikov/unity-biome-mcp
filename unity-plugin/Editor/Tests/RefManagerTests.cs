// NUnit tests for RefManager — CS2.test.1 + CS2.arch.1 (wrap-around bug regression).
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

        // ── GenerateRef ───────────────────────────────────────────────────────

        [Test]
        public void GenerateRef_Zero_ReturnsA()
        {
            Assert.AreEqual("$a", RefManager.GenerateRef(0));
        }

        [Test]
        public void GenerateRef_25_ReturnsZ()
        {
            Assert.AreEqual("$z", RefManager.GenerateRef(25));
        }

        [Test]
        public void GenerateRef_Slot26_ReturnsTwoChars()
        {
            Assert.AreEqual("$aa", RefManager.GenerateRef(26));
        }

        [Test]
        public void GenerateRef_Wraps702_ReturnsA()
        {
            Assert.AreEqual("$a", RefManager.GenerateRef(702));
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

        // ── IsRef ─────────────────────────────────────────────────────────────

        [Test]
        public void IsRef_ValidShortRef_ReturnsTrue()
        {
            Assert.IsTrue(RefManager.IsRef("$a"));
            Assert.IsTrue(RefManager.IsRef("$zz"));
        }

        [Test]
        public void IsRef_TooLong_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef("$abc")); // length 4
        }

        [Test]
        public void IsRef_NoPrefix_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef("abc"));
        }

        [Test]
        public void IsRef_Null_ReturnsFalse()
        {
            Assert.IsFalse(RefManager.IsRef(null));
        }

        [Test]
        public void IsRef_UppercaseLetter_ReturnsFalse()
        {
            // $A looks like a 1-char ref but uppercase — must not match RefManager slots
            Assert.IsFalse(RefManager.IsRef("$A"));
            Assert.IsFalse(RefManager.IsRef("$AB"));
        }

        [Test]
        public void IsRef_Digits_ReturnsFalse()
        {
            // Digits are not valid RefManager slot chars
            Assert.IsFalse(RefManager.IsRef("$3"));
            Assert.IsFalse(RefManager.IsRef("$12"));
        }

        [Test]
        public void IsRef_UppercaseHex_ReturnsFalse()
        {
            // $3E8 is a $HEX object ID, not a RefManager slot
            Assert.IsFalse(RefManager.IsRef("$3E8"));
        }

        // ── Wrap-around regression (CS2.arch.1) ───────────────────────────────

        /// <summary>
        /// Fill all 702 slots (gos[0..701]), then assign a 703rd distinct GO.
        /// GenerateRef(702) wraps to "$a", evicting gos[0]'s _idToRef entry.
        /// OLD code: left _idToRef[gos[0].id] = "$a" stale → Assign(gos[0]) returns "$a"
        ///           (which now resolves to gos[702]), a silent identity collision.
        /// NEW code: evicts gos[0] during overwrite → Assign(gos[0]) allocates a fresh slot.
        /// </summary>
        [Test]
        public void WrapAround_OldGO_Evicted_NewGO_Resolves()
        {
            // 702 GOs fill every slot ($a..$zz); gos[703] is the wrap-trigger.
            var gos = new GameObject[704];
            for (int i = 0; i < 704; i++)
                gos[i] = new GameObject($"Ref_Wrap_{i}");
            try
            {
                // Fill all 702 slots.
                for (int i = 0; i < 702; i++)
                    RefManager.Assign(gos[i]);

                // gos[702] wraps to slot 0 → overwrites "$a" which belonged to gos[0].
                var refFor702 = RefManager.Assign(gos[702]);
                Assert.AreEqual("$a", refFor702, "First wrap must land on slot 0 = $a");

                // NEW: gos[0]'s _idToRef entry was evicted, so Assign(gos[0]) must NOT
                // return "$a" (that slot now belongs to gos[702]).
                // OLD: stale _idToRef still maps gos[0].id → "$a", so Assign returns "$a"
                //      silently making two GOs share one ref — this assertion goes red.
                var refForGos0Again = RefManager.Assign(gos[0]);
                Assert.AreNotEqual("$a", refForGos0Again,
                    "gos[0]'s old ref '$a' was stolen by gos[702]; Assign must give gos[0] a new ref");

                // Sanity: $a must still resolve to gos[702], not gos[0].
                Assert.AreEqual(gos[702], RefManager.Resolve("$a"),
                    "Resolve($a) must return gos[702] after wrap");
            }
            finally
            {
                foreach (var g in gos)
                    if (g != null) Object.DestroyImmediate(g);
            }
        }
    }
}
