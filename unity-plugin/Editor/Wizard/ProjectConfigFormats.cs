// Pure JSON entry build/merge/classify for per-project MCP config files.
// No Unity API — plain string/int in, string/enum out (see Plans/Install/11-phase1a-design.md).
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.Wizard
{
    // Shared by ProjectConfigFormats (JSON) and ProjectConfigToml (Codex TOML).
    internal enum EntryState { Absent, Foreign, OwnedStale, OwnedCurrent }

    internal static class ProjectConfigFormats
    {
        private static readonly Regex MarkerVersionRe = new Regex("\"_v\"\\s*:\\s*\"([^\"]+)\"");
        private static readonly Regex MarkerPortRe = new Regex("\"UNITY_MCP_PORT\"\\s*:\\s*\"(\\d+)\"");
        private static readonly Regex MarkerPinRe = new Regex("\"_pin\"\\s*:\\s*true");

        // Builds the full unity-biome-mcp entry: WizardConfigWriter.Entry(port, gitUrl) plus a
        // trailing "_v": version marker key, inserted just before the closing brace.
        internal static string BuildEntry(int port, string gitUrl, string version)
        {
            var baseEntry = WizardConfigWriter.Entry(port, gitUrl);
            var withoutClosingBrace = baseEntry.Substring(0, baseEntry.Length - 1).TrimEnd();
            return withoutClosingBrace + ",\n      \"_v\": \"" + version + "\"\n    }";
        }

        internal static string BuildFresh(int port, string gitUrl, string version, string rootKey) =>
            WizardConfigWriter.FreshWithEntry(BuildEntry(port, gitUrl, version), rootKey);

        internal static string Merge(string existing, int port, string gitUrl, string version, string rootKey) =>
            WizardConfigWriter.MergeWithEntry(existing, BuildEntry(port, gitUrl, version), rootKey);

        // Scoped to the "unity-biome-mcp" value's own brace span (via WizardConfigWriter's
        // brace-counting), NOT the whole file — a sibling MCP server entry with its own
        // "_v"/"UNITY_MCP_PORT" key must never leak into unity-biome-mcp's classification
        // (was a data-loss bug: a foreign sibling's marker made a hand-edited unity-biome-mcp
        // entry misclassify as OwnedStale and get overwritten).
        // Finds our entry by SERVER_NAME first, then falls back to old name so
        // prior installs ("unity-mcp") still classify and get migrated.
        private static bool FindOurEntry(string text, out int start, out int end) =>
            WizardConfigWriter.FindEntryBounds(text, PermissionConfig.SERVER_NAME, out start, out end) ||
            WizardConfigWriter.FindEntryBounds(text, "unity-mcp", out start, out end);

        internal static string ExtractMarkerVersion(string existingText)
        {
            if (!FindOurEntry(existingText, out var start, out var end))
                return null;
            var m = MarkerVersionRe.Match(existingText, start, end - start);
            return m.Success ? m.Groups[1].Value : null;
        }

        internal static int? ExtractMarkerPort(string existingText)
        {
            if (!FindOurEntry(existingText, out var start, out var end))
                return null;
            var m = MarkerPortRe.Match(existingText, start, end - start);
            return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null;
        }

        // ARC-0b Task 1: "_pin": true sibling of "_v" inside OUR entry marks it as
        // user-pinned — Classify() must return OwnedCurrent regardless of version
        // mismatch. Scoped via FindOurEntry, same as ExtractMarkerVersion/Port, so a
        // sibling MCP server's own "_pin" never leaks into our classification.
        internal static bool IsPinned(string existingText)
        {
            if (!FindOurEntry(existingText, out var start, out var end))
                return false;
            return MarkerPinRe.Match(existingText, start, end - start).Success;
        }

        /// <summary>
        /// Insert a "_v" marker into a Foreign entry without rewriting any other content.
        /// After this call, Classify() returns OwnedCurrent for the given version.
        /// Returns the original reference unchanged when no unity-biome-mcp entry is found.
        /// </summary>
        internal static string Adopt(string existingText, string version)
        {
            if (!FindOurEntry(existingText, out _, out var end)) return existingText;
            var insertAt = end - 1; // index of closing }
            return existingText.Substring(0, insertAt).TrimEnd()
                + ",\n      \"_v\": \"" + version + "\"\n    }"
                + existingText.Substring(end);
        }

        /// <summary>
        /// ARC-11 T1: insert a "_pin": true marker as a sibling of "_v" inside our
        /// entry — surgical insert like Adopt(), reusing WizardConfigWriter's
        /// point-splice primitive (InsertFieldBeforeClosingBrace) so every other key
        /// (env, custom args, ...) is preserved byte-for-byte instead of re-parsed.
        /// Idempotent: returns the input unchanged when already pinned, so a
        /// repeated call never duplicates the marker. Returns the original
        /// reference unchanged when no unity-biome-mcp entry is found (mirrors
        /// Adopt_NoEntry).
        /// </summary>
        internal static string Pin(string existingText)
        {
            if (!FindOurEntry(existingText, out var start, out var end)) return existingText;
            if (IsPinned(existingText)) return existingText;
            var oldSpan = existingText.Substring(start, end - start);
            var patchedSpan = WizardConfigWriter.InsertFieldBeforeClosingBrace(oldSpan, "\"_pin\": true");
            return existingText.Substring(0, start) + patchedSpan + existingText.Substring(end);
        }

        internal static EntryState Classify(string existingText, int port, string version)
        {
            if (string.IsNullOrEmpty(existingText) ||
                !(existingText.Contains("\"unity-mcp\"")
                  || existingText.Contains($"\"{PermissionConfig.SERVER_NAME}\"")))
                return EntryState.Absent;

            var markerVersion = ExtractMarkerVersion(existingText);
            if (markerVersion == null)
                return EntryState.Foreign;

            if (IsPinned(existingText))
                return EntryState.OwnedCurrent;

            // Port is no longer written to JSON entries (discovery via .port files).
            // Staleness is version-only; port parameter kept for API compatibility.
            return markerVersion == version
                ? EntryState.OwnedCurrent
                : EntryState.OwnedStale;
        }
    }
}
