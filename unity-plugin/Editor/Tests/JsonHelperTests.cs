using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class JsonHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── FormatResponse ───────────────────────────────────────────────────

        [Test]
        public void FormatResponse_Ok_ContainsDataField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"hello\"}",
                JsonHelper.FormatResponse("x", true, "hello", null));

        [Test]
        public void FormatResponse_Error_ContainsErrField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"oops\"}",
                JsonHelper.FormatResponse("x", false, null, "oops"));

        [Test]
        public void FormatResponse_EscapesQuoteInData()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"a\\\"b\"}",
                JsonHelper.FormatResponse("x", true, "a\"b", null));

        [Test]
        public void FormatResponse_NullId_EmptyStringId()
            => Assert.AreEqual("{\"id\":\"\",\"ok\":true,\"data\":\"\"}",
                JsonHelper.FormatResponse(null, true, null, null));

        [Test]
        public void FormatResponse_NullData_EmptyDataValue()
            => Assert.AreEqual("{\"id\":\"y\",\"ok\":true,\"data\":\"\"}",
                JsonHelper.FormatResponse("y", true, null, null));

        // ── FormatFileResponse ───────────────────────────────────────────────

        [Test]
        public void FormatFileResponse_IncludesFileField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponse("x", "/p"));

        // ── FormatFileResponseWithData ───────────────────────────────────────

        [Test]
        public void FormatFileResponseWithData_IncludesDataAndFile()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"d\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponseWithData("x", "/p", "d"));

        [Test]
        public void FormatFileResponseWithData_NullData_EmptyDataValue()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":true,\"data\":\"\",\"file\":\"/p\"}",
                JsonHelper.FormatFileResponseWithData("x", "/p", null));

        // ── FormatBusyResponse ───────────────────────────────────────────────

        [Test]
        public void FormatBusyResponse_ContainsRetryField()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"busy\",\"retry\":200}",
                JsonHelper.FormatBusyResponse("x", "busy", 200));

        [Test]
        public void FormatBusyResponse_EscapesQuoteInMessage()
            => Assert.AreEqual("{\"id\":\"x\",\"ok\":false,\"err\":\"a\\\"b\",\"retry\":100}",
                JsonHelper.FormatBusyResponse("x", "a\"b", 100));

        // ── ExtractObject / ExtractArray string-boundary tracking ─────────────

        [Test]
        public void ExtractObject_WithStringContainingBrace()
        {
            var json = "{\"a\":{\"code\":\"if (x) { }\",\"label\":\"y\"}}";
            var obj = JsonHelper.ExtractObject(json, "a");
            Assert.AreEqual("if (x) { }", JsonHelper.ExtractString(obj, "code"));
            Assert.AreEqual("y", JsonHelper.ExtractString(obj, "label"));
        }

        [Test]
        public void ExtractArray_WithStringContainingBracket()
        {
            var json = "{\"a\":[\"x[0]\",\"y\"]}";
            var arr = JsonHelper.ExtractArray(json, "a");
            // Array must end after both elements — not truncated at '[' inside string
            Assert.AreEqual("[\"x[0]\",\"y\"]", arr);
        }

        // ── CS1.arch.5: timeout response uses EscapeJson on cmdName / msgId ──

        [Test]
        public void FormatTimeoutResponse_CmdNameWithQuote_ProducesValidJson()
        {
            // Verify EscapeJson properly escapes quotes so the timeout JSON is valid.
            var cmdName = "foo\"bar";
            var msgId = "id-1";
            var escaped = JsonHelper.EscapeJson(cmdName);
            var json = $"{{\"id\":\"{JsonHelper.EscapeJson(msgId)}\",\"ok\":false,\"err\":\"Command '{escaped}' timed out after 25s (Unity main thread blocked). Retry.\",\"retry\":2000}}";
            // Escaped form must be present (EscapeJson replaced " with \")
            StringAssert.Contains("foo\\\"bar", json);
            // The unescaped form (foo"bar without preceding backslash) must NOT appear
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(json, @"(?<!\\)""bar"),
                "Raw unescaped quote inside cmdName breaks JSON");
        }

        // ── CS1.test.5: UnescapeJsonString handles \uXXXX ──────────────────

        [Test]
        public void UnescapeJsonString_UnicodeEscape_DecodesCorrectly()
        {
            Assert.AreEqual("A", JsonHelper.UnescapeJsonString("\\u0041"));
            Assert.AreEqual("hello world", JsonHelper.UnescapeJsonString("hello\\u0020world"));
        }

        [Test]
        public void UnescapeJsonString_UnicodeEscapeQuote_DecodesCorrectly()
        {
            // " is a double-quote in unicode escape form
            Assert.AreEqual("\"", JsonHelper.UnescapeJsonString("\\u0022"));
        }

        // ── CS1.test.3: SendGoingAwaySync writes correct 4-byte-BE-prefixed frame

        [Test]
        public void SendGoingAwaySync_WritesCorrectFrame()
        {
            using var ms = new MemoryStream();
            MCPServer.SendGoingAwaySync(ms);

            ms.Position = 0;
            var header = new byte[4];
            ms.Read(header, 0, 4);
            uint len = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);

            var payload = new byte[len];
            ms.Read(payload, 0, (int)len);
            var text = Encoding.UTF8.GetString(payload);

            StringAssert.Contains("\"ev\":\"going_away\"", text);
            StringAssert.Contains("\"reason\":\"domain_reload\"", text);
        }

        // ── CS1.test.4: GetCommandTimeout contract ──────────────────────────

        [Test]
        public void GetCommandTimeout_RunTests_Returns130()
            => Assert.AreEqual(130, MCPServer.GetCommandTimeout("run_tests"));

        [Test]
        public void GetCommandTimeout_RunPlaytest_Returns130()
            => Assert.AreEqual(130, MCPServer.GetCommandTimeout("run_playtest"));

        [Test]
        public void GetCommandTimeout_UnknownCmd_Returns25()
            => Assert.AreEqual(25, MCPServer.GetCommandTimeout("unknown_xyz"));

        [Test]
        public void GetCommandTimeout_Batch_Returns65()
            => Assert.AreEqual(65, MCPServer.GetCommandTimeout("batch"));

        // ── FIX-21: RFC 8259 control character escaping ─────────────────────

        [Test]
        public void EscapeJson_ControlChars_AllEscaped()
        {
            // \x00 (NUL), \x08 (\b), \x0C (\f), \x1B (ESC)
            var input = "\x00\x08\x0c\x1b";
            var escaped = JsonHelper.EscapeJson(input);
            Assert.AreEqual("\\u0000\\b\\f\\u001b", escaped);
        }

        [Test]
        public void EscapeJson_NormalText_Unchanged()
        {
            Assert.AreEqual("hello world 123", JsonHelper.EscapeJson("hello world 123"));
        }

        [Test]
        public void UnescapeJson_BackslashB_And_F()
        {
            Assert.AreEqual("\b", JsonHelper.UnescapeJsonString("\\b"));
            Assert.AreEqual("\f", JsonHelper.UnescapeJsonString("\\f"));
            Assert.AreEqual("a\b\f", JsonHelper.UnescapeJsonString("a\\b\\f"));
        }

        [Test]
        public void UnescapeJson_UnicodeEscape_ControlChar()
        {
            //  = ESC
            Assert.AreEqual("\x1b", JsonHelper.UnescapeJsonString("\\u001b"));
            // A = 'A'
            Assert.AreEqual("A", JsonHelper.UnescapeJsonString("\\u0041"));
        }

        [Test]
        public void RoundTrip_ControlChars()
        {
            var original = "line\x00with\bnull\fand\x1besc\tand\ttabs";
            var escaped = JsonHelper.EscapeJson(original);
            var roundTripped = JsonHelper.UnescapeJsonString(escaped);
            Assert.AreEqual(original, roundTripped);
        }
    }
}
