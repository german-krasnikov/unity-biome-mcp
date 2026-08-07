// TDD: CommandRouter Python-only guard — direct TCP calls to Python-only tools
// must return an actionable "Python-only" error, not an opaque exception.
using NUnit.Framework;
using UnityMCP.Editor;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public sealed class PythonOnlyGuardTests : UnityMcpTestBase
    {
        private static string BuildJson(string cmd) =>
            $"{{\"id\":\"test-1\",\"cmd\":\"{cmd}\",\"args\":{{}}}}";

        [Test]
        public void DiscoverTools_DirectTCP_ReturnsActionableError()
        {
            var resp = CommandRouter.Process(BuildJson("discover_tools"));
            Assert.That(resp, Does.Contain("Python-only"));
        }

        [Test]
        public void RunTestsWait_DirectTCP_ReturnsActionableError()
        {
            var resp = CommandRouter.Process(BuildJson("run_tests_wait"));
            Assert.That(resp, Does.Contain("Python-only"));
        }

        [Test]
        public void LintPlaytestSuite_DirectTCP_ReturnsActionableError()
        {
            var resp = CommandRouter.Process(BuildJson("lint_playtest_suite"));
            Assert.That(resp, Does.Contain("Python-only"));
        }
    }
}
