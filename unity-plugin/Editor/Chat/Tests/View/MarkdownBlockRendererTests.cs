// Regression matrix tests for MarkdownBlockRenderer: list items and blockquotes.
// B6: multiple chips in a bullet list item → all pills rendered via MixedParagraphRenderer.
// D1: chip in blockquote → rendered as rich-text link, NOT as a pill.
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MarkdownBlockRendererTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private MarkdownBlockRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
            InlinePreviewBuilder.TextureLoader = _ => UnityEngine.Texture2D.whiteTexture;
            AssetViewerFactory.ReRegisterBuiltIns();
            _renderer = new MarkdownBlockRenderer();
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetForTests();
            ChipPillFactory.ColorResolver = null;
        }

        // B6 (Regression matrix): bullet list item with multiple chips.
        // MarkdownBlockRenderer.RenderList calls MixedParagraphRenderer.InlineElement per item.
        // Verifies end-to-end path: list block → row → mixed content → pills.
        [Test]
        public void B6_RenderList_MultiplePillsInListItem_AllPillsPresent()
        {
            var block = MdBlock.Bullets(new List<string>
            {
                "Стены: [hierarchy:/House/WallBack], [hierarchy:/House/WallFrontL], [hierarchy:/House/WallFrontR]"
            });
            var ve = _renderer.Render(in block);

            Assert.IsTrue(ve.ClassListContains("md-list"), "must be md-list container");
            Assert.AreEqual(1, ve.childCount, "must have 1 row");

            var row = ve[0];
            Assert.IsTrue(row.ClassListContains("md-list-row"), "child must be md-list-row");

            var content = row.Q(className: "md-list-content");
            Assert.IsNotNull(content, "md-list-content must exist");

            var pills = content.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(3, pills.Count,
                $"must have 3 pills, one per chip. Got {pills.Count}");

            // Text separators (, ) must also be present
            var labels = content.Query<Label>().ToList();
            Assert.Greater(labels.Count, 0, "must have text labels between chips");
        }

        // D1 (Regression matrix): chip in blockquote renders as inline link, NOT pill.
        // RenderQuote uses MarkdownInline.ToRichText → ResponseTagInliner.Apply → <link=...>.
        // This confirms the architectural gap: blockquotes bypass MixedParagraphRenderer.
        // If someone accidentally routes blockquotes through MixedParagraphRenderer, this fails.
        [Test]
        public void D1_RenderQuote_ChipInBlockquote_NoInlinePill()
        {
            ChipKindRegistry.ResetForTests();
            var block = MdBlock.Quote(new List<string> { "text [hierarchy:/Car] text" });
            var ve = _renderer.Render(in block);

            Assert.IsTrue(ve.ClassListContains("md-quote"), "must be md-quote container");

            // No pill visual element should exist
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "chip in blockquote must NOT render as a pill");

            // Label must exist with rich-text link
            var lbl = ve.Q<Label>();
            Assert.IsNotNull(lbl, "blockquote must have a label");
            StringAssert.Contains("<link=", lbl.text,
                $"chip in blockquote must render as rich-text link: '{lbl.text}'");
        }
    }
}
