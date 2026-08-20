using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    internal static class AgentConfigSettingsPanel
    {
        internal static VisualElement Build()
        {
            var root = new VisualElement();
            var enabledKeys = AgentConfigPrefs.GetEnabledKeys();
            var toggles = new List<(Toggle toggle, string key)>();

            foreach (var d in BackendDescriptor.All)
            {
                if (!d.AutoProjectConfig) continue;
                var toggle = new Toggle(d.DisplayName) { value = enabledKeys.Contains(d.Key) };
                toggle.RegisterValueChangedCallback(_ => OnToggleChanged(toggles));
                root.Add(toggle);
                toggles.Add((toggle, d.Key));
            }

            return root;
        }

        private static void OnToggleChanged(List<(Toggle toggle, string key)> toggles)
        {
            AgentConfigPrefs.SetEnabledKeys(
                toggles.Where(t => t.toggle.value).Select(t => t.key));
        }
    }
}
