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

        // ── T-7b: nested bullet lists ─────────────────────────────────────────

        [Test]
        public void IsBullet_IndentedLine_ReturnsTrue()
        {
            Assert.IsTrue(MarkdownParser.IsBullet("  - Sub"));
        }

        [Test]
        public void ParseBullets_IndentedItem_PreservesDepth()
        {
            var result = MarkdownParser.Parse("- Top\n  - Sub");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.BulletList, result[0].Kind);
            Assert.AreEqual(2, result[0].Lines.Count);
            Assert.AreEqual("Top", result[0].Lines[0]);
            Assert.AreEqual("Sub", result[0].Lines[1]);
            Assert.IsNotNull(result[0].Depths);
            Assert.AreEqual(0, result[0].Depths[0]);
            Assert.AreEqual(1, result[0].Depths[1]);
        }

        [Test]
        public void ParseBullets_DeeplyNested()
        {
            var result = MarkdownParser.Parse("- A\n  - B\n    - C");
            Assert.AreEqual(1, result.Count);
            var b = result[0];
            Assert.AreEqual(3, b.Depths.Count);
            Assert.AreEqual(0, b.Depths[0]);
            Assert.AreEqual(1, b.Depths[1]);
            Assert.AreEqual(2, b.Depths[2]);
        }

        // ── T-7c-B item 7: table column alignment ────────────────────────────

        [Test]
        public void ParseTable_LeftAlign_SetsAligns()
        {
            var result = MarkdownParser.Parse("| A | B |\n|:--|---|\n| 1 | 2 |");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Table, result[0].Kind);
            Assert.IsNotNull(result[0].Aligns);
            Assert.AreEqual("left", result[0].Aligns[0]);
            Assert.AreEqual("none", result[0].Aligns[1]);
        }

        [Test]
        public void ParseTable_CenterAlign_SetsAligns()
        {
            var result = MarkdownParser.Parse("| A |\n|:---:|\n| 1 |");
            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].Aligns);
            Assert.AreEqual("center", result[0].Aligns[0]);
        }

        [Test]
        public void ParseTable_RightAlign_SetsAligns()
        {
            var result = MarkdownParser.Parse("| A |\n|---:|\n| 1 |");
            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].Aligns);
            Assert.AreEqual("right", result[0].Aligns[0]);
        }

        [Test]
        public void ParseTable_NoColons_AlignNone()
        {
            // Backward compat: separator without colons must parse normally, Aligns all "none"
            var result = MarkdownParser.Parse("| A | B |\n|---|---|\n| 1 | 2 |");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.Table, result[0].Kind);
            Assert.IsNotNull(result[0].Aligns);
            Assert.AreEqual("none", result[0].Aligns[0]);
            Assert.AreEqual("none", result[0].Aligns[1]);
            Assert.AreEqual(2, result[0].TableRows.Count); // header + 1 data row
        }

        [Test]
        public void ParseBullets_TabIndent_CountsAsDepth()
        {
            // tab ('\t') should be treated as one indent level (depth=1)
            var result = MarkdownParser.Parse("- Top\n\t- Sub");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].Depths[1]);
        }

        [Test]
        public void ParseBullets_FlatList_DepthsAllZero()
        {
            var result = MarkdownParser.Parse("- alpha\n- beta\n- gamma");
            Assert.AreEqual(1, result.Count);
            var b = result[0];
            Assert.IsNotNull(b.Depths);
            Assert.AreEqual(3, b.Depths.Count);
            Assert.AreEqual(0, b.Depths[0]);
            Assert.AreEqual(0, b.Depths[1]);
            Assert.AreEqual(0, b.Depths[2]);
        }

        // T1.4: int.Parse on number > INT_MAX throws OverflowException → block not rendered.
        // LLM hallucination: a number like «99999999999999» overflows int.Parse and must not crash.

        [Test]
        public void Parse_OversizedOrderedListNumber_DoesNotThrow()
        {
            // 2147483648 = int.MaxValue + 1 → int.Parse throws OverflowException without fix.
            var md = "2147483648. первый элемент\n2147483649. второй элемент";
            List<MdBlock> result = null;
            Assert.DoesNotThrow(() => result = MarkdownParser.Parse(md),
                "ordered list number > INT_MAX must not throw OverflowException");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count,
                "both items must parse as a single OrderedList block");
            Assert.AreEqual(MdBlockKind.OrderedList, result[0].Kind,
                "block must be OrderedList (not swallowed by the exception)");
        }

        [Test]
        public void Parse_OversizedOrderedListNumber_CyrillicItems_BothRendered()
        {
            // Realistic LLM output with bad numbering; verify actual item text is preserved.
            var md = "2147483648. Запустить игровой объект «Игрок»\n" +
                     "2147483649. Добавить компонент CharacterController";
            var result = MarkdownParser.Parse(md);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(MdBlockKind.OrderedList, result[0].Kind);
            Assert.AreEqual(2, result[0].Lines.Count,
                "both Cyrillic items must be in the block");
        }
    }
}

