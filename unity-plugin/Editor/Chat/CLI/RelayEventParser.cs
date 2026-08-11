// Parser: text-format lines from chat_relay.py → ChatEvent.
// Pure static — no Unity deps, no allocations beyond Split.
// Each line: prefix|field1|field2|... (trailing fields may contain '|').
namespace UnityMCP.Editor.Chat
{
    internal static class RelayEventParser
    {
        /// <summary>Parse one relay event line. Returns null for unknown/empty lines.</summary>
        internal static ChatEvent? Parse(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var sep = line.IndexOf('|');
            if (sep < 0) return null;

            var prefix = line.Substring(0, sep);
            var rest   = line.Substring(sep + 1);

            switch (prefix)
            {
                case "t":  return ChatEvent.TextDelta(rest);
                case "e":  return ChatEvent.Error(rest);
                case "ar": return ChatEvent.AutoReply(rest);
                case "rl": return ChatEvent.RateLimit(rest);
                case "si": return ChatEvent.SessionInit(rest);
                case "hb": return ChatEvent.Heartbeat();
                case "ss": return ChatEvent.SessionState(rest);
                case "th": return ChatEvent.Thinking(rest);

                case "tc":
                {
                    // tc|name|toolId|argsJson  (argsJson may contain '|')
                    var p = SplitN(rest, '|', 3);
                    if (p.Length < 3) return null;
                    return ChatEvent.ToolStart(p[0], p[2], p[1]);
                }

                case "tr":
                {
                    // tr|toolId|ok|text  (text may contain '|')
                    var p = SplitN(rest, '|', 3);
                    if (p.Length < 3) return null;
                    return ChatEvent.ToolResult(p[0], p[2], p[1] == "true");
                }

                case "pp":
                {
                    // pp|toolName|requestId|toolInput  (toolInput may contain '|')
                    var p = SplitN(rest, '|', 3);
                    if (p.Length < 3) return null;
                    return ChatEvent.PermissionPrompt(p[1], p[0], p[2]);
                }

                case "au":
                {
                    // au|requestId|rawJson  (rawJson may contain '|')
                    var p = SplitN(rest, '|', 2);
                    if (p.Length < 2) return null;
                    return ChatEvent.AskUser(p[0], p[1]);
                }

                case "tp":
                {
                    // tp|pct|text
                    var p = SplitN(rest, '|', 2);
                    if (p.Length < 2) return null;
                    float.TryParse(p[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct);
                    return ChatEvent.ToolProgress(pct, p[1]);
                }

                case "d":
                {
                    // d|sessionId|cost|inTok|outTok
                    var p = SplitN(rest, '|', 4);
                    if (p.Length < 4) return null;
                    float.TryParse(p[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var cost);
                    int.TryParse(p[2], out var inTok);
                    int.TryParse(p[3], out var outTok);
                    return ChatEvent.TurnDone(p[0], cost, inTok, outTok);
                }

                default: return null;
            }
        }

        // Split s on sep, returning at most maxParts parts.
        // Last part contains the remainder (may contain sep).
        private static string[] SplitN(string s, char sep, int maxParts)
        {
            var parts = new System.Collections.Generic.List<string>(maxParts);
            int start = 0;
            while (parts.Count < maxParts - 1)
            {
                var idx = s.IndexOf(sep, start);
                if (idx < 0) break;
                parts.Add(s.Substring(start, idx - start));
                start = idx + 1;
            }
            parts.Add(s.Substring(start));
            return parts.ToArray();
        }
    }
}
