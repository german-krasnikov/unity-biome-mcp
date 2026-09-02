using System;
using System.IO;
using NUnit.Framework;
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
