// TDD T2.3a — BashArgsParser: parse command and description from Bash tool argsJson.
//
// Double-red requirement:
//   A — corrupt any Assert → test RED
//   B — stub Parse() to return IsValid=false → all IsValid/field tests RED
//       OR remove "command" extraction → NoDescription/CommandWithQuotes tests RED
//
// Data: real-world Bash tool calls with quoted paths, spaces, Cyrillic, and missing fields.
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests.CLI
{
    [TestFixture]
    public class BashArgsParserTests : UnityMcpTestBase
    {
        // ── Normal: both fields present ────────────────────────────────────────

        [Test]
        public void Parse_CommandAndDescription_BothExtracted()
        {
            // Real Claude Code Bash call: list project files with a human label
            var json = "{\"command\":\"ls -la /Users/german/Work/Python/unity-biome-mcp\",\"description\":\"list project files\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid, "Must be valid when command is present");
            Assert.AreEqual("ls -la /Users/german/Work/Python/unity-biome-mcp", r.Command,
                "Command must be extracted verbatim");
            Assert.AreEqual("list project files", r.Description,
                "Description must be extracted");
        }

        // ── Command with escaped quotes ────────────────────────────────────────

        [Test]
        public void Parse_CommandWithEscapedQuotes_CorrectlyUnescaped()
        {
            // Real Bash call: echo with quoted string
            var json = "{\"command\":\"echo \\\"hello world\\\"\",\"description\":\"greet user\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("echo \"hello world\"", r.Command,
                "Escaped quotes in command must be unescaped by JsonFieldReader");
        }

        // ── Command with spaces in path ────────────────────────────────────────

        [Test]
        public void Parse_CommandWithSpacedPath_PathPreserved()
        {
            // Real Bash call: cat a file whose path contains spaces
            var json = "{\"command\":\"cat \\\"/Users/german/My Documents/project log.txt\\\"\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid, "Valid even with spaced path");
            Assert.AreEqual("cat \"/Users/german/My Documents/project log.txt\"", r.Command,
                "Spaces and quotes in path must be preserved");
        }

        // ── No description: valid, Description=null ────────────────────────────

        [Test]
        public void Parse_NoDescription_IsValidTrue_DescriptionNull()
        {
            // Many Bash calls omit description
            var json = "{\"command\":\"git status\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid, "Valid when only command is present");
            Assert.AreEqual("git status", r.Command);
            Assert.IsNull(r.Description, "Description must be null when absent");
        }

        // ── No command: invalid ────────────────────────────────────────────────

        [Test]
        public void Parse_NoCommand_IsValidFalse()
        {
            // Only description, no command key
            var json = "{\"description\":\"checking something\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsFalse(r.IsValid, "Must be invalid when command key is absent");
        }

        // ── Empty string: invalid ──────────────────────────────────────────────

        [Test]
        public void Parse_EmptyString_IsValidFalse()
        {
            var r = BashArgsParser.Parse("");
            Assert.IsFalse(r.IsValid);
        }

        // ── Null: invalid ──────────────────────────────────────────────────────

        [Test]
        public void Parse_Null_IsValidFalse()
        {
            var r = BashArgsParser.Parse(null);
            Assert.IsFalse(r.IsValid);
        }

        // ── Cyrillic in description and command ────────────────────────────────

        [Test]
        public void Parse_CyrillicDescriptionAndCommand_Preserved()
        {
            // Verify Unicode round-trips through JsonFieldReader
            var json = "{\"command\":\"echo \\\"Привет мир\\\"\",\"description\":\"кириллица в выводе\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual("echo \"Привет мир\"", r.Command,
                "Cyrillic in command must be preserved");
            Assert.AreEqual("кириллица в выводе", r.Description,
                "Cyrillic in description must be preserved");
        }

        // ── Long command: stored in full (truncation is card's job) ────────────

        [Test]
        public void Parse_LongCommand_StoredInFull()
        {
            // 100+ char command without embedded quotes — parser must NOT truncate
            var longCmd = "find /Users/german/Work/Python/unity-biome-mcp/server/src/unity_mcp -maxdepth 1 -name stream_transform.py -exec wc -l {} ;";
            var json = "{\"command\":\"" + longCmd + "\"}";
            var r = BashArgsParser.Parse(json);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(longCmd, r.Command, "Parser must store command in full; card truncates for display");
        }
    }
}
