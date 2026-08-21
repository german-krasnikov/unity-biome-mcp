// TDD: UnityPreflightHints — Tasks 8 (serialized dictionary), 9 (non-serializable type),
// 10 (Analyze composite + edge cases). Analyze is internal; accessible via InternalsVisibleTo.
using NUnit.Framework;
using UnityMCP.Editor.Roslyn;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UnityPreflightHintsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Task 8: CheckSerializedDictionary ─────────────────────────────────

        [Test]
        public void CheckSerializedDictionary_SerializeField_WithDictionary_ProducesWarn()
        {
            var content = "[SerializeField]\nprivate Dictionary<string, int> myDict;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("WARN", result);
            StringAssert.Contains("serialized_dictionary", result);
        }

        [Test]
        public void CheckSerializedDictionary_SerializeField_NoDictionary_NoWarn()
        {
            var content = "[SerializeField]\nprivate int health;";

            var result = UnityPreflightHints.Analyze(null, content);

            Assert.IsFalse(result.Contains("serialized_dictionary"),
                "Non-dictionary [SerializeField] must not trigger dictionary warning");
        }

        [Test]
        public void CheckSerializedDictionary_NoDictionaryAndNoAttribute_NoWarn()
        {
            // No [SerializeField] — dictionary is not serialized, no warning
            var content = "private Dictionary<string, int> myDict;";

            var result = UnityPreflightHints.Analyze(null, content);

            Assert.IsFalse(result.Contains("serialized_dictionary"));
        }

        [Test]
        public void CheckSerializedDictionary_SerializeFieldComma_AlsoDetects()
        {
            // [SerializeField, HideInInspector] — comma variant also triggers
            var content = "[SerializeField, HideInInspector]\nprivate Dictionary<int, string> d;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("serialized_dictionary", result);
        }

        // ── Task 9: CheckSerializedNonSerializableType ────────────────────────

        [Test]
        public void CheckSerializedNonSerializableType_Interface_ProducesWarn()
        {
            // IEnemy is an interface (matches I[A-Z]\w+)
            var content = "[SerializeField] private IEnemy target;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("WARN", result);
            StringAssert.Contains("non_serializable_type", result);
            StringAssert.Contains("IEnemy", result);
        }

        [Test]
        public void CheckSerializedNonSerializableType_ConcreteClass_NoWarn()
        {
            // Enemy is a concrete class name — no I prefix
            var content = "[SerializeField] private Enemy enemy;";

            var result = UnityPreflightHints.Analyze(null, content);

            Assert.IsFalse(result.Contains("non_serializable_type"));
        }

        [Test]
        public void CheckSerializedNonSerializableType_Primitive_NoWarn()
        {
            var content = "[SerializeField] private int health;";

            var result = UnityPreflightHints.Analyze(null, content);

            Assert.IsFalse(result.Contains("non_serializable_type"));
        }

        [Test]
        public void CheckSerializedNonSerializableType_PublicInterfaceField_AlsoWarns()
        {
            // Access modifier "public" — still detected
            var content = "[SerializeField] public IWeapon weapon;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("non_serializable_type", result);
            StringAssert.Contains("IWeapon", result);
        }

        // ── Task 10: Analyze composite + edge cases ───────────────────────────

        [Test]
        public void Analyze_EmptyContent_ReturnsEmpty()
        {
            var result = UnityPreflightHints.Analyze(null, "");

            Assert.IsEmpty(result);
        }

        [Test]
        public void Analyze_BothIssues_ReturnsBothWarnings()
        {
            var content =
                "[SerializeField]\nprivate Dictionary<string, int> myDict;\n" +
                "[SerializeField] private IEnemy target;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("serialized_dictionary", result);
            StringAssert.Contains("non_serializable_type", result);
        }

        [Test]
        public void Analyze_MessageContainsExactInterfaceName()
        {
            // The warning message should embed the matched interface name
            var content = "[SerializeField] private IDamageable damageable;";

            var result = UnityPreflightHints.Analyze(null, content);

            StringAssert.Contains("IDamageable", result);
        }
    }
}
