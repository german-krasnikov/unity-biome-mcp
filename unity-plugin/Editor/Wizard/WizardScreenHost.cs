using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard.Screens;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>
    /// Pure logic host for the setup wizard — testable without EditorWindow.
    /// Flow: Welcome → PickBackend → Configure → InstallSkills (4 screens).
    /// </summary>
    public sealed class WizardScreenHost
    {
        // Local to this file — EditorPrefs key for the wizard's own completion flag.
        private const string DonePrefKey = "MCPWizard.Done";

        private readonly IWizardScreen[] _screens;
        private readonly ConfigureScreen _configureScreen;
        private VisualElement[] _dots;
        private Action _closeCallback;
        private Action _onNavigate;

        public int ScreenCount  => _screens.Length;
        public int CurrentIndex { get; private set; } = -1;
        public int PreviousIndex { get; private set; } = -1;

        /// <param name="closeCallback">Called on Complete() to close the window.</param>
        /// <param name="onNavigate">Called after each Navigate() so the window can re-render.</param>
        public WizardScreenHost(Action closeCallback = null, Action onNavigate = null)
        {
            _closeCallback = closeCallback;
            _onNavigate    = onNavigate;

            _configureScreen = new ConfigureScreen(Next, Back);
            var pickScreen   = new PickBackendScreen(OnBackendSelected, Back);

            _screens = new IWizardScreen[]
            {
                new WelcomeScreen(Next, Cancel),
                pickScreen,
                _configureScreen,
                new InstallSkillsScreen(Complete, Back),
            };
        }

        public IWizardScreen CurrentScreen =>
            CurrentIndex >= 0 && CurrentIndex < _screens.Length
                ? _screens[CurrentIndex]
                : null;

        public void SetDots(VisualElement[] dots) => _dots = dots;

        public void Navigate(int index)
        {
            if (index < 0 || index >= _screens.Length) return;
            CurrentScreen?.OnExit();
            PreviousIndex = CurrentIndex;
            CurrentIndex = index;
            RefreshDots();
            _onNavigate?.Invoke();
        }

        public void Next() => Navigate(CurrentIndex + 1);
        public void Back() => Navigate(CurrentIndex - 1);

        public void Complete()
        {
            EditorPrefs.SetBool(DonePrefKey, true);
            _closeCallback?.Invoke();
        }

        public void Cancel() => _closeCallback?.Invoke();

        // ── Backend handoff ───────────────────────────────────────────────────

        private void OnBackendSelected(BackendDescriptor backend)
        {
            _configureScreen.SetBackend(backend);
            Next();
        }

        // ── Dots ──────────────────────────────────────────────────────────────

        private void RefreshDots()
        {
            if (_dots == null) return;
            for (int i = 0; i < _dots.Length; i++)
            {
                _dots[i].EnableInClassList("wiz-dot--active", i == CurrentIndex);
                _dots[i].EnableInClassList("wiz-dot--complete", i < CurrentIndex);
            }
        }
    }
}
