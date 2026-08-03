// TDD tests for MarkdownInlineFormatter — pure inline markdown → Unity rich text.
// Lives in UnityMCP.Editor (base assembly), no chat dependency.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MarkdownInlineFormatterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Null_ReturnsNull()
        {
            Assert.IsNull(MarkdownInlineFormatter.ToRichText(null));
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", MarkdownInlineFormatter.ToRichText(""));
        }

        [Test]
        public void PlainText_PassesThrough()
        {
            Assert.AreEqual("hello world", MarkdownInlineFormatter.ToRichText("hello world"));
        }

        [Test]
        public void Bold_WrapsWithBTag()
        {
            var result = MarkdownInlineFormatter.ToRichText("**bold**");
            StringAssert.Contains("<b>bold</b>", result);
        }

        [Test]
        public void Italic_SingleStar_WrapsWithITag()
        {
            var result = MarkdownInlineFormatter.ToRichText("*italic*");
            StringAssert.Contains("<i>italic</i>", result);
        }

        [Test]
        public void InlineCode_BackticksColored()
        {
            var result = MarkdownInlineFormatter.ToRichText("`code`");
            StringAssert.Contains("<color=", result);
            StringAssert.Contains("code", result);
        }

        [Test]
        public void CodeSpan_ProtectsInnerStars()
        {
            var result = MarkdownInlineFormatter.ToRichText("`*not bold*`");
            Assert.IsFalse(result.Contains("<b>"), $"Got: {result}");
            Assert.IsFalse(result.Contains("<i>"), $"Got: {result}");
            StringAssert.Contains("*not bold*", result);
        }

        [Test]
        public void AngleBracket_Neutralized()
        {
            var result = MarkdownInlineFormatter.ToRichText("a<b>c");
            StringAssert.Contains("<noparse><</noparse>", result);
        }
    }
}
