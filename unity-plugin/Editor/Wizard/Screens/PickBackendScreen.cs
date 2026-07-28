using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Wizard page 2 — scroll list of backends, pick one to configure.</summary>
    public sealed class PickBackendScreen : IWizardScreen
    {
        private readonly Action<BackendDescriptor> _onSelect;
        private readonly Action _onBack;
        private VisualElement[] _cards;
        private int _buildGeneration;
        private CancellationTokenSource _detectionCancellation;

        public string Title => "Pick Backend";

        public PickBackendScreen(Action<BackendDescriptor> onSelect, Action onBack)
        {
            _onSelect = onSelect;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var title = new Label("Choose your AI tool");
            title.AddToClassList("wiz-title");
            root.Add(title);

            var scroll = new ScrollView();
            scroll.AddToClassList("wiz-scroll");

            var backends = BackendDescriptor.All;
            _cards = new VisualElement[backends.Length];
            int generation = ++_buildGeneration;
            _detectionCancellation?.Cancel();
            _detectionCancellation?.Dispose();
            _detectionCancellation = new CancellationTokenSource();
            var cancellation = _detectionCancellation.Token;
            root.RegisterCallback<DetachFromPanelEvent>(_ => CancelDetection());

            for (int i = 0; i < backends.Length; i++)
            {
                var backend = backends[i]; // capture for lambda
                var (card, badge) = BuildCard(backend);
                card.clicked += () => _onSelect?.Invoke(backend);
                _cards[i] = card;
                scroll.Add(card);

                Task.Run(() => IsDetected(backend, cancellation), cancellation).ContinueWith(task =>
                {
                    bool detected = task.Status == TaskStatus.RanToCompletion && task.Result;
                    EditorApplication.delayCall += () =>
                    {
                        if (cancellation.IsCancellationRequested
                            || generation != _buildGeneration
                            || badge.panel == null)
                            return;
                        badge.text = detected ? "detected" : "not detected";
                        BiomeUI.SetExclusiveClass(
                            badge,
                            detected ? "wiz-badge-detected" : "wiz-badge-missing",
                            "wiz-badge-detected",
                            "wiz-badge-checking",
                            "wiz-badge-missing");
                    };
                }, cancellation);
            }

            root.Add(scroll);

            root.Add(WizardUI.Navigation(
                WizardUI.Secondary("← Back", _onBack)));

            return root;
        }

        public void OnEnter()
        {
            if (_cards == null) return;
            for (int i = 0; i < _cards.Length; i++)
                WizardAnimUtils.SlideInRight(_cards[i], i * 60);
        }

        public void OnExit() => CancelDetection();

        /// <summary>Test hook — simulates clicking card at index.</summary>
        public void SimulateSelect(int index)
        {
            var backends = BackendDescriptor.All;
            if (index >= 0 && index < backends.Length)
                _onSelect?.Invoke(backends[index]);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static (Button card, Label badge) BuildCard(BackendDescriptor b)
        {
            var card = new Button { text = string.Empty };
            card.AddToClassList("wiz-card");
            card.tooltip = $"Configure {b.DisplayName}";

            var header = new VisualElement();
            header.AddToClassList("wiz-card-header");

            var icon = new Label(b.Icon);
            icon.AddToClassList("wiz-card-icon");

            var name = new Label(b.DisplayName);
            name.AddToClassList("wiz-card-title");

            header.Add(icon);
            header.Add(name);

            var badge = new Label("checking...");
            badge.AddToClassList("wiz-badge-detected");
            badge.AddToClassList("wiz-badge-checking");
            header.Add(badge);

            var desc = new Label(b.Description);
            desc.AddToClassList("wiz-card-description");

            card.Add(header);
            card.Add(desc);
            return (card, badge);
        }

        private static bool IsDetected(BackendDescriptor d, CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested) return false;
            if (d.Mechanism == InstallMechanism.ChatAuto) return true;
            if (!string.IsNullOrEmpty(d.BinaryName)
                && BinaryExistsOnPath(d.BinaryName, cancellation))
                return true;
            if (cancellation.IsCancellationRequested) return false;
            if (!string.IsNullOrEmpty(d.ConfigDir) && ConfigDirExists(d.ConfigDir)) return true;
            return false;
        }

        private static bool BinaryExistsOnPath(string tool, CancellationToken cancellation)
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                string[] extensions;
#if UNITY_EDITOR_WIN
                extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                    .Split(';');
#else
                extensions = new[] { string.Empty };
#endif
                foreach (string directory in path.Split(Path.PathSeparator))
                {
                    if (cancellation.IsCancellationRequested) return false;
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    foreach (string extension in extensions)
                    {
                        if (cancellation.IsCancellationRequested) return false;
                        if (File.Exists(Path.Combine(directory, tool + extension)))
                            return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private static bool ConfigDirExists(string path)
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Directory.Exists(path.Replace("~", home));
            }
            catch { return false; }
        }

        private void CancelDetection()
        {
            _buildGeneration++;
            _detectionCancellation?.Cancel();
            _detectionCancellation?.Dispose();
            _detectionCancellation = null;
        }
    }
}
