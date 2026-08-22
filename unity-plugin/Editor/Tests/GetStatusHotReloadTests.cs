using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetStatusHotReloadTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
        public void GetStatus_ContainsHotReloadDetectedField_WhenFalse()
        {
            HotReloadDetector._overrideForTest = () => false;
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("hot_reload_detected=false", result);
        }

        [Test]
        public void GetStatus_ContainsHotReloadDetectedField_WhenTrue()
        {
            HotReloadDetector._overrideForTest = () => true;
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("hot_reload_detected=true", result);
        }
    }
}
