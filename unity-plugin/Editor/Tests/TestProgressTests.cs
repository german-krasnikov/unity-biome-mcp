// TDD: TestRunner progress tracking + result persistence tests.
// Covers: GetProgress() state machine, ETA calculation, TestResultPersistence roundtrip,
//         and ResetOnReload result restoration.
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestProgressTests
    {
        private TempDirScope _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = new TempDirScope("mcp_progress_test");
            SessionState.SetBool(TestRunner.KeyPending, false);
            SessionState.SetString(TestRunner.KeyProgress, "");
            SessionState.SetString(TestRunner.KeyResults, "");
            TestRunner.TestResultPersistence.FilePathOverride =
                System.IO.Path.Combine(_tempDir.Path, "test-results.txt");
        }

        [TearDown]
        public void TearDown()
        {
            TestRunner.TestResultPersistence.FilePathOverride = null;
            _tempDir.Dispose();
        }

        // ── GetProgress state machine ─────────────────────────────────────────

        [Test]
        public void GetProgress_NoTestRunning_ReturnsIdle()
        {
            SessionState.SetBool(TestRunner.KeyPending, false);

            Assert.AreEqual("idle", TestRunner.GetProgress());
        }

        [Test]
        public void GetProgress_RunningNoProgress_ReturnsPendingNoProgressYet()
        {
            SessionState.SetBool(TestRunner.KeyPending, true);
            SessionState.SetString(TestRunner.KeyProgress, "");

            Assert.AreEqual("pending|no-progress-yet", TestRunner.GetProgress());
        }

        [Test]
        public void GetProgress_Running_ReturnsProgressString()
        {
            SessionState.SetBool(TestRunner.KeyPending, true);
            SessionState.SetString(TestRunner.KeyProgress, "10|9|1|0|100|5.0");

            var result = TestRunner.GetProgress();

            StringAssert.StartsWith("running|", result);
            StringAssert.Contains("10|9|1|0|100|5.0", result);
        }

        [Test]
        public void GetProgress_CalculatesEta()
        {
            // rate = 5.0s / 10 tests = 0.5s/test; remaining = 90 * 0.5 = 45s
            SessionState.SetBool(TestRunner.KeyPending, true);
            SessionState.SetString(TestRunner.KeyProgress, "10|9|1|0|100|5.0");

            var result = TestRunner.GetProgress();

            StringAssert.Contains("eta=45s", result);
        }

        // ── TestResultPersistence ─────────────────────────────────────────────

        [Test]
        public void TestResultPersistence_SaveAndLoad_Roundtrip()
        {
            const string data = "42 tests: 41 passed, 1 FAILED (3.7s)";

            TestRunner.TestResultPersistence.Save(data);
            var loaded = TestRunner.TestResultPersistence.Load();

            Assert.AreEqual(data, loaded);
        }

        // ── ResetOnReload restore ─────────────────────────────────────────────

        [Test]
        public void ResetOnReload_RestoresPersistedResults()
        {
            const string data = "5 tests: 5 passed (1.2s)";
            TestRunner.TestResultPersistence.Save(data);
            SessionState.SetString(TestRunner.KeyResults, ""); // simulate domain reload clearing volatile state

            TestRunner.RestorePersistedResults();

            Assert.AreEqual(data, SessionState.GetString(TestRunner.KeyResults, ""));
        }
    }
}
