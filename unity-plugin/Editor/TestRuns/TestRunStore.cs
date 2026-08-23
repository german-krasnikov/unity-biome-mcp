using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor.TestRuns
{
    public sealed class TestRunStoreException : IOException
    {
        public TestRunStoreException(string message) : base(message) { }
        public TestRunStoreException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Project-scoped durable storage for test-run intent and evidence. The root is
    /// injectable so protocol tests never touch production state.
    /// </summary>
    public sealed class TestRunStore
    {
        private static readonly object FileGate = new object();
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public TestRunStore(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("A test-run store root is required.", nameof(rootPath));

            RootPath = Path.GetFullPath(rootPath);
        }

        public string RootPath { get; }

        public static TestRunStore ForProject(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("A project root is required.", nameof(projectRoot));

            return new TestRunStore(Path.Combine(
                Path.GetFullPath(projectRoot), "Library", "UnityMCP", "TestRuns"));
        }

        public string ActivePath => Path.Combine(RootPath, "active.json");
        public string LatestPath => Path.Combine(RootPath, "latest.json");
        public string QuarantinePath => Path.Combine(RootPath, "quarantine");

        public string GetRequestPath(string requestId)
        {
            ValidateIdentity(requestId, nameof(requestId));
            return Path.Combine(RootPath, "requests", requestId + ".json");
        }

        public string GetRunDirectory(string runId)
        {
            ValidateIdentity(runId, nameof(runId));
            return Path.Combine(RootPath, "runs", runId);
        }

        public string GetRunRecordPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "run.json");

        public string GetExpectedTestsPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "expected-tests.jsonl");

        public string GetEventsPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "events.jsonl");

        public string GetSummaryPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "summary.json");

        public string GetEnvironmentPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "environment.json");

        public string GetUtfResultsPath(string runId) =>
            Path.Combine(GetRunDirectory(runId), "utf-results.xml");

        public void WriteRequest(TestRunRequestRecord request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported request schema.", nameof(request));
            ValidateIdentity(request.request_id, nameof(request.request_id));
            ValidateIdentity(request.run_id, nameof(request.run_id));
            var path = GetRequestPath(request.request_id);
            lock (FileGate)
            {
                if (File.Exists(path))
                {
                    var existing = ReadJsonOrThrowWithoutLock<TestRunRequestRecord>(path);
                    ValidateStoredRequest(existing, request.request_id);
                    if (!string.Equals(existing.run_id, request.run_id, StringComparison.Ordinal))
                        throw new TestRunStoreException(
                            $"Request '{request.request_id}' is already bound to run '{existing.run_id}'.");
                    ValidateRequestUpdate(existing, request);
                }
                WriteAtomicJsonWithoutLock(path, request);
            }
        }

        /// <summary>
        /// Persists a request idempotency key once. Repeated calls return the first
        /// durable record, so a transport retry cannot redirect the request to a new
        /// run. Corrupt existing data is surfaced and never overwritten.
        /// </summary>
        public TestRunRequestRecord CreateRequestOnce(TestRunRequestRecord candidate) =>
            CreateRequestOnce(candidate, out _);

        public TestRunRequestRecord CreateRequestOnce(
            TestRunRequestRecord candidate,
            out bool created)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (candidate.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported request schema.", nameof(candidate));
            ValidateIdentity(candidate.request_id, nameof(candidate.request_id));
            ValidateIdentity(candidate.run_id, nameof(candidate.run_id));

            lock (FileGate)
            {
                var path = GetRequestPath(candidate.request_id);
                if (File.Exists(path))
                {
                    var existing = ReadJsonOrThrowWithoutLock<TestRunRequestRecord>(path);
                    ValidateStoredRequest(existing, candidate.request_id);
                    created = false;
                    return existing;
                }
                WriteAtomicJsonWithoutLock(path, candidate);
                created = true;
                return candidate;
            }
        }

        public TestRunRequestRecord ReadRequest(string requestId)
        {
            var request = ReadJsonOrThrow<TestRunRequestRecord>(GetRequestPath(requestId));
            ValidateStoredRequest(request, requestId);
            return request;
        }

        public bool TryReadRequest(string requestId, out TestRunRequestRecord request)
        {
            var path = GetRequestPath(requestId);
            if (!File.Exists(path))
            {
                request = null;
                return false;
            }

            request = ReadJsonOrThrow<TestRunRequestRecord>(path);
            ValidateStoredRequest(request, requestId);
            return true;
        }

        public void WriteRun(TestRunRecord run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (run.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported run schema.", nameof(run));
            ValidateIdentity(run.run_id, nameof(run.run_id));
            if (!string.IsNullOrEmpty(run.request_id))
                ValidateIdentity(run.request_id, nameof(run.request_id));
            var path = GetRunRecordPath(run.run_id);
            lock (FileGate)
            {
                if (File.Exists(path))
                {
                    var existing = ReadJsonOrThrowWithoutLock<TestRunRecord>(path);
                    ValidateStoredRun(existing, run.run_id);
                    if (!string.Equals(existing.request_id ?? "", run.request_id ?? "",
                            StringComparison.Ordinal))
                        throw new TestRunStoreException(
                            $"Run '{run.run_id}' cannot be rebound to another request.");
                    ValidateRunUpdate(existing, run);
                }
                else
                {
                    ValidateInitialRun(run);
                }
                WriteAtomicJsonWithoutLock(path, run);
            }
        }

        public TestRunRecord ReadRun(string runId)
        {
            var run = ReadJsonOrThrow<TestRunRecord>(GetRunRecordPath(runId));
            ValidateStoredRun(run, runId);
            return run;
        }

        public bool TryReadRun(string runId, out TestRunRecord run)
        {
            var path = GetRunRecordPath(runId);
            if (!File.Exists(path))
            {
                run = null;
                return false;
            }

            run = ReadJsonOrThrow<TestRunRecord>(path);
            ValidateStoredRun(run, runId);
            return true;
        }

        public void WriteEnvironment(TestRunEnvironmentRecord environment)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (environment.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported environment schema.", nameof(environment));
            ValidateIdentity(environment.run_id, nameof(environment.run_id));
            ValidateEnvironmentPreviewEvidence(environment);
            RequireRunDirectory(environment.run_id);
            var path = GetEnvironmentPath(environment.run_id);
            lock (FileGate)
            {
                if (File.Exists(path))
                {
                    var existing = ReadJsonOrThrowWithoutLock<TestRunEnvironmentRecord>(path);
                    ValidateStoredEnvironment(existing, environment.run_id);
                    ValidateEnvironmentUpdate(existing, environment);
                }
                WriteAtomicJsonWithoutLock(path, environment);
            }
        }

        public TestRunEnvironmentRecord ReadEnvironment(string runId)
        {
            var environment = ReadJsonOrThrow<TestRunEnvironmentRecord>(GetEnvironmentPath(runId));
            ValidateStoredEnvironment(environment, runId);
            return environment;
        }

        public bool TryReadEnvironment(string runId, out TestRunEnvironmentRecord environment)
        {
            var path = GetEnvironmentPath(runId);
            if (!File.Exists(path))
            {
                environment = null;
                return false;
            }

            environment = ReadJsonOrThrow<TestRunEnvironmentRecord>(path);
            ValidateStoredEnvironment(environment, runId);
            return true;
        }

        public void WriteActive(TestRunPointer pointer)
        {
            ValidatePointer(pointer);
            WriteAtomicJson(ActivePath, pointer);
        }

        public void WriteLatest(TestRunPointer pointer)
        {
            ValidatePointer(pointer);
            WriteAtomicJson(LatestPath, pointer);
        }

        public TestRunPointer ReadActive()
        {
            var pointer = ReadJsonOrThrow<TestRunPointer>(ActivePath);
            ValidateStoredPointer(pointer, "active.json");
            return pointer;
        }

        public TestRunPointer ReadLatest()
        {
            var pointer = ReadJsonOrThrow<TestRunPointer>(LatestPath);
            ValidateStoredPointer(pointer, "latest.json");
            return pointer;
        }

        public bool TryReadActive(out TestRunPointer pointer) =>
            TryReadPointer(ActivePath, "active.json", out pointer);

        public bool TryReadLatest(out TestRunPointer pointer) =>
            TryReadPointer(LatestPath, "latest.json", out pointer);

        /// <summary>
        /// Moves an unreadable active pointer aside without changing its bytes. A
        /// valid pointer is never moved. Callers must still prove UTF inactivity and
        /// scene safety before replacing the now-missing pointer.
        /// </summary>
        public string QuarantineCorruptActive()
        {
            lock (FileGate)
            {
                if (!File.Exists(ActivePath)) return "";
                try
                {
                    var pointer = ReadJsonOrThrowWithoutLock<TestRunPointer>(ActivePath);
                    ValidateStoredPointer(pointer, "active.json");
                    return "";
                }
                catch (TestRunStoreException)
                {
                    Directory.CreateDirectory(QuarantinePath);
                    var quarantine = Path.Combine(
                        QuarantinePath,
                        "active." + DateTime.UtcNow.Ticks + "." +
                        Guid.NewGuid().ToString("N") + ".json");
                    try
                    {
                        File.Move(ActivePath, quarantine);
                        return quarantine;
                    }
                    catch (Exception e)
                    {
                        throw new TestRunStoreException(
                            $"Could not quarantine corrupt active pointer '{ActivePath}'.", e);
                    }
                }
            }
        }

        /// <summary>
        /// Returns valid durable runs newest-first, then by run id. A missing or
        /// corrupt run.json is surfaced instead of silently disappearing from the
        /// operations view.
        /// </summary>
        public string[] ListRunIds()
        {
            var runsPath = Path.Combine(RootPath, "runs");
            if (!Directory.Exists(runsPath)) return Array.Empty<string>();

            var records = new List<KeyValuePair<string, string>>();
            foreach (var directory in Directory.GetDirectories(runsPath))
            {
                var runId = Path.GetFileName(directory);
                try
                {
                    ValidateIdentity(runId, nameof(runId));
                    var createdUtc = "";
                    try
                    {
                        createdUtc = ReadRun(runId).created_utc ?? "";
                    }
                    catch (TestRunStoreException)
                    {
                        // Keep corrupt runs addressable so list/recovery can expose
                        // the per-run protocol issue without hiding healthy history.
                    }
                    records.Add(new KeyValuePair<string, string>(runId, createdUtc));
                }
                catch (ArgumentException)
                {
                    // An unsafe directory name cannot be represented as a run id.
                }
            }

            return records
                .OrderByDescending(run => run.Value, StringComparer.Ordinal)
                .ThenBy(run => run.Key, StringComparer.Ordinal)
                .Select(run => run.Key)
                .ToArray();
        }

        public void AppendExpectedTest(string runId, TestLeafManifestEntry leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            if (leaf.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported manifest schema.", nameof(leaf));
            ValidateIdentity(runId, nameof(runId));
            if (!string.Equals(runId, leaf.run_id, StringComparison.Ordinal))
                throw new ArgumentException("Manifest run_id does not match the target run.", nameof(leaf));
            if (string.IsNullOrWhiteSpace(leaf.unique_name))
                throw new ArgumentException("A manifest leaf requires unique_name.", nameof(leaf));

            RequireRunDirectory(runId);
            AppendJsonLine(GetExpectedTestsPath(runId), leaf);
        }

        /// <summary>
        /// Writes the complete RunStarted manifest in one atomic transaction. This
        /// keeps large suites linear while preserving the append API for recovery
        /// tests and deliberately incremental producers.
        /// </summary>
        public void WriteExpectedTests(
            string runId,
            IEnumerable<TestLeafManifestEntry> leaves)
        {
            ValidateIdentity(runId, nameof(runId));
            if (leaves == null) throw new ArgumentNullException(nameof(leaves));
            RequireRunDirectory(runId);

            var text = new StringBuilder();
            foreach (var leaf in leaves)
            {
                if (leaf == null || leaf.schema_version != TestRunProtocol.SchemaVersion)
                    throw new ArgumentException("Manifest contains an invalid record.", nameof(leaves));
                if (!string.Equals(runId, leaf.run_id, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(leaf.unique_name))
                    throw new ArgumentException(
                        "Manifest record identity does not match the run.", nameof(leaves));
                text.Append(JsonUtility.ToJson(leaf, false)).Append('\n');
            }

            var path = GetExpectedTestsPath(runId);
            var content = text.ToString();
            lock (FileGate)
            {
                if (File.Exists(path))
                {
                    var existing = File.ReadAllText(path, StrictUtf8);
                    if (string.Equals(existing, content, StringComparison.Ordinal)) return;
                    throw new TestRunStoreException(
                        $"Manifest for run '{runId}' is immutable. Existing evidence was preserved.");
                }
                WriteAtomicTextWithoutLock(path, content);
            }
        }

        /// <summary>
        /// Makes manifest completion explicit, including the valid zero-test case.
        /// A crash between file creation and the seal event remains detectably
        /// incomplete instead of looking like a complete empty run.
        /// </summary>
        public void SealManifest(string runId, TestRunEvent sealEvent)
        {
            if (sealEvent == null) throw new ArgumentNullException(nameof(sealEvent));
            if (sealEvent.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported event schema.", nameof(sealEvent));
            ValidateIdentity(runId, nameof(runId));
            if (!string.Equals(runId, sealEvent.run_id, StringComparison.Ordinal))
                throw new ArgumentException("Manifest seal run_id does not match the target run.",
                    nameof(sealEvent));
            if (sealEvent.event_type != TestRunProtocol.EventType.ManifestSealed)
                throw new ArgumentException("SealManifest requires a manifest_sealed event.",
                    nameof(sealEvent));
            if (sealEvent.expected_count < 0)
                throw new ArgumentException("Manifest expected_count cannot be negative.",
                    nameof(sealEvent));

            RequireRunDirectory(runId);
            var manifestPath = GetExpectedTestsPath(runId);
            lock (FileGate)
            {
                if (!File.Exists(manifestPath))
                    WriteAtomicTextWithoutLock(manifestPath, "");
            }
            AppendEvent(runId, sealEvent);
        }

        public void AppendEvent(string runId, TestRunEvent testEvent)
        {
            if (testEvent == null) throw new ArgumentNullException(nameof(testEvent));
            if (testEvent.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported event schema.", nameof(testEvent));
            ValidateIdentity(runId, nameof(runId));
            if (!string.Equals(runId, testEvent.run_id, StringComparison.Ordinal))
                throw new ArgumentException("Event run_id does not match the target run.", nameof(testEvent));
            if (string.IsNullOrWhiteSpace(testEvent.event_type))
                throw new ArgumentException("An event requires event_type.", nameof(testEvent));
            if (string.IsNullOrWhiteSpace(testEvent.event_id))
                testEvent.event_id = Guid.NewGuid().ToString("N");

            RequireRunDirectory(runId);
            var forceToDisk =
                testEvent.event_type != TestRunProtocol.EventType.TestStarted &&
                testEvent.event_type != TestRunProtocol.EventType.TestFinished;
            AppendJsonLine(GetEventsPath(runId), testEvent, forceToDisk);
        }

        public void WriteUtfResults(string runId, string xml)
        {
            ValidateIdentity(runId, nameof(runId));
            RequireRunDirectory(runId);
            WriteAtomicText(GetUtfResultsPath(runId), xml ?? "");
        }

        public void WriteSummary(TestRunSummary summary)
        {
            if (summary == null) throw new ArgumentNullException(nameof(summary));
            if (summary.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported summary schema.", nameof(summary));
            ValidateIdentity(summary.run_id, nameof(summary.run_id));
            RequireRunDirectory(summary.run_id);
            WriteAtomicJson(GetSummaryPath(summary.run_id), summary);
        }

        public TestRunSummary ReadSummary(string runId) =>
            ReadAndValidateSummary(runId);

        public bool TryReadSummary(string runId, out TestRunSummary summary)
        {
            var path = GetSummaryPath(runId);
            if (!File.Exists(path))
            {
                summary = null;
                return false;
            }
            summary = ReadJsonOrThrow<TestRunSummary>(path);
            ValidateStoredSummary(summary, runId);
            return true;
        }

        public bool IsSummaryCurrent(string runId, TestRunSummary summary)
        {
            if (summary == null) return false;
            if (summary.schema_version != TestRunProtocol.SchemaVersion ||
                !string.Equals(summary.run_id, runId, StringComparison.Ordinal))
                return false;
            var run = new FileInfo(GetRunRecordPath(runId));
            var environment = new FileInfo(GetEnvironmentPath(runId));
            var manifest = new FileInfo(GetExpectedTestsPath(runId));
            var events = new FileInfo(GetEventsPath(runId));
            return EvidenceLength(run) == summary.run_evidence_bytes &&
                   EvidenceLength(environment) == summary.environment_evidence_bytes &&
                   EvidenceLength(manifest) == summary.manifest_evidence_bytes &&
                   EvidenceLength(events) == summary.event_evidence_bytes &&
                   EvidenceWriteTicks(run) == summary.run_write_utc_ticks &&
                   EvidenceWriteTicks(environment) == summary.environment_write_utc_ticks &&
                   EvidenceWriteTicks(manifest) == summary.manifest_write_utc_ticks &&
                   EvidenceWriteTicks(events) == summary.event_write_utc_ticks;
        }

        public TestRunJournal ReadJournal(string runId)
        {
            ValidateIdentity(runId, nameof(runId));
            var runPath = GetRunRecordPath(runId);
            var manifestPath = GetExpectedTestsPath(runId);
            var eventsPath = GetEventsPath(runId);
            var issues = new List<TestProtocolIssue>();
            var journal = new TestRunJournal
            {
                run_record_exists = File.Exists(runPath),
                manifest_file_exists = File.Exists(manifestPath),
                events_file_exists = File.Exists(eventsPath)
            };

            lock (FileGate)
            {
                if (journal.run_record_exists)
                    journal.run = TryReadJson<TestRunRecord>(runPath, "RUN_RECORD_CORRUPT", issues);
                else
                    issues.Add(Issue("RUN_RECORD_MISSING", runPath, 0, runId,
                        "The durable run record does not exist."));

                journal.manifest = journal.manifest_file_exists
                    ? ReadJsonLines<TestLeafManifestEntry>(
                        manifestPath, "MANIFEST_LINE_CORRUPT", issues).ToArray()
                    : Array.Empty<TestLeafManifestEntry>();

                journal.events = journal.events_file_exists
                    ? ReadJsonLines<TestRunEvent>(
                        eventsPath, "EVENT_LINE_CORRUPT", issues).ToArray()
                    : Array.Empty<TestRunEvent>();
            }

            journal.issues = issues.ToArray();
            return journal;
        }

        public TestRunSummary Reconcile(string runId, bool persistSummary = true)
        {
            var summary = TestRunReconciler.Reconcile(runId, ReadJournal(runId));
            StampEvidence(summary);
            if (persistSummary) WriteSummary(summary);
            return summary;
        }

        private void StampEvidence(TestRunSummary summary)
        {
            var run = new FileInfo(GetRunRecordPath(summary.run_id));
            var environment = new FileInfo(GetEnvironmentPath(summary.run_id));
            var manifest = new FileInfo(GetExpectedTestsPath(summary.run_id));
            var events = new FileInfo(GetEventsPath(summary.run_id));
            summary.run_evidence_bytes = EvidenceLength(run);
            summary.environment_evidence_bytes = EvidenceLength(environment);
            summary.manifest_evidence_bytes = EvidenceLength(manifest);
            summary.event_evidence_bytes = EvidenceLength(events);
            summary.run_write_utc_ticks = EvidenceWriteTicks(run);
            summary.environment_write_utc_ticks = EvidenceWriteTicks(environment);
            summary.manifest_write_utc_ticks = EvidenceWriteTicks(manifest);
            summary.event_write_utc_ticks = EvidenceWriteTicks(events);
        }

        private TestRunSummary ReadAndValidateSummary(string runId)
        {
            var summary = ReadJsonOrThrow<TestRunSummary>(GetSummaryPath(runId));
            ValidateStoredSummary(summary, runId);
            return summary;
        }

        private static long EvidenceLength(FileInfo file) => file.Exists ? file.Length : -1L;
        private static long EvidenceWriteTicks(FileInfo file) =>
            file.Exists ? file.LastWriteTimeUtc.Ticks : 0L;

        private void RequireRunDirectory(string runId)
        {
            var directory = GetRunDirectory(runId);
            if (!Directory.Exists(directory))
                throw new TestRunStoreException(
                    $"Run '{runId}' has no durable directory. Write run.json before evidence.");
        }

        private static void ValidatePointer(TestRunPointer pointer)
        {
            if (pointer == null) throw new ArgumentNullException(nameof(pointer));
            if (pointer.schema_version != TestRunProtocol.SchemaVersion)
                throw new ArgumentException("Unsupported pointer schema.", nameof(pointer));
            ValidateIdentity(pointer.run_id, nameof(pointer.run_id));
            if (!string.IsNullOrEmpty(pointer.request_id))
                ValidateIdentity(pointer.request_id, nameof(pointer.request_id));
        }

        private static bool TryReadPointer(
            string path, string pointerName, out TestRunPointer pointer)
        {
            if (!File.Exists(path))
            {
                pointer = null;
                return false;
            }

            pointer = ReadJsonOrThrow<TestRunPointer>(path);
            ValidateStoredPointer(pointer, pointerName);
            return true;
        }

        private static void ValidateIdentity(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 200 ||
                value == "." || value == "..")
                throw new ArgumentException("Identity must contain 1-200 safe characters.", parameterName);

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.')
                    continue;
                throw new ArgumentException(
                    "Identity may contain only ASCII letters, digits, '.', '_' and '-'.",
                    parameterName);
            }
        }

        private static void ValidateStoredRequest(TestRunRequestRecord request, string expectedRequestId)
        {
            if (request == null || request.schema_version != TestRunProtocol.SchemaVersion ||
                !string.Equals(request.request_id, expectedRequestId, StringComparison.Ordinal))
                throw new TestRunStoreException(
                    $"Durable request '{expectedRequestId}' has invalid identity or schema. It was preserved.");
            try
            {
                ValidateIdentity(request.run_id, nameof(request.run_id));
            }
            catch (ArgumentException e)
            {
                throw new TestRunStoreException(
                    $"Durable request '{expectedRequestId}' has an invalid run_id. It was preserved.", e);
            }
        }

        private static void ValidateStoredRun(TestRunRecord run, string expectedRunId)
        {
            if (run == null || run.schema_version != TestRunProtocol.SchemaVersion ||
                !string.Equals(run.run_id, expectedRunId, StringComparison.Ordinal))
                throw new TestRunStoreException(
                    $"Durable run '{expectedRunId}' has invalid identity or schema. It was preserved.");
        }

        private static void ValidateRequestUpdate(
            TestRunRequestRecord existing,
            TestRunRequestRecord candidate)
        {
            RequireSame(existing.created_utc, candidate.created_utc,
                "request created_utc");
            if (existing.intent_complete != candidate.intent_complete)
                throw new TestRunStoreException(
                    "The durable request intent_complete marker is immutable.");
            RequireSame(existing.mode, candidate.mode, "request mode");
            RequireSame(existing.group, candidate.group, "request group");
            RequireSame(existing.filter, candidate.filter, "request filter");
            RequireSetOnce(existing.acknowledged_utc, candidate.acknowledged_utc,
                "request acknowledged_utc");
            RequireMonotonicLifecycle(existing.state, candidate.state, "request");
        }

        private static void ValidateRunUpdate(TestRunRecord existing, TestRunRecord candidate)
        {
            RequireSame(existing.source, candidate.source, "run source");
            RequireSame(existing.created_utc, candidate.created_utc, "run created_utc");
            RequireSame(existing.mode, candidate.mode, "run mode");
            RequireSame(existing.group, candidate.group, "run group");
            RequireSame(existing.filter, candidate.filter, "run filter");
            RequireSetOnce(existing.utf_guid, candidate.utf_guid, "run utf_guid");
            RequireSetOnce(existing.project_identity, candidate.project_identity,
                "run project_identity");
            RequireSetOnce(existing.editor_process_identity, candidate.editor_process_identity,
                "run editor_process_identity");
            RequireSetOnce(existing.editor_session_id, candidate.editor_session_id,
                "run editor_session_id");
            RequireSetOnce(existing.build_fingerprint, candidate.build_fingerprint,
                "run build_fingerprint");
            RequireSetOnce(existing.utf_version, candidate.utf_version, "run utf_version");
            RequireSetOnce(existing.assembly_path, candidate.assembly_path, "run assembly_path");
            RequireSetOnce(existing.source_path, candidate.source_path, "run source_path");
            RequireSetOnce(existing.assembly_write_utc, candidate.assembly_write_utc,
                "run assembly_write_utc");
            RequireSetOnce(existing.source_write_utc, candidate.source_write_utc,
                "run source_write_utc");
            RequireSetOnce(existing.build_error, candidate.build_error, "run build_error");
            RequireSetOnce(existing.dispatched_utc, candidate.dispatched_utc,
                "run dispatched_utc");
            RequireSetOnce(existing.started_utc, candidate.started_utc, "run started_utc");
            RequireSetOnce(existing.finished_utc, candidate.finished_utc, "run finished_utc");
            RequireMonotonicLifecycle(existing.lifecycle, candidate.lifecycle, "run");
            if (existing.build_coherent && !candidate.build_coherent)
                throw new TestRunStoreException(
                    $"Run '{existing.run_id}' cannot lose verified build coherence.");

            ValidateOutcomeUpdate(existing, candidate);
        }

        private static void ValidateInitialRun(TestRunRecord run)
        {
            var terminal = string.Equals(run.lifecycle,
                TestRunProtocol.Lifecycle.Terminal, StringComparison.Ordinal);
            var outcome = run.outcome ?? "";
            if (!terminal && !string.IsNullOrEmpty(outcome))
                throw new TestRunStoreException(
                    $"Run '{run.run_id}' cannot set outcome before terminalization.");
            if (terminal && !IsTerminalOutcome(outcome))
                throw new TestRunStoreException(
                    $"Run '{run.run_id}' requires a valid outcome when created terminal.");
        }

        private static void ValidateOutcomeUpdate(
            TestRunRecord existing,
            TestRunRecord candidate)
        {
            var oldOutcome = existing.outcome ?? "";
            var newOutcome = candidate.outcome ?? "";
            var existingTerminal = string.Equals(existing.lifecycle,
                TestRunProtocol.Lifecycle.Terminal, StringComparison.Ordinal);
            var candidateTerminal = string.Equals(candidate.lifecycle,
                TestRunProtocol.Lifecycle.Terminal, StringComparison.Ordinal);

            if (existingTerminal && !string.Equals(oldOutcome, newOutcome,
                    StringComparison.Ordinal))
                throw new TestRunStoreException(
                    $"Run '{existing.run_id}' terminal outcome is immutable.");

            if (!candidateTerminal)
            {
                if (string.IsNullOrEmpty(oldOutcome) && !string.IsNullOrEmpty(newOutcome))
                    throw new TestRunStoreException(
                        $"Run '{existing.run_id}' cannot set outcome before terminalization.");
                if (!string.IsNullOrEmpty(oldOutcome) &&
                    !string.Equals(oldOutcome, newOutcome, StringComparison.Ordinal))
                    throw new TestRunStoreException(
                        $"Run '{existing.run_id}' legacy provisional outcome cannot change before terminalization.");
                return;
            }

            if (!IsTerminalOutcome(newOutcome))
                throw new TestRunStoreException(
                    $"Run '{existing.run_id}' requires a valid outcome when it becomes terminal.");
        }

        private static bool IsTerminalOutcome(string outcome)
        {
            switch (outcome)
            {
                case TestRunProtocol.RunOutcome.Passed:
                case TestRunProtocol.RunOutcome.Failed:
                case TestRunProtocol.RunOutcome.Cancelled:
                case TestRunProtocol.RunOutcome.Incomplete:
                case TestRunProtocol.RunOutcome.Invalid:
                case TestRunProtocol.RunOutcome.DispatchFailed:
                case TestRunProtocol.RunOutcome.DirtySceneBlocked:
                    return true;
                default:
                    return false;
            }
        }

        private static void RequireSame(string existing, string candidate, string label)
        {
            if (!string.Equals(existing ?? "", candidate ?? "", StringComparison.Ordinal))
                throw new TestRunStoreException($"The durable {label} is immutable.");
        }

        private static void RequireSetOnce(string existing, string candidate, string label)
        {
            existing = existing ?? "";
            candidate = candidate ?? "";
            if (!string.IsNullOrEmpty(existing) &&
                !string.Equals(existing, candidate, StringComparison.Ordinal))
                throw new TestRunStoreException($"The durable {label} is immutable once set.");
        }

        private static void RequireMonotonicLifecycle(
            string existing,
            string candidate,
            string label)
        {
            var existingRank = LifecycleRank(existing);
            var candidateRank = LifecycleRank(candidate);
            if (candidateRank < existingRank ||
                (existingRank == LifecycleRank(TestRunProtocol.Lifecycle.Terminal) &&
                 candidateRank != existingRank))
                throw new TestRunStoreException(
                    $"The durable {label} lifecycle cannot move backward from '{existing}' to '{candidate}'.");
        }

        private static int LifecycleRank(string lifecycle)
        {
            switch (lifecycle)
            {
                case TestRunProtocol.Lifecycle.Prepared: return 0;
                case TestRunProtocol.Lifecycle.Dispatched: return 1;
                case TestRunProtocol.Lifecycle.Running: return 2;
                case TestRunProtocol.Lifecycle.Finalizing: return 3;
                case TestRunProtocol.Lifecycle.Terminal: return 4;
                default: return -1;
            }
        }

        private static void ValidateStoredEnvironment(
            TestRunEnvironmentRecord environment, string expectedRunId)
        {
            if (environment == null || environment.schema_version != TestRunProtocol.SchemaVersion ||
                !string.Equals(environment.run_id, expectedRunId, StringComparison.Ordinal))
                throw new TestRunStoreException(
                    $"Durable environment for run '{expectedRunId}' has invalid identity or schema. It was preserved.");
            ValidateEnvironmentPreviewEvidence(environment);
        }

        private static void ValidateEnvironmentPreviewEvidence(
            TestRunEnvironmentRecord environment)
        {
            if (environment.preview_scene_count < 0 ||
                (!environment.preview_scene_baseline_captured &&
                 environment.preview_scene_count != 0))
                throw new TestRunStoreException(
                    $"Durable preview-scene evidence for run '{environment.run_id}' is contradictory.");
        }

        private static void ValidateStoredSummary(
            TestRunSummary summary, string expectedRunId)
        {
            if (summary == null || summary.schema_version != TestRunProtocol.SchemaVersion ||
                !string.Equals(summary.run_id, expectedRunId, StringComparison.Ordinal))
                throw new TestRunStoreException(
                    $"Durable summary for run '{expectedRunId}' has invalid identity or schema. It was preserved.");
        }

        private static void ValidateEnvironmentUpdate(
            TestRunEnvironmentRecord existing, TestRunEnvironmentRecord candidate)
        {
            var baselineChanged = !SequenceEqual(existing.scene_paths, candidate.scene_paths) ||
                                  existing.active_scene_path != candidate.active_scene_path ||
                                  existing.restore_single_untitled != candidate.restore_single_untitled ||
                                  existing.untitled_scene_setup != candidate.untitled_scene_setup ||
                                  existing.owned_scene_path != candidate.owned_scene_path ||
                                  existing.scene_restore_delegated_to_utf !=
                                  candidate.scene_restore_delegated_to_utf ||
                                  existing.preview_scene_baseline_captured !=
                                  candidate.preview_scene_baseline_captured ||
                                  existing.preview_scene_count != candidate.preview_scene_count ||
                                  existing.prepared_utc != candidate.prepared_utc;
            var restorationRewritten = !string.IsNullOrEmpty(existing.restored_utc) &&
                                       existing.restored_utc != candidate.restored_utc;
            if (baselineChanged || restorationRewritten)
                throw new TestRunStoreException(
                    $"Environment baseline for run '{existing.run_id}' is immutable. Existing evidence was preserved.");
        }

        private static bool SequenceEqual(string[] left, string[] right) =>
            (left ?? Array.Empty<string>()).SequenceEqual(right ?? Array.Empty<string>(),
                StringComparer.Ordinal);

        private static void ValidateStoredPointer(TestRunPointer pointer, string pointerName)
        {
            if (pointer == null || pointer.schema_version != TestRunProtocol.SchemaVersion)
                throw new TestRunStoreException(
                    $"Durable pointer '{pointerName}' has an unsupported schema. It was preserved.");
            try
            {
                ValidateIdentity(pointer.run_id, nameof(pointer.run_id));
                if (!string.IsNullOrEmpty(pointer.request_id))
                    ValidateIdentity(pointer.request_id, nameof(pointer.request_id));
            }
            catch (ArgumentException e)
            {
                throw new TestRunStoreException(
                    $"Durable pointer '{pointerName}' has an invalid identity. It was preserved.", e);
            }
        }

        private static T ReadJsonOrThrow<T>(string path) where T : class
        {
            lock (FileGate)
            {
                return ReadJsonOrThrowWithoutLock<T>(path);
            }
        }

        private static T ReadJsonOrThrowWithoutLock<T>(string path) where T : class
        {
            if (!File.Exists(path))
                throw new TestRunStoreException($"Durable protocol file does not exist: {path}");
            try
            {
                var value = JsonUtility.FromJson<T>(File.ReadAllText(path, StrictUtf8));
                if (value == null)
                    throw new TestRunStoreException($"Durable protocol file is empty: {path}");
                return value;
            }
            catch (TestRunStoreException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new TestRunStoreException(
                    $"Could not parse durable protocol file '{path}'. The file was preserved.", e);
            }
        }

        private static T TryReadJson<T>(
            string path,
            string issueCode,
            ICollection<TestProtocolIssue> issues) where T : class
        {
            try
            {
                var value = JsonUtility.FromJson<T>(File.ReadAllText(path, StrictUtf8));
                if (value != null) return value;
                throw new FormatException("JsonUtility returned null.");
            }
            catch (Exception e)
            {
                issues.Add(Issue(issueCode, path, 0, "",
                    $"Could not parse the file; original evidence was preserved. {e.Message}"));
                return null;
            }
        }

        private static List<T> ReadJsonLines<T>(
            string path,
            string issueCode,
            ICollection<TestProtocolIssue> issues) where T : class
        {
            var records = new List<T>();
            var lineNumber = 0;
            using (var stream = new FileStream(
                       path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, StrictUtf8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        issues.Add(Issue(issueCode, path, lineNumber, "",
                            "The journal contains an empty record; original evidence was preserved."));
                        continue;
                    }

                    try
                    {
                        var value = JsonUtility.FromJson<T>(line);
                        if (value == null) throw new FormatException("JsonUtility returned null.");
                        records.Add(value);
                    }
                    catch (Exception e)
                    {
                        issues.Add(Issue(issueCode, path, lineNumber, "",
                            $"Could not parse this journal record; original evidence was preserved. {e.Message}"));
                    }
                }
            }

            return records;
        }

        private static TestProtocolIssue Issue(
            string code, string path, int line, string identity, string message) =>
            new TestProtocolIssue
            {
                code = code,
                severity = TestRunProtocol.IssueSeverity.Error,
                path = path,
                line = line,
                identity = identity,
                message = message
            };

        private static void AppendJsonLine<T>(string path, T value, bool forceToDisk = true)
        {
            var json = JsonUtility.ToJson(value, false);
            lock (FileGate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                try
                {
                    using (var stream = new FileStream(
                               path,
                               FileMode.Append,
                               FileAccess.Write,
                               FileShare.Read,
                               4096,
                               forceToDisk ? FileOptions.WriteThrough : FileOptions.None))
                    using (var writer = new StreamWriter(stream, Utf8WithoutBom))
                    {
                        writer.NewLine = "\n";
                        writer.WriteLine(json);
                        writer.Flush();
                        stream.Flush(forceToDisk);
                    }
                }
                catch (Exception e)
                {
                    throw new TestRunStoreException(
                        $"Could not append durable protocol evidence to '{path}'.", e);
                }
            }
        }

        private static void WriteAtomicJson<T>(string path, T value)
        {
            WriteAtomicText(path, JsonUtility.ToJson(value, true) + "\n");
        }

        private static void WriteAtomicText(string path, string content)
        {
            lock (FileGate)
            {
                WriteAtomicTextWithoutLock(path, content);
            }
        }

        private static void WriteAtomicJsonWithoutLock<T>(string path, T value)
        {
            WriteAtomicTextWithoutLock(path, JsonUtility.ToJson(value, true) + "\n");
        }

        private static void WriteAtomicTextWithoutLock(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, Utf8WithoutBom))
                {
                    writer.Write(content ?? "");
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            catch (Exception e)
            {
                throw new TestRunStoreException(
                    $"Could not atomically write durable protocol file '{path}'.", e);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
