// TDD -- TestRunReconciler must surface the real dispatch-failure reason as its
// own issue instead of letting the downstream MANIFEST_FILE_MISSING warning be
// the only visible entry. Baseline environment finding (A01, this plan):
// get_test_run.issues[] showed only a demoted MANIFEST_FILE_MISSING, never the
// actual dispatch_failed message recorded in events.jsonl.
using System;
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunReconcilerTests : UnityMcpTestBase
    {
        private const string RunId = "run-dispatch-001";
        private const string DispatchMessage =
            "the test ownership root cannot be used as a user scene baseline: " +
            "Assets/TestsTemp/PythonLive/x/GridTest.unity";

        [Test]
        public void Reconcile_ManifestMissingWithPriorInfrastructureError_SurfacesOriginalReason()
        {
            var journal = new TestRunJournal
            {
                run_record_exists = true,
                manifest_file_exists = false,
                events_file_exists = true,
                run = new TestRunRecord
                {
                    run_id = RunId,
                    lifecycle = TestRunProtocol.Lifecycle.Terminal,
                    outcome = TestRunProtocol.RunOutcome.DispatchFailed,
                    build_coherent = true,
                },
                events = new[]
                {
                    new TestRunEvent
                    {
                        run_id = RunId,
                        event_id = "evt-dispatch-failed",
                        event_type = TestRunProtocol.EventType.DispatchFailed,
                        message = DispatchMessage,
                    }
                },
            };

            var summary = TestRunReconciler.Reconcile(RunId, journal);

            var dispatchIndex = Array.FindIndex(summary.issues, i => i.code == "DISPATCH_FAILED");
            var manifestIndex = Array.FindIndex(summary.issues, i => i.code == "MANIFEST_FILE_MISSING");

            Assert.That(dispatchIndex, Is.GreaterThanOrEqualTo(0),
                "the original dispatch-failure reason must be surfaced as its own issue");
            Assert.That(summary.issues[dispatchIndex].severity,
                Is.EqualTo(TestRunProtocol.IssueSeverity.Error));
            Assert.That(summary.issues[dispatchIndex].message, Is.EqualTo(DispatchMessage),
                "the original dispatch-failure message must not be dropped");

            Assert.That(manifestIndex, Is.GreaterThanOrEqualTo(0),
                "MANIFEST_FILE_MISSING is still true context and must not disappear entirely");
            Assert.That(summary.issues[manifestIndex].severity,
                Is.EqualTo(TestRunProtocol.IssueSeverity.Warning));

            Assert.That(dispatchIndex, Is.LessThan(manifestIndex),
                "the original dispatch-failure reason is the primary issue; " +
                "MANIFEST_FILE_MISSING must be demoted to secondary");
        }
    }
}
