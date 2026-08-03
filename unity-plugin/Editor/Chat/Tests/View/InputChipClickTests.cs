// InputChipClick: input field chips navigate on single click via ChipClickRouter.Register.
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
    public class InputChipClickTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        // T1: single left-click on input chip calls provider.Navigate
        [Test]
        public void InputPill_LeftClick_CallsNavigate()
        {
            LogAssert.ignoreFailingMessages = true;
            var window = CreateTestWindow();
            var navigated = false;
            var provider  = new SpyProvider("spy_input_nav", onNavigate: _ => navigated = true);
            ChipKindRegistry.Register(provider);

            var field = new InlineChipField();
            window.rootVisualElement.Add(field);

            var chip = new ChipData("spy_input_nav", "/TestObj", "TestObj", 0);
            field.AddChip(chip);

            // Pill is the first child of the internal pill row (index 0 in _pillRow).
            // We access it via the InlineChipField's first child that has children.
            var pill = FindFirstPill(field);
            Assert.IsNotNull(pill, "pill must exist after AddChip");

            SendClick(pill, 1);

            Assert.IsTrue(navigated, "single click on input chip must call Navigate");
        }

        // T2: double-click must NOT trigger navigation (clickCount==2 guard in ChipClickRouter)
        [Test]
        public void InputPill_DoubleClick_DoesNotCallNavigate()
        {
            LogAssert.ignoreFailingMessages = true;
            var window = CreateTestWindow();
            var navigated = false;
            var provider  = new SpyProvider("spy_input_dbl", onNavigate: _ => navigated = true);
            ChipKindRegistry.Register(provider);

            var field = new InlineChipField();
            window.rootVisualElement.Add(field);

            field.AddChip(new ChipData("spy_input_dbl", "/TestObj", "TestObj", 0));
            var pill = FindFirstPill(field);
            Assert.IsNotNull(pill);

            SendClick(pill, 2);

            Assert.IsFalse(navigated, "double-click must not trigger Navigate");
        }

        // T3: unknown kindKey — click must not throw (graceful null provider)
        [Test]
        public void InputPill_UnknownKind_DoesNotThrow()
        {
            var field = new InlineChipField();
            var chip  = new ChipData("unknown_xyz", "/path", "ref", 0);
            field.AddChip(chip);
            var pill = FindFirstPill(field);
            Assert.IsNotNull(pill);
            // DoesNotThrow covers both AttachReadOnlyBehavior and Click handling
            Assert.DoesNotThrow(() => SendClick(pill, 1));
        }

        // T4: clicking the ✕ remove button removes the chip but does NOT navigate
        [Test]
        public void InputPill_RemoveButton_DoesNotNavigate()
        {
            LogAssert.ignoreFailingMessages = true;
            var window = CreateTestWindow();
            var navigated = false;
            var provider  = new SpyProvider("spy_input_rm", onNavigate: _ => navigated = true);
            ChipKindRegistry.Register(provider);

            var field = new InlineChipField();
            window.rootVisualElement.Add(field);

            field.AddChip(new ChipData("spy_input_rm", "/TestObj", "TestObj", 0));
            var pill = FindFirstPill(field);
            Assert.IsNotNull(pill);

            var removeBtn = pill.Q<Button>(className: "inline-chip-remove");
            Assert.IsNotNull(removeBtn, "input pill must have a remove button");

            // Clicking remove triggers Button.clicked, not ClickEvent on pill
            removeBtn.SendEvent(new ClickEvent { target = removeBtn });

            Assert.IsFalse(navigated, "remove button click must not trigger Navigate");
        }

        // T5: each pill routes click to its own path, not a shared one
        [Test]
        public void InputPill_EachPillNavigatesItsOwnPath()
        {
            var navigated = new System.Collections.Generic.List<string>();
            var provider = new SpyProvider("spy_multi", onNavigate: p => navigated.Add(p));
            ChipKindRegistry.Register(provider);

            LogAssert.ignoreFailingMessages = true;
            var window = CreateTestWindow();
            var field = new InlineChipField();
            window.rootVisualElement.Add(field);
            field.AddChip(new ChipData("spy_multi", "/A", "A", 0));
            field.AddChip(new ChipData("spy_multi", "/B", "B", 0));

            var pills = GetAllPills(field);
            SendClick(pills[0], 1);
            SendClick(pills[1], 1);

            CollectionAssert.AreEqual(new[] { "/A", "/B" }, navigated,
                "each pill must navigate to its own path");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private EditorWindow CreateTestWindow()
        {
            var window = CreateOwnedEditorWindow<InputChipTestWindow>();
            window.ShowUtility();
            return window;
        }

        private static VisualElement FindFirstPill(InlineChipField field)
        {
            var pillRow = field.ElementAt(0);
            return pillRow.childCount > 0 ? pillRow.ElementAt(0) : null;
        }

        private static VisualElement[] GetAllPills(InlineChipField field)
        {
            var pillRow = field.ElementAt(0);
            var result  = new VisualElement[pillRow.childCount];
            for (int i = 0; i < pillRow.childCount; i++)
                result[i] = pillRow.ElementAt(i);
            return result;
        }

        private static void SendClick(VisualElement target, int clickCount)
        {
            var evt = new ClickEvent();
            SetClickCount(evt, clickCount);
            evt.target = target;
            target.SendEvent(evt);
        }

        private static void SetClickCount(ClickEvent evt, int count)
        {
            for (var t = evt.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var f = t.GetField("<clickCount>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) { f.SetValue(evt, count); return; }
            }
        }

        private sealed class InputChipTestWindow : EditorWindow { }

        private sealed class SpyProvider : IChipKindProvider
        {
            private readonly string _key;
            private readonly Action<string> _onNavigate;
            public SpyProvider(string key, Action<string> onNavigate) { _key = key; _onNavigate = onNavigate; }
            public string   Key                => _key;
            public int      Priority           => 50;
            public string   HexColor           => "#888888";
            public string   IconName           => "";
            public string   DefaultDepth       => "shallow";
            public string[] BarePathExtensions => Array.Empty<string>();
            public bool     CanHandle(UnityEngine.Object obj, string assetPath) => false;
            public ChipData Create(UnityEngine.Object obj, string assetPath) => default;
            public string   FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";
            public void     Navigate(string reference) => _onNavigate?.Invoke(reference);
            public void     Ping(string reference) { }
            public void     AppendContextMenuItems(DropdownMenu menu, string path) { }
        }
    }
}
