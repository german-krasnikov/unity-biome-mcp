// Partial: inline chip methods — simplified with InlineChipField (Wave 0).
// F13→F14: @mention injected into TextField for visual display.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private InlineChipField _chipField;

        internal void InsertInlineChip(GameObject go)
        {
            if (go == null) return;
            var path = ComponentSerializer.GetPath(go);
            InsertInlineChip(go, path, go.name);
        }

        internal void InsertInlineChip(Object cap, string path, string displayName)
        {
            if (string.IsNullOrEmpty(path)) return;
            var assetPath  = cap != null ? AssetDatabase.GetAssetPath(cap) : path;
            var kindKey    = ChipKindDetector.Detect(cap, assetPath ?? path);
            var objectId   = cap != null ? TransientObjectId.GetHexRef(cap) : "";

            _chipField?.AddChip(new ChipData(kindKey, path, displayName, objectId));

            UpdateAutoHeight();
        }

        private void AddRefToContext(string refPath)
        {
            if (string.IsNullOrEmpty(refPath)) return;
            var display = refPath.Contains("/")
                ? refPath.Substring(refPath.LastIndexOf('/') + 1)
                : refPath;
            InsertInlineChip(null, refPath, display);
        }
    }
}
