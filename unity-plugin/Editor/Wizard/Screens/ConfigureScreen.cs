using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Wizard page 3 — runs install.py configure for the selected backend.</summary>
    public sealed class ConfigureScreen : IWizardScreen
    {
        private readonly Action _onDone;
        private readonly Action _onBack;
        private BackendDescriptor _backend;
        private Label _logLabel;
        private Label _statusLabel;
        private ScrollView _logScroll;
        private Button _configureBtn;
        private Button _continueBtn;
        private readonly StringBuilder _log = new StringBuilder();
        private Process _proc;
        private VisualElement _root;
        private bool _readyToContinue;
        private int _runGeneration;

        public string Title => "Configure";

        public ConfigureScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public void SetBackend(BackendDescriptor backend) => _backend = backend;

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("wiz-container");
            _root.RegisterCallback<DetachFromPanelEvent>(_ => StopProcess());

            // Header: icon + name
            var header = new VisualElement();
            header.AddToClassList("wiz-screen-header");

            if (_backend != null)
            {
                var icon = new Label(_backend.Icon);
                icon.AddToClassList("wiz-screen-icon");

                var name = new Label(_backend.DisplayName);
                name.AddToClassList("wiz-title");

                header.Add(icon);
                header.Add(name);
            }
            else
            {
                var placeholder = new Label("No backend selected");
                placeholder.AddToClassList("wiz-title");
                header.Add(placeholder);
            }
            _root.Add(header);

            _statusLabel = BiomeUI.StatusLabel();
            _root.Add(_statusLabel);

            // Log area
            _logScroll = new ScrollView();
            _logScroll.AddToClassList("wiz-log");

            _logLabel = new Label();
            _logLabel.AddToClassList("wiz-log-label");
            _logScroll.Add(_logLabel);
            _root.Add(_logScroll);

            // Nav
            _configureBtn = WizardUI.Primary("Configure", RunConfigure);
            _continueBtn = WizardUI.Secondary("Continue →", _onDone);
            _continueBtn.SetEnabled(false);
            _root.Add(WizardUI.Navigation(
                WizardUI.Secondary("← Back", _onBack),
                _configureBtn,
                _continueBtn));
            return _root;
        }

        public void OnEnter()
        {
            _log.Clear();
            if (_logLabel != null) _logLabel.text = "";
            SetState(
                _backend == null ? "Choose a backend on the previous step." : "Ready to configure.",
                _backend == null ? "error" : "neutral",
                readyToContinue: false);
        }

        public void OnExit()
        {
            StopProcess();
        }

        private void StopProcess()
        {
            _runGeneration++;
            var process = _proc;
            _proc = null;
            if (process != null)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch { }
                try { process.Dispose(); } catch { }
            }
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RunConfigure()
        {
            if (_backend == null) return;
            SetState("Configuration in progress...", "warning", readyToContinue: false);

            if (_backend.AutoProjectConfig)
            {
                var autoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var relPath = ProjectConfigTargets.RelativePathFor(_backend.Key) ?? "";
                var path = Path.Combine(autoRoot, relPath);
                AppendLog($"✓ {_backend.DisplayName} is auto-configured per-project at {path}");
                AppendLog("Regenerates automatically on port/version change — no action needed.");
                SetState("Project configuration is ready.", "success", readyToContinue: true);
                return;
            }

            if (_backend.Mechanism == InstallMechanism.ManualInstructions)
            {
                AppendLog(_backend.Instructions);
                int manualPort = MCPServer.IsRunning ? MCPServer.ServerPort : 9500;
                var uvxCmd = $"UNITY_MCP_PORT={manualPort} uvx --from {WizardConfigWriter.GitInstallUrl} unity-biome-mcp";
                GUIUtility.systemCopyBuffer = uvxCmd;
                AppendLog("(uvx command copied to clipboard)");
                SetState("Instructions and command are ready.", "success", readyToContinue: true);
                return;
            }

            if (_backend.Mechanism == InstallMechanism.ChatAuto)
            {
                AppendLog($"✓ {_backend.DisplayName} is auto-configured at chat start — no extra steps needed.");
                SetState("Chat backend configuration is ready.", "success", readyToContinue: true);
                return;
            }

            if (_configureBtn != null) _configureBtn.SetEnabled(false);

            _log.Clear();
            AppendLog($"Configuring {_backend.DisplayName}...");

            var installPy = SetupDiagnostics.ResolveRepoRoot();
            if (installPy == null)
            {
                // UPM git/registry install: no install.py — show JSON for manual copy
                int port = MCPServer.IsRunning ? MCPServer.ServerPort : 9500;
                AppendLog("Installed via UPM. Copy the JSON config below and paste it into your AI tool's config file:");
                var (uvOk, uvHint) = SetupDiagnostics.CheckUv();
                if (!uvOk)
                {
                    AppendLog("");
                    AppendLog($"⚠ uvx not found. The config below requires uv.");
                    AppendLog($"  {uvHint}");
                    AppendLog("  Then restart Unity and click Configure again.");
                    AppendLog("");
                }
                AppendLog("");
                var json = WizardConfigWriter.Fresh(port);
                AppendLog(json);
                GUIUtility.systemCopyBuffer = json;
                AppendLog("(Copied to clipboard)");
                if (_configureBtn != null) _configureBtn.SetEnabled(true);
                SetState("Manual configuration copied to the clipboard.", "success", readyToContinue: true);
                return;
            }

            var pyPath = Path.Combine(installPy, "install.py");
#if UNITY_EDITOR_WIN
            string exe = "python";
#else
            string exe = "python3";
#endif
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var args = $"\"{pyPath}\" configure --tool {_backend.Key} --project-dir \"{projectRoot}\"";

            try
            {
                int generation = ++_runGeneration;
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
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
                        AppendLog(code == 0
                            ? $"✓ Done — restart {_backend.DisplayName} to activate"
                            : $"✗ Exit code {code}");
                        if (_configureBtn != null) _configureBtn.SetEnabled(true);
                        SetState(
                            code == 0 ? "Configuration completed." : $"Configuration failed with exit code {code}.",
                            code == 0 ? "success" : "error",
                            readyToContinue: code == 0);
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
                if (_configureBtn != null) _configureBtn.SetEnabled(true);
                SetState("Could not start configuration.", "error", readyToContinue: false);
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

        private void SetState(string message, string state, bool readyToContinue)
        {
            if (_statusLabel != null)
                BiomeUI.SetStatus(_statusLabel, message, state);
            _continueBtn?.SetEnabled(readyToContinue);
            if (readyToContinue && !_readyToContinue)
                BiomeParticleBurst.Emit(_root);
            _readyToContinue = readyToContinue;
        }
    }
}
