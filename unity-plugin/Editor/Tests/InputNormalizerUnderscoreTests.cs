// NUnit tests for InputNormalizer.NormalizeProperty — underscore-prefix handling.
// RED tests: verify "toolIndex" → "_toolIndex" fallback and no "m__" double-underscore.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    // Minimal stub with [SerializeField] private int _toolIndex for testing.
    internal class UnderscoreTestComponent : MonoBehaviour
    {
        [SerializeField] private int _toolIndex;
        [SerializeField] private float _speed;
    }

    // Stub WITHOUT _toolIndex — used to test double-underscore guard.
    internal class NoUnderscoreFieldComponent : MonoBehaviour
    {
        public int someOtherField;
    }

    [TestFixture]
    public class InputNormalizerUnderscoreTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("IN_Underscore_TestObj");

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // RED: "toolIndex" (no prefix) → should find "_toolIndex" via new step 4
        [Test]
        public void NormalizeProperty_NoPrefixInput_FindsUnderscoreField()
        {
            var so = new SerializedObject(_go.AddComponent<UnderscoreTestComponent>());
            Assert.AreEqual("_toolIndex", InputNormalizer.NormalizeProperty("toolIndex", so));
        }

        // RED: "ToolIndex" (PascalCase) → should find "_toolIndex" via new step 4
        [Test]
        public void NormalizeProperty_PascalInput_FindsUnderscoreField()
        {
            var so = new SerializedObject(_go.AddComponent<UnderscoreTestComponent>());
            Assert.AreEqual("_toolIndex", InputNormalizer.NormalizeProperty("ToolIndex", so));
        }

        // RED: "_toolIndex" on component WITHOUT _toolIndex → must NOT produce "m__toolIndex"
        [Test]
        public void NormalizeProperty_UnderscoreInput_NoDoubleUnderscore()
        {
            var so = new SerializedObject(_go.AddComponent<NoUnderscoreFieldComponent>());
            var result = InputNormalizer.NormalizeProperty("_toolIndex", so);
            StringAssert.DoesNotContain("m__", result);
        }

        // GREEN regression: "_toolIndex" on component WITH _toolIndex → step 1 hits directly
        [Test]
        public void NormalizeProperty_DirectMatch_Passthrough()
        {
            var so = new SerializedObject(_go.AddComponent<UnderscoreTestComponent>());
            Assert.AreEqual("_toolIndex", InputNormalizer.NormalizeProperty("_toolIndex", so));
        }

        // GREEN regression: "localPosition" in PropertyAliases dict → step 2 returns alias
        [Test]
        public void NormalizeProperty_DictAlias_ReturnsAlias()
        {
            var so = new SerializedObject(_go.GetComponent<Transform>());
            Assert.AreEqual("m_LocalPosition", InputNormalizer.NormalizeProperty("localPosition", so));
        }
    }
}
