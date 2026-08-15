// TDD — RED: these tests fail until UIElementSerializer is implemented.
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UIElementSerializerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private ScratchEditorWindow _win;
        private UIElementSerializer _s;

        [SetUp]
        public void SetUpSerializer()
        {
            _win = CreateOwnedEditorWindow<ScratchEditorWindow>();
            _s = new UIElementSerializer();
        }

        // 1: root with no children → exactly one line (TrimEnd removes trailing newline)
        [Test]
        public void Serialize_EmptyRoot_ReturnsOneLine()
        {
            var root = new VisualElement { name = "root" };
            var result = _s.Serialize(root);
            // TrimEnd() removes trailing whitespace; split gives 1 element for a single line
            Assert.That(result.Split('\n').Length, Is.EqualTo(1));
        }

        // 2: child at depth 1 uses 2-space indent
        [Test]
        public void Serialize_ChildElements_IndentedCorrectly()
        {
            var root = new VisualElement { name = "root" };
            var child = new Label { name = "child" };
            root.Add(child);

            var result = _s.Serialize(root);
            var lines = result.Split('\n');

            // Find the child line — it should start with exactly 2 spaces
            bool foundIndentedChild = false;
            foreach (var line in lines)
                if (line.StartsWith("  ") && line.Contains("child"))
                    foundIndentedChild = true;

            Assert.That(foundIndentedChild, Is.True, "Child element should be indented with 2 spaces");
        }

        // 3: depth=1, grandchild is beyond limit → truncation message appears
        [Test]
        public void Serialize_DepthLimit_TruncatesWithMessage()
        {
            var root = new VisualElement { name = "root" };
            var child = new VisualElement { name = "child" };
            var grandchild = new VisualElement { name = "grandchild" };
            root.Add(child);
            child.Add(grandchild);

            var result = _s.Serialize(root, depth: 1);

            Assert.That(result, Does.Contain("depth limit").Or.Contain("more"));
            Assert.That(result, Does.Not.Contain("grandchild"));
        }

        // 4: #unity-* element hidden by default
        [Test]
        public void Serialize_HidesUnityInternal_ByDefault()
        {
            var root = new VisualElement { name = "root" };
            var internal_el = new VisualElement { name = "#unity-fill" };
            root.Add(internal_el);

            var result = _s.Serialize(root);

            Assert.That(result, Does.Not.Contain("#unity-fill"));
        }

        // 5: includeInternal=true shows #unity-* elements
        [Test]
        public void Serialize_ShowsUnityInternal_WhenEnabled()
        {
            var root = new VisualElement { name = "root" };
            var internal_el = new VisualElement { name = "#unity-fill" };
            root.Add(internal_el);

            var result = _s.Serialize(root, includeInternal: true);

            Assert.That(result, Does.Contain("#unity-fill"));
        }

        // 6: name and classes appear in correct format
        [Test]
        public void Serialize_NameAndClasses_Formatted()
        {
            var el = new VisualElement { name = "my-element" };
            el.AddToClassList("btn");
            el.AddToClassList("primary");

            var result = _s.Serialize(el);

            Assert.That(result, Does.Contain("my-element"));
            Assert.That(result, Does.Contain(".btn"));
            Assert.That(result, Does.Contain(".primary"));
        }

        // 7: ResolveRef returns the serialized element
        [Test]
        public void ResolveRef_ValidRef_ReturnsElement()
        {
            var el = new VisualElement { name = "root" };
            _s.Serialize(el);

            var resolved = _s.ResolveRef("~1");
            Assert.That(resolved, Is.SameAs(el));
        }

        // 8: after re-serialize, refs from previous call that don't exist in new table return null
        [Test]
        public void ResolveRef_StaleAfterReserialize_ThrowsOrNull()
        {
            // First serialize: root + child → ~1=root, ~2=child
            var root = new VisualElement { name = "root" };
            root.Add(new VisualElement { name = "child" });
            _s.Serialize(root);
            Assert.That(_s.ResolveRef("~2"), Is.Not.Null); // precondition: ~2 exists

            // Second serialize: single element only → ~1=solo, ~2 does NOT exist
            var solo = new VisualElement { name = "solo" };
            _s.Serialize(solo);

            // ~2 was in old table, no longer in new table → null
            Assert.That(_s.ResolveRef("~2"), Is.Null);
        }

        // 9: 10-element tree serializes to <800 chars (token budget)
        [Test]
        public void Serialize_TokenBudget_Under800Chars()
        {
            var root = new VisualElement { name = "root" };
            for (int i = 0; i < 9; i++)
                root.Add(new VisualElement { name = $"child{i}" });

            var result = _s.Serialize(root);

            Assert.That(result.Length, Is.LessThanOrEqualTo(800),
                $"Token budget exceeded: {result.Length} chars (expected ≤800)");
        }
    }
}
