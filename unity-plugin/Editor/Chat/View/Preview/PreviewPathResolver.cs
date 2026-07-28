// Shared path resolution + file-type guards for preview builders.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class PreviewPathResolver
    {
        // Guard: on Windows, Path.GetExtension/IsPathRooted throw ArgumentException for paths
        // containing illegal chars (e.g. '|' in component/field chip paths like "Root|Transform").
        // Without this guard, every send with a component chip on Windows triggers a 45s WER freeze.
        private static bool HasIllegalChars(string path)
            => path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;

        /// <summary>Returns an absolute filesystem path. Supports absolute or project-relative paths.</summary>
        internal static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path) || HasIllegalChars(path)) return "";
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(Directory.GetCurrentDirectory(), path);
        }

        internal static bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path) || HasIllegalChars(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".tif";
        }

        internal static bool IsAudioFile(string path)
        {
            if (string.IsNullOrEmpty(path) || HasIllegalChars(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".wav" or ".mp3" or ".ogg" or ".aiff";
        }

        internal static bool IsModelFile(string path)
        {
            if (string.IsNullOrEmpty(path) || HasIllegalChars(path)) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".fbx" or ".obj" or ".blend" or ".dae";
        }

        internal static bool IsAssetPath(string path)
            => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/");

        /// <summary>Load a Texture2D/Sprite from an asset database path. Returns null if not found.</summary>
        internal static Texture2D LoadAssetTexture(string assetPath)
        {
            if (!IsAssetPath(assetPath)) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) return tex;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            return sprite?.texture;
        }
    }
}
