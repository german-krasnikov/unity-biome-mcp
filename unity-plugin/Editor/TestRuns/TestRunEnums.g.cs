// Generated from dsl/data/testrun.data-contract, dsl/data/testrun.core-data
// source-sha256=beb4218ac3b7e10cf98fb1ad2136e2d904046a4e3d5aad2969b27760aa13ff5f
// Edit the source, then run biome generate.

#nullable enable
namespace UnityMCP.Editor.TestRuns
{
    public enum Lifecycle
    {
        Prepared,
        Dispatched,
        Running,
        Finalizing,
        Terminal,
    }

    public static class LifecycleValues
    {
        public const string Prepared = "prepared";
        public const string Dispatched = "dispatched";
        public const string Running = "running";
        public const string Finalizing = "finalizing";
        public const string Terminal = "terminal";
        public static readonly string[] Names = { Prepared, Dispatched, Running, Finalizing, Terminal };
    }

    public enum RunOutcome
    {
        Passed,
        Failed,
        Cancelled,
        Incomplete,
        Invalid,
        DispatchFailed,
    }

    public static class RunOutcomeValues
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Cancelled = "cancelled";
        public const string Incomplete = "incomplete";
        public const string Invalid = "invalid";
        public const string DispatchFailed = "dispatch_failed";
        public static readonly string[] Names = { Passed, Failed, Cancelled, Incomplete, Invalid, DispatchFailed };
    }

    public enum LeafOutcome
    {
        Passed,
        Failed,
        Skipped,
        Inconclusive,
        Cancelled,
        Invalid,
    }

    public static class LeafOutcomeValues
    {
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Skipped = "skipped";
        public const string Inconclusive = "inconclusive";
        public const string Cancelled = "cancelled";
        public const string Invalid = "invalid";
        public static readonly string[] Names = { Passed, Failed, Skipped, Inconclusive, Cancelled, Invalid };
    }

    public enum Health
    {
        Healthy,
        Reloading,
        NoTestProgress,
        EditorUnresponsive,
        SuspectedStall,
    }

    public static class HealthValues
    {
        public const string Healthy = "healthy";
        public const string Reloading = "reloading";
        public const string NoTestProgress = "no_test_progress";
        public const string EditorUnresponsive = "editor_unresponsive";
        public const string SuspectedStall = "suspected_stall";
        public static readonly string[] Names =
        {
            Healthy,
            Reloading,
            NoTestProgress,
            EditorUnresponsive,
            SuspectedStall,
        };
    }

    public sealed class TerminalOutcome
    {
        public readonly string Outcome;
        public readonly bool IsFailure;
        public TerminalOutcome(string outcome, bool isFailure)
        {
            this.Outcome = outcome;
            this.IsFailure = isFailure;
        }
    }

    public static class TerminalOutcomeRows
    {
        public static readonly TerminalOutcome[] All =
        {
            new TerminalOutcome(outcome: "passed", isFailure: false),
            new TerminalOutcome(outcome: "failed", isFailure: true),
            new TerminalOutcome(outcome: "cancelled", isFailure: true),
            new TerminalOutcome(outcome: "incomplete", isFailure: true),
            new TerminalOutcome(outcome: "invalid", isFailure: true),
            new TerminalOutcome(outcome: "dispatch_failed", isFailure: true),
        };
    }
}
