using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>First wizard page — brand, subtitle, skip and next buttons.</summary>
    public sealed class WelcomeScreen : IWizardScreen
    {
        private readonly Action _onNext;
        private readonly Action _onSkip;
        private VisualElement _root;
        private VisualElement _logo;
        private VisualElement _subtitle;
        private VisualElement _nav;

        public string Title => "Welcome";

        public WelcomeScreen(Action onNext, Action onSkip)
        {
            _onNext = onNext;
            _onSkip = onSkip;
        }

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("wiz-container");

            _logo = new Label("◆ Unity Biome MCP");
            _logo.AddToClassList("wiz-title");
            var logo = _logo;

            _subtitle = new Label("MCP server for Unity Editor — 2 minutes to configure");
            _subtitle.AddToClassList("wiz-subtitle");
            var subtitle = _subtitle;

            var version = new Label(GetVersion());
            version.AddToClassList("wiz-version");

            var spacer = new VisualElement();
            spacer.AddToClassList("wiz-spacer");

            var skipBtn = WizardUI.Quiet("Set up later", _onSkip);
            var nextBtn = WizardUI.Primary("Next →", _onNext);

            var nav = WizardUI.Navigation(skipBtn, nextBtn);
            _nav = nav;

            _root.Add(logo);
            _root.Add(subtitle);
            _root.Add(version);
            _root.Add(spacer);
            _root.Add(nav);

            return _root;
        }

        public void OnEnter()
        {
            if (_root == null) return;
            WizardAnimUtils.FadeIn(_logo, 80);
            WizardAnimUtils.FadeIn(_subtitle, 200);
            WizardAnimUtils.FadeIn(_nav, 400);
        }

        public void OnExit() { }

        private static string GetVersion()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WelcomeScreen).Assembly);
            return pkg != null ? $"v{pkg.version}" : "";
        }
    }
}
