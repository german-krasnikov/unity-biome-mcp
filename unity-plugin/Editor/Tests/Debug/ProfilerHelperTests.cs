// TDD RED: ProfilerHelper snapshot tests.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProfilerHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetSnapshot_ReturnsNonEmptyString()
        {
            var result = ProfilerHelper.GetSnapshot();
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        [Test]
        public void GetSnapshot_ContainsFpsLine()
        {
            var result = ProfilerHelper.GetSnapshot();
            Assert.IsTrue(result.Contains("fps="), $"Expected 'fps=' in: {result}");
        }

        [Test]
        public void GetSnapshot_ContainsMonoMemory()
        {
            var result = ProfilerHelper.GetSnapshot();
            Assert.IsTrue(result.Contains("mono="), $"Expected 'mono=' in: {result}");
        }

        [Test]
        public void GetSnapshot_ContainsGcStats()
        {
            var result = ProfilerHelper.GetSnapshot();
            Assert.IsTrue(result.Contains("gc_gen0="), $"Expected 'gc_gen0=' in: {result}");
        }
    }
}
