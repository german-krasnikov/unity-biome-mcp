using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Integration: EditorStateHelper.Control("mutation_mode") orchestrates
    /// FastPlayMode + AutoRefreshGuard + MCPSettings atomically.
    /// </summary>
    [TestFixture]
    public class MutationModeIntegrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            FastPlayMode.ResetForTest();
            FastPlayMode._setEnabled = _ => { };
            FastPlayMode._setOptions = _ => { };
            FastPlayMode._getEnabled = () => false;
            FastPlayMode._getOptions = () => EnterPlayModeOptions.None;
            AutoRefreshGuard.ResetForTest();
            AutoRefreshGuard._getAutoRefresh = () => 1;
            AutoRefreshGuard._setAutoRefresh = _ => { };
            HotReloadDetector._cachedPackageInstalled = false;
            MCPSettings.SetMutationMode(false);
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            ProtectEditorPrefBool("UnityMCP_HotReloadMode"); // actual key (backward-compat string in MCPSettings)
            ProtectEditorPrefInt("kAutoRefresh");
            ProtectEditorPrefInt("kAutoRefreshMode");
            RegisterCleanup(FastPlayMode.ResetForTest);
            RegisterCleanup(AutoRefreshGuard.ResetForTest);
            RegisterCleanup(() => HotReloadDetector._cachedPackageInstalled = null);
            RegisterCleanup(() => MCPSettings.SetMutationMode(false));
        }

        [Test]
        public void Enable_ActivatesBothGuards()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(FastPlayMode.IsApplied, "FastPlayMode should be applied");
            Assert.IsTrue(AutoRefreshGuard.IsApplied, "AutoRefreshGuard should be applied");
        }

        [Test]
        public void Enable_SetsMCPSettingsMutationMode()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(MCPSettings.GetMutationMode());
        }

        [Test]
        public void Disable_RestoresBothGuards()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(FastPlayMode.IsApplied, "FastPlayMode should be restored");
            Assert.IsFalse(AutoRefreshGuard.IsApplied, "AutoRefreshGuard should be restored");
        }

        [Test]
        public void Disable_ClearsMCPSettingsMutationMode()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(MCPSettings.GetMutationMode());
        }

        [Test]
        public void Enable_WithoutHotReload_ReturnsWarningLine()
        {
            HotReloadDetector._cachedPackageInstalled = false;
            var result = EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            StringAssert.Contains("warning:no_hot_reload_package", result);
        }

        [Test]
        public void Enable_WithHotReload_NoWarning()
        {
            HotReloadDetector._cachedPackageInstalled = true;
            var result = EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsFalse(result.Contains("warning:"), "No warning when HR installed");
        }
    }
}
