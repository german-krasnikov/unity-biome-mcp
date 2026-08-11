// Tests for MarkdownInline. Pure, NUnit-testable.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MarkdownInlineTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Null_DoesNotThrow()
        {
            string result = null;
            Assert.DoesNotThrow(() => result = MarkdownInline.ToRichText(null));
            // null or empty is acceptable
            Assert.IsTrue(result == null || result == "");
        }

        [Test]
        public void Empty_ReturnsEmpty()
        {
            Assert.AreEqual("", MarkdownInline.ToRichText(""));
        }

        [Test]
        public void Bold_DoubleStar_WrapsB()
        {
            var result = MarkdownInline.ToRichText("**hello**");
            Assert.IsTrue(result.Contains("<b>hello</b>"), $"Got: {result}");
        }

        [Test]
        public void Italic_SingleStar_WrapsI()
        {
            var result = MarkdownInline.ToRichText("*world*");
            Assert.IsTrue(result.Contains("<i>world</i>"), $"Got: {result}");
        }

        [Test]
        public void InlineCode_Backticks_Colored()
        {
            var result = MarkdownInline.ToRichText("`foo`");
            Assert.IsTrue(result.Contains("<color=#9aa5ce>") && result.Contains("foo"), $"Got: {result}");
        }

        [Test]
        public void EscapesAngleBrackets_BeforeTags()
        {
            // <b>hi</b> must NOT become bold. UIToolkit decodes no HTML entities, so we
            // neutralize '<' with a noparse scope (NOT &lt;, which would render verbatim).
            var result = MarkdownInline.ToRichText("<b>hi</b>");
            Assert.IsTrue(result.Contains("<noparse><</noparse>"), $"Bracket not neutralized: {result}");
            Assert.IsFalse(result.Contains("&lt;"), $"Must not emit HTML entities: {result}");
            Assert.IsFalse(result.Contains("<b>hi</b>"), $"Raw tag leaked: {result}");
        }

        [Test]
        public void CodeSpanProtectsInnerStars()
        {
            // Stars inside a code span must NOT be bold/italic
            var result = MarkdownInline.ToRichText("`*not bold*`");
            Assert.IsFalse(result.Contains("<b>"), $"Should not have bold: {result}");
            Assert.IsFalse(result.Contains("<i>"), $"Should not have italic: {result}");
            Assert.IsTrue(result.Contains("*not bold*"), $"Stars should survive: {result}");
        }

        [Test]
        public void Link_RendersAsClickable()
        {
            var result = MarkdownInline.ToRichText("[click](http://example.com)");
            Assert.IsTrue(result.Contains("<link="), $"Expected <link= tag. Got: {result}");
            Assert.IsTrue(result.Contains("click"), $"Link text missing. Got: {result}");
            Assert.IsTrue(result.Contains("http://example.com"), $"URL missing. Got: {result}");
        }

        [Test]
        public void UnpairedStar_LeftLiteral()
        {
            var result = MarkdownInline.ToRichText("hello * world");
            Assert.IsTrue(result.Contains("*"), $"Star should survive: {result}");
            Assert.IsFalse(result.Contains("<i>"), $"Should not italicize: {result}");
        }

        [Test]
        public void BoldItalicNested()
        {
            var result = MarkdownInline.ToRichText("**_nested_**");
            Assert.IsTrue(result.Contains("<b>"), $"Got: {result}");
        }

        [Test]
        public void AngleBracketMidWord_Neutralized()
        {
            var result = MarkdownInline.ToRichText("vector<int>");
            Assert.IsTrue(result.Contains("<noparse><</noparse>"), $"Got: {result}");
            Assert.IsFalse(result.Contains("&lt;"), $"Got: {result}");
            Assert.IsTrue(result.Contains("int>"), $"Got: {result}");
        }

        [Test]
        public void BoldWithAngleBracket_BoldStaysRealBracketNeutralized()
        {
            var result = MarkdownInline.ToRichText("**vector<int>**");
            Assert.IsTrue(result.Contains("<b>"), $"Bold tag missing: {result}");
            Assert.IsTrue(result.Contains("<noparse><</noparse>"), $"Bracket not neutralized: {result}");
            Assert.IsFalse(result.Contains("&lt;"), $"Got: {result}");
        }

        [Test]
        public void LiteralNoparseInput_NotTreatedAsTag()
        {
            var result = MarkdownInline.ToRichText("<noparse>hi");
            Assert.IsTrue(result.StartsWith("<noparse><</noparse>noparse>"), $"Got: {result}");
            Assert.IsFalse(result.Contains("&lt;"), $"Got: {result}");
        }

        [Test]
        public void ToRichText_WithHierarchyTag_ContainsLink()
        {
            // Locks restored step-2b: [kind:ref] in non-paragraph contexts (headings, blockquotes,
            // table cells) must produce a <link= rich-text anchor, NOT literal bracket text.
            UnityMCP.Editor.Chat.ChipKindRegistry.ResetForTests();
            var result = MarkdownInline.ToRichText("[hierarchy:/X#1]");
            StringAssert.Contains("<link=", result, $"Expected <link= tag. Got: {result}");
            StringAssert.DoesNotContain("[hierarchy:/X#1]", result,
                $"Literal tag must not survive: {result}");
        }

        // C1 (Regression matrix): bold span followed by non-bold suffix.
        // The _bold regex must match even when the closing ** is NOT at end-of-string.
        // Separates responsibility: if this passes but B1 (Render) fails, the bug is in
        // StripOrphanBold. If this fails, the bug is in the bold regex itself.
        [Test]
        public void C1_ToRichText_BoldWithNonBoldSuffix_AppliesBold()
        {
            var result = MarkdownInline.ToRichText("**text** — ");
            StringAssert.Contains("<b>text</b>", result,
                $"Bold regex must match mid-string closing **: '{result}'");
            StringAssert.Contains(" — ", result,
                $"Trailing suffix must be preserved: '{result}'");
            StringAssert.DoesNotContain("**", result,
                $"No bare ** should remain: '{result}'");
        }
    }
}
