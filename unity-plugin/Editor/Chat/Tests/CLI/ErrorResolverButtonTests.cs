// TDD: ErrorResolverButton — error grouping, dedup, prompt building (F1).
// Pure C# tests — no TCP, no Unity scene required.
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ErrorResolverButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── GroupErrors ────────────────────────────────────────────────────

        [Test]
        public void GroupErrors_SingleLine_ReturnsLine()
        {
            var result = ErrorResolverButton.GroupErrors("NRE at foo\nstack trace here");
            StringAssert.Contains("NRE at foo", result);
        }

        [Test]
        public void GroupErrors_DuplicateMessages_CollapseCount()
        {
            var raw = "NRE\n\nNRE\n\nNRE";
            var result = ErrorResolverButton.GroupErrors(raw);
            StringAssert.Contains("(x3)", result);
        }

        [Test]
        public void GroupErrors_DifferentMessages_TwoGroups()
        {
            var raw = "Error A\nError B";
            var result = ErrorResolverButton.GroupErrors(raw);
            StringAssert.Contains("Error A", result);
            StringAssert.Contains("Error B", result);
        }

        [Test]
        public void GroupErrors_EmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual("", ErrorResolverButton.GroupErrors(""));
        }

        // ── BuildPrompt ────────────────────────────────────────────────────

        [Test]
        public void BuildPrompt_BestPractices_ContainsPrefix()
        {
            var result = ErrorResolverButton.BuildPrompt("some error", "best_practices");
            StringAssert.StartsWith(
                "Fix these Unity runtime errors following SOLID/Unity best practices:", result);
        }

        [Test]
        public void BuildPrompt_QuickFix_ContainsErrors()
        {
            var result = ErrorResolverButton.BuildPrompt("Error A\nError B", "quick_fix");
            StringAssert.Contains("Error A", result);
            StringAssert.Contains("Error B", result);
        }

        [Test]
        public void BuildPrompt_QuickFix_IncludesGroupedCount()
        {
            var raw = "NRE\n\nNRE\n\nNRE";
            var grouped = ErrorResolverButton.GroupErrors(raw);
            var result = ErrorResolverButton.BuildPrompt(grouped, "quick_fix");
            StringAssert.Contains("(x3)", result);
        }

        [Test]
        public void MenuOnly_IsTrue()
            => Assert.IsTrue(new ErrorResolverButton().MenuOnly, "Fix Errors button must live in hamburger menu only");
    }
}
