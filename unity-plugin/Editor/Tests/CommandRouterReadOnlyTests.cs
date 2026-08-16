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

        [SetUp]
        public void SetUpReadOnly()
        {
            _savedIsReadOnly = CommandRouter.IsReadOnly;
        }

        [TearDown]
        public void TearDownReadOnly()
        {
            CommandRouter.IsReadOnly = _savedIsReadOnly;
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
            StringAssert.Contains("file not found", read, read);
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
            StringAssert.Contains("file not found", read, read);
            StringAssert.Contains("READ_ONLY_BLOCKED", write, write);
        }

        [Test]
        public void GetStatus_ContainsReadOnlyField()
        {
            var result = CommandRouter.Process("{\"id\":\"ro4\",\"cmd\":\"get_status\",\"args\":{}}");
            Assert.IsTrue(result.Contains("readOnly="), result);
        }
    }
}
