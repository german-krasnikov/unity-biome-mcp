using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class EditorStateHelperFastPlayModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            ProtectEditorPrefBool("UnityMCP_HotReloadMode");
            ProtectEditorPrefInt("kAutoRefresh");
            ProtectEditorPrefInt("kAutoRefreshMode");
            RegisterCleanup(FastPlayMode.ResetForTest);
            RegisterCleanup(AutoRefreshGuard.ResetForTest);
            RegisterCleanup(() => HotReloadDetector._cachedPackageInstalled = null);
        }

        [Test]
        public void Control_Enable_CallsFastPlayModeApply()
        {
            var result = EditorStateHelper.Control("fast_play_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(FastPlayMode.IsApplied);
            StringAssert.Contains("fast_play_mode:", result);
        }

        [Test]
        public void Control_Disable_CallsFastPlayModeRestore()
        {
            FastPlayMode.Apply();
            var result = EditorStateHelper.Control("fast_play_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(FastPlayMode.IsApplied);
            StringAssert.Contains("fast_play_mode:", result);
        }

        [Test]
        public void Control_NoEnable_ReturnsCurrentState()
        {
            var result = EditorStateHelper.Control("fast_play_mode", null, null);
            StringAssert.Contains("fast_play_mode:", result);
        }

        [Test]
        public void GetState_IncludesFastPlayModeLine()
        {
            var state = EditorStateHelper.GetState();
            StringAssert.Contains("fast_play_mode:", state);
        }

        [Test]
        public void Control_MutationMode_Enable_SetsMode()
        {
            var result = EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(MCPSettings.GetMutationMode());
            StringAssert.Contains("mutation_mode:", result);
        }

        [Test]
        public void Control_MutationMode_Disable_ClearsMode()
        {
            MCPSettings.SetMutationMode(true);
            var result = EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(MCPSettings.GetMutationMode());
            StringAssert.Contains("mutation_mode:", result);
        }

        [Test]
        public void Control_MutationMode_Enable_AppliesFastPlayMode()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(FastPlayMode.IsApplied);
        }

        [Test]
        public void Control_MutationMode_Enable_AppliesAutoRefreshGuard()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            Assert.IsTrue(AutoRefreshGuard.IsApplied);
        }

        [Test]
        public void Control_MutationMode_Disable_RestoresFastPlayMode()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(FastPlayMode.IsApplied);
        }

        [Test]
        public void Control_MutationMode_Disable_RestoresAutoRefreshGuard()
        {
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"true\"}");
            EditorStateHelper.Control("mutation_mode", null, "{\"enable\":\"false\"}");
            Assert.IsFalse(AutoRefreshGuard.IsApplied);
        }

        // ── WS-MCP-249: ownership ─────────────────────────────────────────────

        [Test]
        public void Control_FastPlayMode_Disable_WhileMutationModeOn_ReturnsError()
        {
            MCPSettings.SetMutationMode(true);
            RegisterCleanup(() => MCPSettings.SetMutationMode(false));
            FastPlayMode.Apply(FastPlayOwner.Mutation);

            var result = EditorStateHelper.Control("fast_play_mode", null, "{\"enable\":\"false\"}");

            StringAssert.StartsWith("err:", result,
                "Disabling Fast Play while Mutation Mode is ON must return an error");
            Assert.IsTrue(FastPlayMode.IsApplied, "Fast Play must remain applied");
        }
    }
}
