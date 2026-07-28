using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;
using UnityMCP.Editor.Wizard.Screens;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ConfigureScreenTests
    {
        private static BackendDescriptor SampleBackend() =>
            System.Array.Find(BackendDescriptor.All, b => b.Key == "claude-code");

        [Test]
        public void Build_ReturnsNonNull()
        {
            var screen = new ConfigureScreen(null, null);
            Assert.IsNotNull(screen.Build());
        }

        [Test]
        public void Title_IsConfigure()
        {
            var screen = new ConfigureScreen(null, null);
            Assert.AreEqual("Configure", screen.Title);
        }

        [Test]
        public void Build_HasConfigureButton()
        {
            var screen = new ConfigureScreen(null, null);
            screen.SetBackend(SampleBackend());
            var root = screen.Build();
            Assert.IsTrue(ContainsButton(root, "Configure"), "Should have Configure button");
        }

        [Test]
        public void Build_HasLogArea()
        {
            var screen = new ConfigureScreen(null, null);
            screen.SetBackend(SampleBackend());
            var root = screen.Build();
            Assert.IsTrue(ContainsClass(root, "wiz-log"), "Should have a log area with wiz-log class");
        }

        [Test]
        public void Build_ShowsBackendName()
        {
            var screen = new ConfigureScreen(null, null);
            var backend = SampleBackend();
            screen.SetBackend(backend);
            var root = screen.Build();
            Assert.IsTrue(ContainsText(root, backend.DisplayName),
                $"Should display backend name '{backend.DisplayName}'");
        }

        [Test]
        public void Build_WithNoBackend_StillReturnsElement()
        {
            var screen = new ConfigureScreen(null, null);
            var root = screen.Build();
            Assert.IsNotNull(root);
        }

        [Test]
        public void OnEnter_DoesNotThrow()
        {
            var screen = new ConfigureScreen(null, null);
            screen.SetBackend(SampleBackend());
            screen.Build();
            Assert.DoesNotThrow(() => screen.OnEnter());
        }

        [Test]
        public void OnExit_DoesNotThrow()
        {
            var screen = new ConfigureScreen(null, null);
            screen.SetBackend(SampleBackend());
            screen.Build();
            Assert.DoesNotThrow(() => screen.OnExit());
        }

        // ── Phase 1A: AutoProjectConfig / ManualInstructions branches ──────────

        [Test]
        public void RunConfigure_AutoProjectConfigBackend_LogsAutoConfiguredMessage()
        {
            var screen = new ConfigureScreen(null, null);
            var backend = System.Array.Find(BackendDescriptor.All, b => b.Key == "cursor");
            screen.SetBackend(backend);
            var root = screen.Build();
            screen.OnEnter();

            InvokeRunConfigure(screen);

            Assert.IsTrue(ContainsTextContaining(root, "auto-configured"),
                "Should log an auto-configured message for AutoProjectConfig backends");
        }

        [Test]
        public void RunConfigure_ManualInstructionsBackend_LogsInstructions()
        {
            var screen = new ConfigureScreen(null, null);
            var backend = System.Array.Find(BackendDescriptor.All, b => b.Key == "rider-ai");
            screen.SetBackend(backend);
            var root = screen.Build();
            screen.OnEnter();

            InvokeRunConfigure(screen);

            Assert.IsTrue(ContainsTextContaining(root, backend.Instructions),
                "Should log the manual instructions text");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void InvokeRunConfigure(ConfigureScreen screen)
        {
            var method = typeof(ConfigureScreen).GetMethod("RunConfigure",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(screen, null);
        }

        private static bool ContainsTextContaining(VisualElement root, string substring)
        {
            if (root is Label lbl && lbl.text.Contains(substring)) return true;
            foreach (var child in root.Children())
                if (ContainsTextContaining(child, substring)) return true;
            return false;
        }

        private static bool ContainsButton(VisualElement root, string text)
        {
            if (root is Button btn && btn.text.Contains(text)) return true;
            foreach (var child in root.Children())
                if (ContainsButton(child, text)) return true;
            return false;
        }

        private static bool ContainsClass(VisualElement root, string cls)
        {
            if (root.ClassListContains(cls)) return true;
            foreach (var child in root.Children())
                if (ContainsClass(child, cls)) return true;
            return false;
        }

        private static bool ContainsText(VisualElement root, string text)
        {
            if (root is Label lbl && lbl.text.Contains(text)) return true;
            foreach (var child in root.Children())
                if (ContainsText(child, text)) return true;
            return false;
        }
    }
}
