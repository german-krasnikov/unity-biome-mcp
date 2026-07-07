using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class NlStepParser
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const string VerbAlt = @"move|go|walk|navigate|teleport|tp|warp|wait|pause|delay|sleep|assert|check|verify|expect|ensure|invoke|call|fire|trigger|log|print|say|section|monitor";
        static readonly string[] Ops = { ">=", "<=", "!=", ">", "<", "==" };

        public static string ConvertToDsl(string nlText)
        {
            if (string.IsNullOrWhiteSpace(nlText)) return "";
            var fragments = SplitFragments(nlText.Trim());
            var lines = new List<string>();
            foreach (var f in fragments)
            {
                var dsl = FragmentToDsl(f.Trim());
                if (!string.IsNullOrEmpty(dsl)) lines.Add(dsl);
            }
            return string.Join("\n", lines);
        }

        internal static string[] SplitFragments(string nl)
        {
            var result = new List<string>();
            foreach (var thenPart in Regex.Split(nl, @"\s+then\s+", RegexOptions.IgnoreCase))
            {
                var conjParts = Regex.Split(thenPart,
                    $@"(?:,\s+|\s+and\s+)(?=(?:{VerbAlt})\b)", RegexOptions.IgnoreCase);
                foreach (var cp in conjParts)
                    if (!string.IsNullOrWhiteSpace(cp)) result.Add(cp);
            }
            return result.ToArray();
        }

        internal static string FragmentToDsl(string fragment)
        {
            if (string.IsNullOrWhiteSpace(fragment)) return "";
            var words = fragment.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var lower = fragment.ToLowerInvariant();
            var verb  = words[0].ToLowerInvariant();

            if (lower.Contains("console") && lower.Contains("clean"))
                return "ASSERT_CONSOLE_CLEAN";

            if (Regex.IsMatch(lower, @"^wait\s+(until|for)\b"))
            {
                var parsed = ParseQueryOpValue(words, 2);
                if (parsed == null) return $"LOG # UNPARSED: {fragment}";
                var timeout = FindTimeout(words) ?? "5";
                return $"WAIT_UNTIL {parsed} TIMEOUT {timeout}";
            }

            if (lower.Contains("timescale") || (lower.Contains("time") && lower.Contains("scale")))
            {
                var m = Regex.Match(fragment, @"(\d+(?:\.\d+)?)");
                return m.Success ? $"TIMESCALE {m.Groups[1].Value}" : $"LOG # UNPARSED: {fragment}";
            }

            if (MatchVerb(verb, "wait", "pause", "delay", "sleep"))
            {
                var n = ExtractSeconds(words, 1);
                return n.HasValue ? $"WAIT {F(n.Value)}" : $"LOG # UNPARSED: {fragment}";
            }

            if (MatchVerb(verb, "teleport", "tp", "warp"))
            {
                int toIdx = Array.FindIndex(words, 1, w => w.Equals("to", StringComparison.OrdinalIgnoreCase));
                string pathWord = words.Length > 1 ? words[1] : null;
                string posWord  = toIdx > 1 && toIdx + 1 < words.Length ? words[toIdx + 1]
                                : words.Length > 2 ? words[2] : null;
                if (posWord != null && TryParsePosition(posWord, out var tp))
                    return $"TELEPORT {NormalizePath(pathWord)} {FormatVec(tp)}";
                return $"LOG # UNPARSED: {fragment}";
            }

            if (MatchVerb(verb, "move", "go", "walk", "navigate"))
            {
                int toIdx = Array.FindIndex(words, 1, w => w.Equals("to", StringComparison.OrdinalIgnoreCase));
                if (toIdx < 0 || toIdx + 1 >= words.Length) return $"LOG # UNPARSED: {fragment}";
                if (!TryParsePosition(words[toIdx + 1], out var pos)) return $"LOG # UNPARSED: {fragment}";
                var path = toIdx > 1 ? NormalizePath(words[1]) : null;
                return path != null ? $"MOVE {path} TO {FormatVec(pos)}" : $"MOVE TO {FormatVec(pos)}";
            }

            if (MatchVerb(verb, "invoke", "call", "fire", "trigger"))
            {
                if (words.Length < 2) return $"LOG # UNPARSED: {fragment}";
                var parts = words[1].Split('.');
                if (parts.Length >= 3) return $"INVOKE {NormalizePath(parts[0])} {parts[1]} {parts[2]}";
                if (parts.Length == 2) return $"INVOKE {NormalizePath(parts[0])} {parts[0]} {parts[1]}";
                return $"LOG # UNPARSED: {fragment}";
            }

            if (MatchVerb(verb, "assert", "check", "verify", "expect", "ensure"))
            {
                var parsed = ParseQueryOpValue(words, 1);
                return parsed != null ? $"ASSERT {parsed}" : $"LOG # UNPARSED: {fragment}";
            }

            if (MatchVerb(verb, "section"))
                return $"SECTION \"{string.Join(" ", words, 1, words.Length - 1)}\"";

            if (MatchVerb(verb, "log", "print", "say"))
                return $"LOG {string.Join(" ", words, 1, words.Length - 1)}";

            if (MatchVerb(verb, "monitor"))
                return $"MONITOR {string.Join(" ", words, 1, words.Length - 1)}";

            return $"LOG # UNPARSED: {fragment}";
        }

        internal static bool TryParsePosition(string s, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Equals("origin", StringComparison.OrdinalIgnoreCase)) return true;
            s = s.Trim('(', ')').Replace(" ", "");
            var parts = s.Split(',');
            if (parts.Length != 3) return false;
            if (float.TryParse(parts[0], NumberStyles.Float, Inv, out var x) &&
                float.TryParse(parts[1], NumberStyles.Float, Inv, out var y) &&
                float.TryParse(parts[2], NumberStyles.Float, Inv, out var z))
            {
                v = new Vector3(x, y, z); return true;
            }
            return false;
        }

        internal static string NormalizePath(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.StartsWith("/") ? name : "/" + name;
        }

        static string ParseQueryOpValue(string[] words, int start)
        {
            if (start + 2 >= words.Length) return null;
            var op = words[start + 1];
            if (!IsOp(op)) return null;
            return $"{words[start]} {op} {words[start + 2]}";
        }

        static bool IsOp(string s) { foreach (var op in Ops) if (s == op) return true; return false; }

        static float? ExtractSeconds(string[] words, int from)
        {
            for (int i = from; i < words.Length; i++)
            {
                var w = words[i];
                if (w.EndsWith("ms", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(w.Substring(0, w.Length - 2), NumberStyles.Float, Inv, out var ms))
                    return ms / 1000f;
                var stripped = w.TrimEnd('s', 'S');
                if (stripped.Length < w.Length && float.TryParse(stripped, NumberStyles.Float, Inv, out var sv))
                    return sv;
                if (float.TryParse(w, NumberStyles.Float, Inv, out var fv)) return fv;
            }
            return null;
        }

        static string FindTimeout(string[] words)
        {
            for (int i = 0; i + 1 < words.Length; i++)
                if (words[i].Equals("timeout", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(words[i + 1], NumberStyles.Float, Inv, out var t))
                    return F(t);
            return null;
        }

        static bool MatchVerb(string word, params string[] verbs)
        {
            foreach (var v in verbs) if (word.Equals(v, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string FormatVec(Vector3 v) => $"{F(v.x)},{F(v.y)},{F(v.z)}";
        static string F(float v) => v.ToString("G", Inv);
    }
}
