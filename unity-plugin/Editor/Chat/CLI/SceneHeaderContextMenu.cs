// Adds "🧬MCP/Copy Ref" to the right-click menu on loaded-scene headers in the Hierarchy.
// Uses hierarchyWindowItemOnGUI; Scene.GetHashCode() == scene.handle == instanceId for headers.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class SceneHeaderContextMenu
    {
        // Injectable seam — lets tests bypass real SceneManager.
        internal static Func<int, string> ScenePathFinder = FindScenePath;

        static SceneHeaderContextMenu()
        {
#if UNITY_6000_4_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItemGUIByEntityId;
#else
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItemGUI;
#endif
        }

#if UNITY_6000_4_OR_NEWER
        private static void OnHierarchyItemGUIByEntityId(EntityId entityId, Rect selectionRect)
            => OnHierarchyItemGUI(unchecked((int)EntityId.ToULong(entityId)), selectionRect);
#endif

        internal static void OnHierarchyItemGUI(int instanceId, Rect selectionRect)
        {
            var evt = Event.current;
            if (evt == null) return;
            if (evt.type != EventType.ContextClick) return;
            if (!selectionRect.Contains(evt.mousePosition)) return;

            var path = ScenePathFinder(instanceId);
            if (path == null) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("🧬MCP/Copy Ref"), false, () =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (asset != null) CopyAsMcpRef.CopySelection(new Object[] { asset });
            });
            menu.ShowAsContext();
            evt.Use();
        }

        // Scene.GetHashCode() returns scene.handle, which Unity uses as the instanceId
        // for scene header rows in the Hierarchy window.
        internal static string FindScenePath(int instanceId)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.GetHashCode() == instanceId && !string.IsNullOrEmpty(s.path))
                    return s.path;
            }
            return null;
        }
    }
}
