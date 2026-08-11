// Pure inline Markdown → Unity rich-text. No chat dependencies.
// Handles bold, italic, inline code, links. Reused by UpdatesPage (changelog).
// MarkdownInline (Chat assembly) delegates here for the shared steps.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    public static class MarkdownInlineFormatter
    {
        public static bool IsDarkTheme { get; set; } = true;
        public static string CodeColor       => IsDarkTheme ? "#9aa5ce" : "#3b5a8a";
        internal static string ActiveLinkColor => IsDarkTheme ? "#566677" : "#2c4a6e";

        // Collision-proof Unicode non-characters for code-span placeholders.
        private const string SlotOpen  = "﷐";
        private const string SlotClose = "﷑";

        private static readonly Regex _code  = new Regex(@"`([^`]+)`",     RegexOptions.Compiled);
        private static readonly Regex _bold  = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex _under = new Regex(@"\b_(.+?)_\b",   RegexOptions.Compiled);
        private static readonly Regex _ital  = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);
        private static readonly Regex _link  = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);

        public static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("<", "<noparse><</noparse>");
        }

        public static string ToRichText(string span)
        {
            if (span == null) return null;
            if (span == "") return "";

            var slots = new List<string>();
            var text = _code.Replace(span, m =>
            {
                slots.Add(m.Groups[1].Value);
                return SlotOpen + (slots.Count - 1) + SlotClose;
            });

            text = Escape(text);
            text = _bold.Replace(text, "<b>$1</b>");
            text = _under.Replace(text, "<i>$1</i>");
            text = _ital.Replace(text, "<i>$1</i>");
            text = _link.Replace(text,
                m => m.Groups[1].Value + " <color=" + ActiveLinkColor + ">" + m.Groups[2].Value + "</color>");

            for (int i = 0; i < slots.Count; i++)
            {
                var inner = Escape(slots[i]);
                text = text.Replace(SlotOpen + i + SlotClose,
                    "<color=" + CodeColor + ">" + inner + "</color>");
            }

            return text;
        }
    }
}
