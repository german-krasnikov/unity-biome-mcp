// Tests for ChipInlinePreviewPanel — toggle / lazy-build / fallback / cancellation.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipInlinePreviewPanelTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string AnyPath = "Assets/tex.png";
        const string UnknownPath = "Assets/unknown.txt";
        const string UnknownKind = "nonexistent_kind";

        [SetUp]
        public void SetUp()
        {
            InlinePreviewBuilder.TextureLoader = _ => Texture2D.whiteTexture;
            ChipKindRegistry.ResetForTests();
            AssetViewerFactory.ReRegisterBuiltIns();
        }

        [TearDown]
        public void TearDown()
        {
            InlinePreviewBuilder.TextureLoader = null;
            AssetViewerFactory.Reset();
        }

        [Test]
        public void IsVisible_Initially_False()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            Assert.IsFalse(panel.IsVisible);
        }

        [Test]
        public void Toggle_FirstCall_ShowsPanel()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            Assert.IsTrue(panel.IsVisible);
        }

        [Test]
        public void Toggle_SecondCall_HidesPanel()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            panel.Toggle();
            Assert.IsFalse(panel.IsVisible);
        }

        [Test]
        public void Toggle_ThirdCall_ShowsAgain()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            panel.Toggle();
            panel.Toggle();
            Assert.IsTrue(panel.IsVisible);
        }

        [Test]
        public void Toggle_BuildsPreviewOnlyOnce()
        {
            int loaderCalls = 0;
            InlinePreviewBuilder.TextureLoader = _ => { loaderCalls++; return Texture2D.whiteTexture; };

            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            panel.Toggle();
            panel.Toggle();
            panel.Toggle();

            Assert.AreEqual(1, loaderCalls, "TextureLoader must be called only once — on first Toggle");
        }

        [Test]
        public void Toggle_WhenBuildReturnsNull_CallsFallback()
        {
            bool fallbackCalled = false;
            var panel = new ChipInlinePreviewPanel(UnknownKind, UnknownPath,
                () => fallbackCalled = true);

            panel.Toggle();

            Assert.IsTrue(fallbackCalled, "fallback must be invoked when Build returns null");
        }

        [Test]
        public void Toggle_WhenBuildReturnsNull_PanelRemainsHidden()
        {
            var panel = new ChipInlinePreviewPanel(UnknownKind, UnknownPath, null);
            panel.Toggle();
            Assert.IsFalse(panel.IsVisible, "panel must stay hidden when Build returns null");
        }

        [Test]
        public void Panel_HasChipInlinePreviewClass()
        {
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null);
            Assert.IsTrue(panel.ClassListContains("chip-inline-preview"));
        }

        [Test]
        public void Toggle_WithPingAction_CallsPingOnFirstShow()
        {
            bool pingCalled = false;
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null,
                pingAction: () => pingCalled = true);
            panel.Toggle();
            Assert.IsTrue(pingCalled, "pingAction must be called on first show");
        }

        [Test]
        public void Toggle_WithPingAction_DoesNotCallOnSecondToggle()
        {
            int pingCount = 0;
            var panel = new ChipInlinePreviewPanel(ChipKindKeys.Texture, AnyPath, null,
                pingAction: () => pingCount++);
            panel.Toggle();
            panel.Toggle();
            Assert.AreEqual(1, pingCount, "pingAction must be called only on first show, not on hide");
        }

        [Test]
        public void Toggle_NullPreview_CallsNavigateNotPing()
        {
            bool navigateCalled = false;
            bool pingCalled = false;
            var panel = new ChipInlinePreviewPanel(UnknownKind, UnknownPath,
                navigateFallback: () => navigateCalled = true,
                pingAction: () => pingCalled = true);
            panel.Toggle();
            Assert.IsTrue(navigateCalled, "navigate fallback must be called when preview is null");
            Assert.IsFalse(pingCalled, "pingAction must NOT be called when falling back to navigate");
        }
    }
}
