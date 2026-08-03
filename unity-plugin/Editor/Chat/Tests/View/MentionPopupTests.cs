// TDD — RED first. Tests for MentionPopup (Phase 4 UI Popup).
// Bare VisualElement tree — no EditorWindow needed.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionPopupTests : UnityMCP.Editor.Testing.UnityMcpTestBase
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

        // ── Helper ───────────────────────────────────────────────────────────

        private static List<MentionCandidate> MakeCandidates(int count)
        {
            var list = new List<MentionCandidate>();
            for (int i = 0; i < count; i++)
            {
                var chip = new ChipData(ChipKindKeys.Hierarchy, $"/obj{i}", $"Object{i}", i);
                list.Add(new MentionCandidate(chip, 100 - i, "d_UnityEditor.SceneHierarchyWindow"));
            }
            return list;
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Show_SetsIsVisible()
        {
            _popup.Show(MakeCandidates(3));
            Assert.IsTrue(_popup.IsVisible);
        }

        [Test]
        public void Dismiss_ClearsIsVisible()
        {
            _popup.Show(MakeCandidates(3));
            _popup.Dismiss();
            Assert.IsFalse(_popup.IsVisible);
        }

        [Test]
        public void MoveDown_Wraps()
        {
            _popup.Show(MakeCandidates(3));
            _popup.MoveDown();
            _popup.MoveDown();
            _popup.MoveDown(); // 3 steps on 3 items → wraps to 0
            Assert.AreEqual(0, _popup.SelectedIndex);
        }

        [Test]
        public void MoveUp_Wraps()
        {
            _popup.Show(MakeCandidates(3));
            _popup.MoveUp(); // from 0 → last (2)
            Assert.AreEqual(2, _popup.SelectedIndex);
        }

        [Test]
        public void ApplySelected_ReturnsCandidate()
        {
            var candidates = MakeCandidates(3);
            _popup.Show(candidates);
            _popup.MoveDown(); // select index 1
            var result = _popup.ApplySelected();
            Assert.IsNotNull(result);
            Assert.AreEqual("/obj1", result.Value.Chip.Path);
        }

        [Test]
        public void Show_Empty_NoPopup()
        {
            _popup.Show(new List<MentionCandidate>());
            Assert.IsFalse(_popup.IsVisible);
        }

        [Test]
        public void ApplySelected_Dismisses()
        {
            _popup.Show(MakeCandidates(3));
            _popup.ApplySelected();
            Assert.IsFalse(_popup.IsVisible);
        }

        [Test]
        public void Commit_ViaCallback_Invoked()
        {
            MentionCandidate? received = null;
            var anchor = new VisualElement();
            var popup  = new MentionPopup(anchor, c => received = c);
            var candidates = MakeCandidates(3);
            popup.Show(candidates);

            // CommitSelected is the internal test-seam for the mouse-click Commit path.
            popup.CommitSelected();

            Assert.IsNotNull(received);
            Assert.AreEqual("/obj0", received.Value.Chip.Path);
            Assert.IsFalse(popup.IsVisible); // Dismiss was called
        }
    }
}
