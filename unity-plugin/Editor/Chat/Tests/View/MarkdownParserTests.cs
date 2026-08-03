// Tests for MarkdownParser. Pure, NUnit-testable.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MarkdownParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Null_DoesNotThrow()
        {
            List<MdBlock> result = null;
            Assert.DoesNotThrow(() => result = MarkdownParser.Parse(null));
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Empty_NoBlocks()
        {
            var result = MarkdownParser.Parse("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Heading_LevelFromHashCount()
        {
            var result = MarkdownParser.Parse("## Hello");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Heading, result[0].Kind);
            Assert.AreEqual(2, result[0].Level);
            Assert.AreEqual("Hello", result[0].Lines[0]);
        }

        [Test]
        public void CodeFence_CapturesLangAndBody()
        {
            var md = "```csharp\nint x = 0;\n```";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.CodeFence, result[0].Kind);
            Assert.AreEqual("csharp", result[0].Lang);
            Assert.AreEqual("int x = 0;", result[0].Lines[0]);
        }

        [Test]
        public void MermaidFence_KindMermaid()
        {
            var md = "```mermaid\ngraph TD\nA-->B\n```";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Mermaid, result[0].Kind);
        }

        [Test]
        public void UnclosedMermaidFence_RendersAsCode()
        {
            // While streaming the closing ``` hasn't arrived → must be Code, not a broken diagram.
            var result = MarkdownParser.Parse("```mermaid\ngraph TD\nA-->B");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.CodeFence, result[0].Kind);
        }

        [Test]
        public void HashInsideFence_NotHeading()
        {
            var md = "```\n# not a heading\n```";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.CodeFence, result[0].Kind);
        }

        [Test]
        public void Image_StandaloneLine()
        {
            var result = MarkdownParser.Parse("![my alt](path/img.png)");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Image, result[0].Kind);
            Assert.AreEqual("path/img.png", result[0].Src);
            Assert.AreEqual("my alt", result[0].Alt);
        }

        [Test]
        public void ImageInline_NotExtracted()
        {
            // Inline image inside other text → stays as paragraph
            var result = MarkdownParser.Parse("See ![img](x.png) here");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Paragraph, result[0].Kind);
        }

        [Test]
        public void Table_HeaderSeparatorRows()
        {
            var md = "| A | B |\n|---|---|\n| 1 | 2 |";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Table, result[0].Kind);
            // separator row excluded; header + data = 2 rows
            Assert.AreEqual(2, result[0].TableRows.Count);
        }

        [Test]
        public void Table_PeekNextLine()
        {
            // A pipe line followed by a non-separator should NOT become a table
            var md = "| A | B |\n| 1 | 2 |";
            var result = MarkdownParser.Parse(md);
            // Without separator the second line won't trigger table mode — treated as paragraph
            Assert.AreNotEqual(MdBlockKind.Table, result[0].Kind);
        }

        [Test]
        public void Bullets_GroupConsecutive()
        {
            var md = "- alpha\n- beta\n- gamma";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.BulletList, result[0].Kind);
            Assert.AreEqual(3, result[0].Lines.Count);
        }

        [Test]
        public void Ordered_StartIndex()
        {
            var md = "3. first\n4. second";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.OrderedList, result[0].Kind);
            Assert.AreEqual(3, result[0].Level); // start index = 3
            Assert.AreEqual(2, result[0].Lines.Count);
        }

        [Test]
        public void BlockQuote_StripsMarker()
        {
            var md = "> line one\n> line two";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.BlockQuote, result[0].Kind);
            Assert.AreEqual("line one", result[0].Lines[0]);
            Assert.AreEqual("line two", result[0].Lines[1]);
        }

        [Test]
        public void HorizontalRule_TripleDash()
        {
            var result = MarkdownParser.Parse("---");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.HorizontalRule, result[0].Kind);
        }

        [Test]
        public void MixedDocument_BlockOrderPreserved()
        {
            var md = "# Title\n\nSome text\n\n- a\n- b";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(MdBlockKind.Heading,   result[0].Kind);
            Assert.AreEqual(MdBlockKind.Paragraph, result[1].Kind);
            Assert.AreEqual(MdBlockKind.BulletList, result[2].Kind);
        }
    }
}
