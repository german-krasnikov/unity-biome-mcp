using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunnerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _root;
        private TestRunStore _store;
        private FakeFrameworkDriver _framework;
        private FakeEnvironment _environment;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-runner-" + Guid.NewGuid().ToString("N"));
            _store = new TestRunStore(_root);
            _framework = new FakeFrameworkDriver();
            _environment = new FakeEnvironment();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void Start_ReturnsImmediateCorrelatedAckAndPersistsGuid()
        {
            var service = CreateService();

            var ack = service.Start("request-1", "EditMode", null, "RunnerTests");

            StringAssert.StartsWith("tests-started|request_id=request-1|", ack);
            StringAssert.Contains("|run_id=run-", ack);
            StringAssert.Contains("|utf_guid=utf-guid-1|state=dispatched", ack);
            Assert.AreEqual(1, _framework.ExecuteCalls);
            var run = _store.ReadRun(_store.ReadRequest("request-1").run_id);
            Assert.AreEqual("utf-guid-1", run.utf_guid);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Dispatched, run.lifecycle);
            Assert.AreEqual("1.6.0", run.utf_version);
            Assert.IsTrue(run.build_coherent);
        }

        [TestCase("PlayMode", "group-a", "filter-a")]
        [TestCase("EditMode", "group-b", "filter-a")]
        [TestCase("EditMode", "group-a", "filter-b")]
        public void Start_ReusedRequestIdRejectsChangedImmutableIntent(
            string secondMode,
            string secondGroup,
            string secondFilter)
        {
            var service = CreateService();
            var first = service.Start(
                "request-intent-conflict", "EditMode", "group-a", "filter-a");

            var second = service.Start(
                "request-intent-conflict", secondMode, secondGroup, secondFilter);

            StringAssert.StartsWith("tests-started|", first);
            StringAssert.StartsWith("Error: request_id is already bound", second);
            Assert.AreEqual(1, _framework.ExecuteCalls);
            var request = _store.ReadRequest("request-intent-conflict");
            Assert.AreEqual("EditMode", request.mode);
            Assert.AreEqual("group-a", request.group);
            Assert.AreEqual("filter-a", request.filter);
        }

        [Test]
        public void Start_PersistsDispatchBoundaryBeforeCallingUtf()
        {
            _framework.OnExecute = () =>
            {
                var runId = _store.ReadRequest("request-boundary").run_id;
                var duringExecute = _store.ReadRun(runId);
                Assert.AreEqual(TestRunProtocol.Lifecycle.Dispatched,
                    duringExecute.lifecycle);
                Assert.IsNotEmpty(duringExecute.dispatched_utc);
                Assert.AreEqual("", duringExecute.utf_guid);
            };

            CreateService().Start("request-boundary", "EditMode", null, null);

            Assert.AreEqual(1, _framework.ExecuteCalls);
        }

        [Test]
        public void Start_RunStartedInfrastructureFailureCancelsExactGuidOnceAndFailsFast()
        {
            _framework.OnExecute = () =>
            {
                var runId = _store.ReadRequest("request-start-failure").run_id;
                _store.AppendEvent(runId, new TestRunEvent
                {
                    run_id = runId,
                    event_id = "observer-start-failure",
                    event_type = TestRunProtocol.EventType.InfrastructureError,
                    occurred_utc = Utc,
                    observer_generation = "test-generation",
                    message = "RunStarted observer failed: scene baseline is not empty"
                });
                var failed = _store.ReadRun(runId);
                failed.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                failed.health = TestRunProtocol.Health.SuspectedStall;
                _store.WriteRun(failed);
            };
            var service = CreateService();

            var first = service.Start(
                "request-start-failure", "EditMode", null, null);
            var retry = CreateService().Start(
                "request-start-failure", "EditMode", null, null);
            var resolved = service.Resolve("request-start-failure");

            var runId = _store.ReadRequest("request-start-failure").run_id;
            StringAssert.StartsWith("test-request|request_id=request-start-failure|run_id=" +
                runId + "|state=finalizing|outcome=", first);
            Assert.AreEqual("RunStarted observer failed: scene baseline is not empty",
                DecodeReason(first));
            Assert.AreEqual(first, retry);
            Assert.AreEqual(first, resolved);
            Assert.AreEqual(1, _framework.ExecuteCalls);
            Assert.AreEqual(1, _framework.CancelCalls);
            Assert.AreEqual("utf-guid-1", _framework.LastCancelledGuid);
            Assert.AreEqual("utf-guid-1", _store.ReadRun(runId).utf_guid);
            var cancelEvents = _store.ReadJournal(runId).events.Where(e =>
                e != null && e.event_type == TestRunProtocol.EventType.CancelRequested).ToArray();
            Assert.AreEqual(1, cancelEvents.Length);
            StringAssert.Contains("utf_guid=utf-guid-1", cancelEvents[0].message);
            var summary = _store.Reconcile(runId);
            Assert.IsFalse(summary.is_terminal);
            Assert.IsEmpty(summary.outcome,
                "A provisional failure must not write a terminal outcome early.");
            Assert.IsTrue(summary.issues.Any(issue =>
                issue.code == "INFRASTRUCTURE_ERROR" &&
                issue.message.Contains("scene baseline is not empty")));
        }

        [TestCase("request-intent-persisted", 1, true)]
        [TestCase("run-record-persisted", 1, true)]
        [TestCase("prepared-pointer-persisted", 1, true)]
        [TestCase("build-evidence-persisted", 1, true)]
        [TestCase("environment-prepared", 1, true)]
        [TestCase("dispatch-state-persisted", 0, false)]
        [TestCase("dispatch-pointer-persisted", 0, false)]
        [TestCase("utf-execute-returned", 1, false)]
        [TestCase("utf-guid-persisted", 1, true)]
        [TestCase("request-acknowledged", 1, true)]
        public void CrashAfterEveryBoundary_RetryUsesOneIntentAndNeverRedispatches(
            string boundary,
            int expectedExecuteCalls,
            bool expectedAck)
        {
            var crashing = CreateService(afterDurableBoundary: reached =>
            {
                if (reached == boundary)
                    throw new TestRunInjectedCrashException(reached);
            });

            Assert.Throws<TestRunInjectedCrashException>(() => crashing.Start(
                "request-crash", "PlayMode", "OriginalGroup", "OriginalA|OriginalB"));

            var request = _store.ReadRequest("request-crash");
            Assert.IsTrue(request.intent_complete);
            Assert.AreEqual("PlayMode", request.mode);
            Assert.AreEqual("OriginalGroup", request.group);
            Assert.AreEqual("OriginalA|OriginalB", request.filter);

            var retry = CreateService().Start(
                "request-crash", "PlayMode", "OriginalGroup", "OriginalA|OriginalB");

            Assert.AreEqual(expectedExecuteCalls, _framework.ExecuteCalls, boundary);
            Assert.AreEqual(1, _store.ListRunIds().Length, boundary);
            var run = _store.ReadRun(request.run_id);
            Assert.AreEqual("PlayMode", run.mode);
            Assert.AreEqual("OriginalGroup", run.group);
            Assert.AreEqual("OriginalA|OriginalB", run.filter);
            if (expectedAck)
                StringAssert.StartsWith(
                    "tests-started|request_id=request-crash|run_id=" + run.run_id, retry);
            else
            {
                StringAssert.StartsWith(
                    "test-request|request_id=request-crash|run_id=" + run.run_id, retry);
                Assert.AreNotEqual(TestRunProtocol.Lifecycle.Prepared, run.lifecycle,
                    "Crossing the dispatch boundary is absorbing even when the " +
                    "run is reconciled as incomplete.");
            }
            if (expectedExecuteCalls == 1)
            {
                Assert.AreEqual(TestMode.PlayMode,
                    _framework.LastSettings.filters.Single().testMode);
                CollectionAssert.AreEqual(new[] { "OriginalA", "OriginalB" },
                    _framework.LastSettings.filters.Single().groupNames);
            }
        }

        [Test]
        public void Resolve_RequestOnlyCrashIsReadOnlyAndReportsRecoverablePreparedIntent()
        {
            var crashing = CreateService(afterDurableBoundary: boundary =>
            {
                if (boundary == TestRunDurableBoundary.RequestIntentPersisted)
                    throw new TestRunInjectedCrashException(boundary);
            });
            Assert.Throws<TestRunInjectedCrashException>(() =>
                crashing.Start("request-only", "EditMode", null, "ExactFilter"));

            var service = CreateService();
            var resolved = service.Resolve("request-only");

            StringAssert.Contains("|state=prepared|outcome=", resolved);
            Assert.IsEmpty(_store.ListRunIds(), "Resolve must not synthesize or dispatch a run.");
            var ack = service.Start("request-only", "EditMode", null, "ExactFilter");
            StringAssert.StartsWith("tests-started|request_id=request-only|", ack);
            Assert.AreEqual("EditMode",
                _store.ReadRun(_store.ReadRequest("request-only").run_id).mode);
            Assert.AreEqual(1, _framework.ExecuteCalls);
        }

        [Test]
        public void CorruptActivePointer_IsQuarantinedByteForByteBeforeSafeDispatch()
        {
            const string corrupt = "{ definitely-not-json\n";
            Directory.CreateDirectory(_store.RootPath);
            File.WriteAllText(_store.ActivePath, corrupt);

            var ack = CreateService().Start(
                "request-quarantine", "EditMode", null, null);

            StringAssert.StartsWith("tests-started|request_id=request-quarantine|", ack);
            Assert.AreEqual(1, _framework.ExecuteCalls);
            var quarantined = Directory.GetFiles(
                _store.QuarantinePath, "active.*.json", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, quarantined.Length);
            Assert.AreEqual(corrupt, File.ReadAllText(quarantined[0]));
            Assert.AreEqual(_store.ReadRequest("request-quarantine").run_id,
                _store.ReadActive().run_id);
        }

        [Test]
        public void ProductionFingerprint_RecordsEditorProjectUtfAndSourceCoherence()
        {
            var fingerprint = TestRunBuildFingerprintProbe.Capture();

            Assert.IsNotEmpty(fingerprint.ProjectIdentity);
            StringAssert.Contains(":", fingerprint.EditorProcessIdentity);
#if UNITY_6000_5_OR_NEWER
            Assert.AreEqual("1.7.0", fingerprint.UtfVersion);
#else
            Assert.AreEqual("1.6.0", fingerprint.UtfVersion);
#endif
            Assert.IsTrue(File.Exists(fingerprint.AssemblyPath), fingerprint.Error);
            Assert.IsTrue(File.Exists(fingerprint.SourcePath), fingerprint.Error);
            if (Application.isBatchMode && !fingerprint.IsCoherent)
                Assert.Ignore("Assembly timestamp coherence unreliable in CI batchmode (git checkout sets fresh timestamps)");
            Assert.IsTrue(fingerprint.IsCoherent, fingerprint.Error);
            StringAssert.Contains("sha256=", fingerprint.Fingerprint);
            StringAssert.Contains("assemblies=", fingerprint.Fingerprint);
        }

        [Test]
        public void ProductionFingerprint_OnDiskMainAssemblyMvidMatchesLoadedModule()
        {
            var assembly = typeof(TestRunner).Assembly;

            Assert.AreEqual(
                assembly.ManifestModule.ModuleVersionId,
                TestRunAssemblyFingerprint.ReadModuleVersionId(assembly.Location));
        }

        [Test]
        public void FingerprintInventory_UsesLoadedTestRootsAndDependencyClosure()
        {
            var dependencies = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["UnityMCP.Editor"] = Array.Empty<string>(),
                ["Consumer.Editor.Tests"] = new[]
                {
                    "Consumer.Runtime",
                    "TestHelpers"
                },
                ["Consumer.Runtime"] = Array.Empty<string>(),
                ["TestHelpers"] = Array.Empty<string>(),
                ["Dormant.Tests"] = Array.Empty<string>(),
                ["Unrelated.Editor"] = Array.Empty<string>()
            };

            var selected = TestRunAssemblyFingerprint.SelectInventoryNames(
                dependencies.Keys,
                new HashSet<string>(new[]
                {
                    "UnityMCP.Editor",
                    "Consumer.Editor.Tests",
                    "Consumer.Runtime",
                    "TestHelpers",
                    "Unrelated.Editor"
                }, StringComparer.Ordinal),
                new HashSet<string>(new[]
                {
                    "Consumer.Editor.Tests",
                    "Dormant.Tests"
                }, StringComparer.Ordinal),
                dependencies);

            CollectionAssert.AreEquivalent(new[]
            {
                "UnityMCP.Editor",
                "Consumer.Editor.Tests",
                "Consumer.Runtime",
                "TestHelpers"
            }, selected);
            CollectionAssert.DoesNotContain(selected, "Dormant.Tests");
            CollectionAssert.DoesNotContain(selected, "Unrelated.Editor");
        }

        [Test]
        public void FingerprintSourcePath_ResolvesProjectPackageAbsoluteAndGeneratedPaths()
        {
            var project = Path.Combine(_root, "Project");
            var package = Path.Combine(_root, "PackageCache", "com.example.tests@1.0.0");
            var absolute = Path.Combine(_root, "Generated", "Absolute.cs");
            string requestedPackage = null;

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(project, "Assets", "Tests", "A.cs")),
                TestRunAssemblyFingerprint.ResolveSourcePath(
                    project, "Assets/Tests/A.cs"));
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(package, "Editor", "B.cs")),
                TestRunAssemblyFingerprint.ResolveSourcePath(
                    project,
                    "Packages/com.example.tests/Editor/B.cs",
                    packageAssetPath =>
                    {
                        requestedPackage = packageAssetPath;
                        return package;
                    }));
            Assert.AreEqual("Packages/com.example.tests", requestedPackage);
            Assert.AreEqual(
                Path.GetFullPath(absolute),
                TestRunAssemblyFingerprint.ResolveSourcePath(project, absolute));
            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(project, "Library", "Generated", "C.cs")),
                TestRunAssemblyFingerprint.ResolveSourcePath(
                    project, "Library/Generated/C.cs"));
        }

        [Test]
        public void RetryWithSameRequestIdAndIntent_ReturnsByteEquivalentAckWithoutRedispatch()
        {
            var service = CreateService();

            var first = service.Start("stable-request", "EditMode", null, null);
            var retry = service.Start("stable-request", "EditMode", null, null);

            Assert.AreEqual(first, retry);
            Assert.AreEqual(1, _framework.ExecuteCalls);
            Assert.AreEqual(1, _store.ListRunIds().Length);
        }

        [TestCase("")]
        [TestCase("bad|request")]
        [TestCase("../request")]
        [TestCase("line\nbreak")]
        public void UnsafeRequestId_IsRejectedBeforeDurableIntentOrDispatch(
            string requestId)
        {
            var response = CreateService().Start(requestId, "EditMode", null, null);

            StringAssert.Contains("request_id must contain", response);
            Assert.AreEqual(0, _framework.ExecuteCalls);
            Assert.IsEmpty(_store.ListRunIds());
        }

        [Test]
        public void ResolveRequest_ReturnsExactAckOrNoneAndNeverDispatches()
        {
            var service = CreateService();
            var ack = service.Start("request-resolve", "EditMode", null, null);

            Assert.AreEqual(ack, service.Resolve("request-resolve"));
            Assert.AreEqual("none", service.Resolve("missing-request"));
            Assert.AreEqual(1, _framework.ExecuteCalls);
        }

        [Test]
        public void IncoherentBuild_IsDurableDispatchFailureAndDoesNotTouchScene()
        {
            var service = CreateService(build: new TestRunBuildFingerprint
            {
                UtfVersion = "1.6.0",
                IsCoherent = false,
                Error = "loaded assembly is stale",
                Fingerprint = "mvid=stale"
            });

            var response = service.Start("request-stale", "EditMode", null, null);

            StringAssert.StartsWith("test-request|request_id=request-stale|", response);
            Assert.AreEqual("loaded assembly is stale", DecodeReason(response));
            Assert.AreEqual(0, _framework.ExecuteCalls);
            Assert.AreEqual(0, _environment.PrepareCalls);
            var run = _store.ReadRun(_store.ReadRequest("request-stale").run_id);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal, run.lifecycle);
            Assert.AreEqual(TestRunProtocol.RunOutcome.DispatchFailed, run.outcome);
            Assert.IsFalse(run.build_coherent);
            Assert.AreEqual(TestRunProtocol.RunOutcome.DispatchFailed,
                _store.Reconcile(run.run_id).outcome);
            var resolved = service.Resolve("request-stale");
            StringAssert.StartsWith("test-request|request_id=request-stale|", resolved);
            StringAssert.Contains("|run_id=" + run.run_id + "|", resolved);
            StringAssert.Contains("|outcome=dispatch_failed", resolved);
        }

        [TestCase(UtfRunActivity.Active, "another UTF run is already active")]
        [TestCase(UtfRunActivity.Unknown, "could not be proven inactive")]
        public void GlobalUtfActivity_PreventsAmbiguousDispatchAndScenePreparation(
            UtfRunActivity activity, string expected)
        {
            _framework.AnyActivity = activity;

            var response = CreateService().Start(
                "request-global-utf", "EditMode", null, null);

            StringAssert.StartsWith("test-request|request_id=request-global-utf|", response);
            StringAssert.Contains(expected, DecodeReason(response));
            Assert.AreEqual(0, _environment.PrepareCalls);
            Assert.AreEqual(0, _framework.ExecuteCalls);
        }

        [Test]
        public void DirtyScenePreflightFailure_IsDurableAndNeverCallsUtf()
        {
            _environment.PrepareError = new InvalidOperationException(
                "scene has unsaved changes: Assets/User.unity");
            var service = CreateService();

            var response = service.Start("request-dirty", "EditMode", null, null);

            StringAssert.StartsWith("test-request|request_id=request-dirty|", response);
            StringAssert.Contains("scene has unsaved changes", DecodeReason(response));
            Assert.AreEqual(0, _framework.ExecuteCalls);
            var runId = _store.ReadRequest("request-dirty").run_id;
            Assert.AreEqual(TestRunProtocol.RunOutcome.DispatchFailed,
                _store.Reconcile(runId).outcome);
        }

        [Test]
        public void ExistingActiveRun_BlocksDispatchWithoutReplacingActivePointer()
        {
            _store.WriteRun(new TestRunRecord
            {
                run_id = "run-active",
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = Utc
            });
            _store.WriteActive(new TestRunPointer
            {
                run_id = "run-active",
                updated_utc = Utc
            });
            var service = CreateService();

            var response = service.Start("request-blocked", "EditMode", null, null);

            StringAssert.StartsWith("test-request|request_id=request-blocked|", response);
            StringAssert.Contains("test run already active: run-active", DecodeReason(response));
            Assert.AreEqual("run-active", _store.ReadActive().run_id);
            Assert.AreEqual(0, _framework.ExecuteCalls);
            var blocked = _store.ReadRun(_store.ReadRequest("request-blocked").run_id);
            Assert.AreEqual(TestRunProtocol.RunOutcome.DispatchFailed, blocked.outcome);
        }

        [Test]
        public void FilterAndMode_ArePassedToUtfWithoutWaitingForCallbacks()
        {
            var service = CreateService();

            service.Start("request-filter", "PlayMode", null, "OneTests|TwoTests");

            Assert.AreEqual(TestMode.PlayMode,
                _framework.LastSettings.filters.Single().testMode);
            CollectionAssert.AreEqual(new[] { "OneTests", "TwoTests" },
                _framework.LastSettings.filters.Single().groupNames);
        }

        [Test]
        public void GetRunJson_ContainsCorrelatedLifecycleAndReconcilerFields()
        {
            var service = CreateService();
            service.Start("request-json", "EditMode", null, null);
            var runId = _store.ReadRequest("request-json").run_id;

            var json = service.GetRunJson(runId);

            StringAssert.Contains("\"run_id\":\"" + runId + "\"", json);
            StringAssert.Contains("\"request_id\":\"request-json\"", json);
            StringAssert.Contains("\"lifecycle\":\"dispatched\"", json);
            StringAssert.Contains("\"state\":\"dispatched\"", json);
            StringAssert.Contains("\"expected_count\":0", json);
            StringAssert.Contains("\"build_coherent\":true", json);
        }

        [Test]
        public void Cancel_UsesUtfGuidAndRequestEventIsNotTerminalEvidence()
        {
            var service = CreateService();
            service.Start("request-cancel", "EditMode", null, null);
            var runId = _store.ReadRequest("request-cancel").run_id;

            var response = service.Cancel(runId);

            Assert.AreEqual("cancel-requested|run_id=" + runId + "|utf_guid=utf-guid-1",
                response);
            Assert.AreEqual("utf-guid-1", _framework.LastCancelledGuid);
            Assert.IsFalse(_store.Reconcile(runId).is_terminal,
                "Cancel request is asynchronous; RunFinished supplies terminal truth.");
            Assert.AreEqual(response, service.Cancel(runId));
            Assert.AreEqual(1, _framework.CancelCalls);
        }

        [Test]
        public void Cancel_WhenUtfNoLongerOwnsGuid_TerminalizesAsIncomplete()
        {
            var service = CreateService();
            service.Start("request-abandoned", "EditMode", null, null);
            var runId = _store.ReadRequest("request-abandoned").run_id;
            _framework.CancelResult = false;
            _framework.Activity = UtfRunActivity.Inactive;

            var response = service.Cancel(runId);

            Assert.AreEqual("cancel-not-active|run_id=" + runId +
                "|state=terminal|outcome=incomplete", response);
            var summary = _store.Reconcile(runId);
            Assert.IsTrue(summary.is_terminal);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Incomplete, summary.outcome);
        }

        [Test]
        public void CancelFalseWhileGuidIsActive_RemainsRetryableUntilUtfAccepts()
        {
            var service = CreateService();
            service.Start("request-already-cancelling", "EditMode", null, null);
            var runId = _store.ReadRequest("request-already-cancelling").run_id;
            _framework.CancelResult = false;

            var rejected = service.Cancel(runId);

            StringAssert.StartsWith("cancel-retryable|run_id=" + runId +
                "|utf_guid=utf-guid-1|reason=utf-cancel-not-accepted", rejected);
            Assert.IsFalse(_store.Reconcile(runId).is_terminal);
            Assert.AreEqual(0, _environment.RestoreCalls);
            Assert.AreEqual(1, _framework.CancelCalls);
            Assert.IsFalse(_store.ReadJournal(runId).events.Any(e =>
                e != null &&
                e.event_type == TestRunProtocol.EventType.CancelRequested));

            _framework.CancelResult = true;
            var accepted = service.Cancel(runId);

            Assert.AreEqual("cancel-requested|run_id=" + runId +
                "|utf_guid=utf-guid-1", accepted);
            Assert.AreEqual(2, _framework.CancelCalls);
            Assert.AreEqual(1, _store.ReadJournal(runId).events.Count(e =>
                e != null &&
                e.event_type == TestRunProtocol.EventType.CancelRequested));
            Assert.AreEqual(accepted, service.Cancel(runId));
            Assert.AreEqual(2, _framework.CancelCalls);
        }

        [Test]
        public void CancelWhenUtfThrows_RemainsRetryableWithoutDurableRequest()
        {
            var service = CreateService();
            service.Start("request-cancel-throws", "EditMode", null, null);
            var runId = _store.ReadRequest("request-cancel-throws").run_id;
            _framework.CancelError = new InvalidOperationException("UTF unavailable");

            var response = service.Cancel(runId);

            StringAssert.StartsWith("cancel-retryable|run_id=" + runId +
                "|utf_guid=utf-guid-1|reason=utf-cancel-threw", response);
            Assert.AreEqual(1, _framework.CancelCalls);
            Assert.IsFalse(_store.ReadJournal(runId).events.Any(e =>
                e != null &&
                e.event_type == TestRunProtocol.EventType.CancelRequested));
            Assert.IsFalse(_store.Reconcile(runId).is_terminal);
        }

        [Test]
        public void Finalizer_UnmanagedRunWithoutOwnedEnvironment_WaitsForUtfInCurrentSession()
        {
            const string runId = "run-unmanaged";
            WriteFinalizingRun(runId, "unity-ui");
            _framework.AnyActivity = UtfRunActivity.Active;
            var finalizer = CreateFinalizer();

            var completedWhileActive = finalizer.TryFinalize(runId);

            Assert.IsFalse(completedWhileActive);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Finalizing,
                _store.ReadRun(runId).lifecycle);
            Assert.AreEqual(0, _environment.RestoreCalls);

            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            Assert.IsTrue(finalizer.TryFinalize(runId));
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);
            Assert.IsTrue(_store.ReadJournal(runId).events.Any(e =>
                e.event_type == TestRunProtocol.EventType.RunFinalized));
            Assert.AreEqual(0, _environment.RestoreCalls,
                "Unmanaged runs own no scene transaction to restore.");
        }

        [Test]
        public void Finalizer_ManagedRunNeverBypassesGlobalUtfActivity()
        {
            const string runId = "run-managed";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Active;
            var finalizer = CreateFinalizer();

            var completed = finalizer.TryFinalize(runId);

            Assert.IsFalse(completed);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Finalizing,
                _store.ReadRun(runId).lifecycle);
            Assert.IsFalse(_store.ReadJournal(runId).events.Any(e =>
                e.event_type == TestRunProtocol.EventType.RunFinalized));
            Assert.AreEqual(0, _environment.RestoreCalls);
        }

        [Test]
        public void Finalizer_ProbeAnyUnknown_OwnInactive_Finalizes()
        {
            const string runId = "run-probe-any-unknown";
            WriteFinalizingRun(runId, "mcp");
            var run = _store.ReadRun(runId);
            run.utf_guid = "utf-known-guid";
            _store.WriteRun(run);
            _framework.AnyActivity = UtfRunActivity.Unknown;  // reflection miss
            _framework.Activity = UtfRunActivity.Inactive;    // our run is done

            var result = CreateFinalizer().TryFinalize(runId);

            Assert.IsTrue(result, "Should finalize: own run Inactive overrides Unknown global");
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal, _store.ReadRun(runId).lifecycle);
            Assert.IsTrue(_store.ReadJournal(runId).events.Any(e =>
                e.event_type == TestRunProtocol.EventType.RunFinalized));
        }

        [Test]
        public void Finalizer_ProbeAnyUnknown_OwnActive_Blocks()
        {
            const string runId = "run-own-active-any-unknown";
            WriteFinalizingRun(runId, "mcp");
            var run = _store.ReadRun(runId);
            run.utf_guid = "utf-known-guid";
            _store.WriteRun(run);
            _framework.AnyActivity = UtfRunActivity.Unknown;
            _framework.Activity = UtfRunActivity.Active;  // our run still running

            var result = CreateFinalizer().TryFinalize(runId);

            Assert.IsFalse(result, "Should not finalize: own run is Active");
            Assert.AreEqual(TestRunProtocol.Lifecycle.Finalizing,
                _store.ReadRun(runId).lifecycle);
            Assert.AreEqual(0, _environment.RestoreCalls);
        }

        [Test]
        public void Finalizer_BothActive_Blocks()
        {
            const string runId = "run-both-active";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Active;
            // Activity defaults to Active in FakeFrameworkDriver; no utf_guid → ownActivity=Unknown

            var result = CreateFinalizer().TryFinalize(runId);

            Assert.IsFalse(result, "Active global probe must block finalization");
            Assert.AreEqual(TestRunProtocol.Lifecycle.Finalizing,
                _store.ReadRun(runId).lifecycle);
            Assert.AreEqual(0, _environment.RestoreCalls);
        }

        [Test]
        public void Finalizer_PreviousEditorSessionCannotPoisonCurrentUtfRun()
        {
            const string runId = "run-previous-editor";
            WriteFinalizingRun(runId, "mcp");
            var run = _store.ReadRun(runId);
            run.editor_session_id = "previous-editor-session";
            _store.WriteRun(run);
            _framework.AnyActivity = UtfRunActivity.Active;
            _framework.Activity = UtfRunActivity.Active;

            var completed = CreateFinalizer().TryFinalize(runId);

            Assert.IsTrue(completed);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);
            Assert.AreEqual(1, _environment.RestoreCalls);
        }

        [Test]
        public void Finalizer_RestoreFailureTerminatesInvalidWithoutClaimingCleanup()
        {
            const string runId = "run-restore-failed";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            _environment.RestoreError = new InvalidOperationException("owned scene is missing");

            var completed = CreateFinalizer().TryFinalize(runId);
            var summary = _store.Reconcile(runId);

            Assert.IsTrue(completed);
            Assert.IsTrue(summary.is_terminal);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsFalse(summary.cleanup_complete);
            Assert.AreEqual(TestRunProtocol.Health.SuspectedStall, summary.health);
            Assert.IsTrue(summary.issues.Any(issue =>
                issue.code == "INFRASTRUCTURE_ERROR" &&
                issue.message.Contains("owned scene is missing")));
        }

        [Test]
        public void Finalizer_MvidChangeBeforeCommitMakesPassingRunInvalid()
        {
            const string runId = "run-build-changed";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            ApplyStartBuild(runId);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            var completion = CoherentBuild();
            completion.Fingerprint = "mvid=changed;utf=1.6.0";

            Assert.IsTrue(CreateFinalizer(completion).TryFinalize(runId));

            var summary = _store.Reconcile(runId);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsTrue(summary.issues.Any(issue =>
                issue.code == "INFRASTRUCTURE_ERROR" &&
                issue.message.Contains("build fingerprint changed")));
        }

        [Test]
        public void Finalizer_SourceChangeBeforeCommitMakesPassingRunInvalid()
        {
            const string runId = "run-source-changed";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            ApplyStartBuild(runId);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            var completion = CoherentBuild();
            completion.IsCoherent = false;
            completion.Error = "loaded assembly is older than source";

            Assert.IsTrue(CreateFinalizer(completion).TryFinalize(runId));

            var summary = _store.Reconcile(runId);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsTrue(summary.issues.Any(issue =>
                issue.code == "INFRASTRUCTURE_ERROR" &&
                issue.message.Contains("older than source")));
        }

        [TestCase("root-1", "Suite", "EditMode", true)]
        [TestCase("root-other", "Suite", "EditMode", false)]
        [TestCase("root-1", "OtherSuite", "EditMode", false)]
        [TestCase("root-1", "Suite", "PlayMode", false)]
        [TestCase("", "Suite", "EditMode", false)]
        public void RunFinishedRootIdentity_MustMatchDurableRunStarted(
            string completedUniqueName,
            string completedFullName,
            string completedMode,
            bool expected)
        {
            var matches = TestRunObserver.RootIdentityMatches(
                new TestRunRecord { mode = "EditMode" },
                new TestRunEvent { unique_name = "root-1", full_name = "Suite" },
                completedUniqueName,
                completedFullName,
                completedMode);

            Assert.AreEqual(expected, matches);
        }

        [TestCase("mcp", "EditMode", true)]
        [TestCase("mcp", "PlayMode", true)]
        [TestCase("unity-ui", "EditMode", true)]
        [TestCase("unity-ui", "PlayMode", false)]
        [TestCase("unity-ui-orphan", "EditMode", false)]
        [TestCase("unity-ui-orphan", "PlayMode", false)]
        public void ObserverEnvironmentPolicy_LeavesDirectPlayModeSceneLifecycleToUtf(
            string source,
            string mode,
            bool expected)
        {
            var run = new TestRunRecord { source = source, mode = mode };

            Assert.AreEqual(expected,
                TestRunObserver.ShouldPrepareManagedEnvironment(run));
            Assert.AreEqual(source == "unity-ui" && mode == "EditMode",
                UnityTestRunEnvironmentController.IsUtfManagedEditModeRun(run));
        }

        [Test]
        public void Finalizer_EditorShutdownCommitsCompletedCommandLineRunSynchronously()
        {
            const string runId = "run-command-line";
            WriteFinalizingRun(runId, "unity-ui",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _store.WriteEnvironment(new TestRunEnvironmentRecord
            {
                run_id = runId,
                prepared_utc = Utc
            });
            _framework.AnyActivity = UtfRunActivity.Active;
            _framework.Activity = UtfRunActivity.Active;
            var finalizer = CreateFinalizer();

            Assert.IsFalse(finalizer.TryFinalize(runId));
            Assert.IsTrue(finalizer.TryFinalizeForEditorShutdown(runId));

            var summary = _store.Reconcile(runId);
            Assert.IsTrue(summary.is_terminal);
            Assert.IsTrue(summary.cleanup_complete);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Passed, summary.outcome);
            Assert.AreEqual(1, _environment.RestoreCalls);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);
        }

        [Test]
        public void Finalizer_LegacyProvisionalOutcomeConflictTerminatesInvalidIdempotently()
        {
            const string runId = "run-legacy-provisional";
            WriteFinalizingRun(runId, "unity-ui",
                TestRunProtocol.RunOutcome.Incomplete,
                TestRunProtocol.RunOutcome.Passed);
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "old-generation",
                outcome = TestRunProtocol.RunOutcome.Passed
            });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            var finalizer = CreateFinalizer();

            Assert.DoesNotThrow(() => finalizer.TryFinalize(runId));
            Assert.DoesNotThrow(() => finalizer.TryFinalize(runId));

            var run = _store.ReadRun(runId);
            var summary = _store.Reconcile(runId);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal, run.lifecycle);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, run.outcome);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, summary.outcome);
            Assert.IsTrue(summary.issues.Any(issue =>
                issue.code == "TERMINAL_OUTCOME_CONTRADICTION"));
        }

        [TestCase("Passed", TestStatus.Passed, TestRunProtocol.LeafOutcome.Passed)]
        [TestCase("Failed", TestStatus.Failed, TestRunProtocol.LeafOutcome.Failed)]
        [TestCase("Skipped:Ignored", TestStatus.Skipped, TestRunProtocol.LeafOutcome.Skipped)]
        [TestCase("Inconclusive", TestStatus.Inconclusive, TestRunProtocol.LeafOutcome.Inconclusive)]
        [TestCase("Failed:Cancelled", TestStatus.Failed, TestRunProtocol.LeafOutcome.Cancelled)]
        [TestCase("Skipped:NotRun", TestStatus.Skipped, TestRunProtocol.LeafOutcome.Cancelled)]
        [TestCase("Failed:Invalid", TestStatus.Failed, TestRunProtocol.LeafOutcome.Invalid)]
        [TestCase("Skipped:NotRunnable", TestStatus.Skipped, TestRunProtocol.LeafOutcome.Invalid)]
        public void UtfResultState_MapsToExactLeafOutcome(
            string resultState, TestStatus status, string expected)
        {
            Assert.AreEqual(expected,
                TestRunObserver.MapLeafOutcome(resultState, status));
        }

        [Test]
        public void ListRuns_IsNewestFirstAndBounded()
        {
            WriteBareRun("run-old", "2026-08-02T10:00:00.0000000Z");
            WriteBareRun("run-new", "2026-08-02T11:00:00.0000000Z");
            var json = CreateService().ListRunsJson(1);

            StringAssert.Contains("run-new", json);
            StringAssert.DoesNotContain("run-old", json);
        }

        [Test]
        public void FinishRun_OnlyClassifiesImmediateDispatchErrors()
        {
            var failed = TestRunner.FinishRun("Error: dispatch rejected");
            var started = TestRunner.FinishRun(
                "tests-started|request_id=r|run_id=x|utf_guid=g|state=dispatched");

            Assert.IsFalse(failed.ok);
            Assert.AreEqual("dispatch rejected", failed.text);
            Assert.IsTrue(started.ok);
            StringAssert.StartsWith("tests-started|", started.text);
        }

        // ── Bug fix: false positive fingerprint when Bee cache-hit causes mtime discrepancy ──

        [Test]
        public void MtimeCoherence_IdleStatusAfterCacheHit_DoesNotThrow()
        {
            var original = TestRunAssemblyFingerprint.CompileStatusGetter;
            try
            {
                TestRunAssemblyFingerprint.CompileStatusGetter = () => "idle|3.2";
                var older = DateTime.UtcNow.AddSeconds(-10);
                var newer = DateTime.UtcNow;
                Assert.DoesNotThrow(() =>
                    TestRunAssemblyFingerprint.ValidateMtimeCoherence(
                        "TestAsm", older, newer, "Source.cs"),
                    "idle|... means Bee cache-hit: mtime discrepancy is expected, must not throw");
            }
            finally { TestRunAssemblyFingerprint.CompileStatusGetter = original; }
        }

        [Test]
        public void MtimeCoherence_IdleNeverStatus_Throws()
        {
            var original = TestRunAssemblyFingerprint.CompileStatusGetter;
            try
            {
                TestRunAssemblyFingerprint.CompileStatusGetter = () => "idle-never|0";
                var older = DateTime.UtcNow.AddSeconds(-10);
                var newer = DateTime.UtcNow;
                Assert.Throws<InvalidDataException>(() =>
                    TestRunAssemblyFingerprint.ValidateMtimeCoherence(
                        "TestAsm", older, newer, "Source.cs"),
                    "idle-never means no compile ran: stale DLL must throw");
            }
            finally { TestRunAssemblyFingerprint.CompileStatusGetter = original; }
        }

        [Test]
        public void MtimeCoherence_CompilingStatus_Throws()
        {
            var original = TestRunAssemblyFingerprint.CompileStatusGetter;
            try
            {
                TestRunAssemblyFingerprint.CompileStatusGetter = () => "compiling|1.5";
                var older = DateTime.UtcNow.AddSeconds(-10);
                var newer = DateTime.UtcNow;
                Assert.Throws<InvalidDataException>(() =>
                    TestRunAssemblyFingerprint.ValidateMtimeCoherence(
                        "TestAsm", older, newer, "Source.cs"),
                    "compiling status means DLL is not yet fresh: must throw");
            }
            finally { TestRunAssemblyFingerprint.CompileStatusGetter = original; }
        }

        // ── GetLegacyResults ──

        [Test]
        public void GetLegacyResults_UnknownRunId_ReturnsNone()
        {
            var result = CreateService().GetLegacyResults("run-does-not-exist");

            Assert.AreEqual("none", result);
        }

        [Test]
        public void GetLegacyResults_NonTerminalRun_ReturnsPending()
        {
            var service = CreateService();
            service.Start("request-legacy-pending", "EditMode", null, null);
            var runId = _store.ReadRequest("request-legacy-pending").run_id;

            var result = service.GetLegacyResults(runId);

            Assert.AreEqual("pending", result);
        }

        [Test]
        public void GetLegacyResults_TerminalRun_ReturnsFormattedCounters()
        {
            const string runId = "run-legacy-terminal";
            WriteFinalizingRun(runId, "unity-ui",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            CreateFinalizer().TryFinalize(runId);

            var result = CreateService().GetLegacyResults(runId);

            StringAssert.StartsWith("0 tests:", result);
            StringAssert.Contains(" passed,", result);
            StringAssert.Contains(" failed,", result);
            StringAssert.Contains("outcome=passed", result);
        }

        // ── GetLegacyProgress ──

        [Test]
        public void GetLegacyProgress_UnknownRunId_ReturnsIdleBare()
        {
            var result = CreateService().GetLegacyProgress("run-does-not-exist");

            Assert.AreEqual("idle", result);
        }

        [Test]
        public void GetLegacyProgress_TerminalRun_ReturnsIdleWithOutcome()
        {
            const string runId = "run-progress-terminal";
            WriteFinalizingRun(runId, "unity-ui",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            CreateFinalizer().TryFinalize(runId);

            var result = CreateService().GetLegacyProgress(runId);

            Assert.AreEqual("idle|run_id=" + runId + "|outcome=passed", result);
        }

        [Test]
        public void GetLegacyProgress_ManifestIncomplete_ReturnsPendingWithRunId()
        {
            var service = CreateService();
            service.Start("request-progress-pending", "EditMode", null, null);
            var runId = _store.ReadRequest("request-progress-pending").run_id;

            var result = service.GetLegacyProgress(runId);

            Assert.AreEqual("pending|run_id=" + runId + "|no-progress-yet", result);
        }

        [Test]
        public void GetLegacyProgress_InFlightWithManifestSealed_ReturnsRunningFormat()
        {
            const string runId = "run-progress-inflight";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = Utc,
                utf_guid = "utf-guid-1",
                build_coherent = true
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunStarted,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                expected_count = 0
            });
            _store.SealManifest(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.ManifestSealed,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                expected_count = 0
            });

            var result = CreateService().GetLegacyProgress(runId);

            Assert.AreEqual(
                "running|0|0|0|0|0|0.0|eta=0s|run_id=" + runId, result);
        }

        // ── Cancel edge cases ──

        [Test]
        public void Cancel_UnknownRunId_ReturnsNone()
        {
            var result = CreateService().Cancel("run-not-in-store");

            Assert.AreEqual("none", result);
        }

        [Test]
        public void Cancel_AlreadyTerminalRun_ReturnsAlreadyTerminal()
        {
            const string runId = "run-cancel-already-terminal";
            WriteFinalizingRun(runId, "unity-ui",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            CreateFinalizer().TryFinalize(runId);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);

            var result = CreateService().Cancel(runId);

            Assert.AreEqual(
                "already-terminal|run_id=" + runId + "|outcome=passed", result);
        }

        [Test]
        public void Cancel_NoUtfGuid_ReturnsCancelRejected()
        {
            const string runId = "run-cancel-no-guid";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Dispatched,
                created_utc = Utc
            });

            var result = CreateService().Cancel(runId);

            Assert.AreEqual(
                "cancel-rejected|run_id=" + runId + "|reason=no-utf-guid", result);
        }

        [Test]
        public void Cancel_ProbeUnknown_FirstCancel_ReturnsAckWithActivityUnknownSuffix()
        {
            var service = CreateService();
            service.Start("request-probe-unknown", "EditMode", null, null);
            var runId = _store.ReadRequest("request-probe-unknown").run_id;
            _framework.Activity = UtfRunActivity.Unknown;

            var response = service.Cancel(runId);

            // Probe=Unknown: not Inactive (no early exit), Cancel proceeds and activityAfter=Unknown
            StringAssert.StartsWith("cancel-requested|run_id=" + runId + "|utf_guid=utf-guid-1",
                response);
            StringAssert.Contains("|activity=unknown", response);
            Assert.AreEqual(1, _framework.CancelCalls);
        }

        [Test]
        public void Cancel_AlreadyRequested_ProbeUnknown_ReturnsAckWithActivityUnknownSuffix()
        {
            var service = CreateService();
            service.Start("request-already-unknown", "EditMode", null, null);
            var runId = _store.ReadRequest("request-already-unknown").run_id;
            // First cancel: Activity=Active, appends CancelRequested
            service.Cancel(runId);
            Assert.AreEqual(1, _framework.CancelCalls);

            // Second cancel: Activity=Unknown + alreadyRequested=true
            _framework.Activity = UtfRunActivity.Unknown;
            var response = service.Cancel(runId);

            StringAssert.StartsWith("cancel-requested|run_id=" + runId + "|utf_guid=utf-guid-1",
                response);
            StringAssert.Contains("|activity=unknown", response);
            Assert.AreEqual(1, _framework.CancelCalls, "Framework.Cancel must not be called when already requested");
        }

        [Test]
        public void Cancel_ProbeInactive_NeverCallsFrameworkCancel()
        {
            var service = CreateService();
            service.Start("request-probe-inactive-nocall", "EditMode", null, null);
            var runId = _store.ReadRequest("request-probe-inactive-nocall").run_id;
            _framework.Activity = UtfRunActivity.Inactive;

            service.Cancel(runId);

            Assert.AreEqual(0, _framework.CancelCalls,
                "Framework.Cancel must not be called when Probe returns Inactive");
        }

        // ── GetRunJson ──

        [Test]
        public void GetRunJson_EmptyRunId_ReturnsNone()
        {
            Assert.AreEqual("none", CreateService().GetRunJson(""));
        }

        [Test]
        public void GetRunJson_WhitespaceRunId_ReturnsNone()
        {
            Assert.AreEqual("none", CreateService().GetRunJson("   "));
        }

        [Test]
        public void GetRunJson_RunDirectoryMissing_ReturnsNone()
        {
            var result = CreateService().GetRunJson("run-no-directory");

            Assert.AreEqual("none", result);
        }

        // ── ListRunsJson ──

        [Test]
        public void ListRunsJson_LimitZero_ClampedToOne()
        {
            WriteBareRun("run-lz-old", "2026-08-02T10:00:00.0000000Z");
            WriteBareRun("run-lz-new", "2026-08-02T11:00:00.0000000Z");

            var json = CreateService().ListRunsJson(0);

            StringAssert.Contains("run-lz-new", json);
            StringAssert.DoesNotContain("run-lz-old", json);
        }

        [Test]
        public void ListRunsJson_CompactSnapshot_StripsIssuesFromInvalidRun()
        {
            const string runId = "run-compact-issues";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            _environment.RestoreError =
                new InvalidOperationException("compact-strip-marker");
            CreateFinalizer().TryFinalize(runId);

            var json = CreateService().ListRunsJson(10);

            StringAssert.Contains(runId, json);
            StringAssert.DoesNotContain("compact-strip-marker", json);
            StringAssert.DoesNotContain("INFRASTRUCTURE_ERROR", json);
        }

        // ── TryFinalizeCore_RestoreFails ──

        [Test]
        public void TryFinalizeCore_RestoreFails_WritesActivePointerAfterTermination()
        {
            const string runId = "run-restore-fail-pointer";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            _environment.RestoreError = new InvalidOperationException("disk full");

            CreateFinalizer().TryFinalize(runId);

            var run = _store.ReadRun(runId);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal, run.lifecycle);
            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, run.outcome);
            Assert.AreEqual(runId, _store.ReadActive().run_id,
                "Active pointer must reference the failed run after restore failure.");
        }

        [Test]
        public void TryFinalizeCore_RestoreFails_AppendInfrastructureOnceDeduplicatesExistingMessage()
        {
            const string runId = "run-restore-dedup";
            WriteFinalizingRun(runId, "mcp");
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;
            _environment.RestoreError = new InvalidOperationException("injected error");
            const string expectedMsg = "scene restoration failed: injected error";
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.InfrastructureError,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                message = expectedMsg
            });

            CreateFinalizer().TryFinalize(runId);

            var count = _store.ReadJournal(runId).events.Count(e =>
                e != null &&
                e.event_type == TestRunProtocol.EventType.InfrastructureError &&
                e.message == expectedMsg);
            Assert.AreEqual(1, count,
                "AppendInfrastructureOnce must not append a duplicate of an existing message.");
        }

        // ── TryFinalizeCore_AlreadyTerminal ──

        [Test]
        public void TryFinalizeCore_AlreadyTerminalRun_ReturnsTrueWithoutCallingRestore()
        {
            const string runId = "run-already-terminal-norestore";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Terminal,
                outcome = TestRunProtocol.RunOutcome.Passed,
                finished_utc = Utc,
                created_utc = Utc
            });

            var result = CreateFinalizer().TryFinalize(runId);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _environment.RestoreCalls);
        }

        [Test]
        public void TryFinalizeCore_AlreadyTerminalRun_RepairsActivePointerWhenRunIdMatchesActive()
        {
            const string runId = "run-terminal-pointer-repair";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Terminal,
                outcome = TestRunProtocol.RunOutcome.Passed,
                finished_utc = Utc,
                created_utc = Utc
            });
            _store.WriteActive(new TestRunPointer
            {
                run_id = runId,
                updated_utc = "2020-01-01T00:00:00Z"
            });

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(Utc, _store.ReadActive().updated_utc,
                "Active pointer updated_utc must be repaired from run.finished_utc.");
        }

        [Test]
        public void TryFinalizeCore_TerminalizeRequest_WritesTerminalStateToLinkedRequest()
        {
            var service = CreateService();
            service.Start("request-terminalize-test", "EditMode", null, null);
            var runId = _store.ReadRequest("request-terminalize-test").run_id;
            var run = _store.ReadRun(runId);
            run.lifecycle = TestRunProtocol.Lifecycle.Terminal;
            run.outcome = TestRunProtocol.RunOutcome.Passed;
            run.finished_utc = Utc;
            _store.WriteRun(run);

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRequest("request-terminalize-test").state,
                "TerminalizeRequest must set the linked request state to terminal.");
        }

        // ── TryFinalizeCore_SuccessfulFinalization ──

        [Test]
        public void TryFinalizeCore_SuccessfulFinalization_AppendsRunFinalizedAndTerminates()
        {
            const string runId = "run-success-final";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            var completed = CreateFinalizer().TryFinalize(runId);

            Assert.IsTrue(completed);
            var run = _store.ReadRun(runId);
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal, run.lifecycle);
            Assert.AreEqual(TestRunProtocol.Health.Healthy, run.health);
            Assert.IsTrue(_store.ReadJournal(runId).events.Any(e =>
                e != null && e.event_type == TestRunProtocol.EventType.RunFinalized));
            Assert.IsTrue(_store.Reconcile(runId).is_terminal);
            Assert.AreEqual(1, _environment.RestoreCalls);
        }

        [Test]
        public void TryFinalizeCore_NoExecutionBoundary_AppendsAbandonedEvent()
        {
            const string runId = "run-no-boundary";
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                source = "mcp",
                lifecycle = TestRunProtocol.Lifecycle.Finalizing,
                created_utc = Utc,
                build_coherent = true
            });
            _store.WriteActive(new TestRunPointer { run_id = runId, updated_utc = Utc });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.IsTrue(_store.ReadJournal(runId).events.Any(e =>
                e != null && e.event_type == TestRunProtocol.EventType.Abandoned),
                "Abandoned event must be appended when no execution boundary exists.");
            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);
        }

        [Test]
        public void TryFinalizeCore_EditorQuitting_WithExecutionBoundary_SkipsActivityProbe()
        {
            const string runId = "run-quitting-boundary";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Active;
            _framework.Activity = UtfRunActivity.Active;

            Assert.IsFalse(CreateFinalizer().TryFinalize(runId),
                "Active framework must block normal finalization.");
            Assert.IsTrue(CreateFinalizer().TryFinalizeForEditorShutdown(runId),
                "EditorShutdown must bypass activity probe when execution boundary exists.");

            Assert.AreEqual(TestRunProtocol.Lifecycle.Terminal,
                _store.ReadRun(runId).lifecycle);
        }

        // ── SelectTerminalOutcome via TryFinalize observable outcomes ──

        [Test]
        public void SelectTerminalOutcome_NoFinalizedEventInJournal_UsesDerivedOutcomeFromSummary()
        {
            const string runId = "run-select-derived";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Failed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Failed,
                _store.ReadRun(runId).outcome,
                "With no RunFinalized event, outcome must be derived from the reconciled summary.");
        }

        [Test]
        public void SelectTerminalOutcome_SingleFinalizedEventAgreeingWithDerived_AcceptsItsOutcome()
        {
            const string runId = "run-select-single-finalized";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = TestRunProtocol.RunOutcome.Passed
            });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Passed, _store.ReadRun(runId).outcome);
        }

        [Test]
        public void SelectTerminalOutcome_TwoConflictingFinalizedEvents_ReturnsInvalid()
        {
            const string runId = "run-select-conflict";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = TestRunProtocol.RunOutcome.Passed
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = TestRunProtocol.RunOutcome.Failed
            });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, _store.ReadRun(runId).outcome,
                "Conflicting RunFinalized events must produce invalid outcome.");
        }

        [Test]
        public void SelectTerminalOutcome_NonTerminalLegacyOutcomeInRunRecord_ReturnsInvalid()
        {
            const string runId = "run-select-nonterminal-legacy";
            WriteFinalizingRun(runId, "mcp",
                provisionalOutcome: "running",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, _store.ReadRun(runId).outcome,
                "Non-terminal legacy run.outcome must be rejected and produce invalid.");
        }

        [Test]
        public void SelectTerminalOutcome_MorePessimistic_FailedOverPassed()
        {
            const string runId = "run-select-pessimistic";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = TestRunProtocol.RunOutcome.Failed
            });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Failed, _store.ReadRun(runId).outcome,
                "MorePessimistic must prefer 'failed' (rank 2) over 'passed' (rank 1).");
        }

        [Test]
        public void SelectTerminalOutcome_InvalidOutcomeIsMostPessimistic()
        {
            const string runId = "run-select-invalid-highest";
            WriteFinalizingRun(runId, "mcp",
                runFinishedOutcome: TestRunProtocol.RunOutcome.Passed);
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = TestRunProtocol.RunOutcome.Invalid
            });
            _framework.AnyActivity = UtfRunActivity.Inactive;
            _framework.Activity = UtfRunActivity.Inactive;

            CreateFinalizer().TryFinalize(runId);

            Assert.AreEqual(TestRunProtocol.RunOutcome.Invalid, _store.ReadRun(runId).outcome,
                "'invalid' has the highest rank and must override any other outcome.");
        }

        private TestRunService CreateService(
            TestRunBuildFingerprint build = null,
            Action<string> afterDurableBoundary = null) =>
            new TestRunService(
                _store,
                _environment,
                _framework,
                () => build ?? CoherentBuild(),
                () => false,
                () => false,
                () => true,
                () => Utc,
                afterDurableBoundary);

        private TestRunFinalizationCoordinator CreateFinalizer(
            TestRunBuildFingerprint completionBuild = null) =>
            new TestRunFinalizationCoordinator(
                _store,
                _environment,
                _framework,
                () => Utc,
                _ => { },
                completionBuild == null
                    ? (Func<TestRunBuildFingerprint>)null
                    : () => completionBuild);

        private void ApplyStartBuild(string runId)
        {
            var run = _store.ReadRun(runId);
            CoherentBuild().ApplyTo(run);
            _store.WriteRun(run);
        }

        private void WriteFinalizingRun(
            string runId,
            string source,
            string provisionalOutcome = "",
            string runFinishedOutcome = TestRunProtocol.RunOutcome.Invalid)
        {
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                source = source,
                lifecycle = TestRunProtocol.Lifecycle.Finalizing,
                created_utc = Utc,
                build_coherent = true,
                utf_version = "1.6.0"
            });
            if (!string.IsNullOrEmpty(provisionalOutcome))
            {
                // Simulate a pre-invariant run.json produced by the legacy
                // implementation. Production writes can no longer create this.
                var legacy = _store.ReadRun(runId);
                legacy.outcome = provisionalOutcome;
                File.WriteAllText(_store.GetRunRecordPath(runId),
                    JsonUtility.ToJson(legacy, true) + "\n");
            }
            _store.WriteActive(new TestRunPointer
            {
                run_id = runId,
                updated_utc = Utc
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunStarted,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                expected_count = 0
            });
            _store.SealManifest(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.ManifestSealed,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                expected_count = 0
            });
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_type = TestRunProtocol.EventType.RunFinished,
                occurred_utc = Utc,
                observer_generation = "test-generation",
                outcome = runFinishedOutcome,
                root_trusted = true,
                has_aggregate = true
            });
        }

        private static TestRunBuildFingerprint CoherentBuild() =>
            new TestRunBuildFingerprint
            {
                ProjectIdentity = "/tmp/project",
                EditorProcessIdentity = TestRunBuildFingerprintProbe.EditorProcessIdentity(),
                Fingerprint = "mvid=fresh;utf=1.6.0",
                UtfVersion = "1.6.0",
                AssemblyPath = "/tmp/UnityMCP.Editor.dll",
                SourcePath = "/tmp/TestRunner.cs",
                IsCoherent = true
            };

        private static string DecodeReason(string response)
        {
            var field = response.Split('|').Single(part =>
                part.StartsWith("reason_b64=", StringComparison.Ordinal));
            return Encoding.UTF8.GetString(Convert.FromBase64String(
                field.Substring("reason_b64=".Length)));
        }

        private void WriteBareRun(string runId, string createdUtc)
        {
            _store.WriteRun(new TestRunRecord
            {
                run_id = runId,
                lifecycle = TestRunProtocol.Lifecycle.Prepared,
                created_utc = createdUtc
            });
        }

        private const string Utc = "2026-08-02T12:00:00.0000000Z";

        private sealed class FakeFrameworkDriver : ITestFrameworkDriver
        {
            internal int ExecuteCalls;
            internal int CancelCalls;
            internal ExecutionSettings LastSettings;
            internal string LastCancelledGuid;
            internal bool CancelResult = true;
            internal Exception CancelError;
            internal UtfRunActivity Activity = UtfRunActivity.Active;
            internal UtfRunActivity AnyActivity = UtfRunActivity.Inactive;
            internal Action OnExecute;

            public string Execute(ExecutionSettings settings)
            {
                ExecuteCalls++;
                LastSettings = settings;
                OnExecute?.Invoke();
                return "utf-guid-1";
            }

            public bool Cancel(string utfGuid)
            {
                CancelCalls++;
                LastCancelledGuid = utfGuid;
                if (CancelError != null) throw CancelError;
                return CancelResult;
            }

            public UtfRunActivity Probe(string utfGuid) => Activity;
            public UtfRunActivity ProbeAny() => AnyActivity;
        }

        private sealed class FakeEnvironment : ITestRunEnvironmentController
        {
            internal int PrepareCalls;
            internal int RestoreCalls;
            internal Exception PrepareError;
            internal Exception RestoreError;

            public TestRunEnvironmentRecord Prepare(
                TestRunStore store, string runId, string utcNow)
            {
                PrepareCalls++;
                if (PrepareError != null) throw PrepareError;
                if (store.TryReadEnvironment(runId, out var existing)) return existing;
                var environment = new TestRunEnvironmentRecord
                {
                    run_id = runId,
                    prepared_utc = utcNow
                };
                store.WriteEnvironment(environment);
                return environment;
            }

            public void Restore(TestRunStore store, string runId, string utcNow)
            {
                RestoreCalls++;
                if (RestoreError != null) throw RestoreError;
            }
        }
    }
}
