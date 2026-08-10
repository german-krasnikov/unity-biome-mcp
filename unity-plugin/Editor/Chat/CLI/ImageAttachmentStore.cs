// Persists dropped/pasted image files under Library/MCPChat/Attachments/.
// Pure IO — no Unity deps. MD5-prefix dedup keeps identical images as one file.
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ImageAttachmentStore
    {
        internal const int MaxBytes = 4_194_304; // 4 MB — Claude API hard-cap

        // Default root under the project Library folder.
        // Call overloads with explicit dir in tests.
        internal static string DefaultRoot =>
            Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", "Library", "MCPChat", "Attachments"));

        /// <summary>Copy srcAbsPath into storeDir; dedup by MD5 prefix. Returns dest path or null on error/oversize.</summary>
        internal static string ImportFile(string srcAbsPath, string storeDir = null)
        {
            if (string.IsNullOrEmpty(srcAbsPath)) return null;
            // C4: canonicalize before touching filesystem
            srcAbsPath = Path.GetFullPath(srcAbsPath);
            if (!File.Exists(srcAbsPath)) return null;
            try
            {
                var bytes = File.ReadAllBytes(srcAbsPath);
                // C1: size guard
                if (bytes.Length > MaxBytes)
                {
                    Debug.LogWarning($"{BiomeLabel.Tag} Image too large (>{MaxBytes / 1_048_576}MB): {Path.GetFileName(srcAbsPath)}");
                    return null;
                }
                var name  = Path.GetFileName(srcAbsPath);
                return Write(bytes, name, storeDir ?? DefaultRoot);
            }
            catch { return null; }
        }

        /// <summary>Save raw bytes as .png into storeDir; dedup by MD5 prefix. Returns dest path or null on oversize.</summary>
        internal static string ImportBytes(byte[] pngBytes, string storeDir = null, string baseName = "paste")
        {
            // C1: size guard
            if (pngBytes == null || pngBytes.Length > MaxBytes) return null;
            storeDir ??= DefaultRoot;
            return Write(pngBytes, baseName + ".png", storeDir);
        }

        /// <summary>Delete files in storeDir older than maxAgeDays.</summary>
        internal static void Purge(string storeDir = null, int maxAgeDays = 30)
        {
            storeDir ??= DefaultRoot;
            if (!Directory.Exists(storeDir)) return;
            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
            foreach (var f in Directory.GetFiles(storeDir))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(f) < cutoff)
                        File.Delete(f);
                }
                catch { /* skip locked files */ }
            }
        }

        // ── private helpers ───────────────────────────────────────────────────

        // C2: valid image magic bytes — PNG or JPEG only
        private static bool HasImageMagicBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2) return false;
            // JPEG: FF D8 (first 2 bytes; FF D8 FF is the full SOI+marker but 2 bytes suffice for identification)
            if (bytes[0] == 0xFF && bytes[1] == 0xD8) return true;
            // PNG: 89 50 4E 47 (requires 4 bytes)
            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
                return true;
            return false;
        }

        private static string Write(byte[] bytes, string originalName, string dir)
        {
            // C2: reject non-image bytes (e.g. disguised executables)
            if (!HasImageMagicBytes(bytes))
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Unsupported image format: {originalName}");
                return null;
            }
            Directory.CreateDirectory(dir);
            var prefix = Md5Prefix(bytes);
            var dest   = Path.Combine(dir, prefix + "_" + originalName);
            if (!File.Exists(dest))
                File.WriteAllBytes(dest, bytes);
            return dest;
        }

        private static string Md5Prefix(byte[] data)
        {
            using var md5  = MD5.Create();
            var hash = md5.ComputeHash(data);
            return BitConverter.ToString(hash, 0, 4).Replace("-", "").ToLowerInvariant();
        }
    }
}
