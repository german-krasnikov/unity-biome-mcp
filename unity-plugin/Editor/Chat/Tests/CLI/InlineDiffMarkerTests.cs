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
                "Markers must survive color-tag wrap and produce <u> tags");
            Assert.IsFalse(finalized.Contains(Open.ToString()),
                "No raw Open markers must remain after FinalizeMarkers");
        }
    }
}
