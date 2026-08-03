// ChipData: identity + metadata for a single inline chip.
// KindKey is the string identity (H6). InlineChipTracker removed in Wave 0 (replaced by InlineChipModel).
using System;
using System.Globalization;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Identity + metadata for a single inline chip. KindKey is the string identity (H6).</summary>
    public readonly struct ChipData
    {
        public readonly string KindKey;
        public readonly string Path;
        public readonly string DisplayName;
        public readonly string ObjectId;
        public readonly GlobalObjectId GlobalObjectId;

        public ChipData(string kindKey, string path, string displayName, string objectId,
            GlobalObjectId globalObjectId = default)
        {
            KindKey     = kindKey     ?? ChipKindKeys.Asset;
            Path        = path        ?? "";
            DisplayName = displayName ?? "";
            ObjectId    = objectId ?? "";
            GlobalObjectId = globalObjectId;
        }

        /// <summary>Compatibility constructor for legacy signed instance ID callers.</summary>
        public ChipData(string kindKey, string path, string displayName, int legacyId,
            GlobalObjectId globalObjectId = default)
            : this(kindKey, path, displayName,
                legacyId == 0 ? "" : legacyId.ToString(CultureInfo.InvariantCulture), globalObjectId)
        {
        }
    }
}
