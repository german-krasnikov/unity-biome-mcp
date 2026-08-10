using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Adds "Add Field to Chat" to every Component context menu in the Inspector.
    /// Shows a submenu of serialized fields; clicking one creates a [field:...] chip.
    /// </summary>
    internal static class FieldContextMenu
    {
        [MenuItem("CONTEXT/Component/🧬MCP/Add Field to Chat", true)]
        private static bool Validate(MenuCommand cmd) => cmd.context is Component;

        [MenuItem("CONTEXT/Component/🧬MCP/Add Field to Chat")]
        private static void Execute(MenuCommand cmd)
        {
            var comp = cmd.context as Component;
            if (comp == null) return;

            var so   = new SerializedObject(comp);
            var menu = new GenericMenu();
            var prop = so.GetIterator();
            bool hasFields = false;

            if (prop.NextVisible(true))
            {
                do
                {
                    if (prop.name == "m_Script") continue;
                    var fieldName   = prop.name;
                    var displayName = prop.displayName;
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        ChipPillFactory.AddChip(BuildChipData(comp, fieldName));
                    });
                    hasFields = true;
                }
                while (prop.NextVisible(false));
            }

            if (!hasFields) menu.AddDisabledItem(new GUIContent("No fields"));
            menu.ShowAsContext();
        }

        internal static ChipData BuildChipData(Component comp, string fieldName)
        {
            var path    = BuildFieldPath(comp, fieldName);
            var display = $"{comp.GetType().Name}.{fieldName}";
            return new ChipData(ChipKindKeys.Field, path, display, 0);
        }

        internal static string BuildFieldPath(Component comp, string fieldName)
        {
            var goPath = ComponentSerializer.GetPath(comp.gameObject);
            return $"{goPath}|{comp.GetType().Name}|{fieldName}";
        }
    }
}
