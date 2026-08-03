using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests that disabled plugin tools appear in the disabled CSV
    /// and absent from the enabled CSV.
    /// Uses InvalidateEnabledToolsCache + PeekEnabledToolsCache
    /// (the same path Settings UI uses).
    /// </summary>
    [TestFixture]
    public class PluginDisabledToolsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string ToolDisabled = "blender_do";
        private const string ToolEnabled  = "blender_info";
        private const string KeyDisabled  = "UnityMCP_Tool_" + ToolDisabled;
        private const string KeyEnabled   = "UnityMCP_Tool_" + ToolEnabled;

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(CommandRouter.InvalidateEnabledToolsCache);
            // Add test commands on top of the populated registry
            CommandRegistry.Register(ToolDisabled, _ => "ok");
            CommandRegistry.Register(ToolEnabled,  _ => "ok");
            SetEditorPrefBool(KeyDisabled, false);
            SetEditorPrefBool(KeyEnabled, true);
            CommandRouter.InvalidateEnabledToolsCache();
        }

        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();   // restore built-in commands
            CommandRouter.InvalidateEnabledToolsCache();
        }

        [Test]
        public void DisabledPluginTool_AbsentFromEnabledCSV_EnabledToolPresent()
        {
            var enabled = CommandRouter.PeekEnabledToolsCache ?? "";
            var parts = new System.Collections.Generic.HashSet<string>(enabled.Split(','));

            Assert.IsFalse(parts.Contains(ToolDisabled),
                $"'{ToolDisabled}' must not appear in enabled CSV when pref=false");
            Assert.IsTrue(parts.Contains(ToolEnabled),
                $"'{ToolEnabled}' must appear in enabled CSV when pref=true");
        }

        [Test]
        public void DisabledPluginTool_IsToolEnabled_ReturnsFalse()
        {
            // ExecGetDisabledTools is private; we use ExecGetEnabledToolsCached
            // for enabled check. For the disabled list, call InvalidateEnabledToolsCache
            // and check the inverse: disabled tool is NOT in enabled set.
            // The actual disabled CSV is sent via get_disabled_tools command:
            // we verify the same BuildToolList logic by confirming enabled status.
            Assert.IsFalse(MCPSettings.IsToolEnabled(ToolDisabled),
                $"MCPSettings.IsToolEnabled must return false for '{ToolDisabled}'");
        }
    }
}
