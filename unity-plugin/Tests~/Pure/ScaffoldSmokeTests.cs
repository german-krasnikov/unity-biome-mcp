using NUnit.Framework;

namespace UnityMCP.Playtest.Core.PureTests
{
    // D15 scaffold: proves the pure dotnet-test lane compiles and runs the Core
    // sources with zero Unity install. Not a Core-behavior test — that starts
    // with D16 (corpus parse) and D17 (Compare parity table).
    [TestFixture]
    public class ScaffoldSmokeTests
    {
        [Test]
        public void Scaffold_Compiles_And_OneAssertPasses()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
