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
        private IVisualElementScheduledItem _beatFastJob;
        private IVisualElementScheduledItem _beatSoftJob;

        [MenuItem("MCP/Status", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPStatusWindow>("MCP Status");
            window.minSize = new Vector2(240, 320);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = MCPEditorUtils.LoadStyleSheet("MCPStatus.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("mcp-root");

            var brand = new Label("UNITY BIOME MCP");
            brand.AddToClassList("brand");

            var stage = new VisualElement();
            stage.AddToClassList("orb-stage");
            stage.Add(StatusAmbientAnim.Build(root));
            _halo = new VisualElement(); _halo.AddToClassList("orb-halo");
            _orb  = new VisualElement(); _orb.AddToClassList("orb");
            stage.Add(_halo);
            stage.Add(_orb);

            _word = new Label(); _word.AddToClassList("status-word");
            _sub  = new Label(); _sub.AddToClassList("status-sub");

            var spacerTop = new VisualElement(); spacerTop.style.flexGrow = 1;
            var spacerBot = new VisualElement(); spacerBot.style.flexGrow = 1;

            var row = new VisualElement(); row.AddToClassList("btn-row");
            row.Add(MakeBtn("Restart",      MCPActions.Restart));
            row.Add(MakeBtn("Kill MCP",     MCPActions.Kill));
            row.Add(MakeBtn("Reimport",     MCPActions.Reimport));
            row.Add(MakeBtn("Diagnose",     OpenDiagnosePanel));

            // Extra action row
            var row2 = new VisualElement(); row2.AddToClassList("btn-row");
            row2.Add(MakeBtn("Setup Wizard",      SetupWizard.ShowWindow));
            row2.Add(MakeBtn("Check for Updates", OnCheckUpdates));

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
            root.Add(_updateLabel);
            root.Add(changelogFold);

            RefreshState();
            RefreshUpdateLabel();
            _refreshJob  = root.schedule.Execute(RefreshState).Every(700);
            _beatFastJob = root.schedule.Execute(BeatFast).Every(900);
            _beatSoftJob = root.schedule.Execute(BeatSoft).Every(1500);
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
            var win = GetWindow<MCPDiagnoseWindow>("MCP Diagnose");
            win.minSize = new Vector2(300, 200);
            win.Show();
        }

        private Button MakeBtn(string t, System.Action a)
        {
            var b = new Button(a) { text = t };
            b.AddToClassList("mcp-btn");
            return b;
        }

        private void OnDisable()
        {
            _refreshJob?.Pause();
            _beatFastJob?.Pause();
            _beatSoftJob?.Pause();
        }

        private void BeatFast()
        {
            if (MCPServer.IsRunning && MCPServer.IsClientConnected)
            {
                _halo.ToggleInClassList("beat");
                _orb.ToggleInClassList("beat");
            }
            else
            {
                _halo.RemoveFromClassList("beat");
                _orb.RemoveFromClassList("beat");
            }
        }

        private void BeatSoft()
        {
            if (MCPServer.IsRunning && !MCPServer.IsClientConnected)
                _halo.ToggleInClassList("beat-soft");
            else
                _halo.RemoveFromClassList("beat-soft");
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

            _word.text = MCPStatusModel.GetLabel(state, MCPServer.ServerPort);
            _sub.text  = MCPStatusModel.GetSub(state);
        }
    }
}
