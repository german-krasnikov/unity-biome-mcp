// WriteSessionGuard — batch N .cs writes into one domain reload.
// Mirrors ReloadGuard.cs (watchdog + SessionState) and BatchHelper.cs (static delegate seams).
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class WriteSessionGuard
    {
        // ── Seams (injectable for tests — same pattern as BatchHelper.cs:L25-26) ──────
        internal static Action _startEditing    = AssetDatabase.StartAssetEditing;
        internal static Action _stopEditing     = AssetDatabase.StopAssetEditing;
        internal static Action _lockAssemblies  = EditorApplication.LockReloadAssemblies;
        internal static Action _unlockAssemblies = EditorApplication.UnlockReloadAssemblies;
        internal static Action _disallowRefresh = AssetDatabase.DisallowAutoRefresh;
        internal static Action _allowRefresh    = AssetDatabase.AllowAutoRefresh;
        internal static Action _refresh         = AssetDatabase.Refresh;
        internal static Func<double> _time      = () => EditorApplication.timeSinceStartup;

        // ── State ────────────────────────────────────────────────────────────────────
        internal const string ActiveKey = "MCP_WriteSession";
        private static bool _active;
        private static double _startTime;
        private static double _watchdogSeconds = 120.0;

        internal static bool IsActive => _active;

        // ── [InitializeOnLoad] crash recovery ────────────────────────────────────────
        // Fires after domain reload. If Unity crashed mid-session, the marker survives.
        // Mirrors ReloadGuard.cs:L79-L96.
        static WriteSessionGuard()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            SessionState.EraseBool(ActiveKey);
            // Best-effort cleanup of partial mid-acquire state at crash.
            try { _stopEditing(); }      catch { }
            try { _unlockAssemblies(); } catch { }
            try { _allowRefresh(); }     catch { }
            // Defer refresh — inline Refresh during InitializeOnLoad retriggers compile immediately.
            // Mirrors ReloadGuard.cs:L92-L94.
            EditorApplication.delayCall += () => { try { AssetDatabase.Refresh(); } catch { } };
        }

        // ── Public API ───────────────────────────────────────────────────────────────

        internal static string Start()
        {
            if (_active) return "err=already_active";
            _startEditing();
            _disallowRefresh();
            _lockAssemblies();
            SessionState.SetBool(ActiveKey, true);
            _startTime = _time();
            EditorApplication.update += WatchdogTick;
            _active = true;
            return "write_session_started";
        }

        internal static string End()
        {
            if (!_active) return "err=not_active";
            return ForceRelease();
        }

        // ForceRelease: called by End(), watchdog, and crash recovery.
        // _stopEditing() is in try-body so it always runs; everything else is in finally
        // so they run even if _stopEditing throws. Mirrors WriteSession design doc.
        internal static string ForceRelease()
        {
            EditorApplication.update -= WatchdogTick;
            try
            {
                _stopEditing();
            }
            finally
            {
                try { _unlockAssemblies(); } catch { }
                try { _allowRefresh(); }    catch { }
                try { _refresh(); }         catch { }
                try { SessionState.EraseBool(ActiveKey); } catch { }
                _active = false;
            }
            return "write_session_ended refresh=triggered";
        }

        // ── Watchdog (fires on EditorApplication.update = main thread) ───────────────
        private static void WatchdogTick()
        {
            if (!_active) { EditorApplication.update -= WatchdogTick; return; }
            if (_time() - _startTime <= _watchdogSeconds) return;
            Debug.LogWarning("[MCP] WriteSession watchdog fired — releasing lock after timeout");
            ForceRelease();
        }

        // ── Test helpers ─────────────────────────────────────────────────────────────

        internal static void ResetForTest()
        {
            _active = false;
            SessionState.EraseBool(ActiveKey);
            EditorApplication.update -= WatchdogTick;
        }

        internal static void OverrideWatchdogSeconds(double s) => _watchdogSeconds = s;

        internal static void InvokeWatchdogTickForTest() => WatchdogTick();

        // Simulates [InitializeOnLoad] crash recovery using injected seams.
        // Tests set the SessionState marker then call this instead of triggering a real reload.
        internal static void SimulateDomainReloadForTest()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            SessionState.EraseBool(ActiveKey);
            try { _stopEditing(); }      catch { }
            try { _unlockAssemblies(); } catch { }
            try { _allowRefresh(); }     catch { }
            // No delayCall/Refresh here — tests verify _stopC/_unlockC/_allowC only.
        }
    }
}
