// Cycle 4 TDD — Integration tests for end-to-end set_property chain.
// Exercises: InputNormalizer (Fix 1) → ValueParser enum (Fix 2) via ObjectManager.SetProperty.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetPropertyIntegrationTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SPI_TestObj");
            _go.AddComponent<EnumTestComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // End-to-end: integer "5" → Wrench (gap enum round-trip via ObjectManager)
        [Test]
        public void SetProperty_GapEnum_IntValue_RoundTrip()
        {
            var result = ObjectManager.SetProperty("/SPI_TestObj", "EnumTestComponent", "_toolType", "5");
            StringAssert.Contains("Wrench", result);
            Assert.AreEqual(ToolType.Wrench, _go.GetComponent<EnumTestComponent>()._toolType);
        }

        // End-to-end: "toolType" (no underscore) → InputNormalizer → "_toolType" → Hammer
        [Test]
        public void SetProperty_UnderscorePrefixDropped_StillSets()
        {
            ObjectManager.SetProperty("/SPI_TestObj", "EnumTestComponent", "toolType", "Hammer");
            Assert.AreEqual(ToolType.Hammer, _go.GetComponent<EnumTestComponent>()._toolType);
        }

        // ExecSetProperty via CommandRouter returns "was" clause after T-3
        [Test]
        public void ExecSetProperty_ResultContainsWasClause()
        {
            CommandRouter.RegisterAll();
            ObjectManager.SetProperty("/SPI_TestObj", "EnumTestComponent", "_toolType", "Hammer");
            var result = CommandRouter.ExecuteCommand("set_property",
                "{\"path\":\"/SPI_TestObj\",\"component\":\"EnumTestComponent\",\"prop\":\"_toolType\",\"value\":\"Wrench\"}");
            StringAssert.Contains("was", result);
            StringAssert.Contains("Hammer", result);
            StringAssert.Contains("Wrench", result);
        }

        [Test]
        public void ExecSetProperty_FormatIsEqualWithWas()
        {
            CommandRouter.RegisterAll();
            ObjectManager.SetProperty("/SPI_TestObj", "EnumTestComponent", "_toolType", "Hammer");
            var result = CommandRouter.ExecuteCommand("set_property",
                "{\"path\":\"/SPI_TestObj\",\"component\":\"EnumTestComponent\",\"prop\":\"_toolType\",\"value\":\"Wrench\"}");
            var firstLine = result.Split('\n')[0];
            StringAssert.StartsWith("_toolType = Wrench (was", firstLine);
        }
    }
}
