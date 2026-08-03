using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolGroupSummaryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Format_2_NoError_NotRunning_ReturnsBaseLabel()
        {
            Assert.AreEqual("⚙ 2 tools", ToolGroupSummary.Format(2, false, false));
        }

        [Test]
        public void Format_3_NoError_Running_AppendsEllipsis()
        {
            Assert.AreEqual("⚙ 3 tools...", ToolGroupSummary.Format(3, false, true));
        }

        [Test]
        public void Format_5_Error_NotRunning_AppendsCross()
        {
            Assert.AreEqual("⚙ 5 tools ✕", ToolGroupSummary.Format(5, true, false));
        }

        [Test]
        public void Format_4_Error_Running_AppendsCrossAndEllipsis()
        {
            Assert.AreEqual("⚙ 4 tools ✕...", ToolGroupSummary.Format(4, true, true));
        }

        [Test]
        public void Format_10_NoError_NotRunning_DoubleDigitsWork()
        {
            Assert.AreEqual("⚙ 10 tools", ToolGroupSummary.Format(10, false, false));
        }

        [Test]
        public void Format_2_Error_NotRunning_MinimumGroupWithError()
        {
            Assert.AreEqual("⚙ 2 tools ✕", ToolGroupSummary.Format(2, true, false));
        }

        [Test] public void Format_1_Singular()
            => Assert.AreEqual("⚙ 1 tool", ToolGroupSummary.Format(1, false, false));
    }
}
