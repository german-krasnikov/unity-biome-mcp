using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CopyTextBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ForUser ───────────────────────────────────────────────────────────

        [Test]
        public void ForUser_NullText_ReturnsEmpty()
        {
            Assert.AreEqual("", CopyTextBuilder.ForUser(null));
        }

        [Test]
        public void ForUser_PlainText_ReturnsVerbatim()
        {
            Assert.AreEqual("hello world", CopyTextBuilder.ForUser("hello world"));
        }

        // ── ForAssistant ──────────────────────────────────────────────────────

        [Test]
        public void ForAssistant_NullMarkdown_ReturnsEmpty()
        {
            Assert.AreEqual("", CopyTextBuilder.ForAssistant(null));
        }

        [Test]
        public void ForAssistant_RichMarkdown_ReturnsRawSource()
        {
            const string md = "# Title\n**bold**";
            Assert.AreEqual(md, CopyTextBuilder.ForAssistant(md));
        }

        // ── ForToolChip ───────────────────────────────────────────────────────

        [Test]
        public void ForToolChip_NameOnly_ReturnsName()
        {
            var rec = new ToolCallRecord("get_hierarchy", "id1", "");
            Assert.AreEqual("get_hierarchy", CopyTextBuilder.ForToolChip(rec));
        }

        [Test]
        public void ForToolChip_NameAndArgs_IncludesArgs()
        {
            var rec = new ToolCallRecord("set_property", "id2", "{\"path\":\"/Cube\"}");
            var result = CopyTextBuilder.ForToolChip(rec);
            Assert.IsTrue(result.Contains("set_property"));
            Assert.IsTrue(result.Contains("{\"path\":\"/Cube\"}"));
        }

        [Test]
        public void ForToolChip_WithResult_IncludesResult()
        {
            var rec = new ToolCallRecord("get_hierarchy", "id3", "{}", "Root/Cube", true);
            var result = CopyTextBuilder.ForToolChip(rec);
            Assert.IsTrue(result.Contains("get_hierarchy"));
            Assert.IsTrue(result.Contains("Root/Cube"));
        }

        // ── ForToolGroup ──────────────────────────────────────────────────────

        [Test]
        public void ForToolGroup_MultipleChildren_JoinsWithNewlines()
        {
            var result = CopyTextBuilder.ForToolGroup(new[] { "a", "b" });
            Assert.AreEqual("a\nb", result);
        }

        [Test]
        public void ForToolGroup_EmptyList_ReturnsEmpty()
        {
            Assert.AreEqual("", CopyTextBuilder.ForToolGroup(new string[0]));
        }
    }
}
