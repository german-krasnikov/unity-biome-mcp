// TDD: RelayEventParser — text-format lines from chat_relay.py → ChatEvent.
// One [Test] per event type. Parser is pure static (no Unity deps).
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayEventParserTests
    {
        [Test]
        public void Parse_TextDelta_ReturnsTextDelta()
        {
            var ev = RelayEventParser.Parse("t|Hello world");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.TextDelta, ev.Value.Kind);
            Assert.AreEqual("Hello world", ev.Value.Text);
        }

        [Test]
        public void Parse_TextDelta_PipeInContent_PreservesRemainder()
        {
            var ev = RelayEventParser.Parse("t|foo|bar");
            Assert.IsNotNull(ev);
            Assert.AreEqual("foo|bar", ev.Value.Text);
        }

        [Test]
        public void Parse_ToolCall_ReturnsToolStart()
        {
            var ev = RelayEventParser.Parse("tc|bash|tid1|{\"cmd\":\"ls\"}");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolStart, ev.Value.Kind);
            Assert.AreEqual("bash", ev.Value.Text);
            Assert.AreEqual("tid1", ev.Value.ToolId);
            Assert.AreEqual("{\"cmd\":\"ls\"}", ev.Value.ArgsJson);
        }

        [Test]
        public void Parse_ToolResult_OK_ReturnsToolResult()
        {
            var ev = RelayEventParser.Parse("tr|tid1|true|output text");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolResult, ev.Value.Kind);
            Assert.AreEqual("tid1", ev.Value.ToolId);
            Assert.IsTrue(ev.Value.IsOk);
            Assert.AreEqual("output text", ev.Value.Text);
        }

        [Test]
        public void Parse_ToolResult_Error_SetsIsOkFalse()
        {
            var ev = RelayEventParser.Parse("tr|tid2|false|error msg");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolResult, ev.Value.Kind);
            Assert.IsFalse(ev.Value.IsOk);
        }

        [Test]
        public void Parse_PermissionPrompt_ReturnsPermissionPrompt()
        {
            var ev = RelayEventParser.Parse("pp|bash|req42|{\"cmd\":\"rm\"}");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.PermissionPrompt, ev.Value.Kind);
            Assert.AreEqual("req42", ev.Value.RequestId);
            Assert.AreEqual("bash", ev.Value.Text);
            Assert.AreEqual("{\"cmd\":\"rm\"}", ev.Value.ToolInput);
        }

        [Test]
        public void Parse_AskUser_ReturnsAskUser()
        {
            var ev = RelayEventParser.Parse("au|req1|{\"questions\":[]}");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.AskUser, ev.Value.Kind);
            Assert.AreEqual("req1", ev.Value.RequestId);
            Assert.AreEqual("{\"questions\":[]}", ev.Value.RawJson);
        }

        [Test]
        public void Parse_ToolProgress_ParsesPercentage()
        {
            var ev = RelayEventParser.Parse("tp|42.5|loading...");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolProgress, ev.Value.Kind);
            Assert.AreEqual(42.5f, ev.Value.Percentage, 0.001f);
            Assert.AreEqual("loading...", ev.Value.Text);
        }

        [Test]
        public void Parse_SessionInit_ReturnsSessionInit()
        {
            var ev = RelayEventParser.Parse("si|sess-abc");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.SessionInit, ev.Value.Kind);
            Assert.AreEqual("sess-abc", ev.Value.SessionId);
        }

        [Test]
        public void Parse_TurnDone_ParsesAllFields()
        {
            var ev = RelayEventParser.Parse("d|sess-xyz|0.012|1234|567");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.TurnDone, ev.Value.Kind);
            Assert.AreEqual("sess-xyz", ev.Value.SessionId);
            Assert.AreEqual(0.012f, ev.Value.CostUsd, 0.0001f);
            Assert.AreEqual(1234, ev.Value.InputTokens);
            Assert.AreEqual(567, ev.Value.OutputTokens);
        }

        [Test]
        public void Parse_Error_ReturnsError()
        {
            var ev = RelayEventParser.Parse("e|something went wrong");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.Error, ev.Value.Kind);
            Assert.AreEqual("something went wrong", ev.Value.Text);
        }

        [Test]
        public void Parse_AutoReply_ReturnsAutoReply()
        {
            var ev = RelayEventParser.Parse("ar|{\"jsonrpc\":\"2.0\"}");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.AutoReply, ev.Value.Kind);
            Assert.AreEqual("{\"jsonrpc\":\"2.0\"}", ev.Value.Text);
        }

        [Test]
        public void Parse_RateLimit_ReturnsRateLimit()
        {
            var ev = RelayEventParser.Parse("rl|30");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.RateLimit, ev.Value.Kind);
            Assert.AreEqual("30", ev.Value.Text);
        }

        [Test]
        public void Parse_SessionState_ReturnsSessionState()
        {
            var ev = RelayEventParser.Parse("ss|running");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.SessionState, ev.Value.Kind);
            Assert.AreEqual("running", ev.Value.State);
        }

        [Test]
        public void Parse_Heartbeat_ReturnsHeartbeat()
        {
            var ev = RelayEventParser.Parse("hb|");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.Heartbeat, ev.Value.Kind);
        }

        [Test]
        public void Parse_UnknownPrefix_ReturnsNull()
        {
            var ev = RelayEventParser.Parse("xyz|stuff");
            Assert.IsNull(ev);
        }

        [Test]
        public void Parse_EmptyLine_ReturnsNull()
        {
            Assert.IsNull(RelayEventParser.Parse(""));
            Assert.IsNull(RelayEventParser.Parse(null));
        }

        // ── ToolStart edge cases ───────────────────────────────────────────────

        [Test]
        public void Parse_ToolStart_EmbeddedPipeInArgs_Preserved()
        {
            // SplitN(maxParts=3) must not split the argsJson on its own '|'
            var ev = RelayEventParser.Parse("tc|get_hierarchy|id1|{\"path\":\"Scene|Child\"}");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolStart, ev.Value.Kind);
            Assert.AreEqual("get_hierarchy",          ev.Value.Text);
            Assert.AreEqual("id1",                    ev.Value.ToolId);
            Assert.AreEqual("{\"path\":\"Scene|Child\"}", ev.Value.ArgsJson);
        }

        [Test]
        public void Parse_ToolStart_MissingArgsField_ReturnsNull()
        {
            // tc|name|id — only 2 fields after prefix → not enough → null
            var ev = RelayEventParser.Parse("tc|name|id");
            Assert.IsNull(ev);
        }

        // ── ToolResult edge cases ──────────────────────────────────────────────

        [Test]
        public void Parse_ToolResult_ErrorFlag_TextPreserved()
        {
            var ev = RelayEventParser.Parse("tr|item_1|false|timeout after 30s");
            Assert.IsNotNull(ev);
            Assert.AreEqual(ChatEventKind.ToolResult, ev.Value.Kind);
            Assert.AreEqual("item_1",          ev.Value.ToolId);
            Assert.IsFalse(ev.Value.IsOk);
            Assert.AreEqual("timeout after 30s", ev.Value.Text);
        }

        [Test]
        public void Parse_ToolResult_TextWithEmbeddedNewline_Preserved()
        {
            // Text field may contain actual newlines after de-escaping by RelayChatProcess
            var ev = RelayEventParser.Parse("tr|call_123|true|Main Camera\nHouse");
            Assert.IsNotNull(ev);
            Assert.AreEqual("call_123", ev.Value.ToolId);
            Assert.IsTrue(ev.Value.IsOk);
            Assert.AreEqual("Main Camera\nHouse", ev.Value.Text);
        }

        [Test]
        public void Parse_ToolResult_NoFields_ReturnsNull()
        {
            // tr| alone → rest="" → SplitN yields 1 part → null
            var ev = RelayEventParser.Parse("tr|");
            Assert.IsNull(ev);
        }
    }
}
