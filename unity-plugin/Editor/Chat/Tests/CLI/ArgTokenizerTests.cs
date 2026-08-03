// TDD tests for ArgTokenizer — shell-style quoted-value tokenizer.
// Pure unit tests, no Unity API required.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ArgTokenizerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Split_Empty_ReturnsEmpty()
        {
            var result = ArgTokenizer.Split("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Split_Null_ReturnsEmpty()
        {
            var result = ArgTokenizer.Split(null);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Split_SimpleFlags_ReturnsTwoTokens()
        {
            var result = ArgTokenizer.Split("--a --b");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--a", result[0]);
            Assert.AreEqual("--b", result[1]);
        }

        [Test]
        public void Split_DoubleQuotedValue_BecomesOneToken()
        {
            // "--sys" and "be terse" must be exactly 2 tokens (not 3).
            var result = ArgTokenizer.Split("--sys \"be terse\"");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--sys", result[0]);
            Assert.AreEqual("be terse", result[1]);
        }

        [Test]
        public void Split_SingleQuotedValue_BecomesOneToken()
        {
            var result = ArgTokenizer.Split("--sys 'be terse'");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--sys", result[0]);
            Assert.AreEqual("be terse", result[1]);
        }

        [Test]
        public void Split_MultipleSpaces_Collapse()
        {
            var result = ArgTokenizer.Split("  --debug   --quiet  ");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--debug", result[0]);
            Assert.AreEqual("--quiet", result[1]);
        }

        [Test]
        public void Split_TabSeparators_Treated_AsWhitespace()
        {
            var result = ArgTokenizer.Split("--a\t--b");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--a", result[0]);
            Assert.AreEqual("--b", result[1]);
        }

        [Test]
        public void Split_UnbalancedDoubleQuote_RestIsOneToken()
        {
            // "--x" + unbalanced "open → two tokens, second = "open"
            var result = ArgTokenizer.Split("--x \"open");
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("--x",  result[0]);
            Assert.AreEqual("open", result[1]);
        }

        [Test]
        public void Split_QuotedValueWithInternalSpaces_StripsQuotes()
        {
            var result = ArgTokenizer.Split("\"hello world\"");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("hello world", result[0]);
        }

        [Test]
        public void Split_MultipleQuotedValues_EachIsOneToken()
        {
            var result = ArgTokenizer.Split("--append-system-prompt \"be terse\" --model \"claude-3-5\"");
            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("--append-system-prompt", result[0]);
            Assert.AreEqual("be terse",               result[1]);
            Assert.AreEqual("--model",                result[2]);
            Assert.AreEqual("claude-3-5",             result[3]);
        }

        [Test]
        public void Split_OnlyWhitespace_ReturnsEmpty()
        {
            var result = ArgTokenizer.Split("   \t   ");
            Assert.AreEqual(0, result.Count);
        }
    }
}
