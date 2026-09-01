// Writes/merges unity-biome-mcp entry into external AI tool config files.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Wizard
{
    public static class WizardConfigWriter
    {
        internal static void Write(string toolName, string configPath, int port)
        {
            try
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string merged;
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, configPath + ".bak", overwrite: true);
                    var existing = File.ReadAllText(configPath, Encoding.UTF8);
                    merged = Merge(existing, port);
                }
                else
                {
                    merged = Fresh(port);
                }

                File.WriteAllText(configPath, merged, new UTF8Encoding(false));
                UnityEditor.EditorUtility.DisplayDialog(
                    $"{toolName} — Config Written",
                    $"unity-biome-mcp added to:\n{configPath}", "OK");
            }
            catch (Exception ex)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Write Failed", $"Could not write config:\n{ex.Message}", "OK");
            }
        }

        internal static string Fresh(int port) => Fresh(port, GitInstallUrl, "mcpServers");

        internal static string Fresh(int port, string gitUrl, string rootKey) =>
            FreshWithEntry(Entry(port, gitUrl), rootKey);

        internal static string Merge(string existing, int port) =>
            Merge(existing, port, GitInstallUrl, "mcpServers");

        internal static string Merge(string existing, int port, string gitUrl, string rootKey) =>
            MergeWithEntry(existing, Entry(port, gitUrl), rootKey);

        // Shared brace-counting merge, parameterized on a pre-built entry string — reused by
        // ProjectConfigFormats with a version-marker-aware entry (BuildEntry) instead of the
        // plain one, so both writers share one merge algorithm (DRY).
        internal static string MergeWithEntry(string existing, string entry, string rootKey)
        {
            // Replace our existing entry (new name first, then the OLD "unity-mcp" name
            // so a prior install is migrated — key AND value swapped for the fresh entry,
            // never left as a duplicate second server).
            foreach (var key in new[] { PermissionConfig.SERVER_NAME, "unity-mcp" })
            {
                if (!FindEntryBounds(existing, key, out var bStart, out var bEnd)) continue;
                var keyStart = existing.LastIndexOf("\"" + key + "\"", bStart, StringComparison.Ordinal);
                if (keyStart < 0) continue;
                // Point-splice (ARC-13 T1): patch only command/args/_v in place, keep
                // every other key (env, cwd, ...) byte-identical — never whole-replace.
                var oldSpan = existing.Substring(bStart, bEnd - bStart);
                var patchedSpan = PatchEntryFields(oldSpan, entry);
                return existing.Substring(0, keyStart)
                     + "\"" + PermissionConfig.SERVER_NAME + "\": " + patchedSpan
                     + existing.Substring(bEnd);
            }

            if (existing.Contains("\"" + rootKey + "\""))
            {
                var idx      = existing.IndexOf("\"" + rootKey + "\"", StringComparison.Ordinal);
                var braceIdx = existing.IndexOf('{', idx + rootKey.Length + 2);
                if (braceIdx < 0) return FreshWithEntry(entry, rootKey);
                var after = existing.Substring(braceIdx + 1).TrimStart();
                var sep   = after.StartsWith("}") ? "" : ",";
                return existing.Substring(0, braceIdx + 1)
                     + "\n    " + entry + sep
                     + existing.Substring(braceIdx + 1);
            }

            var lastBrace = existing.LastIndexOf('}');
            if (lastBrace >= 0)
            {
                var comma = existing.Substring(0, lastBrace).TrimEnd().EndsWith("{") ? "" : ",";
                return existing.Substring(0, lastBrace)
                     + comma
                     + "\n  \"" + rootKey + "\": {\n    " + entry + "\n  }\n}";
            }

            return FreshWithEntry(entry, rootKey);
        }

        internal static string FreshWithEntry(string entry, string rootKey) =>
            "{\n" +
            "  \"" + rootKey + "\": {\n" +
            "    " + entry + "\n" +
            "  }\n" +
            "}\n";

        public const string GitInstallUrl =
            "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server";

        /// <summary>
        /// Returns a uvx --from URL pinned to a specific git tag.
        /// ref = "0.54.1" or "v0.54.1" — both accepted.
        /// Returns the unpinned default URL when ref is null/empty.
        /// </summary>
        public static string GitInstallUrlFor(string @ref)
        {
            if (string.IsNullOrEmpty(@ref)) return GitInstallUrl;
            var clean = @ref.TrimStart('v');
            var parts = clean.Split('.');
            if (parts.Length != 3 || !AllDigits(parts))
                throw new ArgumentException($"Invalid version ref: {@ref}");
            const string RepoBase = "git+https://github.com/german-krasnikov/unity-biome-mcp.git";
            return $"{RepoBase}@v{clean}#subdirectory=server";
        }

        private static bool AllDigits(string[] arr)
        {
            foreach (var p in arr)
                if (!int.TryParse(p, out _)) return false;
            return true;
        }

        internal static string Entry(int port) => Entry(port, GitInstallUrl);

        // Note: UNITY_MCP_PORT intentionally not written. Python uses ~/.unity-biome-mcp/ports/{pid}.port
        // discovery which is updated on every bind (including fallbacks). Baking a port here blocks
        // discovery after Windows port drift and breaks multi-project setups.
        internal static string Entry(int port, string gitUrl) =>
            $"\"{PermissionConfig.SERVER_NAME}\": {{\n" +              // server name (config key)
            "      \"command\": \"uvx\",\n" +
            $"      \"args\": [\"--from\", \"{gitUrl}\", \"unity-biome-mcp\"]\n" +   // 'unity-biome-mcp' = PyPI package
            "    }";

        // ── Backup / Restore ──────────────────────────────────────────────────

        internal static bool HasBackup(string configPath)
            => File.Exists(configPath + ".bak");

        /// <summary>Restores config from .bak. Returns false if no .bak exists.</summary>
        internal static bool RestoreConfig(string configPath)
        {
            var bak = configPath + ".bak";
            if (!File.Exists(bak)) return false;
            try
            {
                File.Copy(bak, configPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                UnityEditor.EditorUtility.DisplayDialog("Restore Failed", ex.Message, "OK");
                return false;
            }
        }

        private static string ReplaceEntry(string json, string key, string newValue)
        {
            if (!FindEntryBounds(json, key, out var start, out var end)) return null;
            return json.Substring(0, start) + newValue + json.Substring(end);
        }

        // Locates the brace-delimited object value for "key": { ... } in a JSON string —
        // shared by ReplaceEntry (merge) and ProjectConfigFormats (marker-regex scoping)
        // so both consumers agree on where one entry ends and the next sibling begins.
        // Returns false if key absent or braces unterminated (malformed JSON) — callers
        // treat that as "can't safely touch it".
        internal static bool FindEntryBounds(string json, string key, out int start, out int end)
        {
            start = end = -1;
            if (string.IsNullOrEmpty(json)) return false;
            var keyIdx = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyIdx < 0) return false;
            var braceStart = json.IndexOf('{', keyIdx + key.Length + 2);
            if (braceStart < 0) return false;
            int depth = 1, pos = braceStart + 1;
            while (pos < json.Length && depth > 0)
            {
                if (json[pos] == '{') depth++;
                else if (json[pos] == '}') depth--;
                pos++;
            }
            if (depth != 0) return false;
            start = braceStart;
            end = pos;
            return true;
        }

        // ARC-13 T1: point-splice the 3 fields the wizard template writes — command,
        // args, and (when the call site's template has one) _v — into an existing
        // entry span, leaving every other key byte-identical. A field missing from
        // `oldSpan` is left untouched here; inserting a missing field is ARC-13 T2,
        // not this task.
        private static readonly string[] PatchedFields = { "command", "args", "_v" };

        private static string PatchEntryFields(string oldSpan, string freshEntry)
        {
            foreach (var field in PatchedFields)
            {
                if (!FindFieldSegment(freshEntry, field, out var freshStart, out var freshEnd))
                    continue; // this call site's template doesn't write this field (e.g. bare Merge has no "_v")

                if (!FindFieldSegment(oldSpan, field, out var oldStart, out var oldEnd))
                    continue; // absent from the old entry — insert-if-absent is ARC-13 T2

                var freshSegment = freshEntry.Substring(freshStart, freshEnd - freshStart);
                oldSpan = oldSpan.Substring(0, oldStart) + freshSegment + oldSpan.Substring(oldEnd);
            }
            return oldSpan;
        }

        // Locates the full `"field": value` segment — from the opening quote of the
        // key to the end of its value — for the two value shapes the wizard template
        // ever writes: a quoted string (command, _v) or a flat string array (args).
        // Callers splice a replacement segment into this span; everything else
        // (surrounding commas, unrelated keys) is untouched.
        private static bool FindFieldSegment(string text, string field, out int start, out int end)
        {
            start = end = -1;
            var m = Regex.Match(text, "\"" + Regex.Escape(field) + "\"\\s*:\\s*");
            if (!m.Success) return false;
            var valueStart = m.Index + m.Length;
            if (valueStart >= text.Length) return false;

            if (text[valueStart] == '"')
            {
                var close = text.IndexOf('"', valueStart + 1);
                if (close < 0) return false;
                start = m.Index;
                end = close + 1;
                return true;
            }

            if (text[valueStart] == '[')
            {
                int depth = 1, pos = valueStart + 1;
                while (pos < text.Length && depth > 0)
                {
                    if (text[pos] == '[') depth++;
                    else if (text[pos] == ']') depth--;
                    pos++;
                }
                if (depth != 0) return false;
                start = m.Index;
                end = pos;
                return true;
            }

            return false; // unsupported value shape — leave untouched rather than guess
        }
    }
}
