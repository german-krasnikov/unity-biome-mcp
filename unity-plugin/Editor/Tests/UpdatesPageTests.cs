using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpdatesPageTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void BuildUpdatesPage_ReturnsNonNull()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            Assert.IsNotNull(page);
        }

        [Test]
        public void BuildUpdatesPage_HasNavPageClass()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            Assert.IsTrue(page.ClassListContains("nav-page"));
        }

        [Test]
        public void BuildUpdatesPage_ContainsCheckButton()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsNotNull(btn, "Expected a Button with class 'updates-check-btn'");
        }

        [Test]
        public void BuildUpdatesPage_ContainsChangelogArea()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var area = page.Q(className: "updates-changelog");
            Assert.IsNotNull(area, "Expected an element with class 'updates-changelog'");
        }

        [Test]
        public void BuildUpdatesPage_NoBanner_WhenNoUpdate()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var banner = page.Q(className: "lvlup-cta");
            Assert.IsNull(banner, "LevelUp panel should not be present when no update available");
        }

        [Test]
        public void BuildUpdatesPage_ShowsBanner_WhenUpdateAvailable()
        {
            UpdateChecker.SetAvailableVersionForTest("9.99.0");
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var banner = page.Q(className: "lvlup-cta");
            Assert.IsNotNull(banner, "LevelUp panel should be present when update available");
        }
    }
}
