using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Owns the kAutoRefresh EditorPref lifecycle on behalf of MCP mutation_mode.
    /// Mirrors FastPlayMode.cs — SessionState flag, test seams, idempotent Apply/Restore.
    /// Skip when HotReload package owns kAutoRefresh itself.
    /// </summary>
    internal static class AutoRefreshGuard
    {
        const string KeyApplied       = "MCP_ARG_Applied";
        const string KeyOrigValue     = "MCP_ARG_OrigValue";
        const string KeyOrigModeValue = "MCP_ARG_OrigModeValue";

        // ── Test seams ──────────────────────────────────────────────────────
        private static int GetAutoRefreshDefault() => EditorPrefs.GetInt("kAutoRefresh", 1);
        private static void SetAutoRefreshDefault(int v) => EditorPrefs.SetInt("kAutoRefresh", v);
        internal static Func<int>    _getAutoRefresh = GetAutoRefreshDefault;
        internal static Action<int>  _setAutoRefresh = SetAutoRefreshDefault;

        internal static void RestoreDefaultSeams()
        {
            _getAutoRefresh = GetAutoRefreshDefault;
            _setAutoRefresh = SetAutoRefreshDefault;
        }

        // ── State ────────────────────────────────────────────────────────────
        internal static bool IsApplied => SessionState.GetBool(KeyApplied, false);

        // ── API ──────────────────────────────────────────────────────────────
        internal static void Apply()
        {
            if (IsApplied) return;
            if (HotReloadDetector.IsPackageInstalled()) return;  // HR owns kAutoRefresh
            SessionState.SetInt(KeyOrigValue, _getAutoRefresh());
            SessionState.SetInt(KeyOrigModeValue, EditorPrefs.GetInt("kAutoRefreshMode", 0));
            SessionState.SetBool(KeyApplied, true);
            _setAutoRefresh(0);
            EditorPrefs.SetInt("kAutoRefreshMode", 2);  // 2 = Disabled (Unity 2021.3+)
            Debug.Log("[MCP] Auto-refresh disabled (kAutoRefresh=0, kAutoRefreshMode=2)");
        }

        internal static void Restore()
        {
            if (!IsApplied) return;
            int orig = SessionState.GetInt(KeyOrigValue, 1);
            int origMode = SessionState.GetInt(KeyOrigModeValue, 0);
            _setAutoRefresh(orig);
            EditorPrefs.SetInt("kAutoRefreshMode", origMode);
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseInt(KeyOrigValue);
            SessionState.EraseInt(KeyOrigModeValue);
            Debug.Log("[MCP] Auto-refresh restored");
        }

        // ── Test support ─────────────────────────────────────────────────────
        internal static void ResetForTest()
        {
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseInt(KeyOrigValue);
            SessionState.EraseInt(KeyOrigModeValue);
        }
    }
}
