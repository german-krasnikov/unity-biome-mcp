// TDD tests for SessionAuthorization — C# defense-in-depth guard.
// Verifies mode-based policy: ask blocks mutations, agent/full-access/null allow all.
using System.Threading.Tasks;
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
        public void Check_AskMode_AllowsUitkFileRead()
        {
            Assert.IsNull(SessionAuthorization.Check(
                "ask", "uitk_file", "{\"action\":\"read\",\"path\":\"Assets/Probe.uxml\"}"));
        }

        [TestCase("write")]
        [TestCase("future_action")]
        public void Check_AskMode_BlocksUitkFileWriteAndUnknownAction(string action)
        {
            var result = SessionAuthorization.Check(
                "ask", "uitk_file", $"{{\"action\":\"{action}\",\"path\":\"Assets/Probe.uxml\"}}");

            StringAssert.Contains("requires agent mode", result);
        }

        [Test]
        public async Task ProcessAsync_AskMode_AllowsUitkFileRead()
        {
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                "{\"id\":\"ask-uitk-read\",\"cmd\":\"uitk_file\",\"args\":{\"action\":\"read\",\"path\":\"Assets/DoesNotExist/AskReadProbe.uxml\"}}",
                tcs,
                "ask");

            var result = await tcs.Task;

            StringAssert.DoesNotContain("requires agent mode", result, result);
            StringAssert.Contains("file not found", result, result);
        }

        [TestCase("write")]
        [TestCase("future_action")]
        public async Task ProcessAsync_AskMode_BlocksUitkFileWriteAndUnknownAction(string action)
        {
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                $"{{\"id\":\"ask-uitk-block\",\"cmd\":\"uitk_file\",\"args\":{{\"action\":\"{action}\",\"path\":\"Assets/DoesNotExist/AskWriteProbe.uxml\",\"content\":\"<ui:UXML xmlns:ui='UnityEngine.UIElements' />\"}}}}",
                tcs,
                "ask");

            var result = await tcs.Task;

            StringAssert.Contains("\"ok\":false", result, result);
            StringAssert.Contains("requires agent mode", result, result);
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
