// Builds the connection-policy settings VisualElement for the status window.
// Controls sync to EditorPrefs and call SaveAction (seam for testing) on change.
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class ConnectionPolicyUI
    {
        // Test seam — replace with a lambda spy in tests.
        internal static Action SaveAction = GlobalConfigSync.SaveToDisk;

        internal static VisualElement Build()
        {
            var root = new VisualElement();

            // 1. Show last command toggle
            var showCmd = MakeToggle("Show last command", PrefKeys.ShowLastCommand, true);
            root.Add(showCmd);

            // 2. Auto-suspend toggle + idle timeout field (field disabled when off)
            var idleTimeout = MakeIntField("Idle timeout (min)", PrefKeys.IdleTimeoutMin, 30, 5, 1440);
            var autoSuspend = MakeToggle("Auto-suspend on idle", PrefKeys.IdleAutoSuspend, true,
                isOn => idleTimeout.SetEnabled(isOn));
            idleTimeout.SetEnabled(EditorPrefs.GetBool(PrefKeys.IdleAutoSuspend, true));
            root.Add(autoSuspend);
            root.Add(idleTimeout);

            // 3. Terminate-orphan toggle + grace field (field disabled when off)
            var graceField = MakeIntField("Orphan grace (min)", PrefKeys.OrphanGraceMin, 2, 0, 60);
            var termOrphan = MakeToggle("Terminate orphan bridges", PrefKeys.TerminateOrphan, true,
                isOn => graceField.SetEnabled(isOn));
            graceField.SetEnabled(EditorPrefs.GetBool(PrefKeys.TerminateOrphan, true));
            root.Add(termOrphan);
            root.Add(graceField);

            return root;
        }

        // ── private builders ─────────────────────────────────────────────────

        private static Toggle MakeToggle(string label, string prefKey, bool def,
            Action<bool> onChanged = null)
        {
            var t = new Toggle(label) { value = EditorPrefs.GetBool(prefKey, def) };
            t.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(prefKey, evt.newValue);
                onChanged?.Invoke(evt.newValue);
                SaveAction?.Invoke();
            });
            return t;
        }

        private static IntegerField MakeIntField(string label, string prefKey, int def,
            int min, int max)
        {
            var f = new IntegerField(label) { value = EditorPrefs.GetInt(prefKey, def) };
            f.RegisterValueChangedCallback(evt =>
            {
                var clamped = Math.Max(min, Math.Min(max, evt.newValue));
                EditorPrefs.SetInt(prefKey, clamped);
                if (clamped != evt.newValue) f.SetValueWithoutNotify(clamped);
                SaveAction?.Invoke();
            });
            return f;
        }
    }
}
