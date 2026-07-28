using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPHubUI
    {
        public static void Build(VisualElement root)
        {
            BiomeUI.LoadCoreStyles(root);
            root.AddToClassList("hub-root");

            var nav = new SettingsNavController(root);

            var home = new ScrollView(ScrollViewMode.Vertical);
            home.AddToClassList("biome-page-scroll");
            var content = home.contentContainer;
            content.AddToClassList("biome-page-content");

            content.Add(HubHeaderAnim.Build(home));
            content.Add(BuildGeneralSection());
            content.Add(MCPHubDivider.Build(home));
            content.Add(HubCardButton.Build("⚙",  "Tools",        "Enable / disable MCP tools",
                () => nav.Push(SettingsPageFactory.BuildToolsPage(() => nav.Pop()))));
            if (PluginRegistry.All.Any(p => p.HasSettingsUI))
                content.Add(HubCardButton.Build("🧩", "Plugins", "Installed plugin settings",
                    () => nav.Push(SettingsPageFactory.BuildPluginsPage(() => nav.Pop()))));
            content.Add(HubCardButton.Build("🔒", "Permissions",   "Agent tool deny-set",
                () => nav.Push(SettingsPageFactory.BuildPermissionsPage(() => nav.Pop()))));
            content.Add(HubCardButton.Build("💬", "Chat Settings",  ChatCardSubtitle(),
                () => nav.Push(SettingsPageFactory.BuildChatPage(() => nav.Pop()))));
            content.Add(HubCardButton.Build("🧠", "LLM Sampling",  "Claude / Codex presets",
                () => nav.Push(SettingsPageFactory.BuildSamplingPage(() => nav.Pop()))));
            content.Add(HubCardButton.Build("🔄", "Updates",
                UpdateChecker.HasUpdate ? $"v{UpdateChecker.AvailableVersion} available" : "Check for updates",
                () => nav.Push(SettingsPageFactory.BuildUpdatesPage(() => nav.Pop()))));
            content.Add(HubCardButton.Build("⏪", "Version Picker",
                "Roll back to any release",
                () => nav.Push(SettingsPageFactory.BuildVersionPickerPage(() => nav.Pop()))));
            content.Add(MCPHubDivider.Build(home));

            var cards = new List<VisualElement>();
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.ElementAt(i);
                if (child.ClassListContains("hub-card"))
                    cards.Add(child);
            }
            ArcadeAnim.StaggerFadeIn(cards, 60);

            nav.SetRoot(home);
        }

        private static VisualElement BuildGeneralSection()
        {
            var section = new VisualElement();
            section.AddToClassList("hub-section");

            var portField = new IntegerField("Port") { value = MCPServer.ServerPort };
            portField.AddToClassList("hub-port-label");
            section.Add(portField);

            var chatPortField = new IntegerField("Chat Port") { value = MCPServer.ServerChatPort };
            chatPortField.AddToClassList("hub-port-label");
            section.Add(chatPortField);

            var reloadPort = MCPServer.ServerReloadPort;
            if (reloadPort != 0)
            {
                var reloadPortField = new IntegerField("Reload Port") { value = reloadPort };
                reloadPortField.AddToClassList("hub-port-label");
                reloadPortField.SetEnabled(false);
                section.Add(reloadPortField);
            }

            var levelNames = new List<string> { "Standard", "Allow All", "Strict" };
            var secLevel = new DropdownField("Security Level", levelNames, (int)MCPSettings.GetSecurityLevel());
            secLevel.tooltip = "Standard: type-info reflection allowed. Allow All: all APIs (no scan). Strict: no reflection.";
            secLevel.AddToClassList("hub-port-label");
            secLevel.RegisterValueChangedCallback(e =>
                MCPSettings.SetSecurityLevel((SecurityLevel)levelNames.IndexOf(e.newValue)));
            section.Add(secLevel);

            var restartWarning = BiomeUI.StatusLabel();
            restartWarning.visible = false;
            restartWarning.AddToClassList("hub-port-restart-warning");
            section.Add(restartWarning);

            portField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535)
                {
                    portField.SetValueWithoutNotify(e.previousValue);
                    ShowPortStatus(restartWarning, "Port must be between 1024 and 65535.", "error");
                    ArcadeAnim.ShakeX(portField);
                    return;
                }
                if (v == chatPortField.value)
                {
                    portField.SetValueWithoutNotify(e.previousValue);
                    ShowPortStatus(restartWarning, "MCP and Chat ports must be different.", "error");
                    ArcadeAnim.ShakeX(portField);
                    return;
                }
                MCPServer.SavePorts(v, chatPortField.value);
                ShowPortStatus(restartWarning, "Saved. Restart the MCP server to apply port changes.", "warning");
            });

            chatPortField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535)
                {
                    chatPortField.SetValueWithoutNotify(e.previousValue);
                    ShowPortStatus(restartWarning, "Chat port must be between 1024 and 65535.", "error");
                    ArcadeAnim.ShakeX(chatPortField);
                    return;
                }
                if (v == portField.value)
                {
                    chatPortField.SetValueWithoutNotify(e.previousValue);
                    ShowPortStatus(restartWarning, "MCP and Chat ports must be different.", "error");
                    ArcadeAnim.ShakeX(chatPortField);
                    return;
                }
                MCPServer.SavePorts(portField.value, v);
                ShowPortStatus(restartWarning, "Saved. Restart the MCP server to apply port changes.", "warning");
            });

            return section;
        }

        private static void ShowPortStatus(Label label, string message, string state)
        {
            label.visible = true;
            BiomeUI.SetStatus(label, message, state);
        }

        // Sync read — no shell spawn: uses cached binary path + EditorPrefs auth key.
        private static string ChatCardSubtitle()
        {
            // TODO(F24d): refresh subtitle dynamically when auth probe completes
            if (!ChatSettingsHook.IsChatBinaryAvailable()) return "CLI not configured";
            var auth = EditorPrefs.GetString(PrefKeys.ChatAuthStatus, "");
            return auth == "ok"   ? "Claude CLI · logged in"
                 : auth == "fail" ? "Claude CLI · not logged in"
                 : "Claude CLI · checking...";
        }
    }
}
