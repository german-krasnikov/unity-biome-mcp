// Adds "Composer" button to the MCPChatWindow footer toolbar.
// Registration is unconditional; the chat toolbar renders only when UNITY_MCP_CHAT is defined.
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class PlaytestComposerButton : IToolbarButtonProvider
    {
        public string Key         => "playtest_composer";
        public int    Order       => 20;
        public string ButtonLabel => "Composer";
        public string Tooltip     => "Open Playtest Composer (MCP/Playtest Composer or Shift+Alt+P)";
        public bool   MenuOnly    => true;

        static PlaytestComposerButton()
            => ToolbarButtonRegistry.Register(new PlaytestComposerButton());

        public void OnClick(EditorWindow _)
            => PlaytestComposerWindow.Open();
    }
}
