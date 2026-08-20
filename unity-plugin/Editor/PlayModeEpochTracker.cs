using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>Tracks play_epoch (monotonic int) and world_ready (first-frame gate).
    /// play_epoch increments on each Play Mode entry.
    /// world_ready becomes true after the first EditorApplication.update fires in Play Mode,
    /// which is after all Awake + Start calls complete — the correct readiness gate.</summary>
    [InitializeOnLoad]
    internal static class PlayModeEpochTracker
    {
        private static int _epoch = 0;
        private static bool _worldReady = false;
        private static bool _waitingForFirstFrame = false;

        static PlayModeEpochTracker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
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

        /// <summary>Simulate EnteredPlayMode callback. Test seam.</summary>
        internal static void SimulateEnteredPlayMode() =>
            OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);

        /// <summary>Simulate the first EditorApplication.update frame. Test seam.</summary>
        internal static void SimulateFirstFrame() => WaitForFirstFrame();
    }
}
