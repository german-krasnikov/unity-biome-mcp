// TDD: RED — AgentEventParser maps JSON AgentEvent lines → ChatEvent.
// Tests run without Unity API deps (pure NUnit EditMode).
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AgentEventParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private AgentEventParser _parser;

        [SetUp]
        public void SetUp() => _parser = new AgentEventParser();

        // ── assistant_delta → TextDelta ──────────────────────────────────────

        [Test]
        public void Parse_AssistantDelta_Returns_TextDelta()
        {
            var json = "{\"kind\":\"assistant_delta\",\"payload\":{\"text\":\"hello\"},\"session_id\":\"s1\"}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.TextDelta));
            Assert.That(ev.Value.Text, Is.EqualTo("hello"));
        }

        // ── thought_delta → Thinking ─────────────────────────────────────────

        [Test]
        public void Parse_ThoughtDelta_Returns_Thinking()
        {
            var json = "{\"kind\":\"thought_delta\",\"payload\":{\"text\":\"thinking...\"},\"session_id\":\"s1\"}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.Thinking));
            Assert.That(ev.Value.Text, Is.EqualTo("thinking..."));
        }

        // ── tool_call_started → ToolStart ────────────────────────────────────

        [Test]
        public void Parse_ToolCallStarted_Returns_ToolStart_With_Args()
        {
            var json = "{\"kind\":\"tool_call_started\",\"payload\":{\"name\":\"bash\",\"id\":\"t1\",\"args\":{\"cmd\":\"ls\"}}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.ToolStart));
            Assert.That(ev.Value.Text, Is.EqualTo("bash"));
            Assert.That(ev.Value.ToolId, Is.EqualTo("t1"));
            Assert.That(ev.Value.ArgsJson, Does.Contain("cmd"));
        }

        // ── tool_call_completed → ToolResult ok=true ─────────────────────────

        [Test]
        public void Parse_ToolCallCompleted_Returns_ToolResult_Ok_True()
        {
            var json = "{\"kind\":\"tool_call_completed\",\"payload\":{\"id\":\"t1\",\"result\":\"output\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.ToolResult));
            Assert.That(ev.Value.ToolId, Is.EqualTo("t1"));
            Assert.That(ev.Value.Text, Is.EqualTo("output"));
            Assert.That(ev.Value.IsOk, Is.True);
        }

        // ── tool_call_failed → ToolResult ok=false ───────────────────────────

        [Test]
        public void Parse_ToolCallFailed_Returns_ToolResult_Ok_False()
        {
            var json = "{\"kind\":\"tool_call_failed\",\"payload\":{\"id\":\"t1\",\"error\":\"fail\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.ToolResult));
            Assert.That(ev.Value.ToolId, Is.EqualTo("t1"));
            Assert.That(ev.Value.IsOk, Is.False);
        }

        // ── session_started → SessionInit ────────────────────────────────────

        [Test]
        public void Parse_SessionStarted_Returns_SessionInit()
        {
            var json = "{\"kind\":\"session_started\",\"payload\":{\"provider_session_id\":\"pid1\"},\"session_id\":\"s1\"}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.SessionInit));
            Assert.That(ev.Value.SessionId, Is.EqualTo("pid1"));
        }

        // ── cost_update → null (accumulated) ────────────────────────────────

        [Test]
        public void Parse_CostUpdate_Returns_Null_Accumulates_State()
        {
            var json = "{\"kind\":\"cost_update\",\"payload\":{\"cost_usd\":0.05,\"input_tokens\":100,\"output_tokens\":50}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Null, "cost_update must return null and accumulate");
        }

        // ── turn_completed after cost_update → TurnDone with cost ────────────

        [Test]
        public void Parse_TurnCompleted_After_CostUpdate_Returns_TurnDone_With_Cost()
        {
            _parser.Parse("{\"kind\":\"cost_update\",\"payload\":{\"cost_usd\":0.05,\"input_tokens\":100,\"output_tokens\":50}}");
            var ev = _parser.Parse("{\"kind\":\"turn_completed\",\"payload\":{},\"session_id\":\"s1\"}");
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.TurnDone));
            Assert.That(ev.Value.CostUsd, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(ev.Value.InputTokens, Is.EqualTo(100));
            Assert.That(ev.Value.OutputTokens, Is.EqualTo(50));
        }

        // ── turn_completed without cost_update → TurnDone zero cost ──────────

        [Test]
        public void Parse_TurnCompleted_Without_CostUpdate_Returns_TurnDone_Zero_Cost()
        {
            var ev = _parser.Parse("{\"kind\":\"turn_completed\",\"payload\":{},\"session_id\":\"s2\"}");
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.TurnDone));
            Assert.That(ev.Value.CostUsd, Is.EqualTo(0f));
            Assert.That(ev.Value.InputTokens, Is.EqualTo(0));
            Assert.That(ev.Value.OutputTokens, Is.EqualTo(0));
        }

        // ── error → Error ────────────────────────────────────────────────────

        [Test]
        public void Parse_Error_Returns_Error()
        {
            var json = "{\"kind\":\"error\",\"payload\":{\"message\":\"oops\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.Error));
            Assert.That(ev.Value.Text, Is.EqualTo("oops"));
        }

        // ── warning with rate_limit code → RateLimit ─────────────────────────

        [Test]
        public void Parse_Warning_RateLimit_Returns_RateLimit()
        {
            var json = "{\"kind\":\"warning\",\"payload\":{\"code\":\"rate_limit\",\"message\":\"slow down\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.RateLimit));
            Assert.That(ev.Value.Text, Is.EqualTo("slow down"));
        }

        // ── capabilities_changed → CapabilitiesChanged ───────────────────────

        [Test]
        public void Parse_CapabilitiesChanged_Returns_CapabilitiesChanged()
        {
            var json = "{\"kind\":\"capabilities_changed\",\"payload\":{\"state\":\"connected\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.CapabilitiesChanged));
            Assert.That(ev.Value.State, Is.EqualTo("connected"));
        }

        // ── plan_step_started → PlanUpdate ──────────────────────────────────

        [Test]
        public void Parse_PlanStepStarted_Returns_PlanUpdate()
        {
            var json = "{\"kind\":\"plan_step_started\",\"payload\":{\"description\":\"step 1\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.PlanUpdate));
            Assert.That(ev.Value.Text, Is.EqualTo("plan_step_started"));
            Assert.That(ev.Value.ArgsJson, Is.EqualTo("step 1"));
        }

        // ── file_change_detected → FileChange ───────────────────────────────

        [Test]
        public void Parse_FileChangeDetected_Returns_FileChange()
        {
            var json = "{\"kind\":\"file_change_detected\",\"payload\":{\"path\":\"/foo/bar.cs\"}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.Kind, Is.EqualTo(ChatEventKind.FileChange));
            Assert.That(ev.Value.Text, Is.EqualTo("/foo/bar.cs"));
        }

        // ── unknown kind → null ──────────────────────────────────────────────

        [Test]
        public void Parse_UnknownKind_Returns_Null()
        {
            var json = "{\"kind\":\"future_event_v99\",\"payload\":{}}";
            var ev = _parser.Parse(json);
            Assert.That(ev, Is.Null);
        }

        // ── malformed JSON → null ────────────────────────────────────────────

        [Test]
        public void Parse_MalformedJson_Returns_Null()
        {
            var ev = _parser.Parse("not json at all");
            Assert.That(ev, Is.Null);
        }

        // ── Reset clears pending cost state ─────────────────────────────────

        [Test]
        public void Reset_Clears_Pending_Cost_State()
        {
            _parser.Parse("{\"kind\":\"cost_update\",\"payload\":{\"cost_usd\":1.5,\"input_tokens\":200,\"output_tokens\":100}}");
            _parser.Reset();
            var ev = _parser.Parse("{\"kind\":\"turn_completed\",\"payload\":{},\"session_id\":\"s3\"}");
            Assert.That(ev, Is.Not.Null);
            Assert.That(ev.Value.CostUsd, Is.EqualTo(0f), "Reset must clear pending cost");
            Assert.That(ev.Value.InputTokens, Is.EqualTo(0));
        }
    }
}
