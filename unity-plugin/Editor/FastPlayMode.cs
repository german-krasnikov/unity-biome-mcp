using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Owns the full lifecycle of enterPlayModeOptionsEnabled + DisableDomainReload on behalf of MCP.
    /// All EditorSettings writes go through test seams — zero test pollution.
    /// </summary>
    internal static class FastPlayMode
    {
        const string KeyApplied     = "MCP_FPM_Applied";
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
        internal static bool IsApplied => SessionState.GetBool(KeyApplied, false);

        // ── API ──────────────────────────────────────────────────────────────
        internal static void Apply()
        {
            if (IsApplied) return;
            bool originalEnabled = _getEnabled();
            var originalOptions = _getOptions();
            // Read options BEFORE setEnabled — Unity 6 may inject defaults (mask=3) as a side-effect.
            // If options were disabled, use None as base so we don't inherit dormant Unity bits.
            var desiredOptions =
                (originalEnabled ? originalOptions : EnterPlayModeOptions.None) |
                EnterPlayModeOptions.DisableDomainReload;
            SessionState.SetBool(KeyOrigEnabled, originalEnabled);
            SessionState.SetInt(KeyOrigOptions, (int)originalOptions);
            SessionState.SetBool(KeyApplied, true);
            _setEnabled(true);
            _setOptions(desiredOptions);
            MCPSettings.SetFastPlayMode(true);
            Debug.Log("[MCP] Fast Play Mode enabled (DisableDomainReload)");
        }

        internal static void Restore()
        {
            if (!IsApplied) return;
            bool origEnabled = SessionState.GetBool(KeyOrigEnabled, false);
            var  origOptions = (EnterPlayModeOptions)SessionState.GetInt(KeyOrigOptions, 0);
            _setEnabled(origEnabled);
            _setOptions(origOptions);
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseBool(KeyOrigEnabled);
            SessionState.EraseInt(KeyOrigOptions);
            MCPSettings.SetFastPlayMode(false);
            Debug.Log("[MCP] Fast Play Mode restored");
        }

        // ── Test support ─────────────────────────────────────────────────────
        internal static void ResetForTest()
        {
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseBool(KeyOrigEnabled);
            SessionState.EraseInt(KeyOrigOptions);
        }
    }
}
