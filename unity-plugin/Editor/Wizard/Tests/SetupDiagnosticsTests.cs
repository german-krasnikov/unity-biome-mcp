using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetupDiagnosticsTests
    {
        [Test]
        public void BuildClaudeCodeSnippet_ContainsPort()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            Assert.That(snippet, Does.Contain("9500"));
        }

        [Test]
        public void BuildClaudeCodeSnippet_ContainsMcpAdd()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            Assert.That(snippet, Does.Contain("mcp add"));
        }

        [Test]
        public void BuildClaudeCodeSnippet_ContainsUnity()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            StringAssert.Contains("unity", snippet.ToLowerInvariant());
        }

        [Test]
        public void BuildClaudeCodeSnippet_DifferentPort_ContainsThatPort()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9501);
            Assert.That(snippet, Does.Contain("9501"));
            Assert.That(snippet, Does.Not.Contain("9500"));
        }

        [Test]
        public void CheckServer_WhenNotRunning_ReturnsFalse()
        {
            if (MCPServer.IsRunning)
                Assert.Ignore("Server is running in this environment — cannot test 'not running' path");
            var (ok, detail) = SetupDiagnostics.CheckServer();
            Assert.IsFalse(ok, "MCPServer should not be running in EditMode test context");
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_NullDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython(null);
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_NonexistentDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython("/nonexistent/path/abc123");
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }

        [Test]
        public void CheckPython_EmptyDir_ReturnsFalse()
        {
            var (ok, detail) = SetupDiagnostics.CheckPython("");
            Assert.IsFalse(ok);
            Assert.IsNotNull(detail);
        }

        // ── P1-A: snippet unification ────────────────────────────────────────

        [Test]
        public void BuildClaudeCodeSnippet_ContainsUvx()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            StringAssert.Contains("uvx", snippet);
            StringAssert.Contains("--from", snippet);
            StringAssert.Contains("github.com", snippet);
        }

        [Test]
        public void BuildClaudeCodeSnippet_DoesNotContainPython3()
        {
            var snippet = SetupDiagnostics.BuildClaudeCodeSnippet(9500);
            Assert.That(snippet, Does.Not.Contain("python3"));
        }

        // ── P0-B fix 1: ResolveRepoRoot delegates ────────────────────────────

        [Test]
        public void ResolveRepoRoot_DelegatesToInstallSourceDetector_WhenOverrideSet()
        {
            var scopePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DiagTest_{System.Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(scopePath);
            System.IO.File.WriteAllText(System.IO.Path.Combine(scopePath, "install.py"), "# stub");
            InstallSourceDetector.SetSourceForTest(InstallSourceDetector.Source.Local);
            InstallSourceDetector.SetLocalRepoRootForTest(scopePath);
            try
            {
                // ResolveRepoRoot must return whatever InstallSourceDetector.LocalRepoRoot() returns
                var direct = InstallSourceDetector.LocalRepoRoot();
                var via    = SetupDiagnostics.ResolveRepoRoot();
                Assert.AreEqual(direct, via, "ResolveRepoRoot must delegate to InstallSourceDetector.LocalRepoRoot()");
            }
            finally
            {
                InstallSourceDetector.ClearTestOverride();
                try { if (System.IO.Directory.Exists(scopePath)) System.IO.Directory.Delete(scopePath, true); } catch { }
            }
        }
    }
}
