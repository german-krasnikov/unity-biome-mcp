using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Setup wizard EditorWindow — hosts WizardScreenHost and renders each page.</summary>
    public class SetupWizard : EditorWindow
    {
        private WizardScreenHost _host;
        private VisualElement    _pageSlot;
        private VisualElement    _progressBar;

        [MenuItem("MCP/Setup Wizard", priority = 2)]
        public static void ShowWindow()
        {
            var w = GetWindow<SetupWizard>("MCP Setup");
            w.minSize = new Vector2(360, 440);
        }

        private void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("Wizard/SetupWizard.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            _host = new WizardScreenHost(Close, OnNavigated);

            // Dots bar
            var dotsBar = new VisualElement();
            dotsBar.AddToClassList("wiz-dots");

            var dots = new VisualElement[_host.ScreenCount];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = new VisualElement();
                dots[i].AddToClassList("wiz-dot");
                dotsBar.Add(dots[i]);
            }
            _host.SetDots(dots);

            _pageSlot = new VisualElement();
            _pageSlot.style.flexGrow = 1;

            _progressBar = WizardStepAnim.BuildProgressBar();

            rootVisualElement.Add(dotsBar);
            rootVisualElement.Add(_pageSlot);
            rootVisualElement.Add(_progressBar);

            _host.Navigate(0);
        }

        private void OnNavigated()
        {
            // Update progress immediately (no visual dependency on old content)
            int count = _host.ScreenCount;
            float ratio = count > 1 ? (float)_host.CurrentIndex / (count - 1) : 1f;
            WizardStepAnim.SetProgress(_progressBar, ratio);

            // Slide old content out, then replace after transition completes
            var oldChildren = _pageSlot.Children().ToList();
            foreach (var child in oldChildren)
                WizardStepAnim.TransitionOut(child);

            var screen = _host.CurrentScreen;
            if (screen == null) return;

            _pageSlot.schedule.Execute(() =>
            {
                _pageSlot.Clear();
                var el = screen.Build();
                _pageSlot.Add(el);
                WizardStepAnim.TransitionIn(el);
                screen.OnEnter();
            }).StartingIn(220);
        }
    }
}
