using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class GetStatusMutationModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() => HotReloadDetector._overrideForTest = null);
            WriteSessionGuard.ResetForTest();
            RegisterCleanup(() => WriteSessionGuard.ResetForTest());
            HeldTypeStore.Clear();
            RegisterCleanup(() => HeldTypeStore.Clear());
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

        [Test]
        public void GetStatus_ContainsWriteSessionField_WhenInactive()
        {
            // WriteSessionGuard.IsActive is false after ResetForTest() in SetUp
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("write_session=false", result);
        }

        [Test]
        public void GetStatus_ContainsHeldTypesField_WhenEmpty()
        {
            // HeldTypeStore.Count is 0 after Clear() in SetUp
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("held_types=0", result);
        }

        [Test]
        public void GetStatus_ContainsFastPlayModeField_WhenDisabled()
        {
            DeleteEditorPrefBool("UnityMCP_FastPlayMode"); // ensure default false
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("fast_play_mode=false", result);
        }
    }
}
