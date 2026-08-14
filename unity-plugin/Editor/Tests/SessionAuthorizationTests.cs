// TDD tests for SessionAuthorization — C# defense-in-depth guard.
// Verifies mode-based policy: ask blocks mutations, agent/full-access/null allow all.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SessionAuthorizationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Check_EmptyChatMode_AllowsMutation()
        {
            Assert.IsNull(SessionAuthorization.Check("", "set_property"));
        }

        [Test]
        public void Check_NullChatMode_AllowsMutation()
        {
            Assert.IsNull(SessionAuthorization.Check(null, "set_property"));
        }

        [Test]
        public void Check_AskMode_BlocksMutation()
        {
            Assert.IsNotNull(SessionAuthorization.Check("ask", "set_property"));
        }

        [Test]
        public void Check_AskMode_AllowsRead()
        {
            Assert.IsNull(SessionAuthorization.Check("ask", "get_hierarchy"));
        }

        [Test]
        public void Check_AgentMode_AllowsMutation()
        {
            Assert.IsNull(SessionAuthorization.Check("agent", "set_property"));
        }

        [Test]
        public void Check_FullAccess_AllowsDeleteObject()
        {
            Assert.IsNull(SessionAuthorization.Check("full-access", "delete_object"));
        }
    }
}
