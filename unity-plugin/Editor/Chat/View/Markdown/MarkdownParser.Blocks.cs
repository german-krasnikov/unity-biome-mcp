// Block-level parsers (partial). Pure, NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    public static partial class MarkdownParser
    {
        private static int ParseFence(string[] lines, int i, List<MdBlock> result)
        {
            var lang = lines[i].Length > 3 ? lines[i].Substring(3).Trim() : "";
            var body = new List<string>();
            i++;
            bool closed = false;
            while (i < lines.Length && !lines[i].StartsWith("```")) { body.Add(lines[i]); i++; }
            if (i < lines.Length) { i++; closed = true; } // consume closing ```
            // Unclosed fence (still streaming) → Code, never a half-parsed Mermaid diagram.
            result.Add(closed && string.Equals(lang, "mermaid", System.StringComparison.OrdinalIgnoreCase)
                ? MdBlock.Mermaid(body)
                : MdBlock.Code(lang, body));
            return i;
        }

        private static int ParseTable(string[] lines, int i, List<MdBlock> result)
        {
            var rows = new List<string[]>();
            rows.Add(SplitCells(lines[i]));
            i += 2; // skip separator
            while (i < lines.Length && lines[i].Contains("|") && !string.IsNullOrWhiteSpace(lines[i]))
            { rows.Add(SplitCells(lines[i])); i++; }
            result.Add(MdBlock.Table(rows));
            return i;
        }

        private static string[] SplitCells(string row)
        {
            var trimmed = row.Trim().Trim('|');
            var parts   = trimmed.Split('|');
            for (int j = 0; j < parts.Length; j++) parts[j] = parts[j].Trim();
            return parts;
        }

        private static int ParseBlockQuote(string[] lines, int i, List<MdBlock> result)
        {
            var items = new List<string>();
            while (i < lines.Length && (lines[i].StartsWith("> ") || lines[i] == ">"))
            { items.Add(lines[i].Length > 2 ? lines[i].Substring(2) : ""); i++; }
            result.Add(MdBlock.Quote(items));
            return i;
        }

        private static int ParseBullets(string[] lines, int i, List<MdBlock> result)
        {
            var items  = new List<string>();
            var depths = new List<int>();
            while (i < lines.Length && IsBullet(lines[i]))
            {
                var raw = lines[i];
                int indent = 0;
                foreach (var ch in raw)
                {
                    if (ch == ' ') indent++;
                    else if (ch == '\t') indent += 2; // tab = one indent level (2 spaces)
                    else break;
                }
                items.Add(raw.TrimStart().Substring(2));
                depths.Add(indent / 2);
                i++;
            }
            result.Add(MdBlock.Bullets(items, depths));
            return i;
        }

        private static int ParseOrdered(string[] lines, int i, List<MdBlock> result, int start)
        {
            var items = new List<string>();
            while (i < lines.Length && OrderedItem.IsMatch(lines[i]))
            { items.Add(OrderedItem.Match(lines[i]).Groups[2].Value); i++; }
            result.Add(MdBlock.Ordered(start, items));
            return i;
        }

        private static int ParseParagraph(string[] lines, int i, List<MdBlock> result)
        {
            var items = new List<string>();
            while (i < lines.Length
                   && !string.IsNullOrWhiteSpace(lines[i])
                   && !lines[i].StartsWith("```")
                   && !lines[i].StartsWith("#")
                   && !IsHRule(lines[i])
                   && !IsBullet(lines[i])
                   && !OrderedItem.IsMatch(lines[i])
                   && !(lines[i].StartsWith("> ") || lines[i] == ">"))
            {
                if (ImageStandalone.IsMatch(lines[i].Trim())) break;
                if (lines[i].Contains("|") && i + 1 < lines.Length && IsSeparator(lines[i + 1])) break;
                items.Add(lines[i]); i++;
            }
            if (items.Count > 0) result.Add(MdBlock.Para(items));
            return i;
        }
    }
}
