// TDD T-5.2 — ThinkingBlock: collapsed Foldout with reasoning text.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ThinkingBlockTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ReturnsFoldout()
        {
            var el = ThinkingBlock.Build("some reasoning");
            Assert.IsInstanceOf<Foldout>(el);
        }

        [Test]
        public void Build_FoldoutCollapsedByDefault()
        {
            var foldout = ThinkingBlock.Build("some reasoning");
            Assert.IsFalse(foldout.value, "Foldout should be collapsed by default");
        }

        [Test]
        public void Build_ContainsThinkingText()
        {
            var el = ThinkingBlock.Build("Let me think about this");
            // contentContainer holds elements added via foldout.Add(); the toggle
            // header label ("Reasoning…") is in the Toggle subtree, not here.
            var label = el.contentContainer.Q<Label>();
            Assert.IsNotNull(label, "content area must contain a Label");
            Assert.IsTrue(label.text.Contains("Let me think about this"),
                "content label must carry the reasoning text");
        }

        [Test]
        public void Build_HasThinkingBlockCssClass()
        {
            var el = ThinkingBlock.Build("text");
            Assert.IsTrue(el.ClassListContains("thinking-block"));
        }
    }
}
