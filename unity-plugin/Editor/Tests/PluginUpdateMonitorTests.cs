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
    }
}
