using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure view-model for the MCP Settings Hub "Mutation Mode (experimental)"
    /// checkbox (P2-04). Maps (state, intentOn, providerPresent, isPlaying) to
    /// (Checked, Enabled, Tooltip, ShowRecoveryWarning). Zero Unity API, zero
    /// side effects, zero calls into SourcePatchHost/SourcePatchModePolicy —
    /// intentOn is always the caller's live SourcePatchModePolicy.IsIntentOn
    /// value so this class never duplicates that formula.
    /// </summary>
    internal readonly struct MutationModeUiState
    {
        internal bool Checked { get; }
        internal bool Enabled { get; }
        internal string Tooltip { get; }
        internal bool ShowRecoveryWarning { get; }

        internal MutationModeUiState(bool @checked, bool enabled, string tooltip, bool showRecoveryWarning)
        {
            Checked = @checked;
            Enabled = enabled;
            Tooltip = tooltip;
            ShowRecoveryWarning = showRecoveryWarning;
        }
    }

    internal static class MutationModeToggleState
    {
        internal const string ProviderAbsentTooltip =
            "Mutation Mode provider package is not installed.";
        internal const string RecoveryTooltip =
            "Mutation Mode needs recovery and requires a domain reload before it can be used again.";
        internal const string BusyTooltip =
            "Mutation Mode is applying a change.";
        internal const string DisablingTooltip =
            "Mutation Mode is disabling.";
        internal const string PlayModeTooltip =
            "Mutation Mode cannot be changed while in Play Mode.";
        internal const string OffTooltip =
            "Enable Mutation Mode to apply .cs edits without waiting for a full script reload.";
        internal const string OnReadyTooltip =
            "Mutation Mode is on. Disabling triggers one script reload.";
        internal const string RecoveryWarningText =
            "Mutation Mode needs recovery — requires a domain reload.";

        /// <summary>Table order is precedence order — first match wins. See
        /// Plans/mutation-mode-hub-toggle.md's State-Mapping Table.</summary>
        internal static MutationModeUiState Resolve(
            SourcePatchState state, bool intentOn, bool providerPresent, bool isPlaying)
        {
            if (!providerPresent)
                return new MutationModeUiState(intentOn, false, ProviderAbsentTooltip, false);

            if (state == SourcePatchState.Recovery)
                return new MutationModeUiState(intentOn, false, RecoveryTooltip, true);

            if (state == SourcePatchState.Busy)
                return new MutationModeUiState(intentOn, false, BusyTooltip, false);

            if (state == SourcePatchState.Disabling)
                return new MutationModeUiState(intentOn, false, DisablingTooltip, false);

            if (isPlaying)
                return new MutationModeUiState(intentOn, false, PlayModeTooltip, false);

            if (state == SourcePatchState.OnReady)
                return new MutationModeUiState(true, true, OnReadyTooltip, false);

            return new MutationModeUiState(false, true, OffTooltip, false);
        }
    }
}
