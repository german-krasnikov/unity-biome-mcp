// TDD tests for SceneAsset allowlisting in ChatChipPolicy.
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatChipPolicySceneTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => DeleteEditorPrefBool("MCPChat.ChipAllow.SceneAsset");

        [TearDown]
        public void TearDown() => DeleteEditorPrefBool("MCPChat.ChipAllow.SceneAsset");

        [Test]
        public void SceneAsset_Allowed_ByDefault()
            => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(SceneAsset)));

        [Test]
        public void SceneAsset_PrefKey_IsSceneAsset()
            => Assert.AreEqual("MCPChat.ChipAllow.SceneAsset", ChatChipPolicy.PrefKey("SceneAsset"));

        [Test]
        public void SceneAsset_DisabledByPref_Rejected()
        {
            SetEditorPrefBool("MCPChat.ChipAllow.SceneAsset", false);
            Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(SceneAsset)));
        }
    }
}
