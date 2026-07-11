// Domain-reload-safe lock + pending-state persistence.
// Prevents assembly reload from killing a live turn; resumes it after reload.
// Unity-API calls (Lock/Unlock/Disallow/Allow) are guarded by _lockDepth counter
// so this class is usable from unit tests via OverrideFilePath + ResetForTest.
// T6 safe pattern: Disallow → Lock (granular try/catch) → ForceUnlock (Allow + Refresh) + SessionState rebalance.
using System;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class ReloadGuard
    {
        // Default path — Library/ is local/gitignored in every Unity project.
        private static string _filePath = Path.Combine("Library", "MCP_ChatPendingTurn.txt");

        // Marker survives reload — native counter doesn't die even though managed state does.
        private const string LockMarkerKey = "MCP_ReloadGuardLocked";

        // Counter (not bool) so OnTurnFinished is always safe even if called extra times.
        private static int _lockDepth;

        // Watchdog: auto-unlock after ~120s to prevent a hung turn blocking all reloads.
        private static double _lockStartTime;
        private static double _watchdogSeconds = 120.0;

        static ReloadGuard()
        {
            // If the marker is set at reload, managed _lockDepth died — native counter may
            // still be held. Rebalance: force-unlock once regardless of depth.
            if (SessionState.GetBool(LockMarkerKey, false))
            {
                SessionState.EraseBool(LockMarkerKey);
                try { EditorApplication.UnlockReloadAssemblies(); } catch { }
                try
                {
                    AssetDatabase.AllowAutoRefresh();
                    // ponytail: delayCall defers past [InitializeOnLoad] sweep —
                    // inline Refresh() re-triggers compilationStarted → new latch
                    EditorApplication.delayCall += () => { try { AssetDatabase.Refresh(); } catch { } };
                }
                catch { }
            }
        }

        internal static bool IsLocked => _lockDepth > 0;

        // ── Lock / Unlock ─────────────────────────────────────────────────────

        internal static void OnTurnStarted()
        {
            if (_lockDepth == 0)
            {
                // Granular acquisition tracking: only increment _lockDepth when BOTH
                // DisallowAutoRefresh AND LockReloadAssemblies succeeded.
                // Prevents ForceUnlock from calling Unlock without a matching Lock.
                bool disallowed = false;
                bool locked = false;
                try
                {
                    AssetDatabase.DisallowAutoRefresh();
                    disallowed = true;
                    EditorApplication.LockReloadAssemblies();
                    locked = true;
                }
                catch
                {
                    // Partial acquisition: roll back Disallow if Lock didn't succeed.
                    if (disallowed && !locked)
                        try { AssetDatabase.AllowAutoRefresh(); } catch { }
                    // Do NOT increment _lockDepth — turn proceeds without lock.
                    return;
                }
                _lockDepth++;
                SessionState.SetBool(LockMarkerKey, true);
                _lockStartTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += WatchdogTick;
                return;
            }
            _lockDepth++;
        }

        internal static void OnTurnFinished()
        {
            if (_lockDepth <= 0) return;
            _lockDepth--;
            if (_lockDepth == 0)
                ForceUnlock();
        }

        internal static void ForceUnlock()
        {
            EditorApplication.update -= WatchdogTick;
            try { EditorApplication.UnlockReloadAssemblies(); } catch { }
            try
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();  // required to re-arm the file watcher; AllowAutoRefresh alone does not
            }
            catch { }
            SessionState.EraseBool(LockMarkerKey);
            _lockDepth = 0;
        }

        private static void WatchdogTick()
        {
            if (_lockDepth <= 0)
            {
                EditorApplication.update -= WatchdogTick;
                return;
            }
            if (EditorApplication.timeSinceStartup - _lockStartTime > _watchdogSeconds)
                ForceUnlock();
        }

        // ── Pending state ─────────────────────────────────────────────────────

        internal static void SavePendingState(PendingTurnState state)
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(_filePath, state.Serialize());
            }
            catch { /* never crash on reload path */ }
        }

        internal static PendingTurnState? LoadPendingState()
        {
            try
            {
                if (!File.Exists(_filePath)) return null;
                var raw = File.ReadAllText(_filePath);
                return PendingTurnState.Deserialize(raw);
            }
            catch
            {
                return null;
            }
        }

        internal static void ClearPendingState()
        {
            try { File.Delete(_filePath); } catch { }
        }

        // ── Test seams (no-op in production — only called from tests) ─────────

        internal static void OverrideFilePath(string path) => _filePath = path;

        internal static void ResetForTest()
        {
            // Reset in-memory counter without touching Unity API
            // (tests run without domain reload machinery).
            _lockDepth = 0;
            _watchdogSeconds = 120.0; // restore default
            EditorApplication.update -= WatchdogTick; // prevent stale delegate accumulation across tests
            SessionState.EraseBool(LockMarkerKey);
        }

        internal static void OverrideWatchdogSeconds(double s) => _watchdogSeconds = s;

        /// <summary>Expose WatchdogTick for tests to invoke directly without waiting for the timer.</summary>
        internal static void InvokeWatchdogTickForTest() => WatchdogTick();
    }
}
