// CH5.test.5: Direct tests for ChatBlockRendererRegistry.RenderBlock.
// Tests the fallback path (no renderer matches) and the first-match-wins dispatch.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatBlockRendererRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ChatBlockRendererRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new ChatBlockRendererRegistry();
        }

        // Fallback: empty registry — no renderer matches → plain Label is returned (never null)
        [Test]
        public void RenderBlock_NoRenderers_FallbackLabelReturned()
        {
            var block = MdBlock.Para(new List<string> { "hello world" });
            var result = _registry.RenderBlock(in block);

            Assert.IsNotNull(result, "RenderBlock must never return null");
            Assert.IsInstanceOf<Label>(result, "fallback must be a Label");
        }

        [Test]
        public void RenderBlock_Fallback_LabelContainsBlockText()
        {
            var block = MdBlock.Para(new List<string> { "some text" });
            var result = _registry.RenderBlock(in block);

            var lbl = result as Label;
            Assert.IsNotNull(lbl);
            StringAssert.Contains("some text", lbl.text);
        }

        // Fallback for null Lines: should not throw
        [Test]
        public void RenderBlock_NullLines_NoThrow()
        {
            var block = default(MdBlock); // Kind=Paragraph, Lines=null
            Assert.DoesNotThrow(() => _registry.RenderBlock(in block));
        }

        // Registered renderer: first CanRender winner takes the block
        [Test]
        public void RenderBlock_RegisteredRenderer_UsedForMatchingBlock()
        {
            var called = false;
            _registry.Register(new AlwaysTrueRenderer(() => { called = true; return new Label("rendered"); }));

            var block = MdBlock.Para(new List<string> { "any" });
            var result = _registry.RenderBlock(in block);

            Assert.IsTrue(called, "registered renderer must be invoked when CanRender returns true");
            Assert.IsInstanceOf<Label>(result);
            Assert.AreEqual("rendered", ((Label)result).text);
        }

        // First-match-wins: second renderer never called when first matches
        [Test]
        public void RenderBlock_FirstMatchWins_SecondRendererNotCalled()
        {
            int call1 = 0, call2 = 0;
            _registry.Register(new AlwaysTrueRenderer(() => { call1++; return new Label("first"); }));
            _registry.Register(new AlwaysTrueRenderer(() => { call2++; return new Label("second"); }));

            var block = MdBlock.Para(new List<string> { "x" });
            _registry.RenderBlock(in block);

            Assert.AreEqual(1, call1, "first renderer must be called once");
            Assert.AreEqual(0, call2, "second renderer must NOT be called when first matches");
        }

        // Helper renderer stub
        private sealed class AlwaysTrueRenderer : IChatBlockRenderer
        {
            private readonly System.Func<VisualElement> _render;
            internal AlwaysTrueRenderer(System.Func<VisualElement> render) => _render = render;
            public bool CanRender(in MdBlock block) => true;
            public VisualElement Render(in MdBlock block) => _render();
        }
    }
}
