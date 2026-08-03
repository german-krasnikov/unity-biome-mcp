using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ComponentContextMenu
    {
        [MenuItem("CONTEXT/Component/Add to Chat Context", true)]
        private static bool Validate(MenuCommand cmd)
            => (cmd.context as Component) != null && FindChatWindow() != null;

        [MenuItem("CONTEXT/Component/Add to Chat Context")]
        private static void Execute(MenuCommand cmd)
        {
            var component = cmd.context as Component;
            if (component == null) return;
            var window = FindChatWindow();
            if (window == null)
            {
                Debug.LogWarning("[MCP Chat] Open the Chat window first.");
                return;
            }
            window.InsertInlineChip(component.gameObject);
            if (component is MonoBehaviour mb)
            {
                var ms = MonoScript.FromMonoBehaviour(mb);
                if (ms != null)
                    window.InsertInlineChip(ms, AssetDatabase.GetAssetPath(ms), ms.name);
            }
        }

        internal static MCPChatWindow FindChatWindow() =>
            HierarchyContextMenu.FindChatWindow();

        internal static MCPChatWindow FindChatWindow(MCPChatWindow[] windows) =>
            HierarchyContextMenu.FindChatWindow(windows);
    }
}
