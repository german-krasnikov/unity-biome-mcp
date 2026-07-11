using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AskUserCardTests
    {
        private const string SingleSelectJson =
            "{\"questions\":[{\"question\":\"Which approach?\",\"header\":\"Approach\"," +
            "\"options\":[{\"label\":\"Fast\"},{\"label\":\"Thorough\"}],\"multiSelect\":false}]}";

        private const string MultiSelectJson =
            "{\"questions\":[{\"question\":\"Pick features\",\"header\":\"Features\"," +
            "\"options\":[{\"label\":\"Auth\"},{\"label\":\"Payments\"}],\"multiSelect\":true}]}";

        private const string NoOptionsJson =
            "{\"questions\":[{\"question\":\"Describe goal\"}]}";

        private const string EmptyQuestionsJson = "{\"questions\":[]}";

        private AskUserCard MakeCard(string rawJson, Action<string> cb = null)
            => new AskUserCard("req-elix", rawJson, cb ?? (_ => { }));

        // ClickSubmit targets the named submit button (pills also use Button)
        private static void ClickSubmit(AskUserCard card)
        {
            var submit = card.Q<Button>(className: "ask-card__submit");
            ((Action)submit.userData)?.Invoke();
        }

        // ── rendering ────────────────────────────────────────────────────────────

        [Test]
        public void Card_SingleQuestion_RendersPillButtons()
        {
            var card = MakeCard(SingleSelectJson);
            var pills = card.Query<Button>(className: "ask-pill").ToList();
            Assert.GreaterOrEqual(pills.Count, 2, "Single-select should render at least 2 pill buttons");
        }

        [Test]
        public void Card_MultiSelect_RendersCheckboxes()
        {
            var card = MakeCard(MultiSelectJson);
            Assert.GreaterOrEqual(card.Query<Toggle>().ToList().Count, 2,
                "MultiSelect question should render at least 2 checkboxes");
        }

        [Test]
        public void Card_NoOptions_RendersTextField()
        {
            var card = MakeCard(NoOptionsJson);
            Assert.IsNotNull(card.Q<TextField>(), "Free-text question should render a TextField");
        }

        // ── auto-submit (SingleSelect) ────────────────────────────────────────────

        [Test]
        public void Card_SingleSelect_SubmitButtonHidden()
        {
            var card = MakeCard(SingleSelectJson);
            var submit = card.Q<Button>(className: "ask-card__submit");
            Assert.AreEqual(DisplayStyle.None, submit.style.display.value,
                "Submit button should be hidden for single-question single-select");
        }

        [Test]
        public void Card_SingleSelect_PillClick_AutoSubmits()
        {
            string received = null;
            var card = MakeCard(SingleSelectJson, json => received = json);
            var pills = card.Query<Button>(className: "ask-pill").ToList();
            ((Action)pills[0].userData)?.Invoke();
            Assert.IsNotNull(received, "Pill click should auto-submit");
        }

        [Test]
        public void Card_SingleSelect_PillClick_ReturnsCorrectLabel()
        {
            string received = null;
            var card = MakeCard(SingleSelectJson, json => received = json);
            var pills = card.Query<Button>(className: "ask-pill").ToList();
            ((Action)pills[1].userData)?.Invoke(); // "Thorough"
            Assert.IsNotNull(received);
            StringAssert.Contains("Thorough", received);
        }

        // ── submit ────────────────────────────────────────────────────────────────

        [Test]
        public void Card_Submit_DisablesAllInputs_MultiSelect()
        {
            var card = MakeCard(MultiSelectJson);
            ClickSubmit(card);
            foreach (var t in card.Query<Toggle>().ToList())
                Assert.IsFalse(t.enabledSelf, $"Toggle '{t.label}' should be disabled after submit");
        }

        [Test]
        public void Card_Submit_CallsOnSubmitWithResultsJson()
        {
            string received = null;
            var card = MakeCard(NoOptionsJson, json => received = json);
            ClickSubmit(card);

            Assert.IsNotNull(received, "onSubmit callback must be called");
            StringAssert.Contains("\"behavior\":\"allow\"", received);
            StringAssert.Contains("updatedInput", received);
            StringAssert.Contains("control_response", received);
        }

        [Test]
        public void Card_EmptyQuestions_RendersFallbackTextField()
        {
            var card = MakeCard(EmptyQuestionsJson);
            Assert.IsNotNull(card.Q<TextField>(), "Empty questions → fallback TextField expected");
        }

        [Test]
        public void Card_DoubleSubmit_SecondClickIsNoop()
        {
            int callCount = 0;
            var card = MakeCard(NoOptionsJson, _ => callCount++);
            ClickSubmit(card);
            ClickSubmit(card);
            Assert.AreEqual(1, callCount, "second submit must be ignored after resolve");
        }

        [Test]
        public void Card_NullRawJson_RendersFallbackTextField()
        {
            var card = MakeCard(null);
            Assert.IsNotNull(card.Q<TextField>(), "null rawJson → fallback TextField expected");
        }

        [Test]
        public void Card_Submit_OtherFieldFilled_SendsFreeResponseNotAnswers()
        {
            string received = null;
            var card = MakeCard(MultiSelectJson, json => received = json);
            var fields = card.Query<TextField>().ToList();
            fields[fields.Count - 1].value = "my custom answer";
            ClickSubmit(card);

            Assert.IsNotNull(received);
            StringAssert.Contains("\"response\":\"my custom answer\"", received);
            StringAssert.DoesNotContain("\"answers\"", received);
        }

        // ── directAnswer=true (MCP path) ─────────────────────────────────────────

        [Test]
        public void Card_DirectAnswer_SkipsElicitationHook_NoControlResponse()
        {
            string received = null;
            var card = new AskUserCard("req-mcp", NoOptionsJson, json => received = json, directAnswer: true);
            ClickSubmit(card);

            Assert.IsNotNull(received);
            StringAssert.DoesNotContain("control_response", received,
                "MCP path must NOT wrap in ElicitationHook");
        }

        [Test]
        public void Card_DirectAnswer_OtherField_ReturnsFreeText()
        {
            string received = null;
            var card = new AskUserCard("req-mcp", MultiSelectJson, json => received = json, directAnswer: true);
            var fields = card.Query<TextField>().ToList();
            fields[fields.Count - 1].value = "free answer";
            ClickSubmit(card);

            Assert.AreEqual("free answer", received,
                "directAnswer + other field = raw free text, not ElicitationHook JSON");
        }

        [Test]
        public void Card_DirectAnswer_NoOtherField_ReturnsAnswersMapJson()
        {
            string received = null;
            var card = new AskUserCard("req-mcp", NoOptionsJson, json => received = json, directAnswer: true);
            ClickSubmit(card);

            Assert.IsNotNull(received);
            StringAssert.StartsWith("{", received, "Should be answers map JSON");
        }

        [Test]
        public void Card_DirectAnswer_SingleSelect_ReturnsSelectedLabel()
        {
            string received = null;
            var card = new AskUserCard("req", SingleSelectJson, json => received = json, directAnswer: true);
            var pills = card.Query<Button>(className: "ask-pill").ToList();
            ((Action)pills[0].userData)?.Invoke(); // click "Fast" → auto-submit
            Assert.IsNotNull(received);
            StringAssert.Contains("Fast", received);
        }

        [Test]
        public void Card_DirectAnswer_MultiSelect_ReturnsSelectedLabels()
        {
            string received = null;
            var card = new AskUserCard("req", MultiSelectJson, json => received = json, directAnswer: true);
            var toggles = card.Query<Toggle>().ToList();
            toggles[0].value = true;
            toggles[1].value = true;
            ClickSubmit(card);
            StringAssert.Contains("Auth", received);
            StringAssert.Contains("Payments", received);
        }

        // ── Codex requestUserInput path ───────────────────────────────────────────

        [Test]
        public void Card_CodexPrefix_ReturnsJsonRpcResponse()
        {
            string received = null;
            var card = new AskUserCard("codex:99", SingleSelectJson, json => received = json);
            var pills = card.Query<Button>(className: "ask-pill").ToList();
            ((Action)pills[0].userData)?.Invoke(); // click "Fast"
            Assert.IsNotNull(received);
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", received);
            StringAssert.Contains("\"id\":99", received);
            StringAssert.Contains("\"answer\":\"Fast\"", received);
        }

        [Test]
        public void Card_CodexPrefix_OtherField_ReturnsJsonRpcOtherAnswer()
        {
            string received = null;
            var card = new AskUserCard("codex:5", MultiSelectJson, json => received = json);
            var fields = card.Query<TextField>().ToList();
            fields[fields.Count - 1].value = "custom";
            ClickSubmit(card);
            Assert.IsNotNull(received);
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", received);
            StringAssert.Contains("\"id\":5", received);
            StringAssert.Contains("\"answer\":\"custom\"", received);
        }
    }
}
