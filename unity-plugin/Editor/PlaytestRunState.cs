using System;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Observable snapshot of the current (or most recently finished) playtest run.
    /// Populated by PlaytestRunner alongside its own closure-local counters (results/passed/
    /// failed/stepIdx/phase) — this is a read-only seam for external observers, not a control
    /// path. Named as the seam Wave P's get_playtest_run/event stream will read.
    /// </summary>
    internal sealed class PlaytestRunState
    {
        internal enum RunPhase { Idle, Running, Passed, Failed }

        internal string RunId { get; }
        internal int StepIndex { get; }
        internal RunPhase Phase { get; }
        internal DateTime StartUtc { get; }
        internal int passed { get; }
        internal int failed { get; }

        PlaytestRunState(string runId, int stepIndex, RunPhase phase, DateTime startUtc, int passedCount, int failedCount)
        {
            RunId = runId;
            StepIndex = stepIndex;
            Phase = phase;
            StartUtc = startUtc;
            passed = passedCount;
            failed = failedCount;
        }

        static readonly PlaytestRunState _idle = new PlaytestRunState(null, -1, RunPhase.Idle, default, 0, 0);

        // Review note (B14, closed as comment only): this setter is a plain auto-property, not
        // `volatile` and not behind a memory barrier. Safe today because every writer
        // (Begin/Update/Finish) and every reader run on Unity's main thread via
        // EditorApplication.update. Wave P's planned TCP reader will read `Current` from a
        // different thread — add `volatile` (or a proper lock/Interlocked swap) before that lands.
        internal static PlaytestRunState Current { get; private set; } = _idle;

        internal static void Begin(string runId, DateTime startUtc)
            => Current = new PlaytestRunState(runId, 0, RunPhase.Running, startUtc, 0, 0);

        internal static void Update(int stepIndex, int passedCount, int failedCount)
            => Current = new PlaytestRunState(Current.RunId, stepIndex, RunPhase.Running, Current.StartUtc, passedCount, failedCount);

        internal static void Finish(int passedCount, int failedCount)
            => Current = new PlaytestRunState(Current.RunId, Current.StepIndex,
                failedCount > 0 ? RunPhase.Failed : RunPhase.Passed, Current.StartUtc, passedCount, failedCount);

        /// <summary>Test hook — resets to the idle sentinel regardless of prior run state.</summary>
        internal static void ResetForTests() => Current = _idle;
    }
}
