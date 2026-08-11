// TDD — JsonFieldReader \uXXXX unicode escape support.
// Real-world case: task subjects contain em dash (U+2014) and Cyrillic text.
// Without the fix, \u2014 returns "u2014" and Cyrillic escapes return garbage.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class JsonFieldReaderTests : UnityMcpTestBase
    {
        // ── Basic string decoding (baseline) ─────────────────────────────────

        [Test]
        public void ReadString_PlainAscii_ReturnsValue()
        {
            Assert.AreEqual("hello", JsonFieldReader.ReadString("{\"k\":\"hello\"}", "k"));
        }

        [Test]
        public void ReadString_EscapedQuote_Decoded()
        {
            Assert.AreEqual("say \"hi\"", JsonFieldReader.ReadString("{\"k\":\"say \\\"hi\\\"\"}", "k"));
        }

        [Test]
        public void ReadString_Newline_Decoded()
        {
            Assert.AreEqual("a\nb", JsonFieldReader.ReadString("{\"k\":\"a\\nb\"}", "k"));
        }

        // ── \uXXXX unicode escapes ────────────────────────────────────────────

        [Test]
        public void ReadString_UnicodeEscape_EmDash_Decodes()
        {
            // Task — Biome: em dash is U+2014. Without fix returns "Task u2014 Biome".
            var json = "{\"title\":\"Task \\u2014 Biome\"}";
            Assert.AreEqual("Task \u2014 Biome", JsonFieldReader.ReadString(json, "title"));
        }

        [Test]
        public void ReadString_UnicodeEscape_CyrillicWord_Decodes()
        {
            // "Задача" fully escaped: \u0417\u0430\u0434\u0430\u0447\u0430
            const string json = "{\"subject\":\"\\u0417\\u0430\\u0434\\u0430\\u0447\\u0430\"}";
            Assert.AreEqual("Задача", JsonFieldReader.ReadString(json, "subject"));
        }

        [Test]
        public void ReadString_UnicodeEscape_MixedCyrillicAndAscii_Decodes()
        {
            // Real session example: mixed unicode and plain ASCII
            const string json = "{\"title\":\"\\u0417\\u0430\\u0434\\u0430\\u0447\\u0430: Biome \\u2014 MCP\"}";
            Assert.AreEqual("Задача: Biome \u2014 MCP", JsonFieldReader.ReadString(json, "title"));
        }

        [Test]
        public void ReadString_UnicodeEscape_Null_HandledGracefully()
        {
            Assert.IsNull(JsonFieldReader.ReadString(null, "k"));
        }

        [Test]
        public void ReadString_MissingKey_ReturnsNull()
        {
            Assert.IsNull(JsonFieldReader.ReadString("{\"other\":\"\\u0041\"}", "k"));
        }
    }
}
