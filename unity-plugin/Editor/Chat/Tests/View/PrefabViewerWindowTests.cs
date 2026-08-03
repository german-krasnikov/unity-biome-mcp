// TDD — PrefabViewerWindow + PrefabPreviewLoader tests (headless, EditMode).
// All tests are headless-safe: no AssetDatabase, no EditorWindow.ShowModal.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PrefabPreviewLoaderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // --- Loader cancel tests ---

        [Test]
        public void Cancel_BeforePoll_DoesNotCallback()
        {
            var calls = new List<Texture2D>();
            Action pollHook = null;
            var loader = new PrefabPreviewLoader(
                null,
                tex => calls.Add(tex),
                hook => pollHook = hook,
                hook => pollHook = null);

            loader.Cancel();
            pollHook?.Invoke(); // should be unregistered

            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void Cancel_AfterCancel_DoesNotThrow()
        {
            var loader = new PrefabPreviewLoader(
                null,
                _ => { },
                _ => { },
                _ => { });

            loader.Cancel();
            Assert.DoesNotThrow(() => loader.Cancel());
        }

        [Test]
        public void Poll_Timeout_CallsBackWithNull()
        {
            Texture2D received = new Texture2D(1, 1); // sentinel non-null
            Action pollHook = null;
            var loader = new PrefabPreviewLoader(
                null,
                tex => received = tex,
                hook => pollHook = hook,
                hook => { },
                getPreview: _ => null,
                timeoutFrames: 2);

            // Poll beyond timeout
            pollHook?.Invoke();
            pollHook?.Invoke();
            pollHook?.Invoke(); // frame 3 — should fire timeout callback

            Assert.IsNull(received);
        }

        [Test]
        public void Poll_PreviewReady_CallsBackImmediately()
        {
            Texture2D fakeTex = new Texture2D(1, 1);
            Texture2D received = null;
            Action pollHook = null;
            var loader = new PrefabPreviewLoader(
                null,
                tex => received = tex,
                hook => pollHook = hook,
                hook => { },
                getPreview: _ => fakeTex,
                timeoutFrames: 10);

            pollHook?.Invoke();

            Assert.AreEqual(fakeTex, received);
        }
    }

    [TestFixture]
    public class PrefabViewerWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // --- BuildUI tests via internal static helper ---

        [Test]
        public void BuildUI_ShowsNameLabel()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "PlayerHUD", false, new string[0], 0);

            var lbl = root.Q<Label>(name: "prefab-name");
            Assert.IsNotNull(lbl, "name label missing");
            StringAssert.Contains("PlayerHUD", lbl.text);
        }

        [Test]
        public void BuildUI_VariantBadge()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "PlayerHUD", isVariant: true, new string[0], 0);

            var lbl = root.Q<Label>(name: "prefab-name");
            StringAssert.Contains("[Variant]", lbl.text);
        }

        [Test]
        public void BuildUI_NoVariant_NoBadge()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "PlayerHUD", isVariant: false, new string[0], 0);

            var lbl = root.Q<Label>(name: "prefab-name");
            StringAssert.DoesNotContain("[Variant]", lbl.text);
        }

        [Test]
        public void BuildUI_ShowsPingButton()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "X", false, new string[0], 0);

            var btn = root.Q<Button>(name: "btn-ping");
            Assert.IsNotNull(btn, "Ping button missing");
        }

        [Test]
        public void BuildUI_ShowsOpenButton()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "X", false, new string[0], 0);

            var btn = root.Q<Button>(name: "btn-open");
            Assert.IsNotNull(btn, "Open button missing");
        }

        [Test]
        public void BuildUI_TruncatesComponentsAtEight()
        {
            var comps = new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" }; // 10
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "X", false, comps, 0);

            // Should have 8 component labels + 1 "…and N more" label
            var compLabels = root.Query<Label>(className: "prefab-component").ToList();
            Assert.AreEqual(8, compLabels.Count, "should show exactly 8 components");

            var moreLbl = root.Q<Label>(name: "prefab-more");
            Assert.IsNotNull(moreLbl, "more label missing");
            StringAssert.Contains("2 more", moreLbl.text);
        }

        [Test]
        public void BuildUI_FewComponents_NoMoreLabel()
        {
            var comps = new[] { "A", "B", "C" };
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "X", false, comps, 0);

            var moreLbl = root.Q<Label>(name: "prefab-more");
            Assert.IsNull(moreLbl, "more label must be absent when <= 8 components");
        }

        [Test]
        public void BuildUI_ShowsChildCount()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildUIForTest(root, "X", false, new string[0], childCount: 5);

            var lbl = root.Q<Label>(name: "prefab-children");
            Assert.IsNotNull(lbl);
            StringAssert.Contains("5", lbl.text);
        }

        [Test]
        public void Show_NullPath_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PrefabViewerWindow.ShowNotFound(null));
        }

        [Test]
        public void ShowNotFound_ReturnsNotFoundLabel()
        {
            var root = new VisualElement();
            PrefabViewerWindow.BuildNotFoundUI(root, "Assets/Missing.prefab");

            var lbl = root.Q<Label>();
            Assert.IsNotNull(lbl);
            StringAssert.Contains("Missing.prefab", lbl.text);
        }
    }
}
