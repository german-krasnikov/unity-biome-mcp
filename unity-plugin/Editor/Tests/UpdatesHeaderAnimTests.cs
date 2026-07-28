using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpdatesHeaderAnimTests
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = UpdatesHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasAnimUpdatesClass() =>
            Assert.IsTrue(_root.ClassListContains("anim-updates"));

        [Test]
        public void Build_RootHasCorrectChildCount() =>
            Assert.AreEqual(7, _root.childCount);

        [Test]
        public void Build_EachChildHasUploadBarClass()
        {
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(_root.ElementAt(i).ClassListContains("upload-bar"));
        }

        [Test]
        public void Build_HasUpdatesParticlePattern() =>
            Assert.IsTrue(_root.Q<BiomeAmbientParticles>()
                .ClassListContains("biome-ambient--updates"));

        [Test]
        public void Build_HasAnimatedLevelUpArrowAndAura()
        {
            Assert.IsNotNull(_root.Q<Label>(className: "levelup-arrow"));
            Assert.AreEqual(
                2,
                _root.Query<VisualElement>(className: "levelup-aura").ToList().Count);
        }
    }
}
