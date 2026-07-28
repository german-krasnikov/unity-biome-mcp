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
        private Button _configureBtn;
        private readonly StringBuilder _log = new StringBuilder();
        private Process _proc;

        public string Title => "Configure";

        public ConfigureScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public void SetBackend(BackendDescriptor backend) => _backend = backend;

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            // Header: icon + name
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;

            if (_backend != null)
            {
                var icon = new Label(_backend.Icon);
                icon.style.fontSize = 22;
                icon.style.marginRight = 8;

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
            root.Add(header);

            // Log area
            var logScroll = new ScrollView();
            logScroll.AddToClassList("wiz-log");
            logScroll.style.flexGrow = 1;
            logScroll.style.minHeight = 120;

            _logLabel = new Label();
            _logLabel.style.fontSize = 11;
            _logLabel.style.whiteSpace = WhiteSpace.Normal;
            _logLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            logScroll.Add(_logLabel);
            root.Add(logScroll);

            // Nav
            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });

            _configureBtn = new Button(RunConfigure) { text = "Configure" };
            _configureBtn.AddToClassList("wiz-btn-primary");
            nav.Add(_configureBtn);

            var doneBtn = new Button(_onDone) { text = "Done ✓" };
            nav.Add(doneBtn);

            root.Add(nav);
            return root;
        }

        public void OnEnter()
        {
            _log.Clear();
            if (_logLabel != null) _logLabel.text = "";
        }

        public void OnExit()
        {
            if (_proc != null && !_proc.HasExited)
            {
                try { _proc.Kill(); } catch { }
            }
            try { _proc?.Dispose(); } catch { }
            _proc = null;
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void RunConfigure()
        {
            if (_backend == null) return;

            if (_backend.AutoProjectConfig)
            {
                var autoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var relPath = ProjectConfigTargets.RelativePathFor(_backend.Key) ?? "";
                var path = Path.Combine(autoRoot, relPath);
                AppendLog($"✓ {_backend.DisplayName} is auto-configured per-project at {path}");
                AppendLog("Regenerates automatically on port/version change — no action needed.");
                return;
            }

            if (_backend.Mechanism == InstallMechanism.ManualInstructions)
            {
                AppendLog(_backend.Instructions);
                int manualPort = MCPServer.IsRunning ? MCPServer.ServerPort : 9500;
                var uvxCmd = $"UNITY_MCP_PORT={manualPort} uvx --from {WizardConfigWriter.GitInstallUrl} unity-biome-mcp";
                GUIUtility.systemCopyBuffer = uvxCmd;
                AppendLog("(uvx command copied to clipboard)");
                return;
            }

            if (_backend.Mechanism == InstallMechanism.ChatAuto)
            {
                AppendLog($"✓ {_backend.DisplayName} is auto-configured at chat start — no extra steps needed.");
                if (_configureBtn != null) _configureBtn.SetEnabled(true);
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
                    {
                        AppendLog(code == 0
                            ? $"✓ Done — restart {_backend.DisplayName} to activate"
                            : $"✗ Exit code {code}");
                        if (_configureBtn != null) _configureBtn.SetEnabled(true);
                    };
                };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLog("ERROR: " + ex.Message);
                if (_configureBtn != null) _configureBtn.SetEnabled(true);
            }
        }

        private void AppendLog(string line)
        {
            _log.AppendLine(line);
            if (_logLabel != null) _logLabel.text = _log.ToString();
        }
    }
}
