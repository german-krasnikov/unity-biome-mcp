using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal static class SceneAnnotationShortcut
    {
        [Shortcut("🧬MCP/Annotation Tool", typeof(SceneView), KeyCode.A, ShortcutModifiers.Shift)]
        static void Activate() => ToolManager.SetActiveTool<SceneAnnotationTool>();
    }
}
