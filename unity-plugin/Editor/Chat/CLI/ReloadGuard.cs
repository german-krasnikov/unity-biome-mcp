// Domain-reload-safe lock + pending-state persistence.
// Prevents assembly reload from killing a live turn; resumes it after reload.
// Unity API calls are routed through IReloadGuardOps. The common test base installs
// a non-native implementation, so unit tests can never latch the editor globally.
// T6 safe pattern: Disallow → Lock (granular try/catch) → ForceUnlock (Allow + Refresh) + SessionState rebalance.
using System;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal interface IReloadGuardOps
    {
        double TimeSinceStartup { get; }
        void DisallowAutoRefresh();
        void AllowAutoRefresh();
        void LockReloadAssemblies();
        void UnlockReloadAssemblies();
        void RefreshAssets();
        void ScheduleRefresh();
        void AddWatchdog(EditorApplication.CallbackFunction callback);
        void RemoveWatchdog(EditorApplication.CallbackFunction callback);
    }

    /// <summary>Marker for implementations which never touch Unity's native reload state.</summary>
    internal interface IReloadGuardTestOps : IReloadGuardOps
    {
    }

    internal sealed class UnityReloadGuardOps : IReloadGuardOps
    {
        public double TimeSinceStartup => EditorApplication.timeSinceStartup;

        public void DisallowAutoRefresh() => AssetDatabase.DisallowAutoRefresh();
        public void AllowAutoRefresh() => AssetDatabase.AllowAutoRefresh();
        public void LockReloadAssemblies() => EditorApplication.LockReloadAssemblies();
        public void UnlockReloadAssemblies() => EditorApplication.UnlockReloadAssemblies();
        public void RefreshAssets() => AssetDatabase.Refresh();

        public void ScheduleRefresh()
        {
            EditorApplication.delayCall += () =>
            {
                try { AssetDatabase.Refresh(); } catch { }
            };
        }

        public void AddWatchdog(EditorApplication.CallbackFunction callback) =>
            EditorApplication.update += callback;

        public void RemoveWatchdog(EditorApplication.CallbackFunction callback) =>
            EditorApplication.update -= callback;
    }

    [InitializeOnLoad]
    internal static class ReloadGuard
    {
        // Default path — Library/ is local/gitignored in every Unity project.
        private static readonly string DefaultFilePath =
            Path.Combine("Library", "MCP_ChatPendingTurn.txt");
        private static string _filePath = DefaultFilePath;

        // Marker survives reload — native counter doesn't die even though managed state does.
        private const string LockMarkerKey = "MCP_ReloadGuardLocked";

        // Counter (not bool) so OnTurnFinished is always safe even if called extra times.
        private static int _lockDepth;
        private static bool _autoRefreshDisallowed;
        private static bool _assembliesLocked;
        private static bool _watchdogRegistered;

        // Watchdog: auto-unlock after ~120s to prevent a hung turn blocking all reloads.
        private static double _lockStartTime;
        private static double _watchdogSeconds = 120.0;

        internal static IReloadGuardOps Ops { get; private set; } = new UnityReloadGuardOps();
        private static TestIsolationScope _activeTestIsolation;

        static ReloadGuard()
        {
            // If the marker is set at reload, managed _lockDepth died — native counter may
            // still be held. Rebalance: force-unlock once regardless of depth.
            if (SessionState.GetBool(LockMarkerKey, false))
            {
                SessionState.EraseBool(LockMarkerKey);
                try { Ops.UnlockReloadAssemblies(); } catch { }
                try
                {
                    Ops.AllowAutoRefresh();
                    // Defer past the InitializeOnLoad sweep. An inline refresh can
                    // immediately retrigger compilation and acquire a new latch.
                    Ops.ScheduleRefresh();
                }
                catch { }
            }
        }

        internal static bool IsLocked => _lockDepth > 0;
        internal static bool HasPersistedLock =>
            SessionState.GetBool(LockMarkerKey, false);
        internal static string FilePath => _filePath;
        internal static bool HasActiveTestIsolation => _activeTestIsolation != null;

        internal static bool IsTestIsolationOwnedBy(string ownerId) =>
            _activeTestIsolation != null &&
            string.Equals(_activeTestIsolation.OwnerId, ownerId, StringComparison.Ordinal);

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
                    Ops.DisallowAutoRefresh();
                    disallowed = true;
                    Ops.LockReloadAssemblies();
                    locked = true;
                }
                catch
                {
                    // Partial acquisition: roll back Disallow if Lock didn't succeed.
                    if (disallowed && !locked)
                        try { Ops.AllowAutoRefresh(); } catch { }
                    // Do NOT increment _lockDepth — turn proceeds without lock.
                    return;
                }
                _autoRefreshDisallowed = disallowed;
                _assembliesLocked = locked;
                _lockDepth++;
                try
                {
                    SessionState.SetBool(LockMarkerKey, true);
                    _lockStartTime = Ops.TimeSinceStartup;
                    AddWatchdog();
                    EnsureScriptCompilationDuringPlay();
                }
                catch
                {
                    ForceUnlock();
                    return;
                }
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
            RemoveWatchdog();
            var persistedLock = SessionState.GetBool(LockMarkerKey, false);
            if (_assembliesLocked || persistedLock)
                try { Ops.UnlockReloadAssemblies(); } catch { }
            if (_autoRefreshDisallowed || persistedLock)
            {
                try { Ops.AllowAutoRefresh(); } catch { }
                // Required to re-arm the file watcher; AllowAutoRefresh alone does not.
                try { Ops.RefreshAssets(); } catch { }
            }
            SessionState.EraseBool(LockMarkerKey);
            _autoRefreshDisallowed = false;
            _assembliesLocked = false;
            _lockDepth = 0;
        }

        private static void EnsureScriptCompilationDuringPlay()
        {
            if (!HotReloadDetector.IsActive()) return;
            // 0 = RecompileAndContinuePlaying (default, may interrupt Play Mode)
            // 1 = RecompileAfterFinishedPlaying (safer with HR)
            if (EditorPrefs.GetInt("ScriptCompilationDuringPlay", 0) == 0)
                EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
        }

        private static void WatchdogTick()
        {
            if (_lockDepth <= 0)
            {
                RemoveWatchdog();
                return;
            }
            if (Ops.TimeSinceStartup - _lockStartTime > _watchdogSeconds)
                ForceUnlock();
        }

        private static void AddWatchdog()
        {
            if (_watchdogRegistered) return;
            Ops.AddWatchdog(WatchdogTick);
            _watchdogRegistered = true;
        }

        private static void RemoveWatchdog()
        {
            if (!_watchdogRegistered) return;
            try { Ops.RemoveWatchdog(WatchdogTick); } catch { }
            _watchdogRegistered = false;
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
            // Tests run against IReloadGuardTestOps. Always balance an acquired
            // operation before clearing managed state so a failed fixture cannot
            // leave Unity's native reload or refresh counters latched.
            if (_lockDepth > 0 || _assembliesLocked || _autoRefreshDisallowed ||
                SessionState.GetBool(LockMarkerKey, false))
                ForceUnlock();
            else
                RemoveWatchdog();
            _watchdogSeconds = 120.0; // restore default
            SessionState.EraseBool(LockMarkerKey);
        }

        internal static void RestoreDefaultFilePathForTest() => _filePath = DefaultFilePath;

        internal static void OverrideOpsForTest(IReloadGuardOps ops)
        {
            if (ops == null) throw new ArgumentNullException(nameof(ops));
            if (_lockDepth > 0 || _assembliesLocked || _autoRefreshDisallowed ||
                SessionState.GetBool(LockMarkerKey, false))
                throw new InvalidOperationException(
                    "ReloadGuard operations cannot be replaced while a lock is held.");
            Ops = ops;
        }

        internal static void RestoreOpsForTest(IReloadGuardOps ops) => OverrideOpsForTest(ops);

        internal static void OverrideWatchdogSeconds(double s) => _watchdogSeconds = s;

        /// <summary>
        /// Installs non-native reload operations under an explicit NUnit test owner. Matching
        /// owners may nest; a different owner identifies an interrupted prior test instead of
        /// silently accepting its test double as a valid baseline.
        /// </summary>
        internal static IDisposable BeginTestIsolation(
            IReloadGuardTestOps ops,
            string ownerId)
        {
            if (ops == null) throw new ArgumentNullException(nameof(ops));
            if (string.IsNullOrEmpty(ownerId))
                throw new ArgumentException("A test-isolation owner id is required.", nameof(ownerId));
            if (_activeTestIsolation != null &&
                !string.Equals(_activeTestIsolation.OwnerId, ownerId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "ReloadGuard test isolation is still owned by another test.");

            var scope = new TestIsolationScope(_activeTestIsolation, ops, ownerId);
            _activeTestIsolation = scope;
            return scope;
        }

        /// <summary>
        /// Repairs a test scope that belongs to a different test, or an unowned marker seam.
        /// Returns true when stale state was found so the caller can fail the new test closed.
        /// </summary>
        internal static bool RepairOrphanedTestIsolation(string currentOwnerId)
        {
            if (IsTestIsolationOwnedBy(currentOwnerId)) return false;

            var orphaned = _activeTestIsolation != null || Ops is IReloadGuardTestOps;
            if (!orphaned) return false;

            try
            {
                ResetForTest();
            }
            finally
            {
                _filePath = DefaultFilePath;
                _watchdogSeconds = 120.0;
                Ops = new UnityReloadGuardOps();
                _activeTestIsolation = null;
            }
            return true;
        }

        private sealed class TestIsolationScope : IDisposable
        {
            private readonly TestIsolationScope _previous;
            private readonly IReloadGuardOps _previousOps;
            private readonly string _previousFilePath;
            private readonly double _previousWatchdogSeconds;
            private readonly BoolSessionValue _lockMarker;
            private bool _disposed;

            internal TestIsolationScope(
                TestIsolationScope previous,
                IReloadGuardTestOps ops,
                string ownerId)
            {
                _previous = previous;
                _previousOps = Ops;
                _previousFilePath = _filePath;
                _previousWatchdogSeconds = _watchdogSeconds;
                _lockMarker = BoolSessionValue.Capture(LockMarkerKey);
                OwnerId = ownerId;
                OverrideOpsForTest(ops);
            }

            internal string OwnerId { get; }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_activeTestIsolation, this))
                    throw new InvalidOperationException(
                        "ReloadGuard test-isolation scopes must be disposed in LIFO order.");

                var errors = new System.Collections.Generic.List<Exception>();
                Restore(ResetForTest, errors);
                Restore(_lockMarker.Restore, errors);
                _filePath = _previousFilePath;
                _watchdogSeconds = _previousWatchdogSeconds;
                Ops = _previousOps;
                _activeTestIsolation = _previous;
                _disposed = true;

                if (errors.Count > 0)
                    throw new AggregateException(
                        "ReloadGuard test-isolation restoration failed.", errors);
            }

            private static void Restore(
                Action restore,
                System.Collections.Generic.ICollection<Exception> errors)
            {
                try { restore(); }
                catch (Exception error) { errors.Add(error); }
            }
        }

        private readonly struct BoolSessionValue
        {
            private readonly string _key;
            private readonly bool _existed;
            private readonly bool _value;

            private BoolSessionValue(string key, bool existed, bool value)
            {
                _key = key;
                _existed = existed;
                _value = value;
            }

            internal static BoolSessionValue Capture(string key)
            {
                var first = SessionState.GetBool(key, false);
                var second = SessionState.GetBool(key, true);
                return new BoolSessionValue(key, first == second, first);
            }

            internal void Restore()
            {
                if (_existed) SessionState.SetBool(_key, _value);
                else SessionState.EraseBool(_key);
            }
        }

        /// <summary>Expose WatchdogTick for tests to invoke directly without waiting for the timer.</summary>
        internal static void InvokeWatchdogTickForTest() => WatchdogTick();
    }
}
