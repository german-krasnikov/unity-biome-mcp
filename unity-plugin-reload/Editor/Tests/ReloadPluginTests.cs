// TDD: ReloadPlugin.ShouldStartReloadServer — batch/worker gating.
using NUnit.Framework;

namespace UnityMCP.Reload.Tests
{
    [TestFixture]
    public class ReloadPluginTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ShouldStart_BatchMode_ReturnsFalse()
        {
            Assert.IsFalse(ReloadPlugin.ShouldStartReloadServer(isBatchMode: true, commandLineArgs: new string[0]));
        }

        [Test]
        public void ShouldStart_AssetImportWorkerInArgs_ReturnsFalse()
        {
            var args = new[] { "/path/to/Unity", "-adb2", "-batchMode", "-name", "AssetImportWorker0", "-projectpath", "/proj" };
            Assert.IsFalse(ReloadPlugin.ShouldStartReloadServer(isBatchMode: false, commandLineArgs: args));
        }

        [Test]
        public void ShouldStart_InteractiveEditor_ReturnsTrue()
        {
            var args = new[] { "/path/to/Unity", "-projectpath", "/my/project" };
            Assert.IsTrue(ReloadPlugin.ShouldStartReloadServer(isBatchMode: false, commandLineArgs: args));
        }

        [Test]
        public void ShouldStart_EmptyArgs_ReturnsTrue()
        {
            Assert.IsTrue(ReloadPlugin.ShouldStartReloadServer(isBatchMode: false, commandLineArgs: new string[0]));
        }
    }
}
