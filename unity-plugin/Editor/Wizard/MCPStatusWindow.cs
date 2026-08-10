using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Scripting.APIUpdating;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor
{
    [MovedFrom(autoUpdateAPI: true, sourceNamespace: "UnityMCP.Editor", sourceAssembly: "UnityMCP.Editor")]
    public class MCPStatusWindow : EditorWindow
    {
        private VisualElement _orb, _halo;
        private Label         _word, _sub;
        private Label         _updateLabel;
        private ScrollView    _changelogScroll;
        private bool          _changelogLoaded;
        private IVisualElementScheduledItem _refreshJob;
        private BiomeAmbientParticles _statusParticles;

        [MenuItem("🧬MCP/Status", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPStatusWindow>($"{BiomeLabel.DisplayName} Status");
            window.minSize = new Vector2(240, 320);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            BiomeUI.LoadCoreStyles(root);
            var ss = MCPEditorUtils.LoadStyleSheet("MCPStatus.uss");
            if (ss != null && !root.styleSheets.Contains(ss))
                root.styleSheets.Add(ss);
            root.AddToClassList("mcp-root");

            var brand = new Label("UNITY BIOME MCP");
            brand.AddToClassList("brand");

            var stage = new VisualElement();
            stage.AddToClassList("orb-stage");
            stage.Add(StatusAmbientAnim.Build(root));
            _statusParticles = BiomeAmbientParticles.Attach(
                stage,
                BiomeParticlePattern.Ecosystem);
            _halo = new VisualElement();
            _halo.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _halo.AddToClassList("orb-halo");
            _orb  = new VisualElement();
            _orb.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _orb.AddToClassList("orb");
            stage.Add(_halo);
            stage.Add(_orb);

            _word = new Label(); _word.AddToClassList("status-word");
            _sub  = new Label(); _sub.AddToClassList("status-sub");

            var spacerTop = new VisualElement(); spacerTop.style.flexGrow = 1;
            var spacerBot = new VisualElement(); spacerBot.style.flexGrow = 1;

            var row = new VisualElement(); row.AddToClassList("btn-row");
            row.Add(MakeBtn(
                "Restart",
                MCPActions.Restart,
                "Restart the Biome server",
                "mcp-btn--primary"));
            row.Add(MakeBtn(
                "Diagnose",
                OpenDiagnosePanel,
                "Inspect server, transport, and project health"));

            var row2 = new VisualElement(); row2.AddToClassList("btn-row");
            row2.Add(MakeBtn(
                "Setup Wizard",
                SetupWizard.ShowWindow,
                "Review Biome setup"));
            row2.Add(MakeBtn(
                "Check for Updates",
                OnCheckUpdates,
                "Check the installed package version"));

            var maintenance = new Foldout { text = "Maintenance", value = false };
            maintenance.AddToClassList("mcp-maintenance");
            var maintenanceRow = new VisualElement();
            maintenanceRow.AddToClassList("btn-row");
            maintenanceRow.Add(MakeBtn(
                "Reimport",
                MCPActions.Reimport,
                "Reimport the Biome editor package"));
            maintenanceRow.Add(MakeBtn(
                "Kill Biome",
                MCPActions.Kill,
                "Force-stop the Biome server",
                "mcp-btn--danger"));
            maintenance.Add(maintenanceRow);

            _updateLabel = new Label();
            _updateLabel.style.fontSize = 10;
            _updateLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _updateLabel.style.marginTop = 2;

            // Changelog foldout
            var changelogFold = new Foldout { text = "Changelog", value = false };
            changelogFold.style.marginTop = 4;
            changelogFold.RegisterValueChangedCallback(evt => { if (evt.newValue) EnsureChangelogLoaded(changelogFold); });

            _changelogScroll = new ScrollView();
            _changelogScroll.style.maxHeight = 180;
            changelogFold.Add(_changelogScroll);

            root.Add(brand);
            root.Add(spacerTop);
            root.Add(stage);
            root.Add(_word);
            root.Add(_sub);
            root.Add(spacerBot);
            root.Add(row);
            root.Add(row2);
            root.Add(maintenance);
            root.Add(_updateLabel);
            root.Add(changelogFold);

            RefreshState();
            RefreshUpdateLabel();
            _refreshJob  = root.schedule.Execute(RefreshState).Every(700);
            ArcadeAnim.SmoothLoop(stage, elapsed =>
            {
                bool connected = MCPServer.IsRunning && MCPServer.IsClientConnected;
                bool listening = MCPServer.IsRunning && !connected;
                float speed = connected ? 3.4f : listening ? 2.0f : 1.1f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * speed);
                float micro = 0.5f + 0.5f
                    * Mathf.Sin(elapsed * (speed * 0.43f) + 1.2f);

                float orbScale = 0.92f + pulse * (connected ? 0.18f : 0.10f);
                _orb.style.scale = new Scale(new Vector3(
                    orbScale,
                    orbScale,
                    1f));

                float haloScale = 0.88f + pulse * 0.25f + micro * 0.05f;
                _halo.style.scale = new Scale(new Vector3(
                    haloScale,
                    haloScale,
                    1f));
                _halo.style.opacity = 0.15f
                    + pulse * (connected ? 0.47f : listening ? 0.30f : 0.12f);
            });
        }

        private void OnCheckUpdates()
        {
            _updateLabel.text = "Checking…";
            UpdateChecker.ForceCheckAsync();
            // Delay label refresh to allow async response
            rootVisualElement.schedule.Execute(RefreshUpdateLabel).StartingIn(2000);
        }

        private void RefreshUpdateLabel()
        {
            _updateLabel.text = UpdateChecker.HasUpdate
                ? $"Update available: v{UpdateChecker.AvailableVersion}"
                : "";
        }

        private void EnsureChangelogLoaded(Foldout fold)
        {
            if (_changelogLoaded) return;
            _changelogLoaded = true;

            var path = ChangelogReader.LocatePath();
            if (path == null) { _changelogScroll.Add(new Label("CHANGELOG.md not found.")); return; }

            string ver;
            try { ver = (UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MCPStatusWindow).Assembly)?.version ?? "0.0.0").TrimStart('v'); }
            catch { ver = "0.0.0"; }

            List<ChangelogReader.Entry> entries;
            try   { entries = ChangelogReader.Parse(System.IO.File.ReadAllText(path), ver); }
            catch (System.Exception ex) { _changelogScroll.Add(new Label("Error: " + ex.Message)); return; }

            foreach (var entry in entries)
            {
                var header = new Label(entry.IsNewer ? $"★ v{entry.Version}  {entry.Date}" : $"v{entry.Version}  {entry.Date}");
                header.style.unityFontStyleAndWeight = entry.IsNewer ? FontStyle.Bold : FontStyle.Normal;
                header.style.marginTop = 6;
                _changelogScroll.Add(header);

                if (!string.IsNullOrEmpty(entry.Content))
                {
                    var body = new Label(entry.Content);
                    body.style.fontSize   = 10;
                    body.style.whiteSpace = WhiteSpace.Normal;
                    _changelogScroll.Add(body);
                }
            }
        }

        private static void OpenDiagnosePanel()
        {
            var win = GetWindow<MCPDiagnoseWindow>($"{BiomeLabel.DisplayName} Diagnose");
            win.minSize = new Vector2(300, 200);
            win.Show();
        }

        private Button MakeBtn(
            string text,
            System.Action action,
            string tooltip,
            string modifierClass = null)
        {
            var b = new Button(action)
            {
                text = text,
                tooltip = tooltip
            };
            b.AddToClassList("mcp-btn");
            if (!string.IsNullOrEmpty(modifierClass))
                b.AddToClassList(modifierClass);
            return b;
        }

        private void OnDisable()
        {
            _refreshJob?.Pause();
        }

        private void RefreshState()
        {
            bool run  = MCPServer.IsRunning;
            bool cli  = MCPServer.IsClientConnected;
            bool chat = ChatBackendProbe.IsChatBackendRunning();
            var state = MCPStatusModel.GetState(run, cli, chat);
            var s     = MCPStatusModel.GetCssKey(state);

            foreach (var k in new[] { "up", "listen", "down", "chat" })
            {
                _orb.RemoveFromClassList("orb--" + k);
                _halo.RemoveFromClassList("halo--" + k);
                _word.RemoveFromClassList("status-word--" + k);
            }

            _orb.AddToClassList("orb--" + s);
            _halo.AddToClassList("halo--" + s);
            _word.AddToClassList("status-word--" + s);
            _statusParticles?.SetState(s);

            _word.text = MCPStatusModel.GetLabel(state, MCPServer.ServerPort);
            _sub.text  = MCPStatusModel.GetSub(state);
        }
    }
}
