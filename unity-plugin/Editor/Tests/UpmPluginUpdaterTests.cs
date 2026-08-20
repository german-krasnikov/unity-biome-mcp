// TDD: UpmPluginUpdater — basic contract tests.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpmPluginUpdaterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
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
    }
}
