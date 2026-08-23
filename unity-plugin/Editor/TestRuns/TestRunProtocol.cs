using System;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Canonical string values used by the durable test-run protocol. Strings are
    /// intentional: JsonUtility serializes enums as integers, which makes journal
    /// evidence harder to inspect and less forward-compatible.
    /// </summary>
    public static class TestRunProtocol
    {
        public const int SchemaVersion = 1;

        public static class Lifecycle
        {
            public const string Prepared = "prepared";
            public const string Dispatched = "dispatched";
            public const string Running = "running";
            public const string Finalizing = "finalizing";
            public const string Terminal = "terminal";
        }

        public static class RunOutcome
        {
            public const string Passed = "passed";
            public const string Failed = "failed";
            public const string Cancelled = "cancelled";
            public const string Incomplete = "incomplete";
            public const string Invalid = "invalid";
            public const string DispatchFailed = "dispatch_failed";
            public const string DirtySceneBlocked = "dirty_scene_blocked";
            public const string NoTestsMatched = "no_tests_matched";
        }

        public static class LeafOutcome
        {
            public const string Passed = "passed";
            public const string Failed = "failed";
            public const string Skipped = "skipped";
            public const string Inconclusive = "inconclusive";
            public const string Cancelled = "cancelled";
            public const string Invalid = "invalid";
        }

        public static class Health
        {
            public const string Healthy = "healthy";
            public const string Reloading = "reloading";
            public const string NoTestProgress = "no_test_progress";
            public const string EditorUnresponsive = "editor_unresponsive";
            public const string SuspectedStall = "suspected_stall";
        }

        public static class UntitledSceneSetup
        {
            public const string Empty = "empty";
        }

        public static class EventType
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
        }

        public static class IssueSeverity
        {
            public const string Warning = "warning";
            public const string Error = "error";
        }
    }

    [Serializable]
    public sealed class TestRunRequestRecord
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string request_id = "";
        public string run_id = "";
        // True only for request-first records that contain the complete immutable
        // dispatch intent. Legacy records remain readable but cannot synthesize a
        // missing run.json safely.
        public bool intent_complete;
        public string mode = "";
        public string group = "";
        public string filter = "";
        public string state = "prepared";
        public string created_utc = "";
        public string acknowledged_utc = "";
    }

    [Serializable]
    public sealed class TestRunRecord
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string request_id = "";
        public string utf_guid = "";
        public string source = "mcp";
        public string lifecycle = TestRunProtocol.Lifecycle.Prepared;
        public string outcome = "";
        public string health = TestRunProtocol.Health.Healthy;
        public string created_utc = "";
        public string dispatched_utc = "";
        public string started_utc = "";
        public string finished_utc = "";
        public string project_identity = "";
        public string editor_process_identity = "";
        public string editor_session_id = "";
        public string build_fingerprint = "";
        public string utf_version = "";
        public string assembly_path = "";
        public string source_path = "";
        public string assembly_write_utc = "";
        public string source_write_utc = "";
        public bool build_coherent;
        public string build_error = "";
        public string mode = "";
        public string group = "";
        public string filter = "";
    }

    [Serializable]
    public sealed class TestRunPointer
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string request_id = "";
        public string updated_utc = "";
    }

    /// <summary>
    /// Durable, run-level scene baseline. The isolation layer records this before
    /// dispatch so terminal and post-reload recovery use the same restoration plan.
    /// This is evidence only; the store never opens, saves or mutates a scene.
    /// </summary>
    [Serializable]
    public sealed class TestRunEnvironmentRecord
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string[] scene_paths = Array.Empty<string>();
        public string active_scene_path = "";
        public bool restore_single_untitled;
        public string untitled_scene_setup = "";
        public string owned_scene_path = "";
        public bool preview_scene_baseline_captured;
        public int preview_scene_count;
        // Direct UTF EditMode runs have already captured the user's SceneSetup
        // before UnityMCP observes RunStarted. UTF must restore that setup; the
        // UnityMCP finalizer then removes only its owned scene/assets.
        public bool scene_restore_delegated_to_utf;
        public string prepared_utc = "";
        public string restored_utc = "";
    }

    [Serializable]
    public sealed class TestLeafManifestEntry
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string unique_name = "";
        public string test_id = "";
        public string full_name = "";
        public string assembly_name = "";
        public string mode = "";
    }

    [Serializable]
    public sealed class TestRunEvent
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string event_id = "";
        public string event_type = "";
        public string occurred_utc = "";
        public string observer_generation = "";

        // Leaf identity and result fields.
        public string unique_name = "";
        public string test_id = "";
        public string full_name = "";
        public string outcome = "";
        public string result_state = "";
        public string label = "";
        public string message = "";
        public string stack_trace = "";
        public double duration_seconds;

        // Manifest seal and optional UTF root aggregate fields.
        public int expected_count;
        public bool has_aggregate;
        public bool root_trusted;
        public int aggregate_passed;
        public int aggregate_failed;
        public int aggregate_skipped;
        public int aggregate_inconclusive;
        public int aggregate_cancelled;
        public int aggregate_invalid;
        public int aggregate_total;
    }

    [Serializable]
    public sealed class ReconciledLeafResult
    {
        public string unique_name = "";
        public string test_id = "";
        public string full_name = "";
        public bool expected;
        public string outcome = "";
        public string result_state = "";
        public string label = "";
        public string message = "";
        public string stack_trace = "";
        public double duration_seconds;
        public int attempt_count;
        public ReconciledAttemptResult[] attempts = Array.Empty<ReconciledAttemptResult>();
    }

    [Serializable]
    public sealed class ReconciledAttemptResult
    {
        public int attempt;
        public string outcome = "";
        public string result_state = "";
        public string label = "";
        public string message = "";
        public string stack_trace = "";
        public double duration_seconds;
    }

    [Serializable]
    public sealed class TestProtocolIssue
    {
        public string code = "";
        public string severity = TestRunProtocol.IssueSeverity.Error;
        public string path = "";
        public int line;
        public string identity = "";
        public string message = "";
    }

    [Serializable]
    public sealed class TestRunSummary
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public string run_id = "";
        public string request_id = "";
        public string utf_guid = "";
        public string lifecycle = "";
        public string state = "";
        public string outcome = "";
        public string health = "";
        public string source = "";
        public string mode = "";
        public string group = "";
        public string filter = "";
        public string project_identity = "";
        public string editor_process_identity = "";
        public string editor_session_id = "";
        public string build_fingerprint = "";
        public string utf_version = "";
        public bool build_coherent;
        public string build_error = "";
        public bool is_terminal;
        public bool execution_finished;
        public bool cleanup_complete;
        public bool run_started_observed;
        public bool manifest_complete;
        public bool run_finished_observed;
        public string utf_xml_scope = "none";
        public string created_utc = "";
        public string started_utc = "";
        public string finished_utc = "";
        public string last_event_utc = "";
        public string current_test = "";
        public double duration_seconds;

        public int expected_count;
        public int declared_expected_count;
        public int readable_manifest_count;
        public int unmaterialized_expected_count;
        public int completed_expected_count;
        public int unique_terminal_count;
        public int started_attempt_count;
        public int finished_attempt_count;
        public int test_started_callback_count;
        public int finish_without_start_count;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public int cancelled;
        public int invalid;
        public int missing_count;
        public int unexpected_count;
        public int conflict_count;

        public string[] missing_tests = Array.Empty<string>();
        public string[] unexpected_tests = Array.Empty<string>();
        public string[] conflicting_tests = Array.Empty<string>();
        public ReconciledLeafResult[] leaves = Array.Empty<ReconciledLeafResult>();
        public TestProtocolIssue[] issues = Array.Empty<TestProtocolIssue>();
        public long manifest_evidence_bytes;
        public long event_evidence_bytes;
        public long run_evidence_bytes;
        public long environment_evidence_bytes;
        public long manifest_write_utc_ticks;
        public long event_write_utc_ticks;
        public long run_write_utc_ticks;
        public long environment_write_utc_ticks;
    }

    [Serializable]
    public sealed class TestRunList
    {
        public int schema_version = TestRunProtocol.SchemaVersion;
        public TestRunSummary[] runs = Array.Empty<TestRunSummary>();
    }

    /// <summary>Raw durable inputs returned by TestRunStore.ReadJournal.</summary>
    public sealed class TestRunJournal
    {
        public bool run_record_exists;
        public bool manifest_file_exists;
        public bool events_file_exists;
        public TestRunRecord run;
        public TestLeafManifestEntry[] manifest = Array.Empty<TestLeafManifestEntry>();
        public TestRunEvent[] events = Array.Empty<TestRunEvent>();
        public TestProtocolIssue[] issues = Array.Empty<TestProtocolIssue>();
    }
}
