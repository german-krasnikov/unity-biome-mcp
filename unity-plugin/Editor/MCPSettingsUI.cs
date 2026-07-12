using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Builds the tools section (presets, search, categories) for the Settings Hub.</summary>
    internal static class MCPSettingsUI
    {
        // ── Entry point ───────────────────────────────────────────────────────
        public static void BuildToolsSection(VisualElement root)
        {
            root.Add(BuildPresets());

            var info = new Label("Changes apply on next MCP reconnect.");
            info.AddToClassList("info-label");
            root.Add(info);

            var searchField = new TextField();
            searchField.value = "";
            searchField.tooltip = "Filter tools by name";
            searchField.AddToClassList("search-field");
            root.Add(searchField);

            var categories = MCPSettings.GetCatalogCategories();
            var allGroups = new List<CategoryGroup>();

            foreach (var kv in categories)
            {
                if (kv.Value == null || kv.Value.Length == 0) continue;
                var group = new CategoryGroup(kv.Key, kv.Value);
                root.Add(group.Element);
                allGroups.Add(group);
            }

            var pluginGroup = BuildPluginsSection(allGroups);
            if (pluginGroup != null) root.Add(pluginGroup);

            searchField.RegisterValueChangedCallback(evt =>
            {
                var q = evt.newValue.ToLowerInvariant().Trim();
                foreach (var g in allGroups) g.Filter(q);
            });
        }

        // ── Presets ───────────────────────────────────────────────────────────
        private static VisualElement BuildPresets()
        {
            var row = new VisualElement();
            row.AddToClassList("preset-row");
            AddPresetButton(row, "Minimal",    ApplyMinimal);
            AddPresetButton(row, "Full",       ApplyFull);
            AddPresetButton(row, "No-visuals", ApplyNoVisuals);
            return row;
        }

        private static void AddPresetButton(VisualElement parent, string label, Action action)
        {
            var btn = new Button(action) { text = label };
            btn.AddToClassList("preset-btn");
            parent.Add(btn);
        }

        private static void ApplyMinimal()
        {
            var cats = MCPSettings.GetCatalogCategories();
            var allTools = cats.SelectMany(kv => kv.Value).Distinct();
            var core = cats.TryGetValue("CORE", out var c) ? new HashSet<string>(c) : new HashSet<string>();
            foreach (var t in allTools)
                EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, core.Contains(t));
            CommandRouter.InvalidateEnabledToolsCache();
        }

        private static void ApplyFull()
        {
            var allTools = MCPSettings.GetToolNames();
            foreach (var t in allTools) EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, true);
            CommandRouter.InvalidateEnabledToolsCache();
        }

        private static readonly HashSet<string> _noVisualsOff =
            new HashSet<string> { "MEDIA" };

        private static void ApplyNoVisuals()
        {
            var cats = MCPSettings.GetCatalogCategories();
            foreach (var kv in cats)
            {
                bool off = _noVisualsOff.Contains(kv.Key);
                foreach (var t in kv.Value) EditorPrefs.SetBool(MCPSettings.KeyPrefix + t, !off);
            }
            CommandRouter.InvalidateEnabledToolsCache();
        }

        // ── Plugins section ───────────────────────────────────────────────────
        private static VisualElement BuildPluginsSection(List<CategoryGroup> allGroups)
        {
            var plugins = PluginRegistry.GetAll();
            if (plugins.Count == 0) return null;

            var section = new VisualElement();
            section.AddToClassList("plugin-section");
            var hdr = new Label("Plugins");
            hdr.AddToClassList("plugin-section-header");
            section.Add(hdr);

            foreach (var plugin in plugins)
            {
                var pluginTools = PluginRegistry.GetCommandsForPlugin(plugin);
                if (pluginTools.Length == 0) continue;

                var groups = PluginToolGrouping.GroupBySubcategory(plugin, pluginTools);
                foreach (var (subLabel, tools) in groups)
                {
                    var displayLabel = groups.Count == 1
                        ? subLabel
                        : $"{plugin.Name} / {subLabel}";
                    var group = new CategoryGroup(displayLabel, tools);
                    section.Add(group.Element);
                    allGroups.Add(group);  // wire into search filter
                }
            }
            return section;
        }

    }
}
