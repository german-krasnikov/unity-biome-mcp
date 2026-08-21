// TDD — TestRunBuildFingerprint.DescribeCompletionMismatch branch coverage.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using NUnit.Framework;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunBuildFingerprintTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static TestRunRecord BaseRecord(string fingerprint = "fp-v1") =>
            new TestRunRecord
            {
                project_identity = "/path/to/project",
                editor_process_identity = "6000.0:12345:999",
                build_fingerprint = fingerprint,
                utf_version = "1.6.0",
                assembly_path = "/Library/ScriptAssemblies/test.dll",
                source_path = "/Assets/Tests/TestScript.cs",
                assembly_write_utc = "2026-01-01T00:00:00Z",
                source_write_utc = "2026-01-01T00:00:00Z",
                build_coherent = true,
            };

        private static TestRunBuildFingerprint BaseCompletion(string fingerprint = "fp-v1") =>
            new TestRunBuildFingerprint
            {
                ProjectIdentity = "/path/to/project",
                EditorProcessIdentity = "6000.0:12345:999",
                Fingerprint = fingerprint,
                UtfVersion = "1.6.0",
                AssemblyPath = "/Library/ScriptAssemblies/test.dll",
                SourcePath = "/Assets/Tests/TestScript.cs",
                AssemblyWriteUtc = "2026-01-01T00:00:00Z",
                SourceWriteUtc = "2026-01-01T00:00:00Z",
                IsCoherent = true,
            };

        // ── Matching fingerprints → no mismatch ───────────────────────────────

        [Test]
        public void DescribeCompletionMismatch_MatchingFingerprints_ReturnsEmpty()
        {
            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(
                BaseRecord(), BaseCompletion());

            Assert.AreEqual("", result, "Identical start/completion must return empty string");
        }

        // ── Null / empty guard branches ────────────────────────────────────────

        [Test]
        public void DescribeCompletionMismatch_NullStarted_ReturnsEmpty()
        {
            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(null, BaseCompletion());

            Assert.AreEqual("", result, "Null started record must return empty (legacy path)");
        }

        [Test]
        public void DescribeCompletionMismatch_EmptyFingerprintInRecord_ReturnsEmpty()
        {
            var started = BaseRecord(fingerprint: ""); // no fingerprint = legacy evidence
            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(started, BaseCompletion());

            Assert.AreEqual("", result, "Empty build_fingerprint means legacy evidence; must return empty");
        }

        [Test]
        public void DescribeCompletionMismatch_NullCompletion_ReturnsMessage()
        {
            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), null);

            StringAssert.Contains("completion", result,
                "Null completion must mention 'completion' in the error message");
        }

        // ── Incoherent completion ─────────────────────────────────────────────

        [Test]
        public void DescribeCompletionMismatch_IncoherentCompletion_ReturnsCoherenceMessage()
        {
            var completion = BaseCompletion();
            completion.IsCoherent = false;
            completion.Error = "compile error";

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("incoherent", result,
                "Incoherent completion must mention 'incoherent'");
        }

        // ── Field mismatch branches ───────────────────────────────────────────

        [Test]
        public void DescribeCompletionMismatch_ProjectIdentityChanged_ReturnsProjectMessage()
        {
            var completion = BaseCompletion();
            completion.ProjectIdentity = "/other/project";

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("project", result,
                "Changed project identity must mention 'project'");
        }

        [Test]
        public void DescribeCompletionMismatch_FingerprintChanged_ReturnsAssemblyMessage()
        {
            var completion = BaseCompletion(fingerprint: "fp-v2");

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("fingerprint", result,
                "Changed fingerprint must mention 'fingerprint'");
        }

        [Test]
        public void DescribeCompletionMismatch_UtfVersionChanged_ReturnsUtfMessage()
        {
            var completion = BaseCompletion();
            completion.UtfVersion = "2.0.0";

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("version", result,
                "Changed UTF version must mention 'version'");
        }

        [Test]
        public void DescribeCompletionMismatch_AssemblyPathChanged_ReturnsAssemblyPathMessage()
        {
            var completion = BaseCompletion();
            completion.AssemblyPath = "/Library/ScriptAssemblies/other.dll";

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("assembly", result,
                "Changed assembly path must mention 'assembly'");
        }

        [Test]
        public void DescribeCompletionMismatch_AssemblyTimestampChanged_ReturnsTimestampMessage()
        {
            var completion = BaseCompletion();
            completion.AssemblyWriteUtc = "2099-01-01T00:00:00Z";

            var result = TestRunBuildFingerprint.DescribeCompletionMismatch(BaseRecord(), completion);

            StringAssert.Contains("timestamp", result,
                "Changed assembly timestamp must mention 'timestamp'");
        }
    }
}
