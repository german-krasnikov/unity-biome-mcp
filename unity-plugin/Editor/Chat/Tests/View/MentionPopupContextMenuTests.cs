// TDD — RED first. Tests for MentionPopup with maxRows param.
// Context menu items are registered at show time; tested via observable row count behavior.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionPopupContextMenuTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private VisualElement _anchor;
        private MentionPopup  _popup;

        [SetUp]
        public void SetUp()
        {
            _anchor = new VisualElement();
            _popup  = new MentionPopup(_anchor, _ => { });
        }

        [TearDown]
        public void TearDown() { _popup = null; }

        private static List<MentionCandidate> MakeCandidates(int count)
        {
            var list = new List<MentionCandidate>();
            for (int i = 0; i < count; i++)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, $"/obj{i}", $"Obj{i}", i);
                list.Add(new MentionCandidate(chip, 100 - i, "icon"));
            }
            return list;
        }

        // ── maxRows param ─────────────────────────────────────────────────────

        [Test]
        public void Show_WithMaxRows3_WrapsAfter3Downs()
        {
            // Verifies that only 3 rows were created when maxRows=3
            _popup.Show(MakeCandidates(10), maxRows: 3);
            _popup.MoveDown();
            _popup.MoveDown();
            _popup.MoveDown(); // 3 steps on 3-item list → wraps to 0
            Assert.AreEqual(0, _popup.SelectedIndex);
        }

        [Test]
        public void Show_DefaultMaxRows_BackwardCompatible_ShowsUpTo8()
        {
            // 10 candidates, default maxRows → wraps after 8 downs
            _popup.Show(MakeCandidates(10));
            for (int i = 0; i < 8; i++) _popup.MoveDown();
            Assert.AreEqual(0, _popup.SelectedIndex);
        }

        [Test]
        public void Show_MaxRows20_AllowsMoreThan8()
        {
            _popup.Show(MakeCandidates(20), maxRows: 15);
            for (int i = 0; i < 15; i++) _popup.MoveDown();
            Assert.AreEqual(0, _popup.SelectedIndex);
        }

        [Test]
        public void Show_WithMaxRows_IsVisible()
        {
            _popup.Show(MakeCandidates(5), maxRows: 3);
            Assert.IsTrue(_popup.IsVisible);
        }

        [Test]
        public void Show_WithMaxRows_CommitWorksByIndex()
        {
            MentionCandidate? committed = null;
            var anchor = new VisualElement();
            var popup  = new MentionPopup(anchor, c => committed = c);

            popup.Show(MakeCandidates(5), maxRows: 3);
            popup.MoveDown(); // select index 1
            popup.CommitSelected();

            Assert.IsNotNull(committed);
            Assert.AreEqual("/obj1", committed.Value.Chip.Path);
        }
    }
}
