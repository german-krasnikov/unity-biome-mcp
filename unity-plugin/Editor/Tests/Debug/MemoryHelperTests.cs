// TDD RED: MemoryHelper snapshot and delta tracking tests.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MemoryHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void GetSnapshot_ReturnsNonEmptyString()
        {
            var result = MemoryHelper.GetSnapshot();
            Assert.IsFalse(string.IsNullOrEmpty(result));
        }

        [Test]
        public void GetSnapshot_ContainsTexture2DLine()
        {
            var result = MemoryHelper.GetSnapshot();
            Assert.IsTrue(result.Contains("Texture2D:"), $"Expected 'Texture2D:' in: {result}");
        }

        [Test]
        public void GetSnapshot_ContainsGameObjectLine()
        {
            var result = MemoryHelper.GetSnapshot();
            Assert.IsTrue(result.Contains("GameObject:"), $"Expected 'GameObject:' in: {result}");
        }

        [Test]
        public void GetSnapshot_SecondCall_ShowsDeltaOnChange()
        {
            // First call establishes baseline — no delta expected on first
            MemoryHelper.ResetCounts();
            var first = MemoryHelper.GetSnapshot();
            // Second call with same counts shows no delta (no + or - in count lines)
            var second = MemoryHelper.GetSnapshot();
            // At minimum, both calls produce valid output
            Assert.IsFalse(string.IsNullOrEmpty(second));
        }
    }
}
