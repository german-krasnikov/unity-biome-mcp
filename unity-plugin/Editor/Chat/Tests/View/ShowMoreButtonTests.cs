// TDD T2.5 — ShowMoreButton: shared "▼ N more …" button helper.
//
// Note: ClickEvent only dispatches through a panel, so the click test
// attaches the container to an EditorWindow (same pattern as ChipClickRouterTests).
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class ShowMoreButtonTests : UnityMcpTestBase
    {
        [Test]
        public void Append_AddsLabelWithCorrectTextAndClass()
        {
            var container = new VisualElement();
            ShowMoreButton.Append(container, "my-show-more", "▼ 5 more…", () => { });

            Assert.AreEqual(1, container.childCount, "Exactly one button added");
            var label = container[0] as Label;
            Assert.IsNotNull(label, "Added element must be a Label");
            Assert.AreEqual("▼ 5 more…", label.text, "Label text must match");
            Assert.IsTrue(label.ClassListContains("my-show-more"), "CSS class must be set");
        }

        [Test]
        public void Append_OnClick_RemovesButtonAndCallsExpand()
        {
            // ClickEvent requires a panel — attach container to an EditorWindow.
            LogAssert.ignoreFailingMessages = true;
            var window = CreateOwnedEditorWindow<DummyWindow>();
            window.ShowUtility();
            LogAssert.ignoreFailingMessages = false;

            var container = new VisualElement();
            window.rootVisualElement.Add(container);

            bool expandCalled = false;
            ShowMoreButton.Append(container, "cls", "▼ 3 more…", () => expandCalled = true);

            var label = container[0] as Label;
            Assert.IsNotNull(label);

            // Simulate click via the event system
            var evt = new ClickEvent { target = label };
            label.SendEvent(evt);

            Assert.IsTrue(expandCalled, "onExpand must be called on click");
            Assert.AreEqual(0, container.childCount,
                "Button must remove itself from container on click");
        }

        private sealed class DummyWindow : EditorWindow { }
    }
}
