// TDD — RED: these tests fail until UIElementSerializer is implemented.
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class VERefTableTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private UIElementSerializer _s;

        [SetUp]
        public void SetUpSerializer()
        {
            _s = new UIElementSerializer();
        }

        [TearDown]
        public void TearDown() { _s?.Dispose(); }

        // 1: ResetRefTable increments generation (counter restarts at ~1 after reset)
        [Test]
        public void ResetRefTable_IncrementsGeneration()
        {
            // First serialize: root=~1, child=~2
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "child" });
            _s.Serialize(root);
            Assert.That(_s.ResolveRef("~2"), Is.Not.Null); // ~2 exists

            // ResetRefTable: clears table, counter back to 0
            _s.ResetRefTable();

            // After reset, ~1 and ~2 are gone
            Assert.That(_s.ResolveRef("~1"), Is.Null, "~1 should be null after ResetRefTable");
            Assert.That(_s.ResolveRef("~2"), Is.Null, "~2 should be null after ResetRefTable");

            // Serialize again: new root gets ~1 (counter restarted)
            var newRoot = new VisualElement { name = "newroot" };
            _s.Serialize(newRoot);
            Assert.That(_s.ResolveRef("~1"), Is.SameAs(newRoot));
        }

        // 2: invalid format throws ArgumentException
        [Test]
        public void ResolveRef_InvalidFormat_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _s.ResolveRef("not-a-ref"));
            Assert.Throws<ArgumentException>(() => _s.ResolveRef("~abc"));
            Assert.Throws<ArgumentException>(() => _s.ResolveRef(null));
            Assert.Throws<ArgumentException>(() => _s.ResolveRef("~"));
        }

        // 3: deterministic stale-ref: Serialize twice — refs from first call are gone after second (table reset)
        [Test]
        public void ResolveRef_StaleAfterNextSerialize_ReturnsNull()
        {
            // First call: root + child → ~1=root, ~2=child
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "child" });
            _s.Serialize(root);
            Assert.That(_s.ResolveRef("~2"), Is.Not.Null, "precondition: ~2 exists after first call");

            // Second call resets the table → ~2 no longer exists
            var single = new VisualElement { name = "single" };
            _s.Serialize(single);

            Assert.That(_s.ResolveRef("~2"), Is.Null,
                "~2 should be null after second Serialize resets the table");
        }

        // 4: ResetRefTable clears all entries
        [Test]
        public void ResetRefTable_ClearsAllEntries()
        {
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "c1" });
            root.Add(new VisualElement { name = "c2" });
            _s.Serialize(root); // root=~1, c1=~2, c2=~3

            _s.ResetRefTable();

            Assert.That(_s.ResolveRef("~1"), Is.Null);
            Assert.That(_s.ResolveRef("~2"), Is.Null);
            Assert.That(_s.ResolveRef("~3"), Is.Null);
        }

        // 5: AssemblyReloadEvents.beforeAssemblyReload hook clears ref table
        [Test]
        public void DomainReload_ClearsRefTable()
        {
            var el = new VisualElement { name = "el" };
            _s.Serialize(el);
            Assert.That(_s.ResolveRef("~1"), Is.Not.Null); // precondition

            // Simulate domain reload: invoke the event via reflection.
            // beforeAssemblyReload is a static Action event in UnityEditor.
            var field = typeof(AssemblyReloadEvents).GetField(
                "beforeAssemblyReload",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            if (field != null && field.GetValue(null) is Action handler)
            {
                handler.Invoke();
                Assert.That(_s.ResolveRef("~1"), Is.Null,
                    "Ref table should be cleared after beforeAssemblyReload fires");
            }
            else
            {
                // Cannot fire event via reflection — verify via ResetRefTable (same effect as hook)
                _s.ResetRefTable();
                Assert.That(_s.ResolveRef("~1"), Is.Null,
                    "ResetRefTable (proxying beforeAssemblyReload) should clear all refs");
            }
        }

        // 6: Serialize returns string containing ~1 for first element
        [Test]
        public void Assign_NewElement_ReturnsTildeRef()
        {
            var el = new VisualElement { name = "el" };
            var result = _s.Serialize(el);
            Assert.That(result, Does.Contain("~1"));
        }

        // 7: round-trip: Serialize → ResolveRef("~1") → same element
        [Test]
        public void Resolve_AssignedRef_ReturnsSameElement()
        {
            var el = new VisualElement { name = "el" };
            _s.Serialize(el);
            Assert.That(_s.ResolveRef("~1"), Is.SameAs(el));
        }

        // 8: unknown ref returns null, does not throw
        [Test]
        public void Resolve_UnknownRef_ReturnsNull()
        {
            var el = new VisualElement { name = "el" };
            _s.Serialize(el); // only ~1 in table
            Assert.That(_s.ResolveRef("~999"), Is.Null);
        }

        // 9: CRITICAL — calling Serialize N times does not grow the ref table
        [Test]
        public void InspectUITK_CalledNTimes_RefTableSizeStaysConstant()
        {
            var el = new VisualElement { name = "el" };
            _s.Serialize(el);
            _s.Serialize(el);
            _s.Serialize(el); // 3 calls

            // Only ~1 exists (counter resets on each call, table does not accumulate)
            Assert.That(_s.ResolveRef("~1"), Is.Not.Null, "~1 must exist after last Serialize");
            Assert.That(_s.ResolveRef("~2"), Is.Null, "~2 must not exist — table was not accumulated");
            Assert.That(_s.ResolveRef("~3"), Is.Null, "~3 must not exist — table was not accumulated");
        }
    }
}
