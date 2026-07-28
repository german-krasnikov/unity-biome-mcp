using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BiomeParticleBurstTests
    {
        [Test]
        public void Emit_CreatesSinglePooledBurst()
        {
            var host = new VisualElement();
            BiomeParticleBurst.Emit(host);
            BiomeParticleBurst.Emit(host);

            Assert.AreEqual(
                1,
                host.Query<BiomeParticleBurst>().ToList().Count);
        }

        [Test]
        public void Emit_BuildsExpectedParticlePool()
        {
            var host = new VisualElement();
            BiomeParticleBurst.Emit(host);

            var burst = host.Q<BiomeParticleBurst>("biome-particle-burst");
            Assert.IsNotNull(burst);
            Assert.AreEqual(BiomeParticleBurst.ParticleCount, burst.childCount);
        }

        [Test]
        public void AttachAmbient_CreatesSinglePooledField()
        {
            var host = new VisualElement();
            BiomeAmbientParticles.Attach(host, BiomeParticlePattern.Chat);
            BiomeAmbientParticles.Attach(host, BiomeParticlePattern.Chat);

            Assert.AreEqual(
                1,
                host.Query<BiomeAmbientParticles>().ToList().Count);
        }

        [Test]
        public void AttachAmbient_BuildsExpectedParticlePool()
        {
            var host = new VisualElement();
            var field = BiomeAmbientParticles.Attach(
                host,
                BiomeParticlePattern.Ecosystem);

            Assert.AreEqual(BiomeAmbientParticles.ParticleCount, field.childCount);
            Assert.IsTrue(field.ClassListContains("biome-ambient--ecosystem"));
            Assert.AreEqual(PickingMode.Ignore, field.pickingMode);
        }
    }
}
