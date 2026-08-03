// TDD — CopyMessageUxTests.
// Verifies CopyFlash seam, UserBubbleData invariants,
// and "Copy as sent to LLM" context-menu label (replaces "Show LLM payload").
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class CopyMessageUxTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            CopyFlash.ShowAction = null;
        }

        // --- CopyFlash ---

        // T1: CopyFlash.Show() with null action does not throw.
        [Test]
        public void CopyFlash_Show_NullAction_DoesNotThrow()
        {
            CopyFlash.ShowAction = null;
            Assert.DoesNotThrow(() => CopyFlash.Show());
        }

        // T2: CopyFlash.Show() invokes the registered action.
        [Test]
        public void CopyFlash_Show_InvokesAction()
        {
            bool called = false;
            CopyFlash.ShowAction = () => called = true;
            CopyFlash.Show();
            Assert.IsTrue(called, "ShowAction must be invoked");
        }

        // T3: After clearing ShowAction, Show() is silent.
        [Test]
        public void CopyFlash_Show_AfterClear_IsSilent()
        {
            CopyFlash.ShowAction = () => throw new Exception("must not fire");
            CopyFlash.ShowAction = null;
            Assert.DoesNotThrow(() => CopyFlash.Show());
        }

        // --- UserBubbleData ---

        // T4: Llm and Display hold distinct values.
        [Test]
        public void UserBubbleData_LlmAndDisplay_AreDistinct()
        {
            var d = new UserBubbleData("short", "full/path payload");
            Assert.AreEqual("short",             d.Display);
            Assert.AreEqual("full/path payload", d.Llm);
        }

        // T5: Constructor allows null arguments without throwing.
        [Test]
        public void UserBubbleData_NullArgs_DoNotThrow()
        {
            Assert.DoesNotThrow(() => new UserBubbleData(null, null));
        }

        // --- CopyableText menu label (via test seam) ---

        // T6: BuildUserBubbleMenuItems adds "Copy as sent to LLM", NOT "Show LLM payload".
        [Test]
        public void CopyableText_BuildMenuItems_HasCopyAsSentToLlm()
        {
            var labels = CopyableText.GetUserBubbleMenuLabels();
            CollectionAssert.Contains(labels,   "Copy as sent to LLM",
                "Must expose 'Copy as sent to LLM' label");
            CollectionAssert.DoesNotContain(labels, "Show LLM payload",
                "Old label 'Show LLM payload' must be gone");
        }
    }
}
