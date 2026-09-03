using System;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>Tracks play_epoch (monotonic int) and world_ready (first-frame gate),
    /// and owns the force_play_stop (T5 recovery) reload-survival contract: entering
    /// Play Mode triggers a domain reload in this project (full domain reload, not the
    /// fast-enter-play-mode option), which wipes any delayCall/update subscription the
    /// force_play_stop command handler registered inline (RELAY-FIX, commit 1bcc90b7).
    /// This class's static ctor re-subscribes on every domain reload and SessionState
    /// survives it, so a pending stop/start is never lost.
    /// play_epoch increments on each Play Mode entry.
    /// world_ready becomes true after the first EditorApplication.update fires in Play Mode,
    /// which is after all Awake + Start calls complete — the correct readiness gate.</summary>
    [InitializeOnLoad]
    internal static class PlayModeEpochTracker
    {
        internal const string PendingPlayStopKey = "MCP_PendingPlayStop";
        internal const string PendingPlayStartKey = "MCP_PendingPlayStart";

        private static int _epoch = 0;
        private static bool _worldReady = false;
        private static bool _waitingForFirstFrame = false;
        private static bool _waitingForCompileToStart = false;

        // Test seams — production defaults; tests substitute these to observe a
        // request without a real Play Mode transition or a real compile wait.
        internal static Action RequestPlayModeExit = () => EditorApplication.isPlaying = false;
        internal static Action RequestPlayModeEnter = () => EditorApplication.isPlaying = true;
        internal static Func<bool> IsCompiling = () => EditorApplication.isCompiling;
        internal static Func<bool> IsPlayingOrWillChange = () => EditorApplication.isPlayingOrWillChangePlaymode;

        static PlayModeEpochTracker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // A domain reload from compilation finishing wipes the update-poll armed by
            // WaitForCompileThenEnterPlayMode below. Re-arm it here — the static ctor
            // re-runs on every domain reload — if force_play_stop's compiling branch
            // left a pending start flag before the reload happened.
            if (SessionState.GetBool(PendingPlayStartKey, false))
                WaitForCompileThenEnterPlayMode();
        }

        public static int Epoch => System.Threading.Volatile.Read(ref _epoch);
        public static bool WorldReady => _worldReady;

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                System.Threading.Interlocked.Increment(ref _epoch);
                _worldReady = false;
                _waitingForFirstFrame = true;
                EditorApplication.update += WaitForFirstFrame;

                if (SessionState.GetBool(PendingPlayStopKey, false))
                {
                    SessionState.EraseBool(PendingPlayStopKey);
                    RequestPlayModeExit();
                }
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                _worldReady = false;
                _waitingForFirstFrame = false;
                EditorApplication.update -= WaitForFirstFrame;
            }
        }

        private static void WaitForFirstFrame()
        {
            if (!_waitingForFirstFrame) return;
            _worldReady = true;
            _waitingForFirstFrame = false;
            EditorApplication.update -= WaitForFirstFrame;
        }

        /// <summary>force_play_stop's compiling-branch recovery: waits for compilation to
        /// finish, then arms the pending-stop flag and requests Play Mode entry. Idempotent
        /// — safe to call again (e.g. from the static ctor after a domain reload) while
        /// already waiting. The pending-stop flag is armed here, immediately before
        /// requesting entry, not up front when compilation starts — arming it earlier would
        /// leave an unrelated later Play Mode session unexpectedly interrupted if entry
        /// never actually happens.</summary>
        internal static void WaitForCompileThenEnterPlayMode()
        {
            if (_waitingForCompileToStart) return;
            _waitingForCompileToStart = true;

            void Poll()
            {
                if (!SessionState.GetBool(PendingPlayStartKey, false))
                {
                    _waitingForCompileToStart = false;
                    EditorApplication.update -= Poll;
                    return;
                }
                if (IsCompiling()) return;

                _waitingForCompileToStart = false;
                EditorApplication.update -= Poll;
                SessionState.EraseBool(PendingPlayStartKey);
                SessionState.SetBool(PendingPlayStopKey, true);
                RequestPlayModeEnter();

                // Entry can still be refused (e.g. new compile errors, or the Editor
                // already mid-transition) — Unity only actually enters on the next
                // frame, so isPlayingOrWillChangePlaymode still reads true one tick
                // later if entry was accepted, and false again if it was refused. On
                // refusal, drop the flag armed above so it cannot force-stop the next
                // unrelated Play Mode session the user starts. On acceptance the
                // domain reload that follows wipes this callback before it can fire.
                EditorTickOnce.Schedule(() =>
                {
                    if (!IsPlayingOrWillChange())
                        SessionState.EraseBool(PendingPlayStopKey);
                });
            }
            EditorApplication.update += Poll;
        }

        /// <summary>Restore to specific state. Test seam — call from RegisterCleanup only.</summary>
        internal static void RestoreForTest(int epoch, bool worldReady)
        {
            System.Threading.Volatile.Write(ref _epoch, epoch);
            _worldReady = worldReady;
            _waitingForFirstFrame = false;
            EditorApplication.update -= WaitForFirstFrame;
        }

        /// <summary>Reset to initial state. Test seam — call from [SetUp] only.</summary>
        internal static void ResetForTest()
        {
            System.Threading.Volatile.Write(ref _epoch, 0);
            _worldReady = false;
            _waitingForFirstFrame = false;
            EditorApplication.update -= WaitForFirstFrame;
        }

        /// <summary>Restore the force_play_stop test seams to production behavior. Test
        /// seam — call from RegisterCleanup only.</summary>
        internal static void ResetPlayModeSeamsForTest()
        {
            RequestPlayModeExit = () => EditorApplication.isPlaying = false;
            RequestPlayModeEnter = () => EditorApplication.isPlaying = true;
            IsCompiling = () => EditorApplication.isCompiling;
            IsPlayingOrWillChange = () => EditorApplication.isPlayingOrWillChangePlaymode;
        }

        /// <summary>Clears a mid-wait WaitForCompileThenEnterPlayMode guard left by a test
        /// that stopped observing before compilation finished. Test seam — call from
        /// RegisterCleanup only.</summary>
        internal static void ResetWaitForCompileGuardForTest() => _waitingForCompileToStart = false;

        /// <summary>Simulate EnteredPlayMode callback. Test seam.</summary>
        internal static void SimulateEnteredPlayMode() =>
            OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);

        /// <summary>Simulate the first EditorApplication.update frame. Test seam.</summary>
        internal static void SimulateFirstFrame() => WaitForFirstFrame();
    }
}
