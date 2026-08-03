// NUnit tests for BatchHelper.ParseLine / ParseLines — CS2.test.4.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BatchHelperParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── ParseLine ─────────────────────────────────────────────────────────

        [Test]
        public void ParseLine_CommandOnly_ReturnsEmptyArgs()
        {
            var (cmd, args) = BatchHelper.ParseLine("create_object");
            Assert.AreEqual("create_object", cmd);
            Assert.AreEqual("{}", args);
        }

        [Test]
        public void ParseLine_UnquotedValue_ExtractedCorrectly()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_active path=/Player value=true");
            Assert.AreEqual("set_active", cmd);
            StringAssert.Contains("\"path\"", args);
            StringAssert.Contains("/Player", args);
            StringAssert.Contains("true", args);
        }

        [Test]
        public void ParseLine_QuotedValue_WithSpaces()
        {
            var (cmd, args) = BatchHelper.ParseLine("create_object name=\"my object\"");
            Assert.AreEqual("create_object", cmd);
            StringAssert.Contains("my object", args);
        }

        [Test]
        public void ParseLine_ParenVector_PreservedAsToken()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_property path=/X value=(1,2,3)");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("(1,2,3)", args);
        }

        [Test]
        public void ParseLine_Empty_ReturnsNullCmd()
        {
            var (cmd, _) = BatchHelper.ParseLine("");
            Assert.IsNull(cmd);
        }

        [Test]
        public void ParseLine_MultipleArgs_AllPresent()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_property path=/A component=Transform prop=m_LocalPosition value=(0,0,0)");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("path", args);
            StringAssert.Contains("component", args);
            StringAssert.Contains("prop", args);
            StringAssert.Contains("value", args);
        }

        // ── ParseLines ────────────────────────────────────────────────────────

        [Test]
        public void ParseLines_SkipsComments()
        {
            var lines = "# this is a comment\ncreate_object name=A";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("create_object", result[0].cmd);
        }

        [Test]
        public void ParseLines_SkipsBlankLines()
        {
            var lines = "\n\ncreate_object name=A\n\n";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void ParseLines_MultipleCommands_AllParsed()
        {
            var lines = "create_object name=A\nset_active path=/A value=false\ndelete_object path=/A";
            var result = BatchHelper.ParseLines(lines);
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("create_object", result[0].cmd);
            Assert.AreEqual("set_active", result[1].cmd);
            Assert.AreEqual("delete_object", result[2].cmd);
        }

        [Test]
        public void ParseLines_NullInput_ReturnsEmpty()
        {
            var result = BatchHelper.ParseLines(null);
            Assert.AreEqual(0, result.Count);
        }

        // ── BUG B: unquoted values with spaces ───────────────────────────────

        [Test]
        public void ParseLine_UnquotedValue_WithSpaces_ConsumesFullValue()
        {
            // Regression: "blue" must NOT appear as a separate key
            var (cmd, args) = BatchHelper.ParseLine(
                "set_property path=/Obj value=Assets/bubble blue small.png component=Image");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("\"Assets/bubble blue small.png\"", args);
            StringAssert.Contains("\"component\"", args);
            StringAssert.Contains("\"Image\"", args);
            StringAssert.DoesNotContain("\"blue\"", args);
        }

        [Test]
        public void ParseLine_UnquotedValue_TrailingSpaceOnly_BaselineUnchanged()
        {
            var (cmd, args) = BatchHelper.ParseLine("set_property path=/Obj value=Assets/Simple.png");
            Assert.AreEqual("set_property", cmd);
            StringAssert.Contains("Assets/Simple.png", args);
        }

        [Test]
        public void ParseLine_UnquotedValue_TwoSpacedTokens()
        {
            var (cmd, args) = BatchHelper.ParseLine("cmd a=foo bar b=baz");
            Assert.AreEqual("cmd", cmd);
            StringAssert.Contains("\"foo bar\"", args);
            StringAssert.Contains("\"baz\"", args);
        }

        [Test]
        public void ParseLine_UnquotedValue_MultipleSpaces()
        {
            var (cmd, args) = BatchHelper.ParseLine(
                "cmd path=/O value=Assets/a b c.png comp=SpriteRenderer");
            Assert.AreEqual("cmd", cmd);
            StringAssert.Contains("\"Assets/a b c.png\"", args);
            StringAssert.Contains("\"SpriteRenderer\"", args);
        }

        // ── Quote-fix tests ────────────────────────────────────────────────────

        /// <summary>BUG B: ParseValue must unescape \" → " so EscapeJson produces one level of escaping.</summary>
        [Test]
        public void ParseValue_EmbeddedEscapedQuote_Unescaped()
        {
            // text as from ExtractString: value="He said \"Hello\""
            // BUG B (old): raw Substring returns He said \"Hello\", EscapeJson triple-escapes → \\\"
            // Fix: StringBuilder returns He said "Hello", EscapeJson → He said \"Hello\" in argsJson
            var (_, args) = BatchHelper.ParseLine("cmd value=\"He said \\\"Hello\\\"\"");
            StringAssert.Contains("He said \\\"Hello\\\"", args);
        }

        /// <summary>BUG B: ParseValue must unescape \\ → \ so EscapeJson produces double backslash.</summary>
        [Test]
        public void ParseValue_EscapedBackslash_Unescaped()
        {
            // text: path="C:\\Windows" (two backslash chars inside)
            // BUG B (old): raw Substring raw-copies \\, EscapeJson → four backslashes
            // Fix: StringBuilder returns C:\Windows, EscapeJson → C:\\Windows in argsJson
            var (_, args) = BatchHelper.ParseLine("cmd path=\"C:\\\\Windows\"");
            StringAssert.Contains("C:\\\\Windows", args);
        }

        /// <summary>BUG A: ParseLines must not call UnescapeJsonString a second time.</summary>
        [Test]
        public void ParseLines_EmbeddedQuoteInValue_Preserved()
        {
            // Simulate what ExtractString returns: already one unescape, so \" are actual chars.
            // BUG A (old): 2nd UnescapeJsonString converts \" → ", corrupting the text to
            //   cmd a="say "hi"" which makes ParseValue exit at the inner " → hi is lost.
            var text = "cmd a=\"say \\\"hi\\\"\"";
            var result = BatchHelper.ParseLines(text);
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("say \\\"hi\\\"", result[0].argsJson);
        }

        /// <summary>Regression guard: simple quoted value (no backslashes) still parses.</summary>
        [Test]
        public void ParseLines_SimpleQuotedValue_Unchanged()
        {
            var text = "create_object name=\"My Object\"";
            var result = BatchHelper.ParseLines(text);
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("My Object", result[0].argsJson);
        }

        /// <summary>Regression guard: unquoted value still parses correctly.</summary>
        [Test]
        public void ParseLines_UnquotedValue_Unchanged()
        {
            var result = BatchHelper.ParseLines("set_active path=/Player value=true");
            StringAssert.Contains("/Player", result[0].argsJson);
            StringAssert.Contains("true", result[0].argsJson);
        }

        /// <summary>Regression guard: no-backslash text is unchanged by single unescape.</summary>
        [Test]
        public void ParseLines_DoesNotDoubleUnescape_SingleQuote()
        {
            var text = "set_property prop=x value=\"Player One\"";
            var result = BatchHelper.ParseLines(text);
            Assert.AreEqual(1, result.Count);
            StringAssert.Contains("Player One", result[0].argsJson);
        }

        // ── Alias expansion integration ────────────────────────────────────────

        [Test]
        public void ParseLine_WithAliasPath_Expands()
        {
            AliasExpander._tableOverride = new System.Collections.Generic.Dictionary<string, string>
                { ["player"] = "/Characters/Player" };
            try
            {
                var (cmd, args) = BatchHelper.ParseLine("get_component path=$player type=Rigidbody");
                Assert.AreEqual("get_component", cmd);
                StringAssert.Contains("/Characters/Player", args);
                StringAssert.DoesNotContain("$player", args);
            }
            finally
            {
                AliasExpander._tableOverride = null;
            }
        }

        // ── ValidateAliases ────────────────────────────────────────────────────

        [Test]
        public void BatchValidateAliases_AllResolved_ReturnsOk()
        {
            AliasExpander._tableOverride = new System.Collections.Generic.Dictionary<string, string>
                { ["hero_path"] = "/Player" };
            try
            {
                var result = BatchHelper.Execute(
                    "ping\nset_property path=$hero_path value=1",
                    "continue", validateAliases: true);
                Assert.AreEqual("ok: all aliases resolved", result);
            }
            finally
            {
                AliasExpander._tableOverride = null;
            }
        }

        [Test]
        public void BatchValidateAliases_UnresolvedAlias_ReturnsUnresolved()
        {
            AliasExpander._tableOverride = new System.Collections.Generic.Dictionary<string, string>();
            try
            {
                var result = BatchHelper.Execute(
                    "set_property path=$unknown_path value=1",
                    "continue", validateAliases: true);
                StringAssert.StartsWith("unresolved:", result);
                StringAssert.Contains("$unknown_path", result);
            }
            finally
            {
                AliasExpander._tableOverride = null;
            }
        }

        [Test]
        public void BatchValidateAliases_MixedResolved_ListsOnlyUnresolved()
        {
            AliasExpander._tableOverride = new System.Collections.Generic.Dictionary<string, string>
                { ["known"] = "/A" };
            try
            {
                var result = BatchHelper.Execute(
                    "cmd path=$known other=$missing",
                    "continue", validateAliases: true);
                StringAssert.StartsWith("unresolved:", result);
                StringAssert.Contains("$missing", result);
                StringAssert.DoesNotContain("$known", result);
            }
            finally
            {
                AliasExpander._tableOverride = null;
            }
        }
    }
}
