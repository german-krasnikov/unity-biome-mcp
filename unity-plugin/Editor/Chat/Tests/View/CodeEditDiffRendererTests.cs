// TDD — T-4.1: CodeEditDiffRenderer tests.
// Canonical pattern: instantiate renderer directly, query by CSS class, no resolvedStyle.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CodeEditDiffRendererTests : UnityMcpTestBase
    {
        private CodeEditDiffRenderer _renderer;

        [SetUp]
        public void SetUp() => _renderer = new CodeEditDiffRenderer();

        private static ToolCallRecord MakeRec(string toolName, string argsJson)
            => new ToolCallRecord(toolName, "id-1", argsJson);

        // ── T-4.1 / Test 1 ──────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_EditTool_RendersDiffBlock()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("Edit", "{\"file_path\":\"a.cs\",\"old_string\":\"x\",\"new_string\":\"y\"}");

            _renderer.OnUpdate(chip, rec);

            Assert.IsNotNull(chip.Q(className: "code-diff-block"),
                "OnUpdate should render a .code-diff-block element");
        }

        // ── T-4.1 / Test 2 ──────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_WriteTool_ExpandedByDefault()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("Write", "{\"file_path\":\"b.cs\",\"content\":\"hello\"}");

            _renderer.OnUpdate(chip, rec);

            Assert.IsNull(chip.Q<Foldout>(),
                "Write tool chip must have no Foldout — content always visible");
        }

        // ── T-4.1 / Test 3 ──────────────────────────────────────────────────────

        [Test]
        public void Header_ContainsEditIconAndPath()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("Edit", "{\"file_path\":\"src/Foo.cs\",\"old_string\":\"a\",\"new_string\":\"b\"}");

            _renderer.OnUpdate(chip, rec);

            var header = chip.Q<Label>(className: "diff-header");
            Assert.IsNotNull(header, "diff-header Label must exist");
            StringAssert.Contains("✎", header.text);
            StringAssert.Contains("src/Foo.cs", header.text);
        }

        // ── T-4.1 / Test 4 ──────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_NoShowMoreButton()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("Edit", "{\"file_path\":\"c.cs\",\"old_string\":\"x\",\"new_string\":\"z\"}");

            _renderer.OnUpdate(chip, rec);

            Assert.IsNull(chip.Q<Button>(className: "diff-show-more"),
                "No 'show more' button — max-height scroll is used instead");
        }

        // ── T-4.1 / Test 5 ──────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_Idempotent_SecondCallNoNewChildren()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("Edit", "{\"file_path\":\"d.cs\",\"old_string\":\"a\",\"new_string\":\"b\"}");

            _renderer.OnUpdate(chip, rec);
            _renderer.OnUpdate(chip, rec); // second call: ArgsComplete → Result stage

            int blockCount = chip.Query(className: "code-diff-block").ToList().Count;
            Assert.AreEqual(1, blockCount,
                "guard ClassListContains('diff-rendered') must prevent double-render on second OnUpdate");
        }

        // ── T-4.1 / Test 6 ──────────────────────────────────────────────────────

        [Test]
        public void OnUpdate_NullArgsJson_NoChildren()
        {
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Edit", "id-null", null);

            _renderer.OnUpdate(chip, rec);

            Assert.AreEqual(0, chip.childCount,
                "null ArgsJson must produce no children");
        }

        // ── T-4.1 / Test 7 ──────────────────────────────────────────────────────

        [Test]
        public void MultiEdit_RegisteredInRegistry()
        {
            // [InitializeOnLoad] registers Edit/Write/MultiEdit at domain load.
            // UnityMcpTestBase isolates the registry state via PreserveStateForTests,
            // so registrations from the static ctor are present throughout.
            Assert.IsNotNull(ToolCardRendererRegistry.Resolve("MultiEdit"),
                "MultiEdit must be registered by CodeEditDiffRenderer [InitializeOnLoad]");
        }
    }
}
