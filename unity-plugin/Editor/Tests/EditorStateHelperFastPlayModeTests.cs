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
            ProtectEditorPrefBool("UnityMCP_FastPlayMode");
            ProtectEditorPrefBool("UnityMCP_HotReloadMode");
            RegisterCleanup(FastPlayMode.ResetForTest);
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
    }
}
