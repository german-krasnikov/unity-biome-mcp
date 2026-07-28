// UIToolkit tri-state foldout group for MCPSettingsPermUI and Chat permission views.
// Mirrors MCPSettingsCategoryGroup visual structure via the same USS classes from MCPSettings.uss.
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Foldout with per-tool toggles and a tri-state master toggle.
    /// Reads/writes via <see cref="PermissionConfig"/> (not EditorPrefs directly).
    /// Reuses MCPSettings.uss class names so it looks identical to the Settings window.
    /// </summary>
    internal sealed class PermCategoryGroup
    {
        private readonly BiomeToggleGroup _group;
        public VisualElement Element => _group.Element;

        public PermCategoryGroup(
            string category,
            string[] tools,
            PermissionConfig config,
            System.Action onChanged = null)
        {
            _group = new BiomeToggleGroup(
                category,
                tools,
                config.IsToolAllowed,
                config.SetToolAllowed,
                allowed => config.SetCategoryAllowed(category, allowed),
                onChanged: onChanged);
        }

        public void Refresh() => _group.Refresh();
        public void Filter(string query) => _group.Filter(query);
    }
}
