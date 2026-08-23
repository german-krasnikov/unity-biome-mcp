using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// Pure replay/reconciliation logic. It derives counters and terminal truth
    /// from durable evidence; it never increments mutable counters.
    /// </summary>
    public static class TestRunReconciler
    {
        public static TestRunSummary Reconcile(string runId, TestRunJournal journal)
        {
            if (string.IsNullOrWhiteSpace(runId))
                throw new ArgumentException("A run id is required.", nameof(runId));
            if (journal == null) throw new ArgumentNullException(nameof(journal));

            var issues = new List<TestProtocolIssue>(journal.issues ?? Array.Empty<TestProtocolIssue>());
            var invalidEvidence = issues.Any(i =>
                i != null && i.severity == TestRunProtocol.IssueSeverity.Error);
            var run = journal.run;

            if (run == null)
            {
                if (!issues.Any(i => i != null && i.code == "RUN_RECORD_MISSING"))
                    AddError(issues, "RUN_RECORD_MISSING", "", 0, runId,
                        "The durable run record is missing or unreadable.");
                invalidEvidence = true;
            }
            else
            {
                if (run.schema_version != TestRunProtocol.SchemaVersion)
                {
                    AddError(issues, "RUN_SCHEMA_UNSUPPORTED", "", 0, runId,
                        $"Run schema {run.schema_version} is not supported.");
                    invalidEvidence = true;
                }
                if (!string.Equals(run.run_id, runId, StringComparison.Ordinal))
                {
                    AddError(issues, "RUN_ID_MISMATCH", "", 0, run.run_id,
                        "run.json belongs to a different run.");
                    invalidEvidence = true;
                }
            }

            var manifests = new Dictionary<string, TestLeafManifestEntry>(StringComparer.Ordinal);
            var manifestOrder = new List<string>();
            var manifestConflicts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var leaf in journal.manifest ?? Array.Empty<TestLeafManifestEntry>())
            {
                if (!ValidateManifestRecord(runId, leaf, issues))
                {
                    invalidEvidence = true;
                    continue;
                }

                if (!manifests.TryGetValue(leaf.unique_name, out var existing))
                {
                    manifests.Add(leaf.unique_name, leaf);
                    manifestOrder.Add(leaf.unique_name);
                }
                else if (!SameManifestEntry(existing, leaf))
                {
                    manifestConflicts.Add(leaf.unique_name);
                    AddError(issues, "MANIFEST_CONFLICT", "", 0, leaf.unique_name,
                        "The same UniqueName has different manifest metadata.");
                    invalidEvidence = true;
                }
            }

            var leafStates = new Dictionary<string, LeafReplayState>(StringComparer.Ordinal);
            var sealedCount = -1;
            var runStartedExpectedCount = -1;
            var manifestSealSeen = false;
            TestRunEvent runFinished = null;
            var runFinishedObserved = false;
            var runFinishedTrusted = false;
            var runStartedObserved = false;
            var runStartedGeneration = "";
            var runFinalizedObserved = false;
            TestRunEvent runFinalized = null;
            var dispatchFailed = false;
            var explicitlyCancelled = false;
            var explicitlyAbandoned = false;
            var rootConflict = false;
            var activeTests = new List<string>();
            var lastEventUtc = "";
            var observedStartedUtc = "";
            var observedFinishedUtc = "";
            var eventIds = new Dictionary<string, TestRunEvent>(StringComparer.Ordinal);

            foreach (var testEvent in journal.events ?? Array.Empty<TestRunEvent>())
            {
                if (!ValidateEventRecord(runId, testEvent, issues))
                {
                    invalidEvidence = true;
                    continue;
                }
                if (eventIds.TryGetValue(testEvent.event_id, out var priorEvent))
                {
                    if (SameEventRecord(priorEvent, testEvent)) continue;
                    AddError(issues, "EVENT_ID_CONFLICT", "", 0, testEvent.event_id,
                        "The same event_id identifies different durable evidence.");
                    invalidEvidence = true;
                    continue;
                }
                eventIds.Add(testEvent.event_id, testEvent);
                if (!string.IsNullOrEmpty(testEvent.occurred_utc))
                    lastEventUtc = testEvent.occurred_utc;

                if (runFinishedObserved && IsLateExecutionEvidence(testEvent.event_type))
                {
                    AddError(issues, "EVIDENCE_AFTER_RUN_FINISHED", "", 0,
                        testEvent.unique_name,
                        "Execution evidence after the first RunFinished boundary was ignored.");
                    invalidEvidence = true;
                    continue;
                }

                switch (testEvent.event_type)
                {
                    case TestRunProtocol.EventType.RunStarted:
                        runStartedObserved = true;
                        runStartedExpectedCount = Math.Max(
                            runStartedExpectedCount, testEvent.expected_count);
                        if (string.IsNullOrEmpty(observedStartedUtc))
                        {
                            observedStartedUtc = testEvent.occurred_utc ?? "";
                            runStartedGeneration = testEvent.observer_generation ?? "";
                        }
                        break;

                    case TestRunProtocol.EventType.ManifestSealed:
                        if (testEvent.expected_count < 0)
                        {
                            AddError(issues, "MANIFEST_SEAL_INVALID", "", 0, runId,
                                "Manifest expected_count cannot be negative.");
                            invalidEvidence = true;
                        }
                        else if (!manifestSealSeen)
                        {
                            manifestSealSeen = true;
                            sealedCount = testEvent.expected_count;
                        }
                        else if (sealedCount != testEvent.expected_count)
                        {
                            AddError(issues, "MANIFEST_SEAL_CONFLICT", "", 0, runId,
                                "Repeated manifest seals disagree about expected_count.");
                            invalidEvidence = true;
                        }
                        break;

                    case TestRunProtocol.EventType.TestStarted:
                        if (string.IsNullOrWhiteSpace(testEvent.unique_name))
                        {
                            AddError(issues, "TEST_STARTED_IDENTITY_MISSING", "", 0, "",
                                "A test_started event requires UniqueName.");
                            invalidEvidence = true;
                            break;
                        }
                        if (StateFor(leafStates, testEvent.unique_name).StartAttempt())
                        {
                            activeTests.Remove(testEvent.unique_name);
                            activeTests.Add(testEvent.unique_name);
                        }
                        break;

                    case TestRunProtocol.EventType.TestFinished:
                        if (!IsLeafOutcome(testEvent.outcome))
                        {
                            AddError(issues, "LEAF_OUTCOME_INVALID", "", 0,
                                testEvent.unique_name,
                                $"Unknown terminal leaf outcome '{testEvent.outcome}'.");
                            invalidEvidence = true;
                            break;
                        }
                        if (string.IsNullOrWhiteSpace(testEvent.unique_name))
                        {
                            AddError(issues, "TEST_FINISHED_IDENTITY_MISSING", "", 0, "",
                                "A test_finished event requires UniqueName.");
                            invalidEvidence = true;
                            break;
                        }

                        var state = StateFor(leafStates, testEvent.unique_name);
                        if (!state.FinishAttempt(testEvent))
                        {
                            AddError(issues, "TERMINAL_EVIDENCE_CONFLICT", "", 0,
                                testEvent.unique_name,
                                "Different terminal evidence arrived without a new test_started boundary.");
                            invalidEvidence = true;
                        }
                        if (!state.AttemptOpen) activeTests.Remove(testEvent.unique_name);
                        break;

                    case TestRunProtocol.EventType.RunFinished:
                        if (!IsRunFinishedOutcome(testEvent.outcome))
                        {
                            AddError(issues, "RUN_FINISHED_OUTCOME_INVALID", "", 0, runId,
                                $"Unknown run_finished outcome '{testEvent.outcome}'.");
                            invalidEvidence = true;
                        }
                        runFinishedObserved = true;
                        observedFinishedUtc = testEvent.occurred_utc ?? "";
                        if (runFinished == null)
                        {
                            runFinished = testEvent;
                            runFinishedTrusted = testEvent.root_trusted &&
                                !string.IsNullOrEmpty(runStartedGeneration) &&
                                string.Equals(runStartedGeneration,
                                    testEvent.observer_generation, StringComparison.Ordinal);
                            if (testEvent.root_trusted && !runFinishedTrusted)
                            {
                                AddError(issues, "ROOT_PROVENANCE_INVALID", "", 0, runId,
                                    "Trusted root evidence does not match the RunStarted observer generation.");
                                invalidEvidence = true;
                            }
                            if (testEvent.has_aggregate && !runFinishedTrusted)
                                AddWarning(issues, "UNTRUSTED_ROOT_AGGREGATE", "", 0,
                                    runId,
                                    "Root aggregate was ignored because observer provenance changed.");
                        }
                        else if (!SameRunFinished(runFinished, testEvent))
                        {
                            rootConflict = true;
                            AddError(issues, "RUN_FINISHED_CONFLICT", "", 0, runId,
                                "Repeated run_finished evidence disagrees.");
                            invalidEvidence = true;
                        }
                        break;

                    case TestRunProtocol.EventType.InfrastructureError:
                        AddError(issues, "INFRASTRUCTURE_ERROR", "", 0, runId,
                            string.IsNullOrEmpty(testEvent.message)
                                ? "UTF reported an infrastructure error."
                                : testEvent.message);
                        invalidEvidence = true;
                        break;

                    case TestRunProtocol.EventType.DispatchFailed:
                        dispatchFailed = true;
                        break;

                    case TestRunProtocol.EventType.CancelRequested:
                        // Cancellation is asynchronous. The request is operational
                        // evidence, not terminal evidence; RunFinished decides truth.
                        break;

                    case TestRunProtocol.EventType.Abandoned:
                        explicitlyAbandoned = true;
                        break;

                    case TestRunProtocol.EventType.Cancelled:
                        explicitlyCancelled = true;
                        break;

                    case TestRunProtocol.EventType.DomainReloading:
                        // Operational checkpoint. It hard-flushes all earlier leaf
                        // evidence but does not change run liveness by itself.
                        break;

                    case TestRunProtocol.EventType.RunFinalized:
                        runFinalizedObserved = true;
                        if (string.IsNullOrWhiteSpace(testEvent.outcome))
                        {
                            AddWarning(issues, "RUN_FINALIZED_OUTCOME_MISSING", "", 0,
                                runId,
                                "Legacy run_finalized evidence has no terminal outcome.");
                        }
                        else if (!IsTerminalRunOutcome(testEvent.outcome))
                        {
                            AddError(issues, "RUN_FINALIZED_OUTCOME_INVALID", "", 0,
                                runId,
                                $"Unknown run_finalized outcome '{testEvent.outcome}'.");
                            invalidEvidence = true;
                        }
                        else if (runFinalized == null)
                        {
                            runFinalized = testEvent;
                        }
                        else if (!string.Equals(Normalize(runFinalized.outcome),
                                     Normalize(testEvent.outcome), StringComparison.Ordinal))
                        {
                            AddError(issues, "RUN_FINALIZED_CONFLICT", "", 0, runId,
                                "Repeated run_finalized evidence disagrees about terminal outcome.");
                            invalidEvidence = true;
                        }
                        break;

                    default:
                        AddError(issues, "EVENT_TYPE_UNKNOWN", "", 0, testEvent.event_type,
                            "The journal contains an unsupported event type.");
                        invalidEvidence = true;
                        break;
                }
            }

            if (!journal.manifest_file_exists)
                AddWarning(issues, "MANIFEST_FILE_MISSING", "", 0, runId,
                    "No expected-tests.jsonl has been persisted yet.");
            if (!journal.events_file_exists)
                AddWarning(issues, "EVENT_FILE_MISSING", "", 0, runId,
                    "No events.jsonl has been persisted yet.");

            var manifestCorrupt = issues.Any(i => i != null &&
                (i.code == "MANIFEST_LINE_CORRUPT" || i.code == "MANIFEST_RECORD_INVALID" ||
                 i.code == "MANIFEST_RUN_ID_MISMATCH" || i.code == "MANIFEST_SCHEMA_UNSUPPORTED"));
            var manifestComplete = journal.manifest_file_exists && manifestSealSeen &&
                                   sealedCount == manifests.Count &&
                                   manifestConflicts.Count == 0 && !manifestCorrupt;
            if (manifestSealSeen && sealedCount != manifests.Count)
            {
                AddWarning(issues, "MANIFEST_COUNT_MISMATCH", "", 0, runId,
                    $"Manifest seal expects {sealedCount} leaves, but {manifests.Count} unique leaves were readable.");
            }

            if (runFinishedObserved && !runStartedObserved)
            {
                AddError(issues, "RUN_STARTED_MISSING", "", 0, runId,
                    "Execution ended without durable RunStarted evidence.");
                invalidEvidence = true;
            }

            var missing = manifestOrder
                .Where(name => !leafStates.TryGetValue(name, out var state) || !state.HasTerminal)
                .ToList();
            var unexpected = leafStates.Keys
                .Where(name => !manifests.ContainsKey(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            var unexpectedTerminal = unexpected
                .Where(name => leafStates[name].HasTerminal)
                .ToList();
            var terminalConflicts = leafStates
                .Where(pair => pair.Value.Conflicting)
                .Select(pair => pair.Key);
            var conflicts = new HashSet<string>(manifestConflicts, StringComparer.Ordinal);
            conflicts.UnionWith(terminalConflicts);

            if (missing.Count > 0 && runFinishedObserved)
                AddWarning(issues, "TERMINAL_EVIDENCE_MISSING", "", 0, runId,
                    $"RunFinished was observed with {missing.Count} expected leaves missing.");
            if (unexpected.Count > 0)
            {
                AddError(issues, "UNEXPECTED_TERMINAL_EVIDENCE", "", 0, runId,
                    $"Terminal evidence exists for {unexpected.Count} leaves outside the manifest.");
                invalidEvidence = true;
            }

            var results = BuildResults(manifestOrder, manifests, leafStates, unexpectedTerminal);
            var declaredExpectedCount = manifestSealSeen
                ? Math.Max(0, sealedCount)
                : Math.Max(manifests.Count, Math.Max(0, runStartedExpectedCount));
            var unmaterializedExpectedCount = Math.Max(
                0, declaredExpectedCount - manifests.Count);
            var summary = new TestRunSummary
            {
                run_id = runId,
                request_id = run?.request_id ?? "",
                utf_guid = run?.utf_guid ?? "",
                lifecycle = run?.lifecycle ?? "",
                state = run?.lifecycle ?? "",
                health = run?.health ?? "",
                source = run?.source ?? "",
                mode = run?.mode ?? "",
                group = run?.group ?? "",
                filter = run?.filter ?? "",
                project_identity = run?.project_identity ?? "",
                editor_process_identity = run?.editor_process_identity ?? "",
                editor_session_id = run?.editor_session_id ?? "",
                build_fingerprint = run?.build_fingerprint ?? "",
                utf_version = run?.utf_version ?? "",
                build_coherent = run != null && run.build_coherent,
                build_error = run?.build_error ?? "",
                manifest_complete = manifestComplete,
                run_started_observed = runStartedObserved,
                run_finished_observed = runFinishedObserved,
                utf_xml_scope = runFinishedObserved
                    ? runFinishedTrusted ? "complete" : "partial"
                    : "none",
                execution_finished = runFinishedObserved || dispatchFailed ||
                                     explicitlyCancelled || explicitlyAbandoned ||
                                     runFinalizedObserved,
                cleanup_complete = runFinalizedObserved,
                created_utc = run?.created_utc ?? "",
                started_utc = string.IsNullOrEmpty(run?.started_utc)
                    ? observedStartedUtc
                    : run.started_utc,
                finished_utc = string.IsNullOrEmpty(run?.finished_utc)
                    ? observedFinishedUtc
                    : run.finished_utc,
                last_event_utc = lastEventUtc,
                current_test = activeTests.Count == 0 ? "" : activeTests[activeTests.Count - 1],
                duration_seconds = runFinishedTrusted ? runFinished?.duration_seconds ?? 0d : 0d,
                expected_count = declaredExpectedCount,
                declared_expected_count = declaredExpectedCount,
                readable_manifest_count = manifests.Count,
                unmaterialized_expected_count = unmaterializedExpectedCount,
                completed_expected_count = manifestOrder.Count(name =>
                    leafStates.TryGetValue(name, out var state) && state.HasTerminal),
                unique_terminal_count = leafStates.Count(pair => pair.Value.HasTerminal),
                started_attempt_count = leafStates.Sum(pair => pair.Value.AttemptCount),
                finished_attempt_count = leafStates.Sum(pair => pair.Value.CompletedAttemptCount),
                test_started_callback_count = leafStates.Sum(
                    pair => pair.Value.ObservedStartCallbackCount),
                finish_without_start_count = leafStates.Sum(
                    pair => pair.Value.FinishWithoutStartCount),
                missing_count = missing.Count + unmaterializedExpectedCount,
                unexpected_count = unexpected.Count,
                conflict_count = conflicts.Count,
                missing_tests = missing.ToArray(),
                unexpected_tests = unexpected.ToArray(),
                conflicting_tests = conflicts.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                leaves = results.ToArray()
            };

            foreach (var result in results.Where(r => r.expected && !conflicts.Contains(r.unique_name)))
                IncrementOutcome(summary, result.outcome);

            if (!runFinishedTrusted)
                summary.duration_seconds = results.Where(r => r.expected)
                    .Sum(r => Math.Max(0d, r.duration_seconds));

            if (runFinishedTrusted && runFinished != null && runFinished.has_aggregate &&
                !AggregateMatches(runFinished, summary))
            {
                AddError(issues, "ROOT_AGGREGATE_CONTRADICTION", "", 0, runId,
                    "UTF root aggregate contradicts the reconciled leaf evidence.");
                invalidEvidence = true;
            }

            if (runFinishedTrusted && runFinished != null &&
                string.Equals(Normalize(runFinished.outcome), TestRunProtocol.LeafOutcome.Passed,
                    StringComparison.Ordinal) &&
                (summary.failed > 0 || summary.inconclusive > 0 ||
                 summary.cancelled > 0 || summary.invalid > 0))
            {
                AddError(issues, "ROOT_OUTCOME_CONTRADICTION", "", 0, runId,
                    "UTF root outcome is passed while reconciled leaves are not all passing/skipped.");
                invalidEvidence = true;
            }

            if (rootConflict || conflicts.Count > 0) invalidEvidence = true;

            if (runFinalized != null &&
                string.Equals(run?.lifecycle, TestRunProtocol.Lifecycle.Terminal,
                    StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(run.outcome) &&
                !string.Equals(Normalize(run.outcome), Normalize(runFinalized.outcome),
                    StringComparison.Ordinal))
            {
                AddError(issues, "TERMINAL_OUTCOME_CONTRADICTION", "", 0, runId,
                    "run.json and run_finalized evidence disagree about terminal outcome.");
                invalidEvidence = true;
            }

            SetTerminalTruth(summary, run, runFinished, runFinalized,
                runFinishedTrusted, invalidEvidence, dispatchFailed,
                explicitlyCancelled, explicitlyAbandoned, runFinalizedObserved);
            summary.issues = issues.ToArray();
            summary.state = summary.lifecycle;
            return summary;
        }

        private static bool ValidateManifestRecord(
            string runId, TestLeafManifestEntry leaf, ICollection<TestProtocolIssue> issues)
        {
            if (leaf == null)
            {
                AddError(issues, "MANIFEST_RECORD_INVALID", "", 0, "",
                    "Manifest record is null.");
                return false;
            }
            if (leaf.schema_version != TestRunProtocol.SchemaVersion)
            {
                AddError(issues, "MANIFEST_SCHEMA_UNSUPPORTED", "", 0, leaf.unique_name,
                    $"Manifest schema {leaf.schema_version} is not supported.");
                return false;
            }
            if (!string.Equals(leaf.run_id, runId, StringComparison.Ordinal))
            {
                AddError(issues, "MANIFEST_RUN_ID_MISMATCH", "", 0, leaf.unique_name,
                    "Manifest record belongs to a different run.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(leaf.unique_name))
            {
                AddError(issues, "MANIFEST_RECORD_INVALID", "", 0, "",
                    "Manifest record has no UniqueName.");
                return false;
            }
            return true;
        }

        private static bool ValidateEventRecord(
            string runId, TestRunEvent testEvent, ICollection<TestProtocolIssue> issues)
        {
            if (testEvent == null)
            {
                AddError(issues, "EVENT_RECORD_INVALID", "", 0, "", "Event record is null.");
                return false;
            }
            if (testEvent.schema_version != TestRunProtocol.SchemaVersion)
            {
                AddError(issues, "EVENT_SCHEMA_UNSUPPORTED", "", 0, testEvent.event_id,
                    $"Event schema {testEvent.schema_version} is not supported.");
                return false;
            }
            if (!string.Equals(testEvent.run_id, runId, StringComparison.Ordinal))
            {
                AddError(issues, "EVENT_RUN_ID_MISMATCH", "", 0, testEvent.event_id,
                    "Event belongs to a different run.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(testEvent.event_type))
            {
                AddError(issues, "EVENT_RECORD_INVALID", "", 0, testEvent.event_id,
                    "Event has no event_type.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(testEvent.event_id))
            {
                AddError(issues, "EVENT_ID_MISSING", "", 0, "",
                    "Event evidence requires a stable event_id.");
                return false;
            }
            return true;
        }

        private static LeafReplayState StateFor(
            IDictionary<string, LeafReplayState> states, string uniqueName)
        {
            if (states.TryGetValue(uniqueName, out var state)) return state;
            state = new LeafReplayState();
            states.Add(uniqueName, state);
            return state;
        }

        private static List<ReconciledLeafResult> BuildResults(
            IEnumerable<string> manifestOrder,
            IReadOnlyDictionary<string, TestLeafManifestEntry> manifests,
            IReadOnlyDictionary<string, LeafReplayState> states,
            IEnumerable<string> unexpected)
        {
            var results = new List<ReconciledLeafResult>();
            foreach (var name in manifestOrder)
            {
                if (!states.TryGetValue(name, out var state) || !state.HasTerminal) continue;
                results.Add(ToResult(state, manifests[name], true));
            }
            foreach (var name in unexpected)
            {
                var state = states[name];
                results.Add(ToResult(state, null, false));
            }
            return results;
        }

        private static ReconciledLeafResult ToResult(
            LeafReplayState state,
            TestLeafManifestEntry manifest,
            bool expected)
        {
            var evidence = state.Final;
            return new ReconciledLeafResult
            {
                unique_name = evidence.unique_name,
                test_id = string.IsNullOrEmpty(evidence.test_id) ? manifest?.test_id ?? "" : evidence.test_id,
                full_name = string.IsNullOrEmpty(evidence.full_name) ? manifest?.full_name ?? "" : evidence.full_name,
                expected = expected,
                outcome = Normalize(evidence.outcome),
                result_state = evidence.result_state ?? "",
                label = evidence.label ?? "",
                message = evidence.message ?? "",
                stack_trace = evidence.stack_trace ?? "",
                duration_seconds = evidence.duration_seconds,
                attempt_count = state.AttemptCount,
                attempts = state.CompletedAttempts
            };
        }

        private static void IncrementOutcome(TestRunSummary summary, string outcome)
        {
            switch (Normalize(outcome))
            {
                case TestRunProtocol.LeafOutcome.Passed: summary.passed++; break;
                case TestRunProtocol.LeafOutcome.Failed: summary.failed++; break;
                case TestRunProtocol.LeafOutcome.Skipped: summary.skipped++; break;
                case TestRunProtocol.LeafOutcome.Inconclusive: summary.inconclusive++; break;
                case TestRunProtocol.LeafOutcome.Cancelled: summary.cancelled++; break;
                case TestRunProtocol.LeafOutcome.Invalid: summary.invalid++; break;
            }
        }

        private static void SetTerminalTruth(
            TestRunSummary summary,
            TestRunRecord run,
            TestRunEvent runFinished,
            TestRunEvent runFinalized,
            bool rootTrusted,
            bool invalidEvidence,
            bool dispatchFailed,
            bool explicitlyCancelled,
            bool explicitlyAbandoned,
            bool runFinalizedObserved)
        {
            var storedOutcome = Normalize(run?.outcome);
            var rootOutcome = rootTrusted ? Normalize(runFinished?.outcome) : "";
            var storedTerminal = string.Equals(run?.lifecycle, TestRunProtocol.Lifecycle.Terminal,
                StringComparison.Ordinal);
            var executionFinished = summary.execution_finished || storedTerminal;

            if (!executionFinished)
                return;

            if (invalidEvidence || summary.invalid > 0 ||
                rootOutcome == TestRunProtocol.RunOutcome.Invalid ||
                storedOutcome == TestRunProtocol.RunOutcome.Invalid)
                summary.outcome = TestRunProtocol.RunOutcome.Invalid;
            else if (dispatchFailed || storedOutcome == TestRunProtocol.RunOutcome.DispatchFailed)
                summary.outcome = TestRunProtocol.RunOutcome.DispatchFailed;
            else if (explicitlyAbandoned ||
                     (!summary.run_finished_observed &&
                      storedOutcome == TestRunProtocol.RunOutcome.Incomplete))
                summary.outcome = TestRunProtocol.RunOutcome.Incomplete;
            else if (!summary.run_finished_observed)
                summary.outcome = explicitlyCancelled ||
                                  storedOutcome == TestRunProtocol.RunOutcome.Cancelled
                    ? TestRunProtocol.RunOutcome.Cancelled
                    : string.IsNullOrEmpty(storedOutcome)
                        ? TestRunProtocol.RunOutcome.Incomplete
                        : storedOutcome;
            else if (explicitlyCancelled || rootOutcome == TestRunProtocol.RunOutcome.Cancelled ||
                     storedOutcome == TestRunProtocol.RunOutcome.Cancelled)
                summary.outcome = TestRunProtocol.RunOutcome.Cancelled;
            else if (!summary.manifest_complete || summary.missing_count > 0)
                summary.outcome = TestRunProtocol.RunOutcome.Incomplete;
            else if (summary.failed > 0 || summary.inconclusive > 0 ||
                     rootOutcome == TestRunProtocol.RunOutcome.Failed ||
                     rootOutcome == TestRunProtocol.LeafOutcome.Inconclusive)
                summary.outcome = TestRunProtocol.RunOutcome.Failed;
            else if (summary.cancelled > 0)
                summary.outcome = TestRunProtocol.RunOutcome.Cancelled;
            else
                summary.outcome = TestRunProtocol.RunOutcome.Passed;

            // Zero-match: filter produced no tests — not a real pass
            if (summary.outcome == TestRunProtocol.RunOutcome.Passed &&
                summary.expected_count == 0 && summary.passed == 0)
                summary.outcome = TestRunProtocol.RunOutcome.NoTestsMatched;

            summary.outcome = MergeTerminalCheckpoint(
                summary.outcome, Normalize(runFinalized?.outcome));
            if (storedTerminal)
                summary.outcome = MergeTerminalCheckpoint(summary.outcome, storedOutcome);

            if (summary.outcome != TestRunProtocol.RunOutcome.DispatchFailed &&
                (run == null || !run.build_coherent))
                summary.outcome = TestRunProtocol.RunOutcome.Invalid;
            if (summary.outcome == TestRunProtocol.RunOutcome.Passed &&
                (!summary.run_started_observed || !summary.run_finished_observed ||
                 !summary.manifest_complete))
                summary.outcome = TestRunProtocol.RunOutcome.Invalid;

            summary.cleanup_complete = runFinalizedObserved;
            if (storedTerminal || runFinalizedObserved)
            {
                summary.lifecycle = TestRunProtocol.Lifecycle.Terminal;
                summary.is_terminal = true;
            }
        }

        private static string MergeTerminalCheckpoint(string derived, string checkpoint)
        {
            derived = Normalize(derived);
            checkpoint = Normalize(checkpoint);
            if (string.IsNullOrEmpty(checkpoint) || checkpoint == derived) return derived;
            if (string.IsNullOrEmpty(derived)) return checkpoint;

            // A durable checkpoint may be more pessimistic than later aggregate
            // evidence, but it must never upgrade an incomplete or failing run.
            return OutcomeRank(checkpoint) > OutcomeRank(derived) ? checkpoint : derived;
        }

        private static int OutcomeRank(string outcome)
        {
            switch (Normalize(outcome))
            {
                case TestRunProtocol.RunOutcome.Invalid: return 6;
                case TestRunProtocol.RunOutcome.DispatchFailed: return 5;
                case TestRunProtocol.RunOutcome.Incomplete: return 4;
                case TestRunProtocol.RunOutcome.Cancelled: return 3;
                case TestRunProtocol.RunOutcome.Failed: return 2;
                case TestRunProtocol.RunOutcome.Passed: return 1;
                case TestRunProtocol.RunOutcome.NoTestsMatched: return 1;
                default: return 0;
            }
        }

        private static bool IsLateExecutionEvidence(string eventType)
        {
            switch (eventType)
            {
                case TestRunProtocol.EventType.RunStarted:
                case TestRunProtocol.EventType.ManifestSealed:
                case TestRunProtocol.EventType.TestStarted:
                case TestRunProtocol.EventType.TestFinished:
                    return true;
                default:
                    return false;
            }
        }

        private static bool AggregateMatches(TestRunEvent root, TestRunSummary summary)
        {
            if (root.aggregate_passed < 0 || root.aggregate_failed < 0 ||
                root.aggregate_skipped < 0 || root.aggregate_inconclusive < 0 ||
                root.aggregate_cancelled < 0 || root.aggregate_invalid < 0 ||
                root.aggregate_total < 0)
                return false;

            var aggregateSum = root.aggregate_passed + root.aggregate_failed +
                               root.aggregate_skipped + root.aggregate_inconclusive +
                               root.aggregate_cancelled + root.aggregate_invalid;
            return root.aggregate_total == aggregateSum &&
                   root.aggregate_total == summary.expected_count &&
                   root.aggregate_passed == summary.passed &&
                   root.aggregate_failed == summary.failed &&
                   root.aggregate_skipped == summary.skipped &&
                   root.aggregate_inconclusive == summary.inconclusive &&
                   root.aggregate_cancelled == summary.cancelled &&
                   root.aggregate_invalid == summary.invalid;
        }

        private static bool SameManifestEntry(TestLeafManifestEntry left, TestLeafManifestEntry right) =>
            left.schema_version == right.schema_version &&
            left.run_id == right.run_id &&
            left.unique_name == right.unique_name &&
            left.test_id == right.test_id &&
            left.full_name == right.full_name &&
            left.assembly_name == right.assembly_name &&
            left.mode == right.mode;

        private static bool SameEventRecord(TestRunEvent left, TestRunEvent right) =>
            left.schema_version == right.schema_version &&
            left.run_id == right.run_id &&
            left.event_id == right.event_id &&
            left.event_type == right.event_type &&
            left.occurred_utc == right.occurred_utc &&
            left.observer_generation == right.observer_generation &&
            left.unique_name == right.unique_name &&
            left.test_id == right.test_id &&
            left.full_name == right.full_name &&
            left.outcome == right.outcome &&
            left.result_state == right.result_state &&
            left.label == right.label &&
            left.message == right.message &&
            left.stack_trace == right.stack_trace &&
            left.duration_seconds.Equals(right.duration_seconds) &&
            left.expected_count == right.expected_count &&
            left.root_trusted == right.root_trusted &&
            left.has_aggregate == right.has_aggregate &&
            left.aggregate_passed == right.aggregate_passed &&
            left.aggregate_failed == right.aggregate_failed &&
            left.aggregate_skipped == right.aggregate_skipped &&
            left.aggregate_inconclusive == right.aggregate_inconclusive &&
            left.aggregate_cancelled == right.aggregate_cancelled &&
            left.aggregate_invalid == right.aggregate_invalid &&
            left.aggregate_total == right.aggregate_total;

        private static bool SameTerminalEvidence(TestRunEvent left, TestRunEvent right) =>
            left.unique_name == right.unique_name &&
            Normalize(left.outcome) == Normalize(right.outcome) &&
            left.result_state == right.result_state &&
            left.label == right.label &&
            left.message == right.message &&
            left.stack_trace == right.stack_trace;

        private static bool SameRunFinished(TestRunEvent left, TestRunEvent right) =>
            Normalize(left.outcome) == Normalize(right.outcome) &&
            left.result_state == right.result_state &&
            left.label == right.label &&
            left.message == right.message &&
            left.stack_trace == right.stack_trace &&
            left.root_trusted == right.root_trusted &&
            left.has_aggregate == right.has_aggregate &&
            left.aggregate_passed == right.aggregate_passed &&
            left.aggregate_failed == right.aggregate_failed &&
            left.aggregate_skipped == right.aggregate_skipped &&
            left.aggregate_inconclusive == right.aggregate_inconclusive &&
            left.aggregate_cancelled == right.aggregate_cancelled &&
            left.aggregate_invalid == right.aggregate_invalid &&
            left.aggregate_total == right.aggregate_total;

        private static bool IsLeafOutcome(string outcome)
        {
            switch (Normalize(outcome))
            {
                case TestRunProtocol.LeafOutcome.Passed:
                case TestRunProtocol.LeafOutcome.Failed:
                case TestRunProtocol.LeafOutcome.Skipped:
                case TestRunProtocol.LeafOutcome.Inconclusive:
                case TestRunProtocol.LeafOutcome.Cancelled:
                case TestRunProtocol.LeafOutcome.Invalid:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsRunFinishedOutcome(string outcome)
        {
            switch (Normalize(outcome))
            {
                case TestRunProtocol.RunOutcome.Passed:
                case TestRunProtocol.RunOutcome.Failed:
                case TestRunProtocol.LeafOutcome.Skipped:
                case TestRunProtocol.LeafOutcome.Inconclusive:
                case TestRunProtocol.RunOutcome.Cancelled:
                case TestRunProtocol.RunOutcome.Invalid:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTerminalRunOutcome(string outcome)
        {
            switch (Normalize(outcome))
            {
                case TestRunProtocol.RunOutcome.Passed:
                case TestRunProtocol.RunOutcome.Failed:
                case TestRunProtocol.RunOutcome.Cancelled:
                case TestRunProtocol.RunOutcome.Incomplete:
                case TestRunProtocol.RunOutcome.Invalid:
                case TestRunProtocol.RunOutcome.DispatchFailed:
                case TestRunProtocol.RunOutcome.NoTestsMatched:
                    return true;
                default:
                    return false;
            }
        }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

        private static void AddWarning(
            ICollection<TestProtocolIssue> issues,
            string code, string path, int line, string identity, string message) =>
            issues.Add(new TestProtocolIssue
            {
                code = code,
                severity = TestRunProtocol.IssueSeverity.Warning,
                path = path,
                line = line,
                identity = identity,
                message = message
            });

        private static void AddError(
            ICollection<TestProtocolIssue> issues,
            string code, string path, int line, string identity, string message) =>
            issues.Add(new TestProtocolIssue
            {
                code = code,
                severity = TestRunProtocol.IssueSeverity.Error,
                path = path,
                line = line,
                identity = identity,
                message = message
            });

        private sealed class LeafReplayState
        {
            private readonly List<TestRunEvent> _completed = new List<TestRunEvent>();
            private TestRunEvent _currentTerminal;
            private bool _attemptOpen;

            public int AttemptCount { get; private set; }
            public int CompletedAttemptCount => _completed.Count;
            public int ObservedStartCallbackCount { get; private set; }
            public int FinishWithoutStartCount { get; private set; }
            public bool Conflicting { get; private set; }
            public bool HasTerminal => _currentTerminal != null && !_attemptOpen;
            public bool AttemptOpen => _attemptOpen;
            public TestRunEvent Final => _currentTerminal;
            public ReconciledAttemptResult[] CompletedAttempts => _completed
                .Select((evidence, index) => new ReconciledAttemptResult
                {
                    attempt = index + 1,
                    outcome = Normalize(evidence.outcome),
                    result_state = evidence.result_state ?? "",
                    label = evidence.label ?? "",
                    message = evidence.message ?? "",
                    stack_trace = evidence.stack_trace ?? "",
                    duration_seconds = evidence.duration_seconds
                })
                .ToArray();

            public bool StartAttempt()
            {
                ObservedStartCallbackCount++;
                if (_attemptOpen && _currentTerminal == null)
                    return false; // At-least-once duplicate TestStarted delivery.

                AttemptCount++;
                _attemptOpen = true;
                _currentTerminal = null;
                return true;
            }

            public bool FinishAttempt(TestRunEvent evidence)
            {
                if (AttemptCount == 0)
                {
                    // A callback observer can be reattached after TestStarted during
                    // domain reload. The terminal callback is still usable evidence.
                    AttemptCount = 1;
                    FinishWithoutStartCount++;
                    _attemptOpen = true;
                }

                if (_currentTerminal == null)
                {
                    _currentTerminal = evidence;
                    _completed.Add(evidence);
                    _attemptOpen = false;
                    return true;
                }

                if (SameTerminalEvidence(_currentTerminal, evidence)) return true;
                Conflicting = true;
                return false;
            }
        }
    }
}
