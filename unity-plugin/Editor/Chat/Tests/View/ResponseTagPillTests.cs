// TDD tests for P7: ResponseTagInliner.HasTags/Split, RefParser, MixedParagraphRenderer.
// Pure headless tests — VE tree assertions, no live panel.
using NUnit.Framework;
using System.Linq;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ResponseTagPillTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => ChipKindRegistry.ResetToBuiltIns();

        // ── HasTags ───────────────────────────────────────────────────────────

        [Test]
        public void HasTags_PlainText_ReturnsFalse()
            => Assert.IsFalse(ResponseTagInliner.HasTags("hello world"));

        [Test]
        public void HasTags_WithHierarchyTag_ReturnsTrue()
            => Assert.IsTrue(ResponseTagInliner.HasTags("see [hierarchy:/Player#1]"));

        // ── Split ─────────────────────────────────────────────────────────────

        [Test]
        public void Split_TextOnly_SingleLiteralSegment()
        {
            var segs = ResponseTagInliner.Split("hello world");
            Assert.AreEqual(1, segs.Count);
            Assert.IsFalse(segs[0].IsTag);
            Assert.AreEqual("hello world", segs[0].Text);
        }

        [Test]
        public void Split_TagOnly_SingleTagSegment()
        {
            var segs = ResponseTagInliner.Split("[hierarchy:/X#1]");
            Assert.AreEqual(1, segs.Count);
            Assert.IsTrue(segs[0].IsTag);
            Assert.AreEqual("hierarchy", segs[0].KindKey);
            Assert.AreEqual("/X#1",     segs[0].Text);
        }

        [Test]
        public void Split_TextTagText_ThreeSegments()
        {
            var segs = ResponseTagInliner.Split("a [script:Foo] b");
            Assert.AreEqual(3, segs.Count);
            Assert.IsFalse(segs[0].IsTag); Assert.AreEqual("a ",    segs[0].Text);
            Assert.IsTrue (segs[1].IsTag); Assert.AreEqual("script", segs[1].KindKey); Assert.AreEqual("Foo", segs[1].Text);
            Assert.IsFalse(segs[2].IsTag); Assert.AreEqual(" b",     segs[2].Text);
        }

        [Test]
        public void Split_MultipleTags_CorrectCount()
        {
            // "[hierarchy:/A#1] and [script:B]" → tag, literal, tag  (3)
            // First char is '[' so no leading text segment.
            var segs = ResponseTagInliner.Split("[hierarchy:/A#1] and [script:B]");
            Assert.AreEqual(3, segs.Count);
            Assert.IsTrue (segs[0].IsTag);   // hierarchy tag
            Assert.IsFalse(segs[1].IsTag);   // " and "
            Assert.IsTrue (segs[2].IsTag);   // script tag
        }

        [Test]
        public void Split_AdjacentTags_NoEmptyTextBetween()
        {
            var segs = ResponseTagInliner.Split("[hierarchy:/A#1][script:B]");
            Assert.AreEqual(2, segs.Count);
            Assert.IsTrue(segs[0].IsTag);
            Assert.IsTrue(segs[1].IsTag);
        }

        [Test]
        public void Split_UnknownKind_LeftLiteral()
        {
            var segs = ResponseTagInliner.Split("[foobar:x]");
            Assert.AreEqual(1, segs.Count);
            Assert.IsFalse(segs[0].IsTag);
            Assert.AreEqual("[foobar:x]", segs[0].Text);
        }

        [Test]
        public void Split_MalformedBracket_LeftLiteral()
        {
            // Empty ref — [^\]]+ requires >=1 char
            var segs = ResponseTagInliner.Split("[hierarchy:]");
            Assert.AreEqual(1, segs.Count);
            Assert.IsFalse(segs[0].IsTag);
        }

        [Test]
        public void Split_ThirdPartyKind_SplitsCorrectly()
        {
            ChipKindRegistry.Register(new MinimalTestProvider("test_kind"));
            var segs = ResponseTagInliner.Split("[test_kind:path]");
            Assert.AreEqual(1, segs.Count);
            Assert.IsTrue(segs[0].IsTag);
            Assert.AreEqual("test_kind", segs[0].KindKey);
        }

        // ── RefParser ─────────────────────────────────────────────────────────

        [Test]
        public void RefParser_HierarchyWithId_ExtractsPathAndId()
        {
            var d = RefParser.Parse("hierarchy", "/Root/Child #-33506");
            Assert.AreEqual("/Root/Child", d.Path);
            Assert.AreEqual("-33506",      d.ObjectId);
            Assert.AreEqual("Child",       d.DisplayName);
        }

        [Test]
        public void RefParser_HierarchyNoId_PathAndZeroId()
        {
            var d = RefParser.Parse("hierarchy", "/Root/Child");
            Assert.AreEqual("/Root/Child", d.Path);
            Assert.AreEqual("",            d.ObjectId);
            Assert.AreEqual("Child",       d.DisplayName);
        }

        [Test]
        public void RefParser_AssetPath_LeafNameExtracted()
        {
            var d = RefParser.Parse("script", "Assets/Scripts/Foo.cs");
            Assert.AreEqual("Assets/Scripts/Foo.cs", d.Path);
            Assert.AreEqual("Foo.cs",                d.DisplayName);
            Assert.AreEqual("",                      d.ObjectId);
        }

        [Test]
        public void RefParser_NegativeInstanceId_ParsedCorrectly()
        {
            var d = RefParser.Parse("hierarchy", "/Cam #-1");
            Assert.AreEqual("-1", d.ObjectId);
        }

        // ── MixedParagraphRenderer VE assembly ───────────────────────────────

        [Test]
        public void MixedParagraphRenderer_TextAndTag_ProducesLabelAndPill()
        {
            var ve = MixedParagraphRenderer.Render("hello [hierarchy:/Player#1] world");

            // Container must have 3 children: Label, wrapper(pill+panel), Label
            Assert.AreEqual(3, ve.childCount,
                $"Expected 3 children (Label+wrapper+Label), got {ve.childCount}");

            Assert.IsInstanceOf<Label>(ve[0], "first child must be Label");
            Assert.IsInstanceOf<Label>(ve[2], "last child must be Label");

            // Middle child is the pill wrapper — pill is inside it
            var wrapper = ve[1];
            Assert.IsTrue(wrapper.ClassListContains("chip-pill-wrapper"),
                "middle child must have 'chip-pill-wrapper' class");

            var pill = wrapper.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "wrapper must contain an inline-chip-pill");

            // Response mode: no remove button (no Button child)
            var btn = pill.Q<Button>();
            Assert.IsNull(btn, "response pill must have no Button (onRemove==null)");
        }

        // ── List item pill rendering (item 2) ────────────────────────────────

        [Test]
        public void InlineElement_ListItemWithTag_ContainsPillChild()
        {
            // A bullet-list line with a [kind:ref] tag must render a pill, not literal text.
            var ve = MixedParagraphRenderer.InlineElement("[hierarchy:/X#1]", "md-list-content");
            Assert.IsTrue(ve.ClassListContains("md-list-content"),
                "InlineElement must add the supplied cssClass");
            // The VE is a mixed container — find the pill by its class
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill,
                $"List item with tag must contain a pill (class inline-chip-pill). Children: {ve.childCount}");
        }

        [Test]
        public void InlineElement_PlainListItem_ReturnsLabel()
        {
            var ve = MixedParagraphRenderer.InlineElement("plain text", "md-list-content");
            Assert.IsTrue(ve.ClassListContains("md-list-content"),
                "InlineElement must add cssClass to plain-text element");
            // Must NOT contain a pill — no tags present
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNull(pill, "Plain text list item must not contain a pill");
        }

        // ── Stale pill detection (Bug 4) ──────────────────────────────────────

        [Test]
        public void BuildPill_MissingAsset_PillIsStale()
        {
            MixedParagraphRenderer.ContextOverride = new PreviewContextStub(
                new FakeChipExistenceService { ExistsImpl = (_, __) => false });
            var ve = MixedParagraphRenderer.Render("[script:Assets/Missing.cs]");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill);
            Assert.AreEqual(0.4f, pill.style.opacity.value, 0.001f,
                "stale pill must have opacity 0.4");
            Assert.IsTrue(pill.tooltip.StartsWith("[NOT FOUND]"),
                "stale pill tooltip must start with [NOT FOUND]");
        }

        [Test]
        public void BuildPill_ValidAsset_PillIsNormal()
        {
            MixedParagraphRenderer.ContextOverride = new PreviewContextStub(
                new FakeChipExistenceService { ExistsImpl = (_, __) => true });
            var ve = MixedParagraphRenderer.Render("[script:Assets/Foo.cs]");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill);
            // opacity keyword = Null/Undefined when not explicitly set (default = 1)
            var opacity = pill.style.opacity;
            Assert.IsTrue(opacity.keyword == StyleKeyword.Null || opacity.keyword == StyleKeyword.Undefined || opacity.value >= 0.99f,
                "valid pill must not be faded");
        }

        [Test]
        public void BuildPill_HierarchyNotInScene_PillIsStale()
        {
            MixedParagraphRenderer.ContextOverride = new PreviewContextStub(
                new FakeChipExistenceService { ExistsImpl = (key, _) => key != ChipKindKeys.Hierarchy });
            var ve = MixedParagraphRenderer.Render("[hierarchy:/Player#1]");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill);
            Assert.AreEqual(0.4f, pill.style.opacity.value, 0.001f,
                "missing hierarchy pill must be stale");
        }

        // ── HierarchyChipProvider.Navigate regression ─────────────────────────

        [Test]
        public void Navigate_RefWithInstanceId_RefParserStripsId()
        {
            // RefParser must produce path "/Player" (no #id) from "/Player #12345"
            var data = RefParser.Parse("hierarchy", "/Player #12345");
            Assert.AreEqual("/Player", data.Path, "RefParser must strip #id suffix for Navigate use");
            Assert.AreEqual("12345",   data.ObjectId);
            Assert.AreEqual("Player",  data.DisplayName);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private sealed class PreviewContextStub : IPreviewContext
        {
            public IAssetPreviewService PreviewService => null;
            public IChipExistenceService ExistenceService { get; }
            public System.Threading.CancellationToken CancellationToken => default;

            public PreviewContextStub(IChipExistenceService existenceService)
                => ExistenceService = existenceService;
        }

        private sealed class MinimalTestProvider : IChipKindProvider
        {
            public MinimalTestProvider(string key) => Key = key;
            public string Key          { get; }
            public int    Priority     => 9999;
            public string IconName     => "";
            public string HexColor     => "#aabbcc";
            public string DefaultDepth => "path";
            public string[] BarePathExtensions => System.Array.Empty<string>();
            public bool   CanHandle(UnityEngine.Object o, string p) => false;
            public ChipData Create(UnityEngine.Object o, string p) => default;
            public string FormatPayload(ChipData c, ChipPayloadContext x) => $"[{Key}:{c.Path}]";
            public void   Navigate(string r) { }
            public void   Ping(string r) { }
            public void   AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string r) { }
        }
    }
}
