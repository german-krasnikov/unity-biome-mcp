using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;
using UnityMCP.Editor.Wizard.Screens;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PickBackendScreenTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Build_ReturnsNonNull()
        {
            var screen = new PickBackendScreen(null, null);
            Assert.IsNotNull(screen.Build());
        }

        [Test]
        public void Title_IsPickBackend()
        {
            var screen = new PickBackendScreen(null, null);
            Assert.AreEqual("Pick Backend", screen.Title);
        }

        [Test]
        public void Build_HasScrollView()
        {
            var screen = new PickBackendScreen(null, null);
            var root = screen.Build();
            Assert.IsTrue(ContainsType<ScrollView>(root), "Should contain a ScrollView");
        }

        [Test]
        public void Build_AllBackendsPresent()
        {
            var screen = new PickBackendScreen(null, null);
            var root = screen.Build();
            int cardCount = CountClass(root, "wiz-card");
            Assert.AreEqual(BackendDescriptor.All.Length, cardCount,
                $"Expected {BackendDescriptor.All.Length} cards, got {cardCount}");
        }

        [Test]
        public void SelectCard_CallsCallback()
        {
            BackendDescriptor selected = null;
            var screen = new PickBackendScreen(b => selected = b, null);
            screen.Build();
            screen.SimulateSelect(0);
            Assert.IsNotNull(selected, "Callback should be called with selected backend");
        }

        [Test]
        public void SelectCard_CallsCallbackWithCorrectBackend()
        {
            BackendDescriptor selected = null;
            var screen = new PickBackendScreen(b => selected = b, null);
            screen.Build();
            screen.SimulateSelect(0);
            Assert.AreEqual(BackendDescriptor.All[0].Key, selected.Key);
        }

        [Test]
        public void OnEnter_DoesNotThrow()
        {
            var screen = new PickBackendScreen(null, null);
            screen.Build();
            Assert.DoesNotThrow(() => screen.OnEnter());
        }

        [Test]
        public void OnExit_DoesNotThrow()
        {
            var screen = new PickBackendScreen(null, null);
            screen.Build();
            Assert.DoesNotThrow(() => screen.OnExit());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static bool ContainsType<T>(VisualElement root) where T : VisualElement
        {
            if (root is T) return true;
            foreach (var child in root.Children())
                if (ContainsType<T>(child)) return true;
            return false;
        }

        private static int CountClass(VisualElement root, string cls)
        {
            int count = root.ClassListContains(cls) ? 1 : 0;
            foreach (var child in root.Children())
                count += CountClass(child, cls);
            return count;
        }
    }
}
