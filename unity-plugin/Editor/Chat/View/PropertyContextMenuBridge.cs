using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Adds "Add to MCP Chat" and "Copy MCP Ref" to every serialized property's right-click
    /// menu in the Inspector (IMGUI path only). Supports Component and ScriptableObject targets.
    /// </summary>
    [InitializeOnLoad]
    internal static class PropertyContextMenuBridge
    {
        static PropertyContextMenuBridge()
            => EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;

#if UNITY_INCLUDE_TESTS
        internal static System.Func<ScriptableObject, string> GetAssetPathOverride;
        internal static System.Action OnItemAdded_TestSeam;
#endif

        internal static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
        {
            var chip = BuildChipForProperty(property);
            if (!chip.HasValue) return;
            var captured = chip.Value;

            menu.AddItem(new GUIContent("Add to MCP Chat"), false,
                () => ChipPillFactory.AddChip(captured));
#if UNITY_INCLUDE_TESTS
            OnItemAdded_TestSeam?.Invoke();
#endif
            menu.AddItem(new GUIContent("Copy MCP Ref"), false,
                () => CopyRefToClipboard(captured));
#if UNITY_INCLUDE_TESTS
            OnItemAdded_TestSeam?.Invoke();
#endif
        }

        internal static ChipData? BuildChipForProperty(SerializedProperty property)
        {
            if (property.name == "m_Script") return null;
            var target = property.serializedObject.targetObject;
            return target switch
            {
                Component comp      => FieldContextMenu.BuildChipData(comp, property.propertyPath),
                ScriptableObject so => BuildChipDataForSO(so, property.propertyPath),
                _                   => null
            };
        }

        private static ChipData? BuildChipDataForSO(ScriptableObject so, string propertyPath)
        {
            var assetPath = GetAssetPath(so);
            if (string.IsNullOrEmpty(assetPath)) return null;
            var path    = $"{assetPath}|{so.GetType().Name}|{propertyPath}";
            var display = $"{so.GetType().Name}.{propertyPath}";
            return new ChipData(ChipKindKeys.Field, path, display, 0);
        }

        private static string GetAssetPath(ScriptableObject so)
        {
#if UNITY_INCLUDE_TESTS
            if (GetAssetPathOverride != null) return GetAssetPathOverride(so);
#endif
            return AssetDatabase.GetAssetPath(so);
        }

        private static void CopyRefToClipboard(ChipData chip)
            => EditorGUIUtility.systemCopyBuffer = chip.Path;
    }
}
