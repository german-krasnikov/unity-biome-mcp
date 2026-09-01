// Pure TOML entry build/merge/classify for .codex/config.toml.
// Mirrors server/src/unity_mcp/config/merger.py:merge_toml_mcp (same section header,
// same command/args/env sub-table shape) — ported rather than re-invented so the two
// independent writers (Python global, C# per-project) produce byte-similar output.
// No Unity API — plain string/int in, string/enum out.
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Wizard
{
    internal static class ProjectConfigToml
    {
        // Marker comment + section header + body are treated as one atomic replaceable
        // unit, including any number of dotted subsections that follow (e.g. .env) —
        // mirrors Python's _UNITY_MCP_SECTION_RE (merger.py) exactly, which matches
        // zero-or-more `[mcp_servers.unity-biome-mcp.<name>]` blocks, not just one hardcoded
        // `.env`. Keeping both regexes structurally identical avoids the two independent
        // writers (Python global, C# per-project) disagreeing on what "the unity-biome-mcp
        // block" spans, which would orphan stale dotted subsections on re-merge.
        // Server name is "unity-biome-mcp" (one "mcp", distinct from the foreign bare
        // [mcp_servers.unity]). Regexes accept the OLD "unity-mcp" name too so an
        // existing install is migrated (replaced) rather than left as a duplicate.
        private static readonly Regex SectionRe = new Regex(
            @"(?:^# unity-(?:biome-mcp|mcp) generated v[\d.]+\r?\n)?" +
            @"\[mcp_servers\.unity-(?:biome-mcp|mcp)\]\r?\n" +
            @"(?:(?!\[)[^\r\n]*\r?\n)*" +
            @"(?:\[mcp_servers\.unity-(?:biome-mcp|mcp)\.[^\]]+\]\r?\n(?:(?!\[)[^\r\n]*\r?\n)*)*",
            RegexOptions.Multiline);

        // Optional " pinned" suffix (ARC-0b Task 1) may follow the version on the same
        // comment line — ExtractMarkerVersion must still find the version in a pinned file.
        private static readonly Regex MarkerVersionRe = new Regex(
            @"^# unity-(?:biome-mcp|mcp) generated v([\d.]+)(?: pinned)?\r?\n\[mcp_servers\.unity-(?:biome-mcp|mcp)\]", RegexOptions.Multiline);

        private static readonly Regex MarkerPortRe = new Regex(@"UNITY_MCP_PORT\s*=\s*'(\d+)'");

        // ARC-0b Task 1: " pinned" suffix on the marker comment line, directly above
        // our own section header — same scoping guarantee as MarkerVersionRe (must be
        // immediately followed by our [mcp_servers.unity-...] header), so a sibling
        // section's comment never leaks into our classification.
        private static readonly Regex PinRe = new Regex(
            @"^# unity-(?:biome-mcp|mcp) generated v[\d.]+ pinned\r?\n\[mcp_servers\.unity-(?:biome-mcp|mcp)\]",
            RegexOptions.Multiline);

        internal static string BuildFresh(int port, string gitUrl, string version) =>
            $"# {PermissionConfig.SERVER_NAME} generated v{version}\n" +
            $"[mcp_servers.{PermissionConfig.SERVER_NAME}]\n" +
            "command = 'uvx'\n" +
            $"args = ['--from', '{gitUrl}', 'unity-biome-mcp']\n" +    // 'unity-biome-mcp' = PyPI package name
            "\n" +
            $"[mcp_servers.{PermissionConfig.SERVER_NAME}.env]\n" +
            $"UNITY_MCP_PORT = '{port}'\n";

        internal static string Merge(string existing, int port, string gitUrl, string version)
        {
            var fresh = BuildFresh(port, gitUrl, version);
            if (SectionRe.IsMatch(existing))
                return SectionRe.Replace(existing, m => fresh, 1);

            var sep = existing.Length > 0 && !existing.EndsWith("\n") ? "\n" : "";
            var blank = existing.Length > 0 ? "\n" : "";
            return existing + sep + blank + fresh;
        }

        internal static string ExtractMarkerVersion(string existingText)
        {
            if (string.IsNullOrEmpty(existingText)) return null;
            var m = MarkerVersionRe.Match(existingText);
            return m.Success ? m.Groups[1].Value : null;
        }

        internal static int? ExtractMarkerPort(string existingText)
        {
            if (string.IsNullOrEmpty(existingText)) return null;
            var m = MarkerPortRe.Match(existingText);
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }

        internal static bool IsPinned(string existingText) =>
            !string.IsNullOrEmpty(existingText) && PinRe.IsMatch(existingText);

        /// <summary>
        /// Insert the version marker comment before the section header of a Foreign entry.
        /// After this call, Classify() returns OwnedCurrent when the existing entry's port matches.
        /// Returns the original reference unchanged when no unity-biome-mcp section is found.
        /// </summary>
        internal static string Adopt(string existingText, string version)
        {
            var m = Regex.Match(existingText,
                @"\[mcp_servers\.unity-(?:biome-mcp|mcp)\]",
                RegexOptions.Multiline);
            if (!m.Success) return existingText;
            var insertAt = m.Index;
            var comment = $"# {PermissionConfig.SERVER_NAME} generated v{version}\n";
            return existingText.Substring(0, insertAt) + comment + existingText.Substring(insertAt);
        }

        internal static EntryState Classify(string existingText, int port, string version)
        {
            if (string.IsNullOrEmpty(existingText) ||
                !(existingText.Contains("[mcp_servers.unity-mcp]")
                  || existingText.Contains($"[mcp_servers.{PermissionConfig.SERVER_NAME}]")))
                return EntryState.Absent;

            var markerVersion = ExtractMarkerVersion(existingText);
            if (markerVersion == null)
                return EntryState.Foreign;

            if (IsPinned(existingText))
                return EntryState.OwnedCurrent;

            var markerPort = ExtractMarkerPort(existingText);
            return markerVersion == version && markerPort == port
                ? EntryState.OwnedCurrent
                : EntryState.OwnedStale;
        }
    }
}
