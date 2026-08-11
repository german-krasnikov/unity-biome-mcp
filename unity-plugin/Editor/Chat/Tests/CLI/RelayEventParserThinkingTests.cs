// TDD T-5.2 — RelayEventParser: parse "th|" lines into ChatEventKind.Thinking events.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayEventParserThinkingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Parse_th_ReturnsThinkingEventWithText()
        {
            var ev = RelayEventParser.Parse("th|some reasoning text");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.Thinking, ev.Value.Kind);
            Assert.AreEqual("some reasoning text", ev.Value.Text);
        }

        [Test]
        public void Parse_th_PreservesPipeInText()
        {
            var ev = RelayEventParser.Parse("th|think|more|and|more");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.Thinking, ev.Value.Kind);
            Assert.AreEqual("think|more|and|more", ev.Value.Text);
        }

        [Test]
        public void Parse_UnknownPrefix_StillNull()
        {
            var ev = RelayEventParser.Parse("xx|something");
            Assert.IsNull(ev);
        }

        [Test]
        public void Parse_th_EmptyText_ReturnsEventWithEmptyString()
        {
            var ev = RelayEventParser.Parse("th|");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.Thinking, ev.Value.Kind);
            Assert.AreEqual("", ev.Value.Text);
        }
    }
}
