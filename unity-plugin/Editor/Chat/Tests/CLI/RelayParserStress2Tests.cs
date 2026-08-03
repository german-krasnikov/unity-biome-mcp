// Monkey tests: RelayEventParser stress — payload extremes, embedded chars,
// tc|/tr|/d| edge cases not covered in RelayEventParserTests or RelayMonkeyTests.
// Pure static method — no Unity deps, no mocks required.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class RelayParserStress2Tests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // Payload size extremes
        [Test] public void Parse_TextDelta_1Char_Preserved()
        { var ev = RelayEventParser.Parse("t|x"); Assert.IsNotNull(ev); Assert.AreEqual("x", ev.Value.Text); }

        [Test] public void Parse_TextDelta_1MB_NoThrow()
        {
            var big = new string('A', 1_000_000); ChatEvent? ev = null;
            Assert.DoesNotThrow(() => ev = RelayEventParser.Parse("t|" + big));
            Assert.IsNotNull(ev); Assert.AreEqual(1_000_000, ev.Value.Text.Length);
        }

        [Test] public void Parse_ToolCall_100KBArgs_Preserved()
        {
            var big = "{\"d\":\"" + new string('x', 98_000) + "\"}";
            var ev = RelayEventParser.Parse($"tc|bash|id1|{big}");
            Assert.IsNotNull(ev); Assert.AreEqual(big, ev.Value.ArgsJson);
        }

        [Test] public void Parse_Error_100KBMessage_LengthPreserved()
        { var ev = RelayEventParser.Parse("e|" + new string('E', 100_000)); Assert.AreEqual(100_000, ev.Value.Text.Length); }

        [Test] public void Parse_RateLimit_100KBText_LengthPreserved()
        { var ev = RelayEventParser.Parse("rl|" + new string('R', 100_000)); Assert.AreEqual(100_000, ev.Value.Text.Length); }

        // Embedded control characters
        [Test] public void Parse_TextDelta_EmbeddedNewline_Preserved()
        { var ev = RelayEventParser.Parse("t|\nhello"); Assert.IsNotNull(ev); Assert.AreEqual("\nhello", ev.Value.Text); }

        [Test] public void Parse_TextDelta_NullByte_DoesNotThrow()
            => Assert.DoesNotThrow(() => RelayEventParser.Parse("t|\0"));

        [Test] public void Parse_TextDelta_CarriageReturn_DoesNotThrow()
            => Assert.DoesNotThrow(() => RelayEventParser.Parse("t|\r"));

        [Test] public void Parse_TextDelta_Tab_Preserved()
        { var ev = RelayEventParser.Parse("t|\thello"); Assert.IsNotNull(ev); Assert.AreEqual("\thello", ev.Value.Text); }

        [Test] public void Parse_TextDelta_OnlyNewline_EventProduced()
        { var ev = RelayEventParser.Parse("t|\n"); Assert.IsNotNull(ev); Assert.AreEqual(ChatEventKind.TextDelta, ev.Value.Kind); }

        // tc| field extremes
        [Test] public void Parse_ToolCall_EmptyToolId_ArgsPreserved()
        {
            var ev = RelayEventParser.Parse("tc|name||{}");
            Assert.IsNotNull(ev); Assert.AreEqual("name", ev.Value.Text); Assert.AreEqual("", ev.Value.ToolId); Assert.AreEqual("{}", ev.Value.ArgsJson);
        }

        [Test] public void Parse_ToolCall_AllFieldsEmpty_EventProduced()
        {
            var ev = RelayEventParser.Parse("tc|||");
            Assert.IsNotNull(ev); Assert.AreEqual("", ev.Value.Text); Assert.AreEqual("", ev.Value.ToolId); Assert.AreEqual("", ev.Value.ArgsJson);
        }

        [Test] public void Parse_ToolCall_SpecialCharsInId_Preserved()
        { var ev = RelayEventParser.Parse("tc|bash|id/path/x|{}"); Assert.IsNotNull(ev); Assert.AreEqual("id/path/x", ev.Value.ToolId); }

        [Test] public void Parse_ToolCall_UnicodeToolName_Parsed()
        { var ev = RelayEventParser.Parse("tc|ツール|id|{}"); Assert.IsNotNull(ev); Assert.AreEqual("ツール", ev.Value.Text); }

        [Test] public void Parse_ToolCall_VeryLongToolName_NoThrow()
            => Assert.DoesNotThrow(() => RelayEventParser.Parse($"tc|{new string('n', 1_000)}|id|{{}}"));

        // tr| edge cases
        [Test] public void Parse_ToolResult_UppercaseTrueIsOkFalse()
        { var ev = RelayEventParser.Parse("tr|id|True|ok"); Assert.IsNotNull(ev); Assert.IsFalse(ev.Value.IsOk); }

        [Test] public void Parse_ToolResult_UppercaseFalseIsOkFalse()
        { var ev = RelayEventParser.Parse("tr|id|FALSE|ok"); Assert.IsNotNull(ev); Assert.IsFalse(ev.Value.IsOk); }

        [Test] public void Parse_ToolResult_EmptyId_EventProduced()
        { var ev = RelayEventParser.Parse("tr||true|text"); Assert.IsNotNull(ev); Assert.AreEqual("", ev.Value.ToolId); Assert.IsTrue(ev.Value.IsOk); }

        [Test] public void Parse_ToolResult_PipeInResultText_Preserved()
        { var ev = RelayEventParser.Parse("tr|id|true|pipe|text"); Assert.IsNotNull(ev); Assert.AreEqual("pipe|text", ev.Value.Text); }

        [Test] public void Parse_ToolResult_1MBResultText_NoThrow()
            => Assert.DoesNotThrow(() => RelayEventParser.Parse($"tr|id|true|{new string('R', 1_000_000)}"));

        // d| TurnDone edge cases
        [Test] public void Parse_TurnDone_MaxIntTokens_Preserved()
        {
            var ev = RelayEventParser.Parse("d|s|0|2147483647|2147483647");
            Assert.IsNotNull(ev); Assert.AreEqual(int.MaxValue, ev.Value.InputTokens); Assert.AreEqual(int.MaxValue, ev.Value.OutputTokens);
        }

        [Test] public void Parse_TurnDone_NegativeTokens_StoredAsIs()
        { var ev = RelayEventParser.Parse("d|s|0|-1|-1"); Assert.IsNotNull(ev); Assert.AreEqual(-1, ev.Value.InputTokens); }

        [Test] public void Parse_TurnDone_EmptySessionId_EventProduced()
        { var ev = RelayEventParser.Parse("d||0|0|0"); Assert.IsNotNull(ev); Assert.AreEqual("", ev.Value.SessionId); }

        [Test] public void Parse_TurnDone_LargeNegativeCost_NoThrow()
            => Assert.DoesNotThrow(() => RelayEventParser.Parse("d|s|-999999.99|0|0"));

        [Test] public void Parse_TurnDone_ScientificNotationCost_Parsed()
        { var ev = RelayEventParser.Parse("d|s|1e-05|10|5"); Assert.IsNotNull(ev); Assert.AreEqual(1e-05f, ev.Value.CostUsd, 1e-06f); }

        // Multi-prefix and structure edge cases
        [Test] public void Parse_Line_OnlyPipe_ReturnsNull()
            => Assert.IsNull(RelayEventParser.Parse("|"));

        [Test] public void Parse_Line_DoublePipe_TextIsPipe()
        {
            // "t||" → prefix="t", rest="|" → TextDelta("|")
            var ev = RelayEventParser.Parse("t||");
            Assert.IsNotNull(ev); Assert.AreEqual(ChatEventKind.TextDelta, ev.Value.Kind); Assert.AreEqual("|", ev.Value.Text);
        }

        [Test] public void Parse_Line_ManyPipes_tc_AllCapturedInArgs()
        {
            // SplitN("n|id|a|b|c|d", '|', 3) → ["n","id","a|b|c|d"]
            var ev = RelayEventParser.Parse("tc|n|id|a|b|c|d");
            Assert.IsNotNull(ev); Assert.AreEqual("a|b|c|d", ev.Value.ArgsJson);
        }

        [Test] public void Parse_Line_PrefixWithLeadingDigit_ReturnsNull()
            => Assert.IsNull(RelayEventParser.Parse("1t|data"));

        [Test] public void Parse_Line_WhitespacePrefix_ReturnsNull()
            => Assert.IsNull(RelayEventParser.Parse(" t|data"));
    }
}
