using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentFrontmatterParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string SampleFrontmatter =
            "---\nname: code-reviewer\ndescription: \"Reviews code\"\nmodel: sonnet\n---\n\nBody here.";

        [Test]
        public void ParseName_WithFrontmatterName_ReturnsThatName() =>
            Assert.AreEqual("code-reviewer", AgentFrontmatterParser.ParseName(SampleFrontmatter, "somefile"));

        [Test]
        public void ParseName_QuotedName_ReturnsUnquoted() =>
            Assert.AreEqual("senior-dev",
                AgentFrontmatterParser.ParseName("---\nname: \"senior-dev\"\n---", "somefile"));

        [Test]
        public void ParseName_NoFrontmatter_ReturnsStem() =>
            Assert.AreEqual("my-agent", AgentFrontmatterParser.ParseName("Just a body with no fences.", "my-agent"));

        [Test]
        public void ParseName_FrontmatterWithoutNameKey_ReturnsStem() =>
            Assert.AreEqual("stemname",
                AgentFrontmatterParser.ParseName("---\ndescription: foo\nmodel: sonnet\n---", "stemname"));

        [Test]
        public void ParseName_CrlfInput_Works() =>
            Assert.AreEqual("haiku-tester",
                AgentFrontmatterParser.ParseName("---\r\nname: haiku-tester\r\nmodel: haiku\r\n---\r\n", "stem"));

        [Test]
        public void ParseName_NameWithExtraSpaces_Trimmed() =>
            Assert.AreEqual("doc-keeper",
                AgentFrontmatterParser.ParseName("---\nname:   doc-keeper  \n---", "stem"));

        [Test]
        public void ParseName_LeadingBlankLine_Works() =>
            Assert.AreEqual("opus",
                AgentFrontmatterParser.ParseName("\n---\nname: opus\n---", "stem"));

        [Test]
        public void ParseName_SingleQuotedName_ReturnsUnquoted() =>
            Assert.AreEqual("code-reviewer",
                AgentFrontmatterParser.ParseName("---\nname: 'code-reviewer'\n---", "somefile"));
    }
}
