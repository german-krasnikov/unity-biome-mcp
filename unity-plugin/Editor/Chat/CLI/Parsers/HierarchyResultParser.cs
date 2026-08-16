// Parser for get_hierarchy tool results.
// Pure C#, no Unity deps — lives in noEngineReferences Parsers assembly.
// Format: HierarchySerializer.Serialize() text-tree output.
// Depth = number of 3-char ancestor groups (│   or   ) before the branch connector.
// REAL FORMAT (from HierarchySerializer.AppendIndent):
//   Root: "Name &Base62Ref [flags]"  (no connector)
//   Depth 1: "│  └─ Name &Base62Ref" or "   └─ Name &Base62Ref" (3-char group + connector)
//   Depth N: N groups + connector
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal struct HierarchyNode
    {
        internal string Name;          // display name (trimmed)
        internal string Reference;     // canonical "&base62" reference, passed to NavTarget
        internal int    Depth;         // 0 = root, 1 = child of root, etc.
        internal bool   IsInactive;    // has " !" suffix
        internal int    HiddenCount;   // N from " +N" suffix; 0 if none
        internal bool   IsSceneHeader; // true for "[SceneName]" lines
        internal string SceneName;     // non-null when IsSceneHeader
        internal string Components;    // "Comp1,Comp2" or null
    }

    internal static class HierarchyResultParser
    {
        private const string TruncationPrefix = "... truncated at";

        // Never throws. Returns [] on empty / NO_CHANGE / error.
        internal static HierarchyNode[] Parse(string resultText, bool parseComponents = false)
        {
            if (string.IsNullOrEmpty(resultText) || resultText == "NO_CHANGE")
                return Array.Empty<HierarchyNode>();

            var lines = resultText.Split('\n');
            var nodes = new List<HierarchyNode>(lines.Length);

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith(TruncationPrefix, StringComparison.Ordinal)) continue;

                if (TryParseSceneHeader(line, out var header))
                {
                    nodes.Add(header);
                    continue;
                }

                int depth = FindDepth(line, out int contentStart);
                if (contentStart > line.Length) continue;

                var content = line.Substring(contentStart);
                if (string.IsNullOrEmpty(content)) continue;

                if (TryParseNode(content, depth, parseComponents, out var node))
                    nodes.Add(node);
            }

            return nodes.ToArray();
        }

        // A serializer scene header is the entire line. A bracket-named object still has
        // its trailing reference (for example "[Gameplay] &1") and is parsed as a node.
        private static bool TryParseSceneHeader(string line, out HierarchyNode node)
        {
            node = default;
            if (line.Length < 3 || line[0] != '[' || line[line.Length - 1] != ']') return false;
            node = new HierarchyNode
            {
                IsSceneHeader = true,
                SceneName     = line.Substring(1, line.Length - 2),
                Reference     = ""
            };
            return true;
        }

        // Counts leading 3-char ancestor groups (│   or   ), then looks for a connector.
        // Returns ancestor-group count as depth; contentStart points past the connector.
        // Root objects (no connector): depth=0, contentStart=0.
        private static int FindDepth(string line, out int contentStart)
        {
            int pos = 0, groups = 0, len = line.Length;

            while (pos + 2 < len)
            {
                char c0 = line[pos], c1 = line[pos + 1], c2 = line[pos + 2];

                if (c0 == '│' && c1 == ' ' && c2 == ' ')           // ancestor not-last
                    { groups++; pos += 3; }
                else if (c0 == ' ' && c1 == ' ' && c2 == ' ')      // ancestor was-last
                    { groups++; pos += 3; }
                else if ((c0 == '└' || c0 == '├') && c1 == '─' && c2 == ' ')
                    { contentStart = pos + 3; return groups; }      // depth = ancestor groups
                else
                    break;
            }

            contentStart = 0;  // root: no connector
            return 0;
        }

        // Parses "Name [Comp1,Comp2] &base62 ! +N" → HierarchyNode.
        // v1.32-format result text used the immediately preceding $HEX shape, so that
        // exact transient-ID form remains readable. Other '$' tokens are rejected.
        private static bool TryParseNode(
            string content, int depth, bool parseComponents, out HierarchyNode node)
        {
            node = default;

            int end = content.Length;
            int hidden = 0;
            bool inactive = false;

            // Serializer order is reference, inactive marker, hidden count. Peel those
            // exact trailing tokens before locating the final reference token.
            if (TryReadLastToken(content, end, out int tokenStart, out var token) &&
                token.Length > 1 && token[0] == '+' &&
                int.TryParse(token.Substring(1), NumberStyles.None,
                    CultureInfo.InvariantCulture, out hidden))
            {
                end = tokenStart - 1;
            }

            if (TryReadLastToken(content, end, out tokenStart, out token) && token == "!")
            {
                inactive = true;
                end = tokenStart - 1;
            }

            if (!TryReadLastToken(content, end, out int referenceStart, out var reference) ||
                (!IsCanonicalReference(reference) && !IsLegacyHexReference(reference)))
                return false;

            var nameSection = content.Substring(0, referenceStart - 1);
            string name, components = null;

            int compStart = parseComponents ? nameSection.LastIndexOf(" [") : -1;
            if (compStart >= 0)
            {
                int compEnd = nameSection.LastIndexOf(']');
                if (compEnd == nameSection.Length - 1)
                {
                    components = nameSection.Substring(compStart + 2, compEnd - compStart - 2);
                    name       = nameSection.Substring(0, compStart).Trim();
                }
                else
                {
                    name = nameSection.Trim();
                }
            }
            else
            {
                name = nameSection.Trim();
            }

            node = new HierarchyNode
            {
                Name        = name,
                Reference   = reference,
                Depth       = depth,
                IsInactive  = inactive,
                HiddenCount = hidden,
                Components  = components
            };
            return true;
        }

        private static bool TryReadLastToken(
            string content, int end, out int tokenStart, out string token)
        {
            tokenStart = -1;
            token = null;
            if (end <= 0 || end > content.Length) return false;

            int separator = content.LastIndexOf(' ', end - 1, end);
            if (separator < 0 || separator == end - 1) return false;

            tokenStart = separator + 1;
            token = content.Substring(tokenStart, end - tokenStart);
            return true;
        }

        private static bool IsCanonicalReference(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '&')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= 'a' && c <= 'z')))
                    return false;
            }
            return true;
        }

        private static bool IsLegacyHexReference(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '$')
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'A' && c <= 'F') ||
                      (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }
    }
}
