using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ArcadePaletteTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Up_MatchesExpectedRGB()
        {
            var c = ArcadePalette.Up;
            Assert.AreEqual(0.227f, c.r, 0.001f);
            Assert.AreEqual(0.824f, c.g, 0.001f);
            Assert.AreEqual(0.624f, c.b, 0.001f);
        }

        [Test]
        public void ForState_Up_ReturnsTealColor() =>
            Assert.AreEqual(ArcadePalette.Up, ArcadePalette.ForState("up"));

        [Test]
        public void ForState_Listen_ReturnsAmberColor() =>
            Assert.AreEqual(ArcadePalette.Listen, ArcadePalette.ForState("listen"));

        [Test]
        public void ForState_Down_ReturnsCrimsonColor() =>
            Assert.AreEqual(ArcadePalette.Down, ArcadePalette.ForState("down"));

        [Test]
        public void ForState_Unknown_ReturnsFallbackColor() =>
            Assert.AreEqual(ArcadePalette.Down, ArcadePalette.ForState("unknown"));
    }
}
