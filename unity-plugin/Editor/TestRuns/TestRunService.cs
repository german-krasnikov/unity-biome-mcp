using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;

namespace UnityMCP.Editor.TestRuns
{
    internal enum UtfRunActivity
    {
        Unknown,
        Inactive,
        Active
    }

    internal interface ITestFrameworkDriver
    {
        string Execute(ExecutionSettings settings);
        bool Cancel(string utfGuid);
        UtfRunActivity Probe(string utfGuid);
        UtfRunActivity ProbeAny();
    }

    internal sealed class UnityTestFrameworkDriver : ITestFrameworkDriver
    {
        private static readonly MethodInfo IsRunningMethod = typeof(TestRunnerApi).GetMethod(
            "IsRunning", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo IsRunActiveMethod = typeof(TestRunnerApi).GetMethod(
            "IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);
        private TestRunnerApi _api;

        public string Execute(ExecutionSettings settings)
        {
            if (_api == null) _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            return _api.Execute(settings);
        }
        public bool Cancel(string utfGuid) => TestRunnerApi.CancelTestRun(utfGuid);

        public UtfRunActivity Probe(string utfGuid) => string.IsNullOrEmpty(utfGuid)
            ? UtfRunActivity.Unknown
            : InvokeProbe(IsRunningMethod, new object[] { utfGuid });

        public UtfRunActivity ProbeAny() => InvokeProbe(IsRunActiveMethod, null);

        private static UtfRunActivity InvokeProbe(MethodInfo method, object[] arguments)
        {
            if (method == null || method.ReturnType != typeof(bool))
                return UtfRunActivity.Unknown;
            try
            {
                return (bool)method.Invoke(null, arguments)
                    ? UtfRunActivity.Active
                    : UtfRunActivity.Inactive;
            }
            catch
            {
                return UtfRunActivity.Unknown;
            }
        }
    }

    internal static class TestRunDispatchContext
    {
        internal static string RunId { get; private set; } = "";

        internal static void Begin(string runId) => RunId = runId ?? "";
        internal static void End(string runId)
        {
            if (string.Equals(RunId, runId, StringComparison.Ordinal)) RunId = "";
        }
    }

    internal static class TestRunDurableBoundary
    {
        internal const string RequestIntentPersisted = "request-intent-persisted";
        internal const string RunRecordPersisted = "run-record-persisted";
        internal const string PreparedPointerPersisted = "prepared-pointer-persisted";
        internal const string BuildEvidencePersisted = "build-evidence-persisted";
        internal const string EnvironmentPrepared = "environment-prepared";
        internal const string DispatchStatePersisted = "dispatch-state-persisted";
        internal const string DispatchPointerPersisted = "dispatch-pointer-persisted";
        internal const string UtfExecuteReturned = "utf-execute-returned";
        internal const string UtfGuidPersisted = "utf-guid-persisted";
        internal const string RequestAcknowledged = "request-acknowledged";
    }

    /// <summary>
    /// Test-only process-loss simulation. It deliberately bypasses dispatch cleanup
    /// so tests can restart the service from exactly the durable files that existed
    /// at a named boundary.
    /// </summary>
    internal sealed class TestRunInjectedCrashException : Exception
    {
        internal TestRunInjectedCrashException(string boundary) : base(boundary) { }
    }

    /// <summary>
    /// Synchronous command-side orchestration. UTF completion is deliberately not
    /// represented by a Task or callback here: Execute returns the durable GUID,
    /// and the global observer owns all later evidence.
    /// </summary>
    internal sealed class TestRunService
    {
        private static readonly object StartGate = new object();
        private readonly TestRunStore _store;
        private readonly ITestRunEnvironmentController _environment;
        private readonly ITestFrameworkDriver _framework;
        private readonly Func<TestRunBuildFingerprint> _captureBuild;
        private readonly Func<bool> _isPlaying;
        private readonly Func<bool> _isCompiling;
        private readonly Func<bool> _isCompileClean;
        private readonly Func<string> _utcNow;
        private readonly Action<string> _afterDurableBoundary;
        private readonly TestRunFinalizationCoordinator _finalizer;

        internal TestRunService(
            TestRunStore store,
            ITestRunEnvironmentController environment,
            ITestFrameworkDriver framework,
            Func<TestRunBuildFingerprint> captureBuild,
            Func<bool> isPlaying,
            Func<bool> isCompiling,
            Func<bool> isCompileClean,
            Func<string> utcNow,
            Action<string> afterDurableBoundary = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _framework = framework ?? throw new ArgumentNullException(nameof(framework));
            _captureBuild = captureBuild ?? throw new ArgumentNullException(nameof(captureBuild));
            _isPlaying = isPlaying ?? throw new ArgumentNullException(nameof(isPlaying));
            _isCompiling = isCompiling ?? throw new ArgumentNullException(nameof(isCompiling));
            _isCompileClean = isCompileClean ?? throw new ArgumentNullException(nameof(isCompileClean));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _afterDurableBoundary = afterDurableBoundary;
            _finalizer = new TestRunFinalizationCoordinator(
                _store,
                _environment,
                _framework,
                _utcNow,
                action => EditorApplication.delayCall += () => action(),
                _captureBuild);
        }

        internal string Start(string requestId, string mode, string group, string filter)
        {
            if (!IsSafeIdentity(requestId))
                return "Error: request_id must contain 1-200 ASCII letters, digits, '.', '_' or '-'";

            lock (StartGate)
                return StartCore(requestId, mode, group, filter);
        }

        private string StartCore(string requestId, string mode, string group, string filter)
        {
            var now = _utcNow();
            var requestedMode = NormalizeMode(mode);
            var requestedGroup = group ?? "";
            var requestedFilter = filter ?? "";
            var request = _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = requestId,
                run_id = "run-" + Guid.NewGuid().ToString("N"),
                intent_complete = true,
                mode = requestedMode,
                group = requestedGroup,
                filter = requestedFilter,
                state = TestRunProtocol.Lifecycle.Prepared,
                created_utc = now
            }, out var requestCreated);
            if (requestCreated)
                Checkpoint(TestRunDurableBoundary.RequestIntentPersisted);
            else if (!RequestMatchesInvocation(
                         request, requestedMode, requestedGroup, requestedFilter))
                return "Error: request_id is already bound to a different immutable " +
                       "test mode, group or filter";

            if (!_store.TryReadRun(request.run_id, out var run))
            {
                if (!request.intent_complete)
                    return RequestStatus(request, null, TestRunProtocol.RunOutcome.Invalid,
                        "legacy request has no recoverable dispatch intent");
                run = RunFromIntent(request);
                _store.WriteRun(run);
                Checkpoint(TestRunDurableBoundary.RunRecordPersisted);
            }
            else if (!RunMatchesIntent(run, request))
            {
                return RequestStatus(request, run, TestRunProtocol.RunOutcome.Invalid,
                    "durable run does not match its immutable request intent");
            }

            // Repair pointer readability before any retry path can enter the
            // finalizer, which also consults active.json.
            TryReadActiveForStart(out _);

            if (!CanResumePrepared(request, run))
            {
                if (!string.IsNullOrEmpty(run.utf_guid) &&
                    TryGetRunStartInfrastructureFailure(run, out var startFailure))
                    return FailFastAfterRunStartFailure(run, request, startFailure);

                var guidMissing = string.IsNullOrEmpty(run.utf_guid);
                var abandonedDispatchIsProven = guidMissing &&
                    run.lifecycle != TestRunProtocol.Lifecycle.Prepared &&
                    run.lifecycle != TestRunProtocol.Lifecycle.Terminal &&
                    _framework.ProbeAny() == UtfRunActivity.Inactive;
                if (run.lifecycle != TestRunProtocol.Lifecycle.Terminal && guidMissing &&
                    (TestRunFinalizationCoordinator.IsPreviousEditorSession(run) ||
                     abandonedDispatchIsProven))
                {
                    _finalizer.TryFinalize(run.run_id);
                    run = _store.ReadRun(run.run_id);
                }
                return string.IsNullOrEmpty(run.utf_guid)
                    ? RequestStatus(request, run, run.outcome)
                    : Ack(run);
            }

            if (TryGetNonTerminalActiveOtherThan(run.run_id, out var activeRun))
                return FailDispatch(run, request,
                    "test run already active: " + activeRun.run_id, false, true);

            try
            {
                RestorePriorTerminalEnvironment();
            }
            catch (Exception e)
            {
                return FailDispatch(run, request,
                    "previous test environment is not restored: " + e.Message,
                    false, true);
            }

            _store.WriteActive(Pointer(run, now));
            Checkpoint(TestRunDurableBoundary.PreparedPointerPersisted);

            var environmentPrepared = _store.TryReadEnvironment(
                run.run_id, out var durableEnvironment) &&
                string.IsNullOrEmpty(durableEnvironment.restored_utc);
            try
            {
                var build = _captureBuild();
                build.ApplyTo(run);
                _store.WriteRun(run);
                Checkpoint(TestRunDurableBoundary.BuildEvidencePersisted);
                if (!build.IsCoherent)
                    return FailDispatch(run, request, build.Error, environmentPrepared);
                if (_isPlaying())
                    return FailDispatch(run, request,
                        "cannot run tests while the Editor is in Play Mode", environmentPrepared);
                if (_isCompiling())
                    return FailDispatch(run, request,
                        "compilation is in progress", environmentPrepared);
                if (!_isCompileClean())
                    return FailDispatch(run, request,
                        "domain reload is pending", environmentPrepared);
                if (string.IsNullOrEmpty(run.mode))
                    return FailDispatch(run, request,
                        "mode must be exactly EditMode or PlayMode", environmentPrepared);

                RequireNoUtfRunActive("before environment preparation");
                _environment.Prepare(_store, run.run_id, _utcNow());
                environmentPrepared = true;
                Checkpoint(TestRunDurableBoundary.EnvironmentPrepared);
                RequireNoUtfRunActive("immediately before dispatch");
                var executionFilter = new Filter { testMode = ParseMode(run.mode) };
                if (!string.IsNullOrEmpty(run.group))
                    executionFilter.groupNames = new[] { run.group };
                if (!string.IsNullOrEmpty(run.filter))
                    executionFilter.groupNames = run.filter.Split(
                        new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                // Persist the dispatch boundary before calling UTF. A reload or
                // process loss between Execute and its returned GUID must never make
                // the next RunStarted look like an unrelated UI run.
                run = _store.ReadRun(run.run_id);
                run.lifecycle = TestRunProtocol.Lifecycle.Dispatched;
                if (string.IsNullOrEmpty(run.dispatched_utc))
                    run.dispatched_utc = _utcNow();
                _store.WriteRun(run);
                Checkpoint(TestRunDurableBoundary.DispatchStatePersisted);
                _store.WriteActive(Pointer(run, run.dispatched_utc));
                Checkpoint(TestRunDurableBoundary.DispatchPointerPersisted);

                TestRunDispatchContext.Begin(run.run_id);
                string utfGuid;
                try
                {
                    utfGuid = _framework.Execute(new ExecutionSettings(executionFilter));
                }
                finally
                {
                    TestRunDispatchContext.End(run.run_id);
                }

                if (string.IsNullOrWhiteSpace(utfGuid))
                    throw new InvalidOperationException("UTF Execute returned an empty GUID");
                Checkpoint(TestRunDurableBoundary.UtfExecuteReturned);

                // RunStarted may have arrived synchronously. Never downgrade running.
                run = _store.ReadRun(run.run_id);
                run.utf_guid = utfGuid;
                if (run.lifecycle == TestRunProtocol.Lifecycle.Prepared)
                    run.lifecycle = TestRunProtocol.Lifecycle.Dispatched;
                if (string.IsNullOrEmpty(run.dispatched_utc))
                    run.dispatched_utc = _utcNow();
                _store.WriteRun(run);
                Checkpoint(TestRunDurableBoundary.UtfGuidPersisted);

                if (TryGetRunStartInfrastructureFailure(run, out var startFailure))
                    return FailFastAfterRunStartFailure(run, request, startFailure);

                request = _store.ReadRequest(request.request_id);
                if (request.state != TestRunProtocol.Lifecycle.Terminal)
                    request.state = run.lifecycle == TestRunProtocol.Lifecycle.Terminal
                        ? TestRunProtocol.Lifecycle.Terminal
                        : run.lifecycle == TestRunProtocol.Lifecycle.Finalizing
                            ? TestRunProtocol.Lifecycle.Finalizing
                            : TestRunProtocol.Lifecycle.Dispatched;
                if (string.IsNullOrEmpty(request.acknowledged_utc))
                    request.acknowledged_utc = run.dispatched_utc;
                _store.WriteRequest(request);
                Checkpoint(TestRunDurableBoundary.RequestAcknowledged);
                _store.WriteActive(Pointer(run, run.dispatched_utc));
                return Ack(run, TryGetSealedManifestCount(run.run_id));
            }
            catch (Exception e) when (!(e is TestRunInjectedCrashException))
            {
                return FailDispatch(run, request, e.Message, environmentPrepared);
            }
        }

        internal string Resolve(string requestId)
        {
            if (!IsSafeIdentity(requestId) ||
                !_store.TryReadRequest(requestId, out var request))
                return "none";
            if (!_store.TryReadRun(request.run_id, out var run))
                return RequestStatus(request, null,
                    request.intent_complete &&
                    request.state == TestRunProtocol.Lifecycle.Prepared
                        ? ""
                        : TestRunProtocol.RunOutcome.Invalid);
            if (!string.IsNullOrEmpty(run.utf_guid) &&
                TryGetRunStartInfrastructureFailure(run, out var startFailure))
                return RequestStatus(request, run, run.outcome, startFailure);
            return string.IsNullOrEmpty(run.utf_guid)
                ? RequestStatus(request, run, run.outcome)
                : Ack(run, TryGetSealedManifestCount(run.run_id));
        }

        internal string GetRunJson(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId)) return "none";
            try
            {
                if (!Directory.Exists(_store.GetRunDirectory(runId))) return "none";
                return JsonUtility.ToJson(ReadSnapshot(runId), false);
            }
            catch (Exception e)
            {
                return JsonUtility.ToJson(new TestRunSummary
                {
                    run_id = runId,
                    lifecycle = TestRunProtocol.Lifecycle.Terminal,
                    state = TestRunProtocol.Lifecycle.Terminal,
                    outcome = TestRunProtocol.RunOutcome.Invalid,
                    is_terminal = true,
                    issues = new[]
                    {
                        new TestProtocolIssue
                        {
                            code = "RUN_SNAPSHOT_UNREADABLE",
                            message = e.Message
                        }
                    }
                }, false);
            }
        }

        internal string ListRunsJson(int limit)
        {
            limit = Math.Max(1, Math.Min(limit, 100));
            var summaries = _store.ListRunIds().Take(limit)
                .Select(ReadCompactSnapshot).ToArray();
            return JsonUtility.ToJson(new TestRunList { runs = summaries }, false);
        }

        internal string Cancel(string runId)
        {
            if (!IsSafeIdentity(runId) || !_store.TryReadRun(runId, out var run))
                return "none";
            var summary = ReadSnapshot(runId);
            if (run.lifecycle == TestRunProtocol.Lifecycle.Terminal)
                return "already-terminal|run_id=" + runId + "|outcome=" + summary.outcome;
            if (string.IsNullOrEmpty(run.utf_guid))
                return "cancel-rejected|run_id=" + runId + "|reason=no-utf-guid";

            var alreadyRequested = _store.ReadJournal(runId).events.Any(e =>
                e != null && e.event_type == TestRunProtocol.EventType.CancelRequested);
            var activity = _framework.Probe(run.utf_guid);
            if (activity == UtfRunActivity.Inactive)
                return TerminalizeInactiveRun(run);
            if (alreadyRequested)
                return activity == UtfRunActivity.Unknown
                    ? CancelAck(run) + "|activity=unknown"
                    : CancelAck(run);

            bool accepted;
            try
            {
                accepted = _framework.Cancel(run.utf_guid);
            }
            catch (Exception e)
            {
                return CancelRetryable(run, "utf-cancel-threw", e.Message);
            }
            var activityAfter = _framework.Probe(run.utf_guid);
            if (!accepted && activityAfter == UtfRunActivity.Inactive)
                return TerminalizeInactiveRun(run);
            if (!accepted)
                return CancelRetryable(run, "utf-cancel-not-accepted",
                    activityAfter == UtfRunActivity.Unknown ? "unknown" : "active");

            // Only an accepted UTF cancellation becomes durable intent. If UTF
            // rejects transiently, leaving the event absent makes the same exact
            // run_id safely retryable instead of converting rejection into an ACK.
            AppendCancelRequested(runId);
            return activityAfter == UtfRunActivity.Unknown
                ? CancelAck(run) + "|activity=unknown"
                : CancelAck(run);
        }

        private static string CancelRetryable(
            TestRunRecord run,
            string reason,
            string detail) =>
            "cancel-retryable|run_id=" + run.run_id + "|utf_guid=" + run.utf_guid +
            "|reason=" + reason +
            (string.IsNullOrEmpty(detail)
                ? ""
                : "|detail_b64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(TruncateReason(detail))));

        private void AppendCancelRequested(string runId, string message = null)
        {
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.CancelRequested,
                occurred_utc = _utcNow(),
                observer_generation = TestRunObserverRegistration.Generation,
                message = string.IsNullOrEmpty(message)
                    ? "Cancellation requested through MCP."
                    : message
            });
        }

        private bool TryGetRunStartInfrastructureFailure(
            TestRunRecord run,
            out string reason)
        {
            reason = "";
            if (run == null ||
                run.lifecycle != TestRunProtocol.Lifecycle.Finalizing)
                return false;

            var journal = _store.ReadJournal(run.run_id);
            if (journal.events.Any(e => e != null &&
                    e.event_type == TestRunProtocol.EventType.RunStarted))
                return false;

            var failure = journal.events.FirstOrDefault(e => e != null &&
                e.event_type == TestRunProtocol.EventType.InfrastructureError);
            if (failure == null) return false;
            reason = string.IsNullOrEmpty(failure.message)
                ? "RunStarted failed before durable start evidence was committed."
                : failure.message;
            return true;
        }

        // Best-effort: UTF usually seals the manifest asynchronously, well after
        // Ack() has already returned, so a null result here is the common case,
        // not a failure. Only the synchronous empty-filter fast path observes a
        // seal this early.
        private int? TryGetSealedManifestCount(string runId)
        {
            var sealEvent = _store.ReadJournal(runId).events.FirstOrDefault(e =>
                e != null && e.event_type == TestRunProtocol.EventType.ManifestSealed);
            return sealEvent != null ? (int?)sealEvent.expected_count : null;
        }

        private string FailFastAfterRunStartFailure(
            TestRunRecord run,
            TestRunRequestRecord request,
            string reason)
        {
            run = _store.ReadRun(run.run_id);
            request = _store.ReadRequest(request.request_id);
            request.state = TestRunProtocol.Lifecycle.Finalizing;
            if (string.IsNullOrEmpty(request.acknowledged_utc))
                request.acknowledged_utc = string.IsNullOrEmpty(run.dispatched_utc)
                    ? _utcNow()
                    : run.dispatched_utc;
            _store.WriteRequest(request);
            _store.WriteActive(Pointer(run, _utcNow()));

            var alreadyRequested = _store.ReadJournal(run.run_id).events.Any(e =>
                e != null && e.event_type == TestRunProtocol.EventType.CancelRequested);
            if (!alreadyRequested)
            {
                // Commit the cancellation intent before the external UTF call. A
                // retry after process loss must never cancel the same GUID twice.
                AppendCancelRequested(run.run_id,
                    "Exact UTF run cancellation requested after RunStarted " +
                    "infrastructure failure; utf_guid=" + run.utf_guid + ".");
                try
                {
                    _framework.Cancel(run.utf_guid);
                }
                catch (Exception e)
                {
                    AppendInfrastructureErrorOnce(run.run_id,
                        "UTF cancellation after RunStarted failure threw: " + e.Message);
                }
            }

            _finalizer.Request(run.run_id);
            return RequestStatus(request, run, run.outcome, reason);
        }

        private void AppendInfrastructureErrorOnce(string runId, string message)
        {
            if (_store.ReadJournal(runId).events.Any(e => e != null &&
                    e.event_type == TestRunProtocol.EventType.InfrastructureError &&
                    string.Equals(e.message, message, StringComparison.Ordinal))) return;
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.InfrastructureError,
                occurred_utc = _utcNow(),
                observer_generation = TestRunObserverRegistration.Generation,
                message = message
            });
        }

        private string TerminalizeInactiveRun(TestRunRecord run)
        {
            var now = _utcNow();
            if (!_store.ReadJournal(run.run_id).events.Any(e => e != null &&
                    e.event_type == TestRunProtocol.EventType.Abandoned))
            {
                _store.AppendEvent(run.run_id, new TestRunEvent
                {
                    run_id = run.run_id,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.Abandoned,
                    occurred_utc = now,
                    observer_generation = TestRunObserverRegistration.Generation,
                    message = "UTF reported that the durable GUID is no longer active."
                });
            }
            run = _store.ReadRun(run.run_id);
            run.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
            _store.WriteRun(run);
            if (!string.IsNullOrEmpty(run.request_id) &&
                _store.TryReadRequest(run.request_id, out var request))
            {
                request.state = TestRunProtocol.Lifecycle.Finalizing;
                _store.WriteRequest(request);
            }
            if (!_finalizer.TryFinalize(run.run_id))
                _finalizer.Request(run.run_id);
            var summary = _store.Reconcile(run.run_id, false);
            return "cancel-not-active|run_id=" + run.run_id +
                "|state=" + summary.lifecycle + "|outcome=" + summary.outcome;
        }

        internal string GetLegacyResults(string runId)
        {
            if (!TryResolveRunId(runId, out var resolved)) return "none";
            var summary = ReadSnapshot(resolved);
            if (!summary.is_terminal) return "pending";
            return string.Format(CultureInfo.InvariantCulture,
                "{0} tests: {1} passed, {2} failed, {3} skipped, {4} inconclusive, " +
                "{5} cancelled, {6} invalid ({7:F1}s) outcome={8}",
                summary.expected_count, summary.passed, summary.failed, summary.skipped,
                summary.inconclusive, summary.cancelled, summary.invalid,
                summary.duration_seconds, summary.outcome);
        }

        internal string GetLegacyProgress(string runId)
        {
            if (!TryResolveRunId(runId, out var resolved)) return "idle";
            var summary = ReadSnapshot(resolved);
            if (summary.is_terminal)
                return "idle|run_id=" + resolved + "|outcome=" + summary.outcome;
            if (!summary.manifest_complete)
                return "pending|run_id=" + resolved + "|no-progress-yet";

            var elapsed = ElapsedSeconds(summary.started_utc, _utcNow());
            var completed = summary.completed_expected_count;
            var eta = completed > 0 && summary.expected_count > completed
                ? Math.Max(0d, (summary.expected_count - completed) * elapsed / completed)
                : 0d;
            return string.Format(CultureInfo.InvariantCulture,
                "running|{0}|{1}|{2}|{3}|{4}|{5:F1}|eta={6:F0}s|run_id={7}",
                completed, summary.passed, summary.failed, summary.skipped,
                summary.expected_count, elapsed, eta, resolved);
        }

        private bool TryGetNonTerminalActiveOtherThan(string runId, out TestRunRecord run)
        {
            run = null;
            if (!TryReadActiveForStart(out var pointer) ||
                string.Equals(pointer.run_id, runId, StringComparison.Ordinal) ||
                !_store.TryReadRun(pointer.run_id, out run))
                return false;
            if (run.lifecycle == TestRunProtocol.Lifecycle.Terminal) return false;
            // Same-session Finalizing runs must also get a self-heal attempt: a
            // stuck ProbeAny "Active" signal (e.g. zero-match filter dispatch)
            // must not wedge every later dispatch just because the stuck run
            // belongs to this editor session. The coordinator's own activity
            // gate and staleness ceiling decide whether it is safe to finalize.
            try
            {
                if (_finalizer.TryFinalize(run.run_id))
                {
                    run = _store.ReadRun(run.run_id);
                    return run.lifecycle != TestRunProtocol.Lifecycle.Terminal;
                }
            }
            catch
            {
                // Preserve the old active pointer and fail this dispatch closed.
            }
            return true;
        }

        private void RestorePriorTerminalEnvironment()
        {
            if (!TryReadActiveForStart(out var pointer) ||
                !_store.TryReadRun(pointer.run_id, out var run) ||
                run.lifecycle != TestRunProtocol.Lifecycle.Terminal) return;
            _environment.Restore(_store, run.run_id, _utcNow());
        }

        private bool TryReadActiveForStart(out TestRunPointer pointer)
        {
            try
            {
                return _store.TryReadActive(out pointer);
            }
            catch (TestRunStoreException)
            {
                var quarantined = _store.QuarantineCorruptActive();
                if (string.IsNullOrEmpty(quarantined))
                    return _store.TryReadActive(out pointer);
                pointer = null;
                return false;
            }
        }

        private void RequireNoUtfRunActive(string stage)
        {
            var activity = _framework.ProbeAny();
            if (activity == UtfRunActivity.Inactive) return;
            throw new InvalidOperationException(activity == UtfRunActivity.Active
                ? "another UTF run is already active " + stage
                : "UTF activity could not be proven inactive " + stage);
        }

        private string FailDispatch(
            TestRunRecord run,
            TestRunRequestRecord request,
            string message,
            bool restoreEnvironment,
            bool preserveActive = false)
        {
            var now = _utcNow();
            _store.AppendEvent(run.run_id, new TestRunEvent
            {
                run_id = run.run_id,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.DispatchFailed,
                occurred_utc = now,
                observer_generation = TestRunObserverRegistration.Generation,
                outcome = TestRunProtocol.RunOutcome.DispatchFailed,
                message = message ?? "test dispatch failed"
            });
            run = _store.ReadRun(run.run_id);
            run.lifecycle = restoreEnvironment
                ? TestRunProtocol.Lifecycle.Finalizing
                : TestRunProtocol.Lifecycle.Terminal;
            if (!restoreEnvironment && string.IsNullOrEmpty(run.outcome))
                run.outcome = TestRunProtocol.RunOutcome.DispatchFailed;
            if (!restoreEnvironment) run.finished_utc = now;
            _store.WriteRun(run);
            request = _store.ReadRequest(request.request_id);
            request.state = restoreEnvironment
                ? TestRunProtocol.Lifecycle.Finalizing
                : TestRunProtocol.Lifecycle.Terminal;
            _store.WriteRequest(request);
            if (!preserveActive) _store.WriteActive(Pointer(run, now));
            _store.WriteLatest(Pointer(run, now));

            if (restoreEnvironment)
            {
                if (!_finalizer.TryFinalize(run.run_id))
                    _finalizer.Request(run.run_id);
            }
            else
                FinalizeWithoutEnvironment(run, now);

            _store.Reconcile(run.run_id, true);
            run = _store.ReadRun(run.run_id);
            request = _store.ReadRequest(request.request_id);
            return RequestStatus(request, run, run.outcome, message);
        }

        private void FinalizeWithoutEnvironment(TestRunRecord run, string now)
        {
            _store.AppendEvent(run.run_id, new TestRunEvent
            {
                run_id = run.run_id,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = now,
                observer_generation = TestRunObserverRegistration.Generation,
                outcome = run.outcome
            });
            var pointer = Pointer(run, now);
            if (!_store.TryReadActive(out var active) ||
                string.Equals(active.run_id, run.run_id, StringComparison.Ordinal))
                _store.WriteActive(pointer);
            _store.WriteLatest(pointer);
        }

        private bool TryResolveRunId(string requested, out string runId)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                runId = requested;
                if (!IsSafeIdentity(runId)) return false;
                return _store.TryReadRun(runId, out _);
            }
            if (_store.TryReadActive(out var active) &&
                _store.TryReadRun(active.run_id, out _))
            {
                runId = active.run_id;
                return true;
            }
            if (_store.TryReadLatest(out var latest) &&
                _store.TryReadRun(latest.run_id, out _))
            {
                runId = latest.run_id;
                return true;
            }
            runId = "";
            return false;
        }

        private TestRunSummary ReadSnapshot(string runId)
        {
            if (_store.TryReadRun(runId, out var run) &&
                run.lifecycle == TestRunProtocol.Lifecycle.Finalizing)
            {
                _finalizer.TryFinalize(runId);
                run = _store.ReadRun(runId);
            }
            if (run != null &&
                run.lifecycle == TestRunProtocol.Lifecycle.Terminal &&
                _store.TryReadSummary(runId, out var cached) &&
                _store.IsSummaryCurrent(runId, cached))
                return cached;
            return _store.Reconcile(runId, false);
        }

        private TestRunSummary ReadCompactSnapshot(string runId)
        {
            try
            {
                var summary = ReadSnapshot(runId);
                summary.missing_tests = Array.Empty<string>();
                summary.unexpected_tests = Array.Empty<string>();
                summary.conflicting_tests = Array.Empty<string>();
                summary.leaves = Array.Empty<ReconciledLeafResult>();
                summary.issues = Array.Empty<TestProtocolIssue>();
                return summary;
            }
            catch (Exception e)
            {
                return new TestRunSummary
                {
                    run_id = runId,
                    lifecycle = TestRunProtocol.Lifecycle.Terminal,
                    state = TestRunProtocol.Lifecycle.Terminal,
                    outcome = TestRunProtocol.RunOutcome.Invalid,
                    is_terminal = true,
                    cleanup_complete = false,
                    issues = new[]
                    {
                        new TestProtocolIssue
                        {
                            code = "RUN_SNAPSHOT_UNREADABLE",
                            message = e.Message
                        }
                    }
                };
            }
        }

        private static string Ack(TestRunRecord run, int? expectedCount = null) =>
            "tests-started|request_id=" + run.request_id +
            "|run_id=" + run.run_id +
            "|utf_guid=" + run.utf_guid +
            "|state=dispatched" +
            (expectedCount.HasValue
                ? "|expected_count=" + expectedCount.Value.ToString(CultureInfo.InvariantCulture)
                : "");

        private static string CancelAck(TestRunRecord run) =>
            "cancel-requested|run_id=" + run.run_id + "|utf_guid=" + run.utf_guid;

        private static string RequestStatus(
            TestRunRequestRecord request,
            TestRunRecord run,
            string outcome,
            string reason = null) =>
            "test-request|request_id=" + request.request_id +
            "|run_id=" + request.run_id +
            "|state=" + (run?.lifecycle ?? request.state ?? "unknown") +
            "|outcome=" + (outcome ?? "") +
            (string.IsNullOrEmpty(reason)
                ? ""
                : "|reason_b64=" + Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(TruncateReason(reason))));

        private static string TruncateReason(string reason) =>
            reason != null && reason.Length > 2048 ? reason.Substring(0, 2048) : reason ?? "";

        private static TestRunPointer Pointer(TestRunRecord run, string utc) =>
            new TestRunPointer
            {
                run_id = run.run_id,
                request_id = run.request_id,
                updated_utc = utc
            };

        private void Checkpoint(string boundary) =>
            _afterDurableBoundary?.Invoke(boundary);

        private static TestRunRecord RunFromIntent(TestRunRequestRecord request) =>
            new TestRunRecord
            {
                run_id = request.run_id,
                request_id = request.request_id,
                source = "mcp",
                lifecycle = TestRunProtocol.Lifecycle.Prepared,
                health = TestRunProtocol.Health.Healthy,
                created_utc = request.created_utc,
                mode = request.mode ?? "",
                group = request.group ?? "",
                filter = request.filter ?? ""
            };

        private static bool RunMatchesIntent(
            TestRunRecord run,
            TestRunRequestRecord request)
        {
            if (!request.intent_complete) return true;
            return string.Equals(run.run_id, request.run_id, StringComparison.Ordinal) &&
                   string.Equals(run.request_id, request.request_id, StringComparison.Ordinal) &&
                   string.Equals(run.source, "mcp", StringComparison.Ordinal) &&
                   string.Equals(run.created_utc ?? "", request.created_utc ?? "",
                       StringComparison.Ordinal) &&
                   string.Equals(run.mode ?? "", request.mode ?? "", StringComparison.Ordinal) &&
                   string.Equals(run.group ?? "", request.group ?? "", StringComparison.Ordinal) &&
                   string.Equals(run.filter ?? "", request.filter ?? "", StringComparison.Ordinal);
        }

        private static bool RequestMatchesInvocation(
            TestRunRequestRecord request,
            string mode,
            string group,
            string filter) =>
            request != null && request.intent_complete &&
            string.Equals(request.mode ?? "", mode ?? "", StringComparison.Ordinal) &&
            string.Equals(request.group ?? "", group ?? "", StringComparison.Ordinal) &&
            string.Equals(request.filter ?? "", filter ?? "", StringComparison.Ordinal);

        private static bool CanResumePrepared(
            TestRunRequestRecord request,
            TestRunRecord run) =>
            request.intent_complete &&
            request.state == TestRunProtocol.Lifecycle.Prepared &&
            run.lifecycle == TestRunProtocol.Lifecycle.Prepared &&
            string.IsNullOrEmpty(run.utf_guid);

        private static string NormalizeMode(string mode)
        {
            if (string.IsNullOrEmpty(mode) || mode == "EditMode") return "EditMode";
            return mode == "PlayMode" ? "PlayMode" : "";
        }

        private static bool IsSafeIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 200 ||
                value == "." || value == "..") return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')
                    continue;
                return false;
            }
            return true;
        }

        private static TestMode ParseMode(string mode) =>
            mode == "PlayMode" ? TestMode.PlayMode : TestMode.EditMode;

        private static double ElapsedSeconds(string startUtc, string nowUtc)
        {
            if (!DateTime.TryParse(startUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var start) ||
                !DateTime.TryParse(nowUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var now))
                return 0d;
            return Math.Max(0d, (now - start).TotalSeconds);
        }
    }
}
#endif
