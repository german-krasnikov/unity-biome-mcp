using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionTokenParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static PositionedChip PC(int offset)
            => new PositionedChip(new ChipData(ChipKindKeys.Hierarchy, "/x", "x", 0), offset);

        // 1. "@Ma" cursor=3 → atIndex=0, query="Ma"
        [Test]
        public void BasicToken_AtStart()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("@Ma", 3, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(0));
            Assert.That(q,  Is.EqualTo("Ma"));
        }

        // 2. "hello @Ma" cursor=9 → atIndex=6, query="Ma"
        [Test]
        public void BasicToken_AfterSpace()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("hello @Ma", 9, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(6));
            Assert.That(q,  Is.EqualTo("Ma"));
        }

        // 3. "hello@world" cursor=11 → false (no whitespace before @)
        [Test]
        public void RejectsEmail_NoWhitespaceBefore()
        {
            Assert.IsFalse(MentionTokenParser.TryExtract("hello@world", 11, Empty(), out _, out _));
        }

        // 4. "@@" cursor=2 → false
        [Test]
        public void RejectsDoubleAt()
        {
            Assert.IsFalse(MentionTokenParser.TryExtract("@@", 2, Empty(), out _, out _));
        }

        // 5. "@#test" cursor=6 → false
        [Test]
        public void RejectsSpecialCharAfterAt()
        {
            Assert.IsFalse(MentionTokenParser.TryExtract("@#test", 6, Empty(), out _, out _));
        }

        // 6. "@" cursor=1 → atIndex=0, query=""
        [Test]
        public void EmptyQuery_BareAt()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("@", 1, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(0));
            Assert.That(q,  Is.EqualTo(""));
        }

        // 7. "@Ma" cursor=1 → atIndex=0, query=""
        [Test]
        public void CursorBetweenAtAndQuery()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("@Ma", 1, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(0));
            Assert.That(q,  Is.EqualTo(""));
        }

        // 8. chip at offset 0, "@Player " cursor=8 → false (already resolved)
        [Test]
        public void ResolvedChip_AtSameOffset_ReturnsFalse()
        {
            var chips = new List<PositionedChip> { PC(0) };
            Assert.IsFalse(MentionTokenParser.TryExtract("@Player ", 8, chips, out _, out _));
        }

        // 9. "@Cube text @Light" cursor=17 → query="Light"
        [Test]
        public void MultipleAt_PicksNearestToCursor()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("@Cube text @Light", 17, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(11));
            Assert.That(q,  Is.EqualTo("Light"));
        }

        // 10. "@Враг" cursor=5 → query="Враг"
        [Test]
        public void Unicode_CyrillicQuery()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("@Враг", 5, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(0));
            Assert.That(q,  Is.EqualTo("Враг"));
        }

        // 11. "text\n@Ma" cursor=8 → atIndex=5, query="Ma"
        [Test]
        public void NewlineBeforeAt()
        {
            Assert.IsTrue(MentionTokenParser.TryExtract("text\n@Ma", 8, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(5));
            Assert.That(q,  Is.EqualTo("Ma"));
        }

        // 12. null/empty → false
        [Test]
        public void NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(MentionTokenParser.TryExtract(null,  0, Empty(), out _, out _));
            Assert.IsFalse(MentionTokenParser.TryExtract("",    0, Empty(), out _, out _));
        }

        // 13. Cursor at text.Length (UIToolkit ChangeEvent fallback position) works correctly.
        [Test]
        public void CursorAtTextLength_ExtractsQuery()
        {
            string text = "@Camera";
            Assert.IsTrue(MentionTokenParser.TryExtract(text, text.Length, Empty(), out int at, out string q));
            Assert.That(at, Is.EqualTo(0));
            Assert.That(q,  Is.EqualTo("Camera"));
        }

        private static IReadOnlyList<PositionedChip> Empty()
            => new List<PositionedChip>();
    }
}
