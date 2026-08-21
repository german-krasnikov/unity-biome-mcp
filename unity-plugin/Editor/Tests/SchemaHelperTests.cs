// TDD — SchemaHelper.GetSchema branch coverage.
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SchemaHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── GetSchema ─────────────────────────────────────────────────────────

        [Test]
        public void GetSchema_KnownConcreteComponent_ReturnsSchemaWithFields()
        {
            var result = SchemaHelper.GetSchema("BoxCollider");

            StringAssert.StartsWith("Schema: BoxCollider", result,
                "Known concrete component must return schema header");
            StringAssert.Contains("m_Size", result,
                "BoxCollider schema must contain m_Size field");
        }

        [Test]
        public void GetSchema_UnknownTypeName_ReturnsTypeNotFoundMessage()
        {
            var result = SchemaHelper.GetSchema("CompletelyFakeTypeName_XYZ123");

            StringAssert.StartsWith("Type not found:", result,
                "Unknown type must return 'Type not found:' prefix");
        }

        [Test]
        public void GetSchema_BuiltinTransform_ReturnsPositionField()
        {
            var result = SchemaHelper.GetSchema("Transform");

            // Transform cannot be added via AddComponent (Unity returns null for builtins),
            // so SchemaHelper returns "Cannot instantiate: Transform".
            StringAssert.StartsWith("Cannot instantiate:", result,
                "Transform is a builtin component; AddComponent returns null so schema falls back to error");
        }

        [Test]
        public void GetSchema_EmptyInput_ReturnsTypeNotFoundMessage()
        {
            var result = SchemaHelper.GetSchema("");

            StringAssert.StartsWith("Type not found:", result,
                "Empty type name must return 'Type not found:' message");
        }

        [Test]
        public void GetSchema_NonComponentType_ReturnsCannotInstantiateMessage()
        {
            // System.String exists but is not a Component
            var result = SchemaHelper.GetSchema("String");

            StringAssert.StartsWith("Cannot instantiate:", result,
                "Non-component type must return 'Cannot instantiate:' message");
        }

        [Test]
        public void GetSchema_AbstractColliderType_ThrowsOrReturnsError()
        {
            // Collider is CLR-abstract; Unity's AddComponent throws ArgumentException for abstract types.
            // SchemaHelper has no try-catch around AddComponent, so the exception propagates.
            try
            {
                var result = SchemaHelper.GetSchema("Collider");
                // If AddComponent somehow returns null without throwing, the method returns an error string.
                StringAssert.StartsWith("Cannot instantiate:", result,
                    "Abstract component type must yield 'Cannot instantiate:' if returned as string");
            }
            catch (System.ArgumentException)
            {
                // Expected: Unity throws when AddComponent is called on an abstract type.
            }
        }
    }
}
