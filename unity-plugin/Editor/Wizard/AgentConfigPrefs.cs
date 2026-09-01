using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Wizard
{
    internal static class AgentConfigPrefs
    {
        internal static bool IsFirstRun =>
            !EditorPrefs.HasKey(PrefKeys.EnabledAgentConfigs);

        internal static HashSet<string> GetEnabledKeys()
        {
            var raw = EditorPrefs.GetString(PrefKeys.EnabledAgentConfigs, "");
            if (string.IsNullOrEmpty(raw)) return new HashSet<string>();
            return new HashSet<string>(raw.Split(','));
        }

        internal static void SetEnabledKeys(IEnumerable<string> keys)
        {
            EditorPrefs.SetString(PrefKeys.EnabledAgentConfigs, string.Join(",", keys));
        }

        internal static void InitializeFromDetected(IEnumerable<string> detectedKeys)
        {
            SetEnabledKeys(detectedKeys);
        }

        // descriptors/dirExists: injected by tests; null means production defaults
        // (BackendDescriptor.All / Directory.Exists). A backend with no ConfigDir is
        // never auto-enabled — it falls through without ever consulting dirExists.
        internal static IEnumerable<string> DetectInstalled(
            BackendDescriptor[] descriptors = null, Func<string, bool> dirExists = null)
        {
            descriptors ??= BackendDescriptor.All;
            dirExists ??= Directory.Exists;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var detected = new List<string>();
            foreach (var d in descriptors)
            {
                if (!d.AutoProjectConfig) continue;
                if (string.IsNullOrEmpty(d.ConfigDir)) continue;
                var expanded = d.ConfigDir.Replace("~", home);
                if (dirExists(expanded)) detected.Add(d.Key);
            }
            if (detected.Count == 0) detected.Add("claude-code");
            return detected;
        }
    }
}
