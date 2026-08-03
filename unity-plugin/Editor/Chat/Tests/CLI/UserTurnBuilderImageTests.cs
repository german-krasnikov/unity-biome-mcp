// TDD tests for UserTurnBuilder multi-image overload.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class UserTurnBuilderImageTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_EmptyImageList_SameAsTextOnly()
        {
            var text   = "hello";
            var noImgs = UserTurnBuilder.Build(text, new List<byte[]>());
            var plain  = UserTurnBuilder.Build(text);
            Assert.AreEqual(plain, noImgs);
        }

        [Test]
        public void Build_NullImageList_SameAsTextOnly()
        {
            var text   = "hello";
            var noImgs = UserTurnBuilder.Build(text, (IReadOnlyList<byte[]>)null);
            var plain  = UserTurnBuilder.Build(text);
            Assert.AreEqual(plain, noImgs);
        }

        [Test]
        public void Build_OneImage_ContainsBase64Block()
        {
            var png  = new byte[] { 1, 2, 3, 4 };
            var json = UserTurnBuilder.Build("Look", new List<byte[]> { png });
            Assert.IsTrue(json.Contains("\"type\":\"image\""));
            Assert.IsTrue(json.Contains("\"media_type\":\"image/png\""));
            Assert.IsTrue(json.Contains(Convert.ToBase64String(png)));
        }

        [Test]
        public void Build_OneImage_ContainsTextBlock()
        {
            var json = UserTurnBuilder.Build("caption", new List<byte[]> { new byte[] { 1 } });
            Assert.IsTrue(json.Contains("caption"));
            Assert.IsTrue(json.Contains("\"type\":\"text\""));
        }

        [Test]
        public void Build_TwoImages_TwoImageBlocks()
        {
            var img1 = new byte[] { 1, 2 };
            var img2 = new byte[] { 3, 4 };
            var json = UserTurnBuilder.Build("two", new List<byte[]> { img1, img2 });

            var count = CountOccurrences(json, "\"type\":\"image\"");
            Assert.AreEqual(2, count, "Must contain exactly 2 image blocks");
            Assert.IsTrue(json.Contains(Convert.ToBase64String(img1)));
            Assert.IsTrue(json.Contains(Convert.ToBase64String(img2)));
        }

        [Test]
        public void Build_MultiImage_EndsWithNewline()
        {
            var json = UserTurnBuilder.Build("x", new List<byte[]> { new byte[] { 1 } });
            Assert.IsTrue(json.EndsWith("\n"));
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0, idx = 0;
            while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
            { count++; idx += pattern.Length; }
            return count;
        }
    }
}
