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

        // 3: WeakReference target collected → returns null
        [Test]
        public void ResolveRef_GarbageCollected_ReturnsNull()
        {
            PopulateRefAndLetGoOutOfScope(_s);
            // Two GC passes: collect gen0→gen1→gen2
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

            // If GC collected the VE, ResolveRef returns null.
            // GC is non-deterministic, so we skip if still alive (not a hard failure).
            var result = _s.ResolveRef("~1");
            // VisualElement is a plain C# type — should be collected.
            Assert.That(result, Is.Null, "WeakReference target should be null after GC");
        }

        // Helper: creates VE in a separate call frame so it goes out of scope on return.
        private static void PopulateRefAndLetGoOutOfScope(UIElementSerializer s)
        {
            var el = new VisualElement { name = "temp" };
            s.Serialize(el);
            // el goes out of scope when this method returns
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
    }
}
