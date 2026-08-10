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
        private Label            _stepLabel;
        private WizardJourneyAnim _journeyAnim;

        [MenuItem("🧬MCP/Setup Wizard", priority = 2)]
        public static void ShowWindow()
        {
            var w = GetWindow<SetupWizard>($"{BiomeLabel.DisplayName} Setup");
            w.minSize = new Vector2(360, 440);
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            BiomeUI.LoadCoreStyles(rootVisualElement, includeWizard: true);

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

            _journeyAnim = new WizardJourneyAnim();

            _pageSlot = new VisualElement();
            _pageSlot.AddToClassList("wiz-page-slot");

            _progressBar = WizardStepAnim.BuildProgressBar();
            _stepLabel = new Label();
            _stepLabel.AddToClassList("wiz-step-label");

            rootVisualElement.Add(dotsBar);
            rootVisualElement.Add(_journeyAnim);
            rootVisualElement.Add(_pageSlot);
            rootVisualElement.Add(_progressBar);
            rootVisualElement.Add(_stepLabel);
            rootVisualElement.focusable = true;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

            _host.Navigate(0);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || _host == null || _host.CurrentIndex <= 0)
                return;

            _host.Back();
            evt.StopPropagation();
        }

        private void OnNavigated()
        {
            // Update progress immediately (no visual dependency on old content)
            int count = _host.ScreenCount;
            float ratio = count > 0 ? (float)(_host.CurrentIndex + 1) / count : 1f;
            WizardStepAnim.SetProgress(_progressBar, ratio);
            _journeyAnim?.SetStep(_host.CurrentIndex, count);
            _stepLabel.text = $"Step {_host.CurrentIndex + 1} of {count}";
            bool backwards = _host.PreviousIndex > _host.CurrentIndex;

            // Slide old content out, then replace after transition completes
            var oldChildren = _pageSlot.Children().ToList();
            foreach (var child in oldChildren)
            {
                child.pickingMode = PickingMode.Ignore;
                WizardStepAnim.TransitionOut(child, backwards);
            }

            var screen = _host.CurrentScreen;
            if (screen == null) return;

            void BuildCurrent()
            {
                _pageSlot.Clear();
                var el = screen.Build();
                _pageSlot.Add(el);
                WizardStepAnim.TransitionIn(el, backwards);
                screen.OnEnter();
            }

            if (oldChildren.Count == 0)
                BuildCurrent();
            else
                _pageSlot.schedule.Execute(BuildCurrent).StartingIn(BiomeUI.MotionNormalMs);
        }

        private void OnDisable()
        {
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            _host?.CurrentScreen?.OnExit();
        }
    }
}
