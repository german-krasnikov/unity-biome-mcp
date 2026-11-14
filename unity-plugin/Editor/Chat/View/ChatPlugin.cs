// First production IMCPPlugin registrant (Plans/task-phase2-chat-plugin.md sub-task 4 /
// Phase 2 sub-task 2.4). RegisterCommands() is intentionally empty: Chat uses its own TCP
// listener (main port + 1 / UNITY_MCP_CHAT_PORT, tcp-bridge.md), not CommandRegistry -- grep
// confirms zero CommandRegistry.Register("chat...") call sites exist today. If a future task
// adds main-port chat commands, they register here. OnDomainReload() is intentionally empty:
// the relay lifecycle is already owned by ReloadLifecycleCoordinator sinks (Task 1.3).
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChatPlugin : IMCPPlugin
    {
        public string Name => "Chat";
        public string CommandPrefix => "chat";
        public void RegisterCommands() { }
        public void OnDomainReload() { }

        public VisualElement BuildSettingsUI()
        {
            var root = new VisualElement();
            ChatConnectionSection.Build(root);
            return root;
        }

        public bool HasSettingsUI => true;
        public string Description => "In-Unity chat (Claude, Codex, Gemini, and other CLI backends).";
    }

    [InitializeOnLoad]
    internal static class ChatPluginBootstrap
    {
        static ChatPluginBootstrap() => PluginRegistry.Register(new ChatPlugin());
    }
}
