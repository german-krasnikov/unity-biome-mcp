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
            var ss = MCPEditorUtils.LoadStyleSheet("MCPHub.uss");
            if (ss != null) root.styleSheets.Add(ss);
            var settingsSs = MCPEditorUtils.LoadStyleSheet("MCPSettings.uss");
            if (settingsSs != null) root.styleSheets.Add(settingsSs);
            var arcadeSs = MCPEditorUtils.LoadStyleSheet("ArcadeAnim.uss");
            if (arcadeSs != null) root.styleSheets.Add(arcadeSs);
            root.AddToClassList("hub-root");

            var nav = new SettingsNavController(root);

            var home = new VisualElement();
            home.Add(HubHeaderAnim.Build(root));
            home.Add(BuildGeneralSection());
            home.Add(MCPHubDivider.Build(root));
            home.Add(HubCardButton.Build("⚙",  "Tools",        "Enable / disable MCP tools",
                () => nav.Push(SettingsPageFactory.BuildToolsPage(() => nav.Pop()))));
            if (PluginRegistry.All.Any(p => p.HasSettingsUI))
                home.Add(HubCardButton.Build("🧩", "Plugins", "Installed plugin settings",
                    () => nav.Push(SettingsPageFactory.BuildPluginsPage(() => nav.Pop()))));
            home.Add(HubCardButton.Build("🔒", "Permissions",   "Agent tool deny-set",
                () => nav.Push(SettingsPageFactory.BuildPermissionsPage(() => nav.Pop()))));
            home.Add(HubCardButton.Build("💬", "Chat Settings",  ChatCardSubtitle(),
                () => nav.Push(SettingsPageFactory.BuildChatPage(() => nav.Pop()))));
            home.Add(HubCardButton.Build("🧠", "LLM Sampling",  "Claude / Codex presets",
                () => nav.Push(SettingsPageFactory.BuildSamplingPage(() => nav.Pop()))));
            home.Add(HubCardButton.Build("🔄", "Updates",
                UpdateChecker.HasUpdate ? $"v{UpdateChecker.AvailableVersion} available" : "Check for updates",
                () => nav.Push(SettingsPageFactory.BuildUpdatesPage(() => nav.Pop()))));
            home.Add(HubCardButton.Build("⏪", "Version Picker",
                "Roll back to any release",
                () => nav.Push(SettingsPageFactory.BuildVersionPickerPage(() => nav.Pop()))));
            home.Add(MCPHubDivider.Build(root));

            var cards = new List<VisualElement>();
            for (int i = 0; i < home.childCount; i++)
            {
                var child = home.ElementAt(i);
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

            var levelNames = new List<string> { "Normal", "Permissive", "Strict" };
            var secLevel = new DropdownField("Security Level", levelNames, (int)MCPSettings.GetSecurityLevel());
            secLevel.tooltip = "Normal: type-info reflection allowed. Permissive: all reflection. Strict: no reflection.";
            secLevel.AddToClassList("hub-port-label");
            secLevel.RegisterValueChangedCallback(e =>
                MCPSettings.SetSecurityLevel((SecurityLevel)System.Enum.Parse(typeof(SecurityLevel), e.newValue)));
            section.Add(secLevel);

            var restartWarning = new Label("Restart required to apply") { visible = false };
            restartWarning.AddToClassList("hub-port-restart-warning");
            section.Add(restartWarning);

            portField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535) { portField.SetValueWithoutNotify(e.previousValue); return; }
                if (v == chatPortField.value) { portField.SetValueWithoutNotify(e.previousValue); return; }
                MCPServer.SavePorts(v, chatPortField.value);
                restartWarning.visible = (v != MCPServer.ServerPort || chatPortField.value != MCPServer.ServerChatPort);
            });

            chatPortField.RegisterValueChangedCallback(e =>
            {
                var v = e.newValue;
                if (v < 1024 || v > 65535) { chatPortField.SetValueWithoutNotify(e.previousValue); return; }
                if (v == portField.value) { chatPortField.SetValueWithoutNotify(e.previousValue); return; }
                MCPServer.SavePorts(portField.value, v);
                restartWarning.visible = (portField.value != MCPServer.ServerPort || v != MCPServer.ServerChatPort);
            });

            return section;
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
