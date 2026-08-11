using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipSystemPromptTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Schema_ContainsAgentInstruction()
        {
            StringAssert.Contains("[agent:", ChipSystemPrompt.Schema);
            StringAssert.Contains("Agent tool", ChipSystemPrompt.Schema);
            StringAssert.DoesNotContain("Task tool", ChipSystemPrompt.Schema);
        }
    }
}
