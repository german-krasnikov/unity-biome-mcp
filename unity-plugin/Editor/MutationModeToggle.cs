using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>
    /// MCP Settings Hub UI shell for the "Mutation Mode (experimental)"
    /// checkbox (P2-04). Thin: polls live SourcePatchHost/SourcePatchModePolicy
    /// state, maps it through MutationModeToggleState.Resolve, and forwards
    /// the user's click to SourcePatchModePolicy.SetMutationIntent — the
    /// identical call target as CommandRouter.MediaHandlers.cs's
    /// editor(action="mutation_mode") handler. Never touches
    /// SourcePatchStateMachine/Coordinator/Host.Coordinator directly.
    /// </summary>
    internal static class MutationModeToggle
    {
        private const int PollIntervalMs = 600;
        private const string WarningState = "warning";

        internal static VisualElement Build()
        {
            var row = new VisualElement();

            var toggle = new Toggle("Mutation Mode (experimental)");
            toggle.AddToClassList("hub-port-label");
            row.Add(toggle);

            var warning = BiomeUI.StatusLabel();
            warning.visible = false;
            row.Add(warning);

            void Refresh()
            {
                var state = SourcePatchHost.CurrentState;
                var intentOn = SourcePatchModePolicy.IsIntentOn;
                var providerPresent = SourcePatchProviderSlot.TryGet(out _);
                var isPlaying = EditorApplication.isPlaying;

                var ui = MutationModeToggleState.Resolve(state, intentOn, providerPresent, isPlaying);

                toggle.SetValueWithoutNotify(ui.Checked);
                toggle.SetEnabled(ui.Enabled);
                toggle.tooltip = ui.Tooltip;

                warning.visible = ui.ShowRecoveryWarning;
                if (ui.ShowRecoveryWarning)
                    BiomeUI.SetStatus(warning, MutationModeToggleState.RecoveryWarningText, WarningState);
            }

            toggle.RegisterValueChangedCallback(e =>
            {
                ApplyIntent(e.newValue);
                Refresh();
            });

            Refresh();
            row.schedule.Execute(Refresh).Every(PollIntervalMs);

            return row;
        }

        /// <summary>Exposed as its own testable unit so tests never depend on
        /// UI Toolkit event dispatch on a detached (no-panel) VisualElement.
        /// Never rethrows: SourcePatchModePolicy's own zero-effect-on-throw
        /// contract means the checkbox simply reverts on the caller's next
        /// Refresh().</summary>
        internal static void ApplyIntent(bool enable)
        {
            try
            {
                SourcePatchModePolicy.SetMutationIntent(enable);
            }
            catch (InvalidOperationException e)
            {
                UnityEngine.Debug.LogWarning($"Mutation Mode toggle: {e.Message}");
            }
        }
    }
}
