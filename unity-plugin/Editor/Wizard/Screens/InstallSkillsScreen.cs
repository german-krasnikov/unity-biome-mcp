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
        private Button  _finishBtn;
        private Button  _skipBtn;
        private Label   _statusLabel;
        private ScrollView _logScroll;
        private SkillsInstallAnim _headerAnim;
        private readonly StringBuilder _log = new StringBuilder();
        private Process _proc;
        private VisualElement _root;
        private int _runGeneration;
        private string _pendingVersion;

        public string Title => "Install AI Skills";

        public InstallSkillsScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("wiz-container");
            _root.RegisterCallback<DetachFromPanelEvent>(_ => StopProcess());

            var title = new Label("Install AI Skills");
            title.AddToClassList("wiz-title");
            _root.Add(title);

            var subtitle = new Label(GetSubtitle());
            subtitle.AddToClassList("wiz-subtitle");
            _root.Add(subtitle);

            _headerAnim = new SkillsInstallAnim();
            _root.Add(_headerAnim);

            _statusLabel = BiomeUI.StatusLabel();
            _root.Add(_statusLabel);

            _logScroll = new ScrollView();
            _logScroll.AddToClassList("wiz-log");
            _logLabel = new Label();
            _logLabel.AddToClassList("wiz-log-label");
            _logScroll.Add(_logLabel);
            _root.Add(_logScroll);

            _overwriteToggle = new Toggle("Overwrite existing files") { value = false };
            _overwriteToggle.AddToClassList("wiz-form-field");
            _root.Add(_overwriteToggle);

            var projectRoot = ProjectRoot();
            _codexToggle = new Toggle("Run Codex sync after install")
                { value = SkillsInstaller.HasCodexDir(projectRoot) };
            _codexToggle.AddToClassList("wiz-form-field");
            _root.Add(_codexToggle);

            var spacer = new VisualElement();
            spacer.AddToClassList("wiz-spacer");
            _root.Add(spacer);

            _installBtn = WizardUI.Primary("Install", RunInstall);
            _finishBtn = WizardUI.Secondary("Finish", _onDone);
            _finishBtn.SetEnabled(false);
            _finishBtn.style.display = DisplayStyle.None;
            _skipBtn = WizardUI.Quiet("Skip skills", _onDone);
            _root.Add(WizardUI.Navigation(
                WizardUI.Secondary("← Back", _onBack),
                _skipBtn,
                _installBtn,
                _finishBtn));

            return _root;
        }

        public void OnEnter()
        {
            _headerAnim?.SetWorking(false);
            _log.Clear();
            if (_logLabel != null) _logLabel.text = "";

            var ver = SkillsInstaller.ReadVersionFile(ProjectRoot());
            if (ver != null)
            {
                AppendLog($"Previously installed: v{ver}");
                SetCompletionActions(true, allowReinstall: true);
                SetStatus($"Skills v{ver} are already installed.", "success");
            }
            else
            {
                SetCompletionActions(false);
                SetStatus("Ready to install project-local AI skills.", "neutral");
            }

            if (SkillsInstaller.FindSource() == null)
            {
                AppendLog("⚠ ClientSkills not found in package.");
                _installBtn?.SetEnabled(false);
                SetStatus("ClientSkills were not found. You can skip this step.", "error");
            }
        }

        public void OnExit()
        {
            StopProcess();
        }

        private void StopProcess()
        {
            _runGeneration++;
            _headerAnim?.SetWorking(false);
            _skipBtn?.SetEnabled(true);
            var process = _proc;
            _proc = null;
            if (process == null) return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch { }
            try { process.Dispose(); } catch { }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RunInstall()
        {
            var src = SkillsInstaller.FindSource();
            if (src == null)
            {
                AppendLog("✗ ClientSkills directory not found.");
                SetStatus("ClientSkills directory not found.", "error");
                _headerAnim?.SetWorking(false);
                return;
            }

            var projectRoot = ProjectRoot();
            _headerAnim?.SetWorking(true);
            _installBtn?.SetEnabled(false);
            _finishBtn?.SetEnabled(false);
            _skipBtn?.SetEnabled(false);
            SetStatus("Installing skills...", "warning");

            var previousVersion = SkillsInstaller.ReadVersionFile(projectRoot);
            if (previousVersion != null && !ClearVersionMarker(projectRoot))
            {
                _headerAnim?.SetWorking(false);
                _installBtn?.SetEnabled(true);
                _skipBtn?.SetEnabled(true);
                SetCompletionActions(false);
                SetStatus("The previous version marker could not be cleared.", "error");
                return;
            }

            var result = SkillsInstaller.Install(src, projectRoot, _overwriteToggle.value);
            AppendLog($"✓ Copied {result.Copied}, unchanged {result.Skipped}, removed {result.Removed}");
            foreach (var e in result.Errors) AppendLog("✗ " + e);

            if (result.IsSuccess)
                _pendingVersion =
                    UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SkillsInstaller).Assembly)?.version
                    ?? "?";
            else
                _pendingVersion = null;

            _installBtn?.SetEnabled(true);
            _skipBtn?.SetEnabled(true);

            if (!result.IsSuccess)
            {
                if (previousVersion != null && result.StateRestored)
                {
                    try
                    {
                        SkillsInstaller.WriteVersionFile(projectRoot, previousVersion);
                    }
                    catch (Exception ex)
                    {
                        AppendLog("ERROR: Could not restore the previous version marker: " + ex.Message);
                    }
                }
                else if (!result.StateRestored)
                {
                    AppendLog(
                        "ERROR: The previous marker was not restored because rollback was incomplete.");
                    if (!string.IsNullOrEmpty(result.RecoveryPath))
                        AppendLog("Recovery files: " + result.RecoveryPath);
                }
                _headerAnim?.SetWorking(false);
                SetCompletionActions(false);
                SetStatus(
                    result.StateRestored
                        ? "Skills installation completed with errors."
                        : "Skills installation requires manual recovery.",
                    "error");
                return;
            }

            BiomeParticleBurst.Emit(_root);
            if (_codexToggle?.value == true)
            {
                SetStatus("Skills installed. Running Codex sync...", "warning");
                RunCodexSync(projectRoot);
            }
            else
            {
                if (!CompleteVersionWrite(projectRoot))
                {
                    _headerAnim?.SetWorking(false);
                    SetCompletionActions(false);
                    SetStatus("Skills installed, but the version marker could not be written.", "error");
                    return;
                }
                _headerAnim?.SetWorking(false);
                SetCompletionActions(true);
                SetStatus("Skills installed successfully.", "success");
            }
        }

        private void RunCodexSync(string projectRoot)
        {
            var script = Path.Combine(projectRoot, ".codex", "scripts", "claude_to_codex.py");
            if (!File.Exists(script))
            {
                _headerAnim?.SetWorking(false);
                SetCompletionActions(false);
                SetStatus("Codex sync script was not found; installation is incomplete.", "error");
                return;
            }

#if UNITY_EDITOR_WIN
            const string exe = "python";
#else
            const string exe = "python3";
#endif
            try
            {
                int generation = ++_runGeneration;
                var psi = new ProcessStartInfo(exe, $"\"{script}\" --repo-root \"{projectRoot}\" --prune")
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
                    QueueIfActive(generation, () => AppendLog(line));
                };
                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    var line = e.Data;
                    QueueIfActive(generation, () => AppendLog("ERR: " + line));
                };
                proc.Exited += (_, __) =>
                {
                    int code;
                    try { code = proc.ExitCode; }
                    catch { return; }
                    QueueIfActive(generation, () =>
                    {
                        AppendLog(code == 0 ? "✓ Codex sync done" : $"✗ Codex exit {code}");
                        var ready = code == 0 && CompleteVersionWrite(projectRoot);
                        _headerAnim?.SetWorking(false);
                        _skipBtn?.SetEnabled(true);
                        SetCompletionActions(ready);
                        SetStatus(
                            ready ? "Skills and Codex sync are ready." :
                                code == 0 ? "Version marker write failed." : "Codex sync failed.",
                            ready ? "success" : "error");
                        ReleaseProcess(proc);
                    });
                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                StopProcess();
                AppendLog("ERROR: " + ex.Message);
                SetCompletionActions(false);
                SetStatus("Could not start Codex sync.", "error");
            }
        }

        private bool CompleteVersionWrite(string projectRoot)
        {
            if (_pendingVersion == null) return false;
            try
            {
                SkillsInstaller.WriteVersionFile(projectRoot, _pendingVersion);
                _pendingVersion = null;
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                return false;
            }
        }

        private bool ClearVersionMarker(string projectRoot)
        {
            try
            {
                SkillsInstaller.DeleteVersionFile(projectRoot);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                return false;
            }
        }

        private void SetCompletionActions(bool ready, bool allowReinstall = false)
        {
            if (_finishBtn != null)
            {
                _finishBtn.SetEnabled(ready);
                _finishBtn.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (_skipBtn != null)
                _skipBtn.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
            if (_installBtn != null)
            {
                _installBtn.text = allowReinstall ? "Reinstall" : "Install";
                _installBtn.style.display =
                    !ready || allowReinstall ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void QueueIfActive(int generation, Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (generation != _runGeneration || _root?.panel == null)
                    return;
                action();
            };
        }

        private void ReleaseProcess(Process process)
        {
            if (!ReferenceEquals(_proc, process)) return;
            _proc = null;
            try { process.Dispose(); } catch { }
        }

        private void AppendLog(string line)
        {
            _log.AppendLine(line);
            if (_logLabel != null) _logLabel.text = _log.ToString();
            _logScroll?.schedule.Execute(() =>
            {
                if (_logLabel?.panel != null)
                    _logScroll.ScrollTo(_logLabel);
            });
        }

        private void SetStatus(string text, string state)
        {
            if (_statusLabel != null)
                BiomeUI.SetStatus(_statusLabel, text, state);
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
                if (f.StartsWith("skills/") &&
                    (f.EndsWith("/SKILL.md") ||
                     f.IndexOf('/', "skills/".Length) < 0 && f.EndsWith(".md")))
                    skills++;
                else if (f.StartsWith("agents/") && f.EndsWith(".md"))
                    agents++;
                else if (f.StartsWith("scripts/") && f.EndsWith(".py"))
                    scripts++;
            }
            return $"{skills} skills · {agents} agents · {scripts} scripts";
        }

        [MenuItem("MCP/Install AI Skills", priority = 3)]
        private static void OpenStandalone()
        {
            var window = EditorWindow.GetWindow<StandaloneWindow>("Install AI Skills");
            window.minSize = new Vector2(440, 500);
            window.Show();
        }

        private sealed class StandaloneWindow : EditorWindow
        {
            private InstallSkillsScreen _screen;

            private void CreateGUI()
            {
                rootVisualElement.Clear();
                BiomeUI.LoadCoreStyles(rootVisualElement, includeWizard: true);
                _screen = new InstallSkillsScreen(Close, Close);
                rootVisualElement.Add(_screen.Build());
                _screen.OnEnter();
            }

            private void OnDisable() => _screen?.OnExit();
        }
    }
}
