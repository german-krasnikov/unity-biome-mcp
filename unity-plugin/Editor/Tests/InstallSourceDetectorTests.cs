// TDD: InstallSourceDetector — pure logic paths testable without PackageInfo.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class InstallSourceDetectorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── IsLocalRepoRoot ───────────────────────────────────────────────────

        [Test]
        public void IsLocalRepoRoot_WithInstallPy_ReturnsTrue()
        {
            using var scope = new TempDirScope();
            System.IO.File.WriteAllText(System.IO.Path.Combine(scope.Path, "install.py"), "");

            Assert.IsTrue(InstallSourceDetector.IsLocalRepoRoot(scope.Path));
        }

        [Test]
        public void IsLocalRepoRoot_WithoutInstallPy_ReturnsFalse()
        {
            using var scope = new TempDirScope();

            Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(scope.Path));
        }

        [Test]
        public void IsLocalRepoRoot_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(null));
        }

        [Test]
        public void IsLocalRepoRoot_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(InstallSourceDetector.IsLocalRepoRoot(""));
        }

#if UNITY_INCLUDE_TESTS
        // ── Injection tests ───────────────────────────────────────────────────

        [Test]
        public void Detect_WithInjectedLocalSource_ReturnsLocal()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            Assert.AreEqual(InstallSourceDetector.Source.Local, InstallSourceDetector.Detect());
            InstallSourceDetector.ClearTestOverride();
        }

        [Test]
        public void Detect_WithInjectedGitSource_ReturnsGit()
        {
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Git);
            Assert.AreEqual(InstallSourceDetector.Source.Git, InstallSourceDetector.Detect());
            InstallSourceDetector.ClearTestOverride();
        }

        [Test]
        public void LocalRepoRoot_WithInjectedPath_ReturnsInjected()
        {
            InstallSourceDetector.SetLocalRepoRootForTest("/fake/repo");
            Assert.AreEqual("/fake/repo", InstallSourceDetector.LocalRepoRoot());
            InstallSourceDetector.ClearTestOverride();
        }
#endif
    }
}
