// Tests for InlinePreviewBuilder compatibility shim.
// Static seams are forwarded to the individual IPreviewBuilder implementations.
using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlinePreviewBuilderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string TexturePath = "Assets/tex.png";
        const string AudioPath   = "Assets/sound.wav";
        const string ModelPath   = "Assets/model.fbx";
        const string UnknownPath = "Assets/unknown.txt";

        [SetUp]
        public void SetUp()
        {
            InlinePreviewBuilder.TextureLoader = _ => Texture2D.whiteTexture;
            InlinePreviewBuilder.AssetPreviewLoader = _ => Texture2D.whiteTexture;
            InlinePreviewBuilder.AudioClipLoader = null;
            ChipKindRegistry.ResetForTests();
            AssetViewerFactory.ReRegisterBuiltIns();
        }

        [Test]
        public void Build_NullPath_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build("texture", null));

        [Test]
        public void Build_EmptyPath_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build("texture", ""));

        [Test]
        public void Build_TextureKey_ReturnsElementWithImageChild()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Texture, TexturePath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Image>(), "texture preview must contain an Image element");
        }

        [Test]
        public void Build_ImageKey_ReturnsElementWithImageChild()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Image, TexturePath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Image>(), "image preview must contain an Image element");
        }

        [Test]
        public void Build_ModelKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Model, ModelPath);
            Assert.IsNotNull(ve);
        }

        [Test]
        public void Build_PrefabKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Prefab, "Assets/hero.prefab");
            Assert.IsNotNull(ve);
        }

        [Test]
        public void Build_MaterialKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Material, "Assets/mat.mat");
            Assert.IsNotNull(ve);
        }

        [Test]
        public void Build_ScriptableObjectKey_ReturnsNonNull()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.ScriptableObject, "Assets/data.asset");
            Assert.IsNotNull(ve);
        }

        [Test]
        public void Build_AudioKey_ContainsLabel()
        {
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Audio, AudioPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Label>(), "audio preview must contain a Label");
        }

        [Test]
        public void Build_AudioKey_WhenClipAvailable_ShowsMetadataLabel()
        {
            InlinePreviewBuilder.AudioClipLoader = _ => (length: 90f, frequency: 44100, channels: 2);
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Audio, AudioPath);
            Assert.IsNotNull(ve);
            var labels = ve.Query<Label>().ToList();
            Assert.GreaterOrEqual(labels.Count, 2, "audio with clip must show filename + metadata labels");
            bool hasMetadata = false;
            foreach (var l in labels)
                if (l.text.Contains("kHz")) { hasMetadata = true; break; }
            Assert.IsTrue(hasMetadata, "metadata label must contain kHz");
        }

        [Test]
        public void Build_AudioKey_WhenClipUnavailable_ShowsFilenameOnly()
        {
            InlinePreviewBuilder.AudioClipLoader = _ => null;
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Audio, AudioPath);
            Assert.IsNotNull(ve);
            var labels = ve.Query<Label>().ToList();
            Assert.AreEqual(1, labels.Count, "audio without clip must show filename label only");
        }

        [Test]
        public void Build_AudioKey_InvokesAudioClipLoaderSeam()
        {
            int callCount = 0;
            InlinePreviewBuilder.AudioClipLoader = _ => { callCount++; return null; };
            InlinePreviewBuilder.Build(ChipKindKeys.Audio, AudioPath);
            Assert.AreEqual(1, callCount, "AudioClipLoader seam must be called once for audio key");
        }

        [Test]
        public void Build_TextureKey_PassesPathToTextureLoader()
        {
            string receivedPath = null;
            InlinePreviewBuilder.TextureLoader = p => { receivedPath = p; return Texture2D.whiteTexture; };
            InlinePreviewBuilder.Build(ChipKindKeys.Texture, TexturePath);
            Assert.AreEqual(TexturePath, receivedPath, "TextureLoader must receive the original path");
        }

        [Test]
        public void Build_UnknownKey_ReturnsNull()
            => Assert.IsNull(InlinePreviewBuilder.Build("nonexistent_kind", UnknownPath));

        [Test]
        public void Build_TextureKey_InvokesTextureLoaderSeam()
        {
            int callCount = 0;
            InlinePreviewBuilder.TextureLoader = path => { callCount++; return Texture2D.whiteTexture; };
            InlinePreviewBuilder.Build(ChipKindKeys.Texture, TexturePath);
            Assert.AreEqual(1, callCount, "TextureLoader seam must be called once for texture key");
        }

        [Test]
        public void Build_AssetPreview_WhenPreviewNull_ReturnsContainerWithLabel()
        {
            InlinePreviewBuilder.AssetPreviewLoader = _ => null;
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Model, ModelPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Label>(), "null preview must fall back to filename Label");
        }

        [Test]
        public void Build_AssetPreview_WhenPreviewAvailable_ReturnsImage()
        {
            InlinePreviewBuilder.AssetPreviewLoader = _ => Texture2D.whiteTexture;
            var ve = InlinePreviewBuilder.Build(ChipKindKeys.Model, ModelPath);
            Assert.IsNotNull(ve);
            Assert.IsNotNull(ve.Q<Image>(), "non-null preview must produce an Image element");
        }

        [Test]
        public void Build_AssetPreview_InvokesLoaderSeam()
        {
            int callCount = 0;
            InlinePreviewBuilder.AssetPreviewLoader = _ => { callCount++; return Texture2D.whiteTexture; };
            InlinePreviewBuilder.Build(ChipKindKeys.Model, ModelPath);
            Assert.AreEqual(1, callCount, "AssetPreviewLoader seam must be called once");
        }

        [Test]
        public void PreserveStateForTests_RestoresExactLoaderDelegates()
        {
            Func<string, Texture2D> textureLoader = _ => Texture2D.whiteTexture;
            Func<UnityEngine.Object, Texture2D> assetLoader = _ => Texture2D.blackTexture;
            Func<string, (float length, int frequency, int channels)?> audioLoader =
                _ => (1f, 22050, 1);
            InlinePreviewBuilder.TextureLoader = textureLoader;
            InlinePreviewBuilder.AssetPreviewLoader = assetLoader;
            InlinePreviewBuilder.AudioClipLoader = audioLoader;

            using (InlinePreviewBuilder.PreserveStateForTests())
            {
                InlinePreviewBuilder.TextureLoader = null;
                InlinePreviewBuilder.AssetPreviewLoader = null;
                InlinePreviewBuilder.AudioClipLoader = null;
            }

            Assert.AreSame(textureLoader, InlinePreviewBuilder.TextureLoader);
            Assert.AreSame(assetLoader, InlinePreviewBuilder.AssetPreviewLoader);
            Assert.AreSame(audioLoader, InlinePreviewBuilder.AudioClipLoader);
        }
    }
}
