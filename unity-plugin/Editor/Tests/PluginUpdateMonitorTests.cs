using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginUpdateMonitorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUpMonitorTest()
        {
            PluginUpdateMonitor._versionOverride = null;
            DeleteEditorPrefString(PluginUpdateMonitor.LastVersionKey);
            SessionState.EraseBool(PluginUpdateMonitor.UpdatedFlagKey);
            // UpmOperationGuard's SessionState keys are outside UnityMcpTestBase's known
            // isolations (ARC-10 T1) — reset explicitly so a leaked claim from a prior
            // test can't bleed in, and register restoration for this one.
            UpmOperationGuard.Complete();
            RegisterCleanup(() =>
            {
                PluginUpdateMonitor._versionOverride = null;
                SessionState.EraseBool(PluginUpdateMonitor.UpdatedFlagKey);
                UpmOperationGuard.Complete();
            });
        }

        [Test]
        public void NoUpdateDetectedOnFirstRun()
        {
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsFalse(SessionState.GetBool(PluginUpdateMonitor.UpdatedFlagKey, false));
        }

        [Test]
        public void UpdateDetectedWhenVersionChanges()
        {
            SetEditorPrefString(PluginUpdateMonitor.LastVersionKey, "1.4.0");
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsTrue(SessionState.GetBool(PluginUpdateMonitor.UpdatedFlagKey, false));
        }

        [Test]
        public void NoUpdateDetectedWhenVersionSame()
        {
            SetEditorPrefString(PluginUpdateMonitor.LastVersionKey, "1.5.0");
            PluginUpdateMonitor._versionOverride = "1.5.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.IsFalse(SessionState.GetBool(PluginUpdateMonitor.UpdatedFlagKey, false));
        }

        [Test]
        public void VersionStoredAfterCheck()
        {
            ProtectEditorPrefString(PluginUpdateMonitor.LastVersionKey);
            PluginUpdateMonitor._versionOverride = "1.6.0";
            PluginUpdateMonitor.CheckVersionChange();
            Assert.AreEqual("1.6.0", EditorPrefs.GetString(PluginUpdateMonitor.LastVersionKey, ""));
        }

        [Test]
        public void GetCurrentVersion_ReturnsNonEmpty()
        {
            // Just verify it doesn't throw and returns something parseable.
            var ver = PluginUpdateMonitor.GetCurrentVersion();
            Assert.IsNotNull(ver);
            Assert.IsNotEmpty(ver);
        }

        // C1 round1 #4/#8: UpmPluginUpdater.Update() claims UpmOperationGuard via
        // TryBegin but only releases it from Poll/PollReload closures on
        // EditorApplication.update. Updating the plugin's own package triggers a
        // domain reload that tears those closures down before Complete() runs, so
        // the SessionState-backed guard (survives reload by design) stays claimed
        // until Editor restart — LevelUpPanel/VersionPickerPage.Build() read
        // IsInFlight directly and keep showing "update in progress" forever.
        [Test]
        public void VersionBumpAfterReload_ReleasesUpmOperationGuard()
        {
            UpmOperationGuard.TryBegin("1.6.0");
            SetEditorPrefString(PluginUpdateMonitor.LastVersionKey, "1.5.0");
            PluginUpdateMonitor._versionOverride = "1.6.0";

            PluginUpdateMonitor.CheckVersionChange();

            Assert.IsFalse(UpmOperationGuard.IsInFlight,
                "a version bump observed after reload must release the guard claimed before it");
        }

        // Companion to VersionBumpAfterReload_ReleasesUpmOperationGuard: guards
        // against an unconditional Complete() call — the guard must only be
        // released when a version bump is actually observed, not on every check.
        [Test]
        public void SameVersionAfterReload_KeepsUpmOperationGuard()
        {
            UpmOperationGuard.TryBegin("1.5.0");
            SetEditorPrefString(PluginUpdateMonitor.LastVersionKey, "1.5.0");
            PluginUpdateMonitor._versionOverride = "1.5.0";

            PluginUpdateMonitor.CheckVersionChange();

            Assert.IsTrue(UpmOperationGuard.IsInFlight,
                "an unrelated in-flight claim must not be released when no version bump occurred");
        }

        // C1 round3 #5: CheckVersionChange's guard-release fast path was reachable only
        // via delayCall, which a backgrounded Editor (no focus/render frames — this
        // plugin's normal MCP-driven posture) does not reliably drain (RELAY-FIX, commit
        // 1bcc90b7) — so a self-update triggered while backgrounded fell back to
        // UpmOperationGuard's 300s staleness ceiling instead of releasing on the next tick.
        [Test]
        public void PluginUpdateMonitor_SelfHealsViaEditorApplicationUpdate_NotDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(PluginUpdateMonitor), "Editor/Updates/PluginUpdateMonitor.cs");
            Assert.That(src, Does.Contain("EditorApplication.update"),
                "PluginUpdateMonitor must self-heal via EditorApplication.update — delayCall alone does not drain in a backgrounded Editor (see RELAY-FIX, commit 1bcc90b7)");
            Assert.That(src, Does.Not.Contain("delayCall"),
                "PluginUpdateMonitor must not depend on delayCall anywhere — it does not drain in a backgrounded Editor");
        }

        // Companion behavioral check: RegisterHooks must subscribe RunOnce to
        // EditorApplication.update, and RunOnce must remove itself after firing, so it
        // never re-fires on the next tick. EditorApplication.update is a plain public
        // field (not a C# event), so its invocation list is directly readable — same
        // technique already used by MCPChatWindowTestIsolationTests.ResumeCallbacksFor
        // for delayCall.
        [Test]
        public void RegisterHooks_SubscribesRunOnce_WhichSelfUnsubscribesAfterFiring()
        {
            RegisterCleanup(() => EditorApplication.update -= PluginUpdateMonitor.RunOnce);

            PluginUpdateMonitor.RegisterHooks();
            var subscribedAfterRegister = IsRunOnceSubscribed();
            Assert.IsTrue(subscribedAfterRegister,
                "RegisterHooks must subscribe RunOnce to EditorApplication.update");

            PluginUpdateMonitor.RunOnce();
            Assert.IsFalse(IsRunOnceSubscribed(),
                "RunOnce must remove itself from EditorApplication.update after firing once");
        }

        // C1 round3 #5: proves the guard-release fast path is reachable by simulating
        // exactly one Editor tick — never touching delayCall — which is what a
        // backgrounded Editor actually does (RELAY-FIX, commit 1bcc90b7).
        [Test]
        public void VersionBumpDetection_ReachableViaEditorApplicationUpdate_WithoutDelayCall()
        {
            UpmOperationGuard.TryBegin("1.6.0");
            SetEditorPrefString(PluginUpdateMonitor.LastVersionKey, "1.5.0");
            PluginUpdateMonitor._versionOverride = "1.6.0";

            PluginUpdateMonitor.RegisterHooks();
            PluginUpdateMonitor.RunOnce(); // simulates the next Editor tick; delayCall never fires

            Assert.IsFalse(UpmOperationGuard.IsInFlight,
                "a version bump reachable only via EditorApplication.update must still release the guard, proving delayCall is not required");
        }

        private static bool IsRunOnceSubscribed() =>
            (EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>())
            .Any(d => d.Method.Name == nameof(PluginUpdateMonitor.RunOnce)
                && d.Method.DeclaringType == typeof(PluginUpdateMonitor));
    }
}
