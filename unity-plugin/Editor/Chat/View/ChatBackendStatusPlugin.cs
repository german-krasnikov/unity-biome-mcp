// Wires ChatBackendProbe.RunningProvider to the live MCPChatWindow status check, without
// giving Core a compile-time dependency on Chat.View. Mirrors SearchContextPlugin.cs (Chat.CLI)
// exactly: an [InitializeOnLoad] static ctor re-runs on every domain reload, so no reset
// logic is needed between reloads (PR-05 05.1).
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class ChatBackendStatusPlugin
    {
        static ChatBackendStatusPlugin()
        {
            ChatBackendProbe.RunningProvider = MCPChatWindow.IsChatBackendRunning;
        }
    }
}
