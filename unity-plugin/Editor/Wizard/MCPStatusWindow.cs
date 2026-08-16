using System;
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
        private VisualElement _serverListContainer;

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
                "Kill",
                MCPActions.KillCurrent,
                "Force-stop this server only",
                "mcp-btn--danger"));
            maintenanceRow.Add(MakeBtn(
                "Kill All",
                () => {
                    if (EditorUtility.DisplayDialog("Kill All MCP Servers",
                        "Stop ALL running MCP servers?", "Kill All", "Cancel"))
                        MCPActions.KillAll();
                },
                "Force-stop ALL MCP servers",
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
            BuildServerListSection(root);
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
                    var body = new Label(MarkdownInlineFormatter.ToRichText(entry.Content));
                    body.enableRichText = true;
                    body.style.fontSize = 10;
                    body.AddToClassList("updates-entry-body");
                    _changelogScroll.Add(body);
                }
            }
        }

        private void BuildServerListSection(VisualElement root)
        {
            var fold = new Foldout { text = "MCP Servers", value = true };
            _serverListContainer = new VisualElement();
            fold.Add(_serverListContainer);
            root.Add(fold);
            RefreshServerList();
        }

        private void RefreshServerList()
        {
            if (_serverListContainer == null) return;
            _serverListContainer.Clear();

            var servers = McpServerScanner.ScanDetailed();
            if (servers.Count == 0) { _serverListContainer.Add(new Label("No servers found")); return; }

            bool hasDead = false;
            foreach (var s in servers)
            {
                if (!HasAliveBridge(s)) hasDead = true;
                _serverListContainer.Add(BuildServerEntry(s));
            }

            if (hasDead)
            {
                var clean = new Button(() => { McpServerScanner.CleanPhantomFiles(); RefreshServerList(); })
                    { text = "Clean up", tooltip = "Remove stale port files for dead servers" };
                clean.AddToClassList("mcp-btn");
                clean.AddToClassList("mcp-btn--inline");
                _serverListContainer.Add(clean);
            }
        }

        private static bool HasAliveBridge(UnityServerInfo s)
        {
            foreach (var c in s.Connections)
                if (c.BridgeAlive) return true;
            return false;
        }

        private VisualElement BuildServerEntry(UnityServerInfo s)
        {
            var entry = new VisualElement();
            entry.AddToClassList("server-entry");
            entry.Add(BuildServerHeader(s));
            if (s.IsCurrentProject) entry.Add(BuildConnectionSection(s.Port));
            return entry;
        }

        private VisualElement BuildServerHeader(UnityServerInfo s)
        {
            var row = new VisualElement();
            row.AddToClassList("server-header");

            var portLabel = new Label($":{s.Port}");
            portLabel.AddToClassList("server-port");
            if (s.IsCurrentProject) portLabel.AddToClassList("server-port--this");
            row.Add(portLabel);

            bool alive = HasAliveBridge(s);
            var badge = new Label(alive ? "● alive" : "○ dead");
            badge.AddToClassList("server-status-badge");
            badge.style.color = alive ? new Color(0.3f, 0.8f, 0.3f) : new Color(0.8f, 0.3f, 0.3f);
            row.Add(badge);

            if (s.UnityPid > 0)
            {
                var pidLabel = new Label($"PID {s.UnityPid}");
                pidLabel.AddToClassList("server-pid");
                row.Add(pidLabel);
            }

            if (s.IsCurrentProject)
            {
                var marker = new Label("(this)");
                marker.AddToClassList("server-this-marker");
                row.Add(marker);
            }

            row.Add(new VisualElement { style = { flexGrow = 1 } });
            row.Add(BuildKillButton(s));
            return row;
        }

        private Button BuildKillButton(UnityServerInfo s)
        {
            int port = s.Port;
            int bridgeCount = s.Connections.Count;
            bool isCurrent = s.IsCurrentProject;

            // No known bridge lock files — Kill would be a no-op. Show disabled button
            // so the user knows kill is unavailable; use the "Clean up" button instead.
            if (bridgeCount == 0)
            {
                var dead = new Button { text = "Kill", tooltip = "No bridge process found; use Clean up" };
                dead.SetEnabled(false);
                dead.AddToClassList("mcp-btn");
                dead.AddToClassList("mcp-btn--danger");
                dead.AddToClassList("mcp-btn--inline");
                return dead;
            }

            int firstPid = s.Connections[0].BridgePid;
            var btn = new Button(() =>
            {
                if (bridgeCount > 1)
                {
                    if (!EditorUtility.DisplayDialog("Stop All Bridges",
                        $"Port :{port} has {bridgeCount} bridges running.\nStop ALL of them?",
                        "Stop All", "Cancel")) return;
                    MCPActions.StopAllOnPort(port);
                }
                else
                {
                    if (isCurrent && !EditorUtility.DisplayDialog("Kill Current Server?",
                        $"Stop MCP server on :{port}?\nClaude will disconnect.",
                        "Kill", "Cancel")) return;
                    MCPActions.TerminateByPid(port, firstPid);
                }
                RefreshServerList();
            }) { text = bridgeCount > 1 ? $"Kill ({bridgeCount})" : "Kill" };

            btn.AddToClassList("mcp-btn");
            btn.AddToClassList("mcp-btn--danger");
            btn.AddToClassList("mcp-btn--inline");
            return btn;
        }

        private VisualElement BuildConnectionSection(int port)
        {
            var section = new VisualElement();
            section.AddToClassList("connections-section");

            var snapshots = MCPServer._mainSlot.GetActiveSnapshots();
            var activePids = ExtractActivePids(snapshots);

            if (snapshots.Length > 0)
            {
                var count = new Label($"{snapshots.Length} connection(s)");
                count.AddToClassList("connections-count");
                section.Add(count);
                foreach (var snap in snapshots)
                    section.Add(BuildConnectionRow(snap));
            }

            var dormant = DormantBridgeScanner.Scan(port, activePids);
            if (dormant.Count > 0)
            {
                var ds = new VisualElement();
                ds.AddToClassList("dormant-section");
                var dh = new Label("Dormant bridges:");
                dh.AddToClassList("dormant-header");
                ds.Add(dh);
                foreach (var d in dormant)
                    ds.Add(BuildDormantRow(port, d));
                section.Add(ds);
            }

            return section;
        }

        private static List<int> ExtractActivePids(ConnectionSnapshot[] snapshots)
        {
            var pids = new List<int>();
            foreach (var snap in snapshots)
                if (snap.BridgePid > 0) pids.Add(snap.BridgePid);
            return pids;
        }

        private VisualElement BuildConnectionRow(ConnectionSnapshot snap)
        {
            var row = new VisualElement();
            row.AddToClassList("connection-row");

            var kind = new Label(string.IsNullOrEmpty(snap.Label) ? "unknown" : snap.Label);
            kind.AddToClassList("conn-kind");
            row.Add(kind);

            var stateName = snap.State.ToString().ToLowerInvariant();
            var stateBadge = new Label(snap.State.ToString());
            stateBadge.AddToClassList("conn-state");
            stateBadge.AddToClassList($"conn-state--{stateName}");
            row.Add(stateBadge);

            if (snap.LastUsefulAt > DateTime.MinValue)
            {
                var idle = new Label(FormatDuration(DateTime.UtcNow - snap.LastUsefulAt));
                idle.AddToClassList("conn-idle");
                row.Add(idle);
            }

            var dur = new Label(FormatDuration(DateTime.UtcNow - snap.ConnectedAt));
            dur.AddToClassList("conn-duration");
            row.Add(dur);

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            int idx = snap.Index; long gen = snap.Generation;
            bool isActive = snap.State == ClientActivityState.Active;
            var discBtn = new Button(() =>
            {
                if (isActive && !EditorUtility.DisplayDialog("Disconnect Active Connection",
                    "Disconnect this connection?\nThe bridge will reconnect.", "Disconnect", "Cancel"))
                    return;
                MCPServer._mainSlot.DisconnectEntry(idx, gen);
                RefreshServerList();
            }) { text = "Disconnect" };
            discBtn.AddToClassList("mcp-btn");
            discBtn.AddToClassList("mcp-btn--inline");
            row.Add(discBtn);

            return row;
        }

        private VisualElement BuildDormantRow(int port, DormantInfo d)
        {
            var row = new VisualElement();
            row.AddToClassList("connection-row");

            var pidLabel = new Label($"PID {d.BridgePid}");
            pidLabel.AddToClassList("conn-kind");
            row.Add(pidLabel);

            var state = new Label("Dormant");
            state.AddToClassList("conn-state");
            state.AddToClassList("conn-state--dormant");
            row.Add(state);

            row.Add(new VisualElement { style = { flexGrow = 1 } });

            int bridgePid = d.BridgePid;
            var termBtn = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("Terminate Dormant Bridge",
                    $"Kill bridge process PID {bridgePid}?", "Terminate", "Cancel")) return;
                MCPActions.TerminateByPid(port, bridgePid);
                RefreshServerList();
            }) { text = "Terminate" };
            termBtn.AddToClassList("mcp-btn");
            termBtn.AddToClassList("mcp-btn--danger");
            termBtn.AddToClassList("mcp-btn--inline");
            row.Add(termBtn);

            return row;
        }

        private static string FormatDuration(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
            return $"{ts.Minutes:00}:{ts.Seconds:00}";
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

            foreach (var k in new[] { "up", "listen", "down", "chat", "error" })
            {
                _orb.RemoveFromClassList("orb--" + k);
                _halo.RemoveFromClassList("halo--" + k);
                _word.RemoveFromClassList("status-word--" + k);
            }

            _orb.AddToClassList("orb--" + s);
            _halo.AddToClassList("halo--" + s);
            _word.AddToClassList("status-word--" + s);
            _statusParticles?.SetState(s);

            var sub = MCPServer.CurrentSubState;
            bool isErrorSub = sub is MCPStatusModel.SubState.CompileFailed
                                   or MCPStatusModel.SubState.BindFailed;
            if (isErrorSub && state != MCPStatusModel.State.Down)
            {
                _orb.AddToClassList("orb--error");
                _halo.AddToClassList("halo--error");
                _word.AddToClassList("status-word--error");
            }

            _word.text = MCPStatusModel.GetLabel(state, sub, MCPServer.ServerPort);
            _sub.text  = MCPStatusModel.GetSub(state, sub, MCPServer.CompileElapsedSeconds);

            if (EditorPrefs.GetBool(PrefKeys.ShowLastCommand, true)
                && !string.IsNullOrEmpty(CommandRouter.LastCommandName)
                && MCPServer.IsClientConnected)
            {
                _sub.text += $"\n↳ {CommandRouter.LastCommandName}";
            }

            RefreshServerList();
        }
    }
}
