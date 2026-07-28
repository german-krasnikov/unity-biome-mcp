using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// UIToolkit foldout with tri-state master toggle for a tool category.
    /// </summary>
    internal sealed class CategoryGroup
    {
        private readonly BiomeToggleGroup _group;
        public VisualElement Element => _group.Element;

        public CategoryGroup(string category, string[] tools)
        {
            _group = new BiomeToggleGroup(
                category,
                tools,
                MCPSettings.IsToolEnabled,
                SetToolEnabled,
                enabled =>
                {
                    foreach (string tool in tools)
                        SetToolEnabled(tool, enabled);
                },
                readOnly: category == "CORE");
        }

        public void Refresh() => _group.Refresh();
        public void Filter(string query) => _group.Filter(query);

        private static void SetToolEnabled(string tool, bool enabled)
        {
            EditorPrefs.SetBool(MCPSettings.KeyPrefix + tool, enabled);
            CommandRouter.InvalidateEnabledToolsCache();
        }
    }
}
