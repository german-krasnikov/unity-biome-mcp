// TDD: UpmPluginUpdater — basic contract tests.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpmPluginUpdaterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // UpmOperationGuard's SessionState keys are outside UnityMcpTestBase's known
        // isolations (ARC-10 T1/T3) — reset explicitly so a leaked claim can't bleed
        // between tests in either direction.
        [SetUp]
        public void SetUpGuard() => UpmOperationGuard.Complete();

        [TearDown]
        public void TearDownGuard() => UpmOperationGuard.Complete();

        [Test]
        public void BuildUrl_ContainsVersionTag()
        {
            var url = UpmPluginUpdater.BuildUrl("unity-plugin", "1.2.3");
            StringAssert.Contains("v1.2.3", url);
            StringAssert.Contains("unity-plugin", url);
        }

        [Test]
        public void BuildUrl_ContainsGitUrl()
        {
            var url = UpmPluginUpdater.BuildUrl("unity-plugin", "1.0.0");
            StringAssert.Contains(UpdateChecker.RepoGitUrl, url);
        }

        [Test]
        public void BuildUrl_ReloadPackage_HasReloadPath()
        {
            var url = UpmPluginUpdater.BuildUrl("unity-plugin-reload", "1.0.0");
            StringAssert.Contains("unity-plugin-reload", url);
        }

        [Test]
        public void BuildUrl_HasPathQueryParam()
        {
            var url = UpmPluginUpdater.BuildUrl("unity-plugin", "1.0.0");
            StringAssert.Contains("?path=", url);
        }

        [Test]
        public void Update_NullVersion_InvokesCallbackFalse()
        {
            bool? result = null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No version specified"));
            UpmPluginUpdater.Update(null, success => result = success);
            Assert.AreEqual(false, result);
        }

        [Test]
        public void Update_EmptyVersion_InvokesCallbackFalse()
        {
            bool? result = null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No version specified"));
            UpmPluginUpdater.Update("", success => result = success);
            Assert.AreEqual(false, result);
        }

        // ARC-10 T3, doc test 2: pins ordering against a future refactor that would
        // claim the guard before the null/empty-version early return, which would
        // deadlock every subsequent Update() call on one no-op invocation.
        [Test]
        public void Update_NullVersion_DoesNotClaimGuard()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("No version specified"));
            UpmPluginUpdater.Update(null, _ => { });
            Assert.IsFalse(UpmOperationGuard.IsInFlight);
        }

        // ARC-10 T3, doc test 1: the actual serialization fix. Seeds the guard as if
        // another caller (LevelUpPanel/rollback/align) already claimed it, then
        // proves a second Update() call short-circuits before touching Client.Add —
        // no real network I/O, so this stays instant and deterministic. Manual Arm-A
        // proof (one-time, not the loop): reverting the TryBegin check makes this
        // hit real UPM for a nonexistent tag — expect slow, not instant, red.
        [Test]
        public void Update_WhileGuardInFlight_SkipsAddAndInvokesCallbackFalse()
        {
            Assert.IsTrue(UpmOperationGuard.TryBegin("9.9.9"));
            bool? result = null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("already in progress"));

            UpmPluginUpdater.Update("1.2.3", success => result = success);

            Assert.AreEqual(false, result);
            Assert.AreEqual("9.9.9", UpmOperationGuard.InFlightVersion);
            StringAssert.Contains("already in progress", UpmPluginUpdater.LastFailureReason);
        }

        // ARC-10 T3, doc test 3: pure composition, no Client.Add involved.
        [Test]
        public void BuildFailureReason_ComposesClassifierOutput()
        {
            const string raw = "fatal: couldn't find remote ref refs/tags/v1.49.0";

            var actual = UpmPluginUpdater.BuildFailureReason("1.49.0", raw);

            Assert.AreEqual(
                UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.GitRefMissing, "1.49.0", raw),
                actual);
        }

        // FinishUpdate is the shared terminal-branch bookkeeping every Poll/PollReload
        // exit path funnels through (success, both timeouts, both add-failures). It is
        // exercised directly rather than through a real Client.Add round-trip: ARC-10
        // §2 explicitly rejected a Client.Add DI seam as bigger than this bug needs,
        // and a real Add() against the live git URL would be slow, network-flaky, and
        // could mutate this disposable worker's manifest from an orphaned background
        // request outliving the test. This still proves Complete()-on-every-terminal-
        // branch and the guard-release contract that a real round-trip would also prove.
        [Test]
        public void FinishUpdate_Success_ReleasesGuardAndClearsFailureReason()
        {
            UpmOperationGuard.TryBegin("1.2.3");
            UpmPluginUpdater.LastFailureReason = "stale reason from a previous failure";
            bool? result = null;

            UpmPluginUpdater.FinishUpdate(true, null, success => result = success);

            Assert.AreEqual(true, result);
            Assert.IsFalse(UpmOperationGuard.IsInFlight);
            Assert.IsNull(UpmPluginUpdater.LastFailureReason);
        }

        [Test]
        public void FinishUpdate_Failure_ReleasesGuardAndRecordsActionableText()
        {
            UpmOperationGuard.TryBegin("1.2.3");
            var reason = UpmErrorClassifier.ActionableText(UpmErrorClassifier.Reason.Network, "1.2.3", "boom");
            bool? result = null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Could not reach GitHub"));

            UpmPluginUpdater.FinishUpdate(false, reason, success => result = success);

            Assert.AreEqual(false, result);
            Assert.IsFalse(UpmOperationGuard.IsInFlight);
            Assert.AreEqual(reason, UpmPluginUpdater.LastFailureReason);
        }

        // Proves the timeout branch's contract without waiting 120s or touching
        // Client.Add: guard released, and a fresh caller can claim it immediately
        // afterward (no stale-ceiling wait needed — Complete() already ran).
        [Test]
        public void FinishUpdate_Timeout_ReleasesGuardAndAllowsImmediateRetry()
        {
            UpmOperationGuard.TryBegin("1.2.3");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("timed out"));

            UpmPluginUpdater.FinishUpdate(false, "UPM update timed out after 120s.", _ => { });

            Assert.IsFalse(UpmOperationGuard.IsInFlight);
            Assert.IsTrue(UpmOperationGuard.TryBegin("2.0.0"));
            Assert.AreEqual("2.0.0", UpmOperationGuard.InFlightVersion);
        }
    }
}
