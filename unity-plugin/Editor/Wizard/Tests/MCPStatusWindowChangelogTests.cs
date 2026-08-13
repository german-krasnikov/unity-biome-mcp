using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPStatusWindowChangelogTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ChangelogBody_ConvertsMarkdown()
        {
            var content = "**bold** and `code`";
            var text = MarkdownInlineFormatter.ToRichText(content);
            StringAssert.Contains("<b>bold</b>", text);
            StringAssert.DoesNotContain("**", text);
        }

    }
}
