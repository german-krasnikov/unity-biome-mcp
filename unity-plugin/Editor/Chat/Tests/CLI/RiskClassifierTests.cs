using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RiskClassifierTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Classify_Bash_ReturnsHigh()
            => Assert.AreEqual(RiskLevel.High, RiskClassifier.Classify("Bash"));

        [Test] public void Classify_Write_ReturnsHigh()
            => Assert.AreEqual(RiskLevel.High, RiskClassifier.Classify("Write"));

        [Test] public void Classify_Edit_ReturnsMedium()
            => Assert.AreEqual(RiskLevel.Medium, RiskClassifier.Classify("Edit"));

        [Test] public void Classify_MultiEdit_ReturnsMedium()
            => Assert.AreEqual(RiskLevel.Medium, RiskClassifier.Classify("MultiEdit"));

        [Test] public void Classify_Read_ReturnsLow()
            => Assert.AreEqual(RiskLevel.Low, RiskClassifier.Classify("Read"));

        [Test] public void Classify_Glob_ReturnsLow()
            => Assert.AreEqual(RiskLevel.Low, RiskClassifier.Classify("Glob"));

        [Test] public void Classify_Grep_ReturnsLow()
            => Assert.AreEqual(RiskLevel.Low, RiskClassifier.Classify("Grep"));

        [Test] public void Classify_McpTool_ReturnsLow()
            => Assert.AreEqual(RiskLevel.Low, RiskClassifier.Classify("mcp__unity__get_hierarchy"));

        [Test] public void Classify_McpToolWithServerPrefix_ReturnsLow()
            => Assert.AreEqual(RiskLevel.Low, RiskClassifier.Classify("mcp__unity-biome-mcp__batch"));

        [Test] public void Classify_UnknownTool_ReturnsMedium()
            => Assert.AreEqual(RiskLevel.Medium, RiskClassifier.Classify("SomeFutureTool"));

        [Test] public void Classify_NullOrEmpty_ReturnsMedium()
        {
            Assert.AreEqual(RiskLevel.Medium, RiskClassifier.Classify(null));
            Assert.AreEqual(RiskLevel.Medium, RiskClassifier.Classify(""));
        }
    }
}
