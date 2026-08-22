using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetStatusMutationModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
            CommandRegistry.Clear();
            CommandRouter.RegisterMetaCommands();
            RegisterCleanup(() =>
            {
                CommandRegistry.Clear();
                CommandRegistry.InitDefaults();
            });
        }

        [Test]
        public void GetStatus_ContainsMutationModeField_WhenFalse()
        {
            HotReloadDetector._overrideForTest = () => false;
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("mutation_mode=false", result);
        }

        [Test]
        public void GetStatus_ContainsMutationModeField_WhenTrue()
        {
            HotReloadDetector._overrideForTest = () => true;
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("mutation_mode=true", result);
        }
    }
}
