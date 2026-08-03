// TDD — MixedParagraphRenderer line-break tests (Bug 2 fix).
// Verifies \n in text segments creates proper break elements in flex-row layout.
using NUnit.Framework;
using System.Linq;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MixedParagraphBreakTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
        }

        // Count break elements: height=0, flexBasis=100%
        private static int CountBreaks(VisualElement container)
        {
            int count = 0;
            foreach (var child in container.Children())
            {
                // Break element: not a Label and not a pill/wrapper
                if (child is Label) continue;
                if (child.ClassListContains("inline-chip-pill")) continue;
                if (child.ClassListContains("chip-pill-wrapper")) continue;
                count++;
            }
            return count;
        }

        // ── Positive ─────────────────────────────────────────────────────────

        // MP1: text with \n and tag → container has break element between lines
        [Test]
        public void MP1_TextNewlineTag_HasBreakBetweenLines()
        {
            // "line1\nline2 [hierarchy:/X#1]"
            var ve = MixedParagraphRenderer.Render("line1\nline2 [hierarchy:/X#1]");
            // Should contain break element(s) for the newline
            Assert.Greater(CountBreaks(ve), 0, "Expected at least one break element for \\n");
        }

        // MP2: three lines with tags → exactly 2 break elements
        [Test]
        public void MP2_ThreeLinesWithTags_TwoBreaks()
        {
            var raw = "line1 [hierarchy:/A#1]\nline2 [hierarchy:/B#2]\nline3";
            var ve = MixedParagraphRenderer.Render(raw);
            Assert.AreEqual(2, CountBreaks(ve), "3 lines → 2 break elements");
        }

        // MP3: tag on first line, text on second → break between them
        [Test]
        public void MP3_TagFirstLineThenText_BreakBetween()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/X#1]\nsecond line");
            Assert.AreEqual(1, CountBreaks(ve), "Expected exactly 1 break for 1 \\n");
            // Must have the pill and the second-line label
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "Pill must be present");
        }

        // ── Negative/edge ─────────────────────────────────────────────────────

        // MP4: single line with tag → NO break elements
        [Test]
        public void MP4_SingleLineWithTag_NoBreaks()
        {
            var ve = MixedParagraphRenderer.Render("hello [hierarchy:/X#1] world");
            Assert.AreEqual(0, CountBreaks(ve), "Single line must have no break elements");
        }

        // MP5: text-only paragraph (no tags) → InlineElement returns plain Label, no mixed container
        [Test]
        public void MP5_PlainText_InlineElementReturnsLabel()
        {
            var ve = MixedParagraphRenderer.InlineElement("just plain text", "md-para");
            // Plain text → Label, not a mixed container
            Assert.IsInstanceOf<Label>(ve, "Plain text must return a Label, not mixed container");
            Assert.IsTrue(ve.ClassListContains("md-para"));
        }

        // MP6: empty lines between content ("\n\n") → break elements for each \n
        [Test]
        public void MP6_DoubleNewline_TwoBreaks()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/A#1]\n\n[hierarchy:/B#2]");
            // Two \n chars → 2 breaks
            Assert.AreEqual(2, CountBreaks(ve), "\\n\\n must produce 2 break elements");
        }

        // MP7: tag-only content (no text, no newline) → no break elements
        [Test]
        public void MP7_TagOnly_NoBreaks()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/X#1]");
            Assert.AreEqual(0, CountBreaks(ve), "Tag-only content must have no breaks");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "Pill must be present");
        }

        // MP9: InlineElement with plain text containing \n → Label, no breaks (plain path)
        [Test]
        public void MP9_PlainTextNewline_InlineElement_HandledCorrectly()
        {
            // No tags → InlineElement returns a plain Label (not mixed container).
            // The \n is part of the text content, not a break element.
            var ve = MixedParagraphRenderer.InlineElement("hello\nworld", "md-para");
            Assert.IsInstanceOf<Label>(ve,
                "Plain text (no tags) must return a Label even with \\n");
            Assert.IsTrue(ve.ClassListContains("md-para"));
        }

        // ── InlineElement with bare image paths ───────────────────────────────

        // MP14: text with relative screenshot path → InlineElement returns mixed container, not Label
        [Test]
        public void InlineElement_TextWithImagePath_ReturnsMixedContainer()
        {
            var ve = MixedParagraphRenderer.InlineElement("saved to ScreenShots/2026-06-17.png", "md-para");
            // Must be a container (md-para--mixed), not a plain Label
            Assert.IsNotInstanceOf<Label>(ve, "Text with image path must go through Render(), not plain Label");
            Assert.IsTrue(ve.ClassListContains("md-para--mixed"), "Mixed container must have md-para--mixed class");
            Assert.IsTrue(ve.ClassListContains("md-para"), "cssClass must be added");
        }

        // MP15: plain text without image path → still returns Label (regression guard)
        [Test]
        public void InlineElement_PlainText_StillReturnsLabel()
        {
            var ve = MixedParagraphRenderer.InlineElement("no images here", "md-para");
            Assert.IsInstanceOf<Label>(ve, "Plain text must remain a Label");
        }

        // MP10: triple newline in tagged content → 3 break elements
        [Test]
        public void MP10_TripleNewline_ThreeBreaks()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/A#1]\n\n\n[hierarchy:/B#2]");
            Assert.AreEqual(3, CountBreaks(ve), "\\n\\n\\n must produce 3 break elements");
        }

        // ── F22: orphan ** bold markers stripped from text segments ──────────

        // F22a: "**" prefix and suffix stripped around pill
        [Test]
        public void F22a_OrphanBoldMarkers_StrippedFromTextSegments()
        {
            // LLM output: "**[hierarchy:/Name#1]**" → text "**" + pill + text "**"
            var ve = MixedParagraphRenderer.Render("** [hierarchy:/Name#1] **");
            // No label should contain bare "**"
            foreach (var lbl in ve.Query<Label>().ToList())
                StringAssert.DoesNotContain("**", lbl.text,
                    $"Label must not contain orphan **: '{lbl.text}'");
        }

        // F22b: coordinates after orphan ** are preserved
        [Test]
        public void F22b_CoordinatesAfterOrphanBold_Preserved()
        {
            // text segment "** (3, 0.5, 3) |" → after strip → "(3, 0.5, 3) |"
            var stripped = MixedParagraphRenderer.StripOrphanBold("** (3, 0.5, 3) |");
            StringAssert.Contains("(3, 0.5, 3)", stripped);
            StringAssert.DoesNotStartWith("**", stripped);
        }

        // F22c: balanced **bold** as whole segment must NOT be stripped
        [Test]
        public void F22c_BalancedBold_NotStripped()
        {
            var result = MixedParagraphRenderer.StripOrphanBold("**important**");
            Assert.AreEqual("**important**", result, "Balanced bold must survive StripOrphanBold");
        }

        // MP8: newline immediately before tag → break element before pill
        [Test]
        public void MP8_NewlineBeforeTag_BreakBeforePill()
        {
            var ve = MixedParagraphRenderer.Render("text\n[hierarchy:/X#1]");
            Assert.AreEqual(1, CountBreaks(ve), "Newline before tag must produce 1 break");
            // Break must appear before the pill in child order
            int breakIdx = -1, pillIdx = -1;
            for (int i = 0; i < ve.childCount; i++)
            {
                var child = ve[i];
                if (!child.ClassListContains("inline-chip-pill") && !(child is Label) && child.Q(className: "inline-chip-pill") == null)
                    breakIdx = i;
                else if (child.ClassListContains("inline-chip-pill") || child.Q(className: "inline-chip-pill") != null)
                    pillIdx = i;
            }
            Assert.Greater(pillIdx, breakIdx, "Break must precede pill in DOM order");
        }
    }
}
