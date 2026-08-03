using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Material/texture scene-wide audit. Uses sharedMaterials — never .material.
    /// action: summary|materials|textures|duplicates|compression|recommendations
    /// </summary>
    internal static partial class MaterialAuditHelper
    {
        public static string Execute(string args)
        {
            var action   = JsonHelper.ExtractString(args, "action") ?? "summary";
            var platform = JsonHelper.ExtractString(args, "platform") ?? "Default";
            return action switch
            {
                "summary"         => Summary(),
                "materials"       => Materials(),
                "textures"        => Textures(),
                "duplicates"      => Duplicates(),
                "compression"     => Compression(platform),
                "recommendations" => Recommendations(),
                _ => $"err:Unknown action '{action}'. Valid: summary|materials|textures|duplicates|compression|recommendations"
            };
        }

        // ── Actions ────────────────────────────────────────────────────────

        private static string Summary()
        {
            CollectAll(out var mats, out var textures, out int instanced);
            long texMem = 0;
            foreach (var tex in textures) texMem += GetTextureMemory(tex);

            var sb = new StringBuilder();
            sb.AppendLine("MATERIAL AUDIT SUMMARY");
            sb.AppendLine($"  mats: {mats.Count}  instanced: {instanced}");
            sb.AppendLine($"  unique textures: {textures.Count}");
            sb.Append($"  tex mem est: {FormatBytes(texMem)}");
            return sb.ToString().TrimEnd();
        }

        private static string Materials()
        {
            var seen = new HashSet<Material>();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var sb = new StringBuilder();
            sb.AppendLine("MATERIALS");
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials) // NEVER .materials
                {
                    if (mat == null || !seen.Add(mat)) continue;
                    var path = AssetDatabase.GetAssetPath(mat);
                    var instFlag = mat.name.EndsWith(" (Instance)") ? " [INST]" : "";
                    sb.AppendLine($"  {mat.name}{instFlag} shader={mat.shader?.name ?? "none"} path={path ?? "procedural"}");
                }
            }
            if (seen.Count == 0) sb.AppendLine("  (none)");
            return sb.ToString().TrimEnd();
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static void CollectAll(
            out HashSet<Material> mats,
            out HashSet<Texture> textures,
            out int instanced)
        {
            mats = new HashSet<Material>();
            textures = new HashSet<Texture>();
            instanced = 0;
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                foreach (var mat in r.sharedMaterials) // NEVER .materials
                {
                    if (mat == null) continue;
                    if (mats.Add(mat))
                    {
                        if (mat.name.EndsWith(" (Instance)")) instanced++;
                        CollectTextures(mat, textures);
                    }
                }
            }
        }

        private static long GetTextureMemory(Texture tex)
        {
            if (tex == null) return 0;
            long size = Profiler.GetRuntimeMemorySizeLong(tex);
            if (size > 0) return size;
            int bpp = tex is Texture2D t2d ? GetBPP(t2d.format) : 4;
            long mipFactor = tex.mipmapCount > 1 ? 4 : 3;
            return (long)tex.width * tex.height * bpp * mipFactor / 3;
        }

        private static int GetBPP(TextureFormat fmt) => fmt switch
        {
            TextureFormat.RGB24  => 3,
            TextureFormat.RGBA32 => 4,
            _ => 4
        };

        private static string FormatBytes(long b) =>
            b >= 1024 * 1024 ? $"{b / (1024.0 * 1024):F1}MB" :
            b >= 1024        ? $"{b / 1024.0:F1}KB" : $"{b}B";
    }
}
