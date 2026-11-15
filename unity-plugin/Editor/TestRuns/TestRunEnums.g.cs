// Generated from dsl/data/testrun.data-contract, dsl/data/testrun.core-data
// source-sha256=31d30200cae9a76f1caa5ada846e683a23cdd94b9b0abe9014f7c66fd73c4112
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

    public enum EventType
    {
        RunStarted,
        ManifestSealed,
        TestStarted,
        TestFinished,
        RunFinished,
        InfrastructureError,
        DispatchFailed,
        CancelRequested,
        Abandoned,
        Cancelled,
        DomainReloading,
        RunFinalized,
    }

    public static class EventTypeValues
    {
        public const string RunStarted = "run_started";
        public const string ManifestSealed = "manifest_sealed";
        public const string TestStarted = "test_started";
        public const string TestFinished = "test_finished";
        public const string RunFinished = "run_finished";
        public const string InfrastructureError = "infrastructure_error";
        public const string DispatchFailed = "dispatch_failed";
        public const string CancelRequested = "cancel_requested";
        public const string Abandoned = "abandoned";
        public const string Cancelled = "cancelled";
        public const string DomainReloading = "domain_reloading";
        public const string RunFinalized = "run_finalized";
        public static readonly string[] Names =
        {
            RunStarted,
            ManifestSealed,
            TestStarted,
            TestFinished,
            RunFinished,
            InfrastructureError,
            DispatchFailed,
            CancelRequested,
            Abandoned,
            Cancelled,
            DomainReloading,
            RunFinalized,
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
