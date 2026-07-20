using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Wizard page 4 — copies ClientSkills into the project's .claude/ and .codex/ dirs.</summary>
    public sealed class InstallSkillsScreen : IWizardScreen
    {
        private readonly Action _onDone;
        private readonly Action _onBack;
        private Label   _logLabel;
        private Toggle  _overwriteToggle;
        private Toggle  _codexToggle;
        private Button  _installBtn;
        private readonly StringBuilder _log = new StringBuilder();
        private Process _proc;

        public string Title => "Install AI Skills";

        public InstallSkillsScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var title = new Label("Install AI Skills");
            title.AddToClassList("wiz-title");
            root.Add(title);

            var subtitle = new Label(GetSubtitle());
            subtitle.AddToClassList("wiz-subtitle");
            root.Add(subtitle);

            var logScroll = new ScrollView();
            logScroll.AddToClassList("wiz-log");
            logScroll.style.flexGrow  = 1;
            logScroll.style.minHeight = 120;
            _logLabel = new Label();
            _logLabel.style.fontSize   = 11;
            _logLabel.style.whiteSpace = WhiteSpace.Normal;
            logScroll.Add(_logLabel);
            root.Add(logScroll);

            _overwriteToggle = new Toggle("Overwrite existing files") { value = false };
            root.Add(_overwriteToggle);

            var projectRoot = ProjectRoot();
            _codexToggle = new Toggle("Run Codex sync after install")
                { value = SkillsInstaller.HasCodexDir(projectRoot) };
            root.Add(_codexToggle);

            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });

            _installBtn = new Button(RunInstall) { text = "Install" };
            _installBtn.AddToClassList("wiz-btn-primary");
            nav.Add(_installBtn);
            nav.Add(new Button(_onDone) { text = "Done ✓" });
            root.Add(nav);

            return root;
        }

        public void OnEnter()
        {
            _log.Clear();
            if (_logLabel != null) _logLabel.text = "";

            var ver = SkillsInstaller.ReadVersionFile(ProjectRoot());
            if (ver != null) AppendLog($"Previously installed: v{ver}");

            if (SkillsInstaller.FindSource() == null) AppendLog("⚠ ClientSkills not found in package.");
        }

        public void OnExit()
        {
            if (_proc != null && !_proc.HasExited) try { _proc.Kill(); } catch { }
            try { _proc?.Dispose(); } catch { }
            _proc = null;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RunInstall()
        {
            var src = SkillsInstaller.FindSource();
            if (src == null) { AppendLog("✗ ClientSkills directory not found."); return; }

            var projectRoot = ProjectRoot();
            _installBtn?.SetEnabled(false);

            var result = SkillsInstaller.Install(src, projectRoot, _overwriteToggle.value);
            AppendLog($"✓ Copied {result.Copied}, skipped {result.Skipped}");
            foreach (var e in result.Errors) AppendLog("✗ " + e);

            if (result.IsSuccess)
            {
                var ver = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SkillsInstaller).Assembly)?.version ?? "?";
                SkillsInstaller.WriteVersionFile(projectRoot, ver);
            }

            _installBtn?.SetEnabled(true);

            if (_codexToggle?.value == true) RunCodexSync(projectRoot);
        }

        private void RunCodexSync(string projectRoot)
        {
            var script = Path.Combine(projectRoot, ".codex", "scripts", "claude_to_codex.py");
            if (!File.Exists(script)) return;

#if UNITY_EDITOR_WIN
            const string exe = "python";
#else
            const string exe = "python3";
#endif
            try
            {
                var psi = new ProcessStartInfo(exe, $"\"{script}\" --repo-root \"{projectRoot}\"")
                {
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                };
                var proc = _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    var line = e.Data;
                    EditorApplication.delayCall += () => AppendLog(line);
                };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    var line = e.Data;
                    EditorApplication.delayCall += () => AppendLog("ERR: " + line);
                };
                proc.Exited += (_, __) =>
                {
                    int code = proc.ExitCode;
                    EditorApplication.delayCall += () =>
                        AppendLog(code == 0 ? "✓ Codex sync done" : $"✗ Codex exit {code}");
                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex) { AppendLog("ERROR: " + ex.Message); }
        }

        private void AppendLog(string line)
        {
            _log.AppendLine(line);
            if (_logLabel != null) _logLabel.text = _log.ToString();
        }

        private static string ProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string GetSubtitle()
        {
            var src = SkillsInstaller.FindSource();
            if (src == null) return "Skills not found";
            int skills = 0, agents = 0, scripts = 0;
            foreach (var f in SkillsInstaller.ListFiles(src))
            {
                if (f.StartsWith("skills/"))  skills++;
                else if (f.StartsWith("agents/"))  agents++;
                else if (f.StartsWith("scripts/")) scripts++;
            }
            return $"{skills} skills · {agents} agents · {scripts} scripts";
        }

        [MenuItem("MCP/Install AI Skills", priority = 3)]
        private static void OpenStandalone() =>
            EditorWindow.GetWindow<StandaloneWindow>("Install AI Skills").Show();

        private sealed class StandaloneWindow : EditorWindow
        {
            private InstallSkillsScreen _screen;

            private void OnEnable()
            {
                _screen = new InstallSkillsScreen(Close, Close);
                rootVisualElement.Add(_screen.Build());
                _screen.OnEnter();
            }

            private void OnDisable() => _screen?.OnExit();
        }
    }
}
