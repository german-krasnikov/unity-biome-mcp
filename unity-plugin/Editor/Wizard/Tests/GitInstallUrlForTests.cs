using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GitInstallUrlForTests
    {
        [Test]
        public void GitInstallUrlFor_NullRef_ReturnsDefaultUrl()
        {
            Assert.AreEqual(WizardConfigWriter.GitInstallUrl, WizardConfigWriter.GitInstallUrlFor(null));
        }

        [Test]
        public void GitInstallUrlFor_EmptyRef_ReturnsDefaultUrl()
        {
            Assert.AreEqual(WizardConfigWriter.GitInstallUrl, WizardConfigWriter.GitInstallUrlFor(""));
        }

        [Test]
        public void GitInstallUrlFor_ValidRef_ContainsTag()
        {
            var url = WizardConfigWriter.GitInstallUrlFor("0.54.1");
            Assert.IsTrue(url.Contains("@v0.54.1"), $"URL should contain @v0.54.1 but was: {url}");
            Assert.IsTrue(url.Contains("#subdirectory=server"), $"URL should contain #subdirectory=server but was: {url}");
        }

        [Test]
        public void GitInstallUrlFor_VPrefixedRef_Normalises()
        {
            Assert.AreEqual(
                WizardConfigWriter.GitInstallUrlFor("0.54.1"),
                WizardConfigWriter.GitInstallUrlFor("v0.54.1"));
        }

        [Test]
        public void GitInstallUrlFor_TagAppearsBeforeFragment()
        {
            var url = WizardConfigWriter.GitInstallUrlFor("0.54.1");
            Assert.Less(url.IndexOf("@v0.54.1"), url.IndexOf("#subdirectory"),
                "Tag should appear before #subdirectory fragment");
        }

        [Test]
        public void GitInstallUrlFor_CorrectFullForm()
        {
            var url = WizardConfigWriter.GitInstallUrlFor("1.2.3");
            Assert.AreEqual(
                "git+https://github.com/german-krasnikov/unity-biome-mcp.git@v1.2.3#subdirectory=server",
                url);
        }

        [Test]
        public void GitInstallUrlFor_MalformedRef_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => WizardConfigWriter.GitInstallUrlFor("bad-ver"));
        }

        [Test]
        public void GitInstallUrlFor_TwoPartVersion_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => WizardConfigWriter.GitInstallUrlFor("0.54"));
        }
    }
}
