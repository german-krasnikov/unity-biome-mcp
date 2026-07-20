using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard;
using UnityMCP.Editor.Wizard.Screens;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SetupWizardTests
    {
        [TearDown]
        public void TearDown()
        {
            // Clean up EditorPref set during tests
            EditorPrefs.DeleteKey("MCPWizard.Done");
        }

        // ── Screen factory tests ──────────────────────────────────────────────

        [Test]
        public void WelcomeScreen_Build_ReturnsNonNull()
        {
            var screen = new WelcomeScreen(null, null);
            var el = screen.Build();
            Assert.IsNotNull(el);
        }

        [Test]
        public void WelcomeScreen_Title_IsWelcome()
        {
            var screen = new WelcomeScreen(null, null);
            Assert.AreEqual("Welcome", screen.Title);
        }

        [Test]
        public void AiConfigScreen_Build_ReturnsNonNull()
        {
            var screen = new AiConfigScreen(null, null);
            var el = screen.Build();
            Assert.IsNotNull(el);
        }

        [Test]
        public void AiConfigScreen_Title_IsAITools()
        {
            var screen = new AiConfigScreen(null, null);
            Assert.AreEqual("AI Tools", screen.Title);
        }

        [Test]
        public void AiConfigScreen_Build_ContainsCards()
        {
            var screen = new AiConfigScreen(null, null);
            var root = screen.Build();
            // Should contain at least one element with wiz-card class
            bool hasCard = ContainsClass(root, "wiz-card");
            Assert.IsTrue(hasCard, "AiConfigScreen must contain at least one .wiz-card element");
        }

        // ── WizardScreenHost tests ────────────────────────────────────────────

        [Test]
        public void InstallSkillsScreen_Build_ReturnsNonNull()
        {
            var screen = new InstallSkillsScreen(null, null);
            Assert.IsNotNull(screen.Build());
        }

        [Test]
        public void InstallSkillsScreen_Title_IsInstallAISkills()
        {
            var screen = new InstallSkillsScreen(null, null);
            Assert.AreEqual("Install AI Skills", screen.Title);
        }

        [Test]
        public void WizardScreenHost_HasFourScreens()
        {
            var host = new WizardScreenHost();
            Assert.AreEqual(4, host.ScreenCount);
        }

        [Test]
        public void WizardScreenHost_Navigate_SetsCorrectIndex()
        {
            var host = new WizardScreenHost();
            host.Navigate(2);
            Assert.AreEqual(2, host.CurrentIndex);
        }

        [Test]
        public void WizardScreenHost_Navigate_UpdatesDots()
        {
            var host = new WizardScreenHost();
            var dots = new VisualElement[4];
            for (int i = 0; i < 4; i++) dots[i] = new VisualElement();
            host.SetDots(dots);

            host.Navigate(1);

            Assert.IsTrue(dots[1].ClassListContains("wiz-dot--active"), "dot[1] should be active");
            Assert.IsFalse(dots[0].ClassListContains("wiz-dot--active"), "dot[0] should not be active");
        }

        [Test]
        public void WizardScreenHost_Complete_SetsEditorPref()
        {
            var host = new WizardScreenHost();
            host.Complete();
            Assert.IsTrue(EditorPrefs.GetBool("MCPWizard.Done", false));
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static bool ContainsClass(VisualElement root, string cls)
        {
            if (root.ClassListContains(cls)) return true;
            foreach (var child in root.Children())
                if (ContainsClass(child, cls)) return true;
            return false;
        }
    }
}
