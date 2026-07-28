using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ToolsHeaderAnimTests
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = ToolsHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasAnimToolsClass() =>
            Assert.IsTrue(_root.ClassListContains("anim-tools"));

        [Test]
        public void Build_RootHasControlsAndParticles() =>
            Assert.AreEqual(6, _root.childCount);

        [Test]
        public void Build_EachChildHasToggleTrackClass()
        {
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(_root.ElementAt(i).ClassListContains("toggle-track"));
        }

        [Test]
        public void Build_EachTrackHasOneChild()
        {
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(1, _root.ElementAt(i).childCount);
        }

        [Test]
        public void Build_EachTrackChildHasToggleKnobClass()
        {
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(_root.ElementAt(i).ElementAt(0).ClassListContains("toggle-knob"));
        }

        [Test]
        public void Build_HasToolParticlePattern() =>
            Assert.IsTrue(_root.Q<BiomeAmbientParticles>()
                .ClassListContains("biome-ambient--tools"));
    }
}
