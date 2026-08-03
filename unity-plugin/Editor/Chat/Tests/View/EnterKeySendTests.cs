// Tests for EnterKeyLogic pure helpers. NUnit-testable.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class EnterKeySendTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Classify_Return_NoAlt_Send()
        {
            Assert.AreEqual(EnterAction.Send, EnterKeyLogic.Classify(true, false));
        }

        [Test]
        public void Classify_Return_Alt_Newline()
        {
            Assert.AreEqual(EnterAction.Newline, EnterKeyLogic.Classify(true, true));
        }

        [Test]
        public void Classify_KeypadEnter_NoAlt_Send()
        {
            // isReturnOrKeypadEnter=true covers both Return and KeypadEnter
            Assert.AreEqual(EnterAction.Send, EnterKeyLogic.Classify(true, false));
        }

        [Test]
        public void Classify_NotReturn_Ignore()
        {
            Assert.AreEqual(EnterAction.Ignore, EnterKeyLogic.Classify(false, false));
            Assert.AreEqual(EnterAction.Ignore, EnterKeyLogic.Classify(false, true));
        }

        [Test]
        public void InsertNewline_AtEnd_AppendsAndAdvancesCaret()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("hello", 5);
            Assert.AreEqual("hello\n", text);
            Assert.AreEqual(6, caret);
        }

        [Test]
        public void InsertNewline_MidString_InsertsAtCaret()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("ab", 1);
            Assert.AreEqual("a\nb", text);
            Assert.AreEqual(2, caret);
        }

        [Test]
        public void InsertNewline_EmptyString_CaretOne()
        {
            var (text, caret) = EnterKeyLogic.InsertNewline("", 0);
            Assert.AreEqual("\n", text);
            Assert.AreEqual(1, caret);
        }

        // ── DecideEnter dedup (Bug 2) ─────────────────────────────────────────

        [Test]
        public void DecideEnter_NotHandled_NoAlt_ReturnsSendAndSuppress()
        {
            var (action, suppress) = EnterKeyLogic.DecideEnter(altHeld: false, alreadyHandled: false);
            Assert.AreEqual(EnterAction.Send, action);
            Assert.IsTrue(suppress);
        }

        [Test]
        public void DecideEnter_NotHandled_Alt_ReturnsNewlineAndSuppress()
        {
            var (action, suppress) = EnterKeyLogic.DecideEnter(altHeld: true, alreadyHandled: false);
            Assert.AreEqual(EnterAction.Newline, action);
            Assert.IsTrue(suppress);
        }

        [Test]
        public void DecideEnter_AlreadyHandled_NoAlt_ReturnsIgnoreAndSuppress()
        {
            // Second keyDown (char echo '\n') must be suppressed, not re-acted on.
            var (action, suppress) = EnterKeyLogic.DecideEnter(altHeld: false, alreadyHandled: true);
            Assert.AreEqual(EnterAction.Ignore, action);
            Assert.IsTrue(suppress);
        }

        [Test]
        public void DecideEnter_AlreadyHandled_Alt_ReturnsIgnoreAndSuppress()
        {
            // Alt+Enter echo also suppressed — no double newline.
            var (action, suppress) = EnterKeyLogic.DecideEnter(altHeld: true, alreadyHandled: true);
            Assert.AreEqual(EnterAction.Ignore, action);
            Assert.IsTrue(suppress);
        }

        // ── IsEnterChar (Bug 2) ───────────────────────────────────────────────

        [Test]
        public void IsEnterChar_Newline_ReturnsTrue()
        {
            Assert.IsTrue(EnterKeyLogic.IsEnterChar('\n'));
        }

        [Test]
        public void IsEnterChar_CarriageReturn_ReturnsTrue()
        {
            Assert.IsTrue(EnterKeyLogic.IsEnterChar('\r'));
        }

        [Test]
        public void IsEnterChar_RegularChar_ReturnsFalse()
        {
            Assert.IsFalse(EnterKeyLogic.IsEnterChar('a'));
        }

        [Test]
        public void IsEnterChar_NullChar_ReturnsFalse()
        {
            Assert.IsFalse(EnterKeyLogic.IsEnterChar('\0'));
        }
    }
}
