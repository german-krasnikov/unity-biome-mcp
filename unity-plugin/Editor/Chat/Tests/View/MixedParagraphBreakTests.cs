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

        // ── V2 structural: text label inside mixed para must have minWidth=0 ────

        // Without minWidth=0, flex-shrink cannot reduce the label below its natural
        // single-line width, so long paragraphs overflow the bubble.
        [Test]
        public void Render_TextLabel_HasMinWidthZero()
        {
            var ve = MixedParagraphRenderer.Render("some text [hierarchy:/Obj]");
            var lbl = ve.Q<Label>();
            Assert.IsNotNull(lbl, "must have a text label");
            // minWidth must be explicitly set to 0 (keyword != Null means it was set inline).
            var mw = lbl.style.minWidth;
            Assert.AreNotEqual(StyleKeyword.Null, mw.keyword,
                "minWidth must be explicitly set to 0 to enable flex-shrink");
            // StyleLength.value is a Length struct; Length.value is the float.
            float minWidthPx = mw.value.value;
            Assert.AreEqual(0f, minWidthPx, 0.001f, "minWidth must be 0, not auto");
        }

        // ── Regression matrix B1-B5: render tests from real answer fixture ────

        // B1 (DEFECT 1 end-to-end — must be RED before fix)
        // "**Деревья** — [chip]" tokenizes to [Text("**Деревья** — "), Tag].
        // After StripOrphanBold fix, text arrives as "**Деревья** — " → ToRichText wraps bold.
        [Test]
        public void B1_Render_BoldCyrillicBeforeChip_LabelHasBoldMarkup()
        {
            var ve = MixedParagraphRenderer.Render("**Деревья** — [hierarchy:/Tree0]");
            var labels = ve.Query<Label>().ToList();
            Assert.IsNotEmpty(labels, "must have at least one label");
            var textLabel = labels[0];
            StringAssert.DoesNotContain("**", textLabel.text,
                $"label must not contain bare **: '{textLabel.text}'");
            StringAssert.Contains("<b>", textLabel.text,
                $"label must have bold markup: '{textLabel.text}'");
        }

        // B2 — **[chip]** (no spaces): orphan ** markers silently stripped, no visible garbage
        [Test]
        public void B2_Render_BoldWrapsChipNoSpaces_NoBareStarsInLabels()
        {
            var ve = MixedParagraphRenderer.Render("**[hierarchy:/House]**");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "pill must exist");
            foreach (var lbl in ve.Query<Label>().ToList())
                StringAssert.DoesNotContain("**", lbl.text,
                    $"label must not contain orphan **: '{lbl.text}'");
        }

        // B3 — long cyrillic sentence with chip at end: DOM completeness guard
        [Test]
        public void B3_Render_LongCyrillicTextThenChip_BothChildrenPresent()
        {
            const string raw =
                "Итого ~48 объектов. Скриптов на объектах нет — только стандартные " +
                "Unity-компоненты, вся геометрия собрана из примитивов. " +
                "Единственная «логика» — Animator на [hierarchy:/Car].";
            var ve = MixedParagraphRenderer.Render(raw);
            Assert.IsTrue(ve.ClassListContains("md-para--mixed"), "must be mixed container");
            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "chip must render as pill (DOM completeness)");
            Assert.Greater(ve.Query<Label>().ToList().Count, 0, "must have at least one label");
        }

        // B4 — chip with /N suffix: suffix preserved as text token
        [Test]
        public void B4_Render_ChipWithSlashSuffix_SuffixPreservedAsText()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/House/WinLeft1]/2, [hierarchy:/House/WinRight1]/2");
            var pills = ve.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count, "must have 2 pills");
            // Text labels between and after chips must contain the /2 fragments
            var allText = string.Join("", ve.Query<Label>().ToList().ConvertAll(l => l.text));
            StringAssert.Contains("/2", allText, "suffix /2 must appear as text");
        }

        // B5 — two chips separated by ".." range operator
        [Test]
        public void B5_Render_TwoChipsWithRangeOp_TwoPillsOneLabelWithDots()
        {
            var ve = MixedParagraphRenderer.Render("[hierarchy:/Tree0]..[hierarchy:/Tree3]");
            var pills = ve.Query(className: "inline-chip-pill").ToList();
            Assert.AreEqual(2, pills.Count, "must have 2 pills");
            // Use direct children only — Query<Label> descends into pill wrappers
            var labels = ve.Children().OfType<Label>().ToList();
            Assert.AreEqual(1, labels.Count, "must have exactly 1 direct-child label (the '..' text)");
            StringAssert.Contains("..", labels[0].text, "label must be the '..' range separator");
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
