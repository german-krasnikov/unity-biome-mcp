using System.Collections.Generic;

namespace UnityMCP.Playtest.Core
{
    /// <summary>
    /// A blank-separated word of a .playtest line. Text is as written: a text in quotes keeps its quotes. Col is its 0-based column in the physical line,
    /// the indent included, in UTF-16 code units (as the reader of the SURFACE in JavaScript counts).
    /// </summary>
    internal readonly struct PatternToken
    {
        public readonly string Text;
        public readonly int Col;

        public PatternToken(string text, int col)
        {
            Text = text;
            Col = col;
        }
    }

    /// <summary>
    /// How a line is cut for the table of PlaytestGrammar when TokensWords is set: into what the JavaScript expression /"[^"]*"|\S+/g finds, which is how the
    /// reader of the SURFACE cuts it. A quote opens a text, blanks included, only where a token starts and only if another quote follows; the text ends at that
    /// quote, with a blank after it or not. Any other quote is part of its word, and [..] is not protected (a path with a blank in [..] is the business of its slot).
    /// </summary>
    internal static class PatternTokens
    {
        /// <summary>The tokens of the line, in order, in linear time. Never throws: no line, an empty one and one of blanks have no token.</summary>
        public static List<PatternToken> Split(string line)
        {
            var tokens = new List<PatternToken>();
            if (line == null) return tokens;
            for (var i = 0; i < line.Length;)
            {
                if (IsBlank(line[i]))
                {
                    i++;
                    continue;
                }
                var start = i;
                // A quote that finds no closing quote after it means that no quote is left in the line, so this search fails once at most.
                var close = line[i] == '"' ? line.IndexOf('"', i + 1) : -1;
                if (close >= 0)
                    i = close + 1;
                else
                    while (i < line.Length && !IsBlank(line[i])) i++;
                tokens.Add(new PatternToken(line.Substring(start, i - start), start));
            }
            return tokens;
        }

        // \s of JavaScript, not char.IsWhiteSpace: that one reads U+0085 as a blank and U+FEFF (the byte order mark) as none.
        private static bool IsBlank(char c) =>
            (c >= '\t' && c <= '\r') || c == ' ' || c == 0x00A0 || c == 0x1680 || (c >= 0x2000 && c <= 0x200A)
            || c == 0x2028 || c == 0x2029 || c == 0x202F || c == 0x205F || c == 0x3000 || c == 0xFEFF;
    }
}
