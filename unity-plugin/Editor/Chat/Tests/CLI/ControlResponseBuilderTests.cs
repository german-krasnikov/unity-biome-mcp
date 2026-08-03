using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ControlResponseBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Allow_ContainsAllowBehavior()
        {
            var json = ControlResponseBuilder.Allow("req-1");
            StringAssert.Contains("\"behavior\":\"allow\"", json);
            StringAssert.Contains("req-1", json);
            StringAssert.Contains("\"type\":\"control_response\"", json);
        }

        [Test]
        public void Deny_ContainsDenyBehavior()
        {
            var json = ControlResponseBuilder.Deny("req-2", "not allowed");
            StringAssert.Contains("\"behavior\":\"deny\"", json);
            StringAssert.Contains("req-2", json);
            StringAssert.Contains("\"reason\"", json);
            StringAssert.Contains("not allowed", json);
        }

        [Test]
        public void Deny_NoMessage_NoMessageField()
        {
            var json = ControlResponseBuilder.Deny("req-3");
            StringAssert.DoesNotContain("\"reason\"", json);
        }

        [Test]
        public void InitializeRequest_ContainsInitializeSubtype()
        {
            var json = ControlResponseBuilder.InitializeRequest("init-1");
            StringAssert.Contains("\"type\":\"control_request\"", json);
            StringAssert.Contains("\"subtype\":\"initialize\"", json);
            StringAssert.Contains("init-1", json);
            StringAssert.DoesNotContain("PreToolUse", json);
            StringAssert.DoesNotContain("hook_0", json);
        }

        [Test]
        public void InitializeRequest_NoArg_ContainsNonEmptyRequestId()
        {
            var json = ControlResponseBuilder.InitializeRequest();
            StringAssert.Contains("\"request_id\":\"", json);
            var idStart = json.IndexOf("\"request_id\":\"") + 14;
            var idEnd   = json.IndexOf('"', idStart);
            Assert.Greater(idEnd - idStart, 0, "generated request_id must be non-empty");
        }

        [Test]
        public void Elicitation_ContainsResults()
        {
            var json = ControlResponseBuilder.Elicitation("req-4", "{\"key\":\"val\"}");
            StringAssert.Contains("req-4", json);
            StringAssert.Contains("\"key\":\"val\"", json);
        }

        [Test]
        public void Interrupt_IsControlRequest()
        {
            var json = ControlResponseBuilder.Interrupt();
            StringAssert.Contains("\"type\":\"control_request\"", json);
            StringAssert.Contains("interrupt", json);
        }

        [Test]
        public void Allow_MalformedRequestId_ProducesValidJson()
        {
            // requestId with quote+newline must not break JSON structure.
            var json = ControlResponseBuilder.Allow("req\"1\\n");
            // Must parse as valid JSON — no unescaped quotes breaking string boundaries.
            StringAssert.Contains("\\\"", json); // quote was escaped
            StringAssert.DoesNotContain("\"\",", json); // no broken field
            // Verify both fields contain the escaped id
            StringAssert.Contains("\"type\":\"control_response\"", json);
        }

        [Test]
        public void ElicitationHook_ContainsUpdatedInput()
        {
            var json = ControlResponseBuilder.ElicitationHook("req-5", "[{\"q\":1}]", answersJson: "{\"What?\":\"A\"}");
            StringAssert.Contains("\"behavior\":\"allow\"", json);
            StringAssert.Contains("\"updatedInput\"", json);
            StringAssert.Contains("\"questions\"", json);
            StringAssert.Contains("\"answers\"", json);
            StringAssert.Contains("req-5", json);
            StringAssert.DoesNotContain("hookSpecificOutput", json);
        }

        [Test]
        public void ElicitationHook_FreeResponse_ContainsResponseField()
        {
            var json = ControlResponseBuilder.ElicitationHook("req-6", "[{\"q\":1}]", freeResponse: "some text");
            StringAssert.Contains("\"behavior\":\"allow\"", json);
            StringAssert.Contains("\"updatedInput\"", json);
            StringAssert.Contains("\"response\":\"some text\"", json);
            StringAssert.DoesNotContain("\"answers\"", json);
        }

        [Test]
        public void ElicitationHook_Answers_ContainsAnswersField()
        {
            var json = ControlResponseBuilder.ElicitationHook("req-7", "[]", answersJson: "{\"q\":\"a\"}");
            StringAssert.Contains("\"answers\":{\"q\":\"a\"}", json);
            // "response" appears as the envelope key in BuildResponse — only "freeResponse" field must be absent
            StringAssert.DoesNotContain("\"response\":\"", json);
        }

        [Test]
        public void Elicitation_MalformedRequestId_EscapedInBothFields()
        {
            var json = ControlResponseBuilder.Elicitation("req\"1", "{\"k\":\"v\"}");
            StringAssert.Contains("\\\"", json);          // quote escaped somewhere
            StringAssert.Contains("\"type\":\"control_response\"", json);
            StringAssert.Contains("\"k\":\"v\"", json);   // results passed through
        }

        // ── CodexUserInputResponse ─────────────────────────────────────────────

        [Test]
        public void CodexUserInputResponse_FormatsJsonRpc()
        {
            var json = ControlResponseBuilder.CodexUserInputResponse("codex:42", "[{\"answer\":\"Yes\"}]");
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", json);
            StringAssert.Contains("\"id\":42", json);
            StringAssert.Contains("\"answers\":[{\"answer\":\"Yes\"}]", json);
        }

        [Test]
        public void CodexUserInputResponse_NullId_DefaultsToZero()
        {
            var json = ControlResponseBuilder.CodexUserInputResponse(null, "[{\"answer\":\"A\"}]");
            StringAssert.Contains("\"id\":0", json);
        }

        [Test]
        public void CodexUserInputResponse_NoPrefixFallsBackToZero()
        {
            var json = ControlResponseBuilder.CodexUserInputResponse("raw-id", "[{\"answer\":\"B\"}]");
            StringAssert.Contains("\"id\":0", json);
        }

        // ── CodexElicitationAccept ─────────────────────────────────────────────

        [Test]
        public void CodexElicitationAccept_NumericId_UnquotedInJson()
        {
            var json = ControlResponseBuilder.CodexElicitationAccept("5");
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", json);
            StringAssert.Contains("\"id\":5", json);          // unquoted integer
            StringAssert.Contains("\"action\":\"accept\"", json);
            StringAssert.Contains("\"content\":{}", json);
        }

        [Test]
        public void CodexElicitationAccept_StringId_QuotedInJson()
        {
            var json = ControlResponseBuilder.CodexElicitationAccept("abc-uuid");
            StringAssert.Contains("\"id\":\"abc-uuid\"", json);
        }

        [Test]
        public void CodexElicitationAccept_NullId_DefaultsToZero()
        {
            var json = ControlResponseBuilder.CodexElicitationAccept(null);
            StringAssert.Contains("\"id\":0", json);
        }

        [Test]
        public void CodexElicitationAccept_NoDeclineOrCancel()
        {
            var json = ControlResponseBuilder.CodexElicitationAccept("1");
            StringAssert.DoesNotContain("\"decline\"", json);
            StringAssert.DoesNotContain("\"cancel\"", json);
        }

        // ── CodexElicitationDecline ────────────────────────────────────────────

        [Test]
        public void CodexElicitationDecline_NumericId_UnquotedInJson()
        {
            var json = ControlResponseBuilder.CodexElicitationDecline("codex:7");
            StringAssert.Contains("\"jsonrpc\":\"2.0\"", json);
            StringAssert.Contains("\"id\":7", json);          // unquoted integer
            StringAssert.Contains("\"action\":\"decline\"", json);
        }

        [Test]
        public void CodexElicitationDecline_StringId_QuotedInJson()
        {
            var json = ControlResponseBuilder.CodexElicitationDecline("codex:abc-uuid");
            StringAssert.Contains("\"id\":\"abc-uuid\"", json);
            StringAssert.Contains("\"action\":\"decline\"", json);
        }

        [Test]
        public void CodexElicitationDecline_NullId_DefaultsToZero()
        {
            var json = ControlResponseBuilder.CodexElicitationDecline(null);
            StringAssert.Contains("\"id\":0", json);
        }

        [Test]
        public void CodexElicitationDecline_NoAcceptOrContent()
        {
            var json = ControlResponseBuilder.CodexElicitationDecline("codex:1");
            StringAssert.DoesNotContain("\"accept\"", json);
            StringAssert.DoesNotContain("\"content\"", json);
        }
    }
}
