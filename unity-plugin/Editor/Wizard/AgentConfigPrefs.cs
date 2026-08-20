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

        internal static IEnumerable<string> DetectInstalled()
        {
            var detected = new List<string>();
            foreach (var d in BackendDescriptor.All)
            {
                if (!d.AutoProjectConfig) continue;
                if (string.IsNullOrEmpty(d.ConfigDir))
                {
                    detected.Add(d.Key);
                    continue;
                }
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var expanded = d.ConfigDir.Replace("~", home);
                if (Directory.Exists(expanded)) detected.Add(d.Key);
            }
            if (detected.Count == 0) detected.Add("claude-code");
            return detected;
        }
    }
}
