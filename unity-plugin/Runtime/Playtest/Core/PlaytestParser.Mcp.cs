using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Playtest.Core
{
    // C01 — `MCP <cmd> key=value... [INTO $result]` syntax + JSON assembler.
    // Pure BCL-only: no UnityEngine/UnityEditor/CommandRegistry/PlaytestMcpPolicy
    // (Editor-only C02 territory), so Wave D (D06) can relocate this unchanged.
    public static partial class PlaytestParser
    {
        private const string IntoKeyword = "INTO";

        // No leading zeros, no NaN/Infinity/hex — everything else falls through
        // to the quoted-string bucket below.
        private static readonly Regex _JsonNumberRegex = new Regex(
            @"^-?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?$", RegexOptions.Compiled);

        // Parses "MCP <cmd> key=value... [INTO $name]" from the raw,
        // already sigil-expanded, trimmed DSL line (starts with "MCP").
        internal static void ParseMcpStep(PlaytestStep step, string line)
        {
            int sp = line.IndexOf(' ');
            var tail = sp >= 0 ? line.Substring(sp + 1) : "";
            var mcpTokens = TokenizeMcpTail(tail);
            if (mcpTokens.Count == 0)
                throw new ArgumentException("MCP syntax: MCP <command> [key=value ...] [INTO $result]");

            step.Type = StepType.Mcp;
            step.Method = mcpTokens[0];

            var pairs = new List<(string key, string jsonValue)>();
            string resultVar = null;

            for (int i = 1; i < mcpTokens.Count; i++)
            {
                var tok = mcpTokens[i];
                if (string.Equals(tok, IntoKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= mcpTokens.Count)
                        throw new ArgumentException("MCP syntax: INTO must be followed by a $name sigil");
                    if (i != mcpTokens.Count - 2)
                        throw new ArgumentException("MCP syntax: INTO $name must be the final token");
                    var dest = mcpTokens[i + 1];
                    if (dest.Length < 2 || dest[0] != '$')
                        throw new ArgumentException($"MCP syntax: INTO must be followed by a $name sigil, got '{dest}'");
                    resultVar = dest.Substring(1);
                    i++;
                    continue;
                }

                var eq = tok.IndexOf('=');
                if (eq <= 0)
                    throw new ArgumentException($"MCP syntax: expected key=value, got '{tok}'");
                var key = tok.Substring(0, eq);
                var rawValue = tok.Substring(eq + 1);
                pairs.Add((key, InferJsonLiteral(rawValue)));
            }

            step.Args = AssembleJsonObject(pairs);
            step.ResultVar = resultVar;
        }

        // Splits the MCP tail into command + "key=value"/"INTO"/"$name" tokens.
        // Top-level (depth 0) quotes delimit spaces and are stripped (re-quoted
        // by InferJsonLiteral). Inside a [ ]/{ } composite (depth>0) quotes are
        // JSON-string content and stay verbatim so {"x":1} / ["a b"] survive.
        private static List<string> TokenizeMcpTail(string tail)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            int depth = 0;
            bool inQuote = false;
            bool inJsonString = false;
            bool escapeNext = false;

            for (int i = 0; i < tail.Length; i++)
            {
                char c = tail[i];

                if (depth > 0 && inJsonString)
                {
                    sb.Append(c);
                    if (escapeNext) { escapeNext = false; continue; }
                    if (c == '\\') { escapeNext = true; continue; }
                    if (c == '"') inJsonString = false;
                    continue;
                }

                if (depth == 0)
                {
                    if (c == '\\' && i + 1 < tail.Length && (tail[i + 1] == '"' || tail[i + 1] == '['))
                    { sb.Append(tail[++i]); continue; }
                    if (c == '"') { inQuote = !inQuote; continue; } // top-level: strip delimiter
                    if (!inQuote)
                    {
                        if (c == '[' || c == '{') { depth++; sb.Append(c); continue; }
                        if (c == ' ')
                        {
                            if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                            continue;
                        }
                    }
                    sb.Append(c);
                    continue;
                }

                // depth > 0, not currently inside a JSON string
                if (c == '"') { inJsonString = true; sb.Append(c); continue; }
                if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') depth--;
                sb.Append(c);
            }

            if (depth != 0)
                throw new ArgumentException($"MCP syntax: unbalanced brackets in '{tail}'");
            if (inQuote)
                throw new ArgumentException($"MCP syntax: unterminated quote in '{tail}'");
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }

        // true/false -> bare bool; null -> bare null; strict numeric -> bare
        // number; [.../{... -> validated-and-passed-through JSON; else -> string.
        private static string InferJsonLiteral(string raw)
        {
            if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)) return "true";
            if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)) return "false";
            if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase)) return "null";
            if (_JsonNumberRegex.IsMatch(raw)) return raw;
            if (raw.Length > 0 && (raw[0] == '[' || raw[0] == '{'))
            {
                if (!IsValidJson(raw))
                    throw new ArgumentException($"MCP syntax: malformed JSON value '{raw}'");
                return raw;
            }
            return "\"" + EscapeJsonString(raw) + "\"";
        }

        private static string AssembleJsonObject(List<(string key, string jsonValue)> pairs)
        {
            if (pairs.Count == 0) return "{}";
            var sb = new StringBuilder("{");
            for (int i = 0; i < pairs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(EscapeJsonString(pairs[i].key)).Append("\":").Append(pairs[i].jsonValue);
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeJsonString(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < ' ') sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        // ── Bounded recursive-descent JSON grammar validator (BCL-only). Only
        // rejects malformed [ / { values; valid composites pass through as-is. ──
        private static bool IsValidJson(string s)
        {
            int i = 0;
            SkipWs(s, ref i);
            if (!TryParseJsonValue(s, ref i)) return false;
            SkipWs(s, ref i);
            return i == s.Length;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        }

        private static bool TryParseJsonValue(string s, ref int i)
        {
            if (i >= s.Length) return false;
            char c = s[i];
            if (c == '{') return TryParseObject(s, ref i);
            if (c == '[') return TryParseArray(s, ref i);
            if (c == '"') return TryParseString(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return TryParseNumber(s, ref i);
            if (TryMatchLiteral(s, ref i, "true")) return true;
            if (TryMatchLiteral(s, ref i, "false")) return true;
            return TryMatchLiteral(s, ref i, "null");
        }

        private static bool TryMatchLiteral(string s, ref int i, string lit)
        {
            if (i + lit.Length > s.Length) return false;
            if (string.CompareOrdinal(s, i, lit, 0, lit.Length) != 0) return false;
            i += lit.Length;
            return true;
        }

        private static bool TryParseObject(string s, ref int i)
        {
            i++; // consume {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return true; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"' || !TryParseString(s, ref i)) return false;
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') return false;
                i++;
                SkipWs(s, ref i);
                if (!TryParseJsonValue(s, ref i)) return false;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return true; }
                return false;
            }
        }

        private static bool TryParseArray(string s, ref int i)
        {
            i++; // consume [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return true; }
            while (true)
            {
                SkipWs(s, ref i);
                if (!TryParseJsonValue(s, ref i)) return false;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return true; }
                return false;
            }
        }

        private static bool TryParseString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') return false;
            i++;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"') { i++; return true; }
                if (c == '\\')
                {
                    i++;
                    if (i >= s.Length) return false;
                    i++;
                    continue;
                }
                i++;
            }
            return false; // unterminated
        }

        private static bool TryParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && s[i] == '-') i++;
            if (i >= s.Length || !char.IsDigit(s[i])) return false;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i < s.Length && s[i] == '.')
            {
                i++;
                if (i >= s.Length || !char.IsDigit(s[i])) return false;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                i++;
                if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                if (i >= s.Length || !char.IsDigit(s[i])) return false;
                while (i < s.Length && char.IsDigit(s[i])) i++;
            }
            return i > start;
        }
    }
}
