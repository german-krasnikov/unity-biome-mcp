// TDD — F19: Tool detail labels preserve full text (no C#-side truncation).
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ToolDetailBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static VisualElement MakeChip() =>
            new VisualElement { name = "tool-chip" };

        [Test]
        public void AttachOrUpdate_ArgsLabel_FullTextPreserved()
        {
            var chip = MakeChip();
            var rec  = new ToolCallRecord("get_hierarchy", "1", "{\"path\":\"/Root\"}");
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-args");
            Assert.IsNotNull(label);
            StringAssert.Contains("path", label.text);
            StringAssert.Contains("/Root", label.text);
        }

        [Test]
        public void AttachOrUpdate_LongArgsJson_FullyPreservedInLabel()
        {
            var chip    = MakeChip();
            string args = "{\"command\":\"" + new string('x', 500) + "\",\"value\":42}";
            var rec     = new ToolCallRecord("set_property", "2", args);
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-args");
            Assert.IsNotNull(label);
            // All 500 'x' chars must survive formatting (they're inside a string literal)
            StringAssert.Contains(new string('x', 500), label.text);
        }

        [Test]
        public void AttachOrUpdate_LongResultText_FullyPreservedInLabel()
        {
            var chip       = MakeChip();
            string longRes = "OK: " + new string('y', 800);
            var rec        = new ToolCallRecord("run_tests", "3", "{}", longRes, true);
            ToolDetailBuilder.AttachOrUpdate(chip, rec);

            var label = chip.Q<Label>(className: "tool-detail-result");
            Assert.IsNotNull(label);
            Assert.AreEqual(longRes, label.text);
        }

        // CH2.test.4: second AttachOrUpdate call updates existing label text (no duplicate)
        [Test]
        public void AttachOrUpdate_CalledTwice_UpdatesLabelNotDuplicate()
        {
            var chip  = MakeChip();
            var rec1  = new ToolCallRecord("my_tool", "id1", "{\"a\":1}");
            var rec2  = new ToolCallRecord("my_tool", "id1", "{\"a\":2}");

            ToolDetailBuilder.AttachOrUpdate(chip, rec1);
            ToolDetailBuilder.AttachOrUpdate(chip, rec2);

            // Must have exactly one args label (no duplicate created by second call)
            var argsLabels = chip.Query<Label>(className: "tool-detail-args").ToList();
            Assert.AreEqual(1, argsLabels.Count, "second call must update existing label, not add a second one");
            StringAssert.Contains("2", argsLabels[0].text, "label text must reflect updated record");
        }

        [Test]
        public void AttachOrUpdate_ResultCall_UpdatesResultLabel()
        {
            var chip  = MakeChip();
            var args  = new ToolCallRecord("my_tool", "id2", "{\"x\":1}");
            var result = new ToolCallRecord("my_tool", "id2", "{\"x\":1}", "first result", true);
            var result2 = new ToolCallRecord("my_tool", "id2", "{\"x\":1}", "updated result", true);

            ToolDetailBuilder.AttachOrUpdate(chip, args);
            ToolDetailBuilder.AttachOrUpdate(chip, result);
            ToolDetailBuilder.AttachOrUpdate(chip, result2);

            var resultLabels = chip.Query<Label>(className: "tool-detail-result").ToList();
            Assert.AreEqual(1, resultLabels.Count, "must not duplicate result label on third call");
            Assert.AreEqual("updated result", resultLabels[0].text);
        }
    }
}
