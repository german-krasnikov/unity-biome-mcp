using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    // Textures, duplicates, compression, recommendations, fingerprinting.
    internal static partial class MaterialAuditHelper
    {
        private static string Textures()
        {
            var seen = new HashSet<Texture>();
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != null) CollectTextures(mat, seen);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("TEXTURES");
            foreach (var tex in seen)
            {
                var mem = GetTextureMemory(tex);
                var fmt = tex is Texture2D t2d ? t2d.format.ToString() : "?";
                sb.AppendLine($"  {tex.name} {tex.width}x{tex.height} fmt={fmt} mem={FormatBytes(mem)}");
            }
            if (seen.Count == 0) sb.AppendLine("  (none)");
            return sb.ToString().TrimEnd();
        }

        private static string Duplicates()
        {
            var fpMap = new Dictionary<string, List<Material>>();
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var seenMats = new HashSet<Material>();
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials) // NEVER .materials
                {
                    if (mat == null || !seenMats.Add(mat)) continue;
                    var fp = Fingerprint(mat);
                    if (!fpMap.TryGetValue(fp, out var list))
                        fpMap[fp] = list = new List<Material>();
                    list.Add(mat);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("DUPLICATES (same shader+keywords+props)");
            int groups = 0;
            foreach (var list in fpMap.Values)
            {
                if (list.Count < 2) continue;
                groups++;
                sb.Append($"  group({list.Count}):");
                foreach (var m in list) sb.Append($" {m.name}");
                sb.AppendLine();
            }
            if (groups == 0) sb.AppendLine("  no duplicates found");
            return sb.ToString().TrimEnd();
        }

        private static string Compression(string platform)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"COMPRESSION [{platform}]");
            var guids = AssetDatabase.FindAssets("t:Texture2D");
            int flagged = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;

                var settings = imp.GetPlatformTextureSettings(platform);
                if (!settings.overridden)
                    settings = imp.GetDefaultPlatformTextureSettings();

                if (settings.format == TextureImporterFormat.Automatic ||
                    settings.format == TextureImporterFormat.RGBA32 ||
                    settings.format == TextureImporterFormat.RGB24)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null && tex.width >= 256 && tex.height >= 256)
                    {
                        sb.AppendLine($"  WARN uncompressed {tex.width}x{tex.height}: {path}");
                        if (++flagged >= 20) { sb.AppendLine("  ... (truncated)"); break; }
                    }
                }
            }
            if (flagged == 0) sb.AppendLine("  all textures compressed or no large uncompressed textures found");
            return sb.ToString().TrimEnd();
        }

        private static string Recommendations()
        {
            CollectAll(out var mats, out _, out int instanced);
            var recs = new List<(int priority, string msg)>();

            if (instanced > 0)
                recs.Add((10, $"CRIT: {instanced} instanced materials — script uses .material instead of .sharedMaterial"));

            var fpMap = new Dictionary<string, int>();
            foreach (var mat in mats)
            {
                var fp = Fingerprint(mat);
                fpMap[fp] = fpMap.TryGetValue(fp, out int c) ? c + 1 : 1;
            }
            int dupCount = 0;
            foreach (var count in fpMap.Values)
                if (count > 1) dupCount += count - 1;
            if (dupCount > 0)
                recs.Add((8, $"HIGH: {dupCount} redundant materials — consolidate to reduce draw calls"));

            recs.Sort((a, b) => b.priority.CompareTo(a.priority));

            var sb = new StringBuilder();
            sb.AppendLine("RECOMMENDATIONS (ranked by impact)");
            if (recs.Count == 0) sb.AppendLine("  no issues found");
            foreach (var (_, msg) in recs) sb.AppendLine($"  {msg}");
            return sb.ToString().TrimEnd();
        }

        private static void CollectTextures(Material mat, HashSet<Texture> textures)
        {
            if (mat.shader == null) return;
            int count = mat.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (mat.shader.GetPropertyType(i) != ShaderPropertyType.Texture) continue;
                var tex = mat.GetTexture(mat.shader.GetPropertyName(i));
                if (tex == null) continue;
                var path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) continue; // skip procedural textures
                textures.Add(tex);
            }
        }

        /// <summary>
        /// Material fingerprint: shader + sorted keywords + non-texture property values.
        /// Textures excluded — different textures on same shader are intentional, not duplicates.
        /// </summary>
        private static string Fingerprint(Material mat)
        {
            if (mat.shader == null) return TransientObjectId.GetWireValue(mat);
            var sb = new StringBuilder(mat.shader.name);
            var kws = mat.shaderKeywords;
            Array.Sort(kws);
            foreach (var kw in kws) sb.Append('|').Append(kw);

            int propCount = mat.shader.GetPropertyCount();
            for (int i = 0; i < propCount; i++)
            {
                var pName = mat.shader.GetPropertyName(i);
                var pType = mat.shader.GetPropertyType(i);
                if (pType == ShaderPropertyType.Texture) continue;
                sb.Append('|').Append(pName).Append('=');
                try
                {
                    switch (pType)
                    {
                        case ShaderPropertyType.Color:  sb.Append(mat.GetColor(pName));  break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:  sb.Append(mat.GetFloat(pName).ToString("F3")); break;
                        case ShaderPropertyType.Vector: sb.Append(mat.GetVector(pName)); break;
                        case ShaderPropertyType.Int:    sb.Append(mat.GetInt(pName));    break;
                    }
                }
                catch { /* skip inaccessible properties */ }
            }
            return sb.ToString();
        }
    }
}
