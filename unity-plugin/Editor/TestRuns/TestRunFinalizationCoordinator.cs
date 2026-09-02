using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

#if UNITY_INCLUDE_TESTS
namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Completes a durable run only after UTF has stopped all jobs and the scene
    /// baseline has been restored. RunFinished itself is an execution boundary,
    /// not permission to change scenes from inside UTF's callback stack.
    /// </summary>
    internal sealed class TestRunFinalizationCoordinator
    {
        private readonly TestRunStore _store;
        private readonly ITestRunEnvironmentController _environment;
        private readonly ITestFrameworkDriver _framework;
        private readonly Func<string> _utcNow;
        private readonly Action<Action> _schedule;
        private readonly Func<TestRunBuildFingerprint> _captureBuild;
        private readonly HashSet<string> _queued = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// A same-session run stuck in Finalizing past this many seconds since
        /// dispatch is forced through the activity gate below, but only when it
        /// already has a durable execution boundary (RunFinished/DispatchFailed/
        /// Abandoned/Cancelled). This heals a persistent ProbeAny "Active" false
        /// signal (observed with zero-match filter dispatches) without ever
        /// finalizing a run that is genuinely still executing.
        /// </summary>
        private const double SameSessionStalenessCeilingSeconds = 180d;

        internal TestRunFinalizationCoordinator(
            TestRunStore store,
            ITestRunEnvironmentController environment,
            ITestFrameworkDriver framework,
            Func<string> utcNow,
            Action<Action> schedule,
            Func<TestRunBuildFingerprint> captureBuild = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _framework = framework ?? throw new ArgumentNullException(nameof(framework));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _captureBuild = captureBuild ?? TestRunBuildFingerprintProbe.Capture;
        }

        internal void Request(string runId)
        {
            if (string.IsNullOrEmpty(runId) || !_queued.Add(runId)) return;
            _schedule(() =>
            {
                _queued.Remove(runId);
                TryFinalize(runId);
            });
        }

        internal bool TryFinalize(string runId)
        {
            return TryFinalizeCore(runId, false);
        }

        /// <summary>
        /// Final synchronous checkpoint used from EditorApplication.quitting. UTF's
        /// command-line runner exits before delayCall is serviced, so a run that has
        /// already emitted an execution boundary must restore and commit now.
        /// </summary>
        internal bool TryFinalizeForEditorShutdown(string runId)
        {
            return TryFinalizeCore(runId, true);
        }

        private bool TryFinalizeCore(string runId, bool editorIsQuitting)
        {
            if (!_store.TryReadRun(runId, out var run)) return true;
            if (run.lifecycle == TestRunProtocol.Lifecycle.Terminal)
            {
                TerminalizeRequest(run);
                if (_store.TryReadActive(out var terminalActive) &&
                    string.Equals(terminalActive.run_id, run.run_id, StringComparison.Ordinal))
                {
                    var repaired = new TestRunPointer
                    {
                        run_id = run.run_id,
                        request_id = run.request_id,
                        updated_utc = run.finished_utc
                    };
                    _store.WriteActive(repaired);
                    _store.WriteLatest(repaired);
                }
                return true;
            }

            var journal = _store.ReadJournal(runId);
            var hasExecutionBoundary = HasExecutionBoundary(journal);
            if (editorIsQuitting && !hasExecutionBoundary)
                return false;

            var unmanagedWithoutEnvironment = IsUnmanagedWithoutEnvironment(run);
            var previousEditorSession = IsPreviousEditorSession(run);
            var staleBeyondCeiling = hasExecutionBoundary &&
                ElapsedSeconds(run.dispatched_utc, _utcNow()) > SameSessionStalenessCeilingSeconds;
            if (!editorIsQuitting && !previousEditorSession && !staleBeyondCeiling)
            {
                var anyActivity = _framework.ProbeAny();
                var ownActivity = string.IsNullOrEmpty(run.utf_guid)
                    ? UtfRunActivity.Unknown
                    : _framework.Probe(run.utf_guid);
                if (anyActivity != UtfRunActivity.Inactive ||
                    ownActivity == UtfRunActivity.Active)
                {
                    var health = anyActivity == UtfRunActivity.Unknown
                        ? TestRunProtocol.Health.SuspectedStall
                        : TestRunProtocol.Health.Healthy;
                    if (!string.Equals(run.health, health, StringComparison.Ordinal))
                    {
                        run.health = health;
                        _store.WriteRun(run);
                    }
                    Request(runId);
                    return false;
                }
            }

            if (!hasExecutionBoundary)
            {
                _store.AppendEvent(runId, Event(runId,
                    TestRunProtocol.EventType.Abandoned,
                    "UTF became inactive without a durable RunFinished boundary."));
                journal = _store.ReadJournal(runId);
            }

            run = _store.ReadRun(runId);
            if (run.lifecycle != TestRunProtocol.Lifecycle.Finalizing)
            {
                run.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                _store.WriteRun(run);
            }

            try
            {
                if (!unmanagedWithoutEnvironment)
                    _environment.Restore(_store, runId, _utcNow());
            }
            catch (Exception e)
            {
                AppendInfrastructureOnce(runId,
                    "scene restoration failed: " + e.Message);
                run = _store.ReadRun(runId);
                run.lifecycle = TestRunProtocol.Lifecycle.Terminal;
                run.health = TestRunProtocol.Health.SuspectedStall;
                run.outcome = TestRunProtocol.RunOutcome.Invalid;
                if (string.IsNullOrEmpty(run.finished_utc)) run.finished_utc = _utcNow();
                _store.WriteRun(run);
                TerminalizeRequest(run);
                var failedPointer = new TestRunPointer
                {
                    run_id = run.run_id,
                    request_id = run.request_id,
                    updated_utc = run.finished_utc
                };
                if (!_store.TryReadActive(out var failedActive) ||
                    string.Equals(failedActive.run_id, run.run_id, StringComparison.Ordinal))
                    _store.WriteActive(failedPointer);
                _store.WriteLatest(failedPointer);
                _store.Reconcile(runId, true);
                return true;
            }

            journal = _store.ReadJournal(runId);
            if (!HasValidFinalizedOutcome(journal))
            {
                ValidateCompletionBuild(run);
                journal = _store.ReadJournal(runId);
            }

            var provisional = _store.Reconcile(runId, false);
            var terminalOutcome = SelectTerminalOutcome(run, provisional, journal);
            if (!HasValidFinalizedOutcome(journal))
            {
                _store.AppendEvent(runId, new TestRunEvent
                {
                    run_id = runId,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.RunFinalized,
                    occurred_utc = _utcNow(),
                    observer_generation = TestRunObserverRegistration.Generation,
                    outcome = terminalOutcome
                });
            }

            var summary = _store.Reconcile(runId, false);
            run = _store.ReadRun(runId);
            journal = _store.ReadJournal(runId);
            terminalOutcome = SelectTerminalOutcome(run, summary, journal);
            run.lifecycle = TestRunProtocol.Lifecycle.Terminal;
            run.outcome = terminalOutcome;
            run.health = hasExecutionBoundary
                ? TestRunProtocol.Health.Healthy
                : DeriveAbandonedHealth(journal);
            if (string.IsNullOrEmpty(run.finished_utc)) run.finished_utc = _utcNow();
            _store.WriteRun(run);
            TerminalizeRequest(run);

            var pointer = new TestRunPointer
            {
                run_id = run.run_id,
                request_id = run.request_id,
                updated_utc = run.finished_utc
            };
            if (!_store.TryReadActive(out var active) ||
                string.Equals(active.run_id, run.run_id, StringComparison.Ordinal))
                _store.WriteActive(pointer);
            _store.WriteLatest(pointer);
            _store.Reconcile(runId, true);
            return true;
        }

        /// <summary>
        /// An abandoned run (no RunFinished/DispatchFailed/Cancelled evidence) is
        /// never stamped Healthy. Whether any TestStarted event ever landed tells
        /// apart "never began" from "began, then UTF went silent."
        /// </summary>
        private static string DeriveAbandonedHealth(TestRunJournal journal) =>
            (journal?.events ?? Array.Empty<TestRunEvent>()).Any(e =>
                e != null && e.event_type == TestRunProtocol.EventType.TestStarted)
                ? TestRunProtocol.Health.EditorUnresponsive
                : TestRunProtocol.Health.NoTestProgress;

        private static bool HasExecutionBoundary(TestRunJournal journal) =>
            journal != null && (journal.events ?? Array.Empty<TestRunEvent>()).Any(e =>
                e != null &&
                (e.event_type == TestRunProtocol.EventType.RunFinished ||
                 e.event_type == TestRunProtocol.EventType.DispatchFailed ||
                 e.event_type == TestRunProtocol.EventType.Abandoned ||
                 e.event_type == TestRunProtocol.EventType.Cancelled));

        private static bool HasValidFinalizedOutcome(TestRunJournal journal) =>
            FinalizedOutcomes(journal).Any();

        private static string SelectTerminalOutcome(
            TestRunRecord run,
            TestRunSummary summary,
            TestRunJournal journal)
        {
            var derived = NormalizeOutcome(summary?.outcome);
            if (!IsTerminalOutcome(derived)) derived = TestRunProtocol.RunOutcome.Incomplete;

            var finalized = FinalizedOutcomes(journal).Distinct(StringComparer.Ordinal).ToArray();
            if (finalized.Any(outcome => !IsTerminalOutcome(outcome)) || finalized.Length > 1)
                return TestRunProtocol.RunOutcome.Invalid;

            var legacy = NormalizeOutcome(run?.outcome);
            if (!string.IsNullOrEmpty(legacy) && !IsTerminalOutcome(legacy))
                return TestRunProtocol.RunOutcome.Invalid;
            if (!string.IsNullOrEmpty(legacy) && finalized.Length == 1 &&
                !string.Equals(legacy, finalized[0], StringComparison.Ordinal))
                return TestRunProtocol.RunOutcome.Invalid;

            var selected = MorePessimistic(derived, legacy);
            return finalized.Length == 0
                ? selected
                : MorePessimistic(selected, finalized[0]);
        }

        private static IEnumerable<string> FinalizedOutcomes(TestRunJournal journal) =>
            (journal?.events ?? Array.Empty<TestRunEvent>())
                .Where(e => e != null &&
                            e.event_type == TestRunProtocol.EventType.RunFinalized &&
                            !string.IsNullOrWhiteSpace(e.outcome))
                .Select(e => NormalizeOutcome(e.outcome));

        private static string MorePessimistic(string left, string right)
        {
            left = NormalizeOutcome(left);
            right = NormalizeOutcome(right);
            if (string.IsNullOrEmpty(right)) return left;
            if (string.IsNullOrEmpty(left)) return right;
            return OutcomeRank(right) > OutcomeRank(left) ? right : left;
        }

        private static int OutcomeRank(string outcome)
        {
            switch (NormalizeOutcome(outcome))
            {
                case TestRunProtocol.RunOutcome.Invalid: return 6;
                case TestRunProtocol.RunOutcome.DispatchFailed: return 5;
                case TestRunProtocol.RunOutcome.Incomplete: return 4;
                case TestRunProtocol.RunOutcome.Cancelled: return 3;
                case TestRunProtocol.RunOutcome.Failed: return 2;
                case TestRunProtocol.RunOutcome.Passed: return 1;
                default: return 0;
            }
        }

        private static bool IsTerminalOutcome(string outcome) =>
            OutcomeRank(outcome) > 0;

        private static double ElapsedSeconds(string dispatchedUtc, string nowUtc)
        {
            if (!DateTime.TryParse(dispatchedUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dispatched) ||
                !DateTime.TryParse(nowUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var now))
                return 0d;
            return Math.Max(0d, (now - dispatched).TotalSeconds);
        }

        private static string NormalizeOutcome(string outcome) =>
            string.IsNullOrWhiteSpace(outcome) ? "" : outcome.Trim().ToLowerInvariant();

        internal static bool IsPreviousEditorSession(TestRunRecord run) =>
            run != null &&
            ((!string.IsNullOrEmpty(run.editor_process_identity) &&
              !string.Equals(run.editor_process_identity,
                  TestRunBuildFingerprintProbe.EditorProcessIdentity(),
                  StringComparison.Ordinal)) ||
             (!string.IsNullOrEmpty(run.editor_session_id) &&
              !string.Equals(run.editor_session_id,
                  TestRunBuildFingerprintProbe.EditorSessionId(),
                  StringComparison.Ordinal)));

        private bool IsUnmanagedWithoutEnvironment(TestRunRecord run)
        {
            if (!string.Equals(run.source, "unity-ui", StringComparison.Ordinal) &&
                !string.Equals(run.source, "unity-ui-orphan", StringComparison.Ordinal))
                return false;
            return !_store.TryReadEnvironment(run.run_id, out var environment) ||
                   !string.IsNullOrEmpty(environment.restored_utc);
        }

        private void TerminalizeRequest(TestRunRecord run)
        {
            if (string.IsNullOrEmpty(run.request_id) ||
                !_store.TryReadRequest(run.request_id, out var request)) return;
            request.state = TestRunProtocol.Lifecycle.Terminal;
            if (string.IsNullOrEmpty(request.acknowledged_utc) &&
                !string.IsNullOrEmpty(run.utf_guid))
                request.acknowledged_utc = _utcNow();
            _store.WriteRequest(request);
        }

        private void AppendInfrastructureOnce(string runId, string message)
        {
            if (_store.ReadJournal(runId).events.Any(e => e != null &&
                    e.event_type == TestRunProtocol.EventType.InfrastructureError &&
                    string.Equals(e.message, message, StringComparison.Ordinal))) return;
            _store.AppendEvent(runId, Event(runId,
                TestRunProtocol.EventType.InfrastructureError, message));
        }

        private void ValidateCompletionBuild(TestRunRecord run)
        {
            if (run == null || string.IsNullOrEmpty(run.build_fingerprint)) return;
            string mismatch;
            try
            {
                mismatch = TestRunBuildFingerprint.DescribeCompletionMismatch(
                    run, _captureBuild());
            }
            catch (Exception e)
            {
                mismatch = "completion build fingerprint capture failed: " + e.Message;
            }
            if (!string.IsNullOrEmpty(mismatch))
                AppendInfrastructureOnce(run.run_id,
                    "test build changed before finalization: " + mismatch);
        }

        private bool HasEvent(string runId, string eventType) =>
            _store.ReadJournal(runId).events.Any(e =>
                e != null && e.event_type == eventType);

        private TestRunEvent Event(string runId, string eventType, string message) =>
            new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = eventType,
                occurred_utc = _utcNow(),
                observer_generation = TestRunObserverRegistration.Generation,
                message = message ?? ""
            };
    }
}
#endif
