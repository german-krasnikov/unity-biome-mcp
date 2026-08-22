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
        internal static Action<bool> _setEnabled =
            v => EditorSettings.enterPlayModeOptionsEnabled = v;
        internal static Action<EnterPlayModeOptions> _setOptions =
            v => EditorSettings.enterPlayModeOptions = v;
        internal static Func<bool> _getEnabled =
            () => EditorSettings.enterPlayModeOptionsEnabled;
        internal static Func<EnterPlayModeOptions> _getOptions =
            () => EditorSettings.enterPlayModeOptions;

        // ── State ────────────────────────────────────────────────────────────
        internal static bool IsApplied => SessionState.GetBool(KeyApplied, false);

        // ── API ──────────────────────────────────────────────────────────────
        internal static void Apply()
        {
            if (IsApplied) return;
            SessionState.SetBool(KeyOrigEnabled, _getEnabled());
            SessionState.SetInt(KeyOrigOptions, (int)_getOptions());
            SessionState.SetBool(KeyApplied, true);
            _setEnabled(true);
            _setOptions(_getOptions() | EnterPlayModeOptions.DisableDomainReload);
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
