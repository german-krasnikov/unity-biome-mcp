// TDD: CommandRouter ReadOnly guard tests.
// Verifies CheckGuards blocks mutating commands when IsReadOnly=true,
// and that get_status emits the readOnly field.
using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterReadOnlyTests : SceneTestBase
    {
        private Func<bool> _savedIsReadOnly;
        private Func<bool> _savedIsPlayMode;

        [SetUp]
        public void SetUpReadOnly()
        {
            _savedIsReadOnly = CommandRouter.IsReadOnly;
            _savedIsPlayMode = CommandRouter.IsPlayMode;
        }

        [TearDown]
        public void TearDownReadOnly()
        {
            CommandRouter.IsReadOnly = _savedIsReadOnly;
            CommandRouter.IsPlayMode = _savedIsPlayMode;
        }

        [Test]
        public void Process_ReadOnly_BlocksMutatingCommand_ReturnsReadOnlyBlocked()
        {
            CommandRouter.IsReadOnly = () => true;
            var result = CommandRouter.Process("{\"id\":\"ro1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"test\"}}");
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("READ_ONLY_BLOCKED"), result);
        }

        [Test]
        public void Process_ReadOnly_AllowsReadCommand_ReturnsPong()
        {
            CommandRouter.IsReadOnly = () => true;
            var result = CommandRouter.Process("{\"id\":\"ro2\",\"cmd\":\"ping\",\"args\":{}}");
            Assert.IsTrue(result.Contains("pong"), result);
            Assert.IsFalse(result.Contains("READ_ONLY_BLOCKED"), result);
        }

        [Test]
        public void Process_NotReadOnly_AllowsMutatingFlow_NoReadOnlyError()
        {
            CommandRouter.IsReadOnly = () => false;
            var result = CommandRouter.Process("{\"id\":\"ro3\",\"cmd\":\"create_object\",\"args\":{\"name\":\"test\"}}");
            Assert.IsFalse(result.Contains("READ_ONLY_BLOCKED"), result);
        }

        [Test]
        public void Process_ReadOnly_AllowsUitkFileRead_ButBlocksWrite()
        {
            CommandRouter.IsReadOnly = () => true;
            const string path = "Assets/DoesNotExist/ReadOnlyProbe.uxml";

            var read = CommandRouter.Process(
                $"{{\"id\":\"ro-uitk-read\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{path}\",\"action\":\"read\"}}}}");
            var write = CommandRouter.Process(
                $"{{\"id\":\"ro-uitk-write\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{path}\",\"action\":\"write\",\"content\":\"x\"}}}}");

            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", read, read);
            Assert.IsTrue(read.Contains("file not found") || read.Contains("escapes Assets"), read);
            StringAssert.Contains("READ_ONLY_BLOCKED", write, write);
        }

        [Test]
        public async Task ProcessAsync_ReadOnly_UsesUitkFileActionMutability()
        {
            CommandRouter.IsReadOnly = () => true;
            const string path = "Assets/DoesNotExist/ReadOnlyAsyncProbe.uxml";

            var readTcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                $"{{\"id\":\"ro-uitk-async-read\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{path}\",\"action\":\"read\"}}}}",
                readTcs);
            var read = await readTcs.Task;

            var writeTcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                $"{{\"id\":\"ro-uitk-async-write\",\"cmd\":\"uitk_file\",\"args\":{{\"path\":\"{path}\",\"action\":\"create_uxml\"}}}}",
                writeTcs);
            var write = await writeTcs.Task;

            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", read, read);
            Assert.IsTrue(read.Contains("file not found") || read.Contains("escapes Assets"), read);
            StringAssert.Contains("READ_ONLY_BLOCKED", write, write);
        }

        [TestCase("execute_code", "{\"code\":\"return null;\"}")]
        [TestCase("screenshot", "{\"output_path\":\"ScreenShots/ReadOnlyProbe.png\"}")]
        [TestCase("wait_until", "{\"path\":\"/A\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\",\"abort_on_fail\":\"true\"}")]
        [TestCase("get_changes", "{\"clear\":\"true\"}")]
        [TestCase("profile", "{\"action\":\"start\"}")]
        [TestCase("profile", "{\"action\":\"stop\"}")]
        public void Process_ReadOnly_BlocksConditionalAndFileMutations(string cmd, string argsJson)
        {
            CommandRouter.IsReadOnly = () => true;
            // Runtime mutations need Play Mode to reach the read-only guard. get_changes
            // is editor state, so keep it in Edit Mode and avoid the earlier Play guard.
            CommandRouter.IsPlayMode = () => cmd != "get_changes";

            var result = CommandRouter.Process(
                $"{{\"id\":\"ro-conditional\",\"cmd\":\"{cmd}\",\"args\":{argsJson}}}");

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
        }

        [TestCase("wait_until", "{\"path\":\"/A\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\",\"abort_on_fail\":\"false\"}")]
        [TestCase("get_changes", "{\"clear\":\"false\"}")]
        [TestCase("profile", "{\"action\":\"status\"}")]
        public void Process_ReadOnly_DoesNotBlockConditionalReads(string cmd, string argsJson)
        {
            CommandRouter.IsReadOnly = () => true;
            CommandRouter.IsPlayMode = () => true;

            if (cmd == "wait_until")
                UnityEngine.TestTools.LogAssert.Expect(
                    UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex(
                        "Command failed: STATE: wait_until requires async dispatch"));

            var result = CommandRouter.Process(
                $"{{\"id\":\"ro-read\",\"cmd\":\"{cmd}\",\"args\":{argsJson}}}");

            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", result, result);
        }

        [TestCase("execute_code", "{\"code\":\"return null;\"}")]
        [TestCase("screenshot", "{\"output_path\":\"ScreenShots/ReadOnlyAsyncProbe.png\"}")]
        [TestCase("wait_until", "{\"path\":\"/A\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\",\"abort_on_fail\":\"true\"}")]
        public async Task ProcessAsync_ReadOnly_BlocksBeforeDispatch(string cmd, string argsJson)
        {
            CommandRouter.IsReadOnly = () => true;
            CommandRouter.IsPlayMode = () => true;
            var tcs = new TaskCompletionSource<string>();

            CommandRouter.ProcessAsync(
                $"{{\"id\":\"ro-async\",\"cmd\":\"{cmd}\",\"args\":{argsJson}}}",
                tcs);
            var result = await tcs.Task;

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
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
        public void Process_ReadOnly_NavMeshUsesActionMutability(string action, bool blocked)
        {
            CommandRouter.IsReadOnly = () => true;
            var result = CommandRouter.Process(
                $"{{\"id\":\"ro-nav\",\"cmd\":\"navmesh\",\"args\":{{\"action\":\"{action}\"}}}}");

            Assert.AreEqual(blocked, result.Contains("READ_ONLY_BLOCKED"), result);
        }

        [TestCase("status", false)]
        [TestCase("bake", true)]
        [TestCase("future", true)]
        public async Task ProcessAsync_ReadOnly_NavMeshUsesActionMutability(
            string action, bool blocked)
        {
            CommandRouter.IsReadOnly = () => true;
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                $"{{\"id\":\"ro-nav-async\",\"cmd\":\"navmesh\",\"args\":{{\"action\":\"{action}\"}}}}",
                tcs);
            var result = await tcs.Task;

            Assert.AreEqual(blocked, result.Contains("READ_ONLY_BLOCKED"), result);
        }
#endif

        [Test]
        public void GetStatus_ContainsReadOnlyField()
        {
            var result = CommandRouter.Process("{\"id\":\"ro4\",\"cmd\":\"get_status\",\"args\":{}}");
            Assert.IsTrue(result.Contains("readOnly="), result);
        }
    }
}
