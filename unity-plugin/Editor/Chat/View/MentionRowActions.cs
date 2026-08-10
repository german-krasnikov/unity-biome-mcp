// Static helpers for @-mention popup right-click context menu actions.
// All methods are main-thread safe (called from UI event callbacks).
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class MentionRowActions
    {
        /// <summary>Copies the chip bracket-ref format to the system clipboard.</summary>
        internal static void CopyRef(MentionCandidate c)
        {
            EditorGUIUtility.systemCopyBuffer =
                ChipContextResolver.FormatChipRef(c.Chip.KindKey, c.Chip.Path, c.Chip.ObjectId);
        }

        /// <summary>Selects and pings a hierarchy chip's GameObject. No-op for asset chips.</summary>
        internal static void PingInHierarchy(MentionCandidate c)
        {
            var go = SceneObjectFinder.FindGameObject(c.Chip.Path);
            if (go == null)
            {
                Debug.LogWarning(BiomeLabel.Tag + " Reference stale: " + c.Chip.Path);
                return;
            }
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }

        /// <summary>Pings an asset chip in the Project window. No-op for hierarchy chips.</summary>
        internal static void PingInProject(MentionCandidate c)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(c.Chip.Path);
            if (obj == null)
            {
                Debug.LogWarning(BiomeLabel.Tag + " Asset not found: " + c.Chip.Path);
                return;
            }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }

        /// <summary>Returns true when the chip represents a scene hierarchy object.</summary>
        internal static bool IsHierarchyChip(MentionCandidate c)
            => c.Chip.KindKey == ChipKindKeys.Hierarchy;
    }
}
