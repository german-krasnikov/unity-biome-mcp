// Tests for MixedParagraphRenderer after the media-preview refactor:
// tokenizer-based rendering, StaleStateDecorator, ChipClickRouter and IPreviewContext injection.
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MixedParagraphRendererTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = false;
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.ColorResolver = null;
            InlinePreviewBuilder.TextureLoader = _ => Texture2D.whiteTexture;
            AssetViewerFactory.ReRegisterBuiltIns();
        }

        [Test]
        public void Render_TextAndTag_CreatesLabelAndPill()
        {
            var ctx = new MprPreviewContext(new FakeChipExistenceService { ExistsImpl = (_, __) => true });
            var ve = MixedParagraphRenderer.Render("hello [hierarchy:/Player#1] world", ctx);

            Assert.AreEqual(3, ve.childCount,
                $"Expected Label + wrapper + Label, got {ve.childCount} children");
            Assert.IsInstanceOf<Label>(ve[0], "first child must be Label");
            Assert.IsInstanceOf<Label>(ve[2], "last child must be Label");

            var wrapper = ve[1];
            Assert.IsTrue(wrapper.ClassListContains("chip-pill-wrapper"),
                "middle child must be the pill wrapper");

            var pill = wrapper.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "wrapper must contain a pill");
            Assert.IsNull(pill.Q<Button>(), "response pill must have no remove button");
        }

        [Test]
        public void Render_BareImagePath_CreatesPill()
        {
            var ctx = new MprPreviewContext(new FakeChipExistenceService { ExistsImpl = (_, __) => true });
            var ve = MixedParagraphRenderer.Render("saved to img.png", ctx);

            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill, "bare image path must render a pill");
        }

        [Test]
        public void Render_StalePill_SetsOpacity()
        {
            var ctx = new MprPreviewContext(new FakeChipExistenceService { ExistsImpl = (_, __) => false });
            var ve = MixedParagraphRenderer.Render("[script:Assets/Missing.cs]", ctx);

            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill);
            Assert.AreEqual(0.4f, pill.style.opacity.value, 0.001f,
                "stale pill must have opacity 0.4");
            Assert.IsTrue(pill.tooltip.StartsWith("[NOT FOUND]"),
                "stale pill tooltip must start with [NOT FOUND]");
        }

        [Test]
        public void Render_DeferredStalePill_SetsOpacityWhenResolved()
        {
            var service = new FakeChipExistenceService { ExistsImpl = (_, __) => null };
            var ctx = new MprPreviewContext(service);
            var ve = MixedParagraphRenderer.Render("[script:Assets/Later.cs]", ctx);

            var pill = ve.Q(className: "inline-chip-pill");
            Assert.IsNotNull(pill);
            Assert.That(pill.style.opacity.keyword == StyleKeyword.Null || pill.style.opacity.keyword == StyleKeyword.Undefined,
                "pill must not be faded before resolution");

            service.Resolve("script", "Assets/Later.cs", false);

            Assert.AreEqual(0.4f, pill.style.opacity.value, 0.001f,
                "pill must fade when resolved as missing");
        }

        [Test]
        public void Render_PillDetached_DisposesExistenceSubscription()
        {
            var service = new FakeChipExistenceService { ExistsImpl = (_, __) => null };
            var ctx = new MprPreviewContext(service);
            var ve = MixedParagraphRenderer.Render("[script:Assets/Missing.cs]", ctx);
            var wrapper = ve.Q(className: "chip-pill-wrapper");
            Assert.IsNotNull(wrapper);

            // Attach to a real panel so DetachFromPanelEvent fires on removal.
            var window = CreateTestWindow();
            window.rootVisualElement.Add(wrapper);
            Assert.AreEqual(0, service.DisposedCount,
                "subscription must not be disposed while attached");

            window.rootVisualElement.Remove(wrapper);

            Assert.AreEqual(1, service.DisposedCount,
                "subscription must be disposed on detach");
        }

        [Test]
        public void Render_PillClick_DoesNotTogglePreviewPanel()
        {
            // UX change: single-click = navigate (not preview toggle).
            // Preview is accessible via right-click "Show Preview" context menu.
            var ctx = new MprPreviewContext(new FakeChipExistenceService { ExistsImpl = (_, __) => true });
            var ve = MixedParagraphRenderer.Render("[hierarchy:/Player#1]", ctx);
            var wrapper = ve.Q(className: "chip-pill-wrapper");
            var pill = wrapper.Q(className: "inline-chip-pill");
            var panel = wrapper.Q(className: "chip-inline-preview");
            Assert.IsFalse(panel.style.display == DisplayStyle.Flex,
                "preview panel must be hidden initially");

            var window = CreateTestWindow();
            window.rootVisualElement.Add(wrapper);

            var click = new ClickEvent();
            Assert.IsTrue(SetClickCount(click, 1),
                "test must be able to set clickCount via reflection");
            click.target = pill;
            pill.SendEvent(click);

            // Single click now calls navigate — preview panel stays hidden.
            Assert.IsFalse(panel.style.display == DisplayStyle.Flex,
                "single click must NOT toggle preview panel (navigate instead)");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        [Test]
        public void PreserveStateForTests_RestoresExactContextInstance()
        {
            var original = new MprPreviewContext(
                new FakeChipExistenceService { ExistsImpl = (_, __) => true });
            var replacement = new MprPreviewContext(
                new FakeChipExistenceService { ExistsImpl = (_, __) => false });
            MixedParagraphRenderer.ContextOverride = original;

            using (MixedParagraphRenderer.PreserveStateForTests())
                MixedParagraphRenderer.ContextOverride = replacement;

            Assert.AreSame(original, MixedParagraphRenderer.ContextOverride);
        }

        sealed class MprPreviewContext : IPreviewContext
        {
            public IAssetPreviewService PreviewService => null;
            public IChipExistenceService ExistenceService { get; }
            public System.Threading.CancellationToken CancellationToken => default;

            public MprPreviewContext(IChipExistenceService existenceService)
                => ExistenceService = existenceService;
        }

        EditorWindow CreateTestWindow()
        {
            // EditorWindow creation in batchmode logs GUI errors; ignore them for this test.
            LogAssert.ignoreFailingMessages = true;
            var window = CreateOwnedEditorWindow<MprTestEditorWindow>();
            window.ShowUtility();
            return window;
        }

        static bool SetClickCount(ClickEvent evt, int count)
        {
            var type = evt.GetType();
            while (type != null && type != typeof(object))
            {
                var field = type.GetField("<clickCount>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(evt, count);
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        class MprTestEditorWindow : EditorWindow { }
    }
}
