// G30: query_state float precision — must return >=4 significant figures (G6).
// Red: G4 gives only 4 sig figs; G6 gives 6. Test asserts >=5 chars after decimal point
// for a precise float value so the test distinguishes G4 from G6.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GameStatePrecisionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class PrecisionComponent : MonoBehaviour
        {
            // Value with many significant digits — G4 rounds to 4, G6 keeps 6
            public float preciseFloat = 1.23456789f;
        }

        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("GSP_Test");
            TrackOwnedObject(_go);
            _go.AddComponent<PrecisionComponent>();
        }

        [Test]
        public void Snapshot_FloatField_ReturnsSixSignificantFigures()
        {
            var path = ComponentSerializer.GetPath(_go);
            var result = GameStateHelper.Snapshot($"{path}|PrecisionComponent|preciseFloat");
            // G4 → "1.235"; G6 → "1.23457" (at least 5 chars after decimal point)
            StringAssert.Contains(".", result);
            var valueLine = result.Split('=');
            Assert.GreaterOrEqual(valueLine.Length, 2);
            var value = valueLine[valueLine.Length - 1].Trim();
            var dotIdx = value.IndexOf('.');
            Assert.Greater(dotIdx, -1, "float value must contain decimal point");
            var decimals = value.Substring(dotIdx + 1).TrimEnd();
            Assert.GreaterOrEqual(decimals.Length, 5,
                $"Expected >=5 decimal digits (G6), got '{value}' with {decimals.Length} decimal digits");
        }
    }
}
