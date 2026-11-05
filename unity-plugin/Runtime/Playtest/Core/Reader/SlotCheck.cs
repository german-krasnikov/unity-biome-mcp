using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Element = UnityMCP.Playtest.Core.PlaytestGrammar.Element;
using SlotType = UnityMCP.Playtest.Core.PlaytestGrammar.SlotType;

namespace UnityMCP.Playtest.Core
{
    /// <summary>
    /// What a slot of a form takes as a value, as the reader of the SURFACE in JavaScript decides it (types.ts, checkParam), and what a message calls the slot.
    /// The value is a token of PatternTokens.Split: a blank is none of its characters unless it is in quotes, so JavaScript and .NET cannot disagree about one.
    /// </summary>
    internal static class SlotCheck
    {
        // A token that the expression of a slot takes a long time over (a long run of [ against the expression of a path) must not hold the editor up.
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

        // One Regex per expression of the table, made when a line first asks for it. The parser may run on any thread.
        private static readonly ConcurrentDictionary<string, Regex> Expressions = new ConcurrentDictionary<string, Regex>();

        /// <summary>
        /// Whether the slot takes the token. Text takes any string; Number is -?[0-9]+(\.[0-9]+)?, UInt is [0-9]+ (digits of ASCII only); Match is a regular
        /// expression that must read the whole token, and a match that runs out of time is not a match; Enum is one of the words of Arg, as written.
        /// No value (null) is taken by none. A value never throws; a slot type that this class does not know does, so that a type added to the table is not accepted in silence.
        /// </summary>
        public static bool Accepts(Element slot, string raw) => slot.Type switch
        {
            SlotType.Text => raw != null,
            SlotType.Number => raw != null && IsNumber(raw),
            SlotType.UInt => raw != null && IsUInt(raw),
            SlotType.Enum => raw != null && IsWordOf(slot.Arg, raw),
            SlotType.Match => raw != null && Reads(slot.Arg, raw),
            _ => throw Unknown(slot.Type),
        };

        /// <summary>What a message calls the value of the slot, as the reader in JavaScript does: "a number", "one of A|B", "like [expression]", "A or B" for a Match of only words.</summary>
        public static string Describe(Element slot) => slot.Type switch
        {
            SlotType.Text => "a text",
            SlotType.Number => "a number",
            SlotType.UInt => "a uint",
            SlotType.Enum => "one of " + slot.Arg,
            SlotType.Match => OnlyWords(slot.Arg) ? slot.Arg.Replace("|", " or ") : "like " + slot.Arg,
            _ => throw Unknown(slot.Type),
        };

        private static NotSupportedException Unknown(SlotType type) =>
            new NotSupportedException("The reader does not know the slot type " + type + ": teach SlotCheck about it before the table uses it.");

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        // The end of the run of digits that starts at `from`.
        private static int SkipDigits(string s, int from)
        {
            var i = from;
            while (i < s.Length && IsDigit(s[i])) i++;
            return i;
        }

        private static bool IsUInt(string raw) => raw.Length > 0 && SkipDigits(raw, 0) == raw.Length;

        // -?[0-9]+(\.[0-9]+)?
        private static bool IsNumber(string raw)
        {
            var whole = raw.Length > 0 && raw[0] == '-' ? 1 : 0;
            var point = SkipDigits(raw, whole);
            if (point == whole) return false;
            if (point == raw.Length) return true;
            var end = raw[point] == '.' ? SkipDigits(raw, point + 1) : -1;
            return end > point + 1 && end == raw.Length;
        }

        // The token is one of the words of the list, which are separated by |. Without a copy: this runs for every token of every line.
        private static bool IsWordOf(string list, string raw)
        {
            for (var start = 0; start <= list.Length;)
            {
                var end = list.IndexOf('|', start);
                if (end < 0) end = list.Length;
                if (end - start == raw.Length && string.CompareOrdinal(list, start, raw, 0, raw.Length) == 0) return true;
                start = end + 1;
            }
            return false;
        }

        // ECMAScript mode, so that \d, \s and \w read as they do in JavaScript. $ of .NET also matches before a final line feed, JavaScript's does not,
        // so the match must be as long as the token.
        private static bool Reads(string expression, string raw)
        {
            var regex = Expressions.GetOrAdd(expression, e => new Regex("^(?:" + e + ")$", RegexOptions.ECMAScript, MatchTimeout));
            try
            {
                var match = regex.Match(raw);
                return match.Success && match.Length == raw.Length;
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        // A Match whose expression is only words (FROM|ON) is called by them, as matchWords of types.ts does.
        private static bool OnlyWords(string expression)
        {
            foreach (var word in expression.Split('|'))
            {
                if (word.Length == 0 || IsDigit(word[0])) return false;
                foreach (var c in word)
                    if (!(IsDigit(c) || c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))) return false;
            }
            return true;
        }
    }
}
