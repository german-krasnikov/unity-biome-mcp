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
            // Protect MCPSettings mutation mode key (UnityMCP_HotReloadMode)
            ProtectEditorPrefBool("UnityMCP_HotReloadMode");
        }

        [Test]
        public void GetStatus_ContainsMutationModeField_WhenFalse()
        {
            MCPSettings.SetMutationMode(false);
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("mutation_mode=false", result);
        }

        [Test]
        public void GetStatus_ContainsMutationModeField_WhenTrue()
        {
            MCPSettings.SetMutationMode(true);
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("mutation_mode=true", result);
        }

        [Test]
        public void GetStatus_MutationMode_ExternalARDisabled_ConfiguredOff_ReportsFalse()
        {
            // HotReloadDetector.IsActive() would return true (simulating external AR disabled),
            // but mutation_mode must report MCPSettings — the configured state.
            HotReloadDetector._overrideForTest = () => true;
            MCPSettings.SetMutationMode(false);
            var result = CommandRegistry.Execute("get_status", "{}");
            StringAssert.Contains("mutation_mode=false", result,
                "mutation_mode must reflect MCPSettings, not HotReloadDetector.IsActive()");
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
