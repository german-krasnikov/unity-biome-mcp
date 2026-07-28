// FlowBar partial: active-only Biome data stream with pooled particles.
// Also owns BuildFooterBar / MakeModeBtn (footer is tightly coupled to mode-toggle state).
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private ChatActivityState _activity = new ChatActivityState();
        private VisualElement _flowBar;
        private VisualElement _flowFill;
        private VisualElement _flowAura;
        private VisualElement[] _flowParticles;
        private ArcadeAnim.MotionHandle _flowMotion;
        private Button _sendBtn, _stopBtn;

        private VisualElement BuildFlowBar()
        {
            _flowBar = new VisualElement();
            _flowBar.pickingMode = PickingMode.Ignore;
            _flowBar.usageHints |= UsageHints.GroupTransform;
            _flowBar.AddToClassList("flowbar");

            _flowAura = new VisualElement();
            _flowAura.pickingMode = PickingMode.Ignore;
            _flowAura.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _flowAura.AddToClassList("flowbar__aura");

            _flowFill = new VisualElement();
            _flowFill.pickingMode = PickingMode.Ignore;
            _flowFill.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
            _flowFill.AddToClassList("flowbar__fill");
            _flowBar.Add(_flowAura);
            _flowBar.Add(_flowFill);

            var particleLayer = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            particleLayer.AddToClassList("flowbar__particles");
            _flowParticles = new VisualElement[7];
            for (int i = 0; i < _flowParticles.Length; i++)
            {
                var particle = new VisualElement
                {
                    pickingMode = PickingMode.Ignore
                };
                particle.usageHints |= UsageHints.DynamicTransform | UsageHints.DynamicColor;
                particle.AddToClassList("flowbar__particle");
                particle.AddToClassList(i % 3 == 0
                    ? "flowbar__particle--hot"
                    : "flowbar__particle--soft");
                particle.style.left = Length.Percent(8f + i * 14f);
                particle.style.top = Length.Percent(35f + (i % 3) * 14f);
                particleLayer.Add(particle);
                _flowParticles[i] = particle;
            }
            _flowBar.Add(particleLayer);
            _flowMotion = ArcadeAnim.ControlledSmoothLoop(_flowBar, AnimateFlowBar);
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
                        SetFlowBarActive(true);
                        break;
                    case ActivityPhase.Receiving:
                        SetFlowBarActive(false);
                        break;
                }
            }
            else
            {
                _flowBar.RemoveFromClassList("flowbar--active");
                _flowBar.RemoveFromClassList("flowbar--sending");
                _flowBar.RemoveFromClassList("flowbar--receiving");
                _flowFill.RemoveFromClassList("flowbar__fill--sending");
                _flowFill.RemoveFromClassList("flowbar__fill--receiving");
                _flowMotion?.SetActive(false);
                ResetFlowBar();
            }
            _inputArea?.EnableInClassList("input-area--working", active);
            // F20: swap Send ↔ Stop button visibility; treat askPending same as idle.
            if (_sendBtn != null && _stopBtn != null)
            {
                bool idle = !active;
                _sendBtn.style.display = idle ? DisplayStyle.Flex : DisplayStyle.None;
                _stopBtn.style.display = idle ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void SetFlowBarActive(bool sending)
        {
            _flowBar.AddToClassList("flowbar--active");
            _flowBar.EnableInClassList("flowbar--sending", sending);
            _flowBar.EnableInClassList("flowbar--receiving", !sending);
            _flowFill.EnableInClassList("flowbar__fill--sending", sending);
            _flowFill.EnableInClassList("flowbar__fill--receiving", !sending);
            _flowMotion?.SetActive(true);
        }

        private void AnimateFlowBar(float elapsed)
        {
            float width = _flowBar.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f)
                width = 1f;

            float fillWidth = _flowFill.resolvedStyle.width;
            if (float.IsNaN(fillWidth) || fillWidth <= 0f)
                fillWidth = width * 0.28f;

            float travel = Mathf.Max(0f, width - fillWidth);
            float yoyo = 0.5f - 0.5f * Mathf.Cos(elapsed * Mathf.PI * 1.08f);
            float x = travel * yoyo;
            float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 5.1f);
            float micro = 0.5f + 0.5f * Mathf.Sin(elapsed * 2.37f + 1.2f);

            _flowFill.style.translate = new Translate(x, 0f);
            _flowFill.style.scale = new Scale(new Vector3(
                0.96f + micro * 0.08f,
                0.82f + pulse * 0.34f,
                1f));
            _flowFill.style.opacity = 0.82f + pulse * 0.18f;

            _flowAura.style.translate = new Translate(x, 0f);
            _flowAura.style.scale = new Scale(new Vector3(
                1.04f + pulse * 0.16f,
                0.78f + micro * 0.42f,
                1f));
            _flowAura.style.opacity = 0.12f + pulse * 0.30f;

            float lead = yoyo * width;
            for (int i = 0; i < _flowParticles.Length; i++)
            {
                float baseX = width * (0.08f + i * 0.14f);
                float normalizedDistance = Mathf.Abs(baseX - lead) / Mathf.Max(width, 1f);
                float wake = Mathf.Exp(-normalizedDistance * normalizedDistance * 28f);
                float phase = elapsed * (2.2f + i * 0.13f) + i * 1.73f;
                float life = 0.5f + 0.5f * Mathf.Sin(phase);
                float driftX = Mathf.Sin(phase * 0.71f) * (1.5f + i * 0.2f);
                float driftY = Mathf.Cos(phase * 1.17f) * (1.5f + (i % 3));
                float scale = 0.52f + life * 0.38f + wake * 0.48f;

                var particle = _flowParticles[i];
                particle.style.translate = new Translate(driftX, driftY);
                particle.style.scale = new Scale(new Vector3(scale, scale, 1f));
                particle.style.opacity = 0.06f + life * 0.12f + wake * 0.74f;
            }
        }

        private void ResetFlowBar()
        {
            if (_flowFill != null)
            {
                _flowFill.style.translate = new Translate(0f, 0f);
                _flowFill.style.scale = new Scale(Vector3.one);
                _flowFill.style.opacity = 0f;
            }
            if (_flowAura != null)
            {
                _flowAura.style.translate = new Translate(0f, 0f);
                _flowAura.style.scale = new Scale(Vector3.one);
                _flowAura.style.opacity = 0f;
            }
            if (_flowParticles == null) return;
            foreach (var particle in _flowParticles)
                particle.style.opacity = 0f;
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
