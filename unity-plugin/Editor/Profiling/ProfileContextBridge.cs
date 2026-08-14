using System;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Cross-assembly event seam: lets PerfWindow (UnityMCP.Editor) signal
    /// MCPChatWindow (UnityMCP.Editor.Chat.View) without a direct reference.
    /// MCPChatWindow subscribes via [InitializeOnLoad].
    /// </summary>
    internal static class ProfileContextBridge
    {
        internal static event Action<string> AnalyzeInChatRequested;

        internal static void RequestAnalyzeInChat(string sessionId)
            => AnalyzeInChatRequested?.Invoke(sessionId);
    }
}
