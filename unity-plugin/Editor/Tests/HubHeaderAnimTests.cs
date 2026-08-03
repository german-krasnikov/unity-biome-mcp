using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HubHeaderAnimTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = HubHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasHanRootClass() =>
            Assert.IsTrue(_root.ClassListContains("hub-anim-root"));

        [Test]
        public void Build_RootHasExpectedChildren() =>
            Assert.AreEqual(11, _root.childCount);

        [Test]
        public void Build_FirstChildHasNodeSmClass() =>
            Assert.IsTrue(_root.ElementAt(0).ClassListContains("han-node--sm"));

        [Test]
        public void Build_ThirdChildHasNodeMdClass() =>
            Assert.IsTrue(_root.ElementAt(2).ClassListContains("han-node--md"));

        [Test]
        public void Build_FifthChildIsHub() =>
            Assert.IsTrue(_root.ElementAt(4).ClassListContains("han-hub"));

        [Test]
        public void Build_HubHasStatusLabel() =>
            Assert.IsTrue(_root.ElementAt(4).ElementAt(0).ClassListContains("han-status"));

        [Test]
        public void Build_LastChildIsPacket() =>
            Assert.IsTrue(_root.ElementAt(9).ClassListContains("han-packet"));

        [Test]
        public void Build_HasAmbientParticleField() =>
            Assert.IsNotNull(_root.Q<BiomeAmbientParticles>("biome-ambient-particles"));

        [Test]
        public void Build_Children1357HaveLineClass()
        {
            Assert.IsTrue(_root.ElementAt(1).ClassListContains("han-line"));
            Assert.IsTrue(_root.ElementAt(3).ClassListContains("han-line"));
            Assert.IsTrue(_root.ElementAt(5).ClassListContains("han-line"));
            Assert.IsTrue(_root.ElementAt(7).ClassListContains("han-line"));
        }

        [Test]
        public void Build_HubHasOneChild() =>
            Assert.AreEqual(1, _root.ElementAt(4).childCount);

        [Test]
        public void Build_SixthChildHasNodeMdClass() =>
            Assert.IsTrue(_root.ElementAt(6).ClassListContains("han-node--md"));

        [Test]
        public void Build_EighthChildHasNodeSmClass() =>
            Assert.IsTrue(_root.ElementAt(8).ClassListContains("han-node--sm"));
    }
}
