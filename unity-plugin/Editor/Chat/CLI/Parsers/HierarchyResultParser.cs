// Parser for get_hierarchy tool results.
// Pure C#, no Unity deps — lives in noEngineReferences Parsers assembly.
// Format: HierarchySerializer.Serialize() text-tree output.
// Depth = number of 3-char ancestor groups (│   or   ) before the branch connector.
// REAL FORMAT (from HierarchySerializer.AppendIndent):
//   Root: "Name $HexRef [flags]"  (no connector)
//   Depth 1: "│  └─ Name $HexRef" or "   └─ Name $HexRef" (3-char group + connector)
//   Depth N: N groups + connector
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Parsers
{
    internal struct HierarchyNode
    {
        internal string Name;          // display name (trimmed)
        internal string HexRef;        // "$A1B2" format, passed to NavTarget
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
        internal static HierarchyNode[] Parse(string resultText)
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

                if (TryParseNode(content, depth, out var node))
                    nodes.Add(node);
            }

            return nodes.ToArray();
        }

        // Scene header: starts with '[', ends with ']', no '$' (not a component list).
        private static bool TryParseSceneHeader(string line, out HierarchyNode node)
        {
            node = default;
            if (line.Length < 3 || line[0] != '[' || line[line.Length - 1] != ']') return false;
            if (line.IndexOf('$') >= 0) return false;
            node = new HierarchyNode
            {
                IsSceneHeader = true,
                SceneName     = line.Substring(1, line.Length - 2),
                HexRef        = ""
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

        // Parses "Name [Comp1,Comp2] $HexRef ! +N" → HierarchyNode.
        // Returns false for truncated / invalid lines (no hex ref).
        private static bool TryParseNode(string content, int depth, out HierarchyNode node)
        {
            node = default;

            int hexSep = content.LastIndexOf(" $");
            if (hexSep < 0) return false;   // truncated or malformed line → skip

            // Split: everything before " $" is name+components; after " $" is "$HexRef [flags]"
            var afterHexSep = content.Substring(hexSep + 1);   // "$HexRef ! +N"
            int spaceAfterHex = afterHexSep.IndexOf(' ', 1);
            string hexRef, flags;
            if (spaceAfterHex < 0)
            {
                hexRef = afterHexSep;
                flags  = "";
            }
            else
            {
                hexRef = afterHexSep.Substring(0, spaceAfterHex);
                flags  = afterHexSep.Substring(spaceAfterHex);
            }

            bool inactive = flags.Contains(" !");
            int  hidden   = 0;
            int  plusIdx  = flags.IndexOf(" +", StringComparison.Ordinal);
            if (plusIdx >= 0)
            {
                var numStr = flags.Substring(plusIdx + 2);
                int sp = numStr.IndexOf(' ');
                if (sp > 0) numStr = numStr.Substring(0, sp);
                int.TryParse(numStr, out hidden);
            }

            var nameSection = content.Substring(0, hexSep);
            string name, components = null;

            int compStart = nameSection.LastIndexOf(" [");
            if (compStart >= 0)
            {
                int compEnd = nameSection.LastIndexOf(']');
                if (compEnd > compStart)
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
                HexRef      = hexRef,
                Depth       = depth,
                IsInactive  = inactive,
                HiddenCount = hidden,
                Components  = components
            };
            return true;
        }
    }
}
