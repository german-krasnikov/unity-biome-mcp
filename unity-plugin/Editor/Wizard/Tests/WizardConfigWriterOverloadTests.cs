using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    // Additive overload regression guard — existing WizardConfigWriterTests /
    // GitInstallUrlForTests must stay green and unedited (zero-behavior-change contract).
    [TestFixture]
    public class WizardConfigWriterOverloadTests
    {
        private const string CustomUrl =
            "git+https://example.com/fork.git@v9.9.9#subdirectory=server";

        [Test]
        public void Entry_WithExplicitGitUrl_UsesProvidedUrl_NotDefault()
        {
            var result = WizardConfigWriter.Entry(9500, CustomUrl);
            StringAssert.Contains(CustomUrl, result);
            StringAssert.DoesNotContain(WizardConfigWriter.GitInstallUrl, result);
        }

        [Test]
        public void Fresh_WithExplicitRootKey_UsesProvidedKey_NotMcpServers()
        {
            var result = WizardConfigWriter.Fresh(9500, WizardConfigWriter.GitInstallUrl, "servers");
            StringAssert.Contains("\"servers\"", result);
            StringAssert.DoesNotContain("\"mcpServers\"", result);
        }

        [Test]
        public void Merge_WithExplicitRootKey_PreservesSiblingKeysUnderThatRootKey()
        {
            var existing = "{\"servers\":{\"other-tool\":{\"command\":\"x\"}}}";
            var result = WizardConfigWriter.Merge(existing, 9501, WizardConfigWriter.GitInstallUrl, "servers");
            StringAssert.Contains("other-tool", result);
            StringAssert.Contains("unity-kiss", result);
            StringAssert.Contains("9501", result);
        }

        [Test]
        public void Fresh_ZeroArgOverload_StillDefaultsToMcpServersAndUnpinnedUrl()
        {
            var result = WizardConfigWriter.Fresh(9500);
            Assert.AreEqual(WizardConfigWriter.Fresh(9500, WizardConfigWriter.GitInstallUrl, "mcpServers"), result);
        }

        [Test]
        public void Merge_ZeroArgOverload_StillDefaultsToMcpServersAndUnpinnedUrl()
        {
            var existing = "{\"mcpServers\":{}}";
            var result = WizardConfigWriter.Merge(existing, 9500);
            Assert.AreEqual(WizardConfigWriter.Merge(existing, 9500, WizardConfigWriter.GitInstallUrl, "mcpServers"), result);
        }
    }
}
