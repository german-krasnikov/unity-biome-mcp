using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class VersionPickerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // UpmOperationGuard's SessionState keys and UpmPluginUpdater.LastFailureReason
        // are outside UnityMcpTestBase's known isolations (ARC-10 T1/T3) — reset
        // explicitly so seeded in-flight/failure state can't bleed either way (ARC-10 T4).
        [SetUp]
        public void SetUpGuard()
        {
            UpmOperationGuard.Complete();
            UpmPluginUpdater.LastFailureReason = null;
        }

        [TearDown]
        public void TearDownGuard()
        {
            UpmOperationGuard.Complete();
            UpmPluginUpdater.LastFailureReason = null;
        }

        [Test]
        public void BuildVersionPickerPage_ReturnsNonNull()
        {
            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });
            Assert.IsNotNull(page);
        }

        [Test]
        public void BuildVersionPickerPage_HasNavPageClass()
        {
            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });
            Assert.IsTrue(page.ClassListContains("nav-page"));
        }

        [Test]
        public void BuildVersionPickerPage_HasBackHeader()
        {
            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });
            Assert.IsNotNull(page.Q(className: "nav-back-header"));
        }

        [Test]
        public void BuildVersionPickerPage_HasRollbackButton()
        {
            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });
            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsNotNull(btn, "Expected a Button with class 'updates-check-btn'");
        }

        [Test]
        public void VersionPickerPage_RollbackButton_Enabled_WhenGuardNotInFlight()
        {
            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });
            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsTrue(btn.enabledSelf);
        }

        [Test]
        public void VersionPickerPage_RollbackButton_Disabled_WhenGuardInFlight()
        {
            UpmOperationGuard.TryBegin("0.42.0");

            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });

            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsNotNull(btn, "Expected a Button with class 'updates-check-btn'");
            Assert.IsFalse(btn.enabledSelf);
        }

        [Test]
        public void VersionPickerPage_RollbackButton_ShowsInProgressText_WhenGuardInFlight()
        {
            UpmOperationGuard.TryBegin("0.42.0");

            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });

            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.AreEqual("Update in progress…", btn.text);
        }

        // Simulates a rebuild after a domain reload: the guard (SessionState) is still
        // claimed, but nothing in-memory remembers this — a fresh Build() call must read
        // UpmOperationGuard directly, never a static UI cache, to restore the same state.
        [Test]
        public void VersionPickerPage_RollbackButton_SurvivesRebuildWhileGuardStillInFlight()
        {
            UpmOperationGuard.TryBegin("0.42.0");
            SettingsPageFactory.BuildVersionPickerPage(() => { }); // pre-reload build

            var rebuilt = SettingsPageFactory.BuildVersionPickerPage(() => { }); // post-reload

            var btn = rebuilt.Q<Button>(className: "updates-check-btn");
            Assert.IsFalse(btn.enabledSelf);
        }

        // C1 r2 #5: companion to LevelUpPanel_Build_RecoversFromStaleGuard_WithoutVersionBump
        // — the rollback button must recover from the same 300s ceiling on any rebuild,
        // not only when the reload happens to be the update's own version bump.
        [Test]
        public void VersionPickerPage_RollbackButton_RecoversFromStaleGuard_WithoutVersionBump()
        {
            var originalClock = UpmOperationGuard.NowSecondsFloat;
            RegisterCleanup(() => UpmOperationGuard.NowSecondsFloat = originalClock);
            var now = 0f;
            UpmOperationGuard.NowSecondsFloat = () => now;

            UpmOperationGuard.TryBegin("0.42.0");
            now = UpmOperationGuard.StaleCeilingSeconds + 1f;

            var page = SettingsPageFactory.BuildVersionPickerPage(() => { });

            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsTrue(btn.enabledSelf, "Recovered state must re-enable the rollback button.");
            Assert.AreNotEqual("Update in progress…", btn.text,
                "Recovered state must restore the idle rollback label, not stay on the busy text.");
        }

        [Test]
        public void VersionPickerPage_AlignButton_Disabled_WhenGuardInFlight_AndIncoherent()
        {
            var current = UpdateChecker.GetCurrentVersion();
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp,
                    "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\",\"args\":[\"--from\"," +
                    "\"git+https://github.com/german-krasnikov/unity-biome-mcp.git@v0.0.1#subdirectory=server\"," +
                    "\"unity-biome-mcp\"]}}}");
                VersionCoherenceChecker._testConfigPath = tmp;
                UpmOperationGuard.TryBegin(current);

                var page = SettingsPageFactory.BuildVersionPickerPage(() => { });

                var alignBtn = page.Q<Button>(className: "biome-button--secondary");
                Assert.IsNotNull(alignBtn, "Expected an Align Both button when versions diverge.");
                Assert.IsFalse(alignBtn.enabledSelf);
            }
            finally
            {
                File.Delete(tmp);
                VersionCoherenceChecker._testConfigPath = null;
            }
        }

        [Test]
        public void FormatResultMessage_Success_ReturnsDone()
        {
            Assert.AreEqual("Done.", VersionPickerPage.FormatResultMessage(true));
        }

        [Test]
        public void FormatResultMessage_FailureWithReason_ReturnsLastFailureReason()
        {
            UpmPluginUpdater.LastFailureReason =
                "Could not reach GitHub. Check your network connection and try again.";

            Assert.AreEqual(
                UpmPluginUpdater.LastFailureReason,
                VersionPickerPage.FormatResultMessage(false));
        }

        [Test]
        public void FormatResultMessage_FailureWithoutReason_FallsBackToGenericMessage()
        {
            UpmPluginUpdater.LastFailureReason = null;

            Assert.AreEqual("UPM failed — check Console.", VersionPickerPage.FormatResultMessage(false));
        }

        [Test]
        public void RollbackButtonText_FormatsVersionIntoLabel()
        {
            Assert.AreEqual("Roll Back to v1.2.3", VersionPickerPage.RollbackButtonText("1.2.3"));
        }

        // Proves the immediate-feedback mutation (C1 #14) in isolation, with no
        // UpmPluginUpdater.Update involved — the round-trip test below cannot
        // discriminate this step on its own because Update always resolves
        // synchronously to false in a network-free test, overwriting this state
        // with the restore state before either the button or an assertion could
        // observe it.
        [Test]
        public void SetRollingBackState_DisablesButtonAndSetsRollingBackText()
        {
            var btn = new Button { text = "placeholder" };

            VersionPickerPage.SetRollingBackState(btn);

            Assert.IsFalse(btn.enabledSelf);
            Assert.AreEqual(VersionPickerPage.RollingBackButtonText, btn.text);
        }

        // Guard pre-claimed by a different version forces UpmPluginUpdater.Update's
        // busy short-circuit, which resolves onComplete(false) synchronously and
        // network-free (mirrors UpmPluginUpdaterTests.Update_WhileGuardInFlight_
        // SkipsAddAndInvokesCallbackFalse). This proves DoRollback's callback fires
        // exactly once and restores the button via the shared
        // RollbackButtonText(version) — a prior defect left the button disabled
        // with in-progress text for the entire round trip because no callback ever
        // touched it.
        [Test]
        public void DoRollback_WhenGuardBusy_CallbackFiresAndRestoresButton()
        {
            Assert.IsTrue(UpmOperationGuard.TryBegin("9.9.9"));
            var btn = new Button { text = "placeholder" };
            int dialogCalls = 0;
            string dialogTitle = null;
            VersionPickerPage.ResultDialogOverride = (title, _) => { dialogCalls++; dialogTitle = title; };
            RegisterCleanup(() => VersionPickerPage.ResultDialogOverride = null);
            LogAssert.Expect(LogType.Error, new Regex("already in progress"));

            VersionPickerPage.DoRollback("1.0.0", btn);

            Assert.IsTrue(btn.enabledSelf);
            Assert.AreEqual(VersionPickerPage.RollbackButtonText("1.0.0"), btn.text);
            Assert.AreEqual(1, dialogCalls, "Expected the result dialog callback to fire exactly once.");
            Assert.AreEqual("Roll Back", dialogTitle);
        }
    }

    [TestFixture]
    public class VersionCoherenceCheckerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            VersionCoherenceChecker._testConfigPath = null;
        }

        [Test]
        public void IsCoherent_NullServerRef_ReturnsTrue()
        {
            Assert.IsTrue(VersionCoherenceChecker.IsCoherent("0.55.2", null));
        }

        [Test]
        public void IsCoherent_MatchingVersions_ReturnsTrue()
        {
            Assert.IsTrue(VersionCoherenceChecker.IsCoherent("0.54.1", "0.54.1"));
        }

        [Test]
        public void IsCoherent_DivergentVersions_ReturnsFalse()
        {
            Assert.IsFalse(VersionCoherenceChecker.IsCoherent("0.55.2", "0.54.1"));
        }

        [Test]
        public void GetServerPinnedRef_UnpinnedUrl_ReturnsNull()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\",\"args\":[\"--from\",\"git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server\",\"unity-biome-mcp\"]}}}");
                VersionCoherenceChecker._testConfigPath = tmp;
                Assert.IsNull(VersionCoherenceChecker.GetServerPinnedRef());
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void GetServerPinnedRef_PinnedUrl_ReturnsVersion()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"uvx\",\"args\":[\"--from\",\"git+https://github.com/german-krasnikov/unity-biome-mcp.git@v0.54.1#subdirectory=server\",\"unity-biome-mcp\"]}}}");
                VersionCoherenceChecker._testConfigPath = tmp;
                Assert.AreEqual("0.54.1", VersionCoherenceChecker.GetServerPinnedRef());
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void GetServerPinnedRef_MissingFile_ReturnsNull()
        {
            VersionCoherenceChecker._testConfigPath = "/tmp/nonexistent_unity_mcp_config_xyz.json";
            Assert.IsNull(VersionCoherenceChecker.GetServerPinnedRef());
        }
    }
}
