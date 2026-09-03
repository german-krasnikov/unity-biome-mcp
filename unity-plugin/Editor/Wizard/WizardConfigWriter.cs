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

                WriteAtomic(configPath, merged);
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

        // B3 #21: atomic write shared by every config writer (manual Configure here,
        // ProjectConfigWriter's per-project sync) — write to tmp, then delegate the
        // swap to the shared AtomicFile.Swap helper (Editor/AtomicFile.cs), which
        // PortResolver's port-file writers also use (C1 r6 #1). Unlike
        // delete-then-move, File.Replace has no intermediate state where the path
        // is missing, so a sharing violation on the original (AV scan, sync
        // client, another process) between delete and move can no longer
        // permanently lose the original config (C1 r5 #2).
        internal static void WriteAtomic(string path, string content)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            AtomicFile.Swap(tmp, path);
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
        /// ref = "0.54.1", "v0.54.1", or a semver pre-release tag like "1.51.0-rc.1"
        /// (same pre-release grammar ProjectConfigToml.VersionPattern accepts for
        /// TOML markers — shared, not duplicated) — all accepted.
        /// Returns the unpinned default URL when ref is null/empty.
        /// </summary>
        public static string GitInstallUrlFor(string @ref)
        {
            if (string.IsNullOrEmpty(@ref)) return GitInstallUrl;
            var clean = @ref.TrimStart('v');
            // Whole-ref grammar check first — rejects any character in a
            // "-<pre-release>" suffix that ProjectConfigToml's shared VersionPattern
            // wouldn't accept in a TOML marker (e.g. "rc_1" or "rc/1").
            if (!Regex.IsMatch(clean, "^" + ProjectConfigToml.VersionPattern + "$"))
                throw new ArgumentException($"Invalid version ref: {@ref}");
            // VersionPattern's base component ([\d.]+) doesn't require exactly
            // three numeric parts — that stricter X.Y.Z shape is this call site's
            // own requirement, still enforced here.
            var dashIdx = clean.IndexOf('-');
            var basePart = dashIdx < 0 ? clean : clean.Substring(0, dashIdx);
            var parts = basePart.Split('.');
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
            // Quote/escape-aware, same gate as DepthAt below — a brace-like character
            // inside a string value (e.g. "env": {"GREETING": "hello } world"}) must
            // never be mistaken for real JSON structure, or the point-splice below
            // truncates/over-extends the entry and corrupts the config (C1 r3 cw-1).
            int depth = 1, pos = braceStart + 1;
            bool inString = false;
            while (pos < json.Length && depth > 0)
            {
                var c = json[pos];
                if (inString)
                {
                    if (c == '\\') pos++; // skip escaped char (e.g. \")
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}') depth--;
                pos++;
            }
            if (depth != 0) return false;
            start = braceStart;
            end = pos;
            return true;
        }

        // ARC-13 T1/T2: point-splice the 3 fields the wizard template writes —
        // command, args, and (when the call site's template has one) _v — into an
        // existing entry span, leaving every other key byte-identical. A field
        // present in the fresh template but absent from the old entry is inserted
        // (T2), never left permanently missing.
        private static readonly string[] PatchedFields = { "command", "args", "_v" };

        private static string PatchEntryFields(string oldSpan, string freshEntry)
        {
            foreach (var field in PatchedFields)
            {
                if (!FindFieldSegment(freshEntry, field, out var freshStart, out var freshEnd))
                    continue; // this call site's template doesn't write this field (e.g. bare Merge has no "_v")

                var freshSegment = freshEntry.Substring(freshStart, freshEnd - freshStart);

                oldSpan = FindFieldSegment(oldSpan, field, out var oldStart, out var oldEnd)
                    ? oldSpan.Substring(0, oldStart) + freshSegment + oldSpan.Substring(oldEnd)
                    : InsertFieldBeforeClosingBrace(oldSpan, freshSegment);
            }
            return oldSpan;
        }

        // Point-inserts a "field": value segment just before oldSpan's closing
        // brace — the exact technique ProjectConfigFormats.Adopt() already uses for
        // "_v" (ProjectConfigFormats.cs Adopt), generalized to any wizard-owned
        // field name. Position/formatting are cosmetic only (ARC-13 §6 Risks) —
        // output stays valid JSON either way. Internal (not private): reused by
        // ProjectConfigFormats.Pin() (ARC-11 T1) to insert "_pin": true without
        // duplicating this splice logic (DRY).
        internal static string InsertFieldBeforeClosingBrace(string oldSpan, string fieldSegment)
        {
            var insertAt = oldSpan.Length - 1; // index of closing '}'
            var before = oldSpan.Substring(0, insertAt).TrimEnd();
            var sep = before.EndsWith("{") ? "" : ",";
            return before + sep + "\n      " + fieldSegment + "\n    }";
        }

        // Locates the full `"field": value` segment — from the opening quote of the
        // key to the end of its value — for the two value shapes the wizard template
        // ever writes: a quoted string (command, _v) or a flat string array (args).
        // Scoped to depth 1 (a direct child of the entry's own object): without this,
        // Regex.Match would take the FIRST textual occurrence of "command"/"args" in
        // the span, which a user's own nested object (e.g. "env": {"args": ...}) can
        // precede — corrupting user data instead of touching our own field (ARC-13
        // T2 review). Callers splice a replacement/insertion into this span; every
        // unrelated key, at any depth, is untouched.
        private static bool FindFieldSegment(string text, string field, out int start, out int end)
        {
            start = end = -1;
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*";
            foreach (Match m in Regex.Matches(text, pattern))
            {
                if (DepthAt(text, m.Index) != 1) continue; // not a direct child of the entry object

                var valueStart = m.Index + m.Length;
                if (valueStart >= text.Length) continue;

                if (text[valueStart] == '"')
                {
                    var close = text.IndexOf('"', valueStart + 1);
                    if (close < 0) continue;
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
                    if (depth != 0) continue;
                    start = m.Index;
                    end = pos;
                    return true;
                }
                // unsupported value shape at depth 1 — keep scanning rather than guess
            }
            return false;
        }

        // Counts {}/[] nesting depth up to (not including) `pos`, skipping quoted
        // string content so brace-like characters inside a value never distort the
        // count — the same inString/escape-aware technique FindEntryBounds itself
        // uses to scan its own {}-only entry bounds.
        private static int DepthAt(string text, int pos)
        {
            int depth = 0;
            bool inString = false;
            var limit = Math.Min(pos, text.Length);
            for (int i = 0; i < limit; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (c == '\\') i++; // skip escaped char (e.g. \")
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }
            return depth;
        }
    }
}
