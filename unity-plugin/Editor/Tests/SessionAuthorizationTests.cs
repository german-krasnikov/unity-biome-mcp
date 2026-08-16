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

        [TestCase("wait_until", "{\"abort_on_fail\":\"false\"}")]
        [TestCase("get_changes", "{\"clear\":\"false\"}")]
        [TestCase("profile", "{\"action\":\"status\"}")]
        [TestCase("profile", "{\"action\":\"analyze\"}")]
        public void Check_AskMode_AllowsConditionalReads(string cmd, string argsJson)
        {
            Assert.IsNull(SessionAuthorization.Check("ask", cmd, argsJson));
        }

        [TestCase("execute_code", "{\"code\":\"return null;\"}")]
        [TestCase("screenshot", "{}")]
        [TestCase("wait_until", "{\"abort_on_fail\":\"true\"}")]
        [TestCase("get_changes", "{\"clear\":\"true\"}")]
        [TestCase("profile", "{\"action\":\"start\"}")]
        [TestCase("profile", "{\"action\":\"stop\"}")]
        public void Check_AskMode_BlocksConditionalAndFileMutations(string cmd, string argsJson)
        {
            StringAssert.Contains(
                "requires agent mode",
                SessionAuthorization.Check("ask", cmd, argsJson));
        }

#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
        [TestCase("sample", false)]
        [TestCase("path", false)]
        [TestCase("raycast", false)]
        [TestCase("status", false)]
        [TestCase("get_settings", false)]
        [TestCase("bake", true)]
        [TestCase("clear", true)]
        [TestCase("set_settings", true)]
        [TestCase("future", true)]
        public void Check_AskMode_NavMeshUsesActionMutability(string action, bool blocked)
        {
            var result = SessionAuthorization.Check(
                "ask", "navmesh", $"{{\"action\":\"{action}\"}}");
            Assert.AreEqual(blocked, result != null, result);
        }
#endif

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
