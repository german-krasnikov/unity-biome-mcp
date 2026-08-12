// Async wrapper around RelaySpawner's spawn sequence so a uvx cold-start (up to 45s, see
// RelaySpawner.TimeoutFor) never freezes the Unity main thread. The "already running" fast
// path (PID + TCP alive) completes inline; only a genuine cold-start hops to the ThreadPool.
//
// Threading contract (Unity 6): Editor APIs (SessionState, EditorPrefs, PackageInfo, Debug.Log)
// are NOT safe to call off the main thread. So the cold-start path is split in three steps:
//   1. PrepareSpawn() — MAIN thread — resolves cmd/argv/timeout (CommandResolver/InstallSourceDetector)
//   2. ExecuteSpawn() — ThreadPool  — pure I/O: Process.Start + read stdout for the port line
//   3. CommitSpawn()  — MAIN thread — persists SessionState, then fires onReady/onError
// Only step 2 runs off-thread. Callback marshalling reuses MainThreadDispatcher — the same
// queue MCPServer's async TCP handlers already use to get back onto the main thread after a
// ConfigureAwait(false).
using System;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal static class RelaySpawnState
    {
        internal static bool   IsReady   { get; private set; }
        internal static bool   IsPending { get; private set; }
        internal static int    Port      { get; private set; }
        internal static string Error     { get; private set; }

        // F-2: callers that arrive while IsPending accumulate here; all fire when spawn resolves.
        private static readonly System.Collections.Generic.List<(Action<int> onReady, Action<string> onError)>
            _waiters = new System.Collections.Generic.List<(Action<int>, Action<string>)>();

#if UNITY_INCLUDE_TESTS
        // Test seams — avoid a real Process/ThreadPool hop in unit tests.
        internal static Func<int>  EnsureRunningOverride;          // fast-path only (see RunFastPath)
        internal static Func<bool> LooksAlreadyRunningOverride;
        internal static Func<RelaySpawner.SpawnPlan>                       PreparePlanOverride; // main-thread step
        internal static Func<RelaySpawner.SpawnPlan, (int port, int pid)> ExecutePlanOverride;  // background step
        internal static Func<RelaySpawner.SpawnPlan, Task<(int port, int pid)>> ExecutePlanAsyncOverride;
        internal static Action<Action> DispatchOverride;

        private sealed class TestIsolation : IDisposable
        {
            private readonly TestIsolation _previous;
            private readonly bool _isReady;
            private readonly bool _isPending;
            private readonly int _port;
            private readonly string _error;
            private readonly Func<int> _ensureRunningOverride;
            private readonly Func<bool> _looksAlreadyRunningOverride;
            private readonly Func<RelaySpawner.SpawnPlan> _preparePlanOverride;
            private readonly Func<RelaySpawner.SpawnPlan, (int port, int pid)> _executePlanOverride;
            private readonly Func<RelaySpawner.SpawnPlan, Task<(int port, int pid)>> _executePlanAsyncOverride;
            private readonly Action<Action> _dispatchOverride;
            private bool _disposed;

            internal TestIsolation(TestIsolation previous)
            {
                _previous = previous;
                _isReady = IsReady;
                _isPending = IsPending;
                _port = Port;
                _error = Error;
                _ensureRunningOverride = EnsureRunningOverride;
                _looksAlreadyRunningOverride = LooksAlreadyRunningOverride;
                _preparePlanOverride = PreparePlanOverride;
                _executePlanOverride = ExecutePlanOverride;
                _executePlanAsyncOverride = ExecutePlanAsyncOverride;
                _dispatchOverride = DispatchOverride;
                ResetForTests();
            }

            public void Dispose()
            {
                if (_disposed) return;
                if (!ReferenceEquals(_testIsolation, this))
                    throw new InvalidOperationException(
                        "Relay spawn test-isolation scopes must be disposed in reverse order.");

                ResetForTests();
                IsReady = _isReady;
                IsPending = _isPending;
                Port = _port;
                Error = _error;
                EnsureRunningOverride = _ensureRunningOverride;
                LooksAlreadyRunningOverride = _looksAlreadyRunningOverride;
                PreparePlanOverride = _preparePlanOverride;
                ExecutePlanOverride = _executePlanOverride;
                ExecutePlanAsyncOverride = _executePlanAsyncOverride;
                DispatchOverride = _dispatchOverride;
                _testIsolation = _previous;
                _disposed = true;
            }
        }

        private static TestIsolation _testIsolation;
        private static long _testGeneration;

        internal static IDisposable BeginTestIsolation()
        {
            if (IsPending)
                throw new InvalidOperationException(
                    "Cannot isolate RelaySpawnState while a spawn is still pending.");
            var scope = new TestIsolation(_testIsolation);
            _testIsolation = scope;
            return scope;
        }

        internal static void ResetForTests()
        {
            _testGeneration++;
            IsReady = false; IsPending = false; Port = 0; Error = null;
            _waiters.Clear();
            EnsureRunningOverride       = null;
            LooksAlreadyRunningOverride = null;
            PreparePlanOverride         = null;
            ExecutePlanOverride         = null;
            ExecutePlanAsyncOverride    = null;
            DispatchOverride            = null;
        }
#endif

        /// <summary>
        /// Ensures the relay is running without blocking the caller when a cold start (uvx
        /// download) is required. Exactly one of onReady/onError fires, marshalled onto the
        /// Unity main thread. If a spawn is already in flight, the call is a no-op — the
        /// original caller's callbacks will still fire when that spawn resolves.
        /// </summary>
        internal static void RequestSpawn(Action<int> onReady, Action<string> onError)
        {
            if (LooksAlreadyRunning())
            {
                RunFastPath(onReady, onError);
                return;
            }

            if (IsPending)
            {
                // F-2: accumulate this caller so it fires when the in-flight spawn resolves.
                _waiters.Add((onReady, onError));
                return;
            }
            IsPending = true; IsReady = false; Error = null;

#if UNITY_INCLUDE_TESTS
            var generation = ++_testGeneration;
#endif
            var dispatch = ResolveDispatcher();

            // MAIN THREAD: resolve cmd/argv/timeout before touching the ThreadPool. This is the
            // only part of the cold-start path allowed to call Editor APIs.
            RelaySpawner.SpawnPlan plan;
            try
            {
                plan = PreparePlan();
            }
            catch (Exception ex)
            {
                IsPending = false; Error = ex.Message;
                onError?.Invoke(ex.Message);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    // BACKGROUND: pure I/O only — Process.Start + read the port line. No
                    // SessionState/EditorPrefs/PackageInfo/Debug calls happen in this frame.
                    var (port, pid) = await ExecutePlanAsync(plan).ConfigureAwait(false);
                    dispatch(() =>
                    {
#if UNITY_INCLUDE_TESTS
                        if (generation != _testGeneration) return;
#endif
                        RelaySpawner.CommitSpawn(port, pid);
                        IsPending = false; IsReady = true; Port = port; Error = null;
                        onReady?.Invoke(port);
                        DrainWaiters(port, null);
                    });
                }
                catch (Exception ex)
                {
                    dispatch(() =>
                    {
#if UNITY_INCLUDE_TESTS
                        if (generation != _testGeneration) return;
#endif
                        IsPending = false; Error = ex.Message;
                        onError?.Invoke(ex.Message);
                        DrainWaiters(-1, ex.Message);
                    });
                }
            });
        }

        // Drain accumulated F-2 waiters: fire each one with the final port (success)
        // or error message (failure). Must run on the main thread; called after IsPending is cleared.
        private static void DrainWaiters(int port, string error)
        {
            if (_waiters.Count == 0) return;
            var snapshot = new (Action<int>, Action<string>)[_waiters.Count];
            _waiters.CopyTo(snapshot);
            _waiters.Clear();
            foreach (var (onReady, onError) in snapshot)
            {
                if (error == null) onReady?.Invoke(port);
                else               onError?.Invoke(error);
            }
        }

        // Fast path: relay already alive — EnsureRunning() here is cheap (no Spawn(), just
        // SessionState + PID/TCP checks), so running it inline on the calling (main) thread
        // is safe and avoids a needless ThreadPool hop.
        private static void RunFastPath(Action<int> onReady, Action<string> onError)
        {
            try
            {
                var port = EnsureRunningOverrideOrDefault();
                IsPending = false; IsReady = true; Port = port; Error = null;
                onReady?.Invoke(port);
            }
            catch (Exception ex)
            {
                IsPending = false; Error = ex.Message;
                onError?.Invoke(ex.Message);
            }
        }

        private static int EnsureRunningOverrideOrDefault()
        {
#if UNITY_INCLUDE_TESTS
            if (EnsureRunningOverride != null) return EnsureRunningOverride();
#endif
            return RelaySpawner.EnsureRunning();
        }

        private static RelaySpawner.SpawnPlan PreparePlan()
        {
#if UNITY_INCLUDE_TESTS
            if (PreparePlanOverride != null) return PreparePlanOverride();
#endif
            return RelaySpawner.PrepareSpawn();
        }

        private static (int port, int pid) ExecutePlan(RelaySpawner.SpawnPlan plan)
        {
#if UNITY_INCLUDE_TESTS
            if (ExecutePlanOverride != null) return ExecutePlanOverride(plan);
#endif
            return RelaySpawner.ExecuteSpawn(plan);
        }

        private static Task<(int port, int pid)> ExecutePlanAsync(
            RelaySpawner.SpawnPlan plan)
        {
#if UNITY_INCLUDE_TESTS
            if (ExecutePlanAsyncOverride != null)
                return ExecutePlanAsyncOverride(plan);
#endif
            return Task.FromResult(ExecutePlan(plan));
        }

        private static Action<Action> ResolveDispatcher()
        {
#if UNITY_INCLUDE_TESTS
            if (DispatchOverride != null)
                return DispatchOverride;
#endif
            return MainThreadDispatcher.Enqueue;
        }

        // Mirrors the same alive-check RelaySpawner.EnsureRunning() uses internally — predicts
        // whether that call would hit the (potentially 45s) Spawn() path, so we know up front
        // whether to hop to the ThreadPool or just call it inline.
        private static bool LooksAlreadyRunning()
        {
#if UNITY_INCLUDE_TESTS
            if (LooksAlreadyRunningOverride != null) return LooksAlreadyRunningOverride();
#endif
            var port = RelaySpawner.RelayPort;
            var pid  = RelaySpawner.RelayPid;
            // PID check is cheap (OS-level) and accurate. IsTcpAlive has a 3s cache that
            // can return stale-alive after the relay dies — skip it here and let EnsureRunning
            // do the authoritative TCP check on the fast path.
            return port > 0 && RelaySpawner.IsProcessAlive(pid);
        }
    }
}
