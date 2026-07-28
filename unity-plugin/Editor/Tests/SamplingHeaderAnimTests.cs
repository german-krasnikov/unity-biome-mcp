using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SamplingHeaderAnimTests
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = SamplingHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasFreqRootClass() =>
            Assert.IsTrue(_root.ClassListContains("freq-root"));

        [Test]
        public void Build_RootHasBarsAndParticles() =>
            Assert.AreEqual(8, _root.childCount);

        [Test]
        public void Build_EachChildHasFreqBarClass()
        {
            for (int i = 0; i < 7; i++)
                Assert.IsTrue(_root.ElementAt(i).ClassListContains("freq-bar"));
        }

        [Test]
        public void Build_HasSamplingParticlePattern() =>
            Assert.IsTrue(_root.Q<BiomeAmbientParticles>()
                .ClassListContains("biome-ambient--sampling"));
    }
}
