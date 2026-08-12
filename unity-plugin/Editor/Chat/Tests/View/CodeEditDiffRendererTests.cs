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

        // ── C2: primary-pass retry contract ──────────────────────────────────────
        //
        // Double-red:
        //   RED A: corrupt Assert.IsFalse → immediate failure.
        //   RED B: revert CodeEditDiffRenderer to set "diff-rendered" marker BEFORE
        //          RenderEdits (old IToolCardRenderer OnUpdate pattern) → seam fires after
        //          marker is set → chip.ClassListContains("diff-rendered") returns TRUE →
        //          Assert.IsFalse FAILS.

        [Test]
        public void OnUpdate_RenderEditsThrows_MarkerNotSetAllowsRetry()
        {
            CodeEditDiffRenderer._renderEditsException =
                new System.InvalidOperationException("simulated RenderEdits failure");
            RegisterCleanup(() => { CodeEditDiffRenderer._renderEditsException = null; });

            var chip = new VisualElement();
            var rec  = MakeRec("Edit",
                "{\"file_path\":\"Foo.cs\",\"old_string\":\"x\",\"new_string\":\"y\"}");

            // In the RED state the exception propagates (old OnUpdate, no try/catch around it).
            // In the GREEN state ToolCardBase catches it before the marker is set.
            try { _renderer.OnUpdate(chip, rec); } catch { }

            Assert.IsFalse(chip.ClassListContains("diff-rendered"),
                "Marker must NOT be set when RenderEdits throws. " +
                "Bug: marker placed before RenderEdits — card frozen, no retry.");
            Assert.IsNull(chip.Q(className: "code-diff-block"),
                "No diff block after failed primary build");

            // Retry: clear seam, next call must render successfully.
            CodeEditDiffRenderer._renderEditsException = null;
            _renderer.OnUpdate(chip, rec);

            Assert.IsTrue(chip.ClassListContains("diff-rendered"),
                "Marker must be set after successful retry");
            Assert.IsNotNull(chip.Q(className: "code-diff-block"),
                "Diff block must be present after retry");
        }

        // ── Collapse threshold (≥5 edits → Foldout) ─────────────────────────

        private static string MakeEditsJson(int count)
        {
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"old_string\":\"line" + i + "\",\"new_string\":\"LINE" + i + "\"}");
            }
            sb.Append(']');
            return sb.ToString();
        }

        [Test]
        public void OnUpdate_FiveEdits_RendersCollapsedFoldout()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("MultiEdit",
                "{\"file_path\":\"many.cs\",\"edits\":" + MakeEditsJson(5) + "}");

            _renderer.OnUpdate(chip, rec);

            var foldout = chip.Q<Foldout>(className: "diff-edits-foldout");
            Assert.IsNotNull(foldout,
                "five or more edits must collapse into a Foldout with class diff-edits-foldout");
            Assert.IsFalse(foldout.value,
                "collapsed foldout must start closed (value=false)");
        }

        [Test]
        public void OnUpdate_FourEdits_NoFoldout()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("MultiEdit",
                "{\"file_path\":\"few.cs\",\"edits\":" + MakeEditsJson(4) + "}");

            _renderer.OnUpdate(chip, rec);

            Assert.IsNull(chip.Q<Foldout>(),
                "fewer than five edits must NOT produce a Foldout");
        }

        [Test]
        public void OnUpdate_TenEdits_FoldoutLabelShowsCount()
        {
            var chip = new VisualElement();
            var rec  = MakeRec("MultiEdit",
                "{\"file_path\":\"big.cs\",\"edits\":" + MakeEditsJson(10) + "}");

            _renderer.OnUpdate(chip, rec);

            var foldout = chip.Q<Foldout>(className: "diff-edits-foldout");
            Assert.IsNotNull(foldout, "ten edits must produce a collapsed Foldout");
            StringAssert.Contains("10", foldout.text,
                "foldout label must include the edit count");
        }
    }
}
