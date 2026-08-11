// TDD T-5.2 — ChatTranscript.AppendThinkingBlock: ephemeral, not serialized.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatTranscriptTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _container;
        private ChatTranscript _transcript;

        [SetUp]
        public void SetUp()
        {
            _container = new VisualElement();
            _transcript = new ChatTranscript(
                _container, ChatBlockRendererFactory.CreateDefault(null, null));
        }

        [Test]
        public void AppendThinkingBlock_AddsElementToContainer()
        {
            _transcript.AppendThinkingBlock("reasoning text");
            Assert.IsNotNull(_container.Q(className: "thinking-block"),
                "thinking-block element must appear in the container");
        }

        [Test]
        public void AppendThinkingBlock_NotAddedToEntries()
        {
            _transcript.AppendThinkingBlock("reasoning text");
            // _entries is private; observable via SerializeForReload():
            // a fresh transcript with only a thinking block must produce empty serialized data.
            Assert.AreEqual("", _transcript.SerializeForReload(),
                "thinking block must not be written to _entries (ephemeral)");
        }
    }
}
