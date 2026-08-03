using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class StatusAmbientAnimTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = StatusAmbientAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasStatusAmbientClass() =>
            Assert.IsTrue(_root.ClassListContains("status-ambient"));

        [Test]
        public void Build_ContainsScanlineElement()
        {
            bool found = false;
            _root.Query<VisualElement>().ForEach(e => { if (e.ClassListContains("status-scanline")) found = true; });
            Assert.IsTrue(found);
        }

        [Test]
        public void Build_ContainsGridElement()
        {
            bool found = false;
            _root.Query<VisualElement>().ForEach(e => { if (e.ClassListContains("status-grid")) found = true; });
            Assert.IsTrue(found);
        }

        [Test]
        public void Build_ContainsSonarElement()
        {
            bool found = false;
            _root.Query<VisualElement>().ForEach(e => { if (e.ClassListContains("status-sonar")) found = true; });
            Assert.IsTrue(found);
        }

        [Test]
        public void Build_GridHas16Dots()
        {
            VisualElement grid = null;
            _root.Query<VisualElement>().ForEach(e => { if (e.ClassListContains("status-grid")) grid = e; });
            Assert.IsNotNull(grid);
            Assert.AreEqual(16, grid.childCount);
        }
    }
}
