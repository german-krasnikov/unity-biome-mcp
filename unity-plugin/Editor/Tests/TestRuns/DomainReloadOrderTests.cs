using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class DomainReloadOrderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
        }

        [Test]
        public void AllDurableProtocolCommands_AreRegisteredWithCorrectCompileGuards()
        {
            var readCommands = new[]
            {
                "resolve_test_request", "get_test_run", "list_test_runs",
                "get_test_results", "get_test_progress", "get_test_count"
            };
            foreach (var command in readCommands)
            {
                Assert.IsTrue(CommandRegistry.IsRegistered(command), command);
                Assert.IsTrue(CommandRouter.IsAllowedDuringCompile(command), command);
            }

            Assert.IsTrue(CommandRegistry.IsRegistered("run_tests"));
            Assert.IsFalse(CommandRouter.IsAllowedDuringCompile("run_tests"));
            Assert.IsTrue(CommandRegistry.IsRegistered("cancel_test_run"));
            Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("cancel_test_run"));
        }

        [Test]
        public void TestRunner_HasNoReloadCallbackOrPerRunCollector()
        {
            var reset = typeof(TestRunner).GetMethod("ResetOnReload",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var collector = typeof(TestRunner).GetNestedType("ResultCollector",
                BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNull(reset,
                "Tests must not be able to reflection-call production callback registration.");
            Assert.IsNull(collector,
                "Per-run collectors duplicate global callbacks after domain reload.");
        }

        [Test]
        public void ObserverRegistration_IsInitializeOnLoadAndObserverHandlesInfrastructureErrors()
        {
            Assert.IsTrue(typeof(TestRunObserverRegistration)
                .GetCustomAttributes(typeof(InitializeOnLoadAttribute), false).Any());
            Assert.IsTrue(typeof(IErrorCallbacks).IsAssignableFrom(typeof(TestRunObserver)));
            Assert.IsNotNull(typeof(TestRunObserverRegistration).TypeInitializer,
                "The static constructor is the single registration point per domain.");
        }

        [Test]
        public void NewStoreInstance_ResumesSameActiveRunWithoutSessionState()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-reload-order-" + Guid.NewGuid().ToString("N"));
            try
            {
                var beforeReload = new TestRunStore(root);
                beforeReload.WriteRun(new TestRunRecord
                {
                    run_id = "run-reload",
                    request_id = "request-reload",
                    utf_guid = "utf-reload",
                    lifecycle = TestRunProtocol.Lifecycle.Running,
                    created_utc = "2026-08-02T12:00:00.0000000Z"
                });
                beforeReload.WriteActive(new TestRunPointer
                {
                    run_id = "run-reload",
                    request_id = "request-reload"
                });

                var afterReload = new TestRunStore(root);
                var pointer = afterReload.ReadActive();
                var run = afterReload.ReadRun(pointer.run_id);

                Assert.AreEqual("run-reload", run.run_id);
                Assert.AreEqual("utf-reload", run.utf_guid);
                Assert.AreEqual(TestRunProtocol.Lifecycle.Running, run.lifecycle);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void ReloadedObserver_DoesNotTrustPartialRootAggregate()
        {
            var root = Path.Combine(Path.GetTempPath(),
                "unity-mcp-reload-aggregate-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new TestRunStore(root);
                store.WriteRun(new TestRunRecord
                {
                    run_id = "run-aggregate",
                    lifecycle = TestRunProtocol.Lifecycle.Running,
                    created_utc = "2026-08-02T12:00:00.0000000Z",
                    build_coherent = true,
                    utf_version = "1.6.0"
                });
                store.AppendEvent("run-aggregate", Event(
                    TestRunProtocol.EventType.RunStarted, "domain-a"));
                store.AppendExpectedTest("run-aggregate", new TestLeafManifestEntry
                {
                    run_id = "run-aggregate",
                    unique_name = "suite.before-reload"
                });
                store.AppendExpectedTest("run-aggregate", new TestLeafManifestEntry
                {
                    run_id = "run-aggregate",
                    unique_name = "suite.after-reload"
                });
                var seal = Event(TestRunProtocol.EventType.ManifestSealed, "domain-a");
                seal.expected_count = 2;
                store.SealManifest("run-aggregate", seal);
                var beforeReload = Event(TestRunProtocol.EventType.TestFinished, "domain-a");
                beforeReload.unique_name = "suite.before-reload";
                beforeReload.outcome = TestRunProtocol.LeafOutcome.Failed;
                store.AppendEvent("run-aggregate", beforeReload);
                var afterReload = Event(TestRunProtocol.EventType.TestFinished, "domain-b");
                afterReload.unique_name = "suite.after-reload";
                afterReload.outcome = TestRunProtocol.LeafOutcome.Passed;
                store.AppendEvent("run-aggregate", afterReload);
                var finished = Event(TestRunProtocol.EventType.RunFinished, "domain-b");
                finished.outcome = TestRunProtocol.RunOutcome.Passed;
                finished.root_trusted = false;
                finished.has_aggregate = false;
                store.AppendEvent("run-aggregate", finished);
                store.AppendEvent("run-aggregate", Event(
                    TestRunProtocol.EventType.RunFinalized, "domain-b"));

                var summary = store.Reconcile("run-aggregate");

                Assert.AreEqual(TestRunProtocol.RunOutcome.Failed, summary.outcome);
                Assert.AreEqual(1, summary.passed);
                Assert.AreEqual(1, summary.failed);
                Assert.IsFalse(summary.issues.Any(i =>
                    i.code == "ROOT_AGGREGATE_CONTRADICTION"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static TestRunEvent Event(string type, string generation) =>
            new TestRunEvent
            {
                run_id = "run-aggregate",
                event_type = type,
                observer_generation = generation,
                occurred_utc = "2026-08-02T12:00:00.0000000Z"
            };
    }
}
