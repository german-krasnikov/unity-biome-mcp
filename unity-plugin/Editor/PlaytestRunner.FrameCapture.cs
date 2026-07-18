using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class FrameStitcher
    {
        /// <summary>Stitch frames side by side into one PNG. Returns the output file path.</summary>
        public static string StitchHorizontal(List<string> paths)
        {
            var textures = new Texture2D[paths.Count];
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    textures[i] = new Texture2D(2, 2);
                    textures[i].LoadImage(File.ReadAllBytes(paths[i]));
                }

                int totalWidth = 0, maxHeight = 0;
                foreach (var t in textures)
                {
                    totalWidth += t.width;
                    if (t.height > maxHeight) maxHeight = t.height;
                }

                var composite = new Texture2D(totalWidth, maxHeight, TextureFormat.RGB24, false);
                int x = 0;
                foreach (var t in textures)
                {
                    composite.SetPixels(x, 0, t.width, t.height, t.GetPixels());
                    x += t.width;
                }
                composite.Apply();
                var png = composite.EncodeToPNG();
                Object.DestroyImmediate(composite);
                return FileOutputHelper.WritePng(png, prefix: "strip");
            }
            finally
            {
                foreach (var t in textures)
                    if (t != null) Object.DestroyImmediate(t);
            }
        }

        /// <summary>Returns true if at least one frame differs from the first (pixel checksum).</summary>
        public static bool AreFramesDifferent(List<string> paths)
        {
            if (paths.Count < 2) return false;
            long refSum = PixelChecksum(paths[0]);
            for (int i = 1; i < paths.Count; i++)
                if (PixelChecksum(paths[i]) != refSum) return true;
            return false;
        }

        static long PixelChecksum(string path)
        {
            var tex = new Texture2D(2, 2);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                long sum = 0;
                foreach (var c in tex.GetPixels32())
                    sum += c.r | ((long)c.g << 8) | ((long)c.b << 16);
                return sum;
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
