// TDD: CommandRouter ReadOnly guard tests.
// Verifies CheckGuards blocks mutating commands when IsReadOnly=true,
// and that get_status emits the readOnly field.
using System;
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
        public void GetStatus_ContainsReadOnlyField()
        {
            var result = CommandRouter.Process("{\"id\":\"ro4\",\"cmd\":\"get_status\",\"args\":{}}");
            Assert.IsTrue(result.Contains("readOnly="), result);
        }
    }
}
