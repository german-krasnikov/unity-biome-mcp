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

#if UNITY_INCLUDE_TESTS
        // Test seams — avoid a real Process/ThreadPool hop in unit tests.
        internal static Func<int>  EnsureRunningOverride;          // fast-path only (see RunFastPath)
        internal static Func<bool> LooksAlreadyRunningOverride;
        internal static Func<RelaySpawner.SpawnPlan>                       PreparePlanOverride; // main-thread step
        internal static Func<RelaySpawner.SpawnPlan, (int port, int pid)> ExecutePlanOverride;  // background step

        internal static void ResetForTests()
        {
            IsReady = false; IsPending = false; Port = 0; Error = null;
            EnsureRunningOverride       = null;
            LooksAlreadyRunningOverride = null;
            PreparePlanOverride         = null;
            ExecutePlanOverride         = null;
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

            if (IsPending) return;
            IsPending = true; IsReady = false; Error = null;

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

            Task.Run(() =>
            {
                try
                {
                    // BACKGROUND: pure I/O only — Process.Start + read the port line. No
                    // SessionState/EditorPrefs/PackageInfo/Debug calls happen in this frame.
                    var (port, pid) = ExecutePlan(plan);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        RelaySpawner.CommitSpawn(port, pid);
                        IsPending = false; IsReady = true; Port = port; Error = null;
                        onReady?.Invoke(port);
                    });
                }
                catch (Exception ex)
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        IsPending = false; Error = ex.Message;
                        onError?.Invoke(ex.Message);
                    });
                }
            });
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

        // Mirrors the same alive-check RelaySpawner.EnsureRunning() uses internally — predicts
        // whether that call would hit the (potentially 45s) Spawn() path, so we know up front
        // whether to hop to the ThreadPool or just call it inline.
        private static bool LooksAlreadyRunning()
        {
#if UNITY_INCLUDE_TESTS
            if (LooksAlreadyRunningOverride != null) return LooksAlreadyRunningOverride();
#endif
            var port = SessionState.GetInt(RelaySpawner.PortKey, 0);
            var pid  = SessionState.GetInt(RelaySpawner.PidKey, 0);
            // PID check is cheap (OS-level) and accurate. IsTcpAlive has a 3s cache that
            // can return stale-alive after the relay dies — skip it here and let EnsureRunning
            // do the authoritative TCP check on the fast path.
            return port > 0 && RelaySpawner.IsProcessAlive(pid);
        }
    }
}
