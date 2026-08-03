// TDD — AssetViewerFactory + ViewerLauncher seam tests (headless, EditMode).
// All tests headless-safe: no AssetDatabase, no EditorWindow calls.
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    // Fake viewer that records calls.
    internal sealed class FakeViewer : IAssetViewer
    {
        public readonly List<string> Shown = new List<string>();
        public void Show(string assetPath) => Shown.Add(assetPath);
    }

    [TestFixture]
    public class AssetViewerFactoryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Register_ThenForPath_ReturnsViewer()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".fbx", fake);

            var viewer = AssetViewerFactory.ForPath("Assets/Char.fbx");

            Assert.AreSame(fake, viewer);
        }

        [Test]
        public void ForPath_CaseInsensitive()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".fbx", fake);

            Assert.AreSame(fake, AssetViewerFactory.ForPath("Assets/Char.FBX"));
            Assert.AreSame(fake, AssetViewerFactory.ForPath("Assets/Char.Fbx"));
        }

        [Test]
        public void ForPath_UnknownExtension_ReturnsNull()
        {
            var result = AssetViewerFactory.ForPath("Assets/Char.unknown_ext_xyz");
            Assert.IsNull(result);
        }

        [Test]
        public void TryShow_RegisteredExt_ReturnsTrue()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".mp3", fake);

            bool result = AssetViewerFactory.TryShow("Assets/sound.mp3");

            Assert.IsTrue(result);
            Assert.AreEqual(1, fake.Shown.Count);
        }

        [Test]
        public void TryShow_UnregisteredExt_ReturnsFalse()
        {
            bool result = AssetViewerFactory.TryShow("Assets/file.unknownxyz");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryShow_CallsShowWithCorrectPath()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".wav", fake);
            const string path = "Assets/Audio/beep.wav";

            AssetViewerFactory.TryShow(path);

            Assert.AreEqual(path, fake.Shown[0]);
        }

        [Test]
        public void Register_OverridesExisting_KeepsLast()
        {
            var first  = new FakeViewer();
            var second = new FakeViewer();
            AssetViewerFactory.Register(".obj", first);
            AssetViewerFactory.Register(".obj", second);

            var viewer = AssetViewerFactory.ForPath("mesh.obj");

            Assert.AreSame(second, viewer);
        }

        [Test]
        public void PreserveStateForTests_RestoresRegistryAndDelegatesExactly()
        {
            var originalViewer = new FakeViewer();
            Func<string, bool> originalLauncher = _ => false;
            Action<string> originalFallback = _ => { };
            AssetViewerFactory.Register(".biome_scope_original", originalViewer);
            AssetChipProviderBase.ViewerLauncher = originalLauncher;
            ImageChipProvider.ImageFallbackViewer = originalFallback;

            using (AssetViewerFactory.PreserveStateForTests())
            {
                AssetViewerFactory.Reset();
                AssetViewerFactory.Register(".biome_scope_added", new FakeViewer());
                AssetChipProviderBase.ViewerLauncher = _ => true;
                ImageChipProvider.ImageFallbackViewer = null;
            }

            Assert.AreSame(originalViewer,
                AssetViewerFactory.ForPath("Assets/value.biome_scope_original"));
            Assert.IsNull(AssetViewerFactory.ForPath("Assets/value.biome_scope_added"));
            Assert.AreSame(originalLauncher, AssetChipProviderBase.ViewerLauncher);
            Assert.AreSame(originalFallback, ImageChipProvider.ImageFallbackViewer);
        }
    }

    [TestFixture]
    public class ViewerLauncherSeamTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ViewerLauncher_Set_CalledOnNavigate()
        {
            int callCount = 0;
            string capturedPath = null;
            AssetChipProviderBase.ViewerLauncher = path =>
            {
                callCount++;
                capturedPath = path;
                return true;
            };

            var provider = new ModelChipProvider();
            provider.Navigate("Assets/Char.fbx");

            Assert.AreEqual(1, callCount);
            Assert.AreEqual("Assets/Char.fbx", capturedPath);
        }

        [Test]
        public void ModelChipProvider_CanHandle_Fbx()
        {
            var provider = new ModelChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.fbx"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.FBX"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.obj"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.blend"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.dae"));
        }

        [Test]
        public void ModelChipProvider_CanHandle_RejectsPng()
        {
            var provider = new ModelChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "Assets/tex.png"));
            Assert.IsFalse(provider.CanHandle(null, "Assets/mat.mat"));
        }

        [Test]
        public void AudioChipProvider_CanHandle_AudioExts()
        {
            var provider = new AudioChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.wav"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.mp3"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.ogg"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.aiff"));
        }

        [Test]
        public void AudioChipProvider_CanHandle_RejectsFbx()
        {
            var provider = new AudioChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "Assets/model.fbx"));
        }

        [Test]
        public void AudioChipProvider_Priority_BetweenMaterialAndTexture()
        {
            // Material=500, Texture=600 → Audio should be between
            var audio = new AudioChipProvider();
            Assert.Greater(audio.Priority, 500);
            Assert.Less(audio.Priority, 600);
        }

        [Test]
        public void ModelChipProvider_Priority_BetweenPrefabAndMaterial()
        {
            // Prefab=400, Material=500 → Model should be between
            var model = new ModelChipProvider();
            Assert.Greater(model.Priority, 400);
            Assert.Less(model.Priority, 500);
        }
    }

    [TestFixture]
    public class AudioUtilProxyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void TypeLookup_DoesNotThrow()
        {
            // Verifies reflection init doesn't crash in Editor environment.
            Assert.DoesNotThrow(() => AudioUtilProxy.IsAvailable.ToString());
        }

        [Test]
        public void Stop_WhenClipNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioUtilProxy.Stop());
        }

        [Test]
        public void Pause_WhenClipNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioUtilProxy.Pause());
        }

        [Test]
        public void GetPosition_WhenClipNull_ReturnsZero()
        {
            float result = AudioUtilProxy.GetPosition(null);
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void GetWaveform_WhenClipNull_ReturnsNull()
        {
            var result = AudioUtilProxy.GetWaveform(null, 128, 32);
            Assert.IsNull(result);
        }
    }

    // ── BUG 1: ImageChipProvider.Navigate should use ViewerLauncher, not OpenURL ──────────

    [TestFixture]
    public class ImageChipProviderNavigateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Navigate_WhenViewerLauncherHandles_DoesNotCallFallback()
        {
            // Arrange
            bool fallbackCalled = false;
            ImageChipProvider.ImageFallbackViewer = _ => fallbackCalled = true;
            AssetChipProviderBase.ViewerLauncher = _ => true; // viewer handles it

            // Act
            new ImageChipProvider().Navigate("/tmp/screenshot.png");

            // Assert — ViewerLauncher short-circuits, fallback must not be called
            Assert.IsFalse(fallbackCalled, "Navigate must not call fallback when ViewerLauncher handles the path");
        }

        [Test]
        public void Navigate_WhenViewerLauncherNull_CallsImageFallbackViewer()
        {
            // Arrange
            string capturedUrl = null;
            AssetChipProviderBase.ViewerLauncher = null;
            ImageChipProvider.ImageFallbackViewer = url => capturedUrl = url;

            // Act
            new ImageChipProvider().Navigate("/tmp/screenshot.png");

            // Assert
            Assert.IsNotNull(capturedUrl, "Navigate must invoke ImageFallbackViewer fallback");
            StringAssert.Contains("/tmp/screenshot.png", capturedUrl);
        }

        [Test]
        public void Navigate_WhenViewerLauncherRegistered_CallsViewer()
        {
            // BUG 1: original code skips ViewerLauncher for ImageChipProvider entirely.
            // After fix, .bmp/.gif should route through ViewerLauncher when registered.
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".bmp", fake);
            AssetChipProviderBase.ViewerLauncher = AssetViewerFactory.TryShow;

            new ImageChipProvider().Navigate("Assets/icon.bmp");

            Assert.AreEqual(1, fake.Shown.Count, "Navigate must invoke registered viewer for .bmp");
        }
    }

    // ── NRE: ModelChipProvider.Create / AudioChipProvider.Create with null obj ───────────

    [TestFixture]
    public class AssetChipProviderNullObjTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ModelChipProvider inherits AssetChipProviderBase.Create which does obj.name → NRE
        [Test]
        public void ModelChipProvider_Create_NullObj_NoThrow()
        {
            var provider = new ModelChipProvider();
            Assert.DoesNotThrow(() => provider.Create(null, "Assets/model.fbx"),
                "Create(null, path) must not throw NRE");
        }

        [Test]
        public void ModelChipProvider_Create_NullObj_ReturnsChipWithPath()
        {
            var provider = new ModelChipProvider();
            ChipData chip = default;
            Assert.DoesNotThrow(() => chip = provider.Create(null, "Assets/model.fbx"));
            Assert.AreEqual("Assets/model.fbx", chip.Path);
        }

        // AudioChipProvider inherits AssetChipProviderBase.Create → same NRE
        [Test]
        public void AudioChipProvider_Create_NullObj_NoThrow()
        {
            var provider = new AudioChipProvider();
            Assert.DoesNotThrow(() => provider.Create(null, "Assets/clip.wav"),
                "Create(null, path) must not throw NRE");
        }

        [Test]
        public void AudioChipProvider_Create_NullObj_ReturnsChipWithPath()
        {
            var provider = new AudioChipProvider();
            ChipData chip = default;
            Assert.DoesNotThrow(() => chip = provider.Create(null, "Assets/clip.wav"));
            Assert.AreEqual("Assets/clip.wav", chip.Path);
        }
    }

    // ── BUG 8: AssetViewerFactory.RegisterBuiltIns missing .bmp and .gif ────────────────

    [TestFixture]
    public class AssetViewerFactoryBuiltInCoverageTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => AssetViewerFactory.ReRegisterBuiltIns();

        [Test]
        public void RegisterBuiltIns_Bmp_HasViewer()
        {
            // BUG 8: .bmp is not registered in RegisterBuiltIns — this will FAIL
            var viewer = AssetViewerFactory.ForPath("Assets/icon.bmp");
            Assert.IsNotNull(viewer, ".bmp must have a registered viewer after RegisterBuiltIns");
        }

        [Test]
        public void RegisterBuiltIns_Gif_HasViewer()
        {
            // BUG 8: .gif is not registered in RegisterBuiltIns — this will FAIL
            var viewer = AssetViewerFactory.ForPath("Assets/anim.gif");
            Assert.IsNotNull(viewer, ".gif must have a registered viewer after RegisterBuiltIns");
        }

        [Test]
        public void RegisterBuiltIns_Png_HasViewer()
        {
            // Sanity: already registered, must keep passing
            var viewer = AssetViewerFactory.ForPath("Assets/sprite.png");
            Assert.IsNotNull(viewer, ".png must have a registered viewer");
        }

        [Test]
        public void RegisterBuiltIns_Jpg_HasViewer()
        {
            var viewer = AssetViewerFactory.ForPath("Assets/photo.jpg");
            Assert.IsNotNull(viewer, ".jpg must have a registered viewer");
        }
    }
}
