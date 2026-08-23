using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>Builds the tools section (presets, search, categories) for the Settings Hub.</summary>
    internal static class MCPSettingsUI
    {
        // ── Entry point ───────────────────────────────────────────────────────
        public static void BuildToolsSection(VisualElement root)
        {
            var allGroups = new List<CategoryGroup>();
            var summary = BiomeUI.StatusLabel();

            void RefreshVisibleState()
            {
                foreach (var group in allGroups)
                    group.Refresh();
                int total = MCPSettings.GetToolNames().Length;
                int enabled = MCPSettings.GetToolNames().Count(MCPSettings.IsToolEnabled);
                BiomeUI.SetStatus(
                    summary,
                    $"{enabled} of {total} tools enabled. Changes apply on next {BiomeLabel.DisplayName} reconnect.",
                    "neutral");
            }

            root.Add(BuildPresets(RefreshVisibleState));
            root.Add(summary);

            var emojiToggle = new Toggle("Use emoji label") { value = BiomeLabel.UseEmoji };
            emojiToggle.tooltip = "Toggle between emoji and text in log tags and UI labels. Window titles refresh on reopen.";
            emojiToggle.RegisterValueChangedCallback(evt => BiomeLabel.UseEmoji = evt.newValue);
            root.Add(emojiToggle);

            var mmModeToggle = new Toggle("Mutation Mode (experimental)")
            {
                value = MCPSettings.GetMutationMode()
            };
            mmModeToggle.tooltip =
                "Enable when SingularityGroup Hot Reload package is installed. " +
                "Skips Refresh() calls that HR patches to no-ops. Default: OFF.";
            mmModeToggle.RegisterValueChangedCallback(evt =>
            {
                EditorStateHelper.Control("mutation_mode", null,
                    $"{{\"enable\":\"{(evt.newValue ? "true" : "false")}\"}}");
                if (evt.newValue && !HotReloadDetector.IsPackageInstalled())
                    Debug.LogWarning("[MCP] Mutation Mode: Hot Reload package not installed. " +
                        "Static fields persist across Play sessions. " +
                        "Use [RuntimeInitializeOnLoadMethod(SubsystemRegistration)] to reset mutable runtime statics.");
            });
            root.Add(mmModeToggle);

            var searchField = new TextField();
            searchField.value = "";
            searchField.label = "Search";
            searchField.tooltip = "Filter tools by name";
            searchField.AddToClassList("search-field");
            root.Add(searchField);

            var categories = MCPSettings.GetCatalogCategories();

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

            RefreshVisibleState();
        }

        // ── Presets ───────────────────────────────────────────────────────────
        private static VisualElement BuildPresets(Action onApplied)
        {
            var row = new VisualElement();
            row.AddToClassList("preset-row");
            AddPresetButton(row, "Minimal",    ApplyMinimal, onApplied);
            AddPresetButton(row, "Full",       ApplyFull, onApplied);
            AddPresetButton(row, "No visuals", ApplyNoVisuals, onApplied);
            return row;
        }

        private static void AddPresetButton(
            VisualElement parent,
            string label,
            Action action,
            Action onApplied)
        {
            var btn = BiomeUI.SecondaryButton(label, () =>
            {
                action();
                onApplied?.Invoke();
            });
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
            new HashSet<string> { "MEDIA", "UGUI", "UITOOLKIT" };

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
