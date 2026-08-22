// UIFileHelper — USS surgical edit methods (partial class).
// Loaded by EditUIFile dispatch: set-rule, remove-rule.
// Uses line-scan (no CSS parser) per PHASE3 algorithm spec.
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static partial class UIFileHelper
    {
        // set-rule: find selector block by line-scan, update/insert property value.
        // Creates a new rule block at EOF if selector is absent.
        internal static string UssSetRule(string abs, string path, string selector, string prop, string value)
        {
            var lines = new List<string>(File.ReadAllLines(abs, JsonHelper.Utf8NoBom));
            bool inBlock = false; int depth = 0, blockStart = -1; bool foundProp = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (!inBlock)
                {
                    if (t == selector + " {" || t.StartsWith(selector + " {"))
                    { inBlock = true; depth = 1; blockStart = i; continue; }
                }
                else
                {
                    depth += CountChar(lines[i], '{') - CountChar(lines[i], '}');
                    if (depth == 0)
                    {
                        if (!foundProp) lines.Insert(i, $"    {prop}: {value};");
                        break;
                    }
                    if (t.StartsWith(prop + ":"))
                    {
                        var existing = t.Substring(prop.Length + 1).Trim().TrimEnd(';');
                        if (existing == value) return $"no-op: property already has value \"{value}\"";
                        lines[i] = $"    {prop}: {value};";
                        foundProp = true;
                    }
                }
            }

            if (blockStart == -1)
            {
                lines.Add(""); lines.Add($"{selector} {{");
                lines.Add($"    {prop}: {value};"); lines.Add("}");
            }

            BackupFile(abs);
            File.WriteAllText(abs, string.Join("\n", lines), JsonHelper.Utf8NoBom);
            _importAsset(path, ImportAssetOptions.Default);
            return $"ok: set-rule selector={selector} prop={prop} value={value}\npath: {path}";
        }

        // remove-rule: find and remove an entire rule block by selector line-scan.
        internal static string UssRemoveRule(string abs, string path, string selector)
        {
            var lines = new List<string>(File.ReadAllLines(abs, JsonHelper.Utf8NoBom));
            int blockStart = -1, depth = 0, blockEnd = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                if (blockStart == -1 && (t == selector + " {" || t.StartsWith(selector + " {")))
                { blockStart = i; depth = 1; }
                else if (blockStart != -1)
                {
                    depth += CountChar(lines[i], '{') - CountChar(lines[i], '}');
                    if (depth == 0) { blockEnd = i; break; }
                }
            }

            if (blockStart == -1) return $"no-op: no rule with selector '{selector}' found";

            BackupFile(abs);
            lines.RemoveRange(blockStart, blockEnd - blockStart + 1);
            File.WriteAllText(abs, string.Join("\n", lines), JsonHelper.Utf8NoBom);
            _importAsset(path, ImportAssetOptions.Default);
            return $"ok: remove-rule selector={selector}\npath: {path}";
        }

        private static int CountChar(string s, char c)
        {
            int count = 0;
            foreach (var ch in s) if (ch == c) count++;
            return count;
        }
    }
}
