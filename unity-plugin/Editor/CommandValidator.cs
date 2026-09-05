using System;
using System.Collections.Generic;
using System.Text;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure validation functions over CommandRegistry's per-command contract
    /// (Entry.Required/Optional). Replaces the old hand-maintained CommandSchema
    /// dictionary — the contract now lives at the Register() call site, next to
    /// the handler, so there is one source of truth instead of two.
    /// </summary>
    internal static class CommandValidator
    {
        /// <summary>Validate command + args. Returns null if valid, error message if not.</summary>
        public static string Validate(string cmd, string argsJson)
        {
            if (!CommandRegistry.TryGetContract(cmd, out var required, out var optional, out var isFreeForm))
            {
                var best = StringDistance.ClosestMatch(cmd, CommandRegistry.GetAllCommands());
                return best != null
                    ? $"Unknown command '{cmd}'. Did you mean '{best}'?"
                    : $"Unknown command '{cmd}'.";
            }

            if (isFreeForm) return null; // Required==null on the entry — no contract, no validation

            var keys = ExtractKeys(argsJson);

            var missing = new List<string>(2);
            foreach (var req in required)
                if (!keys.Contains(req))
                    missing.Add(req);

            var valid = new HashSet<string>(required);
            valid.UnionWith(optional);

            var unknownSigils = new List<string>(2);
            bool hasUnknown = false;
            foreach (var key in keys)
            {
                if (valid.Contains(key)) continue;
                hasUnknown = true;
                var closest = StringDistance.ClosestMatch(key, valid);
                unknownSigils.Add(closest != null ? $"?{key}→{closest}" : $"?{key}");
            }

            if (missing.Count == 0 && !hasUnknown) return null;

            var sb = new StringBuilder(cmd);
            foreach (var m in missing) sb.Append(" !").Append(m);
            foreach (var u in unknownSigils) sb.Append(' ').Append(u);
            if (hasUnknown) sb.Append(" Unknown param.");
            sb.Append("\n  ").Append(AutoUsage(cmd, required, optional));
            return sb.ToString();
        }

        /// <summary>Computed usage string, e.g. "get_component path=... type=... [scene=...]". Never hand-written.
        /// Optional params are capped at 5 (+N more) to bound AutoUsage token cost for high-arity
        /// commands (e.g. shader has 18 optional params — Issue 23 review M5).</summary>
        internal static string AutoUsage(string cmd, string[] required, string[] optional)
        {
            var sb = new StringBuilder(cmd);
            if (required != null)
                foreach (var r in required) sb.Append(' ').Append(r).Append("=...");
            if (optional != null)
            {
                int show = Math.Min(optional.Length, 5);
                for (int i = 0; i < show; i++)
                    sb.Append(" [").Append(optional[i]).Append("=...]");
                if (optional.Length > 5)
                    sb.Append(" [+").Append(optional.Length - 5).Append(" more]");
            }
            return sb.ToString();
        }

        /// <summary>Extract top-level keys from flat JSON object.</summary>
        internal static List<string> ExtractKeys(string json)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(json) || json == "{}") return keys;
            int i = 0;
            while (i < json.Length)
            {
                var q1 = json.IndexOf('"', i);
                if (q1 == -1) break;
                var q2 = json.IndexOf('"', q1 + 1);
                if (q2 == -1) break;
                keys.Add(json.Substring(q1 + 1, q2 - q1 - 1));
                var colon = json.IndexOf(':', q2);
                if (colon == -1) break;
                i = colon + 1;
                while (i < json.Length && json[i] == ' ') i++;
                if (i < json.Length && json[i] == '"')
                {
                    i++;
                    while (i < json.Length)
                    {
                        if (json[i] == '"')
                        {
                            int backslashes = 0;
                            int b = i - 1;
                            while (b >= 0 && json[b] == '\\') { backslashes++; b--; }
                            if (backslashes % 2 == 0) break;
                        }
                        i++;
                    }
                    if (i < json.Length) i++;
                }
                else
                {
                    while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                }
                if (i < json.Length && json[i] == ',') i++;
            }
            return keys;
        }
    }
}
