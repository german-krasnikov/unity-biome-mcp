namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Cross-assembly seam: lets PerfWindow (UnityMCP.Editor) signal
    /// MCPChatWindow (UnityMCP.Editor.Chat.View) without a direct reference.
    /// Forwards through ChatHostBridge.Current — null until Chat.View's
    /// ChatHostBridgeImpl registers on domain reload.
    /// </summary>
    internal static class ProfileContextBridge
    {
        internal static void RequestAnalyzeInChat(string sessionId)
            => ChatHostBridge.Current?.OnAnalyzeInChatRequested(sessionId);
    }
}
