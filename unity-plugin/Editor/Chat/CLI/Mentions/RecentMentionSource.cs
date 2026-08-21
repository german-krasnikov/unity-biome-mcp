// Phase 2: Recently used / currently selected items source.
// No dirty-flag — always reads from Selection live (O(1)).
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class RecentMentionSource : IMentionSource
    {
        private const long ScoreBoost = 2000;
        private const string HierarchyIcon = "d_UnityEditor.SceneHierarchyWindow";

        // No-op: always fresh from Selection
        public void RefreshIfDirty() { }

        public void Search(string query, int maxResults, List<MentionCandidate> results)
        {
            if (string.IsNullOrEmpty(query)) return;
            if (results.Count >= maxResults) return;

            var lower = query.ToLowerInvariant();

            // Currently selected GameObject
            var activeGO = Selection.activeGameObject;
            if (activeGO != null)
            {
                var name  = activeGO.name;
                var score = ScoreFor(name, lower);
                if (score > 0)
                {
                    var path = ComponentSerializer.GetPath(activeGO);
                    var goid = GlobalObjectId.GetGlobalObjectIdSlow(activeGO);
                    var chip = new ChipData(ChipKindKeys.Hierarchy, path, name,
                        RefManager.Assign(activeGO), goid);
                    results.Add(new MentionCandidate(chip, score + ScoreBoost, HierarchyIcon));
                    if (results.Count >= maxResults) return;
                }
            }

            // Currently selected asset
            var activeObj = Selection.activeObject;
            if (activeObj != null && !(activeObj is GameObject))
            {
                var assetPath = AssetDatabase.GetAssetPath(activeObj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var name  = activeObj.name;
                    var score = ScoreFor(name, lower);
                    if (score > 0)
                    {
                        var ext  = System.IO.Path.GetExtension(assetPath).ToLowerInvariant();
                        var kind = AssetMentionIndex.KindKeyForExtension(ext);
                        var chip = new ChipData(kind, assetPath, name, 0);
                        results.Add(new MentionCandidate(chip, score + ScoreBoost, "d_DefaultAsset Icon"));
                    }
                }
            }
        }

        private static long ScoreFor(string name, string queryLower)
        {
            var nameLower = name.ToLowerInvariant();
            var mask      = MentionFuzzyScorer.BuildCharMask(nameLower);
            var qmask     = MentionFuzzyScorer.BuildCharMask(queryLower);
            if (!MentionFuzzyScorer.PassesPreFilter(qmask, mask)) return 0;
            return MentionFuzzyScorer.Score(queryLower, nameLower, name);
        }
    }
}
