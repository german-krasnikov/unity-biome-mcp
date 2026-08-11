// TDD tests for ResponseTagTokenizer — pure unit, no Unity objects.
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ResponseTagTokenizerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetForTests();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetForTests();

        // ── Basic text ────────────────────────────────────────────────────────

        [Test]
        public void Tokenize_Null_ReturnsEmpty()
        {
            var tokens = ResponseTagTokenizer.Tokenize((string)null);
            Assert.IsEmpty(tokens);
        }

        [Test]
        public void Tokenize_Empty_ReturnsEmpty()
        {
            var tokens = ResponseTagTokenizer.Tokenize("");
            Assert.IsEmpty(tokens);
        }

        [Test]
        public void Tokenize_PlainText_ReturnsSingleTextToken()
        {
            var tokens = ResponseTagTokenizer.Tokenize("hello world");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
            Assert.AreEqual("hello world", tokens[0].Raw);
        }

        // ── Bracket tags ──────────────────────────────────────────────────────

        [Test]
        public void Tokenize_LegacyBracket_ReturnsTag()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[hierarchy:/Player]");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("hierarchy", tokens[0].KindKey);
            Assert.AreEqual("/Player", tokens[0].Ref);
        }

        [Test]
        public void Tokenize_BracketWithHashId_ReturnsTag()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[hierarchy:/Player#123]");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("hierarchy", tokens[0].KindKey);
            Assert.AreEqual("/Player#123", tokens[0].Ref);
        }

        [Test]
        public void Tokenize_UnknownKind_ReturnsText()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[unknown:foo]");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
        }

        [Test]
        public void Tokenize_EmptyRef_ReturnsText()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[hierarchy:]");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
        }

        [Test]
        public void Tokenize_MarkdownLink_ReturnsText()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[Click here](https://example.com)");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
        }

        [Test]
        public void Tokenize_BracketPathWithNestedBrackets_ParsesCorrectly()
        {
            var tokens = ResponseTagTokenizer.Tokenize("[hierarchy:[GAMEPLAY]/[PLACEMENTS]/Repair]");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("[GAMEPLAY]/[PLACEMENTS]/Repair", tokens[0].Ref);
        }

        // ── Unicode fences ────────────────────────────────────────────────────

        [Test]
        public void Tokenize_UnicodeFence_ReturnsTag()
        {
            var tokens = ResponseTagTokenizer.Tokenize("⟦hierarchy:/Player⟧");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("hierarchy", tokens[0].KindKey);
            Assert.AreEqual("/Player", tokens[0].Ref);
        }

        [Test]
        public void Tokenize_UnknownFence_ReturnsText()
        {
            var tokens = ResponseTagTokenizer.Tokenize("⟦unknown:foo⟧");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
        }

        [Test]
        public void Tokenize_FenceWithNestedBrackets_ParsesCorrectly()
        {
            var tokens = ResponseTagTokenizer.Tokenize("⟦hierarchy:[GAMEPLAY]/Repair⟧");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("[GAMEPLAY]/Repair", tokens[0].Ref);
        }

        // ── Bare paths ────────────────────────────────────────────────────────

        [Test]
        public void Tokenize_BareImagePath_ReturnsBarePath()
        {
            var tokens = ResponseTagTokenizer.Tokenize("saved to img.png");
            Assert.AreEqual(2, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
            Assert.AreEqual("saved to ", tokens[0].Raw);
            Assert.AreEqual(TokenKind.BarePath, tokens[1].Kind);
            Assert.AreEqual("image", tokens[1].KindKey);
            Assert.AreEqual("img.png", tokens[1].Ref);
        }

        [Test]
        public void Tokenize_BacktickQuotedPath_ReturnsBarePath()
        {
            var tokens = ResponseTagTokenizer.Tokenize("saved to `img with spaces.png` here");
            Assert.AreEqual(3, tokens.Count);
            Assert.AreEqual(TokenKind.BarePath, tokens[1].Kind);
            Assert.AreEqual("img with spaces.png", tokens[1].Ref);
        }

        [Test]
        public void Tokenize_NoImageTokens_AllText()
        {
            var tokens = ResponseTagTokenizer.Tokenize("hello world no images here");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind);
        }

        [Test]
        public void Tokenize_BarePathInsideTag_NotDoubleTokenized()
        {
            var tokens = ResponseTagTokenizer.Tokenize("⟦image:photo.png⟧");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.Tag, tokens[0].Kind);
            Assert.AreEqual("photo.png", tokens[0].Ref);
        }

        // ── Mixed ─────────────────────────────────────────────────────────────

        [Test]
        public void Tokenize_MixedTextAndTags_PreservesOrder()
        {
            var tokens = ResponseTagTokenizer.Tokenize("Check [hierarchy:/Player] and [script:Foo]");
            var tags = tokens.Where(t => t.Kind == TokenKind.Tag).ToList();
            Assert.AreEqual(2, tags.Count);
            Assert.AreEqual("hierarchy", tags[0].KindKey);
            Assert.AreEqual("/Player", tags[0].Ref);
            Assert.AreEqual("script", tags[1].KindKey);
            Assert.AreEqual("Foo", tags[1].Ref);
        }

        // ── Custom provider extensions ────────────────────────────────────────

        [Test]
        public void Tokenize_ProviderExtension_RecognizesCustomBareExtension()
        {
            ChipKindRegistry.Register(new TestKindProvider());
            var tokens = ResponseTagTokenizer.Tokenize("file.custom_ext");
            Assert.AreEqual(1, tokens.Count);
            Assert.AreEqual(TokenKind.BarePath, tokens[0].Kind);
            Assert.AreEqual("test_kind", tokens[0].KindKey);
            Assert.AreEqual("file.custom_ext", tokens[0].Ref);
        }

        // ── helper ────────────────────────────────────────────────────────────

        private sealed class TestKindProvider : IChipKindProvider
        {
            public string Key          => "test_kind";
            public int    Priority     => 10;
            public string IconName     => "d_DefaultAsset Icon";
            public string HexColor     => "#abcdef";
            public string DefaultDepth => "path";
            public string[] BarePathExtensions => new[] { ".custom_ext" };

            public bool CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath) => default;
            public string FormatPayload(ChipData chip, ChipPayloadContext ctx) => $"[{Key}:{chip.Path}]";
            public void Navigate(string reference) { }
            public void Ping(string reference) { }
            public void AppendContextMenuItems(UnityEngine.UIElements.DropdownMenu menu, string reference) { }
        }

        // E1 (Regression matrix): emoji before chip must not corrupt char-index calculation.
        // 🌳 (U+1F333) is a surrogate pair in UTF-16 (2 chars). The '[' for the chip tag
        // sits at char index 2, not 1. A span-index bug here would miss the tag entirely.
        [Test]
        public void E1_Tokenize_EmojiBeforeChip_CorrectTokens()
        {
            var tokens = ResponseTagTokenizer.Tokenize("🌳 [hierarchy:/Tree0]");
            Assert.AreEqual(2, tokens.Count, "must produce exactly 2 tokens");
            Assert.AreEqual(TokenKind.Text, tokens[0].Kind, "first token must be Text");
            Assert.AreEqual("🌳 ", tokens[0].Raw, "emoji and space must be in text token");
            Assert.AreEqual(TokenKind.Tag, tokens[1].Kind, "second token must be Tag");
            Assert.AreEqual("hierarchy", tokens[1].KindKey);
            Assert.AreEqual("/Tree0", tokens[1].Ref);
        }
    }
}
