using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatLabelWrapTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string LongText =
            "[hierarchy: Tree0] — это пустой GameObject в сцене. " +
            "Только Transform на позиции (-5, 0, -3), никаких компонентов " +
            "(ни MeshRenderer, ни Collider, ничего). Детей тоже нет.";

        [Test]
        public void Selectable_HasChatTextClass()
        {
            var label = ChatLabel.Selectable(LongText);
            Assert.IsTrue(label.ClassListContains("chat-text"),
                "Selectable label must have chat-text class for text wrapping");
        }

        [Test]
        public void Selectable_RichText_HasChatTextClass()
        {
            var label = ChatLabel.Selectable(LongText, richText: true);
            Assert.IsTrue(label.ClassListContains("chat-text"),
                "Rich-text selectable label must also wrap");
        }

        [Test]
        public void ThinkingBlock_ContentLabel_HasChatTextClass()
        {
            var foldout = ThinkingBlock.Build(LongText);
            var label = foldout.contentContainer.Q<Label>();
            Assert.IsNotNull(label);
            Assert.IsTrue(label.ClassListContains("chat-text"),
                "ThinkingBlock content label must wrap long reasoning text");
        }
    }
}
