using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPDiagnosePanelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ReturnsNonNull()
        {
            var panel = MCPDiagnosePanel.Build();
            Assert.IsNotNull(panel);
        }

        [Test]
        public void Build_HasWizContainerClass()
        {
            var panel = MCPDiagnosePanel.Build();
            Assert.IsTrue(panel.ClassListContains("wiz-container"));
        }

        [Test]
        public void BuildStatusRow_OkShowsCheckmark()
        {
            var row = MCPDiagnosePanel.BuildStatusRow("Python", true, "3.12 found");
            var icon = row.Q<Label>(className: "wiz-status-ok");
            Assert.IsNotNull(icon);
            Assert.AreEqual("✓", icon.text);
        }

        [Test]
        public void BuildStatusRow_FailShowsX()
        {
            var row = MCPDiagnosePanel.BuildStatusRow("Server", false, "not running");
            var icon = row.Q<Label>(className: "wiz-status-fail");
            Assert.IsNotNull(icon);
            Assert.AreEqual("✗", icon.text);
        }

        [Test]
        public void BuildStatusRow_HasStatusRowClass()
        {
            var row = MCPDiagnosePanel.BuildStatusRow("Compile", true, "clean");
            Assert.IsTrue(row.ClassListContains("wiz-status-row"));
        }

        [Test]
        public void BuildScanDots_HasThreeDots()
        {
            var (dots, _) = MCPDiagnosePanel.BuildScanDots();
            var dotList = dots.Query<VisualElement>(className: "wiz-scan-dot").ToList();
            Assert.AreEqual(3, dotList.Count);
        }
    }
}
