using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Owns the full lifecycle of enterPlayModeOptionsEnabled + DisableDomainReload on behalf of MCP.
    /// Supports multiple independent owners (User, Mutation) — settings are restored only when the
    /// last owner releases. All EditorSettings writes go through test seams — zero test pollution.
    /// </summary>
    [Flags]
    internal enum FastPlayOwner { None = 0, User = 1, Mutation = 2 }

    internal static class FastPlayMode
    {
        const string KeyOwners      = "MCP_FPM_Owners";
        const string KeyOrigEnabled = "MCP_FPM_OrigEnabled";
        const string KeyOrigOptions = "MCP_FPM_OrigOptions";

        // ── Test seams ──────────────────────────────────────────────────────
        private static void SetEnabledDefault(bool v) => EditorSettings.enterPlayModeOptionsEnabled = v;
        private static void SetOptionsDefault(EnterPlayModeOptions v) => EditorSettings.enterPlayModeOptions = v;
        private static bool GetEnabledDefault() => EditorSettings.enterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions GetOptionsDefault() => EditorSettings.enterPlayModeOptions;

        internal static Action<bool> _setEnabled = SetEnabledDefault;
        internal static Action<EnterPlayModeOptions> _setOptions = SetOptionsDefault;
        internal static Func<bool> _getEnabled = GetEnabledDefault;
        internal static Func<EnterPlayModeOptions> _getOptions = GetOptionsDefault;

        internal static void RestoreDefaultSeams()
        {
            _setEnabled = SetEnabledDefault;
            _setOptions = SetOptionsDefault;
            _getEnabled = GetEnabledDefault;
            _getOptions = GetOptionsDefault;
        }

        // ── State ────────────────────────────────────────────────────────────
        internal static bool IsApplied => GetOwners() != FastPlayOwner.None;

        private static FastPlayOwner GetOwners() =>
            (FastPlayOwner)SessionState.GetInt(KeyOwners, 0);

        private static void SetOwners(FastPlayOwner v) =>
            SessionState.SetInt(KeyOwners, (int)v);

        // ── API ──────────────────────────────────────────────────────────────
        internal static void Apply(FastPlayOwner owner = FastPlayOwner.User)
        {
            var current = GetOwners();
            if (current.HasFlag(owner)) return; // already owned by this owner

            bool wasEmpty = current == FastPlayOwner.None;
            SetOwners(current | owner);

            if (wasEmpty) // first owner — snapshot + write settings
            {
                bool originalEnabled = _getEnabled();
                var originalOptions = _getOptions();
                // Read options BEFORE setEnabled — Unity 6 may inject defaults (mask=3) as a side-effect.
                // If options were disabled, use None as base so we don't inherit dormant Unity bits.
                var desiredOptions =
                    (originalEnabled ? originalOptions : EnterPlayModeOptions.None) |
                    EnterPlayModeOptions.DisableDomainReload;
                SessionState.SetBool(KeyOrigEnabled, originalEnabled);
                SessionState.SetInt(KeyOrigOptions, (int)originalOptions);
                _setEnabled(true);
                _setOptions(desiredOptions);
                MCPSettings.SetFastPlayMode(true);
                Debug.Log("[MCP] Fast Play Mode enabled (DisableDomainReload)");
            }
        }

        internal static void Restore(FastPlayOwner owner = FastPlayOwner.User)
        {
            var current = GetOwners();
            if (!current.HasFlag(owner)) return; // not owned by this owner

            SetOwners(current & ~owner);

            if (GetOwners() == FastPlayOwner.None) // last owner released
            {
                bool origEnabled = SessionState.GetBool(KeyOrigEnabled, false);
                var  origOptions = (EnterPlayModeOptions)SessionState.GetInt(KeyOrigOptions, 0);
                _setEnabled(origEnabled);
                _setOptions(origOptions);
                SessionState.EraseBool(KeyOrigEnabled);
                SessionState.EraseInt(KeyOrigOptions);
                MCPSettings.SetFastPlayMode(false);
                Debug.Log("[MCP] Fast Play Mode restored");
            }
        }

        // ── Test support ─────────────────────────────────────────────────────
        internal static void ResetForTest()
        {
            SessionState.EraseInt(KeyOwners);
            SessionState.EraseBool(KeyOrigEnabled);
            SessionState.EraseInt(KeyOrigOptions);
        }
    }
}
