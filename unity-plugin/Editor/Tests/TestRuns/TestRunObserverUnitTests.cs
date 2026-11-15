// TDD — TestRunObserver static methods: edge cases not covered by TestRunnerTests.cs.
// Tasks 3-5: RootIdentityMatches null/mode-empty branches, ShouldPrepareManagedEnvironment
// null, MapLeafOutcome null adaptor, ResultLabel private method (via reflection).
using System.Reflection;
using NUnit.Framework;
using UnityEditor.TestTools.TestRunner.Api;
using UnityMCP.Editor.TestRuns;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class TestRunObserverUnitTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── RootIdentityMatches edge cases ──

        [Test]
        public void RootIdentityMatches_RunNull_ReturnsFalse()
        {
            var started = new TestRunEvent { unique_name = "root", full_name = "Suite" };
            var result = TestRunObserver.RootIdentityMatches(null, started, "root", "Suite", "EditMode");
            Assert.IsFalse(result);
        }

        [Test]
        public void RootIdentityMatches_StartedNull_ReturnsFalse()
        {
            var run = new TestRunRecord { mode = "EditMode" };
            var result = TestRunObserver.RootIdentityMatches(run, null, "root", "Suite", "EditMode");
            Assert.IsFalse(result);
        }

        [Test]
        public void RootIdentityMatches_BothNull_ReturnsFalse()
        {
            var result = TestRunObserver.RootIdentityMatches(null, null, "root", "Suite", "EditMode");
            Assert.IsFalse(result);
        }

        [Test]
        public void RootIdentityMatches_EmptyRunMode_TrueForAnyCompletedMode()
        {
            // run.mode == "" → mode check is skipped → true as long as names match
            var run = new TestRunRecord { mode = "" };
            var started = new TestRunEvent { unique_name = "root-1", full_name = "Suite" };

            Assert.IsTrue(
                TestRunObserver.RootIdentityMatches(run, started, "root-1", "Suite", "PlayMode"),
                "Empty run mode must match any completed mode");
            Assert.IsTrue(
                TestRunObserver.RootIdentityMatches(run, started, "root-1", "Suite", "EditMode"),
                "Empty run mode must match any completed mode");
        }

        [Test]
        public void RootIdentityMatches_StartedUniqueNameWhitespace_ReturnsFalse()
        {
            var run = new TestRunRecord { mode = "EditMode" };
            var started = new TestRunEvent { unique_name = "   ", full_name = "Suite" };

            var result = TestRunObserver.RootIdentityMatches(run, started, "   ", "Suite", "EditMode");

            Assert.IsFalse(result, "Whitespace-only unique_name must not match");
        }

        // ── ShouldPrepareManagedEnvironment ──

        [Test]
        public void ShouldPrepareManagedEnvironment_NullRun_ReturnsFalse()
        {
            Assert.IsFalse(TestRunObserver.ShouldPrepareManagedEnvironment(null));
        }

        // ── IsStaleActiveRun (R8-01: the RecoverTerminalEnvironments OR-decision
        // had zero direct coverage — an ||→&& inversion passed the whole suite) ──

        private static readonly string CurrentEditorSessionId =
            TestRunBuildFingerprintProbe.EditorSessionId();

        [TestCase("other-session", UtfRunActivity.Active, UtfRunActivity.Active, true,
            TestName = "IsStaleActiveRun_SessionMismatchBothActive_TrueProvesOr")]
        [TestCase(null, UtfRunActivity.Inactive, UtfRunActivity.Inactive, true,
            TestName = "IsStaleActiveRun_MatchingSessionBothInactive_True")]
        [TestCase(null, UtfRunActivity.Active, UtfRunActivity.Inactive, false,
            TestName = "IsStaleActiveRun_MatchingSessionOwnActive_False")]
        [TestCase(null, UtfRunActivity.Inactive, UtfRunActivity.Active, false,
            TestName = "IsStaleActiveRun_MatchingSessionAnyActiveOnly_False")]
        [TestCase("", UtfRunActivity.Inactive, UtfRunActivity.Inactive, true,
            TestName = "IsStaleActiveRun_EmptySessionBothInactive_True")]
        public void IsStaleActiveRun_EvaluatesOrOfSessionMismatchAndInactivity(
            string editorSessionId, UtfRunActivity ownActivity, UtfRunActivity anyActivity, bool expected)
        {
            var run = new TestRunRecord { editor_session_id = editorSessionId ?? CurrentEditorSessionId };

            var result = TestRunObserver.IsStaleActiveRun(run, ownActivity, anyActivity);

            Assert.That(result, Is.EqualTo(expected));
        }

        // ── MapLeafOutcome null adaptor overload ──

        [Test]
        public void MapLeafOutcome_NullAdaptor_ReturnsInvalid()
        {
            var result = TestRunObserver.MapLeafOutcome((ITestResultAdaptor)null);
            Assert.AreEqual(TestRunProtocol.LeafOutcome.Invalid, result);
        }

        // ── ResultLabel (private static, tested via reflection) ──

        private static string CallResultLabel(string state)
        {
            var method = typeof(TestRunObserver).GetMethod(
                "ResultLabel",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "ResultLabel not found on TestRunObserver");
            return (string)method.Invoke(null, new object[] { state });
        }

        [Test]
        public void ResultLabel_StateWithColon_ReturnsTextAfterColon()
        {
            Assert.AreEqual("Ignored", CallResultLabel("Skipped:Ignored"));
            Assert.AreEqual("Error", CallResultLabel("Failed:Error"));
            Assert.AreEqual("Cancelled", CallResultLabel("Failed:Cancelled"));
        }

        [Test]
        public void ResultLabel_StateWithoutColon_ReturnsEmpty()
        {
            Assert.AreEqual("", CallResultLabel("Passed"));
            Assert.AreEqual("", CallResultLabel("Failed"));
        }

        [Test]
        public void ResultLabel_EmptyState_ReturnsEmpty()
        {
            Assert.AreEqual("", CallResultLabel(""));
        }

        [Test]
        public void ResultLabel_NullState_ReturnsEmpty()
        {
            Assert.AreEqual("", CallResultLabel(null));
        }

        [Test]
        public void ResultLabel_ColonAtEnd_ReturnsEmpty()
        {
            // separator == state.Length - 1 → ""
            Assert.AreEqual("", CallResultLabel("Passed:"));
        }
    }
}
