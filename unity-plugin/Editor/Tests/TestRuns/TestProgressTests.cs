using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestProgressTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _root;
        private TestRunStore _store;
        private TestRunService _service;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-progress-" + Guid.NewGuid().ToString("N"));
            _store = new TestRunStore(_root);
            _service = new TestRunService(
                _store,
                new NoopEnvironment(),
                new NoopFramework(),
                () => new TestRunBuildFingerprint { IsCoherent = true, UtfVersion = "1.6.0" },
                () => false,
                () => false,
                () => true,
                () => "2026-08-02T12:00:10.0000000Z");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void NoDurableRun_ProjectsIdleAndNone()
        {
            Assert.AreEqual("idle", _service.GetLegacyProgress(null));
            Assert.AreEqual("none", _service.GetLegacyResults(null));
        }

        [Test]
        public void PreparedRunWithoutManifest_ProjectsCorrelatedPending()
        {
            WriteRunningRun();

            Assert.AreEqual("pending|run_id=run-progress|no-progress-yet",
                _service.GetLegacyProgress(null));
            Assert.AreEqual("pending", _service.GetLegacyResults(null));
        }

        [Test]
        public void ProgressIsReplayedFromLeafEvidenceNotMutableCounters()
        {
            WriteRunningRun();
            AddExpected("suite.a");
            AddExpected("suite.b");
            Seal(2);
            Finish("suite.a", TestRunProtocol.LeafOutcome.Passed);

            var progress = _service.GetLegacyProgress("run-progress");

            StringAssert.StartsWith("running|1|1|0|0|2|10.0|", progress);
            StringAssert.Contains("|run_id=run-progress", progress);
        }

        [Test]
        public void TerminalProjectionIncludesEveryOutcomeAndExactExpectedTotal()
        {
            WriteRunningRun();
            AddExpected("suite.pass");
            AddExpected("suite.skip");
            AddExpected("suite.cancel");
            Seal(3);
            Finish("suite.pass", TestRunProtocol.LeafOutcome.Passed);
            Finish("suite.skip", TestRunProtocol.LeafOutcome.Skipped);
            Finish("suite.cancel", TestRunProtocol.LeafOutcome.Cancelled);
            _store.AppendEvent("run-progress", new TestRunEvent
            {
                run_id = "run-progress",
                event_type = TestRunProtocol.EventType.RunFinished,
                outcome = TestRunProtocol.RunOutcome.Cancelled,
                observer_generation = "progress-gen",
                root_trusted = true,
                duration_seconds = 4.25,
                occurred_utc = "2026-08-02T12:00:04.2500000Z"
            });
            _store.AppendEvent("run-progress", new TestRunEvent
            {
                run_id = "run-progress",
                event_type = TestRunProtocol.EventType.RunFinalized,
                occurred_utc = "2026-08-02T12:00:04.3000000Z"
            });

            var results = _service.GetLegacyResults("run-progress");
            var progress = _service.GetLegacyProgress("run-progress");

            Assert.AreEqual(
                "3 tests: 1 passed, 0 failed, 1 skipped, 0 inconclusive, " +
                "1 cancelled, 0 invalid (4.3s) outcome=cancelled",
                results);
            Assert.AreEqual("idle|run_id=run-progress|outcome=cancelled", progress);
        }

        [Test]
        public void RunIdentityPreventsReadingAnotherRunsResult()
        {
            WriteRunningRun();

            Assert.AreEqual("none", _service.GetLegacyResults("run-other"));
            Assert.AreEqual("idle", _service.GetLegacyProgress("run-other"));
        }

        private void WriteRunningRun()
        {
            _store.WriteRun(new TestRunRecord
            {
                run_id = "run-progress",
                request_id = "request-progress",
                lifecycle = TestRunProtocol.Lifecycle.Running,
                created_utc = "2026-08-02T12:00:00.0000000Z",
                started_utc = "2026-08-02T12:00:00.0000000Z",
                build_coherent = true,
                utf_version = "1.6.0"
            });
            _store.AppendEvent("run-progress", new TestRunEvent
            {
                run_id = "run-progress",
                event_type = TestRunProtocol.EventType.RunStarted,
                observer_generation = "progress-gen",
                occurred_utc = "2026-08-02T12:00:00.0000000Z"
            });
            _store.WriteActive(new TestRunPointer
            {
                run_id = "run-progress",
                request_id = "request-progress",
                updated_utc = "2026-08-02T12:00:00.0000000Z"
            });
        }

        private void AddExpected(string name)
        {
            _store.AppendExpectedTest("run-progress", new TestLeafManifestEntry
            {
                run_id = "run-progress",
                unique_name = name,
                full_name = name
            });
        }

        private void Seal(int count)
        {
            _store.SealManifest("run-progress", new TestRunEvent
            {
                run_id = "run-progress",
                event_type = TestRunProtocol.EventType.ManifestSealed,
                expected_count = count
            });
        }

        private void Finish(string name, string outcome)
        {
            _store.AppendEvent("run-progress", new TestRunEvent
            {
                run_id = "run-progress",
                event_type = TestRunProtocol.EventType.TestFinished,
                unique_name = name,
                full_name = name,
                outcome = outcome,
                result_state = outcome
            });
        }

        private sealed class NoopEnvironment : ITestRunEnvironmentController
        {
            public TestRunEnvironmentRecord Prepare(
                TestRunStore store, string runId, string utcNow) =>
                new TestRunEnvironmentRecord { run_id = runId };

            public void Restore(TestRunStore store, string runId, string utcNow) { }
        }

        private sealed class NoopFramework : ITestFrameworkDriver
        {
            public string Execute(ExecutionSettings settings) => "guid";
            public bool Cancel(string utfGuid) => true;
            public UtfRunActivity Probe(string utfGuid) => UtfRunActivity.Active;
            public UtfRunActivity ProbeAny() => UtfRunActivity.Inactive;
        }
    }
}
