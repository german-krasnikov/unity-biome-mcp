using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserTurnBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_TextOnly_ContainsUserRole()
        {
            var json = UserTurnBuilder.Build("Hello");
            Assert.IsTrue(json.Contains("\"role\":\"user\""));
            Assert.IsTrue(json.Contains("\"type\":\"user\""));
        }

        [Test]
        public void Build_TextOnly_ContainsText()
        {
            var json = UserTurnBuilder.Build("Hello world");
            Assert.IsTrue(json.Contains("Hello world"));
        }

        [Test]
        public void Build_TextOnly_EndsWithNewline()
        {
            var json = UserTurnBuilder.Build("test");
            Assert.IsTrue(json.EndsWith("\n"));
        }

        [Test]
        public void Build_TextWithSpecialChars_Escaped()
        {
            var json = UserTurnBuilder.Build("Say \"hello\"");
            Assert.IsTrue(json.Contains("\\\"hello\\\""));
        }

        [Test]
        public void Build_NullText_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UserTurnBuilder.Build(null));
        }

        [Test]
        public void Build_WithPng_ContainsImageBlock()
        {
            var png = new byte[] { 1, 2, 3, 4 };
            var json = UserTurnBuilder.Build("Look at this", png);
            Assert.IsTrue(json.Contains("\"type\":\"image\""));
            Assert.IsTrue(json.Contains("\"media_type\":\"image/png\""));
            Assert.IsTrue(json.Contains("\"type\":\"base64\""));
            Assert.IsTrue(json.Contains(Convert.ToBase64String(png)));
        }

        [Test]
        public void Build_WithPng_ContainsTextBlock()
        {
            var png = new byte[] { 1, 2, 3 };
            var json = UserTurnBuilder.Build("Look at this", png);
            Assert.IsTrue(json.Contains("Look at this"));
            Assert.IsTrue(json.Contains("\"type\":\"text\""));
        }

        [Test]
        public void Build_NullPng_FallsBackToTextOnly()
        {
            var json = UserTurnBuilder.Build("hello", (byte[])null);
            Assert.IsFalse(json.Contains("\"type\":\"image\""));
        }

        [Test]
        public void Build_EmptyPng_FallsBackToTextOnly()
        {
            var json = UserTurnBuilder.Build("hello", new byte[0]);
            Assert.IsFalse(json.Contains("\"type\":\"image\""));
        }
    }
}
