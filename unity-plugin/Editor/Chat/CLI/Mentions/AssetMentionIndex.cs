// Phase 2: Asset search source implementing IMentionSource.
// Dirty-flag via ChipExistenceAssetPostprocessor.OnAssetsChanged.
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AssetMentionIndex : IMentionSource, System.IDisposable
    {
        private readonly struct Entry
        {
            public readonly string FileName;
            public readonly string FileNameLower;
            public readonly uint   CharMask;
            public readonly string Path;
            public readonly string KindKey;
            public readonly string IconName;

            public Entry(string fn, string fnLower, uint mask, string path, string kind, string icon)
            {
                FileName      = fn;
                FileNameLower = fnLower;
                CharMask      = mask;
                Path          = path;
                KindKey       = kind;
                IconName      = icon;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>(512);
        private bool _dirty = true;

        public AssetMentionIndex()
        {
            ChipExistenceAssetPostprocessor.OnAssetsChanged += OnAssetsChanged;
        }

        public void Dispose()
        {
            ChipExistenceAssetPostprocessor.OnAssetsChanged -= OnAssetsChanged;
        }

        private void OnAssetsChanged(string[] imported, string[] deleted, string[] moved)
            => _dirty = true;

        public void RefreshIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;
            Rebuild();
        }

        public void Search(string query, int maxResults, List<MentionCandidate> results)
        {
            if (string.IsNullOrEmpty(query)) return;

            var lower = query.ToLowerInvariant();
            var queryMask = MentionFuzzyScorer.BuildCharMask(lower);

            foreach (var e in _entries)
            {
                if (results.Count >= maxResults) break;
                if (!MentionFuzzyScorer.PassesPreFilter(queryMask, e.CharMask)) continue;

                var score = MentionFuzzyScorer.Score(lower, e.FileNameLower, e.FileName);
                if (score <= 0) continue;

                var chip = new ChipData(e.KindKey, e.Path, e.FileName, 0);
                results.Add(new MentionCandidate(chip, score, e.IconName));
            }
        }

        private void Rebuild()
        {
            _entries.Clear();
            var paths = AssetDatabase.GetAllAssetPaths();
            foreach (var assetPath in paths)
            {
                if (!ShouldIncludePath(assetPath)) continue;
                var fn    = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                var lower = fn.ToLowerInvariant();
                var mask  = MentionFuzzyScorer.BuildCharMask(lower);
                var ext   = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();
                var kind  = KindKeyForExtension(ext);
                var icon  = IconForKindKey(kind);
                _entries.Add(new Entry(fn, lower, mask, assetPath, kind, icon));
            }
        }

        // ── Public helpers (used by tests) ────────────────────────────────────

        internal static bool ShouldIncludePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            // Exclude .meta, .dll, .so, .pdb
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".meta" || ext == ".dll" || ext == ".so" || ext == ".pdb") return false;
            // Exclude Packages/ unless it's our own package
            if (path.StartsWith("Packages/") && !path.Contains("/com.unity-biome-mcp/")) return false;
            return true;
        }

        internal static string KindKeyForExtension(string ext)
        {
            switch (ext)
            {
                case ".cs":                        return ChipKindKeys.Script;
                case ".prefab":                    return ChipKindKeys.Prefab;
                case ".mat":                       return ChipKindKeys.Material;
                case ".unity":                     return ChipKindKeys.Scene;
                case ".png": case ".jpg":
                case ".tga": case ".jpeg":         return ChipKindKeys.Texture;
                case ".fbx": case ".obj":
                case ".blend": case ".dae":        return ChipKindKeys.Model;
                case ".wav": case ".mp3":
                case ".ogg": case ".aiff":         return ChipKindKeys.Audio;
                case ".asset":                     return ChipKindKeys.ScriptableObject;
                case "":                           return ChipKindKeys.Folder;
                default:                           return ChipKindKeys.Asset;
            }
        }

        private static string IconForKindKey(string kind)
        {
            switch (kind)
            {
                case ChipKindKeys.Script:          return "d_cs Script Icon";
                case ChipKindKeys.Prefab:          return "d_Prefab Icon";
                case ChipKindKeys.Material:        return "d_Material Icon";
                case ChipKindKeys.Scene:           return "d_SceneAsset Icon";
                case ChipKindKeys.Texture:         return "d_Texture Icon";
                case ChipKindKeys.Model:           return "d_Mesh Icon";
                case ChipKindKeys.Audio:           return "d_AudioClip Icon";
                case ChipKindKeys.ScriptableObject: return "d_ScriptableObject Icon";
                case ChipKindKeys.Folder:          return "d_Folder Icon";
                default:                           return "d_DefaultAsset Icon";
            }
        }
    }
}
