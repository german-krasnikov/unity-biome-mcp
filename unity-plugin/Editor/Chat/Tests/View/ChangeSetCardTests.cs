// T16: ChangeSetCard unit tests (8 tests).
// Tests render output by querying CSS classes — no resolvedStyle.
using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChangeSetCardTests : UnityMcpTestBase
    {
        private ChangeSetCard _card;

        [SetUp]
        public void SetUp() => _card = new ChangeSetCard();

        private static ToolCallRecord NoResult() =>
            new ToolCallRecord("get_changeset", "id-1", "{}");

        private static ToolCallRecord WithResult(string text) =>
            new ToolCallRecord("get_changeset", "id-1", "{}", text, true);

        private static string MakeText(int opCount = 1)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"cs:abc12345 status:open ops:{opCount} nc:0 nm:{opCount} nd:0"
            };
            for (int i = 0; i < opCount; i++)
                lines.Add($"modify scene_object /Player{i} rev:true");
            return string.Join("\n", lines);
        }

        [Test]
        public void OnUpdate_NoResult_DoesNotRender()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, NoResult());
            Assert.IsFalse(chip.ClassListContains("changeset-rendered"),
                "marker must not be set when HasResult is false");
        }

        [Test]
        public void OnUpdate_WithNoChangeset_DoesNotRender()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, WithResult("no_changeset"));
            Assert.IsFalse(chip.ClassListContains("changeset-rendered"),
                "marker must not be set for 'no_changeset' result");
        }

        [Test]
        public void OnUpdate_WithResult_SetsMarker()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, WithResult(MakeText(1)));
            Assert.IsTrue(chip.ClassListContains("changeset-rendered"),
                "marker must be set after successful render");
        }

        [Test]
        public void OnUpdate_Idempotent_NoDoubleRender()
        {
            var chip = new VisualElement();
            var rec  = WithResult(MakeText(1));
            _card.OnUpdate(chip, rec);
            int firstCount = chip.Query<Label>().ToList().Count;
            _card.OnUpdate(chip, rec);
            int secondCount = chip.Query<Label>().ToList().Count;
            Assert.That(secondCount, Is.EqualTo(firstCount),
                "second OnUpdate must not add more labels");
        }

        [Test]
        public void OnUpdate_RendersHeaderLabel()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, WithResult(MakeText(1)));
            var header = chip.Q<Label>(className: "changeset-header");
            Assert.IsNotNull(header, ".changeset-header Label must exist");
            StringAssert.Contains("abc12345", header.text);
        }

        [Test]
        public void OnUpdate_RendersOpRow_PerOperation()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, WithResult(MakeText(3)));
            var rows = chip.Query(className: "changeset-op-row").ToList();
            Assert.That(rows.Count, Is.EqualTo(3), "3 ops → 3 changeset-op-row elements");
        }

        [Test]
        public void OnUpdate_CollapsesAboveThreshold()
        {
            var chip = new VisualElement();
            _card.OnUpdate(chip, WithResult(MakeText(9)));
            var foldout = chip.Q<Foldout>(className: "changeset-ops-foldout");
            Assert.IsNotNull(foldout, "9 ops → Foldout with class changeset-ops-foldout");
        }

        [Test]
        public void Registered_ForGetChangeset()
        {
            // [InitializeOnLoad] wires registration on domain load.
            var renderer = ToolCardRendererRegistry.Resolve("get_changeset");
            Assert.IsNotNull(renderer, "ToolCardRendererRegistry must have 'get_changeset' after domain load");
        }
    }
}
