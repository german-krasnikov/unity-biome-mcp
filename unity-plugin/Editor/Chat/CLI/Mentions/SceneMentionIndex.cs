// Phase 2: Scene object search source implementing IMentionSource.
// Dirty-flag via VersionTracker.Version — O(1) check per Search call.
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Chat
{
    internal sealed class SceneMentionIndex : IMentionSource
    {
        private const int MaxEntries = 3000;
        private const string IconName = "d_UnityEditor.SceneHierarchyWindow";

        private readonly struct Entry
        {
            public readonly string Name;
            public readonly string NameLower;
            public readonly uint   CharMask;
            public readonly string Path;
            public readonly string ObjectId;

            public Entry(string name, string nameLower, uint mask, string path, string objectId)
            {
                Name       = name;
                NameLower  = nameLower;
                CharMask   = mask;
                Path       = path;
                ObjectId   = objectId;
            }
        }

        private readonly List<Entry> _entries = new List<Entry>(256);
        private int _cachedVersion = -1;

        // TEST seam: expose cached version for dirty-flag verification
        internal int  CachedVersion => _cachedVersion;
        internal bool IsDirty       => _cachedVersion != VersionTracker.Version;
        internal int  EntryCount    => _entries.Count;
        internal uint GetEntryMask(int i) => _entries[i].CharMask;

        public void RefreshIfDirty()
        {
            var current = VersionTracker.Version;
            if (_cachedVersion == current) return;
            _cachedVersion = current;
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

                var score = MentionFuzzyScorer.Score(lower, e.NameLower, e.Name);
                if (score <= 0) continue;

                var chip = new ChipData(ChipKindKeys.Hierarchy, e.Path, e.Name, e.ObjectId);
                results.Add(new MentionCandidate(chip, score, IconName));
            }
        }

        private void Rebuild()
        {
            _entries.Clear();

            // Walk all loaded scenes
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    WalkTransform(root.transform);
            }

            // Include prefab stage objects
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
                WalkTransform(prefabStage.prefabContentsRoot.transform);
        }

        private void WalkTransform(Transform root)
        {
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (_entries.Count >= MaxEntries)
                {
                    Debug.LogWarning("[SceneMentionIndex] Cap of 3000 entries reached — some objects omitted.");
                    return;
                }
                var t     = stack.Pop();
                var go    = t.gameObject;
                var name  = go.name;
                var lower = name.ToLowerInvariant();
                var mask  = MentionFuzzyScorer.BuildCharMask(lower);
                var path  = ComponentSerializer.GetPath(go);
                _entries.Add(new Entry(name, lower, mask, path, TransientObjectId.GetWireValue(go)));

                for (int i = t.childCount - 1; i >= 0; i--)
                    stack.Push(t.GetChild(i));
            }
        }
    }
}
