// TDD: TestRunner core-path tests — EditMode only, no UNITY_INCLUDE_TESTS required.
// Covers: registration, stale-timeout detection, concurrent-run guard,
//         GetResults state machine, and result format contract.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunnerTests
    {
        // ── Reflection helpers ────────────────────────────────────────────────

        private static FieldInfo IsRunningField() =>
            typeof(TestRunner).GetField("_isRunning",
                BindingFlags.Static | BindingFlags.NonPublic);

        private const string KeyPending   = "UnityMCP_tests_pending";
        private const string KeyResults   = "UnityMCP_test_results";
        private const string KeyStartTime = "UnityMCP_tests_start";

        [SetUp]
        public void SetUp()
        {
            // Reset static state before each test
            IsRunningField()?.SetValue(null, 0);
            SessionState.SetBool(KeyPending, false);
            SessionState.SetString(KeyResults, "");
            SessionState.SetFloat(KeyStartTime, 0f);
            // Reset seams to production defaults
            TestRunner.GetIsCompiling    = () => EditorApplication.isCompiling;
            TestRunner.GetIsCompileClean = () => SyncHelper.IsCompileClean;
            TestRunner.GetTimeSinceStartup = () => EditorApplication.timeSinceStartup;
        }

        [TearDown]
        public void TearDown() => SetUp(); // symmetric cleanup

        // ── 1. Registration ───────────────────────────────────────────────────

        [Test]
        public void TestRunner_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("run_tests"),
                "run_tests must be registered in CommandRegistry");

        // ── 2. Stale-timeout detection ────────────────────────────────────────

        [Test]
        public void TestRunner_StaleTimeout_DetectsExpiredRun()
        {
            // Inject fake uptime via seam — test always runs regardless of actual Editor uptime
            TestRunner.GetTimeSinceStartup = () => 1000.0;
            try
            {
                // Start time = 1000 - 700 = 300, so elapsed = 700 > StaleTimeoutSec(600)
                const float fakeStart = 300f;
                SessionState.SetBool(KeyPending, true);
                SessionState.SetFloat(KeyStartTime, fakeStart);

                var result = TestRunner.GetResults();

                Assert.AreEqual("none (stale pending cleared)", result,
                    "Expired pending run must be cleared and reported as stale");
                Assert.IsFalse(SessionState.GetBool(KeyPending, true),
                    "KeyPending must be reset after stale detection");
            }
            finally
            {
                TestRunner.GetTimeSinceStartup = () => EditorApplication.timeSinceStartup;
            }
        }

        [Test]
        public void TestRunner_ActiveRun_ReturnsPending()
        {
            // Run started just now — should NOT be considered stale
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);

            Assert.AreEqual("pending", TestRunner.GetResults());
        }

        // ── 3. Filter format — field exists on Execute signature ──────────────

        [Test]
        public void TestRunner_FilterParam_ExistsOnExecuteMethod()
        {
            // Verify the public Execute method accepts a filter parameter.
            // The actual pipe-split is internal; this guards against signature regressions.
            var method = typeof(TestRunner).GetMethod("Execute",
                BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(method, "Execute method must be public static");

            var parms = method.GetParameters();
            var filterParam = System.Array.Find(parms, p => p.Name == "filter");
            Assert.IsNotNull(filterParam, "Execute must have a 'filter' parameter");
            Assert.AreEqual(typeof(string), filterParam.ParameterType);
        }

        // ── 4. Concurrent run guard — via _isRunning reflection ───────────────

        [Test]
        public void TestRunner_ConcurrentRun_IsRunningFieldExists_AndIsInt()
        {
            var field = IsRunningField();
            Assert.IsNotNull(field, "_isRunning field must exist");
            Assert.AreEqual(typeof(int), field.FieldType,
                "_isRunning must be int (used with Interlocked.CompareExchange)");
        }

        [Test]
        public void TestRunner_ConcurrentRun_WhenRunningIsOne_GetResultsReturnsPending()
        {
            // Simulate _isRunning=1 with a fresh start time → GetResults reports pending
            IsRunningField().SetValue(null, 1);
            SessionState.SetBool(KeyPending, true);
            SessionState.SetFloat(KeyStartTime, (float)EditorApplication.timeSinceStartup);

            Assert.AreEqual("pending", TestRunner.GetResults(),
                "While _isRunning=1 and pending not stale, GetResults must return 'pending'");
        }

        // ── 5. Result format contract ─────────────────────────────────────────

        [Test]
        public void TestRunner_ResultFormat_IncludesPassCount()
        {
            const string stored = "5 tests: 4 passed, 1 FAILED (12.3s)\nFAIL SomeTest (0.01s)";
            SessionState.SetString(KeyResults, stored);
            SessionState.SetBool(KeyPending, false);

            var result = TestRunner.GetResults();

            StringAssert.Contains("passed", result, "Result must contain 'passed'");
            StringAssert.Contains("FAILED", result, "Result must contain 'FAILED'");
        }

        [Test]
        public void TestRunner_GetResults_WhenNoRun_ReturnsNone()
        {
            // Fresh state — no results stored, not pending
            Assert.AreEqual("none", TestRunner.GetResults());
        }

        [Test]
        public void TestRunner_GetResults_AfterResults_ReturnsStoredString()
        {
            const string fakeResult = "10 tests: 10 passed (2.0s)";
            SessionState.SetString(KeyResults, fakeResult);
            SessionState.SetBool(KeyPending, false);

            Assert.AreEqual(fakeResult, TestRunner.GetResults());
        }

        // ── 6. get_test_count registration ───────────────────────────────────

        [Test]
        public void GetTestCount_IsRegistered()
            => Assert.IsTrue(CommandRegistry.IsRegistered("get_test_count"),
                "get_test_count must be registered in CommandRegistry");

        [Test]
        public void GetTestCount_IsAllowedDuringCompile()
            => Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("get_test_count"),
                "get_test_count must be allowed during compile");

        // ── 7. Gap-window guard — GetIsCompileClean seam ─────────────────────

        [Test]
        public void TestRunner_Execute_RejectsWhenCompileNotClean()
        {
            TestRunner.GetIsCompiling    = () => false;  // pass existing guard
            TestRunner.GetIsCompileClean = () => false;  // gap window active
            string captured = null;
            TestRunner.Execute("EditMode", s => captured = s);
            Assert.IsNotNull(captured, "onComplete must be called");
            StringAssert.Contains("domain reload pending", captured,
                "Must reject with 'domain reload pending' message");
            Assert.AreEqual(0, (int)IsRunningField().GetValue(null),
                "_isRunning must remain 0 — never entered the run");
        }

        [Test]
        public void TestRunner_Execute_ProceedsWhenCompileClean()
        {
            TestRunner.GetIsCompiling    = () => false;
            TestRunner.GetIsCompileClean = () => true;
            TestRunner.GetTimeSinceStartup = () => 0.0;  // elapsed = 0 < 120 → no stale clear
            IsRunningField().SetValue(null, 1);           // force concurrent-run guard next
            string captured = null;
            TestRunner.Execute("EditMode", s => captured = s);
            StringAssert.DoesNotContain("domain reload pending", captured,
                "compile-clean guard must have been passed");
            StringAssert.Contains("already in progress", captured,
                "must reach and hit the concurrent-run guard");
        }

        [Test]
        public void TestRunner_Execute_DefaultCompileCleanIsTrue()
        {
            SyncHelper.ResetForTest(); // CleanKey erased → GetBool returns default=true
            Assert.IsTrue(SyncHelper.IsCompileClean,
                "IsCompileClean default must be true on cold start");
            Assert.IsNotNull(TestRunner.GetIsCompileClean,
                "GetIsCompileClean seam must exist");
            Assert.IsTrue(TestRunner.GetIsCompileClean(),
                "Default seam must return SyncHelper.IsCompileClean which defaults to true");
        }

        // ── 8. FinishRun (C7a, review sprint v0.70) ──────────────────────────
        // Extracted from CommandRouter.AsyncRunTests' completion callback — parses the raw
        // Execute() result into (ok, text), stripping the "Error:" prefix on failure.

        private static FieldInfo UndoCurrentGroupField() =>
            typeof(UndoGroupHelper).GetField("_currentGroup",
                BindingFlags.Static | BindingFlags.NonPublic);

        [Test]
        public void FinishRun_ErrorPrefixedResult_ReturnsNotOkWithMessage()
        {
            var (ok, text) = TestRunner.FinishRun("Error: boom");
            Assert.IsFalse(ok);
            Assert.AreEqual("boom", text);
        }

        [Test]
        public void FinishRun_SuccessResult_ReturnsOkWithText()
        {
            var (ok, text) = TestRunner.FinishRun("5 tests: 5 passed (1.0s)");
            Assert.IsTrue(ok);
            Assert.AreEqual("5 tests: 5 passed (1.0s)", text);
        }

        [TestCase("Error: boom")]
        [TestCase("5 tests: 5 passed (1.0s)")]
        public void FinishRun_AlwaysEndsUndoGroup_RegardlessOfBranch(string result)
        {
            UndoGroupHelper.BeginGroup("finish_run_test");
            var field = UndoCurrentGroupField();
            Assert.GreaterOrEqual((int)field.GetValue(null), 0,
                "sanity: undo group must be open before FinishRun runs");

            TestRunner.FinishRun(result);

            Assert.AreEqual(-1, (int)field.GetValue(null),
                "FinishRun must call UndoGroupHelper.EndGroup() regardless of ok/error branch");
        }
    }
}
