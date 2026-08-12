// Regression matrix tests for MarkdownBlockRenderer: list items, blockquotes, and table cells.
// B6: multiple chips in a bullet list item → all pills rendered via MixedParagraphRenderer.
// D1: chip in blockquote → rendered as rich-text link, NOT as a pill.
// E1-E5: chips in table data cells → pills with click navigation; alignment preserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
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

        // ── E1-E5: table data cells → pills (D3 fix) ─────────────────────────────

        // E1: data cell with hierarchy chip → pill rendered AND click triggers navigation.
        // Verifies outcome (navigate invoked), not just element presence.
        // Real data: hierarchy path from Plans/regression-sample-real-answer.md.
        [Test]
        public void E1_RenderTable_DataCellChip_ClickNavigatesToHierarchyPath()
        {
            LogAssert.ignoreFailingMessages = true;
            string captured = null;

            // Replace built-in HierarchyChipProvider with a spy.
            ChipKindRegistry.Unregister(ChipKindKeys.Hierarchy);
            ChipKindRegistry.Register(new SpyChipProvider(ChipKindKeys.Hierarchy, r => captured = r));

            // Real table from regression-sample: objects + types.
            var rows = new List<string[]>
            {
                new[] { "Объект", "Тип" },
                new[] { "[hierarchy:/Car]", "Animator" },
            };
            var block = MdBlock.Table(rows);
            var table = _renderer.Render(in block);

            // Structure: data row → first cell has md-td and contains a pill.
            var dataRow = table[1];
            Assert.IsTrue(dataRow.ClassListContains("md-table-row"), "data row must be md-table-row");
            var firstCell = dataRow[0];
            Assert.IsTrue(firstCell.ClassListContains("md-td"), "data cell must carry md-td class");

            var pill = firstCell.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "chip in data cell must render as a pill");

            // Attach to live panel — ClickEvent requires UIElements panel.
            var window = CreateOwnedEditorWindow<TableCellTestWindow>();
            window.ShowUtility();
            window.rootVisualElement.Add(table);

            SendClick(pill, clickCount: 1);

            Assert.AreEqual("/Car", captured,
                "Single click on pill in table data cell must invoke Navigate('/Car')");
        }

        // E2: header cell always remains a Label; alignment preserved (regression guard).
        [Test]
        public void E2_RenderTable_HeaderCell_RemainsLabelWithAlignment()
        {
            var rows = new List<string[]>
            {
                new[] { "Объект", "Компонент" },
                new[] { "[hierarchy:/Car]", "Rigidbody" },
            };
            // center-align second column.
            var aligns = new[] { "none", "center" };
            var block = MdBlock.Table(rows, aligns);
            var table = _renderer.Render(in block);

            var headerRow = table[0];
            var objHeader = headerRow[0];
            var compHeader = headerRow[1];

            Assert.IsInstanceOf<Label>(objHeader, "header cell must remain a Label");
            Assert.IsTrue(objHeader.ClassListContains("md-th"), "header cell must carry md-th");
            // Center-aligned header must retain text alignment style.
            Assert.AreEqual(UnityEngine.TextAnchor.MiddleCenter,
                ((Label)compHeader).style.unityTextAlign.value,
                "center-aligned header cell must keep MiddleCenter text alignment");
        }

        // E3: multiple chips in one data cell → multiple pills.
        // Real data: wall objects from regression-sample.
        [Test]
        public void E3_RenderTable_DataCellWithMultipleChips_AllPillsPresent()
        {
            var rows = new List<string[]>
            {
                new[] { "Стены" },
                new[] { "[hierarchy:/House/WallBack], [hierarchy:/House/WallFrontL]" },
            };
            var block = MdBlock.Table(rows);
            var table = _renderer.Render(in block);

            var cell = table[1][0];
            Assert.IsTrue(cell.ClassListContains("md-td"), "cell must have md-td");
            var pills = cell.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count, $"two chips in one data cell must produce two pills; got {pills.Count}");
        }

        // E4: table with no chips in data cells renders as plain Labels (plain-table guard).
        [Test]
        public void E4_RenderTable_NoChips_DataCellsAreLabels()
        {
            var rows = new List<string[]>
            {
                new[] { "Название", "Значение" },
                new[] { "Позиция X", "1.5" },
            };
            var block = MdBlock.Table(rows);
            var table = _renderer.Render(in block);

            var dataRow = table[1];
            Assert.IsInstanceOf<Label>(dataRow[0], "plain data cell must remain a Label");
            Assert.IsInstanceOf<Label>(dataRow[1], "plain data cell must remain a Label");
            Assert.IsTrue(dataRow[0].ClassListContains("md-td"), "plain data cell must carry md-td");

            // No pills anywhere.
            var pills = table.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(0, pills.Count, "table without chips must have no pills");
        }

        // E5: blockquote regression guard — D1 behaviour unchanged after table fix.
        [Test]
        public void E5_RenderQuote_ChipNotPill_RegressionAfterTableFix()
        {
            var block = MdBlock.Quote(new List<string> { "Animator на [hierarchy:/Car]" });
            var ve = _renderer.Render(in block);

            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "blockquote must NOT produce a pill after table data cell fix");
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private sealed class TableCellTestWindow : EditorWindow { }

        private sealed class SpyChipProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly Action<string> _navigate;

            public SpyChipProvider(string key, Action<string> navigate)
            {
                _key = key;
                _navigate = navigate;
            }

            public string   Key                => _key;
            public int      Priority           => 500;
            public string   HexColor           => "#000000";
            public string   IconName           => "";
            public string   DefaultDepth       => "path";
            public string[] BarePathExtensions => Array.Empty<string>();
            public bool     CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath)    => default;
            public string   FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";
            public void     Navigate(string reference) => _navigate?.Invoke(reference);
            public void     Ping(string reference) { }
            public void     AppendContextMenuItems(DropdownMenu menu, string reference) { }
        }

        private static void SendClick(VisualElement target, int clickCount)
        {
            var evt = new ClickEvent();
            SetClickCount(evt, clickCount);
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SetClickCount(ClickEvent evt, int count)
        {
            var type = evt.GetType();
            while (type != null && type != typeof(object))
            {
                var field = type.GetField("<clickCount>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) { field.SetValue(evt, count); return; }
                type = type.BaseType;
            }
        }
    }
}
