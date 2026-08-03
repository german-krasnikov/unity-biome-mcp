// AtMentionNormalizer: converts @Name mentions in LLM responses to [kind:ref] tags.
// Fixes Bug 2: LLM echoes @Name back; normalizer converts to bracket format
// so MixedParagraphRenderer can render them as pills.
// Uses manual longest-first scan (not regex) to correctly handle multi-word names.
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Replaces @Name or @/FullPath mentions in LLM response text with [kind:path #id] bracket tags
    /// when a matching chip exists in the sent chips for the turn.
    /// Multi-word names and full-path echoes handled via longest-first matching.
    /// </summary>
    internal static class AtMentionNormalizer
    {
        /// <summary>Normalize @Name / @/Path mentions in text using sentChips as the lookup source.</summary>
        internal static string Normalize(string text, IReadOnlyList<ChipData> sentChips)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (sentChips == null || sentChips.Count == 0) return text;

            // Build candidate list: for each chip emit (matchText, chip) for DisplayName
            // and (if non-empty and different) for Path. Then sort globally longest-first so
            // @/UI Canvas/Main Camera wins over @Main Camera wins over @Main.
            var candidates = new List<(string MatchText, ChipData Chip)>();
            foreach (var chip in sentChips)
            {
                if (!string.IsNullOrEmpty(chip.DisplayName))
                    candidates.Add((chip.DisplayName, chip));
                if (!string.IsNullOrEmpty(chip.Path) && chip.Path != chip.DisplayName)
                    candidates.Add((chip.Path, chip));
            }
            if (candidates.Count == 0) return text;

            // Longest-first across both DisplayName and Path entries.
            candidates.Sort((a, b) => b.MatchText.Length.CompareTo(a.MatchText.Length));

            var sb  = new StringBuilder(text.Length);
            int pos = 0;

            while (pos < text.Length)
            {
                int at = text.IndexOf('@', pos);
                if (at < 0)
                {
                    sb.Append(text, pos, text.Length - pos);
                    break;
                }

                sb.Append(text, pos, at - pos);

                int nameStart = at + 1;
                bool matched  = false;
                foreach (var (matchText, chip) in candidates)
                {
                    if (nameStart + matchText.Length > text.Length) continue;
                    if (string.Compare(text, nameStart, matchText, 0, matchText.Length,
                            System.StringComparison.OrdinalIgnoreCase) != 0) continue;

                    int after = nameStart + matchText.Length;
                    bool boundary = after >= text.Length
                        || (!char.IsLetterOrDigit(text[after]) && text[after] != '_');
                    if (!boundary) continue;

                    sb.Append(ChipContextResolver.FormatChipRef(
                        chip.KindKey, chip.Path, chip.ObjectId));
                    pos     = after;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    sb.Append('@');
                    pos = nameStart;
                }
            }
            return sb.ToString();
        }
    }
}
