using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class CopyAsMcpRef
    {
        [MenuItem("GameObject/🧬MCP/Copy Ref", false, 10)]
        private static void ExecuteHierarchy()
            => CopySelection(Selection.gameObjects);

        [MenuItem("GameObject/🧬MCP/Copy Ref", true)]
        private static bool ValidateHierarchy()
            => Selection.gameObjects.Length > 0;

        [MenuItem("Assets/🧬MCP/Copy Ref")]
        private static void ExecuteAssets()
            => CopySelection(Selection.objects);

        [MenuItem("Assets/🧬MCP/Copy Ref", true)]
        private static bool ValidateAssets()
        {
            foreach (var obj in Selection.objects)
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj))) return false;
            return Selection.objects.Length > 0;
        }

        [MenuItem("CONTEXT/Component/🧬MCP/Copy Ref")]
        private static void ExecuteComponent(MenuCommand cmd)
        {
            if (cmd.context != null)
                CopySelection(new Object[] { cmd.context });
        }

        [MenuItem("CONTEXT/Component/🧬MCP/Copy Ref", true)]
        private static bool ValidateComponent(MenuCommand cmd)
            => cmd.context as Component != null;

        internal static void CopySelection(Object[] objects)
        {
            var lines = new List<string>();
            foreach (var obj in objects)
            {
                var target = obj is Component c ? (Object)c.gameObject : obj;
                var r = ChipContextResolver.FormatAsRef(target);
                if (r != null) lines.Add(r);

                if (obj is MonoBehaviour mb)
                {
                    var ms = MonoScript.FromMonoBehaviour(mb);
                    if (ms != null)
                    {
                        var scriptRef = ChipContextResolver.FormatAsRef(ms);
                        if (scriptRef != null) lines.Add(scriptRef);
                    }
                }
            }
            if (lines.Count == 0) return;
            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
                sv.ShowNotification(new GUIContent($"Copied {lines.Count} ref(s)"), 1.5f);
            else if (!Application.isBatchMode)
                Debug.Log($"{BiomeLabel.Tag} Copied {lines.Count} ref(s): {string.Join(", ", lines)}");
        }
    }
}
