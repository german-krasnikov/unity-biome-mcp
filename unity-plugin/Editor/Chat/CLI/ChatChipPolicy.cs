// Pure type-allowlist with per-type EditorPrefs gating.
// Determines which dragged asset types are accepted as context chips.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatChipPolicy
    {
        internal static string PrefKey(string typeName) => $"MCPChat.ChipAllow.{typeName}";

        internal static bool IsTypeEnabled(string typeName)
            => EditorPrefs.GetBool(PrefKey(typeName), true);

        /// <summary>
        /// Returns true if the asset type is allowed as a context chip.
        /// Rejects folders (DefaultAsset), null, and unlisted types.
        /// Per-type can be disabled via EditorPrefs "MCPChat.ChipAllow.{TypeName}".
        /// </summary>
        internal static bool IsAllowedAssetType(Type t)
        {
            if (t == null)                         return false;
            if (t == typeof(DefaultAsset))         return false; // folders
            if (t == typeof(GameObject))           return IsTypeEnabled("GameObject");
            if (t == typeof(Material))             return IsTypeEnabled("Material");
            if (typeof(Texture).IsAssignableFrom(t))          return IsTypeEnabled("Texture");
            if (t == typeof(AnimationClip))                    return IsTypeEnabled("AnimationClip");
            if (t == typeof(MonoScript))                       return IsTypeEnabled("MonoScript");
            if (t == typeof(Mesh))                             return IsTypeEnabled("Mesh");
            if (t == typeof(AudioClip))                        return IsTypeEnabled("AudioClip");
            if (typeof(ScriptableObject).IsAssignableFrom(t)) return IsTypeEnabled("ScriptableObject");
            if (t == typeof(SceneAsset)) return IsTypeEnabled("SceneAsset");
            return false;
        }
    }
}
