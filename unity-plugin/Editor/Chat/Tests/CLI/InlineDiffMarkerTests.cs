// TDD Phase 2.2 — InlineDiffMarker. 10 tests, all RED before implementation.
// Key test: FinalizeMarkers_SurvivesColorTagWrap closes the <mark>/<u> mine.
//
// NOTE on C# hex escapes: "\x01b" is U+001B (greedy, 3 hex digits), NOT U+0001+'b'.
// Use (char)1 / (char)2 for unambiguous marker values in tests.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class InlineDiffMarkerTests : UnityMcpTestBase
    {
        // Marker sentinels — must match InlineDiffMarker internals
        private static readonly char Open  = (char)1;   // U+0001 SOH
        private static readonly char Close = (char)2;   // U+0002 STX

        // === FindChangeBounds ===

        [Test]
        public void FindChangeBounds_MethodRename_CorrectRegion()
        {
            // "Get" is shared prefix (3 chars); "(go);" is shared suffix (5 chars)
            var (prefix, suffix) = InlineDiffMarker.FindChangeBounds("GetWireValue(go);", "GetHexRef(go);");
            Assert.AreEqual(3, prefix, "Shared prefix 'Get' has length 3");
            Assert.AreEqual(5, suffix, "Shared suffix '(go);' has length 5");
        }

        [Test]
        public void FindChangeBounds_IdenticalLines_FullPrefixSuffix()
        {
            var (prefix, suffix) = InlineDiffMarker.FindChangeBounds("abc", "abc");
            Assert.AreEqual(3, prefix + suffix, "prefix+suffix must equal length for identical lines");
        }

        [Test]
        public void FindChangeBounds_SingleCharDeletion_Correct()
        {
            var (prefix, suffix) = InlineDiffMarker.FindChangeBounds("abc", "ab");
            Assert.AreEqual(2, prefix);
            Assert.AreEqual(0, suffix);
        }

        [Test]
        public void FindChangeBounds_AdditionAtEnd_SuffixZero()
        {
            var (prefix, suffix) = InlineDiffMarker.FindChangeBounds("a", "ab");
            Assert.AreEqual(1, prefix);
            Assert.AreEqual(0, suffix);
        }

        [Test]
        public void FindChangeBounds_EmptyOld_ZeroZero()
        {
            var (prefix, suffix) = InlineDiffMarker.FindChangeBounds("", "abc");
            Assert.AreEqual(0, prefix);
            Assert.AreEqual(0, suffix);
        }

        // === InsertMarkers ===

        [Test]
        public void InsertMarkers_PlacesMarkersCorrectly()
        {
            // "abc" prefix=1 suffix=1 → "a" + Open + "b" + Close + "c"
            var expected = "a" + Open + "b" + Close + "c";
            var result   = InlineDiffMarker.InsertMarkers("abc", prefix: 1, suffix: 1);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void InsertMarkers_FullLineChanged_WrapsAll()
        {
            // prefix=0 suffix=0 → Open + "abc" + Close
            var expected = Open + "abc" + Close;
            var result   = InlineDiffMarker.InsertMarkers("abc", prefix: 0, suffix: 0);
            Assert.AreEqual(expected, result);
        }

        // === FinalizeMarkers ===

        [Test]
        public void FinalizeMarkers_ReplacesWithUnderlineTags()
        {
            var input  = Open + "changed" + Close;
            var result = InlineDiffMarker.FinalizeMarkers(input.ToString());
            Assert.AreEqual("<u>changed</u>", result);
        }

        [Test]
        public void FinalizeMarkers_NoMarkers_Unchanged()
        {
            var result = InlineDiffMarker.FinalizeMarkers("plain text");
            Assert.AreEqual("plain text", result);
        }

        [Test]
        public void FinalizeMarkers_SurvivesColorTagWrap()
        {
            // Simulate: mark → SyntaxHighlighter wraps in <color> → FinalizeMarkers.
            // Verifies that \x01/\x02 markers survive color-tag wrapping.
            // This closes the mine: <mark> does NOT work in UI Toolkit.
            var (p, s) = InlineDiffMarker.FindChangeBounds("GetWireValue(go);", "GetHexRef(go);");
            var marked      = InlineDiffMarker.InsertMarkers("GetWireValue(go);", p, s);
            var highlighted = "<color=#9aa5ce>" + marked + "</color>";
            var finalized   = InlineDiffMarker.FinalizeMarkers(highlighted);

            StringAssert.Contains("<u>", finalized,
                "Open marker must survive color-tag wrap and produce <u>");
            StringAssert.Contains("</u>", finalized,
                "Close marker must survive color-tag wrap and produce </u>");
            Assert.IsFalse(finalized.Contains(Open.ToString()),
                "No raw Open markers must remain after FinalizeMarkers");
            Assert.IsFalse(finalized.Contains(Close.ToString()),
                "No raw Close markers must remain after FinalizeMarkers");
        }

        // === Strengthened FinalizeMarkers coverage ===

        [Test]
        public void FinalizeMarkers_ExactOutput_BothMarkersReplaced()
        {
            // Exact match — proves BOTH \x01→<u> and \x02→</u> in one assertion.
            var input  = "prefix" + Open + "changed" + Close + "suffix";
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("prefix<u>changed</u>suffix", result);
        }

        [Test]
        public void FinalizeMarkers_MarkerInsideColorTag_Survives()
        {
            // Open marker lands INSIDE a <color> span; Close is after the closing tag.
            // This is what happens when the SyntaxHighlighter tokenises "prefix\x01middle"
            // as one chunk and wraps it — the marker survives inside the tag.
            var input  = "<color=#9aa5ce>prefix" + Open + "middle</color>" + Close + "suffix";
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("<color=#9aa5ce>prefix<u>middle</color></u>suffix", result);
        }

        [Test]
        public void FinalizeMarkers_MarkerAtTagBoundary_BeforeOpenTag()
        {
            // Open marker immediately before an opening color tag.
            // Verifies boundary position does not confuse replacement.
            var input  = Open + "<color=#9aa5ce>abc</color>" + Close;
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("<u><color=#9aa5ce>abc</color></u>", result);
        }

        [Test]
        public void FinalizeMarkers_TwoChangedRegions_BothUnderlined()
        {
            // Two separate Open/Close pairs in one line (future multi-region support).
            // FinalizeMarkers must replace ALL occurrences of each marker.
            var input  = "a" + Open + "b" + Close + "c" + Open + "d" + Close + "e";
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("a<u>b</u>c<u>d</u>e", result);
        }

        [Test]
        public void FinalizeMarkers_NoColorTags_ExactOutput()
        {
            // No syntax highlighting at all — plain text with markers.
            // Regression: must not require color tags to function.
            var input  = "prefix" + Open + "changed" + Close + "suffix";
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("prefix<u>changed</u>suffix", result);
        }

        [Test]
        public void FinalizeMarkers_MarkupCharsInContent_PassThrough()
        {
            // Angle brackets and quotes inside the changed region must not be
            // mangled — they must appear verbatim inside the <u> tag.
            const string content = "if (x > 0) { \"hello\" }";
            var input  = Open + content + Close;
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("<u>" + content + "</u>", result);
        }

        [Test]
        public void FinalizeMarkers_EmptyChangedRegion_ProducesEmptyUnderline()
        {
            // Adjacent markers (no content between them) → <u></u>.
            // Occurs when FindChangeBounds returns prefix+suffix == line length.
            var input  = "prefix" + Open + Close + "suffix";
            var result = InlineDiffMarker.FinalizeMarkers(input);
            Assert.AreEqual("prefix<u></u>suffix", result);
        }

        [Test]
        public void FullPipeline_MarkerSurvivesTokenizedHighlight()
        {
            // Full pipeline: InsertMarkers → per-token color wrapping → FinalizeMarkers.
            // Verifies that markers inserted before highlighting survive token-level
            // <color> tags applied by the SyntaxHighlighter.
            //
            // "GetWireValue(go);" → "GetHexRef(go);" — prefix "Get", suffix "(go);"
            var (p, s) = InlineDiffMarker.FindChangeBounds("GetWireValue(go);", "GetHexRef(go);");
            var marked = InlineDiffMarker.InsertMarkers("GetHexRef(go);", p, s);
            // marked = "Get" + Open + "HexRef" + Close + "(go);"

            // Simulate: SyntaxHighlighter matches "Get\x01HexRef" as one identifier
            // token and wraps it — Open marker ends up INSIDE the color tag.
            var tokenBody  = "Get" + Open + "HexRef";
            var highlighted = "<color=#9aa5ce>" + tokenBody + "</color>" + Close + "(go);";

            var finalized = InlineDiffMarker.FinalizeMarkers(highlighted);

            Assert.AreEqual("<color=#9aa5ce>Get<u>HexRef</color></u>(go);", finalized,
                "Marker inside color tag must survive and produce underline");
            Assert.IsFalse(finalized.Contains(Open.ToString()),  "No raw Open markers");
            Assert.IsFalse(finalized.Contains(Close.ToString()), "No raw Close markers");
        }
    }
}
