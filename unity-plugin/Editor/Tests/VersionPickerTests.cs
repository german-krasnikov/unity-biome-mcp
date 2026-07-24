using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class VersionPickerTests
    {
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
    }

    [TestFixture]
    public class VersionCoherenceCheckerTests
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
