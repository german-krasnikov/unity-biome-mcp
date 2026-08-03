// BareNameNormalizer: converts bare scene object names in LLM responses to [kind:path #id] tags.
// F14a: mirrors AtMentionNormalizer's longest-first scan but matches bare words (no @ prefix).
// Protected ranges: existing [kind:ref] bracket tags + backtick code spans are never re-tagged.
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal static class BareNameNormalizer
    {
        internal static string Normalize(string text, IReadOnlyList<ChipData> sentChips)
        {
            if (text == null) return "";
            if (string.IsNullOrEmpty(text)) return text;
            if (sentChips == null || sentChips.Count == 0) return text;

            // Skip single-char names (too ambiguous). Longest-first prevents partial matches.
            var sorted = sentChips
                .Where(c => !string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.Length > 1)
                .OrderByDescending(c => c.DisplayName.Length)
                .ToList();
            if (sorted.Count == 0) return text;

            // Build protected ranges: [kind:ref] tags and `backtick` spans
            var protected_ = BuildProtectedRanges(text);

            var sb  = new StringBuilder(text.Length);
            int pos = 0;

            while (pos < text.Length)
            {
                bool matched = false;
                if (!InProtectedRange(protected_, pos))
                {
                    foreach (var chip in sorted)
                    {
                        var name = chip.DisplayName;
                        if (pos + name.Length > text.Length) continue;
                        if (string.Compare(text, pos, name, 0, name.Length,
                                System.StringComparison.OrdinalIgnoreCase) != 0) continue;

                        int after = pos + name.Length;
                        // Word boundary: char before must not be letter/digit/underscore
                        bool boundBefore = pos == 0 || !IsWordChar(text[pos - 1]);
                        bool boundAfter  = after >= text.Length || !IsWordChar(text[after]);
                        if (boundBefore && boundAfter)
                        {
                            sb.Append(ChipContextResolver.FormatChipRef(
                                chip.KindKey, chip.Path, chip.ObjectId));
                            pos     = after;
                            matched = true;
                            break;
                        }
                    }
                }

                if (!matched) { sb.Append(text[pos]); pos++; }
            }

            return sb.ToString();
        }

        // ── private helpers ───────────────────────────────────────────────────

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>Returns (start, exclusiveEnd) pairs for protected zones.</summary>
        private static List<(int start, int end)> BuildProtectedRanges(string text)
        {
            var ranges = new List<(int, int)>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '[')
                {
                    // Look for [kind:ref] — find matching ']'
                    int close = text.IndexOf(']', i + 1);
                    if (close > i) { ranges.Add((i, close + 1)); i = close + 1; continue; }
                }
                else if (text[i] == '`')
                {
                    // Triple-backtick fenced block — must be checked BEFORE single backtick
                    if (i + 2 < text.Length && text[i+1] == '`' && text[i+2] == '`')
                    {
                        int close = text.IndexOf("```", i + 3, System.StringComparison.Ordinal);
                        if (close > i) { ranges.Add((i, close + 3)); i = close + 3; continue; }
                    }
                    int singleClose = text.IndexOf('`', i + 1);
                    if (singleClose > i) { ranges.Add((i, singleClose + 1)); i = singleClose + 1; continue; }
                }
                i++;
            }
            return ranges;
        }

        private static bool InProtectedRange(List<(int start, int end)> ranges, int pos)
        {
            foreach (var (s, e) in ranges)
            {
                if (pos >= s && pos < e) return true;
            }
            return false;
        }
    }
}
