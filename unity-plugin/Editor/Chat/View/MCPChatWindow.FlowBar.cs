// FlowBar partial: fixed track with inner chip sweep — fixes the ±100% glitch.
// Animation: track fades in/out; chip sweeps left↔right via CSS translate classes.
// Also owns BuildFooterBar / MakeModeBtn (footer is tightly coupled to mode-toggle state).
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private ChatActivityState _activity = new ChatActivityState();
        private VisualElement _flowBar;
        private VisualElement _flowFill;
        private bool _sweepPhase;
        private Button _sendBtn, _stopBtn;

        private VisualElement BuildFlowBar()
        {
            _flowBar = new VisualElement();
            _flowBar.AddToClassList("flowbar");
            _flowFill = new VisualElement();
            _flowFill.AddToClassList("flowbar__fill");
            _flowBar.Add(_flowFill);
            return _flowBar;
        }

        private void OnActivityChanged()
        {
            if (_flowBar == null) return; // defense-in-depth: guard against pre-CreateGUI calls
            bool active = !_askPending && _activity.Phase != ActivityPhase.Idle;
            if (active)
            {
                switch (_activity.Phase)
                {
                    case ActivityPhase.Sending:
                        SetFlowBarActive("flowbar__fill--sending", "flowbar__fill--receiving");
                        break;
                    case ActivityPhase.Receiving:
                        SetFlowBarActive("flowbar__fill--receiving", "flowbar__fill--sending");
                        break;
                }
            }
            else
            {
                _flowBar.RemoveFromClassList("flowbar--active");
                _flowFill.RemoveFromClassList("flowbar__fill--sending");
                _flowFill.RemoveFromClassList("flowbar__fill--receiving");
                _flowFill.RemoveFromClassList("flowbar__fill--a");
                _flowFill.RemoveFromClassList("flowbar__fill--b");
                _sweepPhase = false;
            }
            // F20: swap Send ↔ Stop button visibility; treat askPending same as idle.
            if (_sendBtn != null && _stopBtn != null)
            {
                bool idle = !active;
                _sendBtn.style.display = idle ? DisplayStyle.Flex : DisplayStyle.None;
                _stopBtn.style.display = idle ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void SetFlowBarActive(string addCls, string removeCls)
        {
            _flowBar.AddToClassList("flowbar--active");
            _flowFill.RemoveFromClassList(removeCls);
            _flowFill.AddToClassList(addCls);
            // Kick chip to --a immediately so motion starts now, not ~950ms later
            _flowFill.RemoveFromClassList("flowbar__fill--b");
            _flowFill.AddToClassList("flowbar__fill--a");
            _sweepPhase = false; // first tick flips to --b → chip sweeps --a→--b, no dead hold
        }

        private void TickFlowBarSweep()
        {
            if (_activity.Phase == ActivityPhase.Idle || _askPending) return;
            _sweepPhase = !_sweepPhase;
            _flowFill.EnableInClassList("flowbar__fill--a", !_sweepPhase);
            _flowFill.EnableInClassList("flowbar__fill--b",  _sweepPhase);
        }

        // ── Footer bar (moved here from MCPChatWindow.cs to stay under 200 lines) ─

        private VisualElement BuildFooterBar()
        {
            var bar = new VisualElement(); bar.AddToClassList("footer-bar");

            var sel = BuildAgentSelector();
            sel.AddToClassList("footer-selector");
            bar.Add(sel);

            var modelSel = BuildModelSelector();
            modelSel.AddToClassList("footer-selector");
            bar.Add(modelSel);

            var seg = new VisualElement(); seg.AddToClassList("mode-segment");
            _askBtn   = MakeModeBtn("Ask",   false);
            _agentBtn = MakeModeBtn("Agent", true);
            _agentBtn.AddToClassList("mode-toggle-btn--last");
            seg.Add(_askBtn); seg.Add(_agentBtn);
            bar.Add(seg);

            BuildPluginButtons(bar);

            var spacer = new VisualElement(); spacer.AddToClassList("footer-spacer");
            bar.Add(spacer);

            bar.Add(BuildSessionMenuButton());

            _tokenReadout = new Label(""); _tokenReadout.AddToClassList("token-readout");
            bar.Add(_tokenReadout);

            // Tier 2: relay cold-start status — hidden until RelaySpawnState.IsPending is true.
            _relayStatusLabel = new Label("");
            _relayStatusLabel.AddToClassList("token-readout");
            _relayStatusLabel.style.display = DisplayStyle.None;
            bar.Add(_relayStatusLabel);

            _contextBar = new ContextProgressBar();
            bar.Add(_contextBar);

            _sendBtn = new Button(OnSend) { text = "Send" };
            _sendBtn.AddToClassList("chat-btn"); _sendBtn.AddToClassList("chat-btn--send");
            _stopBtn = new Button(CancelTurn) { text = "Stop" };
            _stopBtn.AddToClassList("chat-btn"); _stopBtn.AddToClassList("chat-btn--stop");
            _stopBtn.style.display = DisplayStyle.None;
            bar.Add(_sendBtn); bar.Add(_stopBtn);
            return bar;
        }

        private Button MakeModeBtn(string label, bool isAgent)
        {
            var btn = new Button(() => SetMode(isAgent)) { text = label };
            btn.AddToClassList("mode-toggle-btn");
            if (_agentMode == isAgent) btn.AddToClassList("mode-toggle-btn--active");
            return btn;
        }

        private void OnAttachImage()
        {
            var path = EditorUtility.OpenFilePanelWithFilters(
                "Attach image", "", new[] { "Image files", "png,jpg,jpeg,gif,webp", "All files", "*" });
            if (!string.IsNullOrEmpty(path))
                ProcessExternalPath(path, InsertInlineChip);
        }

        private void OnCopyCliResume()
        {
            var projectDir = SessionScanner.ProjectDir();
            var cmd = SessionHandoff.GetResumeCommand(_selectedKind, _backend?.SessionId, projectDir);
            if (cmd == null) return;
            EditorGUIUtility.systemCopyBuffer = cmd;
            CopyFlash.Show();
        }

    }
}
