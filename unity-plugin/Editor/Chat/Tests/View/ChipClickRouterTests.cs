// TDD — ChipClickRouter: single-click = navigate (T4, T5).
// ClickEvent only dispatches through the UIElements panel — pill must be attached to a window.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipClickRouterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // T4: single click calls navigateAction
        [Test]
        public void SingleClick_CallsNavigateAction()
        {
            var window = CreateTestWindow();
            var pill      = new VisualElement();
            var callCount = 0;
            window.rootVisualElement.Add(pill);
            ChipClickRouter.Register(pill, null, () => callCount++);

            SendClick(pill, clickCount: 1);

            Assert.AreEqual(1, callCount, "Single click must call navigateAction exactly once");
        }

        // T5: clickCount==2 must not trigger navigate
        [Test]
        public void DoubleClick_DoesNotCallNavigateTwice()
        {
            var window = CreateTestWindow();
            var pill      = new VisualElement();
            var callCount = 0;
            window.rootVisualElement.Add(pill);
            ChipClickRouter.Register(pill, null, () => callCount++);

            SendClick(pill, clickCount: 2);

            Assert.AreEqual(0, callCount, "clickCount==2 event must not trigger navigate");
        }

        [Test]
        public void NullPill_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChipClickRouter.Register(null, null, () => { }));
        }

        [Test]
        public void NullNavigateAction_DoesNotThrow()
        {
            var window = CreateTestWindow();
            var pill = new VisualElement();
            window.rootVisualElement.Add(pill);
            ChipClickRouter.Register(pill, null, null);
            Assert.DoesNotThrow(() => SendClick(pill, 1));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static void SendClick(VisualElement target, int clickCount)
        {
            var evt = new ClickEvent();
            SetClickCount(evt, clickCount);
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SetClickCount(ClickEvent evt, int count)
        {
            // Walk the hierarchy; backing field lives in PointerEventBase<T>.
            var type = evt.GetType();
            while (type != null && type != typeof(object))
            {
                var field = type.GetField("<clickCount>k__BackingField",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) { field.SetValue(evt, count); return; }
                type = type.BaseType;
            }
        }

        private EditorWindow CreateTestWindow()
        {
            LogAssert.ignoreFailingMessages = true;
            var window = CreateOwnedEditorWindow<ChipClickTestWindow>();
            window.ShowUtility();
            return window;
        }

        private sealed class ChipClickTestWindow : EditorWindow { }
    }
}
