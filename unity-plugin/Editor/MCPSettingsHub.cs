using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    public class MCPSettingsHub : EditorWindow
    {
        [MenuItem("🧬MCP/Settings", priority = 2)]
        public static void ShowWindow()
        {
            var w = GetWindow<MCPSettingsHub>($"{BiomeLabel.DisplayName} Settings");
            w.minSize = new Vector2(360, 420);
        }

        private void CreateGUI()
        {
            MCPHubUI.Build(rootVisualElement);
        }
    }
}
