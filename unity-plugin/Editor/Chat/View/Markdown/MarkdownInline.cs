// Converts inline Markdown spans to Unity rich-text. Pure, NUnit-testable. F20: Linker seam removed.
// Delegates base formatting to MarkdownInlineFormatter (UnityMCP.Editor assembly),
// then applies chat-specific [kind:ref] tag expansion via ResponseTagInliner.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static class MarkdownInline
    {
        internal static string CodeColor => MarkdownInlineFormatter.CodeColor; // single source; ChatLinkify matches this

        // U+FDD0/U+FDD1 are permanent Unicode non-characters — they can never appear in
        // real text, so they are collision-proof placeholders for extracted code spans.
        private const string SlotOpen  = "﷐";
        private const string SlotClose = "﷑";

        private static readonly Regex _code = new Regex(@"`([^`]+)`", RegexOptions.Compiled);

        /// <summary>Neutralizes a literal '&lt;' with a noparse scope so UIToolkit rich-text
        /// won't treat it as a tag. UIToolkit decodes NO HTML entities, so emitting '&amp;lt;'
        /// renders verbatim (the bug). '&amp;' and '&gt;' are harmless and pass through.</summary>
        public static string Escape(string s) => MarkdownInlineFormatter.Escape(s);

        /// <summary>
        /// Converts inline markdown to Unity rich-text. Neutralizes literal '&lt;' FIRST
        /// (via noparse) so raw HTML from input never becomes real tags. Code spans protect inner stars.
        /// Also expands chat [kind:ref] tags via ResponseTagInliner.
        /// </summary>
        public static string ToRichText(string span)
        {
            if (span == null) return null;
            if (span == "") return "";

            // Step 1: extract code spans before escaping so their content is preserved verbatim.
            var codeSlots = new List<string>();
            var withPlaceholders = _code.Replace(span, m =>
            {
                codeSlots.Add(m.Groups[1].Value);
                return SlotOpen + (codeSlots.Count - 1) + SlotClose;
            });

            // Step 2: escape HTML in the non-code text.
            withPlaceholders = MarkdownInlineFormatter.Escape(withPlaceholders);

            // Step 2b: convert [kind:ref] tags to rich-text links (headings, blockquotes,
            // table cells, and the plain-paragraph fallback path). In the pill path the text
            // is Split first, so individual segments never reach here — Apply is a no-op on
            // tagless runs, so no double-render is possible.
            withPlaceholders = ResponseTagInliner.Apply(withPlaceholders);

            // Step 3: apply bold, italic, links via shared formatter.
            // Re-run on the code-slotted text so placeholders survive bold/italic.
            withPlaceholders = ApplySpanFormatting(withPlaceholders);

            // Step 4: restore code spans as colored (not italic — code isn't emphasis).
            for (int i = 0; i < codeSlots.Count; i++)
            {
                var inner = MarkdownInlineFormatter.Escape(codeSlots[i]);
                withPlaceholders = withPlaceholders.Replace(
                    SlotOpen + i + SlotClose,
                    "<color=" + CodeColor + ">" + inner + "</color>");
            }

            return withPlaceholders;
        }

        private static readonly Regex _bold  = new Regex(@"\*\*(.+?)\*\*",     RegexOptions.Compiled);
        private static readonly Regex _under = new Regex(@"\b_(.+?)_\b",       RegexOptions.Compiled);
        private static readonly Regex _ital  = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex _link  = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static string ApplySpanFormatting(string s)
        {
            s = _bold.Replace(s, "<b>$1</b>");
            s = _under.Replace(s, "<i>$1</i>");
            s = _ital.Replace(s, "<i>$1</i>");
            s = _link.Replace(s, m => m.Groups[1].Value + " <color=" + MarkdownInlineFormatter.ActiveLinkColor + ">" + m.Groups[2].Value + "</color>");
            return s;
        }
    }
}
