using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;

#if UNITY_INCLUDE_TESTS
using UnityEditor.TestTools.TestRunner.Api;

namespace UnityMCP.Editor.TestRuns
{
    /// <summary>
    /// The only UnityMCP UTF callback registered in a domain. It observes every
    /// start source and derives state from durable evidence instead of counters.
    /// </summary>
    internal sealed class TestRunObserver : IErrorCallbacks
    {
        private readonly TestRunStore _store;
        private readonly ITestRunEnvironmentController _environment;
        private readonly ITestFrameworkDriver _framework;
        private readonly TestRunFinalizationCoordinator _finalizer;
        private readonly string _generation;
        private readonly Func<string> _utcNow;

        internal TestRunObserver(
            TestRunStore store,
            ITestRunEnvironmentController environment,
            ITestFrameworkDriver framework,
            TestRunFinalizationCoordinator finalizer,
            string generation,
            Func<string> utcNow)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _framework = framework ?? throw new ArgumentNullException(nameof(framework));
            _finalizer = finalizer ?? throw new ArgumentNullException(nameof(finalizer));
            _generation = generation ?? throw new ArgumentNullException(nameof(generation));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            try
            {
                var run = AcquireForRunStarted(testsToRun);
                if (IsDuplicateRunStarted(run.run_id, testsToRun)) return;
                // MCP prepares this transaction before Execute(). Direct Unity UI
                // and command-line runs first become observable here. Prepare is
                // deliberately idempotent so a replay after reload only restores
                // the durable ownership hint.
                if (ShouldPrepareManagedEnvironment(run))
                    _environment.Prepare(_store, run.run_id, _utcNow());
                var now = _utcNow();
                run.lifecycle = TestRunProtocol.Lifecycle.Running;
                run.health = TestRunProtocol.Health.Healthy;
                if (string.IsNullOrEmpty(run.started_utc)) run.started_utc = now;
                _store.WriteRun(run);
                _store.WriteActive(Pointer(run, now));

                // UTF can keep TestCaseCount at the unfiltered root count. The
                // materialized leaf tree is the authoritative selected manifest.
                var identities = new HashSet<string>(StringComparer.Ordinal);
                var manifest = new List<TestLeafManifestEntry>();
                CollectManifestLeaves(run, testsToRun, identities, manifest);
                var started = Event(run.run_id,
                    TestRunProtocol.EventType.RunStarted, now);
                started.unique_name = testsToRun?.UniqueName ?? "";
                started.full_name = testsToRun?.FullName ?? "";
                started.expected_count = identities.Count;
                _store.AppendEvent(run.run_id, started);

                _store.WriteExpectedTests(run.run_id, manifest);
                _store.SealManifest(run.run_id, new TestRunEvent
                {
                    run_id = run.run_id,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.ManifestSealed,
                    occurred_utc = _utcNow(),
                    observer_generation = _generation,
                    expected_count = identities.Count
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RecordObserverFailure("RunStarted observer failed: " + e.Message);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
            if (test == null || test.IsSuite) return;
            try
            {
                var run = AcquireForEvidence("TestStarted arrived without an active durable run.");
                _store.AppendEvent(run.run_id, new TestRunEvent
                {
                    run_id = run.run_id,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.TestStarted,
                    occurred_utc = _utcNow(),
                    observer_generation = _generation,
                    unique_name = RequireUniqueName(test),
                    test_id = test.Id ?? "",
                    full_name = test.FullName ?? ""
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RecordObserverFailure("TestStarted observer failed: " + e.Message);
            }
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (result == null || result.Test == null || result.Test.IsSuite) return;
            try
            {
                var run = AcquireForEvidence("TestFinished arrived without an active durable run.");
                _store.AppendEvent(run.run_id, ResultEvent(run.run_id,
                    TestRunProtocol.EventType.TestFinished, result));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RecordObserverFailure("TestFinished observer failed: " + e.Message);
            }
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            try
            {
                var run = AcquireForRunFinished(result);
                if (run.lifecycle == TestRunProtocol.Lifecycle.Terminal ||
                    HasEvent(run.run_id, TestRunProtocol.EventType.RunFinished))
                    return;
                var now = _utcNow();
                run.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                _store.WriteRun(run);

                var journal = _store.ReadJournal(run.run_id);
                var start = journal.events.FirstOrDefault(e =>
                    e != null && e.event_type == TestRunProtocol.EventType.RunStarted);
                var trustRoot = start != null &&
                    string.Equals(start.observer_generation, _generation, StringComparison.Ordinal);
                var finished = ResultEvent(run.run_id,
                    TestRunProtocol.EventType.RunFinished, result);
                finished.outcome = MapRunOutcome(result);
                finished.root_trusted = trustRoot;
                finished.has_aggregate = trustRoot;
                if (trustRoot) PopulateAggregate(finished, result);
                _store.AppendEvent(run.run_id, finished);
                try
                {
                    SaveUtfXml(run.run_id, result);
                }
                catch (Exception xmlError)
                {
                    AppendInfrastructureError(run.run_id,
                        "UTF result XML capture failed: " + xmlError.Message);
                }
                finally
                {
                    _finalizer.Request(run.run_id);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                RecordObserverFailure("RunFinished observer failed: " + e.Message);
            }
        }

        public void OnError(string message)
        {
            try
            {
                AcquireForEvidence("UTF OnError arrived without an active durable run.");
                RecordObserverFailure(message ?? "UTF reported an infrastructure error.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private TestRunRecord AcquireForRunStarted(ITestAdaptor root)
        {
            if (_store.TryReadActive(out var pointer) &&
                _store.TryReadRun(pointer.run_id, out var active) &&
                active.lifecycle != TestRunProtocol.Lifecycle.Terminal)
            {
                var hasStarted = _store.ReadJournal(active.run_id).events.Any(e =>
                    e != null && e.event_type == TestRunProtocol.EventType.RunStarted);
                var dispatchingNow = string.Equals(TestRunDispatchContext.RunId,
                    active.run_id, StringComparison.Ordinal);
                if (TestRunFinalizationCoordinator.IsPreviousEditorSession(active) &&
                    _finalizer.TryFinalize(active.run_id))
                    return CreateObservedRun("unity-ui",
                        root?.TestMode.ToString() ?? "");
                if (!hasStarted &&
                    (active.lifecycle == TestRunProtocol.Lifecycle.Dispatched ||
                     (active.lifecycle == TestRunProtocol.Lifecycle.Prepared && dispatchingNow)))
                    return active;
                if (hasStarted && IsDuplicateRunStarted(active.run_id, root))
                    return active;
                if (IsUnmanagedRun(active))
                {
                    active.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                    active.health = TestRunProtocol.Health.SuspectedStall;
                    _store.WriteRun(active);
                    if (_finalizer.TryFinalize(active.run_id))
                        return CreateObservedRun("unity-ui",
                            root?.TestMode.ToString() ?? "");
                }
                throw new InvalidOperationException(
                    "a concurrent or uncorrelated UTF RunStarted arrived while run " +
                    active.run_id + " is still active");
            }

            return CreateObservedRun("unity-ui", root?.TestMode.ToString() ?? "");
        }

        private static bool IsUnmanagedRun(TestRunRecord run) =>
            run != null && (string.Equals(run.source, "unity-ui", StringComparison.Ordinal) ||
                            string.Equals(run.source, "unity-ui-orphan", StringComparison.Ordinal));

        internal static bool ShouldPrepareManagedEnvironment(TestRunRecord run) =>
            run != null && (!IsUnmanagedRun(run) ||
                            (string.Equals(run.source, "unity-ui", StringComparison.Ordinal) &&
                             string.Equals(run.mode, "EditMode", StringComparison.Ordinal)));

        private TestRunRecord AcquireForEvidence(string orphanReason)
        {
            if (_store.TryReadActive(out var pointer) &&
                _store.TryReadRun(pointer.run_id, out var active) &&
                active.lifecycle != TestRunProtocol.Lifecycle.Terminal)
                return active;

            var orphan = CreateObservedRun("unity-ui-orphan", "");
            AppendInfrastructureError(orphan.run_id, orphanReason);
            return orphan;
        }

        private TestRunRecord AcquireForRunFinished(ITestResultAdaptor result)
        {
            var preserveActive = false;
            if (_store.TryReadActive(out var pointer) &&
                _store.TryReadRun(pointer.run_id, out var active))
            {
                preserveActive = active.lifecycle != TestRunProtocol.Lifecycle.Terminal;
                var journal = _store.ReadJournal(active.run_id);
                var started = journal.events.FirstOrDefault(e =>
                    e != null && e.event_type == TestRunProtocol.EventType.RunStarted);
                var rootMatches = RootIdentityMatches(
                    active,
                    started,
                    result?.Test?.UniqueName ?? "",
                    result?.Test?.FullName ?? "",
                    result?.Test?.TestMode.ToString() ?? "");
                var priorFinished = journal.events.LastOrDefault(e =>
                    e != null && e.event_type == TestRunProtocol.EventType.RunFinished);
                var hasFinished = priorFinished != null;
                if (rootMatches &&
                    (!hasFinished || active.lifecycle == TestRunProtocol.Lifecycle.Finalizing))
                    return active;
                if (rootMatches && active.lifecycle == TestRunProtocol.Lifecycle.Terminal &&
                    string.Equals(priorFinished.observer_generation, _generation,
                        StringComparison.Ordinal) &&
                    string.Equals(priorFinished.unique_name ?? "",
                        result?.Test?.UniqueName ?? "", StringComparison.Ordinal))
                    return active;
            }

            var orphan = CreateObservedRun("unity-ui-orphan",
                result?.Test?.TestMode.ToString() ?? "", !preserveActive);
            AppendInfrastructureError(orphan.run_id,
                preserveActive
                    ? "RunFinished root identity did not match the active durable run."
                    : "RunFinished arrived without a matching durable RunStarted.");
            if (result?.Test != null)
            {
                var identities = new HashSet<string>(StringComparer.Ordinal);
                var manifest = new List<TestLeafManifestEntry>();
                CollectManifestLeaves(orphan, result.Test, identities, manifest);
                _store.WriteExpectedTests(orphan.run_id, manifest);
                _store.SealManifest(orphan.run_id, new TestRunEvent
                {
                    run_id = orphan.run_id,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.ManifestSealed,
                    occurred_utc = _utcNow(),
                    observer_generation = _generation,
                    expected_count = identities.Count
                });
            }
            return orphan;
        }

        private TestRunRecord CreateObservedRun(
            string source,
            string mode,
            bool writeActive = true)
        {
            var now = _utcNow();
            var run = new TestRunRecord
            {
                run_id = "run-" + Guid.NewGuid().ToString("N"),
                source = source,
                lifecycle = TestRunProtocol.Lifecycle.Running,
                health = TestRunProtocol.Health.Healthy,
                created_utc = now,
                started_utc = now,
                mode = mode ?? ""
            };
            var build = TestRunBuildFingerprintProbe.Capture();
            build.ApplyTo(run);
            _store.WriteRun(run);
            if (writeActive) _store.WriteActive(Pointer(run, now));
            if (!build.IsCoherent)
                AppendInfrastructureError(run.run_id,
                    "Observed UTF run has an incoherent build: " + build.Error);
            return run;
        }

        internal static bool RootIdentityMatches(
            TestRunRecord run,
            TestRunEvent started,
            string completedUniqueName,
            string completedFullName,
            string completedMode)
        {
            if (run == null || started == null ||
                string.IsNullOrWhiteSpace(started.unique_name) ||
                string.IsNullOrWhiteSpace(completedUniqueName) ||
                !string.Equals(started.unique_name, completedUniqueName,
                    StringComparison.Ordinal))
                return false;
            if (!string.Equals(started.full_name ?? "", completedFullName ?? "",
                    StringComparison.Ordinal))
                return false;
            return string.IsNullOrEmpty(run.mode) ||
                   string.Equals(run.mode, completedMode ?? "", StringComparison.Ordinal);
        }

        private static void CollectManifestLeaves(
            TestRunRecord run,
            ITestAdaptor node,
            ISet<string> identities,
            ICollection<TestLeafManifestEntry> manifest)
        {
            if (node == null) return;
            if (!node.IsSuite)
            {
                var uniqueName = RequireUniqueName(node);
                if (!identities.Add(uniqueName)) return;
                manifest.Add(new TestLeafManifestEntry
                {
                    run_id = run.run_id,
                    unique_name = uniqueName,
                    test_id = node.Id ?? "",
                    full_name = node.FullName ?? "",
                    assembly_name = FindAssemblyName(node),
                    mode = node.TestMode.ToString()
                });
                return;
            }

            foreach (var child in node.Children ?? Enumerable.Empty<ITestAdaptor>())
                CollectManifestLeaves(run, child, identities, manifest);
        }

        private void SaveUtfXml(string runId, ITestResultAdaptor result)
        {
            var capturePath = _store.GetUtfResultsPath(runId) +
                ".capture-" + Guid.NewGuid().ToString("N") + ".xml";
            try
            {
                TestRunnerApi.SaveResultToFile(result, capturePath);
                if (!File.Exists(capturePath) || new FileInfo(capturePath).Length == 0)
                    throw new IOException("UTF result XML capture is missing or empty.");
                var xml = File.ReadAllText(capturePath);
                var document = new XmlDocument { PreserveWhitespace = true };
                document.LoadXml(xml);
                if (document.DocumentElement == null ||
                    document.DocumentElement.Name != "test-run")
                    throw new InvalidDataException(
                        "UTF result XML does not contain an NUnit test-run root.");
                _store.WriteUtfResults(runId, xml);
            }
            finally
            {
                if (File.Exists(capturePath)) File.Delete(capturePath);
            }
        }

        private TestRunEvent ResultEvent(
            string runId,
            string eventType,
            ITestResultAdaptor result)
        {
            var resultState = result?.ResultState ?? "";
            return new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = eventType,
                occurred_utc = _utcNow(),
                observer_generation = _generation,
                unique_name = result?.Test == null ? "" : RequireUniqueName(result.Test),
                test_id = result?.Test?.Id ?? "",
                full_name = result?.Test?.FullName ?? result?.FullName ?? "",
                outcome = eventType == TestRunProtocol.EventType.TestFinished
                    ? MapLeafOutcome(result)
                    : MapRunOutcome(result),
                result_state = resultState,
                label = ResultLabel(resultState),
                message = result?.Message ?? "",
                stack_trace = result?.StackTrace ?? "",
                duration_seconds = result?.Duration ?? 0d
            };
        }

        private void AppendInfrastructureError(string runId, string message)
        {
            _store.AppendEvent(runId, new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = TestRunProtocol.EventType.InfrastructureError,
                occurred_utc = _utcNow(),
                observer_generation = _generation,
                message = message ?? "UTF infrastructure error"
            });
        }

        private void RecordObserverFailure(string message)
        {
            try
            {
                if (!_store.TryReadActive(out var pointer) ||
                    !_store.TryReadRun(pointer.run_id, out var run) ||
                    run.lifecycle == TestRunProtocol.Lifecycle.Terminal) return;
                try
                {
                    AppendInfrastructureError(run.run_id, message);
                }
                catch (Exception evidenceError)
                {
                    Debug.LogException(evidenceError);
                }

                run = _store.ReadRun(run.run_id);
                run.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                run.health = TestRunProtocol.Health.SuspectedStall;
                _store.WriteRun(run);
                _finalizer.Request(run.run_id);
                if (!string.IsNullOrEmpty(run.utf_guid))
                {
                    var cancellationAlreadyRequested = _store.ReadJournal(run.run_id)
                        .events.Any(e => e != null &&
                            e.event_type == TestRunProtocol.EventType.CancelRequested);
                    if (!cancellationAlreadyRequested)
                    {
                        // Persist intent first so repeated callback failures cannot
                        // issue duplicate cancellation calls for the exact UTF GUID.
                        _store.AppendEvent(run.run_id, new TestRunEvent
                        {
                            run_id = run.run_id,
                            event_id = Guid.NewGuid().ToString("N"),
                            event_type = TestRunProtocol.EventType.CancelRequested,
                            occurred_utc = _utcNow(),
                            observer_generation = _generation,
                            message = "Exact UTF run cancellation requested after " +
                                      "observer infrastructure failure; utf_guid=" +
                                      run.utf_guid + "."
                        });
                        try
                        {
                            _framework.Cancel(run.utf_guid);
                        }
                        catch (Exception cancelError)
                        {
                            Debug.LogException(cancelError);
                        }
                    }
                }
            }
            catch (Exception nested)
            {
                Debug.LogException(nested);
            }
        }

        private bool HasEvent(string runId, string eventType) =>
            _store.ReadJournal(runId).events.Any(e =>
                e != null && e.event_type == eventType);

        private bool IsDuplicateRunStarted(string runId, ITestAdaptor root)
        {
            var prior = _store.ReadJournal(runId).events.FirstOrDefault(e =>
                e != null && e.event_type == TestRunProtocol.EventType.RunStarted);
            return prior != null &&
                   string.Equals(prior.observer_generation, _generation,
                       StringComparison.Ordinal) &&
                   string.Equals(prior.unique_name ?? "", root?.UniqueName ?? "",
                       StringComparison.Ordinal) &&
                   string.Equals(prior.full_name ?? "", root?.FullName ?? "",
                       StringComparison.Ordinal);
        }

        private static void PopulateAggregate(TestRunEvent target, ITestResultAdaptor root)
        {
            foreach (var leaf in ResultLeaves(root))
            {
                target.aggregate_total++;
                switch (MapLeafOutcome(leaf))
                {
                    case TestRunProtocol.LeafOutcome.Passed: target.aggregate_passed++; break;
                    case TestRunProtocol.LeafOutcome.Failed: target.aggregate_failed++; break;
                    case TestRunProtocol.LeafOutcome.Skipped: target.aggregate_skipped++; break;
                    case TestRunProtocol.LeafOutcome.Inconclusive: target.aggregate_inconclusive++; break;
                    case TestRunProtocol.LeafOutcome.Cancelled: target.aggregate_cancelled++; break;
                    case TestRunProtocol.LeafOutcome.Invalid: target.aggregate_invalid++; break;
                }
            }
        }

        private static IEnumerable<ITestResultAdaptor> ResultLeaves(ITestResultAdaptor node)
        {
            if (node == null) yield break;
            if (node.Test != null && !node.Test.IsSuite)
            {
                yield return node;
                yield break;
            }
            foreach (var child in node.Children ?? Enumerable.Empty<ITestResultAdaptor>())
                foreach (var leaf in ResultLeaves(child))
                    yield return leaf;
        }

        internal static string MapLeafOutcome(ITestResultAdaptor result)
        {
            if (result == null) return TestRunProtocol.LeafOutcome.Invalid;
            return MapLeafOutcome(result.ResultState, result.TestStatus);
        }

        internal static string MapLeafOutcome(string resultState, TestStatus status)
        {
            var state = (resultState ?? "").ToLowerInvariant();
            if (state.Contains("invalid") || state.Contains("notrunnable"))
                return TestRunProtocol.LeafOutcome.Invalid;
            if (state.Contains("cancel") || state.Contains("notrun"))
                return TestRunProtocol.LeafOutcome.Cancelled;
            switch (status)
            {
                case TestStatus.Passed: return TestRunProtocol.LeafOutcome.Passed;
                case TestStatus.Failed: return TestRunProtocol.LeafOutcome.Failed;
                case TestStatus.Skipped: return TestRunProtocol.LeafOutcome.Skipped;
                case TestStatus.Inconclusive: return TestRunProtocol.LeafOutcome.Inconclusive;
                default: return TestRunProtocol.LeafOutcome.Invalid;
            }
        }

        internal static string MapRunOutcome(ITestResultAdaptor result)
        {
            var leaf = MapLeafOutcome(result);
            switch (leaf)
            {
                case TestRunProtocol.LeafOutcome.Passed:
                case TestRunProtocol.LeafOutcome.Skipped:
                    return TestRunProtocol.RunOutcome.Passed;
                case TestRunProtocol.LeafOutcome.Cancelled:
                    return TestRunProtocol.RunOutcome.Cancelled;
                case TestRunProtocol.LeafOutcome.Invalid:
                    return TestRunProtocol.RunOutcome.Invalid;
                default:
                    return TestRunProtocol.RunOutcome.Failed;
            }
        }

        private static string ResultLabel(string state)
        {
            var separator = string.IsNullOrEmpty(state) ? -1 : state.IndexOf(':');
            return separator < 0 || separator == state.Length - 1
                ? ""
                : state.Substring(separator + 1);
        }

        private static string RequireUniqueName(ITestAdaptor test)
        {
            if (test == null || string.IsNullOrWhiteSpace(test.UniqueName))
                throw new InvalidOperationException("UTF test node has no UniqueName.");
            return test.UniqueName;
        }

        private static string FindAssemblyName(ITestAdaptor test)
        {
            for (var current = test; current != null; current = current.Parent)
                if (current.IsTestAssembly) return current.Name ?? "";
            var uniqueName = test?.UniqueName ?? "";
            var separator = uniqueName.IndexOf('/');
            return separator > 0 ? uniqueName.Substring(0, separator) : "";
        }

        private TestRunEvent Event(string runId, string type, string now) =>
            new TestRunEvent
            {
                run_id = runId,
                event_id = Guid.NewGuid().ToString("N"),
                event_type = type,
                occurred_utc = now,
                observer_generation = _generation
            };

        private static TestRunPointer Pointer(TestRunRecord run, string now) =>
            new TestRunPointer
            {
                run_id = run.run_id,
                request_id = run.request_id,
                updated_utc = now
            };
    }

    [InitializeOnLoad]
    internal static class TestRunObserverRegistration
    {
        private static readonly TestRunStore Store;
        private static readonly ITestRunEnvironmentController Environment;
        private static readonly ITestFrameworkDriver Framework;
        private static readonly TestRunFinalizationCoordinator Finalizer;
        private static readonly TestRunObserver Observer;

        // Test seam — production default; tests substitute this to simulate Play Mode
        // transitions without a real Play Mode entry (mirrors
        // PlayModeEpochTracker.IsPlayingOrWillChange, same file family).
        internal static Func<bool> IsPlayingOrWillChangePlaymode =
            () => EditorApplication.isPlayingOrWillChangePlaymode;

        internal static string Generation { get; }

        static TestRunObserverRegistration()
        {
            Generation = Guid.NewGuid().ToString("N");
            Store = TestRunStore.ForProject(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
            Environment = new UnityTestRunEnvironmentController();
            Framework = new UnityTestFrameworkDriver();
            Finalizer = new TestRunFinalizationCoordinator(
                Store,
                Environment,
                Framework,
                UtcNow,
                MainThreadDispatcher.Enqueue);
            Observer = new TestRunObserver(
                Store,
                Environment,
                Framework,
                Finalizer,
                Generation,
                UtcNow);
            TestRunnerApi.RegisterTestCallback(Observer, -1000);
            AssemblyReloadEvents.beforeAssemblyReload += MarkReloading;
            EditorApplication.quitting += FinalizeActiveOnEditorShutdown;
            MainThreadDispatcher.Enqueue(MarkHealthyAfterReload);
            MainThreadDispatcher.Enqueue(RecoverTerminalEnvironments);
        }

        private static void FinalizeActiveOnEditorShutdown()
        {
            try
            {
                if (!Store.TryReadActive(out var pointer) ||
                    !Store.TryReadRun(pointer.run_id, out var run) ||
                    run.lifecycle == TestRunProtocol.Lifecycle.Terminal)
                    return;

                Finalizer.TryFinalizeForEditorShutdown(run.run_id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void MarkReloading()
        {
            try
            {
                if (!Store.TryReadActive(out var pointer) ||
                    !Store.TryReadRun(pointer.run_id, out var run) ||
                    run.lifecycle == TestRunProtocol.Lifecycle.Terminal) return;
                Store.AppendEvent(run.run_id, new TestRunEvent
                {
                    run_id = run.run_id,
                    event_id = Guid.NewGuid().ToString("N"),
                    event_type = TestRunProtocol.EventType.DomainReloading,
                    occurred_utc = UtcNow(),
                    observer_generation = Generation
                });
                run.health = TestRunProtocol.Health.Reloading;
                Store.WriteRun(run);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void MarkHealthyAfterReload()
        {
            TryUpdateActiveHealth(TestRunProtocol.Health.Healthy);
        }

        private static void TryUpdateActiveHealth(string health)
        {
            try
            {
                if (!Store.TryReadActive(out var pointer) ||
                    !Store.TryReadRun(pointer.run_id, out var run) ||
                    run.lifecycle == TestRunProtocol.Lifecycle.Terminal) return;
                run.health = health;
                Store.WriteRun(run);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static void RecoverTerminalEnvironments()
        {
            if (IsPlayingOrWillChangePlaymode())
            {
                // Idempotent: a second call while still in/entering Play Mode (e.g. a
                // mid-Play-Mode script recompile re-running this static ctor) must not
                // accumulate a duplicate WaitForPlayModeExit subscriber.
                EditorApplication.update -= WaitForPlayModeExit;
                EditorApplication.update += WaitForPlayModeExit;
                return;
            }

            if (Store.TryReadActive(out var activePointer) &&
                Store.TryReadRun(activePointer.run_id, out var activeRun) &&
                activeRun.lifecycle != TestRunProtocol.Lifecycle.Terminal)
            {
                try
                {
                    var staleEditorSession = !string.IsNullOrEmpty(activeRun.editor_session_id) &&
                        !string.Equals(activeRun.editor_session_id,
                            TestRunBuildFingerprintProbe.EditorSessionId(), StringComparison.Ordinal);
                    var ownActivity = string.IsNullOrEmpty(activeRun.utf_guid)
                        ? UtfRunActivity.Inactive
                        : Framework.Probe(activeRun.utf_guid);
                    var anyActivity = Framework.ProbeAny();
                    if (staleEditorSession ||
                        (ownActivity == UtfRunActivity.Inactive &&
                         anyActivity == UtfRunActivity.Inactive))
                    {
                        activeRun.lifecycle = TestRunProtocol.Lifecycle.Finalizing;
                        activeRun.health = TestRunProtocol.Health.SuspectedStall;
                        Store.WriteRun(activeRun);
                        Finalizer.Request(activeRun.run_id);
                    }
                    else if (activeRun.lifecycle == TestRunProtocol.Lifecycle.Finalizing)
                    {
                        Finalizer.Request(activeRun.run_id);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"{BiomeLabel.Tag} Could not recover active test run " +
                        activeRun.run_id + ": " + e.Message);
                }
                return;
            }

            if (Store.TryReadActive(out activePointer) &&
                Store.TryReadRun(activePointer.run_id, out activeRun) &&
                activeRun.lifecycle == TestRunProtocol.Lifecycle.Terminal)
                Finalizer.TryFinalize(activeRun.run_id);

            if (Framework.ProbeAny() != UtfRunActivity.Inactive) return;

            foreach (var runId in Store.ListRunIds())
            {
                try
                {
                    if (!Store.TryReadRun(runId, out var run) ||
                        run.lifecycle != TestRunProtocol.Lifecycle.Terminal ||
                        !Store.TryReadEnvironment(runId, out var record) ||
                        !string.IsNullOrEmpty(record.restored_utc)) continue;
                    Environment.Restore(Store, runId, UtcNow());
                }
                catch (Exception e)
                {
                    Debug.LogError($"{BiomeLabel.Tag} Could not recover test environment for " +
                        runId + ": " + e.Message);
                }
            }
        }

        // DEV-66: self re-arming poll — same idiom as RuntimeHelper.TimeoutCheck — that
        // replaces the old one-shot-requeue-on-every-tick trigger. Keeps ticking on
        // EditorApplication.update until Play Mode has actually exited, then
        // unsubscribes and resumes the recovery this guarded against re-entering
        // mid-Play-Mode.
        internal static void WaitForPlayModeExit()
        {
            if (IsPlayingOrWillChangePlaymode()) return;
            EditorApplication.update -= WaitForPlayModeExit;
            RecoverTerminalEnvironments();
        }

        /// <summary>Restore the Play Mode seam to production behavior. Test seam — call
        /// from RegisterCleanup only.</summary>
        internal static void ResetPlayModeSeamForTest() =>
            IsPlayingOrWillChangePlaymode = () => EditorApplication.isPlayingOrWillChangePlaymode;

        private static string UtcNow() => DateTime.UtcNow.ToString("O");
    }
}
#endif
