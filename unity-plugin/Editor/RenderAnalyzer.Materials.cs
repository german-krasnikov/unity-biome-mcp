// Materials and shaders analysis.
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static partial class RenderAnalyzer
    {
        private static string Materials(string path, string detail)
        {
            var renderers = GetRenderers(path);
            var groups = new Dictionary<Material, (string name, string shader, int count)>();
            foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (groups.TryGetValue(mat, out var g))
                    groups[mat] = (g.name, g.shader, g.count + 1);
                else
                    groups[mat] = (mat.name, mat.shader?.name ?? "null", 1);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"MATERIALS: {groups.Count} unique ({renderers.Length} renderers)");
            foreach (var kv in groups)
                sb.AppendLine($"  {kv.Value.name} ({kv.Value.shader}) ×{kv.Value.count}");
            return sb.ToString().TrimEnd();
        }

        private static string Shaders(string path, string detail)
        {
            var renderers = GetRenderers(path);
            var seen = new Dictionary<string, (int passes, int keywords, int refs)>();
            foreach (var r in renderers)
            foreach (var mat in r.sharedMaterials)
            {
                if (mat?.shader == null) continue;
                var sn = mat.shader.name;
                if (!seen.TryGetValue(sn, out var s))
                    s = (mat.shader.passCount, (int)mat.shader.keywordSpace.keywordCount, 0);
                seen[sn] = (s.passes, s.keywords, s.refs + 1);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"SHADERS: {seen.Count} unique");
            foreach (var kv in seen)
            {
                var (passes, kw, refs) = kv.Value;
                string warn = passes > 2 ? " WARN:high-pass-count" : "";
                sb.AppendLine($"  {kv.Key}: passes={passes} kw:{kw} refs={refs}{warn}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
