using System;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Owns the kAutoRefresh EditorPref lifecycle on behalf of MCP mutation_mode.
    /// Mirrors FastPlayMode.cs — SessionState flag, test seams, idempotent Apply/Restore.
    /// Skip when HotReload package owns kAutoRefresh itself.
    /// </summary>
    internal static class AutoRefreshGuard
    {
        const string KeyApplied   = "MCP_ARG_Applied";
        const string KeyOrigValue = "MCP_ARG_OrigValue";

        // ── Test seams ──────────────────────────────────────────────────────
        internal static Func<int>    _getAutoRefresh = () => EditorPrefs.GetInt("kAutoRefresh", 1);
        internal static Action<int>  _setAutoRefresh = v  => EditorPrefs.SetInt("kAutoRefresh", v);

        // ── State ────────────────────────────────────────────────────────────
        internal static bool IsApplied => SessionState.GetBool(KeyApplied, false);

        // ── API ──────────────────────────────────────────────────────────────
        internal static void Apply()
        {
            if (IsApplied) return;
            if (HotReloadDetector.IsPackageInstalled()) return;  // HR owns kAutoRefresh
            SessionState.SetInt(KeyOrigValue, _getAutoRefresh());
            SessionState.SetBool(KeyApplied, true);
            _setAutoRefresh(0);
        }

        internal static void Restore()
        {
            if (!IsApplied) return;
            int orig = SessionState.GetInt(KeyOrigValue, 1);
            _setAutoRefresh(orig);
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseInt(KeyOrigValue);
        }

        // ── Test support ─────────────────────────────────────────────────────
        internal static void ResetForTest()
        {
            SessionState.EraseBool(KeyApplied);
            SessionState.EraseInt(KeyOrigValue);
        }
    }
}
