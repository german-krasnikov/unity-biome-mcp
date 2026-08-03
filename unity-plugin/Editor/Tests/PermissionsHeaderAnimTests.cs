using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PermissionsHeaderAnimTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = PermissionsHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasShieldRootClass() =>
            Assert.IsTrue(_root.ClassListContains("shield-root"));

        [Test]
        public void Build_RootHasHeaderAndParticles() =>
            Assert.AreEqual(4, _root.childCount);

        [Test]
        public void Build_LineLHasShieldLineClass() =>
            Assert.IsTrue(_root.ElementAt(0).ClassListContains("shield-line"));

        [Test]
        public void Build_LineRHasShieldLineClass() =>
            Assert.IsTrue(_root.ElementAt(2).ClassListContains("shield-line"));

        [Test]
        public void Build_HubHasFiveChildren() =>
            Assert.AreEqual(5, _root.ElementAt(1).childCount);

        [Test]
        public void Build_HubFirstChildHasShieldBodyClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(0).ClassListContains("shield-body"));

        [Test]
        public void Build_HubSecondChildHasLockShackleClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(1).ClassListContains("lock-shackle"));

        [Test]
        public void Build_HubThirdChildHasLockBarClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(2).ClassListContains("lock-bar"));

        [Test]
        public void Build_HubFourthChildHasLockDotClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(3).ClassListContains("lock-dot"));

        [Test]
        public void Build_HubFifthChildHasShieldScanClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(4).ClassListContains("shield-scan"));

        [Test]
        public void Build_HasAmbientParticleField() =>
            Assert.IsNotNull(_root.Q<BiomeAmbientParticles>());
    }
}
