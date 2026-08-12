// TDD — T-4.4: AgentCard tests.
// Canonical pattern: construct directly, query by CSS class, no resolvedStyle.
// Registration tests rely on [InitializeOnLoad] static constructor running on domain load.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentCardTests : UnityMcpTestBase
    {
        private AgentCard _card;

        [SetUp]
        public void SetUp() => _card = new AgentCard();

        private const string AgentArgsJson =
            "{\"subagent_type\":\"workflow\",\"description\":\"Search X\"," +
            "\"prompt\":\"Find files\",\"run_in_background\":false}";

        private static ToolCallRecord MakeRec(
            string argsJson, bool withResult = false, string resultText = "ok")
        {
            return withResult
                ? new ToolCallRecord("Agent", "id-1", argsJson, resultText, true)
                : new ToolCallRecord("Agent", "id-1", argsJson);
        }

        // ── Test 1: renders subagent type header ──────────────────────────────────

        [TestCase(false)]
        [TestCase(true)]
        public void OnUpdate_RendersSubagentTypeHeader(bool withResult)
        {
            var chip = new VisualElement();
            var rec  = MakeRec(AgentArgsJson, withResult);

            _card.OnUpdate(chip, rec);

            var labels = chip.Query<Label>().ToList();
            Assert.IsTrue(
                labels.Exists(l => l.text.Contains("workflow")),
                "chip must contain a Label with 'workflow' (the subagent_type)");
        }

        // ── Test 2: renders description ───────────────────────────────────────────

        [TestCase(false)]
        [TestCase(true)]
        public void OnUpdate_RendersDescription(bool withResult)
        {
            var chip = new VisualElement();
            var rec  = MakeRec(AgentArgsJson, withResult);

            _card.OnUpdate(chip, rec);

            var labels = chip.Query<Label>().ToList();
            Assert.IsTrue(
                labels.Exists(l => l.text.Contains("Search X")),
                "chip must contain a Label with description 'Search X'");
        }

        // ── Test 3: missing subagent_type → description only ──────────────────────

        [Test]
        public void OnUpdate_MissingSubagentType_ShowsDescriptionOnly()
        {
            var chip = new VisualElement();
            var args = "{\"description\":\"Search X\",\"prompt\":\"...\"}";
            var rec  = MakeRec(args);

            _card.OnUpdate(chip, rec);

            Assert.IsNull(chip.Q(className: "agent-type"),
                "No .agent-type element when subagent_type is absent from args");
            var descLabel = chip.Q<Label>(className: "agent-desc");
            Assert.IsNotNull(descLabel,
                ".agent-desc Label must exist when only description is present");
        }

        // ── Test 4: no .agent-result section before result arrives; card not empty ─

        [Test]
        public void OnUpdate_BeforeResult_NoResultSection()
        {
            var chip = new VisualElement();
            var rec  = MakeRec(AgentArgsJson, withResult: false);

            _card.OnUpdate(chip, rec);

            Assert.IsNull(chip.Q(className: "agent-result"),
                ".agent-result must not exist when HasResult == false");
            // Card must NOT be empty — shows description at minimum.
            Assert.IsNotNull(chip.Q<Label>(),
                "Card must have at least one Label (description) when args are present");
        }

        // ── Test 5: result section shows summary when result arrives ──────────────

        [Test]
        public void OnUpdate_AfterResult_AppendsSummary()
        {
            var chip = new VisualElement();
            var rec  = MakeRec(AgentArgsJson, withResult: true, resultText: "Found 5 files...");

            _card.OnUpdate(chip, rec);

            var resultLabel = chip.Q<Label>(className: "agent-result");
            Assert.IsNotNull(resultLabel, ".agent-result Label must exist when HasResult == true");
            StringAssert.Contains("Found 5 files", resultLabel.text,
                ".agent-result text must contain the result summary");
        }

        // ── Test 6: long result is truncated to at most 200 display chars ─────────

        [Test]
        public void OnUpdate_LongResult_TruncatedTo200()
        {
            var chip       = new VisualElement();
            var longResult = new string('x', 500);
            var rec        = MakeRec(AgentArgsJson, withResult: true, resultText: longResult);

            _card.OnUpdate(chip, rec);

            var resultLabel = chip.Q<Label>(className: "agent-result");
            Assert.IsNotNull(resultLabel, ".agent-result must exist");
            // 200 content chars + optional 1-char ellipsis = at most 202
            Assert.LessOrEqual(resultLabel.text.Length, 202,
                "Displayed result text must not exceed 200 chars (plus optional ellipsis)");
        }

        // ── Test 7: idempotent — second OnUpdate must not duplicate .agent-desc ───

        [Test]
        public void OnUpdate_Idempotent_SecondCallNoDuplication()
        {
            var chip = new VisualElement();
            var rec  = MakeRec(AgentArgsJson);

            _card.OnUpdate(chip, rec);
            _card.OnUpdate(chip, rec); // simulate second call (args-complete → result stage)

            var count = chip.Query(className: "agent-desc").ToList().Count;
            Assert.AreEqual(1, count,
                "Second OnUpdate call must not duplicate .agent-desc elements");
        }

        // ── Test 8a: null argsJson — no children added, idempotency class not set ─

        [Test]
        public void OnUpdate_NullArgsJson_NoChildrenAndNotRendered()
        {
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Agent", "id-1", null);  // null argsJson

            _card.OnUpdate(chip, rec);

            Assert.AreEqual(0, chip.childCount,
                "null argsJson must not add any children to chip");
            Assert.IsFalse(chip.ClassListContains("agent-rendered"),
                "null argsJson must not mark chip as rendered (prevents future real render)");
        }

        // ── Test 8: "Agent" is registered (192 real calls confirmed) ─────────────

        [Test]
        public void RegistrationCoversAgent()
        {
            Assert.IsNotNull(
                ToolCardRendererRegistry.Resolve("Agent"),
                "ToolCardRendererRegistry must have a renderer registered for 'Agent'");
        }

        // ── Test 9: "Task" is registered as insurance for older SDK versions ──────

        [Test]
        public void RegistrationCoversTaskFallback()
        {
            Assert.IsNotNull(
                ToolCardRendererRegistry.Resolve("Task"),
                "ToolCardRendererRegistry must have a renderer registered for 'Task' (insurance)");
        }

        // ── B6: description with escaped quote must survive extraction ─────────────

        [Test]
        public void OnUpdate_DescriptionWithEscapedQuote_RenderedUnescaped()
        {
            // B6: ExtractStringValue stopped at the first '"' it found after the
            // opening quote, so a value like Run \"tests\" was returned as "Run \"
            // (truncated at the backslash-escaped inner quote).
            var chip = new VisualElement();
            var args = "{\"description\":\"Run \\\"tests\\\"\",\"prompt\":\"...\"}";
            // args json value = {"description":"Run \"tests\"","prompt":"..."}
            // decoded description = Run "tests"

            _card.OnUpdate(chip, new ToolCallRecord("Agent", "id-b6", args));

            var descLabel = chip.Q<Label>(className: "agent-desc");
            Assert.IsNotNull(descLabel, ".agent-desc must be present");
            StringAssert.Contains("Run \"tests\"", descLabel.text,
                "description must include the escaped quote as a real quote character");
        }

        // ── B6b: null-valued field must not return the next key's name ─────────────

        [Test]
        public void OnUpdate_NullValuedSubagentType_NotRendered()
        {
            // B6: ExtractStringValue found the next key's opening '"' as the value
            // start when the actual value was null → returned "description" as type.
            var chip = new VisualElement();
            var args = "{\"subagent_type\":null,\"description\":\"ok\"}";

            _card.OnUpdate(chip, new ToolCallRecord("Agent", "id-b6b", args));

            var typeLabel = chip.Q<Label>(className: "agent-type");
            Assert.IsNull(typeLabel, "subagent_type:null must not render an agent-type label");
        }

        // ── T1.1: empty argsJson must not freeze card (B = fails when fix reverted) ─

        [Test]
        public void OnUpdate_EmptyArgsJson_NoChildrenAndNotRendered()
        {
            // T1.1: empty string "" passes the old "== null" guard, marks chip
            // "agent-rendered", and blocks any future real render.
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("Agent", "id-empty", "");  // empty, not null

            _card.OnUpdate(chip, rec);

            Assert.AreEqual(0, chip.childCount,
                "empty argsJson must add no children");
            Assert.IsFalse(chip.ClassListContains("agent-rendered"),
                "empty argsJson must not mark chip rendered — would prevent a subsequent real render");
        }

        [Test]
        public void OnUpdate_EmptyArgsJson_ThenRealArgs_StillRenders()
        {
            // T1.1: after a first call with empty argsJson, a second call with real
            // data (субагент «workflow», задача «Поиск объекта Игрок в сцене») must render.
            var chip = new VisualElement();
            var realArgs =
                "{\"subagent_type\":\"workflow\",\"description\":\"Поиск объекта Игрок в сцене\"}";

            _card.OnUpdate(chip, new ToolCallRecord("Agent", "id-seq", ""));      // empty first
            _card.OnUpdate(chip, new ToolCallRecord("Agent", "id-seq", realArgs)); // real second

            var descLabel = chip.Q<Label>(className: "agent-desc");
            Assert.IsNotNull(descLabel,
                "chip must render description on second call after first was empty");
            StringAssert.Contains("Поиск объекта Игрок", descLabel.text,
                "description must contain Cyrillic text from real args");
        }
    }
}
