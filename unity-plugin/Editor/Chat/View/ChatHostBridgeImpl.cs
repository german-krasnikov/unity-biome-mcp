// Chat.View implementation of the Core→Chat typed seam (Task 3.1, sub-tasks 2.2/2.3
// of Plans/task-phase2-chat-plugin.md). Registers itself on every domain reload —
// this is the single registration point for ChatHostBridge.Current.
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ChatHostBridgeImpl : IChatHostBridge
    {
        static ChatHostBridgeImpl() => ChatHostBridge.Current = new ChatHostBridgeImpl();

        public void BuildSettingsUI(VisualElement root) => ChatConnectionSection.Build(root);
        public bool IsBinaryAvailable() => ChatBinaryResolver.Resolve(false) != null;

        public void StopRelay() => RelaySpawner.Stop();

        public void OnAnalyzeInChatRequested(string sessionId)
        {
            var win = EditorWindow.GetWindow<MCPChatWindow>($"{BiomeLabel.DisplayName} Chat");
            win.Focus();
            win.InsertProfileChip(sessionId);
        }
    }
}
