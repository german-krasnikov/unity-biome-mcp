using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Typed seam for the few places Core needs to call into Chat (settings UI,
    /// binary availability, relay lifecycle, profile-chip injection) without a
    /// compile-time Core→Chat reference. Implemented by Chat.View's
    /// ChatHostBridgeImpl and registered on <see cref="ChatHostBridge"/>.
    /// </summary>
    internal interface IChatHostBridge
    {
        void BuildSettingsUI(VisualElement root);
        bool IsBinaryAvailable();
        void StopRelay();
        void OnAnalyzeInChatRequested(string sessionId);
    }
}
