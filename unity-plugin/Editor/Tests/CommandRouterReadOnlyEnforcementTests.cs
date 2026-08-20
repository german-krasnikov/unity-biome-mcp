// TDD: CommandRouter ReadOnly enforcement — uniform blocking of specific mutation commands.
// MCP-RO-030: complements CommandRouterReadOnlyTests with focused per-command coverage.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterReadOnlyEnforcementTests : SceneTestBase
    {
        private Func<bool> _savedIsReadOnly;
        private Func<bool> _savedIsPlayMode;

        [SetUp]
        public void SetUpReadOnly()
        {
            _savedIsReadOnly = CommandRouter.IsReadOnly;
            _savedIsPlayMode = CommandRouter.IsPlayMode;
            // EditMode tests run without Play Mode — guard against CheckGuards' PlayMode check
            // firing before the ReadOnly check for mutating commands.
            CommandRouter.IsPlayMode = () => false;
        }

        [TearDown]
        public void TearDownReadOnly()
        {
            CommandRouter.IsReadOnly = _savedIsReadOnly;
            CommandRouter.IsPlayMode = _savedIsPlayMode;
        }

        // MCP-RO-030 (Test 7): set_property is a mutating command — must be blocked when
        // IsReadOnly=true, regardless of whether it is the only mutation command tested.
        [Test]
        public void Router_ReadOnly_BlocksSetProperty()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process(
                "{\"id\":\"ro-sp\",\"cmd\":\"set_property\"," +
                "\"args\":{\"path\":\"/TestObj\",\"component\":\"Transform\"," +
                "\"field\":\"position\",\"value\":\"0,0,0\"}}");

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
        }

        // MCP-RO-030 (Test 8): create_object is a mutating command — must be blocked.
        // Mirrors the existing Process_ReadOnly_BlocksMutatingCommand_ReturnsReadOnlyBlocked
        // with an explicit assertion that the uniform enforcement is command-specific.
        [Test]
        public void Router_ReadOnly_BlocksCreateObject()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process(
                "{\"id\":\"ro-co\",\"cmd\":\"create_object\"," +
                "\"args\":{\"name\":\"ReadOnlyProbe\"}}");

            StringAssert.Contains("READ_ONLY_BLOCKED", result, result);
        }

        // MCP-RO-030 (Test 9): get_hierarchy is a read-only command — must be allowed through
        // even when IsReadOnly=true. The response must not contain READ_ONLY_BLOCKED.
        [Test]
        public void Router_ReadOnly_AllowsGetHierarchy()
        {
            CommandRouter.IsReadOnly = () => true;

            var result = CommandRouter.Process(
                "{\"id\":\"ro-gh\",\"cmd\":\"get_hierarchy\"," +
                "\"args\":{\"depth\":\"1\"}}");

            StringAssert.DoesNotContain("READ_ONLY_BLOCKED", result, result);
        }
    }
}
