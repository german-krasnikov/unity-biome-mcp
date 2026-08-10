// @-mention popup configuration: max result count + sort order.
// Serialized via JsonUtility as part of BackendConfigStore.
using System;

namespace UnityMCP.Editor.Chat
{
    [Serializable]
    internal sealed class MentionConfig
    {
        public int             MaxPopupRows = 8;  // valid range: 3–20 (clamped in UI)
        public MentionSortOrder SortOrder   = MentionSortOrder.ByRelevance;
    }

    internal enum MentionSortOrder
    {
        ByRelevance = 0,
        ByName      = 1,
        ByType      = 2,
        ByRecency   = 3,
    }
}
