using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPHubDividerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ReturnsElementWithHubDividerClass()
        {
            var host = new VisualElement();
            var el = MCPHubDivider.Build(host);
            Assert.IsTrue(el.ClassListContains("hub-divider"));
        }

        [Test]
        public void Build_HasThreeChildren()
        {
            var host = new VisualElement();
            var el = MCPHubDivider.Build(host);
            Assert.AreEqual(3, el.childCount);
        }

        [Test]
        public void Build_MiddleChildHasSpikeClass()
        {
            var host = new VisualElement();
            var el = MCPHubDivider.Build(host);
            var spike = el.ElementAt(1);
            Assert.IsTrue(spike.ClassListContains("hub-divider-spike"));
        }

        [Test]
        public void Build_FirstChildHasDividerLineClass()
        {
            var host = new VisualElement();
            var el = MCPHubDivider.Build(host);
            Assert.IsTrue(el.ElementAt(0).ClassListContains("hub-divider-line"));
        }

        [Test]
        public void Build_LastChildHasDividerLineClass()
        {
            var host = new VisualElement();
            var el = MCPHubDivider.Build(host);
            Assert.IsTrue(el.ElementAt(2).ClassListContains("hub-divider-line"));
        }
    }
}
