// Agent Tool Permissions section for MCP Settings window.
// Hosts PermCategoryGroup foldouts backed by PermissionConfig (shared EditorPrefs prefix).
// Always shown — no #if guard — lets users pre-configure before enabling Agent Chat.
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPSettingsPermUI
    {
        public static VisualElement BuildSection()
        {
            var foldout = new Foldout { text = "Agent Tool Permissions" };
            foldout.AddToClassList("category-foldout");
            foldout.value = false;
            foldout.Add(BuildCore(new PermissionConfig()));
            return foldout;
        }

        /// <summary>
        /// Standalone version without outer Foldout — for settings nav page.
        /// </summary>
        public static VisualElement BuildContent(PermissionConfig config)
        {
            return BuildCore(config);
        }

        private static VisualElement BuildCore(PermissionConfig config)
        {
            var groups = new List<PermCategoryGroup>();

            var container = new VisualElement();
            container.AddToClassList("permission-content");
            var summary = BiomeUI.StatusLabel();

            void Refresh()
            {
                foreach (var group in groups)
                    group.Refresh();
                var states = config.GetToolStates();
                int allowed = states.Count(state => state.allowed);
                BiomeUI.SetStatus(
                    summary,
                    $"{allowed} of {states.Count} tools allowed for the in-Unity agent.",
                    allowed == states.Count ? "warning" : "neutral");
            }

            var presetRow = new VisualElement();
            presetRow.AddToClassList("preset-row");
            AddPresetBtn(presetRow, "Allow All", () =>
            {
                config.AllowAll();
                Refresh();
            });
            AddPresetBtn(presetRow, "Deny All", () =>
            {
                config.DenyAll();
                Refresh();
            });
            container.Add(presetRow);
            container.Add(summary);

            var search = new TextField("Search") { tooltip = "Filter tools by name" };
            search.AddToClassList("search-field");
            container.Add(search);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("tool-scroll");
            container.Add(scroll);

            BuildGroups(scroll, config, groups, Refresh);

            search.RegisterValueChangedCallback(evt =>
            {
                var q = evt.newValue.Trim();
                foreach (var g in groups) g.Filter(q);
            });

            Refresh();
            return container;
        }

        private static void BuildGroups(
            ScrollView scroll,
            PermissionConfig config,
            List<PermCategoryGroup> groups,
            System.Action onChanged)
        {
            var byCategory = config.GetToolStates()
                .GroupBy(s => s.category)
                .ToDictionary(g => g.Key, g => g.Select(s => s.toolName).ToArray());

            foreach (var kv in byCategory)
            {
                var group = new PermCategoryGroup(kv.Key, kv.Value, config, onChanged);
                scroll.Add(group.Element);
                groups.Add(group);
            }
        }

        private static void AddPresetBtn(VisualElement parent, string label, System.Action onClick)
        {
            var btn = BiomeUI.SecondaryButton(label, onClick);
            btn.AddToClassList("preset-btn");
            parent.Add(btn);
        }
    }
}
