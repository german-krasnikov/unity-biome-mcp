// Parses Markdown text into a list of MdBlocks. Single-pass. Pure, NUnit-testable.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Chat
{
    public static partial class MarkdownParser
    {
        internal static readonly Regex ImageStandalone =
            new Regex(@"^!\[([^\]]*)\]\(([^)]*)\)$", RegexOptions.Compiled);

        internal static readonly Regex OrderedItem =
            new Regex(@"^(\d+)\.\s+(.*)", RegexOptions.Compiled);

        /// <summary>Parses markdown text into blocks. Null/empty → empty list.</summary>
        public static List<MdBlock> Parse(string md)
        {
            if (string.IsNullOrEmpty(md)) return new List<MdBlock>();

            var lines  = md.Split('\n');
            var result = new List<MdBlock>();
            int i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // ── fenced code (FIRST priority) ─────────────────────────────
                if (line.StartsWith("```")) { i = ParseFence(lines, i, result); continue; }

                // ── blank line ────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

                // ── heading ───────────────────────────────────────────────────
                if (line.StartsWith("#"))
                {
                    int lvl = CountLeadingHashes(line);
                    if (lvl > 0 && lvl <= 6 && line.Length > lvl && line[lvl] == ' ')
                    { result.Add(MdBlock.Heading(lvl, line.Substring(lvl + 1))); i++; continue; }
                }

                // ── horizontal rule ───────────────────────────────────────────
                if (IsHRule(line)) { result.Add(MdBlock.Rule()); i++; continue; }

                // ── table (peek next line for separator) ──────────────────────
                if (line.Contains("|") && i + 1 < lines.Length && IsSeparator(lines[i + 1]))
                { i = ParseTable(lines, i, result); continue; }

                // ── standalone image ──────────────────────────────────────────
                var imgM = ImageStandalone.Match(line.Trim());
                if (imgM.Success)
                { result.Add(MdBlock.Image(imgM.Groups[2].Value, imgM.Groups[1].Value)); i++; continue; }

                // ── blockquote ────────────────────────────────────────────────
                if (line.StartsWith("> ") || line == ">")
                { i = ParseBlockQuote(lines, i, result); continue; }

                // ── bullet list ───────────────────────────────────────────────
                if (IsBullet(line)) { i = ParseBullets(lines, i, result); continue; }

                // ── ordered list ──────────────────────────────────────────────
                if (OrderedItem.IsMatch(line))
                {
                    var rawNum = OrderedItem.Match(line).Groups[1].Value;
                    int start = int.TryParse(rawNum, out var n) ? n : 1;
                    i = ParseOrdered(lines, i, result, start);
                    continue;
                }

                // ── paragraph ─────────────────────────────────────────────────
                i = ParseParagraph(lines, i, result);
            }

            return result;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        internal static bool IsHRule(string line)
        {
            var t = line.Trim();
            if (t.Length < 3) return false;
            char c = t[0];
            if (c != '-' && c != '*' && c != '_') return false;
            int count = 0;
            foreach (var ch in t) { if (ch != c && ch != ' ') return false; if (ch == c) count++; }
            return count >= 3;
        }

        internal static bool IsBullet(string line)
        {
            var t = line.TrimStart();
            return t.StartsWith("- ") || t.StartsWith("* ") || t.StartsWith("+ ");
        }

        internal static bool IsSeparator(string line)
        {
            var t = line.Trim();
            if (!t.Contains("-")) return false;
            foreach (var c in t) if (c != '|' && c != '-' && c != ':' && c != ' ') return false;
            return true;
        }

        private static int CountLeadingHashes(string line)
        {
            int n = 0;
            foreach (var c in line) { if (c == '#') n++; else break; }
            return n;
        }
    }
}
