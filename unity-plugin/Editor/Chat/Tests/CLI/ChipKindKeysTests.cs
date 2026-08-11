using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipKindKeysTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Agent_IsLiteral() =>
            Assert.AreEqual("agent", ChipKindKeys.Agent);
    }
}
