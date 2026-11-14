// Fills the Chat settings nav page with connection content (binary, auth, backends,
// chips) — no outer Foldout wrapper. Called directly by ChatHostBridgeImpl.BuildSettingsUI;
// no longer self-registering since ChatHostBridgeImpl owns the [InitializeOnLoad] seam.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatConnectionSection
    {
        internal static void Build(VisualElement root) => ChatSettingsSection.BuildContent(root);
    }
}
