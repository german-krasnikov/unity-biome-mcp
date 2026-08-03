// NUnit tests for ChatChipPolicy type-allowlist.
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatChipPolicyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            foreach (var name in new[] { "GameObject", "Material", "Texture", "AnimationClip", "MonoScript", "Mesh", "AudioClip", "ScriptableObject" })
                DeleteEditorPrefBool($"MCPChat.ChipAllow.{name}");
        }

        [Test] public void Prefab_Allowed()     => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.GameObject)));
        [Test] public void Material_Allowed()   => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
        [Test] public void Texture2D_Allowed()  => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture2D)));
        [Test] public void Texture_Allowed()    => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture)));
        [Test] public void AnimClip_Allowed()   => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AnimationClip)));
        [Test] public void MonoScript_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEditor.MonoScript)));
        [Test] public void ScriptableObject_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.ScriptableObject)));

        [Test] public void Cubemap_Allowed()       => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Cubemap)));
        [Test] public void RenderTexture_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.RenderTexture)));
        [Test] public void UserScriptableObjectSubclass_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(FakeSO)));

        [Test] public void Mesh_Allowed()      => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Mesh)));
        [Test] public void AudioClip_Allowed() => Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AudioClip)));

        [Test] public void DefaultAsset_Rejected() => Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEditor.DefaultAsset)));
        [Test] public void Null_Rejected()          => Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(null));

        // --- Pref-gating tests ---

        [Test]
        public void Material_DisabledByPref_Rejected()
        {
            SetEditorPrefBool("MCPChat.ChipAllow.Material", false);
            Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
        }

        [Test]
        public void Material_EnabledByPref_Allowed()
        {
            SetEditorPrefBool("MCPChat.ChipAllow.Material", true);
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
        }

        [Test]
        public void Default_NoPrefsSet_MaterialAllowed()
        {
            DeleteEditorPrefBool("MCPChat.ChipAllow.Material");
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
        }

        [Test]
        public void Texture2D_DisabledViaTextureKey()
        {
            SetEditorPrefBool("MCPChat.ChipAllow.Texture", false);
            Assert.IsFalse(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture2D)));
        }

        [Test]
        public void KnownTypes_AllDefaultTrue()
        {
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.GameObject)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Material)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Texture2D)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AnimationClip)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEditor.MonoScript)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.Mesh)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.AudioClip)));
            Assert.IsTrue(ChatChipPolicy.IsAllowedAssetType(typeof(UnityEngine.ScriptableObject)));
        }

        [Test]
        public void PrefKey_Format()
        {
            Assert.AreEqual("MCPChat.ChipAllow.Material", ChatChipPolicy.PrefKey("Material"));
        }

        private class FakeSO : UnityEngine.ScriptableObject {}
    }
}
