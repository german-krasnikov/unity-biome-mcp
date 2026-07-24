using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPEditorUtils
    {
        internal static StyleSheet LoadStyleSheet(string filename)
        {
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"Packages/com.unity-biome-mcp.editor/Editor/{filename}");
            if (ss != null) return ss;
            return AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"Assets/../Packages/com.unity-biome-mcp.editor/Editor/{filename}");
        }
    }
}
