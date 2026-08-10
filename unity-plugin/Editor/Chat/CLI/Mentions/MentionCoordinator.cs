// Phase 3: Orchestrates all IMentionSource providers.
// Merges, deduplicates by path (keeps higher score), sorts desc, caps results.
// Pure synchronous logic — no UI dependency, fully NUnit-testable.
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MentionCoordinator
    {
        private readonly List<IMentionSource> _sources;
        private readonly List<MentionCandidate> _temp = new List<MentionCandidate>();
        private readonly Dictionary<string, MentionCandidate> _dedupMap = new Dictionary<string, MentionCandidate>();
        private int _requestId;

        /// <summary>Optional: inject for ByRecency sort. Null = fallback to ByRelevance.</summary>
        internal MentionHistory History { get; set; }

        internal MentionCoordinator(params IMentionSource[] sources)
        {
            _sources = new List<IMentionSource>(sources);
        }

        /// <summary>
        /// Search all sources, merge + dedup by path (higher score wins), sort, cap at maxResults.
        /// Returns request ID for staleness check via IsCurrent().
        /// Default sortOrder = ByRelevance (backward-compatible).
        /// </summary>
        internal int Search(string query, int maxResults, List<MentionCandidate> results,
            MentionSortOrder sortOrder = MentionSortOrder.ByRelevance)
        {
            int id = ++_requestId;

            if (string.IsNullOrEmpty(query))
                return id;

            _dedupMap.Clear();
            _temp.Clear();

            foreach (var source in _sources)
            {
                source.RefreshIfDirty();
                source.Search(query, maxResults * 2, _temp);
            }

            // Dedup by path — keep higher score
            foreach (var candidate in _temp)
            {
                string path = candidate.Chip.Path;
                if (!_dedupMap.TryGetValue(path, out var existing) || candidate.Score > existing.Score)
                    _dedupMap[path] = candidate;
            }

            // Collect, sort, cap
            _temp.Clear();
            foreach (var kv in _dedupMap.Values)
                _temp.Add(kv);

            ApplySort(_temp, sortOrder);

            int count = System.Math.Min(_temp.Count, maxResults);
            for (int i = 0; i < count; i++)
                results.Add(_temp[i]);

            return id;
        }

        /// <summary>True if the given request ID is still the latest (no newer search started).</summary>
        internal bool IsCurrent(int requestId) => requestId == _requestId;

        // ── private ──────────────────────────────────────────────────────────

        private void ApplySort(List<MentionCandidate> list, MentionSortOrder order)
        {
            switch (order)
            {
                case MentionSortOrder.ByName:
                    list.Sort((a, b) => string.Compare(
                        a.Chip.DisplayName, b.Chip.DisplayName,
                        System.StringComparison.OrdinalIgnoreCase));
                    break;

                case MentionSortOrder.ByType:
                    list.Sort((a, b) =>
                    {
                        int kind = string.Compare(
                            a.Chip.KindKey, b.Chip.KindKey,
                            System.StringComparison.Ordinal);
                        return kind != 0 ? kind : string.Compare(
                            a.Chip.DisplayName, b.Chip.DisplayName,
                            System.StringComparison.OrdinalIgnoreCase);
                    });
                    break;

                case MentionSortOrder.ByRecency:
                    if (History == null) goto default;
                    list.Sort((a, b) =>
                        History.GetTimestamp(b.Chip.Path)
                               .CompareTo(History.GetTimestamp(a.Chip.Path)));
                    break;

                default: // ByRelevance
                    list.Sort((a, b) => b.Score.CompareTo(a.Score));
                    break;
            }
        }
    }
}
