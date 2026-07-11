// Adds "Aliases" button to the MCPChatWindow footer toolbar.
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class PlaytestAliasButton : IToolbarButtonProvider
    {
        public string Key         => "playtest_alias";
        public int    Order       => 21;
        public string ButtonLabel => "Aliases";
        public string Tooltip     => "Open Alias Manager (MCP/Alias Manager or Shift+Alt+A)";
        public bool   MenuOnly    => true;

        static PlaytestAliasButton()
            => ToolbarButtonRegistry.Register(new PlaytestAliasButton());

        public void OnClick(EditorWindow _)
            => PlaytestAliasWindow.Open();
    }
}
