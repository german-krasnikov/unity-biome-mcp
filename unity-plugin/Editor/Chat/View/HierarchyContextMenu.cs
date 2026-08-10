using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public static class HierarchyContextMenu
    {
        [MenuItem("GameObject/🧬MCP/Add to Chat", false, 10)]
        private static void Execute()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;
            var window = FindChatWindow();
            if (window == null)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Open the Chat window first.");
                return;
            }
            window.InsertInlineChip(go);
        }

        [MenuItem("GameObject/🧬MCP/Add to Chat", true)]
        private static bool Validate()
            => Selection.activeGameObject != null && FindChatWindow() != null;

        internal static MCPChatWindow FindChatWindow()
        {
            return FindChatWindow(Resources.FindObjectsOfTypeAll<MCPChatWindow>());
        }

        internal static MCPChatWindow FindChatWindow(MCPChatWindow[] windows)
        {
            return windows.Length > 0 ? windows[0] : null;
        }
    }
}
