// TDD — Phase 1.1a: ToolCardRendererRegistry unit tests.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolCardRendererRegistryTests : UnityMcpTestBase
    {
        private class FakeRenderer : IToolCardRenderer
        {
            public int OnStartCount;
            public int OnUpdateCount;
            public void OnStart(VisualElement chip, ToolCallRecord rec) => OnStartCount++;
            public void OnUpdate(VisualElement chip, ToolCallRecord rec) => OnUpdateCount++;
        }

        [SetUp]
        public void ResetRegistry() => ToolCardRendererRegistry.ResetForTests();

        [Test]
        public void Register_ValidName_RendererStored()
        {
            var r = new FakeRenderer();
            ToolCardRendererRegistry.Register("Bash", r);
            Assert.AreSame(r, ToolCardRendererRegistry.Resolve("Bash"));
        }

        [Test]
        public void Register_DuplicateName_KeepsFirst_ReturnsFalse()
        {
            var r1 = new FakeRenderer();
            var r2 = new FakeRenderer();
            ToolCardRendererRegistry.Register("Edit", r1);
            var result = ToolCardRendererRegistry.Register("Edit", r2);
            Assert.IsFalse(result);
            Assert.AreSame(r1, ToolCardRendererRegistry.Resolve("Edit"));
        }

        [Test]
        public void Register_NullRenderer_ReturnsFalse()
            => Assert.IsFalse(ToolCardRendererRegistry.Register("Bash", null));

        [Test]
        public void Register_NullName_ReturnsFalse()
            => Assert.IsFalse(ToolCardRendererRegistry.Register(null, new FakeRenderer()));

        [Test]
        public void Register_EmptyName_ReturnsFalse()
            => Assert.IsFalse(ToolCardRendererRegistry.Register("", new FakeRenderer()));

        [Test]
        public void Unregister_ExistingName_Removed_ReturnsTrue()
        {
            ToolCardRendererRegistry.Register("Write", new FakeRenderer());
            Assert.IsTrue(ToolCardRendererRegistry.Unregister("Write"));
            Assert.IsNull(ToolCardRendererRegistry.Resolve("Write"));
        }

        [Test]
        public void Unregister_UnknownName_ReturnsFalse()
            => Assert.IsFalse(ToolCardRendererRegistry.Unregister("nope"));

        [Test]
        public void Resolve_UnknownName_ReturnsNull()
            => Assert.IsNull(ToolCardRendererRegistry.Resolve("nope"));

        [Test]
        public void Resolve_NullName_ReturnsNull()
            => Assert.IsNull(ToolCardRendererRegistry.Resolve(null));

        [Test]
        public void Version_IncreasesOnRegister()
        {
            int v = ToolCardRendererRegistry.Version;
            ToolCardRendererRegistry.Register("Grep", new FakeRenderer());
            Assert.Greater(ToolCardRendererRegistry.Version, v);
        }

        [Test]
        public void Version_IncreasesOnUnregister()
        {
            ToolCardRendererRegistry.Register("LS", new FakeRenderer());
            int v = ToolCardRendererRegistry.Version;
            ToolCardRendererRegistry.Unregister("LS");
            Assert.Greater(ToolCardRendererRegistry.Version, v);
        }

        [Test]
        public void PreserveStateForTests_RestoresAfterDispose()
        {
            var r1 = new FakeRenderer();
            ToolCardRendererRegistry.Register("Glob", r1);
            using (ToolCardRendererRegistry.PreserveStateForTests())
            {
                ToolCardRendererRegistry.Register("Glob", new FakeRenderer()); // ignored (keep-first)
                ToolCardRendererRegistry.Register("NewTool", new FakeRenderer());
            }
            Assert.AreSame(r1, ToolCardRendererRegistry.Resolve("Glob"));
            Assert.IsNull(ToolCardRendererRegistry.Resolve("NewTool"));
        }

        [Test]
        public void ResetForTests_ClearsAll()
        {
            ToolCardRendererRegistry.Register("Bash2", new FakeRenderer());
            int vBefore = ToolCardRendererRegistry.Version;
            ToolCardRendererRegistry.ResetForTests();
            Assert.IsNull(ToolCardRendererRegistry.Resolve("Bash2"));
            Assert.Greater(ToolCardRendererRegistry.Version, vBefore);
        }

        // Phase 4 barrier: every IToolCardRenderer.OnUpdate must be idempotent.
        // After Phase 2.9, OnUpdate is called twice (ArgsComplete then Result).
        // A renderer without a guard doubles its children on the second call.
        //
        // RED proof: NaiveFakeRenderer (no guard) → childCount=2, test FAILS.
        // GREEN:     IdempotentFakeRenderer (Q guard) → childCount=1, test PASSES.
        private class NaiveFakeRenderer : IToolCardRenderer
        {
            public void OnStart(VisualElement chip, ToolCallRecord rec) { }
            public void OnUpdate(VisualElement chip, ToolCallRecord rec)
                => chip.Add(new Label("detail")); // no guard — doubles on second call
        }

        private class IdempotentFakeRenderer : IToolCardRenderer
        {
            public void OnStart(VisualElement chip, ToolCallRecord rec) { }
            public void OnUpdate(VisualElement chip, ToolCallRecord rec)
            {
                if (chip.Q("detail-label") != null) return; // idempotency guard
                var lbl = new Label(rec.ArgsJson ?? ""); lbl.name = "detail-label";
                chip.Add(lbl);
            }
        }

        [Test]
        public void OnUpdate_CalledTwice_IdempotentRendererDoesNotDoubleChildren()
        {
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Bash", "id-x", "{\"cmd\":\"ls\"}");
            IToolCardRenderer renderer = new IdempotentFakeRenderer();

            renderer.OnUpdate(chip, rec);
            renderer.OnUpdate(chip, rec); // second call: ArgsComplete → Result (Phase 2.9+)

            Assert.AreEqual(1, chip.childCount,
                "OnUpdate must be idempotent: second call must not add duplicate children (Phase 4 barrier)");
        }
    }
}
