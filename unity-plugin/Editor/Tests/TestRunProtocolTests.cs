using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunProtocolTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string RunId = "run-001";
        private string _root;
        private TestRunStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "unity-mcp-run-protocol-" + Guid.NewGuid().ToString("N"));
            _store = new TestRunStore(_root);
            _store.WriteRun(new TestRunRecord
            {
                run_id = RunId,
                request_id = "request-001",
                source = "unity-ui",
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = "2026-08-02T12:00:00.0000000Z",
                build_coherent = true,
                utf_version = "1.6.0"
            });
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunStarted,
                observer_generation = "test-gen"
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void PassedRequiresSealedManifestRunFinishedAndEveryExpectedLeaf()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed);

            var beforeRunFinished = _store.Reconcile(RunId);

            Assert.IsFalse(beforeRunFinished.is_terminal);
            Assert.AreEqual("", beforeRunFinished.outcome);

            RunFinished(TestRunProtocol.RunOutcome.Passed);
            var completed = _store.Reconcile(RunId);

            Assert.IsTrue(completed.is_terminal);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Passed, completed.outcome);
            Assert.AreEqual(1, completed.expected_count);
            Assert.AreEqual(1, completed.completed_expected_count);
            Assert.AreEqual(1, completed.passed);
            Assert.AreEqual(0, completed.missing_count);
            Assert.IsTrue(completed.manifest_complete);
            Assert.IsTrue(completed.run_finished_observed);
        }

        [Test]
        public void UnityUiRun_CanPassFromCompleteLeafEvidenceWithoutUtfGuid()
        {
            AddExpected("suite.ui-test");
            SealManifest(1);
            Finish("suite.ui-test", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.IsTrue(summary.is_terminal);
            Assert.IsTrue(summary.cleanup_complete);
            Assert.AreEqual("unity-ui", summary.source);
            Assert.AreEqual("", summary.utf_guid);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Passed, summary.outcome);
        }

        [Test]
        public void RuntimeReconciliationAcceptsCoherentNonCanonicalUtfVersion()
        {
            const string runId = "run-compatible-utf";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = "2026-08-02T12:00:00.0000000Z",
                build_coherent = true,
                utf_version = "1.6.0"
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunStarted,
                observer_generation = "compatible-gen"
            });
            _store.SealManifest(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.ManifestSealed,
                expected_count = 0
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunFinished,
                outcome = TestRunProtocol.RunOutcome.Passed,
                observer_generation = "compatible-gen",
                root_trusted = true
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunFinalized,
                outcome = TestRunProtocol.RunOutcome.Passed
            });

            Assert.AreEqual(TestRunProtocol.RunOutcome.NoTestsMatched,
                _store.Reconcile(runId).outcome);
        }

        [Test]
        public void MissingLeafAfterRunFinishedIsIncompleteNotPartialPass()
        {
            AddExpected("suite.test-a");
            AddExpected("suite.test-b");
            SealManifest(2);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Incomplete, summary.outcome);
            Assert.AreEqual(2, summary.expected_count);
            Assert.AreEqual(1, summary.completed_expected_count);
            Assert.AreEqual(1, summary.missing_count);
            CollectionAssert.AreEqual(new[] { "suite.test-b" }, summary.missing_tests);
        }

        [Test]
        public void DeclaredExpectedCountSurvivesTruncatedReadableManifest()
        {
            AddExpected("suite.readable");
            SealManifest(6964);
            Finish("suite.readable", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(6964, summary.expected_count);
            Assert.AreEqual(6964, summary.declared_expected_count);
            Assert.AreEqual(1, summary.readable_manifest_count);
            Assert.AreEqual(6963, summary.unmaterialized_expected_count);
            Assert.AreEqual(6963, summary.missing_count);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Incomplete, summary.outcome);
        }

        [Test]
        public void LeafEvidenceAfterRunFinishedCannotTurnIncompleteIntoPassed()
        {
            AddExpected("suite.late");
            SealManifest(1);
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinished,
                outcome = TestRunProtocol.RunOutcome.Passed,
                observer_generation = "test-gen",
                root_trusted = true
            });
            Finish("suite.late", TestRunProtocol.LeafOutcome.Passed);
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinalized
            });

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(0, summary.completed_expected_count);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsTrue(summary.issues.Any(i => i.code == "EVIDENCE_AFTER_RUN_FINISHED"));
        }

        [TestCase(TestRunProtocol.LeafOutcome.Invalid)]
        [TestCase(TestRunProtocol.LeafOutcome.Cancelled)]
        public void TerminalLeafOutcomeDoesNotEndRunBeforeRunFinished(string outcome)
        {
            AddExpected("suite.first");
            AddExpected("suite.second");
            SealManifest(2);
            Finish("suite.first", outcome);

            var running = _store.Reconcile(RunId);

            Assert.IsFalse(running.is_terminal);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Running, running.lifecycle);
            Assert.AreEqual("", running.outcome);

            RunFinished(outcome == TestRunProtocol.LeafOutcome.Cancelled
                ? TestRunProtocol.RunOutcome.Cancelled
                : TestRunProtocol.RunOutcome.Invalid);
            Assert.IsTrue(_store.Reconcile(RunId).is_terminal);
        }

        [Test]
        public void AuthoritativeCancellationWinsWhileRetainingMissingMetrics()
        {
            AddExpected("suite.current");
            AddExpected("suite.not-run");
            SealManifest(2);
            Finish("suite.current", TestRunProtocol.LeafOutcome.Cancelled);

            RunFinished(TestRunProtocol.RunOutcome.Cancelled);
            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Cancelled, summary.outcome);
            Assert.AreEqual(1, summary.missing_count);
        }

        [Test]
        public void AbandonedEventRequiresCleanupFinalizationBoundary()
        {
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.Abandoned
            });

            var summary = _store.Reconcile(RunId);

            Assert.IsFalse(summary.is_terminal);
            Assert.IsTrue(summary.execution_finished);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Incomplete, summary.outcome);

            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinalized
            });
            Assert.IsTrue(_store.Reconcile(RunId).is_terminal);
        }

        [Test]
        public void StartedLeafOutsideManifestInvalidatesOnlyAtRunBoundary()
        {
            AddExpected("suite.expected");
            SealManifest(1);
            Start("suite.unexpected");

            var running = _store.Reconcile(RunId);

            Assert.IsFalse(running.is_terminal);
            Assert.AreEqual(1, running.unexpected_count);
            CollectionAssert.AreEqual(new[] { "suite.unexpected" }, running.unexpected_tests);

            RunFinished(TestRunProtocol.RunOutcome.Passed);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid,
                _store.Reconcile(RunId).outcome);
        }

        [Test]
        public void DomainReloadCheckpointIsNonTerminalOperationalEvidence()
        {
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.DomainReloading
            });

            var summary = _store.Reconcile(RunId);

            Assert.IsFalse(summary.is_terminal);
            Assert.IsFalse(summary.issues.Any(i => i.code == "EVENT_TYPE_UNKNOWN"));
        }

        [Test]
        public void IdenticalTerminalDeliveryIsIdempotent()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Start("suite.test-a");
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed, "Passed", 0.25);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed, "Passed", 0.25);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var first = _store.Reconcile(RunId);
            var firstJson = JsonUtility.ToJson(first);
            var second = _store.Reconcile(RunId);

            Assert.AreEqual(firstJson, JsonUtility.ToJson(second));
            Assert.AreEqual(1, second.passed);
            Assert.AreEqual(1, second.unique_terminal_count);
            Assert.AreEqual(0, second.conflict_count);
            Assert.AreEqual(1, second.leaves.Single().attempt_count);
            Assert.AreEqual(1, second.leaves.Single().attempts.Length);
        }

        [Test]
        public void DifferentTerminalDeliveryWithoutNewStartIsInvalidConflict()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Start("suite.test-a");
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Failed, "Failed");
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed, "Passed");
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.AreEqual(1, summary.conflict_count);
            CollectionAssert.Contains(summary.conflicting_tests, "suite.test-a");
            Assert.IsTrue(summary.issues.Any(i => i.code == "TERMINAL_EVIDENCE_CONFLICT"));
        }

        [Test]
        public void NewStartCreatesLegitimateRetryAndLastAttemptWins()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Start("suite.test-a");
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Failed, "Failed", 0.2);
            Start("suite.test-a");
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed, "Passed", 0.1);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);
            var leaf = summary.leaves.Single();

            Assert.AreEqual(TestRunProtocol.RunOutcome.Passed, summary.outcome);
            Assert.AreEqual(TestRunProtocol.LeafOutcome.Passed, leaf.outcome);
            Assert.AreEqual(2, leaf.attempt_count);
            Assert.AreEqual(2, leaf.attempts.Length);
            Assert.AreEqual(2, summary.started_attempt_count);
            Assert.AreEqual(2, summary.finished_attempt_count);
            Assert.AreEqual("", summary.current_test);
            Assert.AreEqual(TestRunProtocol.LeafOutcome.Failed, leaf.attempts[0].outcome);
            Assert.AreEqual(TestRunProtocol.LeafOutcome.Passed, leaf.attempts[1].outcome);
        }

        [Test]
        public void EveryTerminalLeafStatusIsCounted()
        {
            var outcomes = new[]
            {
                TestRunProtocol.LeafOutcome.Passed,
                TestRunProtocol.LeafOutcome.Failed,
                TestRunProtocol.LeafOutcome.Skipped,
                TestRunProtocol.LeafOutcome.Inconclusive,
                TestRunProtocol.LeafOutcome.Cancelled,
                TestRunProtocol.LeafOutcome.Invalid
            };
            foreach (var outcome in outcomes) AddExpected("suite." + outcome);
            SealManifest(outcomes.Length);
            foreach (var outcome in outcomes) Finish("suite." + outcome, outcome);
            RunFinished(TestRunProtocol.RunOutcome.Invalid);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(1, summary.passed);
            Assert.AreEqual(1, summary.failed);
            Assert.AreEqual(1, summary.skipped);
            Assert.AreEqual(1, summary.inconclusive);
            Assert.AreEqual(1, summary.cancelled);
            Assert.AreEqual(1, summary.invalid);
            Assert.AreEqual(6, summary.completed_expected_count);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
        }

        [Test]
        public void UnexpectedTerminalEvidenceInvalidatesRun()
        {
            AddExpected("suite.expected");
            SealManifest(1);
            Finish("suite.expected", TestRunProtocol.LeafOutcome.Passed);
            Finish("suite.not-in-manifest", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.AreEqual(1, summary.unexpected_count);
            CollectionAssert.AreEqual(new[] { "suite.not-in-manifest" }, summary.unexpected_tests);
        }

        [Test]
        public void ConflictingManifestDuplicateInvalidatesRun()
        {
            AddExpected("suite.test-a", "id-1");
            AddExpected("suite.test-a", "id-2");
            SealManifest(1);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.AreEqual(1, summary.conflict_count);
            Assert.IsTrue(summary.issues.Any(i => i.code == "MANIFEST_CONFLICT"));
        }

        [Test]
        public void CorruptJournalLineIsPreservedAndSurfaced()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed);
            RunFinished(TestRunProtocol.RunOutcome.Passed);
            var path = _store.GetEventsPath(RunId);
            File.AppendAllText(path, "{\"torn\":\n");
            var evidenceBefore = File.ReadAllText(path);

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(evidenceBefore, File.ReadAllText(path));
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            var issue = summary.issues.Single(i => i.code == "EVENT_LINE_CORRUPT");
            Assert.Greater(issue.line, 0);
            Assert.AreEqual(path, issue.path);
        }

        [Test]
        public void RootAggregateContradictionInvalidatesRun()
        {
            AddExpected("suite.test-a");
            SealManifest(1);
            Finish("suite.test-a", TestRunProtocol.LeafOutcome.Passed);
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinished,
                outcome = TestRunProtocol.RunOutcome.Passed,
                observer_generation = "test-gen",
                root_trusted = true,
                has_aggregate = true,
                aggregate_failed = 1,
                aggregate_total = 1
            });
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinalized
            });

            var summary = _store.Reconcile(RunId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsTrue(summary.issues.Any(i => i.code == "ROOT_AGGREGATE_CONTRADICTION"));
        }

        [Test]
        public void EmptySealedRunCanCompleteWithoutSpecialCaseFiles()
        {
            SealManifest(0);
            RunFinished(TestRunProtocol.RunOutcome.Passed);

            var summary = _store.Reconcile(RunId);

            Assert.IsTrue(File.Exists(_store.GetExpectedTestsPath(RunId)));
            Assert.IsTrue(summary.manifest_complete);
            Assert.AreEqual(0, summary.expected_count);
            Assert.AreEqual(TestRunProtocol.RunOutcome.NoTestsMatched, summary.outcome);
        }

        [Test]
        public void RequestIdempotencyKeyNeverRedirectsToSecondRun()
        {
            var first = _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = "stable-request",
                run_id = "first-run",
                intent_complete = true,
                mode = "EditMode",
                group = "FirstGroup",
                filter = "FirstFilter"
            });
            var retry = _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = "stable-request",
                run_id = "second-run",
                intent_complete = true,
                mode = "PlayMode",
                group = "ReplacementGroup",
                filter = "ReplacementFilter"
            });

            Assert.AreEqual("first-run", first.run_id);
            Assert.AreEqual("first-run", retry.run_id);
            Assert.AreEqual("EditMode", retry.mode);
            Assert.AreEqual("FirstGroup", retry.group);
            Assert.AreEqual("FirstFilter", retry.filter);
            Assert.AreEqual("first-run", _store.ReadRequest("stable-request").run_id);
        }

        [Test]
        public void CompleteRequestIntentIsImmutableAfterFirstCommit()
        {
            _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = "immutable-intent",
                run_id = RunId,
                intent_complete = true,
                mode = "PlayMode",
                group = "OriginalGroup",
                filter = "OriginalFilter",
                created_utc = "2026-08-02T12:00:00.0000000Z"
            });

            var rewriteMode = _store.ReadRequest("immutable-intent");
            rewriteMode.mode = "EditMode";
            Assert.Throws<TestRunStoreException>(() => _store.WriteRequest(rewriteMode));

            var rewriteGroup = _store.ReadRequest("immutable-intent");
            rewriteGroup.group = "OtherGroup";
            Assert.Throws<TestRunStoreException>(() => _store.WriteRequest(rewriteGroup));

            var rewriteFilter = _store.ReadRequest("immutable-intent");
            rewriteFilter.filter = "OtherFilter";
            Assert.Throws<TestRunStoreException>(() => _store.WriteRequest(rewriteFilter));

            var removeMarker = _store.ReadRequest("immutable-intent");
            removeMarker.intent_complete = false;
            Assert.Throws<TestRunStoreException>(() => _store.WriteRequest(removeMarker));

            var durable = _store.ReadRequest("immutable-intent");
            Assert.AreEqual("PlayMode", durable.mode);
            Assert.AreEqual("OriginalGroup", durable.group);
            Assert.AreEqual("OriginalFilter", durable.filter);
            Assert.IsTrue(durable.intent_complete);
        }

        [Test]
        public void ValidActivePointerIsNeverQuarantined()
        {
            _store.WriteActive(new TestRunPointer
            {
                run_id = RunId,
                request_id = "request-001",
                updated_utc = "2026-08-02T12:00:00.0000000Z"
            });

            Assert.AreEqual("", _store.QuarantineCorruptActive());
            Assert.IsTrue(File.Exists(_store.ActivePath));
            Assert.IsFalse(Directory.Exists(_store.QuarantinePath));
        }

        [Test]
        public void DurableRunLifecycleIsMonotonicAndTerminalIsAbsorbing()
        {
            var terminal = _store.ReadRun(RunId);
            terminal.lifecycle = TestRunProtocol.Lifecycle.Terminal;
            terminal.outcome = TestRunProtocol.RunOutcome.Failed;
            terminal.finished_utc = "2026-08-02T12:05:00.0000000Z";
            _store.WriteRun(terminal);

            var downgrade = _store.ReadRun(RunId);
            downgrade.lifecycle = TestRunProtocol.Lifecycle.Running;

            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(downgrade));
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(RunId).lifecycle);
        }

        [Test]
        public void RunOutcomeIsUnsetUntilTerminalAndImmutableAfterCommit()
        {
            var premature = _store.ReadRun(RunId);
            premature.outcome = TestRunProtocol.RunOutcome.Failed;
            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(premature));

            var terminal = _store.ReadRun(RunId);
            terminal.lifecycle = TestRunProtocol.Lifecycle.Terminal;
            terminal.outcome = TestRunProtocol.RunOutcome.Failed;
            _store.WriteRun(terminal);

            var rewrite = _store.ReadRun(RunId);
            rewrite.outcome = TestRunProtocol.RunOutcome.Invalid;
            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(rewrite));
        }

        [Test]
        public void NewRunCannotBypassTerminalOutcomeInvariant()
        {
            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(new TestRunRecord
            {
                run_id = "run-premature-outcome",
                lifecycle = TestRunProtocol.Lifecycle.Finalizing,
                outcome = TestRunProtocol.RunOutcome.Incomplete
            }));
            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(new TestRunRecord
            {
                run_id = "run-terminal-without-outcome",
                lifecycle = TestRunProtocol.Lifecycle.Terminal
            }));
            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(new TestRunRecord
            {
                run_id = "run-terminal-unknown-outcome",
                lifecycle = TestRunProtocol.Lifecycle.Terminal,
                outcome = "unknown"
            }));
        }

        [Test]
        public void DurableUtfGuidIsSetOnce()
        {
            var first = _store.ReadRun(RunId);
            first.utf_guid = "utf-first";
            _store.WriteRun(first);

            var rewrite = _store.ReadRun(RunId);
            rewrite.utf_guid = "utf-other";

            Assert.Throws<TestRunStoreException>(() => _store.WriteRun(rewrite));
            Assert.AreEqual("utf-first", _store.ReadRun(RunId).utf_guid);
        }

        [Test]
        public void DurableRequestStateCannotMoveBackward()
        {
            var request = _store.CreateRequestOnce(new TestRunRequestRecord
            {
                request_id = "request-monotonic",
                run_id = RunId,
                state = TestRunProtocol.Lifecycle.Prepared,
                created_utc = "2026-08-02T12:00:00.0000000Z"
            });
            request.state = TestRunProtocol.Lifecycle.Terminal;
            _store.WriteRequest(request);

            var downgrade = _store.ReadRequest("request-monotonic");
            downgrade.state = TestRunProtocol.Lifecycle.Prepared;

            Assert.Throws<TestRunStoreException>(() => _store.WriteRequest(downgrade));
        }

        [Test]
        public void SceneEnvironmentRoundtripsWithoutGlobalEditorState()
        {
            var environment = new TestRunEnvironmentRecord
            {
                run_id = RunId,
                scene_paths = new[] { "Assets/Scenes/A.unity", "Assets/Scenes/B.unity" },
                active_scene_path = "Assets/Scenes/B.unity",
                untitled_scene_setup = "",
                owned_scene_path = "Assets/TestsTemp/run-001.unity",
                scene_restore_delegated_to_utf = false,
                preview_scene_baseline_captured = true,
                preview_scene_count = 2,
                prepared_utc = "2026-08-02T12:00:01.0000000Z"
            };
            _store.WriteEnvironment(environment);

            Assert.IsTrue(_store.TryReadEnvironment(RunId, out var restored));
            CollectionAssert.AreEqual(
                new[] { "Assets/Scenes/A.unity", "Assets/Scenes/B.unity" },
                restored.scene_paths);
            Assert.AreEqual("Assets/Scenes/B.unity", restored.active_scene_path);
            Assert.AreEqual("Assets/TestsTemp/run-001.unity", restored.owned_scene_path);
            Assert.IsTrue(restored.preview_scene_baseline_captured);
            Assert.AreEqual(2, restored.preview_scene_count);
            Assert.AreEqual(_store.GetEnvironmentPath(RunId),
                Path.Combine(_store.GetRunDirectory(RunId), "environment.json"));

            environment.restored_utc = "2026-08-02T12:03:00.0000000Z";
            _store.WriteEnvironment(environment);
            Assert.AreEqual(environment.restored_utc, _store.ReadEnvironment(RunId).restored_utc);

            environment.active_scene_path = "Assets/Scenes/A.unity";
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));

            environment.active_scene_path = "Assets/Scenes/B.unity";
            environment.untitled_scene_setup = TestRunProtocol.UntitledSceneSetup.Empty;
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));

            environment.untitled_scene_setup = "";
            environment.scene_restore_delegated_to_utf = true;
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));

            environment.scene_restore_delegated_to_utf = false;
            environment.preview_scene_count = 3;
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));
            Assert.AreEqual(2, _store.ReadEnvironment(RunId).preview_scene_count);

            environment.preview_scene_count = 2;
            environment.preview_scene_baseline_captured = false;
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));
            Assert.IsTrue(_store.ReadEnvironment(RunId).preview_scene_baseline_captured);
        }

        [Test]
        public void SceneEnvironmentRejectsContradictoryPreviewEvidence()
        {
            var environment = new TestRunEnvironmentRecord
            {
                run_id = RunId,
                restore_single_untitled = true,
                untitled_scene_setup = TestRunProtocol.UntitledSceneSetup.Empty,
                owned_scene_path = "Assets/TestsTemp/run-001.unity",
                preview_scene_baseline_captured = false,
                preview_scene_count = 1,
                prepared_utc = "2026-08-02T12:00:01.0000000Z"
            };

            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));
            Assert.IsFalse(_store.TryReadEnvironment(RunId, out _));

            environment.preview_scene_baseline_captured = true;
            environment.preview_scene_count = -1;
            Assert.Throws<TestRunStoreException>(() => _store.WriteEnvironment(environment));
            Assert.IsFalse(_store.TryReadEnvironment(RunId, out _));

            environment.preview_scene_baseline_captured = false;
            environment.preview_scene_count = 0;
            _store.WriteEnvironment(environment);
            Assert.IsFalse(_store.ReadEnvironment(RunId).preview_scene_baseline_captured);
        }

        [Test]
        public void TerminalSummaryCacheTracksRunAndEnvironmentEvidence()
        {
            var environment = new TestRunEnvironmentRecord
            {
                run_id = RunId,
                restore_single_untitled = true,
                untitled_scene_setup = TestRunProtocol.UntitledSceneSetup.Empty,
                owned_scene_path = "Assets/TestsTemp/run-001.unity",
                prepared_utc = "2026-08-02T12:00:01.0000000Z"
            };
            _store.WriteEnvironment(environment);
            var summary = _store.Reconcile(RunId, true);
            Assert.IsTrue(_store.IsSummaryCurrent(RunId, summary));

            environment.restored_utc = "2026-08-02T12:03:00.0000000Z";
            _store.WriteEnvironment(environment);

            Assert.IsFalse(_store.IsSummaryCurrent(RunId, summary));
        }

        [Test]
        public void PointerReadsDistinguishMissingFromCorruptAndRunsAreStableNewestFirst()
        {
            Assert.IsFalse(_store.TryReadActive(out _));
            Assert.IsFalse(_store.TryReadLatest(out _));

            var pointer = new TestRunPointer
            {
                run_id = RunId,
                request_id = "request-001",
                updated_utc = "2026-08-02T12:00:01.0000000Z"
            };
            _store.WriteActive(pointer);
            _store.WriteLatest(pointer);
            _store.WriteRun(new TestRunRecord
            {
                run_id = "run-newer",
                request_id = "request-newer",
                created_utc = "2026-08-02T13:00:00.0000000Z"
            });

            Assert.IsTrue(_store.TryReadActive(out var active));
            Assert.IsTrue(_store.TryReadLatest(out var latest));
            Assert.AreEqual(RunId, active.run_id);
            Assert.AreEqual(RunId, latest.run_id);
            CollectionAssert.AreEqual(new[] { "run-newer", RunId }, _store.ListRunIds());
        }

        [Test]
        public void UnsafeIdentityCannotEscapeStoreRoot()
        {
            Assert.Throws<ArgumentException>(() => _store.GetRunDirectory("../outside"));
            Assert.Throws<ArgumentException>(() => _store.GetRequestPath("nested/path"));
        }

        private void AddExpected(string uniqueName, string id = "")
        {
            _store.AppendExpectedTest(RunId, new TestLeafManifestEntry
            {
                run_id = RunId,
                unique_name = uniqueName,
                test_id = id,
                full_name = uniqueName
            });
        }

        private void SealManifest(int expectedCount)
        {
            _store.SealManifest(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.ManifestSealed,
                expected_count = expectedCount
            });
        }

        private void Start(string uniqueName)
        {
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.TestStarted,
                unique_name = uniqueName
            });
        }

        private void Finish(
            string uniqueName,
            string outcome,
            string resultState = "",
            double duration = 0.1)
        {
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.TestFinished,
                unique_name = uniqueName,
                full_name = uniqueName,
                outcome = outcome,
                result_state = resultState,
                duration_seconds = duration
            });
        }

        private void RunFinished(string outcome)
        {
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinished,
                outcome = outcome,
                observer_generation = "test-gen",
                root_trusted = true
            });
            _store.AppendEvent(RunId, new TestRunEvent
            {
                run_id = RunId,
                event_type = TestRunProtocol.EventType.RunFinalized,
                outcome = outcome
            });
        }
    }
}
