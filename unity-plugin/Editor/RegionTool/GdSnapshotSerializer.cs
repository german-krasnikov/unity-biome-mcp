using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>Converts RegionSnapshot instances to DSL ALIAS lines for playtest scripts.</summary>
    internal static class GdSnapshotSerializer
    {
        /// <summary>Single snapshot → one or more ALIAS lines (no trailing newline).</summary>
        internal static string ToAliasLines(RegionSnapshot snap)
        {
            var label = SanitizeLabel(snap.Label, snap.Id);
            var sb = new StringBuilder();

            switch (snap.AnnotationType ?? "region")
            {
                case "point":
                case "region":
                    sb.Append(AliasLine(label, snap.CenterX, snap.PlaneY, snap.CenterZ));
                    break;

                case "polyline":
                    AppendVertexLines(sb, label, snap, suffix: true);
                    break;

                case "measurement":
                    if (snap.VerticesFlat == null || snap.VerticesFlat.Length < 4)
                        return $"# @{label} — no vertices";
                    sb.AppendLine(AliasLine(label + "_start",
                        snap.VerticesFlat[0], snap.PlaneY, snap.VerticesFlat[1]));
                    sb.Append(AliasLine(label + "_end",
                        snap.VerticesFlat[2], snap.PlaneY, snap.VerticesFlat[3]));
                    break;

                default:
                    return $"# @{label} — unknown type {snap.AnnotationType}";
            }

            return sb.ToString();
        }

        /// <summary>All snapshots → preamble block of ALIAS lines.</summary>
        internal static string ToPlaytestPreamble(IEnumerable<RegionSnapshot> snapshots)
        {
            var sb = new StringBuilder();
            foreach (var snap in snapshots)
            {
                sb.AppendLine(ToAliasLines(snap));
            }
            return sb.ToString().TrimEnd();
        }

        // ── helpers ───────────────────────────────────────────────────────────

        static string AliasLine(string label, float x, float y, float z)
            => string.Format(CultureInfo.InvariantCulture,
                "ALIAS @{0} {1:F2},{2:F2},{3:F2}", label, x, y, z);

        static void AppendVertexLines(StringBuilder sb, string label, RegionSnapshot snap, bool suffix)
        {
            var flat = snap.VerticesFlat;
            if (flat == null || flat.Length == 0) return;
            int count = flat.Length / 2;
            for (int i = 0; i < count; i++)
            {
                var line = AliasLine(label + "_" + i, flat[i * 2], snap.PlaneY, flat[i * 2 + 1]);
                if (i < count - 1) sb.AppendLine(line);
                else sb.Append(line);
            }
        }

        static string SanitizeLabel(string label, string id)
        {
            if (string.IsNullOrWhiteSpace(label))
                return "gd_" + id;

            var lower = label.ToLowerInvariant().Replace(' ', '_');
            return Regex.Replace(lower, @"[^a-z0-9_]", "");
        }
    }
}
