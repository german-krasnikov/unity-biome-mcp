// Public interface for extensible standalone EditorWindow panel registration.
// Implement and call PanelProviderRegistry.Register() from [InitializeOnLoad].

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Registers a standalone EditorWindow that integrates with the chat system.
    /// Implement and call PanelProviderRegistry.Register() from [InitializeOnLoad].
    /// </summary>
    public interface IPanelProvider
    {
        /// <summary>Unique lowercase key. Must match ^[a-z0-9_]+$.</summary>
        string Key { get; }

        /// <summary>e.g. "🧬MCP/My Panel"</summary>
        string MenuPath { get; }

        int MenuPriority { get; }
        string WindowTitle { get; }

        /// <summary>Implementation calls GetWindow&lt;T&gt;() internally.</summary>
        void Show();
    }
}
